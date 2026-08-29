namespace ERP.Shared.Models;

public sealed class ApiResponse<T>
{
    public T Data { get; init; } = default!;
    public static ApiResponse<T> Ok(T data) => new() { Data = data };
}

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = Array.Empty<T>();
    public PaginationMeta Pagination { get; init; } = new();
}

public sealed class PaginationMeta
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ApiError
{
    public string Code { get; init; } = "ERROR";
    public string Message { get; init; } = "An error occurred.";
    public string? TraceId { get; init; }
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }
}

public sealed class ApiErrorResponse
{
    public ApiError Error { get; init; } = new();
}
