using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Remotely.Shared.Entities;

public class UserOrganizationMembership
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    [JsonIgnore]
    public RemotelyUser? User { get; set; }

    public string OrganizationId { get; set; } = null!;

    [JsonIgnore]
    public Organization? Organization { get; set; }

    public bool IsAdministrator { get; set; }

    public bool CanInvite { get; set; }
}
