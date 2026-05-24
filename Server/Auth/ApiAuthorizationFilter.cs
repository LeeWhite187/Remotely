using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared;
using System.Net;

namespace Remotely.Server.Auth;

/// <summary>
/// Authorizes API endpoints by validating either the cookie-authenticated user
/// or an API key header. Per FR-28 / KD-05, API tokens are identity-only and do
/// NOT supply org context. Per KD-06, the target org is supplied as an explicit
/// <c>organizationId</c> route or query parameter, validated by downstream
/// authorization handlers (FR-30) and <see cref="IDataService"/> methods (FR-27).
/// </summary>
public class ApiAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IDataService _dataService;
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<ApiAuthorizationFilter> _logger;

    public ApiAuthorizationFilter(
        IDataService dataService,
        IAppDbFactory appDbFactory,
        ILogger<ApiAuthorizationFilter> logger)
    {
        _dataService = dataService;
        _appDbFactory = appDbFactory;
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
            _logger.LogError(ex, "Error while authorizing API request.");
        }
    }

    private async Task Authorize(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;

        // Cookie-authenticated user: let the request proceed. Per KD-06 the target
        // org is supplied per-request, and OrganizationAdminRequirementHandler /
        // DataService methods (NFR-01, FR-27) enforce org-scoped access.
        if (http.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        // Otherwise this must be an API token request.
        if (!http.Request.Headers.TryGetValue(AppConstants.ApiKeyHeaderName, out var apiHeaderValue))
        {
            Reject(context, "API key required.");
            return;
        }

        var headerComponents = apiHeaderValue.ToString().Split(":");
        if (headerComponents.Length < 2)
        {
            Reject(context, "API key malformed.");
            return;
        }

        var keyId = headerComponents[0].Trim();
        var secret = headerComponents[1].Trim();

        // FR-31: legacy token detection MUST occur before any other request processing.
        // A legacy token is one with a non-null OrganizationID (issued before this migration).
        var keyResult = await _dataService.GetApiKey(keyId);
        if (keyResult.IsSuccess && !string.IsNullOrWhiteSpace(keyResult.Value.OrganizationID))
        {
            await InvalidateLegacyToken(keyId);
            _logger.LogInformation(
                "Legacy API token {KeyId} detected and invalidated. Client must re-authenticate and generate a new token.",
                keyId);

            context.Result = new ObjectResult(new
            {
                error = "legacy_token_invalidated",
                message =
                    "This API token was issued under a previous version that bound tokens to a single " +
                    "organization. It has been invalidated. Log in and generate a new token. New tokens " +
                    "authenticate user identity only; supply the target organization id as the " +
                    "'organizationId' route or query parameter on each request."
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

        if (!isValid)
        {
            Reject(context, "API key invalid.");
            return;
        }

        // Token is valid and not legacy; proceed. No header is set: org context is
        // supplied per-request via the explicit organizationId parameter (KD-06).
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
            // Best-effort: even if the delete fails, we still 401 the request.
            _logger.LogWarning(ex, "Failed to remove legacy API token {TokenId} from the database.", tokenId);
        }
    }

    private static void Reject(AuthorizationFilterContext context, string reason)
    {
        context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Result = new UnauthorizedObjectResult(new { error = "unauthorized", message = reason });
    }
}
