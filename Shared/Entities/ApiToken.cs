using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Remotely.Shared.Entities;

public class ApiToken
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string ID { get; set; } = null!;

    public DateTimeOffset? LastUsed { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    [JsonIgnore]
    public Organization? Organization { get; set; }

    // Retained nullable per spec §6.1 / FR-28 for legacy-token detection (FR-31).
    // Dropped in a follow-on migration once all legacy tokens have cycled out (OI-08).
    public string? OrganizationID { get; set; }

    // Added (Phase 3 spec extension): FK to the user who created the token.
    // Required to support FR-28 "tokens authenticate user identity only" and to
    // give GetAllApiTokens(userId) sensible per-user scope. Nullable to tolerate
    // legacy rows pre-migration; new tokens always set this.
    public string? CreatorId { get; set; }

    [JsonIgnore]
    public RemotelyUser? Creator { get; set; }

    public string? Secret { get; set; }
}
