namespace Evaluation.Service.DTOs;

/// <summary>
/// Read DTO for the admin audit screen — identical schema to Stagiaire.Service's
/// <c>AuditEntryReadDto</c> so the frontend can merge results from all three services.
/// </summary>
public sealed class AuditEntryReadDto
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public string? UserRole { get; set; }
    public string? UserEmail { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
    public string? TraceId { get; set; }
}

/// <summary>
/// Query parameters for the audit log endpoint — mirrors Stagiaire.Service's
/// <c>AuditQueryParameters</c>.
/// </summary>
public sealed class AuditQueryParameters
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public long? UserId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
