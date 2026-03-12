namespace Fennec.App.Routing;

public interface ISearchableRoute
{
    string SearchWatermark { get; }
    void ApplySearch(string query);
    void ClearSearch();
}
