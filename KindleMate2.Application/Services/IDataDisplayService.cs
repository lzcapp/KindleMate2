using System.Data;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services;

public interface IDataDisplayService {
    List<Clipping> Clippings { get; }
    List<OriginalClippingLine> OriginClippings { get; }
    List<Vocab> Vocabs { get; }
    List<Lookup> Lookups { get; }
    void QueryData(string searchText, AppEntities.SearchType searchType);
    List<string> GetDistinctBookNames();
    List<string> GetDistinctWordNames();
    List<Clipping> GetClippingsByBook(string bookName);
    List<Lookup> GetLookupsByWord(string word);
    List<Lookup> GetLookupsByWordKeySuffix(string wordKeySuffix);
    DataTable ClippingsToDataTable();
    DataTable ClippingsToDataTable(List<Clipping> clippings);
    DataTable LookupsToDataTable();
    DataTable LookupsToDataTable(List<Lookup> lookups);
    List<string> GetSearchSuggestions(AppEntities.SearchType searchType);
    string GetStatusText(int tabIndex);
}
