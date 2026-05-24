using Microsoft.AspNetCore.Authorization;
using Remotely.Server.Services;

namespace Remotely.Server.Auth;

/// <summary>
/// Per spec FR-30: resolves the target organization id from the HTTP request
/// (route data preferred, then query string) and grants the policy only if the
/// authenticated user is an administrator of that org via
/// <see cref="UserOrganizationMembership"/>, or is a server admin (FR-18 / KD-02).
/// </summary>
/// <remarks>
/// This handler applies to HTTP pipeline authorization only. Blazor circuit
/// pages gate org-admin access through <see cref="IActiveOrganizationContext"/>
/// and <see cref="IDataService"/> method checks (NFR-01).
/// </remarks>
public class OrganizationAdminRequirementHandler : AuthorizationHandler<OrganizationAdminRequirement>
{
    private const string OrgIdParamName = "organizationId";

    private readonly IDataService _dataService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrganizationAdminRequirementHandler(
        IDataService dataService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dataService = dataService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationAdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(context.User.Identity.Name))
        {
            context.Fail();
            return;
        }

        var userResult = await _dataService.GetUserByName(context.User.Identity.Name);
        if (!userResult.IsSuccess)
        {
            context.Fail();
            return;
        }

        var user = userResult.Value;

        // KD-02 / FR-18: server admins have implicit org admin everywhere.
        if (user.IsServerAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        var orgId = ResolveOrgId();
        if (string.IsNullOrWhiteSpace(orgId))
        {
            // FR-30: if no organizationId is present in the request, deny access.
            context.Fail();
            return;
        }

        var membership = await _dataService.GetMembership(orgId, user.Id);
        if (membership?.IsAdministrator == true)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }

    /// <summary>
    /// Resolve organizationId per FR-30: route data takes precedence over query
    /// string; request body is not consulted.
    /// </summary>
    private string? ResolveOrgId()
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null)
        {
            return null;
        }

        if (http.Request.RouteValues.TryGetValue(OrgIdParamName, out var routeValue) &&
            routeValue is string routeStr &&
            !string.IsNullOrWhiteSpace(routeStr))
        {
            return routeStr;
        }

        if (http.Request.Query.TryGetValue(OrgIdParamName, out var queryValue) &&
            !string.IsNullOrWhiteSpace(queryValue))
        {
            return queryValue.ToString();
        }

        return null;
    }
}
