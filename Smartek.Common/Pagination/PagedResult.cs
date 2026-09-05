using Microsoft.EntityFrameworkCore;

namespace Smartek.Common.Pagination;

/// <summary>
/// One page of results plus the metadata a table needs to render its pager.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Rows matching the filter across all pages — not the size of <see cref="Items"/>.</summary>
    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(PaginationQuery query) => new()
    {
        Items = Array.Empty<T>(),
        TotalCount = 0,
        Page = query.Page,
        PageSize = query.PageSize
    };
}

public static class QueryableExtensions
{
    /// <summary>
    /// Runs COUNT then the page query, both server-side.
    /// </summary>
    /// <remarks>
    /// Callers must apply a deterministic <c>OrderBy</c> first: without one, PostgreSQL may return
    /// rows in any order and OFFSET/LIMIT paging can then repeat or skip rows between pages.
    /// </remarks>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await source.CountAsync(cancellationToken);

        // Skip the second round-trip when the filter matched nothing.
        if (totalCount == 0)
        {
            return PagedResult<T>.Empty(query);
        }

        var items = await source
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}
