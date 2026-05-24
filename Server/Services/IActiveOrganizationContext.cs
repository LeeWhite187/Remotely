using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// Per-circuit scoped service holding the active organization context for a
/// Blazor Server session. Convenience layer for the UI; not a security boundary
/// (see spec §5.3). DataService methods must independently validate org access.
/// </summary>
public interface IActiveOrganizationContext
{
    /// <summary>The connected user's id, resolved on RefreshAsync. Null until first refresh.</summary>
    string? UserId { get; }

    /// <summary>The currently active organization ID. Null until initialized.</summary>
    string? ActiveOrganizationId { get; }

    /// <summary>
    /// The user's membership record for the active org. Null if the user is a
    /// server admin acting in an org without an explicit membership.
    /// </summary>
    UserOrganizationMembership? ActiveMembership { get; }

    /// <summary>
    /// True if the current user is an org admin in the active org, either via
    /// membership or via IsServerAdmin (FR-18).
    /// </summary>
    bool IsOrgAdmin { get; }

    /// <summary>True if the current user can invite new users in the active org.</summary>
    bool CanInvite { get; }

    /// <summary>All orgs available to this user in the switcher (FR-04/FR-05).</summary>
    IReadOnlyList<Organization> AvailableOrganizations { get; }

    /// <summary>
    /// Switch the active org. Validates membership or server admin status (FR-07).
    /// Returns false if the switch is rejected.
    /// </summary>
    Task<bool> SetActiveOrganizationAsync(string organizationId);

    /// <summary>
    /// Refresh membership state from the database. Called on circuit init and
    /// on receipt of <see cref="Models.Messages.MembershipChangedMessage"/>.
    /// Returns true if observable state changed (triggers toast in UI).
    /// </summary>
    Task<bool> RefreshAsync();

    /// <summary>Raised when active org or membership state changes.</summary>
    event Action? StateChanged;

    /// <summary>
    /// Per FR-29: raised when the user's previously-active organization is revoked
    /// (membership for that org deleted while the circuit is connected). Carries
    /// the name of the revoked org for the toast/UI notice. The UI MUST NOT
    /// silently switch to a different org — the user must re-select via the
    /// org switcher.
    /// </summary>
    event Action<string>? OrganizationRevoked;
}
