using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared;
using Remotely.Shared.Utilities;
using System.Net;

namespace Remotely.Server.Auth;

/// <summary>
/// Authorizes endpoints that accept any of: cookie auth, API key, or a short-lived
/// expiring token. Aligned with <see cref="ApiAuthorizationFilter"/> on the API-key
/// path — legacy tokens (non-null <c>ApiToken.OrganizationID</c>) are invalidated
/// per FR-31. Per KD-06, org context is supplied per-request via the explicit
/// <c>organizationId</c> parameter; this filter does not populate any header.
/// </summary>
public class ExpiringTokenFilter : ActionFilterAttribute, IAsyncAuthorizationFilter
{
    private readonly IDataService _dataService;
    private readonly IAppDbFactory _appDbFactory;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<ExpiringTokenFilter> _logger;

    public ExpiringTokenFilter(
        IExpiringTokenService expiringTokenService,
        IDataService dataService,
        IAppDbFactory appDbFactory,
        ILogger<ExpiringTokenFilter> logger)
    {
        _dataService = dataService;
        _appDbFactory = appDbFactory;
        _expiringTokenService = expiringTokenService;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await Authorize(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while authorizing expiring token.");
        }
    }

    private async Task Authorize(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;

        // Cookie-authenticated user: proceed. Downstream handlers / DataService methods
        // enforce org-scoped access (NFR-01, FR-27).
        if (http.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        // API key path.
        if (http.Request.Headers.TryGetValue(AppConstants.ApiKeyHeaderName, out var apiHeaderValue))
        {
            var headerComponents = apiHeaderValue.ToString().Split(":");
            if (headerComponents.Length < 2)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var keyId = headerComponents[0].Trim();
            var secret = headerComponents[1].Trim();

            // FR-31: legacy token detection before any other processing.
            var keyResult = await _dataService.GetApiKey(keyId);
            if (keyResult.IsSuccess && !string.IsNullOrWhiteSpace(keyResult.Value.OrganizationID))
            {
                await InvalidateLegacyToken(keyId);
                _logger.LogInformation(
                    "Legacy API token {KeyId} detected and invalidated. Client must re-authenticate.",
                    keyId);
                context.Result = new ObjectResult(new
                {
                    error = "legacy_token_invalidated",
                    message = "This API token was bound to an organization and has been invalidated. " +
                              "Log in and generate a new token; supply organizationId per request."
                })
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized
                };
                return;
            }

            var isValid = await _dataService.ValidateApiKey(
                keyId,
                secret,
                http.Request.Path,
                $"{http.Connection.RemoteIpAddress}");

            if (isValid)
            {
                _logger.LogDebug("Expiring token endpoint authorized via API key. Key ID: {KeyId}.", keyId);
                return;
            }
        }

        // Expiring token path.
        if (http.Request.Headers.TryGetValue(AppConstants.ExpiringTokenHeaderName, out var expiringToken))
        {
            if (_expiringTokenService.TryGetExpiration(expiringToken.ToString(), out var expiration) &&
                expiration > Time.Now)
            {
                _logger.LogDebug("Expiring token authorized. Token: {Token}. Expiration: {Expiration}", expiringToken, expiration);
                return;
            }
        }

        _logger.LogDebug("Expiring token authorization failed.");
        context.Result = new UnauthorizedResult();
    }

    private async Task InvalidateLegacyToken(string tokenId)
    {
        try
        {
            using var db = _appDbFactory.GetContext();
            var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.ID == tokenId);
            if (token is not null)
            {
                db.ApiTokens.Remove(token);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove legacy API token {TokenId} from the database.", tokenId);
        }
    }
}
