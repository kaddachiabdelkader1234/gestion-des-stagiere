namespace Stagiaire.Service.DTOs;

/// <summary>Read DTO for the admin audit screen.</summary>
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

/// <summary>Query parameters for the audit log endpoint.</summary>
public sealed class AuditQueryParameters
{
    /// <summary>Filter by action type (e.g. CANDIDATURE_ACCEPTED).</summary>
    public string? Action { get; set; }

    /// <summary>Filter by entity type (e.g. Stagiaire, Convention).</summary>
    public string? EntityType { get; set; }

    /// <summary>Filter by user id.</summary>
    public long? UserId { get; set; }

    /// <summary>Start of date range (inclusive, UTC).</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>End of date range (inclusive, UTC).</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Free-text search in details and user email.</summary>
    public string? Search { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Items per page (max 100).</summary>
    public int PageSize { get; set; } = 25;
}
