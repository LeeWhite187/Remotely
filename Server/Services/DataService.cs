using Remotely.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Bitbound.SimpleMessenger;
using Remotely.Server.Data;
using Remotely.Server.Extensions;
using Remotely.Server.Models;
using Remotely.Server.Models.Messages;
using Remotely.Shared;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Utilities;
using Remotely.Shared.ViewModels;
using System.Text.Json;

namespace Remotely.Server.Services;

// TODO: Separate this into domain-specific services.
public interface IDataService
{
    Task AddAlert(string deviceId, string organizationId, string alertMessage, string? details = null);

    Task<Result<DeviceGroup>> AddDeviceGroup(string orgId, DeviceGroup deviceGroup);
    Task<Result> AddDeviceToGroup(string deviceId, string groupId);
    Task<Result<InviteLink>> AddInvite(string orgId, InviteViewModel invite);

    Task<Result<Device>> AddOrUpdateDevice(DeviceClientDto device);

    Task<Result> AddOrUpdateSavedScript(SavedScript script, string userId, string organizationId);

    Task AddOrUpdateScriptSchedule(ScriptSchedule schedule);

    Task<Result<ScriptResult>> AddScriptResult(ScriptResultDto dto);
    Task<Result> AddScriptResultToScriptRun(string scriptResultId, int scriptRunId);

    Task AddScriptRun(ScriptRun scriptRun);

    Task<string> AddSharedFile(IBrowserFile file, string organizationId, Action<double, string> progressCallback);

    Task<string> AddSharedFile(IFormFile file, string organizationId);

    bool AddUserToDeviceGroup(string orgId, string groupId, string userName, out string resultMessage);

    // NEW (FR-08): direct-add an existing user to an org with base privileges.
    Task<Result> AddUserToOrganization(string orgId, string userId);

    Task CleanupOldRecords();

    // FR-28 / KD-05: token is identity-only; OrganizationID is set null.
    Task<Result<ApiToken>> CreateApiToken(string userName, string tokenName, string secretHash);

    Task<Result<Device>> CreateDevice(DeviceSetupOptions options);

    // NEW (FR-23): server-admin org creation.
    Task<Result<Organization>> CreateOrganization(string organizationName);

    Task<Result> CreateUser(string userEmail, bool isAdmin, string organizationId);

    Task DeleteAlert(Alert alert);

    Task DeleteAllAlerts(string orgId, string? userName = null);

    // FR-28: tokens belong to users; scope by userId only.
    Task<Result> DeleteApiToken(string userName, string tokenId);

    Task<Result> DeleteDeviceGroup(string orgId, string deviceGroupId);

    Task<Result> DeleteInvite(string orgId, string inviteId);

    Task DeleteSavedScript(Guid scriptId);

    Task DeleteScriptSchedule(int scriptScheduleId);

    Task<Result> DeleteUser(string orgId, string targetUserId);

    void DeviceDisconnected(string deviceId);

    bool DoesUserExist(string userName);

    bool DoesUserHaveAccessToDevice(string deviceId, RemotelyUser remotelyUser, string organizationId, bool isOrgAdmin);

    bool DoesUserHaveAccessToDevice(string deviceId, string remotelyUserId);

    string[] FilterDeviceIdsByUserPermission(string[] deviceIds, RemotelyUser remotelyUser);

    string[] FilterUsersByDevicePermission(IEnumerable<string> userIds, string deviceId);

    Task<Result<Alert>> GetAlert(string alertId);

    Alert[] GetAlerts(string userId);

    // FR-28: tokens are user-scoped; no org filter.
    ApiToken[] GetAllApiTokens(string userId);

    ScriptResult[] GetAllCommandResults(string orgId);

    ScriptResult[] GetAllCommandResultsForUser(string orgId, string userName, string deviceId);

    Device[] GetAllDevices(string orgId);

    InviteLink[] GetAllInviteLinks(string organizationId);

    // NEW (FR-05, FR-22): all orgs on the server.
    Task<IReadOnlyList<Organization>> GetAllOrganizations();

    ScriptResult[] GetAllScriptResults(string orgId, string deviceId);

    ScriptResult[] GetAllScriptResultsForUser(string orgId, string userName);

    RemotelyUser[] GetAllUsersForServer();

    Task<RemotelyUser[]> GetAllUsersInOrganization(string orgId);

    Task<Result<ApiToken>> GetApiKey(string keyId);

    Task<Result<BrandingInfo>> GetBrandingInfo(string organizationId);

    Task<Result<Organization>> GetDefaultOrganization();

    Task<Result<Device>> GetDevice(
          string deviceId,
          Action<IQueryable<Device>>? queryBuilder = null);

    Task<Result<Device>> GetDevice(string orgId, string deviceId);

    int GetDeviceCount();

    int GetDeviceCount(string organizationId);

    Task<Result<DeviceGroup>> GetDeviceGroup(
        string deviceGroupId,
        bool includeDevices = false,
        bool includeUsers = false);

    // §8.1: explicit organizationId and isOrgAdmin per FR-25.
    DeviceGroup[] GetDeviceGroups(string username, string organizationId, bool isOrgAdmin);

    DeviceGroup[] GetDeviceGroupsForOrganization(string organizationId);

    List<Device> GetDevices(IEnumerable<string> deviceIds);

    // §8.1: explicit organizationId per FR-25.
    Device[] GetDevicesForUser(string userName, string organizationId, bool isOrgAdmin);

    // NEW: utility for callers needing a single membership record.
    Task<UserOrganizationMembership?> GetMembership(string orgId, string userId);

    // NEW: used by IActiveOrganizationContext on circuit initialization.
    Task<IReadOnlyList<UserOrganizationMembership>> GetMembershipsForUser(string userId);

    Task<Result<Organization>> GetOrganizationById(string organizationId);

    int GetOrganizationCount();
    Task<int> GetOrganizationCountAsync();

    Task<Result<string>> GetOrganizationNameById(string organizationId);

    Task<IEnumerable<ScriptRun>> GetPendingScriptRuns(string deviceId);

    Task<List<SavedScript>> GetQuickScripts(string userId);

    Task<Result<SavedScript>> GetSavedScript(Guid scriptId);

    Task<Result<SavedScript>> GetSavedScript(string userId, Guid scriptId);

    Task<List<SavedScript>> GetSavedScriptsWithoutContent(string userId, string organizationId);

    Task<Result<ScriptResult>> GetScriptResult(string resultId);

    Task<Result<ScriptResult>> GetScriptResult(string resultId, string orgId);

    Task<List<ScriptSchedule>> GetScriptSchedules(string organizationId);

    Task<List<ScriptSchedule>> GetScriptSchedulesDue();

    List<string> GetServerAdmins();

    Task<SettingsModel> GetSettings();

    Task<Result<SharedFile>> GetSharedFiled(string fileId);

    int GetTotalDevices();

    Task<Result<RemotelyUser>> GetUserById(string userId);

    Task<Result<RemotelyUser>> GetUserByName(
        string userName,
        Action<IQueryable<RemotelyUser>>? queryBuilder = null);

    Task<Result<RemotelyUserOptions>> GetUserOptions(string userName);

    Task<Result> JoinViaInvitation(string userName, string inviteId);

    void RemoveDevices(string[] deviceIds);

    Task<bool> RemoveUserFromDeviceGroup(string orgId, string groupId, string userId);

    // NEW (FR-20): remove user from org with FR-15 + FR-24 enforcement.
    Task<Result> RemoveUserFromOrganization(string orgId, string userId);

    // FR-28: tokens belong to users; scope by userId only.
    Task<Result> RenameApiToken(string userName, string tokenId, string tokenName);

    Task ResetBranding(string organizationId);

    Task SaveSettings(SettingsModel settings);

    Task SetAllDevicesNotOnline();

    Task SetDisplayName(RemotelyUser user, string displayName);

    Task SetIsDefaultOrganization(string orgId, bool isDefault);

    Task SetIsServerAdmin(string targetUserId, bool isServerAdmin, string callerUserId);

    // NEW (FR-15): replaces ChangeUserIsAdmin; enforces last-admin lockout prevention.
    Task<Result> SetMemberIsAdmin(string orgId, string userId, bool isAdmin);

    // NEW (FR-21): grant/revoke invite privilege.
    Task<Result> SetMemberCanInvite(string orgId, string userId, bool canInvite);

    void SetServerVerificationToken(string deviceId, string verificationToken);

    Task<bool> TempPasswordSignIn(string email, string password);

    Task UpdateBrandingInfo(
        string organizationId,
        string productName,
        byte[] iconBytes);

    Task<Result<Device>> UpdateDevice(DeviceSetupOptions deviceOptions, string organizationId);

    Task UpdateDevice(string deviceId, string? tag, string? alias, string? deviceGroupId, string? notes);

    Task<Result> UpdateOrganizationName(string orgId, string newName);

    Task UpdateTags(string deviceID, string tags);

    Task<Result> UpdateUserOptions(string userName, RemotelyUserOptions options);
    Task<bool> ValidateApiKey(string keyId, string apiSecret, string requestPath, string remoteIP);
}

public class DataService : IDataService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DataService> _logger;
    private readonly IMessenger _messenger;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);

    public DataService(
        IHostEnvironment hostEnvironment,
        IAppDbFactory appDbFactory,
        IMessenger messenger,
        ILogger<DataService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _appDbFactory = appDbFactory;
        _messenger = messenger;
        _logger = logger;
    }

    /// <summary>
    /// Per spec FR-14 / §8.3: notify any connected circuits for the affected user
    /// that their membership state has changed. The message is broadcast on the
    /// user-id channel; every <see cref="IActiveOrganizationContext"/> subscribes
    /// on its own user id and re-queries on receipt.
    /// </summary>
    /// <remarks>
    /// The channel is the user id rather than the connection id to avoid a DI
    /// cycle (ActiveOrganizationContext otherwise would need ICircuitConnection,
    /// which itself depends on IActiveOrganizationContext in circuit code).
    /// </remarks>
    private async Task NotifyMembershipChanged(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }
        try
        {
            await _messenger.Send(new MembershipChangedMessage(userId), userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send MembershipChangedMessage for user {UserId}.", userId);
        }
    }

    public async Task AddAlert(string deviceId, string organizationId, string alertMessage, string? details = null)
    {
        using var dbContext = _appDbFactory.GetContext();

        var users = dbContext.Users
           .Include(x => x.Alerts)
           .Where(x => x.Memberships.Any(m => m.OrganizationId == organizationId));

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var filteredUserIDs = FilterUsersByDevicePermissionInternal(
                dbContext,
                users.Select(x => x.Id),
                deviceId);

            users = users.Where(x => filteredUserIDs.Contains(x.Id));
        }

        await users.ForEachAsync(x =>
        {
            var alert = new Alert()
            {
                CreatedOn = DateTimeOffset.Now,
                DeviceID = deviceId,
                Message = alertMessage,
                OrganizationID = organizationId,
                Details = details
            };
            x.Alerts ??= new List<Alert>();
            x.Alerts.Add(alert);
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task<Result<DeviceGroup>> AddDeviceGroup(string orgId, DeviceGroup deviceGroup)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = dbContext.Organizations
            .Include(x => x.DeviceGroups)
            .FirstOrDefault(x => x.ID == orgId);

        if (organization is null)
        {
            return Result.Fail<DeviceGroup>("Organization not found.");
        }

        if (dbContext.DeviceGroups.Any(x =>
            x.OrganizationID == orgId &&
            x.Name.ToLower() == deviceGroup.Name.ToLower()))
        {
            return Result.Fail<DeviceGroup>("Device group already exists.");
        }

        dbContext.Attach(deviceGroup);
        deviceGroup.Organization = organization;
        deviceGroup.OrganizationID = orgId;

        organization.DeviceGroups ??= new List<DeviceGroup>();
        organization.DeviceGroups.Add(deviceGroup);
        await dbContext.SaveChangesAsync();
        return Result.Ok(deviceGroup);
    }

    public async Task<Result> AddDeviceToGroup(string deviceId, string groupId)
    {
        using var context = _appDbFactory.GetContext();
        var device = await context.Devices.FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return Result.Fail("Device not found.");
        }

        var group = await context.DeviceGroups.FirstOrDefaultAsync(x => 
            x.OrganizationID == device.OrganizationID &&
            x.ID == groupId);

        if (group is null)
        {
            return Result.Fail("Group not found.");
        }

        group.Devices ??= new();
        group.Devices.Add(device);
        device.DeviceGroup = group;
        device.DeviceGroupID = group.ID;
        await context.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result<InviteLink>> AddInvite(string orgId, InviteViewModel invite)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = dbContext.Organizations
            .Include(x => x.InviteLinks)
            .FirstOrDefault(x => x.ID == orgId);

        if (organization is null)
        {
            return Result.Fail<InviteLink>("Organization not found.");
        }

        // KD-04: invitees always receive base privileges; InviteLink.IsAdmin removed (FR-19 / §6.1).
        var inviteLink = new InviteLink()
        {
            InvitedUser = invite.InvitedUser?.ToLower(),
            DateSent = DateTimeOffset.Now,
            Organization = organization,
            OrganizationID = organization.ID,
        };

        organization.InviteLinks ??= new List<InviteLink>();
        organization.InviteLinks.Add(inviteLink);
        await dbContext.SaveChangesAsync();
        return Result.Ok(inviteLink);
    }

    public async Task<Result<Device>> AddOrUpdateDevice(DeviceClientDto deviceDto)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceDto.ID);

        if (device is null)
        {
            device = new Device
            {
                OrganizationID = deviceDto.OrganizationID,
                ID = deviceDto.ID,
            };
            await dbContext.Devices.AddAsync(device);
        }

        device.CurrentUser = deviceDto.CurrentUser;
        device.DeviceName = deviceDto.DeviceName;
        device.Drives = deviceDto.Drives;
        device.CpuUtilization = deviceDto.CpuUtilization;
        device.UsedMemory = deviceDto.UsedMemory;
        device.UsedStorage = deviceDto.UsedStorage;
        device.Is64Bit = deviceDto.Is64Bit;
        device.IsOnline = true;
        device.OSArchitecture = deviceDto.OSArchitecture;
        device.OSDescription = deviceDto.OSDescription;
        device.Platform = deviceDto.Platform;
        device.ProcessorCount = deviceDto.ProcessorCount;
        device.PublicIP = deviceDto.PublicIP;
        device.TotalMemory = deviceDto.TotalMemory;
        device.TotalStorage = deviceDto.TotalStorage;
        device.AgentVersion = deviceDto.AgentVersion;
        device.MacAddresses = deviceDto.MacAddresses ?? Array.Empty<string>();
        device.LastOnline = DateTimeOffset.Now;

        if (_hostEnvironment.IsDevelopment() && dbContext.Organizations.Any())
        {
            var org = await dbContext.Organizations.FirstAsync();
            device.Organization = org;
            device.OrganizationID = org.ID;
        }

        if (!await dbContext.Organizations.AnyAsync(x => x.ID == device.OrganizationID))
        {
            _logger.LogInformation(
                "Unable to add device {deviceName} because organization {organizationID}" +
                "does not exist.  Device ID: {ID}.",
                device.DeviceName,
                device.OrganizationID,
                device.ID);

            return Result.Fail<Device>("Organization does not exist.");
        }

        await dbContext.SaveChangesAsync();
        return Result.Ok(device);
    }

    public async Task<Result> AddOrUpdateSavedScript(SavedScript script, string userId, string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        dbContext.SavedScripts.Update(script);

        if (script.Creator is null)
        {
            var user = await dbContext.Users.FindAsync(userId);
            if (user is null)
            {
                return Result.Fail("User not found.");
            }

            script.CreatorId = user.Id;
            script.Creator = user;
            script.OrganizationID = organizationId;
        }

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task AddOrUpdateScriptSchedule(ScriptSchedule schedule)
    {
        using var dbContext = _appDbFactory.GetContext();

        var existingSchedule = await dbContext.ScriptSchedules
            .Include(x => x.Creator)
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .FirstOrDefaultAsync(x => x.Id == schedule.Id);

        if (existingSchedule is null)
        {
            dbContext.Update(schedule);
        }
        else
        {
            var entry = dbContext.Entry(existingSchedule);
            entry.CurrentValues.SetValues(schedule);

            existingSchedule.Devices.Clear();
            if (schedule.Devices?.Any() == true)
            {
                var deviceIds = schedule.Devices.Select(x => x.ID);
                var newDevices = await dbContext.Devices
                    .Where(x => deviceIds.Contains(x.ID))
                    .ToListAsync();
                existingSchedule.Devices.AddRange(newDevices);
            }

            existingSchedule.DeviceGroups.Clear();
            if (schedule.DeviceGroups?.Any() == true)
            {
                var deviceGroupIds = schedule.DeviceGroups.Select(x => x.ID);
                var newDeviceGroups = await dbContext.DeviceGroups
                    .Where(x => deviceGroupIds.Contains(x.ID))
                    .ToListAsync();
                existingSchedule.DeviceGroups.AddRange(newDeviceGroups);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<Result<ScriptResult>> AddScriptResult(ScriptResultDto dto)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = dbContext.Devices.Find(dto.DeviceID);

        if (device is null)
        {
            return Result.Fail<ScriptResult>("Device not found.");
        }

        var scriptResult = new ScriptResult
        {
            DeviceID = dto.DeviceID,
            OrganizationID = device.OrganizationID
        };

        var entry = dbContext.Attach(scriptResult);
        entry.CurrentValues.SetValues(dto);
        entry.State = EntityState.Added;
        await dbContext.ScriptResults.AddAsync(scriptResult);
        await dbContext.SaveChangesAsync();
        return Result.Ok(scriptResult);
    }

    public async Task<Result> AddScriptResultToScriptRun(string scriptResultId, int scriptRunId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var run = await dbContext.ScriptRuns
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == scriptRunId);

        if (run is null)
        {
            return Result.Fail("Run not found.");
        }

        var result = await dbContext.ScriptResults.FindAsync(scriptResultId);

        if (result is null)
        {
            return Result.Fail("Results not found.");
        }

        run.Results ??= new List<ScriptResult>();
        run.Results.Add(result);

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task AddScriptRun(ScriptRun scriptRun)
    {
        using var dbContext = _appDbFactory.GetContext();

        dbContext.Attach(scriptRun);
        dbContext.ScriptRuns.Add(scriptRun);
        await dbContext.SaveChangesAsync();
    }

    public async Task<string> AddSharedFile(IBrowserFile file, string organizationId, Action<double, string> progressCallback)
    {
        var fileSize = file.Size;
        var fileName = file.Name;

        var fileContents = new byte[fileSize];
        var stream = file.OpenReadStream(AppConstants.MaxUploadFileSize);

        var bytesRead = 0;
        while (bytesRead < fileSize)
        {
            var segmentEnd = Math.Min(50_000, fileSize - bytesRead);
            var read = await stream.ReadAsync(fileContents.AsMemory(bytesRead, (int)segmentEnd));
            if (read == 0)
            {
                break;
            }
            bytesRead += read;
            progressCallback.Invoke((double)bytesRead / fileSize, fileName);
        }

        progressCallback.Invoke(1, fileName);

        return await AddSharedFileImpl(file.Name, fileContents, file.ContentType, organizationId);
    }

    public async Task<string> AddSharedFile(IFormFile file, string organizationId)
    {
        var fileContents = new byte[file.Length];
        using var stream = file.OpenReadStream();
        await stream.ReadAsync(fileContents.AsMemory(0, (int)file.Length));

        return await AddSharedFileImpl(file.Name, fileContents, file.ContentType, organizationId);
    }

    public bool AddUserToDeviceGroup(string orgId, string groupId, string userName, out string resultMessage)
    {
        using var dbContext = _appDbFactory.GetContext();

        resultMessage = string.Empty;

        var deviceGroup = dbContext.DeviceGroups
            .Include(x => x.Users)
            .FirstOrDefault(x =>
                x.ID == groupId &&
                x.OrganizationID == orgId);

        if (deviceGroup == null)
        {
            resultMessage = "Device group not found.";
            return false;
        }

        userName = userName.Trim().ToLower();

        var user = dbContext.Users
            .Include(x => x.DeviceGroups)
            .FirstOrDefault(x =>
                x.UserName!.ToLower() == userName &&
                x.Memberships.Any(m => m.OrganizationId == orgId));

        if (user == null)
        {
            resultMessage = "User not found.";
            return false;
        }

        deviceGroup.Devices ??= new List<Device>();
        user.DeviceGroups ??= new List<DeviceGroup>();

        if (deviceGroup.Users.Any(x => x.Id == user.Id))
        {
            resultMessage = "User already in group.";
            return false;
        }

        deviceGroup.Users.Add(user);
        user.DeviceGroups.Add(deviceGroup);
        dbContext.SaveChanges();
        resultMessage = user.Id;
        return true;
    }

    // ChangeUserIsAdmin was removed per §8.1 — superseded by SetMemberIsAdmin.

    public async Task CleanupOldRecords()
    {
        var settings = await GetSettings();
        using var dbContext = _appDbFactory.GetContext();

        if (settings.DataRetentionInDays < 0)
        {
            return;
        }

        var expirationDate = DateTimeOffset.Now - TimeSpan.FromDays(settings.DataRetentionInDays);

        var scriptRuns = await dbContext.ScriptRuns
            .Include(x => x.Results)
            .Include(x => x.Devices)
            .Where(x => x.RunAt < expirationDate)
            .ToArrayAsync();

        foreach (var run in scriptRuns)
        {
            run.Devices?.Clear();
            run.Results?.Clear();
        }

        dbContext.RemoveRange(scriptRuns);

        var commandResults = dbContext.ScriptResults
                                .Where(x => x.TimeStamp < expirationDate);

        dbContext.RemoveRange(commandResults);

        var sharedFiles = dbContext.SharedFiles
                                .Where(x => x.Timestamp < expirationDate);

        dbContext.RemoveRange(sharedFiles);

        await dbContext.SaveChangesAsync();
    }

    public async Task<Result<ApiToken>> CreateApiToken(string userName, string tokenName, string secretHash)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = dbContext.Users.FirstOrDefault(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail<ApiToken>("User not found.");
        }

        // FR-28 / KD-05: tokens are identity-only; OrganizationID is null on new tokens.
        // CreatorId binds the token to the issuing user; the target org is supplied
        // per-request via an explicit organizationId parameter.
        var newToken = new ApiToken()
        {
            Name = tokenName,
            OrganizationID = null,
            CreatorId = user.Id,
            Secret = secretHash
        };
        dbContext.ApiTokens.Add(newToken);
        await dbContext.SaveChangesAsync();
        return Result.Ok(newToken);
    }

    public async Task<Result<Device>> CreateDevice(DeviceSetupOptions options)
    {
        using var dbContext = _appDbFactory.GetContext();

        try
        {
            if (options is null ||
                string.IsNullOrWhiteSpace(options.DeviceID) ||
                string.IsNullOrWhiteSpace(options.OrganizationID) ||
                dbContext.Devices.Any(x => x.ID == options.DeviceID))
            {
                return Result.Fail<Device>("Required parameters are missing or incorrect.");
            }

            var device = new Device()
            {
                ID = options.DeviceID,
                OrganizationID = options.OrganizationID
            };

            if (!string.IsNullOrWhiteSpace(options.DeviceAlias))
            {
                device.Alias = options.DeviceAlias;
            }

            if (!string.IsNullOrWhiteSpace(options.DeviceGroupName))
            {
                var group = dbContext.DeviceGroups.FirstOrDefault(x =>
                    x.Name.ToLower() == options.DeviceGroupName.ToLower() &&
                    x.OrganizationID == device.OrganizationID);
                device.DeviceGroup = group;
            }

            dbContext.Devices.Add(device);

            await dbContext.SaveChangesAsync();

            return Result.Ok(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating device for organization {id}.", options.OrganizationID);
            return Result.Fail<Device>("An error occurred while creating the device.");
        }
    }

    public async Task<Result> CreateUser(string userEmail, bool isAdmin, string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        try
        {
            var org = dbContext.Organizations
                .Include(x => x.Memberships)
                .FirstOrDefault(x => x.ID == organizationId);

            if (org is null)
            {
                return Result.Fail("Organization not found.");
            }

            var user = new RemotelyUser()
            {
                UserName = userEmail.Trim().ToLower(),
                Email = userEmail.Trim().ToLower(),
                UserOptions = new RemotelyUserOptions(),
                LockoutEnabled = true
            };

            dbContext.Users.Add(user);
            // Note: CreateUser is a server-side seed/admin operation (not the invite or
            // direct-add flow). KD-04's base-privilege-default applies to FR-08 / FR-09;
            // here the caller is the server admin explicitly setting the admin flag.
            var membership = new UserOrganizationMembership
            {
                User = user,
                UserId = user.Id,
                Organization = org,
                OrganizationId = org.ID,
                IsAdministrator = isAdmin,
                CanInvite = false
            };
            org.Memberships.Add(membership);
            dbContext.UserOrganizationMemberships.Add(membership);
            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating user for organization {id}.", organizationId);
            return Result.Fail("An error occurred while creating user.");
        }
    }

    public async Task DeleteAlert(Alert alert)
    {
        using var dbContext = _appDbFactory.GetContext();

        dbContext.Alerts.Remove(alert);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAllAlerts(string orgId, string? userName = null)
    {
        using var dbContext = _appDbFactory.GetContext();

        var alerts = dbContext.Alerts.Where(x => x.OrganizationID == orgId);

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var userResult = await GetUserByName(userName);

            if (userResult.IsSuccess)
            {
                var userId = userResult.Value.Id;
                alerts = alerts.Where(x => x.UserID == userId);
            }
        }

        dbContext.Alerts.RemoveRange(alerts);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Result> DeleteApiToken(string userName, string tokenId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = dbContext.Users.FirstOrDefault(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        // FR-28: tokens belong to users; scope by CreatorId.
        var token = dbContext.ApiTokens.FirstOrDefault(x =>
            x.ID == tokenId && x.CreatorId == user.Id);

        if (token is null)
        {
            return Result.Fail("Token not found.");
        }

        dbContext.ApiTokens.Remove(token);
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteDeviceGroup(string orgId, string deviceGroupID)
    {
        using var dbContext = _appDbFactory.GetContext();

        var deviceGroup = dbContext.DeviceGroups
            .Include(x => x.Devices)
            .Include(x => x.Users)
            .ThenInclude(x => x.DeviceGroups)
            .FirstOrDefault(x =>
                x.ID == deviceGroupID &&
                x.OrganizationID == orgId);

        if (deviceGroup is null)
        {
            return Result.Fail("Device group not found.");
        }

        deviceGroup.Devices.ForEach(x =>
        {
            x.DeviceGroup = null;
            x.DeviceGroupID = null;
        });

        deviceGroup.Users.ForEach(x =>
        {
            x.DeviceGroups.Remove(deviceGroup);
        });

        deviceGroup.Devices.Clear();
        deviceGroup.Users.Clear();

        dbContext.DeviceGroups.Remove(deviceGroup);

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteInvite(string orgId, string inviteID)
    {
        using var dbContext = _appDbFactory.GetContext();

        var invite = dbContext.InviteLinks.FirstOrDefault(x =>
            x.OrganizationID == orgId &&
            x.ID == inviteID);

        if (invite is null)
        {
            return Result.Fail("Invite not found.");
        }

        var user = dbContext.Users.FirstOrDefault(x => x.UserName == invite.InvitedUser);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            dbContext.Remove(user);
        }

        dbContext.Remove(invite);
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task DeleteSavedScript(Guid scriptId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var schedules = await dbContext.ScriptSchedules
            .Where(x => x.SavedScriptId == scriptId)
            .ToListAsync();

        if (schedules.Count > 0)
        {
            dbContext.ScriptSchedules.RemoveRange(schedules);
        }

        var script = await dbContext.SavedScripts
            .Include(x => x.ScriptResults)
            .Include(x => x.ScriptRuns)
            .FirstOrDefaultAsync(x => x.Id == scriptId);

        if (script is not null)
        {
            dbContext.SavedScripts.Remove(script);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteScriptSchedule(int scriptScheduleId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var schedule = await dbContext.ScriptSchedules
            .Include(x => x.ScriptRuns)
            .ThenInclude(x => x.Results)
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .FirstOrDefaultAsync(x => x.Id == scriptScheduleId);

        if (schedule is not null)
        {
            dbContext.ScriptSchedules.Remove(schedule);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Result> DeleteUser(string orgId, string targetUserId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = dbContext
            .Organizations
            .Include(x => x.Memberships)
            .FirstOrDefault(x => x.ID == orgId);

        if (org is null)
        {
            return Result.Fail("Organization not found.");
        }

        // All the joins are necessary for client-side cascade delete.
        // This method will be called rarely, so I'm not concerned
        // about the performance.
        var target = dbContext.Users
            .Include(x => x.DeviceGroups)
            .ThenInclude(x => x.Devices)
            .Include(x => x.Memberships)
            .Include(x => x.Alerts)
            .Include(x => x.SavedScripts)
            .ThenInclude(x => x.ScriptRuns)
            .Include(x => x.SavedScripts)
            .ThenInclude(x => x.ScriptResults)
            .Include(x => x.ScriptSchedules)
            .ThenInclude(x => x.ScriptRuns)
            .ThenInclude(x => x.Results)
            .FirstOrDefault(x =>
                x.Id == targetUserId &&
                x.Memberships.Any(m => m.OrganizationId == orgId));

        if (target is null)
        {
            return Result.Fail("User not found.");
        }

        dbContext.Users.Remove(target);

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public void DeviceDisconnected(string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = dbContext.Devices.Find(deviceId);
        if (device != null)
        {
            device.LastOnline = DateTimeOffset.Now;
            device.IsOnline = false;
            dbContext.SaveChanges();
        }
    }

    public bool DoesUserExist(string userName)
    {
        using var dbContext = _appDbFactory.GetContext();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        return dbContext.Users
            .Where(x => x.UserName != null)
            .Any(x => x.UserName!.Trim().ToLower() == userName.Trim().ToLower());
    }

    public bool DoesUserHaveAccessToDevice(string deviceId, RemotelyUser remotelyUser, string organizationId, bool isOrgAdmin)
    {
        if (remotelyUser is null)
        {
            return false;
        }

        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .Include(x => x.DeviceGroup)
            .ThenInclude(x => x!.Users)
            .Any(device =>
                device.OrganizationID == organizationId &&
                device.ID == deviceId &&
                (
                    isOrgAdmin ||
                    device.DeviceGroup!.Users.Any(user => user.Id == remotelyUser.Id
                )));
    }

    public bool DoesUserHaveAccessToDevice(string deviceId, string remotelyUserId)
    {
        using var dbContext = _appDbFactory.GetContext();

        // Look up the device first so we know which org we're checking against.
        var device = dbContext.Devices
            .Include(x => x.DeviceGroup)
            .ThenInclude(x => x!.Users)
            .FirstOrDefault(x => x.ID == deviceId);

        if (device is null || string.IsNullOrWhiteSpace(device.OrganizationID))
        {
            return false;
        }

        var remotelyUser = dbContext.Users
            .Include(x => x.Memberships)
            .FirstOrDefault(x => x.Id == remotelyUserId);

        if (remotelyUser is null)
        {
            return false;
        }

        // Server admins implicitly have access to any device (FR-18).
        if (remotelyUser.IsServerAdmin)
        {
            return true;
        }

        // Check the user's membership in the device's org.
        var membership = remotelyUser.Memberships.FirstOrDefault(m => m.OrganizationId == device.OrganizationID);
        if (membership is null)
        {
            return false;
        }

        if (membership.IsAdministrator)
        {
            return true;
        }

        // Otherwise, user must have an explicit DeviceGroup assignment that includes this device.
        return device.DeviceGroup?.Users.Any(u => u.Id == remotelyUserId) == true;
    }

    public string[] FilterDeviceIdsByUserPermission(string[] deviceIds, RemotelyUser remotelyUser)
    {
        if (remotelyUser is null)
        {
            return Array.Empty<string>();
        }

        using var dbContext = _appDbFactory.GetContext();

        var userId = remotelyUser.Id;
        var isServerAdmin = remotelyUser.IsServerAdmin;

        return dbContext.Devices
            .Include(x => x.DeviceGroup)
            .ThenInclude(x => x!.Users)
            .Where(device =>
                deviceIds.Contains(device.ID) &&
                (
                    isServerAdmin ||
                    dbContext.UserOrganizationMemberships.Any(m =>
                        m.UserId == userId &&
                        m.OrganizationId == device.OrganizationID &&
                        (m.IsAdministrator || device.DeviceGroup!.Users.Any(u => u.Id == userId)))
                ))
            .Select(x => x.ID)
            .ToArray();
    }

    public string[] FilterUsersByDevicePermission(IEnumerable<string> userIds, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return FilterUsersByDevicePermissionInternal(dbContext, userIds, deviceId);
    }

    public async Task<Result<Alert>> GetAlert(string alertId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var alert = await dbContext.Alerts
            .AsNoTracking()
            .Include(x => x.Device)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.ID == alertId);

        if (alert is null)
        {
            return Result.Fail<Alert>("Alert not found.");
        }

        return Result.Ok(alert);
    }

    public Alert[] GetAlerts(string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Alerts
            .AsNoTracking()
            .Include(x => x.Device)
            .Include(x => x.User)
            .Where(x => x.UserID == userId)
            .OrderByDescending(x => x.CreatedOn)
            .ToArray();
    }

    public ApiToken[] GetAllApiTokens(string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        // FR-28: tokens are user-scoped via CreatorId; no org filter.
        return dbContext.ApiTokens
            .AsNoTracking()
            .Where(x => x.CreatorId == userId)
            .OrderByDescending(x => x.LastUsed)
            .ToArray();
    }

    public ScriptResult[] GetAllCommandResults(string orgId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.ScriptResults
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId)
            .OrderByDescending(x => x.TimeStamp)
            .ToArray();
    }

    public ScriptResult[] GetAllCommandResultsForUser(string orgId, string userName, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.ScriptResults
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId &&
                x.SenderUserName == userName &&
                x.DeviceID == deviceId)
            .OrderByDescending(x => x.TimeStamp)
            .ToArray();
    }

    public Device[] GetAllDevices(string orgId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId)
            .ToArray();
    }

    public InviteLink[] GetAllInviteLinks(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.InviteLinks
            .AsNoTracking()
            .Where(x => x.OrganizationID == organizationId)
            .ToArray();
    }

    public ScriptResult[] GetAllScriptResults(string orgId, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.ScriptResults
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId && x.DeviceID == deviceId)
            .OrderByDescending(x => x.TimeStamp)
            .ToArray();
    }

    public ScriptResult[] GetAllScriptResultsForUser(string orgId, string userName)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.ScriptResults
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId && x.SenderUserName == userName)
            .OrderByDescending(x => x.TimeStamp)
            .ToArray();
    }

    public RemotelyUser[] GetAllUsersForServer()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Users
            .AsNoTracking()
            .ToArray();
    }

    public async Task<RemotelyUser[]> GetAllUsersInOrganization(string orgId)
    {
        if (string.IsNullOrWhiteSpace(orgId))
        {
            return Array.Empty<RemotelyUser>();
        }

        using var dbContext = _appDbFactory.GetContext();

        // Include Memberships so callers can read per-org role flags (e.g. for the
        // ManageOrganization admin checkbox) without an extra query per row.
        return await dbContext.Users
            .AsNoTracking()
            .Include(u => u.Memberships)
            .Where(u => u.Memberships.Any(m => m.OrganizationId == orgId))
            .ToArrayAsync();
    }

    public async Task<Result<ApiToken>> GetApiKey(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return Result.Fail<ApiToken>("Key ID cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var token = await dbContext.ApiTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == keyId);

        if (token is null)
        {
            return Result.Fail<ApiToken>("API key not found.");
        }

        return Result.Ok(token);
    }

    public async Task<Result<BrandingInfo>> GetBrandingInfo(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Result.Fail<BrandingInfo>("Organization ID cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations
          .AsNoTracking()
          .Include(x => x.BrandingInfo)
          .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (organization is null)
        {
            return Result.Fail<BrandingInfo>("Organization not found.");
        }

        if (organization.BrandingInfo is null)
        {
            var brandingInfo = new BrandingInfo()
            {
                OrganizationId = organizationId
            };

            dbContext.BrandingInfos.Add(brandingInfo);
            organization.BrandingInfo = brandingInfo;

            await dbContext.SaveChangesAsync();
        }
        return Result.Ok(organization.BrandingInfo);
    }

    public async Task<Result<Organization>> GetDefaultOrganization()
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsDefaultOrganization);
        
        if (org is null)
        {
            return Result.Fail<Organization>("Organization not found.");
        }

        return Result.Ok(org);
    }

    public async Task<Result<Device>> GetDevice(string orgId, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OrganizationID == orgId &&
                x.ID == deviceId);

        if (device is null)
        {
            return Result.Fail<Device>("Device not found.");
        }
        return Result.Ok(device);
    }

    public async Task<Result<Device>> GetDevice(
        string deviceId,
        Action<IQueryable<Device>>? queryBuilder = null)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .AsNoTracking()
            .Apply(queryBuilder)
            .FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return Result.Fail<Device>("Device not found.");
        }
        return Result.Ok(device);
    }

    public int GetDeviceCount()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices.Count();
    }

    public int GetDeviceCount(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .AsNoTracking()
            .Count(x => x.OrganizationID == organizationId);
    }

    public async Task<Result<DeviceGroup>> GetDeviceGroup(
        string deviceGroupId,
        bool includeDevices = false,
        bool includeUsers = false)
    {
        using var dbContext = _appDbFactory.GetContext();

        var query = dbContext.DeviceGroups
            .AsNoTracking()
            .AsQueryable();

        if (includeDevices)
        {
            query = query.Include(x => x.Devices);
        }
        if (includeUsers)
        {
            query = query.Include(x => x.Users);
        }

        var group = await query.FirstOrDefaultAsync(x => x.ID == deviceGroupId);

        if (group is null)
        {
            return Result.Fail<DeviceGroup>("Device group not found.");
        }
        return Result.Ok(group);
    }

    public DeviceGroup[] GetDeviceGroups(string username, string organizationId, bool isOrgAdmin)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = dbContext.Users
            .AsNoTracking()
            .FirstOrDefault(x => x.UserName == username);

        if (user is null)
        {
            return Array.Empty<DeviceGroup>();
        }
        var userId = user.Id;

        var groupIds = dbContext.DeviceGroups
            .AsNoTracking()
            .Include(x => x.Users)
            .ThenInclude(x => x.DeviceGroups)
            .Where(x =>
                x.OrganizationID == organizationId &&
                (
                    isOrgAdmin ||
                    x.Users.Any(u => u.Id == userId)
                )
            )
            .Select(x => x.ID)
            .ToHashSet();

        if (groupIds.Any())
        {
            return dbContext.DeviceGroups
                .AsNoTracking()
                .Where(x => groupIds.Contains(x.ID))
                .OrderBy(x => x.Name)
                .ToArray();
        }

        return Array.Empty<DeviceGroup>();
    }

    public DeviceGroup[] GetDeviceGroupsForOrganization(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.DeviceGroups
            .AsNoTracking()
            .Include(x => x.Users)
            .Where(x => x.OrganizationID == organizationId)
            .OrderBy(x => x.Name)
            .ToArray();
    }

    public List<Device> GetDevices(IEnumerable<string> deviceIds)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .AsNoTracking()
            .Where(x => deviceIds.Contains(x.ID))
            .ToList();
    }

    public Device[] GetDevicesForUser(string userName, string organizationId, bool isOrgAdmin)
    {
        using var dbContext = _appDbFactory.GetContext();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return Array.Empty<Device>();
        }

        if (isOrgAdmin)
        {
            return dbContext.Devices
                .AsNoTracking()
                .Where(x => x.OrganizationID == organizationId)
                .ToArray();
        }

        return dbContext.Users
            .AsNoTracking()
            .Include(x => x.DeviceGroups)
            .ThenInclude(x => x.Devices)
            .Where(x => x.UserName == userName)
            .SelectMany(x => x.DeviceGroups)
            .Where(g => g.OrganizationID == organizationId)
            .SelectMany(g => g.Devices)
            .ToArray();
    }

    public async Task<Result<Organization>> GetOrganizationById(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations.FindAsync(organizationId);

        if (org is null)
        {
            return Result.Fail<Organization>("Organization not found.");
        }
        return Result.Ok(org);
    }


    // GetOrganizationByUserName was removed per multi-tenant refactor — a user may
    // belong to multiple orgs, so this method no longer has a well-defined return.
    // Callers must source the active org from IActiveOrganizationContext (Blazor
    // circuits) or from the request parameter (HTTP) per §5.4.

    public int GetOrganizationCount()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Organizations.Count();
    }

    public async Task<int> GetOrganizationCountAsync()
    {
        using var dbContext = _appDbFactory.GetContext();

        return await dbContext.Organizations.CountAsync();
    }

    public async Task<Result<string>> GetOrganizationNameById(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (org is null)
        {
            return Result.Fail<string>("Organization not found.");
        }

        return Result.Ok(org.OrganizationName);
    }

    // GetOrganizationNameByUserName was removed for the same reason as
    // GetOrganizationByUserName above. Use GetOrganizationNameById with the
    // active org id from the caller's context.

    public async Task<IEnumerable<ScriptRun>> GetPendingScriptRuns(string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .AsNoTracking()
            .Include(x => x.ScriptRuns)
            .ThenInclude(x => x.Results)
            .Include(x => x.ScriptResults)
            .FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return Enumerable.Empty<ScriptRun>();
        }

        device.ScriptResults ??= new();
        var scriptResultsLookup = device.ScriptResults
            .Select(x => x.ScriptRunId)
            .Distinct()
            .ToHashSet();

        return device.ScriptRuns
            .OrderByDescending(x => x.RunAt)
            .DistinctBy(x => x.SavedScriptId)
            .Where(x => !scriptResultsLookup.Contains(x.Id))
            .ToArray();
    }

    public async Task<List<SavedScript>> GetQuickScripts(string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return await dbContext.SavedScripts
            .Where(x => x.CreatorId == userId && x.IsQuickScript)
            .ToListAsync();
    }

    public async Task<Result<SavedScript>> GetSavedScript(string userId, Guid scriptId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var script = await dbContext.SavedScripts
            .AsNoTracking()
            .Include(x => x.Creator)
            .FirstOrDefaultAsync(x =>
                x.Id == scriptId &&
                (x.IsPublic || x.CreatorId == userId));

        if (script is null)
        {
            return Result.Fail<SavedScript>("Script not found.");
        }
        return Result.Ok(script);
    }

    public async Task<Result<SavedScript>> GetSavedScript(Guid scriptId)
    {
        using var dbContext = _appDbFactory.GetContext();
        var script = await dbContext.SavedScripts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == scriptId);

        if (script is null)
        {
            return Result.Fail<SavedScript>("Script not found.");
        }
        return Result.Ok(script);
    }

    public async Task<List<SavedScript>> GetSavedScriptsWithoutContent(string userId, string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        // SavedScript.OrganizationID is set at creation by the originating org context
        // (see AddOrUpdateSavedScript). Filter the script row directly rather than via
        // Creator's org, since under the multi-tenant model the creator may belong to
        // multiple orgs.
        return await dbContext.SavedScripts
            .AsNoTracking()
            .Include(x => x.Creator)
            .Where(x =>
                x.OrganizationID == organizationId &&
                (x.IsPublic || x.CreatorId == userId))
            .Select(x => new SavedScript()
            {
                Creator = x.Creator,
                CreatorId = x.CreatorId,
                FolderPath = x.FolderPath,
                Id = x.Id,
                IsPublic = x.IsPublic,
                IsQuickScript = x.IsQuickScript,
                Name = x.Name,
                OrganizationID = x.OrganizationID
            })
            .ToListAsync();
    }

    public async Task<Result<ScriptResult>> GetScriptResult(string resultId, string orgId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var result = await dbContext.ScriptResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OrganizationID == orgId &&
                x.ID == resultId);

        if (result is null)
        {
            return Result.Fail<ScriptResult>("Script result not found.");
        }
        return Result.Ok(result);
    }

    public async Task<Result<ScriptResult>> GetScriptResult(string resultId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var result = await dbContext.ScriptResults.FindAsync(resultId);

        if (result is null)
        {
            return Result.Fail<ScriptResult>("Script result not found.");
        }
        return Result.Ok(result);
    }

    public async Task<List<ScriptSchedule>> GetScriptSchedules(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();
        return await dbContext.ScriptSchedules
            .AsNoTracking()
            .Include(x => x.Creator)
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .Where(x => x.OrganizationID == organizationId)
            .ToListAsync();
    }

    public async Task<List<ScriptSchedule>> GetScriptSchedulesDue()
    {
        using var dbContext = _appDbFactory.GetContext();

        var now = Time.Now;

        return await dbContext.ScriptSchedules
            .AsNoTracking()
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .ThenInclude(x => x.Devices)
            .Where(x => x.NextRun < now)
            .ToListAsync();
    }

    public List<string> GetServerAdmins()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsServerAdmin && x.UserName != null)
            .Select(x => x.UserName!)
            .ToList();
    }

    public async Task<SettingsModel> GetSettings()
    {
        await _settingsLock.WaitAsync();
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            return await dbContext.GetAppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting settings from database.");
            return new();
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<Result<SharedFile>> GetSharedFiled(string fileId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var file = await dbContext.SharedFiles.FindAsync(fileId);

        if (file is null)
        {
            return Result.Fail<SharedFile>("File not found.");
        }
        return Result.Ok(file);
    }

    public int GetTotalDevices()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices.Count();
    }

    public async Task<Result<RemotelyUser>> GetUserById(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Fail<RemotelyUser>("User ID cannot be empty.");
        }
        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
        {
            return Result.Fail<RemotelyUser>("User not found.");
        }
        return Result.Ok(user);
    }

    public async Task<Result<RemotelyUser>> GetUserByName(
        string userName,
        Action<IQueryable<RemotelyUser>>? queryBuilder = null)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail<RemotelyUser>("Username cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users
            .AsNoTracking()
            .Apply(queryBuilder)
            .FirstOrDefaultAsync(x =>
                x.UserName!.ToLower().Trim() == userName.ToLower().Trim());

        if (user is null)
        {
            return Result.Fail<RemotelyUser>("User not found.");
        }
        return Result.Ok(user);
    }

    public async Task<Result<RemotelyUserOptions>> GetUserOptions(string userName)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail<RemotelyUserOptions>("User not found.");
        }
        return Result.Ok(user.UserOptions ?? new());
    }

    public async Task<Result> JoinViaInvitation(string userName, string inviteId)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail("Username cannot be empty.");
        }
        if (string.IsNullOrWhiteSpace(inviteId))
        {
            return Result.Fail("Invite ID cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var invite = await dbContext.InviteLinks
            .FirstOrDefaultAsync(x =>
                x.InvitedUser!.ToLower() == userName.ToLower() &&
                x.ID == inviteId);

        if (invite is null)
        {
            return Result.Fail("Invite not found.");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var organization = await dbContext.Organizations
            .Include(x => x.Memberships)
            .FirstOrDefaultAsync(x => x.ID == invite.OrganizationID);

        if (organization is null)
        {
            return Result.Fail("Organization not found.");
        }

        // KD-04: invitees always receive base privileges (IsAdministrator=false, CanInvite=false).
        var membership = new UserOrganizationMembership
        {
            User = user,
            UserId = user.Id,
            Organization = organization,
            OrganizationId = organization.ID,
            IsAdministrator = false,
            CanInvite = false
        };
        dbContext.UserOrganizationMemberships.Add(membership);
        organization.Memberships.Add(membership);

        await dbContext.SaveChangesAsync();

        dbContext.InviteLinks.Remove(invite);
        await dbContext.SaveChangesAsync();

        // FR-14: notify any open circuits for this user of the new membership.
        await NotifyMembershipChanged(user.Id);

        return Result.Ok();
    }

    public void RemoveDevices(string[] deviceIDs)
    {
        using var dbContext = _appDbFactory.GetContext();

        var devices = dbContext.Devices
            .Include(x => x.ScriptResults)
            .Include(x => x.ScriptRuns)
            .Include(x => x.ScriptSchedules)
            .Include(x => x.DeviceGroup)
            .Include(x => x.Alerts)
            .Where(x => deviceIDs.Contains(x.ID));

        dbContext.Devices.RemoveRange(devices);
        dbContext.SaveChanges();
    }

    public async Task<bool> RemoveUserFromDeviceGroup(string orgID, string groupID, string userID)
    {
        using var dbContext = _appDbFactory.GetContext();

        var deviceGroup = await dbContext.DeviceGroups
            .Include(x => x.Users)
            .ThenInclude(x => x.DeviceGroups)
            .FirstOrDefaultAsync(x =>
                x.ID == groupID &&
                x.OrganizationID == orgID);

        if (deviceGroup?.Users?.Any(x => x.Id == userID) != true)
        {
            return false;
        }

        var user = deviceGroup.Users.FirstOrDefault(x => x.Id == userID);

        if (user is null)
        {
            return false;
        }

        user.DeviceGroups.Remove(deviceGroup);
        deviceGroup.Users.Remove(user);

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<Result> RenameApiToken(string userName, string tokenId, string tokenName)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.UserName == userName);
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        // FR-28: tokens belong to users; scope by CreatorId.
        var token = await dbContext.ApiTokens.FirstOrDefaultAsync(x =>
            x.ID == tokenId && x.CreatorId == user.Id);

        if (token is null)
        {
            return Result.Fail("API token not found.");
        }

        token.Name = tokenName;
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task ResetBranding(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations
           .Include(x => x.BrandingInfo)
           .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (organization?.BrandingInfo is null)
        {
            return;
        }

        var entry = dbContext.Entry(organization.BrandingInfo);
        entry.CurrentValues.SetValues(BrandingInfo.Default);
        
        await dbContext.SaveChangesAsync();
    }

    public async Task SaveSettings(SettingsModel settings)
    {
        await _settingsLock.WaitAsync();
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var record = await dbContext.KeyValueRecords.FindAsync(SettingsModel.DbKey);
            if (record is null)
            {
                record = new()
                {
                    Key = SettingsModel.DbKey,
                };
                await dbContext.KeyValueRecords.AddAsync(record);
                await dbContext.SaveChangesAsync();
            }
            record.Value = JsonSerializer.Serialize(settings);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving settings to database.");
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task SetAllDevicesNotOnline()
    {
        using var dbContext = _appDbFactory.GetContext();

        await dbContext.Devices.ForEachAsync(x =>
        {
            x.IsOnline = false;
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task SetDisplayName(RemotelyUser user, string displayName)
    {
        using var dbContext = _appDbFactory.GetContext();

        dbContext.Attach(user);
        user.UserOptions ??= new();
        user.UserOptions.DisplayName = displayName;
        await dbContext.SaveChangesAsync();
    }

    public async Task SetIsDefaultOrganization(string orgID, bool isDefault)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations.FindAsync(orgID);
        if (organization is null)
        {
            return;
        }

        if (isDefault)
        {
            await dbContext.Organizations.ForEachAsync(x => x.IsDefaultOrganization = false);
        }

        organization.IsDefaultOrganization = isDefault;
        await dbContext.SaveChangesAsync();
    }

    public async Task SetIsServerAdmin(string targetUserId, bool isServerAdmin, string callerUserId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var caller = await dbContext.Users.FindAsync(callerUserId);
        if (caller?.IsServerAdmin != true)
        {
            return;
        }

        var targetUser = await dbContext.Users.FindAsync(targetUserId);

        if (targetUser is null)
        {
            return;
        }

        // FR-17: a server admin cannot demote themselves regardless of remaining admin count.
        if (caller.Id == targetUser.Id)
        {
            return;
        }

        // FR-16: cannot remove the last active server admin.
        if (!isServerAdmin && targetUser.IsServerAdmin)
        {
            var otherActiveAdmins = await dbContext.Users
                .CountAsync(u => u.IsServerAdmin && u.Id != targetUserId && u.LockoutEnd == null);
            if (otherActiveAdmins == 0)
            {
                return;
            }
        }

        targetUser.IsServerAdmin = isServerAdmin;
        await dbContext.SaveChangesAsync();

        // Server admin status materially changes effective permissions across every org (KD-02).
        await NotifyMembershipChanged(targetUserId);
    }

    public void SetServerVerificationToken(string deviceID, string verificationToken)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = dbContext.Devices.Find(deviceID);
        if (device != null)
        {
            device.ServerVerificationToken = verificationToken;
            dbContext.SaveChanges();
        }
    }

    public async Task<bool> TempPasswordSignIn(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var userResult = await GetUserByName(email);

        if (!userResult.IsSuccess)
        {
            return false;
        }

        var user = userResult.Value;

        using var dbContext = _appDbFactory.GetContext();

        if (user.TempPassword != password)
        {
            return false;
        }

        user.TempPassword = string.Empty;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task UpdateBrandingInfo(
        string organizationId,
        string productName,
        byte[] iconBytes)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations
            .Include(x => x.BrandingInfo)
            .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (organization is null)
        {
            return;
        }

        organization.BrandingInfo ??= new BrandingInfo();

        organization.BrandingInfo.Product = productName;

        if (iconBytes?.Any() == true)
        {
            organization.BrandingInfo.Icon = iconBytes;
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateDevice(string deviceId, string? tag, string? alias, string? deviceGroupId, string? notes)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .Include(x => x.DeviceGroup)
            .FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(deviceGroupId))
        {
            device.DeviceGroup?.Devices?.RemoveAll(x => x.ID == deviceId);
            device.DeviceGroup = null;
            device.DeviceGroupID = null;
        }
        else
        {
            device.DeviceGroupID = deviceGroupId;
        }

        device.Tags = tag;
        device.Alias = alias;
        device.Notes = notes;
        await dbContext.SaveChangesAsync();
    }

    public async Task<Result<Device>> UpdateDevice(DeviceSetupOptions deviceOptions, string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceOptions.DeviceID);
        if (device == null || device.OrganizationID != organizationId)
        {
            return Result.Fail<Device>("Device not found.");
        }

        var group = await dbContext.DeviceGroups.FirstOrDefaultAsync(x =>
          x.Name.ToLower() == $"{deviceOptions.DeviceGroupName}".ToLower() &&
          x.OrganizationID == device.OrganizationID);
        device.DeviceGroup = group;

        device.Alias = deviceOptions.DeviceAlias;
        await dbContext.SaveChangesAsync();
        return Result.Ok(device);
    }

    public async Task<Result> UpdateOrganizationName(string orgId, string newName)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations.FirstOrDefaultAsync(x => x.ID == orgId);

        if (org is null)
        {
            return Result.Fail("Organization not found.");
        }
        
        org.OrganizationName = newName;
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task UpdateTags(string deviceID, string tags)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceID);
        if (device == null)
        {
            return;
        }

        device.Tags = tags;
        await dbContext.SaveChangesAsync();
    }

    public async Task<Result> UpdateUserOptions(string userName, RemotelyUserOptions options)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }
        user.UserOptions = options;
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<bool> ValidateApiKey(string keyId, string apiSecret, string requestPath, string remoteIP)
    {
        using var dbContext = _appDbFactory.GetContext();

        var hasher = new PasswordHasher<string>();
        var token = await dbContext.ApiTokens.FirstOrDefaultAsync(x => x.ID == keyId);

        var isValid = 
            !string.IsNullOrWhiteSpace(token?.Secret) &&
            hasher.VerifyHashedPassword(string.Empty, token.Secret, apiSecret) == PasswordVerificationResult.Success;

        if (token is not null)
        {
            token.LastUsed = DateTimeOffset.Now;
            await dbContext.SaveChangesAsync();
        }

        _logger.LogInformation(
            "API token used.  Token: {keyId}.  Path: {requestPath}.  Validated: {isValid}.  Remote IP: {remoteIP}", 
            keyId,
            requestPath,
            isValid,
            remoteIP);

        return isValid;
    }

    private async Task<string> AddSharedFileImpl(
        string fileName,
        byte[] fileContents,
        string contentType,
        string organizationId)
    {
        var settings = await GetSettings();
        using var dbContext = _appDbFactory.GetContext();

        var expirationDate = DateTimeOffset.Now.AddDays(-settings.DataRetentionInDays);
        var expiredFiles = dbContext.SharedFiles.Where(x => x.Timestamp < expirationDate);
        dbContext.RemoveRange(expiredFiles);

        var sharedFile = new SharedFile()
        {
            FileContents = fileContents,
            FileName = fileName,
            ContentType = contentType,
            OrganizationID = organizationId
        };

        dbContext.SharedFiles.Add(sharedFile);

        await dbContext.SaveChangesAsync();
        return sharedFile.ID;
    }

    private string[] FilterUsersByDevicePermissionInternal(AppDb dbContext, IEnumerable<string> userIDs, string deviceID)
    {
        var device = dbContext.Devices
             .Include(x => x.DeviceGroup)
             .ThenInclude(x => x!.Users)
             .FirstOrDefault(x => x.ID == deviceID);

        if (device is null)
        {
            return Array.Empty<string>();
        }

        // Org membership via the new join table: a user is "in" the device's org
        // when they have a UserOrganizationMembership for that org, or are a server admin (FR-18).
        var orgUsers = dbContext.Users
            .Where(user =>
                userIDs.Contains(user.Id) &&
                (
                    user.IsServerAdmin ||
                    user.Memberships.Any(m => m.OrganizationId == device.OrganizationID)
                ));

        if (string.IsNullOrWhiteSpace(device.DeviceGroupID))
        {
            return orgUsers
                .Select(x => x.Id)
                .ToArray();
        }

        var allowedUsers = device?.DeviceGroup?.Users?.Select(x => x.Id) ?? Array.Empty<string>();
        var deviceOrgId = device!.OrganizationID;

        return orgUsers
            .Where(user =>
                user.IsServerAdmin ||
                user.Memberships.Any(m => m.OrganizationId == deviceOrgId && m.IsAdministrator) ||
                allowedUsers.Contains(user.Id)
            )
            .Select(x => x.Id)
            .ToArray();
    }

    // ============================================================
    // Multi-tenant membership methods (added per §8.1 / FR-08, FR-15, FR-20, FR-21, FR-22, FR-23, FR-24).
    // ============================================================

    public async Task<Result> AddUserToOrganization(string orgId, string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var org = await dbContext.Organizations.FirstOrDefaultAsync(o => o.ID == orgId);
        if (org is null)
        {
            return Result.Fail("Organization not found.");
        }

        var existing = await dbContext.UserOrganizationMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == orgId);
        if (existing is not null)
        {
            return Result.Fail("User is already a member of this organization.");
        }

        // KD-04: base privileges only.
        var membership = new UserOrganizationMembership
        {
            UserId = userId,
            OrganizationId = orgId,
            IsAdministrator = false,
            CanInvite = false
        };
        dbContext.UserOrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        await NotifyMembershipChanged(userId);
        return Result.Ok();
    }

    public async Task<Result> RemoveUserFromOrganization(string orgId, string userId)
    {
        using var dbContext = _appDbFactory.GetContext();
        using var tx = await dbContext.Database.BeginTransactionAsync();

        var user = await dbContext.Users
            .Include(u => u.Memberships)
            .Include(u => u.DeviceGroups)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var membership = user.Memberships.FirstOrDefault(m => m.OrganizationId == orgId);
        if (membership is null)
        {
            return Result.Fail("Membership not found.");
        }

        // FR-24: prevent removing the user's last membership if not a server admin.
        if (!user.IsServerAdmin && user.Memberships.Count == 1)
        {
            return Result.Fail(
                "Cannot remove the user's only organization membership. " +
                "Delete the user account instead.");
        }

        // FR-15: prevent removing the last org admin unless a server admin exists as fallback.
        if (membership.IsAdministrator)
        {
            var otherAdmins = await dbContext.UserOrganizationMemberships
                .CountAsync(m =>
                    m.OrganizationId == orgId &&
                    m.IsAdministrator &&
                    m.UserId != userId);

            if (otherAdmins == 0)
            {
                var hasServerAdmin = await dbContext.Users
                    .AnyAsync(u => u.IsServerAdmin && u.LockoutEnd == null);
                if (!hasServerAdmin)
                {
                    return Result.Fail(
                        "Cannot remove the last administrator of this organization " +
                        "while no server admin exists as a fallback.");
                }
            }
        }

        // FR-26: strip DeviceGroup assignments belonging to this org, in the same transaction.
        var groupsInOrg = user.DeviceGroups.Where(g => g.OrganizationID == orgId).ToList();
        foreach (var group in groupsInOrg)
        {
            user.DeviceGroups.Remove(group);
        }

        dbContext.UserOrganizationMemberships.Remove(membership);
        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        await NotifyMembershipChanged(userId);
        return Result.Ok();
    }

    public async Task<Result> SetMemberIsAdmin(string orgId, string userId, bool isAdmin)
    {
        using var dbContext = _appDbFactory.GetContext();
        using var tx = await dbContext.Database.BeginTransactionAsync();

        var membership = await dbContext.UserOrganizationMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == orgId);
        if (membership is null)
        {
            return Result.Fail("Membership not found.");
        }

        if (membership.IsAdministrator == isAdmin)
        {
            return Result.Ok();
        }

        // FR-15: prevent demoting the last org admin unless a server admin exists as fallback.
        if (!isAdmin && membership.IsAdministrator)
        {
            var otherAdmins = await dbContext.UserOrganizationMemberships
                .CountAsync(m =>
                    m.OrganizationId == orgId &&
                    m.IsAdministrator &&
                    m.UserId != userId);

            if (otherAdmins == 0)
            {
                var hasServerAdmin = await dbContext.Users
                    .AnyAsync(u => u.IsServerAdmin && u.LockoutEnd == null);
                if (!hasServerAdmin)
                {
                    return Result.Fail(
                        "Cannot demote the last administrator of this organization " +
                        "while no server admin exists as a fallback.");
                }
            }
        }

        membership.IsAdministrator = isAdmin;
        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        await NotifyMembershipChanged(userId);
        return Result.Ok();
    }

    public async Task<Result> SetMemberCanInvite(string orgId, string userId, bool canInvite)
    {
        using var dbContext = _appDbFactory.GetContext();

        var membership = await dbContext.UserOrganizationMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == orgId);
        if (membership is null)
        {
            return Result.Fail("Membership not found.");
        }

        if (membership.CanInvite == canInvite)
        {
            return Result.Ok();
        }

        membership.CanInvite = canInvite;
        await dbContext.SaveChangesAsync();

        await NotifyMembershipChanged(userId);
        return Result.Ok();
    }

    public async Task<UserOrganizationMembership?> GetMembership(string orgId, string userId)
    {
        using var dbContext = _appDbFactory.GetContext();
        return await dbContext.UserOrganizationMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == orgId);
    }

    public async Task<IReadOnlyList<UserOrganizationMembership>> GetMembershipsForUser(string userId)
    {
        using var dbContext = _appDbFactory.GetContext();
        return await dbContext.UserOrganizationMemberships
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Organization>> GetAllOrganizations()
    {
        using var dbContext = _appDbFactory.GetContext();
        return await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(o => o.OrganizationName)
            .ToListAsync();
    }

    public async Task<Result<Organization>> CreateOrganization(string organizationName)
    {
        if (string.IsNullOrWhiteSpace(organizationName))
        {
            return Result.Fail<Organization>("Organization name is required.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var trimmed = organizationName.Trim();
        var existing = await dbContext.Organizations
            .FirstOrDefaultAsync(o => o.OrganizationName == trimmed);
        if (existing is not null)
        {
            return Result.Fail<Organization>("An organization with that name already exists.");
        }

        var org = new Organization { OrganizationName = trimmed };
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();
        return Result.Ok(org);
    }
}