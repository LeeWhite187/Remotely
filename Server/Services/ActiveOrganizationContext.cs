using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Server.Models.Messages;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// Blazor Server circuit-scoped service. Holds the active organization ID and
/// membership state for a connected user. Implements <see cref="IActiveOrganizationContext"/>.
/// </summary>
/// <remarks>
/// Per spec §5.3 this is a UX convenience layer, not a security boundary.
/// Per FR-25 / KD-06 this MUST NOT be injected into HTTP controllers,
/// authorization handlers, background services, or non-circuit contexts.
/// SimpleMessenger channel is the user id (not the connection id) so that the
/// service stays independent of ICircuitConnection — this avoids a DI cycle
/// since ICircuitConnection consumers also need this context. See spec §5.3.
/// </remarks>
public sealed class ActiveOrganizationContext : IActiveOrganizationContext, IDisposable
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IAppDbFactory _appDbFactory;
    private readonly IMessenger _messenger;
    private readonly ILogger<ActiveOrganizationContext> _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private IDisposable? _messengerRegistration;

    private string? _userId;
    private bool _isServerAdmin;
    private string? _activeOrganizationId;
    private UserOrganizationMembership? _activeMembership;
    private List<Organization> _availableOrganizations = new();

    public ActiveOrganizationContext(
        AuthenticationStateProvider authStateProvider,
        IAppDbFactory appDbFactory,
        IMessenger messenger,
        ILogger<ActiveOrganizationContext> logger)
    {
        _authStateProvider = authStateProvider;
        _appDbFactory = appDbFactory;
        _messenger = messenger;
        _logger = logger;
    }

    /// <summary>
    /// Lazy registration: we don't know the user id until RefreshAsync runs.
    /// Called the first time we successfully resolve a user.
    /// </summary>
    private void EnsureMessengerRegistration(string userId)
    {
        if (_messengerRegistration is not null)
        {
            return;
        }
        _messengerRegistration = _messenger.Register<MembershipChangedMessage, string>(
            this,
            userId,
            HandleMembershipChangedMessage);
    }

    private static async Task HandleMembershipChangedMessage(object recipient, MembershipChangedMessage message)
    {
        var ctx = (ActiveOrganizationContext)recipient;
        if (!string.Equals(message.UserId, ctx._userId, StringComparison.Ordinal))
        {
            return;
        }
        try
        {
            await ctx.RefreshAsync();
        }
        catch (Exception ex)
        {
            ctx._logger.LogError(ex, "Failed to refresh membership state after MembershipChangedMessage.");
        }
    }

    public string? UserId => _userId;
    public string? ActiveOrganizationId => _activeOrganizationId;
    public UserOrganizationMembership? ActiveMembership => _activeMembership;
    public bool IsOrgAdmin => _isServerAdmin || _activeMembership?.IsAdministrator == true;
    public bool CanInvite => IsOrgAdmin || _activeMembership?.CanInvite == true;
    public IReadOnlyList<Organization> AvailableOrganizations => _availableOrganizations;

    public event Action? StateChanged;
    public event Action<string>? OrganizationRevoked;

    public async Task<bool> RefreshAsync()
    {
        await _stateLock.WaitAsync();
        string? revokedOrgName = null;
        try
        {
            var prevOrgId = _activeOrganizationId;
            var prevIsAdmin = _activeMembership?.IsAdministrator;
            var prevCanInvite = _activeMembership?.CanInvite;
            var prevAvailableCount = _availableOrganizations.Count;
            // Capture the previously-active org's name in case it's been revoked,
            // so FR-29 has a friendly string to show in the toast.
            var prevActiveOrgName = _availableOrganizations
                .FirstOrDefault(o => o.ID == prevOrgId)?.OrganizationName;

            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var userName = authState.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return ResetState(prevOrgId, prevIsAdmin, prevCanInvite, prevAvailableCount);
            }

            using var db = _appDbFactory.GetContext();

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == userName);

            if (user is null)
            {
                return ResetState(prevOrgId, prevIsAdmin, prevCanInvite, prevAvailableCount);
            }

            _userId = user.Id;
            _isServerAdmin = user.IsServerAdmin;
            EnsureMessengerRegistration(user.Id);

            if (_isServerAdmin)
            {
                _availableOrganizations = await db.Organizations
                    .AsNoTracking()
                    .OrderBy(o => o.OrganizationName)
                    .ToListAsync();
            }
            else
            {
                _availableOrganizations = await db.UserOrganizationMemberships
                    .AsNoTracking()
                    .Where(m => m.UserId == user.Id)
                    .Include(m => m.Organization)
                    .Select(m => m.Organization!)
                    .OrderBy(o => o.OrganizationName)
                    .ToListAsync();
            }

            if (_activeOrganizationId is not null &&
                _availableOrganizations.Any(o => o.ID == _activeOrganizationId))
            {
                _activeMembership = await db.UserOrganizationMemberships
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganizationId == _activeOrganizationId);
            }
            else if (_activeOrganizationId is not null && prevActiveOrgName is not null)
            {
                // FR-29 live revocation: the previously-active org is no longer
                // accessible. Do NOT silently switch — clear the active org and
                // raise OrganizationRevoked so the UI can prompt re-selection.
                revokedOrgName = prevActiveOrgName;
                _activeOrganizationId = null;
                _activeMembership = null;
            }
            else if (_activeOrganizationId is null && _availableOrganizations.Count > 0)
            {
                // First-time initialization (no previously-active org): adopt the
                // first available membership as the default. localStorage-driven
                // default is added in Phase 7 (OI-03).
                _activeOrganizationId = _availableOrganizations[0].ID;
                _activeMembership = await db.UserOrganizationMemberships
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganizationId == _activeOrganizationId);
            }
            else
            {
                _activeOrganizationId = null;
                _activeMembership = null;
            }

            var changed = prevOrgId != _activeOrganizationId
                       || prevIsAdmin != _activeMembership?.IsAdministrator
                       || prevCanInvite != _activeMembership?.CanInvite
                       || prevAvailableCount != _availableOrganizations.Count;

            if (changed)
            {
                StateChanged?.Invoke();
            }

            return changed;
        }
        finally
        {
            _stateLock.Release();
            // Fire the revocation event AFTER releasing the lock so subscribers
            // can call back into this context (e.g. AvailableOrganizations) without deadlocking.
            if (revokedOrgName is not null)
            {
                try { OrganizationRevoked?.Invoke(revokedOrgName); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OrganizationRevoked subscriber threw.");
                }
            }
        }
    }

    public async Task<bool> SetActiveOrganizationAsync(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return false;
        }

        await _stateLock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(_userId))
            {
                return false;
            }

            using var db = _appDbFactory.GetContext();

            UserOrganizationMembership? targetMembership = null;
            if (_isServerAdmin)
            {
                var orgExists = await db.Organizations
                    .AsNoTracking()
                    .AnyAsync(o => o.ID == organizationId);
                if (!orgExists)
                {
                    return false;
                }
                // Server admin may have a membership too; load it if present.
                targetMembership = await db.UserOrganizationMemberships
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.UserId == _userId && m.OrganizationId == organizationId);
            }
            else
            {
                targetMembership = await db.UserOrganizationMemberships
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.UserId == _userId && m.OrganizationId == organizationId);
                if (targetMembership is null)
                {
                    return false;
                }
            }

            _activeOrganizationId = organizationId;
            _activeMembership = targetMembership;

            StateChanged?.Invoke();
            return true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private bool ResetState(string? prevOrgId, bool? prevIsAdmin, bool? prevCanInvite, int prevAvailableCount)
    {
        _userId = null;
        _isServerAdmin = false;
        _activeOrganizationId = null;
        _activeMembership = null;
        _availableOrganizations = new();

        var changed = prevOrgId is not null
                   || prevIsAdmin is not null
                   || prevCanInvite is not null
                   || prevAvailableCount != 0;

        if (changed)
        {
            StateChanged?.Invoke();
        }

        return changed;
    }

    public void Dispose()
    {
        try
        {
            _messengerRegistration?.Dispose();
        }
        catch { /* best-effort */ }
        _stateLock.Dispose();
    }
}
