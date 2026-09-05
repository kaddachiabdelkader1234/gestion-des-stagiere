using System.ComponentModel.DataAnnotations;

namespace Stagiaire.Service.Models;

/// <summary>
/// Lightweight audit trail: who did what, when, to which entity.
/// For a bank application this is more important than in most apps — "who accepted this candidature"
/// needs to be answerable months later, not just "someone did".
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }

    /// <summary>Action performed: CANDIDATURE_ACCEPTED, EVALUATION_VALIDATED, etc.</summary>
    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>auth-service user_id of the caller.</summary>
    public long? UserId { get; set; }

    /// <summary>Role of the caller at the time of the action (ADMIN, TRAINER, LEARNER).</summary>
    [MaxLength(20)]
    public string? UserRole { get; set; }

    /// <summary>Email of the caller, denormalized so the admin screen doesn't need to call auth-service.</summary>
    [MaxLength(200)]
    public string? UserEmail { get; set; }

    /// <summary>Entity type affected: Stagiaire, Convention, Evaluation, JournalEntry, Encadrant.</summary>
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the affected entity (Guid or long, stored as string).</summary>
    [MaxLength(100)]
    public string? EntityId { get; set; }

    /// <summary>Optional human-readable summary: "Candidature de Jean Dupont acceptée, encadrant: M. Karim"</summary>
    [MaxLength(500)]
    public string? Details { get; set; }

    /// <summary>When the action occurred (UTC).</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Optional: the trace ID from the request, linking the audit entry to the full request log.</summary>
    [MaxLength(100)]
    public string? TraceId { get; set; }
}
