namespace ReactPosApi.DTOs;

/// <summary>
/// Generic wrapper for paginated API responses.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

/// <summary>
/// Query parameters accepted by paginated endpoints.
/// </summary>
public class PaginationQuery
{
    private int _page = 1;
    private int _pageSize = 10;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : (value > 100 ? 100 : value);
    }

    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}
