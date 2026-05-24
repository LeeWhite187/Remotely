using Microsoft.AspNetCore.Identity;
using Remotely.Shared.Models;
using System.Text.Json.Serialization;

namespace Remotely.Shared.Entities;

public class RemotelyUser : IdentityUser
{
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public List<DeviceGroup> DeviceGroups { get; set; } = new();

    public bool IsServerAdmin { get; set; }

    [JsonIgnore]
    public ICollection<UserOrganizationMembership> Memberships { get; set; } = new List<UserOrganizationMembership>();

    public List<SavedScript> SavedScripts { get; set; } = new();
    public List<ScriptSchedule> ScriptSchedules { get; set; } = new();

    public string? TempPassword { get; set; }

    public RemotelyUserOptions? UserOptions { get; set; }
}
