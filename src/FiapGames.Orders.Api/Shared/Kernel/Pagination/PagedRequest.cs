namespace FiapGames.Shared.Kernel.Pagination;

public sealed class PagedRequest
{
    private const int MaxPageSize = 100;

    private int? _page;
    private int? _pageSize;

    // Nullable so ASP.NET Core's [AsParameters] binding treats page/pageSize
    // as optional query parameters — a non-nullable int here makes the
    // framework reject any request that omits them with an empty-body 400
    // before the handler ever runs.
    public int? Page
    {
        get => _page;
        set => _page = value is null or < 1 ? 1 : value;
    }

    public int? PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            null => 10,
            < 1 => 10,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public int Skip => ((Page ?? 1) - 1) * (PageSize ?? 10);
}
