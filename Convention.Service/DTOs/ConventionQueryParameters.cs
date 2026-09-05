using Smartek.Common.Pagination;
using Convention.Service.Models;

namespace Convention.Service.DTOs;

/// <summary>
/// Filters for <c>GET /api/v1/conventions</c>, on top of <c>page</c>/<c>pageSize</c>.
/// </summary>
public class ConventionQueryParameters : PaginationQuery
{
    /// <summary>Signature status (BROUILLON / SIGNEE equivalent). Omit for all.</summary>
    public StatutSignature? StatutSignature { get; set; }

    /// <summary>Restrict to one stagiaire's convention — used by the stagiaire's own dashboard.</summary>
    public Guid? StagiaireId { get; set; }
}
