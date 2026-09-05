using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Smartek.Common.Pagination;

/// <summary>
/// Query-string paging accepted by every list endpoint, e.g.
/// <c>GET /api/v1/stagiaires?page=2&amp;pageSize=20</c>.
///
/// Bounds are enforced rather than trusted: an unbounded pageSize lets one request pull the whole
/// table, which is exactly what the brief's "don't just dump all rows into the frontend" forbids.
/// </summary>
public class PaginationQuery
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values below 1 are clamped to 1.</summary>
    [Range(1, int.MaxValue)]
    [DefaultValue(1)]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Rows per page, clamped to [1, <see cref="MaxPageSize"/>].</summary>
    [Range(1, MaxPageSize)]
    [DefaultValue(DefaultPageSize)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Rows to skip for the current page.</summary>
    public int Skip => (Page - 1) * PageSize;
}
