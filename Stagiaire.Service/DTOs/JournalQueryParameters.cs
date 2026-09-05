using Smartek.Common.Pagination;

namespace Stagiaire.Service.DTOs;

/// <summary>
/// Filters for <c>GET /api/v1/stagiaires/{id}/journal</c>, on top of the inherited
/// <c>page</c>/<c>pageSize</c>.
///
/// Example: <c>?du=2026-09-01&amp;au=2026-10-31&amp;sansCommentaire=true</c>
/// </summary>
public class JournalQueryParameters : PaginationQuery
{
    /// <summary>Only entries whose week falls on or after this date.</summary>
    public DateOnly? Du { get; set; }

    /// <summary>Only entries whose week falls on or before this date.</summary>
    public DateOnly? Au { get; set; }

    /// <summary>
    /// When true, only entries the encadrant has not reviewed yet — the encadrant's "to do" filter.
    /// </summary>
    public bool? SansCommentaire { get; set; }
}
