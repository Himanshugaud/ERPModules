using ERP.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public static class PagingExtensions
{
    /// <summary>
    /// Total row count for a paged list. When the first page came back short the page already holds every row,
    /// so the extra COUNT round trip to the database is skipped.
    /// </summary>
    public static async Task<long> TotalAsync<T>(this IQueryable<T> unpaged, PageRequest filter, int returned, CancellationToken ct) =>
        filter.Page <= 1 && returned < filter.PageSize ? returned : await unpaged.LongCountAsync(ct);
}
