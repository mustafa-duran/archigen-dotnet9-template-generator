namespace Core.Application.Requests;

public class PageRequest
{
    private int? _pageIndex;
    private int? _pageSize;

    public int PageIndex
    {
        get => !_pageIndex.HasValue || _pageIndex < 0 ? 0 : _pageIndex.Value;
        set => _pageIndex = value;
    }

    public int PageSize
    {
        get => !_pageSize.HasValue || _pageSize <= 0 ? 10 : _pageSize.Value;
        set => _pageSize = value;
    }

}
