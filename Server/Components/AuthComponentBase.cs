using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using System.Diagnostics.CodeAnalysis;

namespace Remotely.Server.Components;

[Authorize]
public class AuthComponentBase : MessengerSubscriber
{
    [Inject]
    protected IAuthService AuthService { get; set; } = null!;

    /// <summary>
    /// Per-circuit org context. Provides the active organization id and per-org
    /// role flags for the connected user. See spec §5.1 / §5.3.
    /// </summary>
    [Inject]
    protected IActiveOrganizationContext ActiveOrgContext { get; set; } = null!;

    protected RemotelyUser? User { get; private set; }

    protected string? UserName => User?.UserName;

    /// <summary>Active org id from the per-circuit context (empty if unset).</summary>
    protected string ActiveOrgId => ActiveOrgContext.ActiveOrganizationId ?? string.Empty;

    /// <summary>True if the user is an admin in the active org (or a server admin per FR-18).</summary>
    protected bool IsOrgAdmin => ActiveOrgContext.IsOrgAdmin;

    /// <summary>True if the user may invite new members in the active org (FR-21).</summary>
    protected bool CanInvite => ActiveOrgContext.CanInvite;

    [MemberNotNull(nameof(User), nameof(UserName))]
    protected void EnsureUserSet()
    {
        if (User is null)
        {
            throw new InvalidOperationException("User has not been set.");
        }

        if (UserName is null)
        {
            throw new InvalidOperationException("UserName has not been set.");
        }
    }

    protected override async Task OnInitializedAsync()
    {
        var userResult = await AuthService.GetUser();
        if (userResult.IsSuccess)
        {
            User = userResult.Value;
        }
        // Initialize per-circuit org context. Defaults to first available membership
        // (localStorage-driven default deferred to Phase 7 / OI-03).
        await ActiveOrgContext.RefreshAsync();
        await base.OnInitializedAsync();
    }
}
