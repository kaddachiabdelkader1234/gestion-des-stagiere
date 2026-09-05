using System.ComponentModel.DataAnnotations;

namespace Convention.Service.Models;

/// <summary>
/// Lightweight audit trail for Convention.Service: who did what, when, to which entity.
/// Same schema as Stagiaire.Service's AuditEntry for consistency.
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    public long? UserId { get; set; }

    [MaxLength(20)]
    public string? UserRole { get; set; }

    [MaxLength(200)]
    public string? UserEmail { get; set; }

    [Required, MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? EntityId { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? TraceId { get; set; }
}
