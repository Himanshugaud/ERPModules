namespace ERP.Shared.Pagination;

public class PageRequest
{
    public const int MaxPageSize = 100;
    private int _pageSize = 25;
    private int _page = 1;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 25 : (value > MaxPageSize ? MaxPageSize : value);
    }

    public string? Sort { get; set; }
    public int Skip => (Page - 1) * PageSize;
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public long TotalItems { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
