using System.Data;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services;

/// <summary>
/// Manages data querying, display data preparation, and tree/grid population for the main UI.
/// Separates data retrieval and formatting from UI controls.
/// </summary>
public class DataDisplayService : IDataDisplayService {
    private readonly IClippingService _clippingService;
    private readonly IOriginalClippingLineService _originalClippingLineService;
    private readonly IVocabService _vocabService;
    private readonly ILookupService _lookupService;

    public List<Clipping> Clippings { get; private set; } = [];
    public List<OriginalClippingLine> OriginClippings { get; private set; } = [];
    public List<Vocab> Vocabs { get; private set; } = [];
    public List<Lookup> Lookups { get; private set; } = [];

    public DataDisplayService(IClippingService clippingService, IOriginalClippingLineService originalClippingLineService,
        IVocabService vocabService, ILookupService lookupService) {
        _clippingService = clippingService;
        _originalClippingLineService = originalClippingLineService;
        _vocabService = vocabService;
        _lookupService = lookupService;
    }

    /// <summary>
    /// Queries data based on search text and type, populating all collections.
    /// </summary>
    public void QueryData(string searchText, AppEntities.SearchType searchType) {
        if (string.IsNullOrWhiteSpace(searchText)) {
            Clippings = _clippingService.GetAllClippings();
            OriginClippings = _originalClippingLineService.GetAllOriginalClippingLines();
            Vocabs = _vocabService.GetAllVocabs();
            Lookups = _lookupService.GetAllLookups();
        } else {
            Clippings = _clippingService.GetByFuzzySearch(searchText, searchType);
            OriginClippings = _originalClippingLineService.GetByFuzzySearch(searchText, searchType);
            Vocabs = _vocabService.GetByFuzzySearch(searchText, searchType);
            Lookups = _lookupService.GetByFuzzySearch(searchText, searchType);
        }

        // Enrich lookups with stem and frequency from vocabs (O(n+m) via dictionary lookup)
        var vocabMap = Vocabs
            .Where(v => v.WordKey != null)
            .GroupBy(v => v.WordKey!)
            .ToDictionary(g => g.Key, g => g.First());
        foreach (Lookup row in Lookups) {
            if (row.WordKey != null && vocabMap.TryGetValue(row.WordKey, out var vocab)) {
                row.Stem = vocab.Stem ?? string.Empty;
                row.Frequency = vocab.Frequency?.ToString() ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Gets sorted distinct book names for TreeView population.
    /// </summary>
    public List<string> GetDistinctBookNames() {
        return Clippings.Select(row => row.BookName)
            .Distinct()
            .OrderBy(book => book)
            .ToList();
    }

    /// <summary>
    /// Gets sorted distinct word names for TreeView population.
    /// </summary>
    public List<string> GetDistinctWordNames() {
        return Vocabs.Select(row => row.Word)
            .Distinct()
            .OrderBy(word => word)
            .ToList();
    }

    /// <summary>
    /// Gets clippings filtered by book name.
    /// </summary>
    public List<Clipping> GetClippingsByBook(string bookName) {
        return Clippings.Where(row => row.BookName == bookName).ToList();
    }

    /// <summary>
    /// Gets lookups filtered by word.
    /// </summary>
    public List<Lookup> GetLookupsByWord(string word) {
        return Lookups.Where(row => string.Equals(row.Word, word, StringComparison.InvariantCultureIgnoreCase)).ToList();
    }

    /// <summary>
    /// Gets lookups filtered by word key suffix.
    /// </summary>
    public List<Lookup> GetLookupsByWordKeySuffix(string wordKeySuffix) {
        return Lookups.Where(row => row.WordKey?.Length >= 3 && row.WordKey[3..] == wordKeySuffix).ToList();
    }

    /// <summary>
    /// Converts clippings to DataTable for DataGridView binding.
    /// </summary>
    public DataTable ClippingsToDataTable() {
        return DataTableHelper.ToDataTable(Clippings);
    }

    /// <summary>
    /// Converts filtered clippings to DataTable.
    /// </summary>
    public DataTable ClippingsToDataTable(List<Clipping> clippings) {
        return DataTableHelper.ToDataTable(clippings);
    }

    /// <summary>
    /// Converts lookups to DataTable for DataGridView binding.
    /// </summary>
    public DataTable LookupsToDataTable() {
        return DataTableHelper.ToDataTable(Lookups);
    }

    /// <summary>
    /// Converts filtered lookups to DataTable.
    /// </summary>
    public DataTable LookupsToDataTable(List<Lookup> lookups) {
        return DataTableHelper.ToDataTable(lookups);
    }

    /// <summary>
    /// Gets auto-complete suggestions for the search box based on search type.
    /// </summary>
    public List<string> GetSearchSuggestions(AppEntities.SearchType searchType) {
        var list = new List<string>();
        switch (searchType) {
            case AppEntities.SearchType.BookTitle:
                list.AddRange(_clippingService.GetClippingsBookTitleList());
                break;
            case AppEntities.SearchType.Author:
                list.AddRange(_clippingService.GetClippingsAuthorList());
                break;
            case AppEntities.SearchType.Vocabulary:
                list.AddRange(_vocabService.GetVocabWordList());
                break;
            case AppEntities.SearchType.Stem:
                list.AddRange(_vocabService.GetVocabStemList());
                break;
            default:
                list.AddRange(_clippingService.GetClippingsBookTitleList());
                list.AddRange(_clippingService.GetClippingsAuthorList());
                list.AddRange(_vocabService.GetVocabWordList());
                list.AddRange(_vocabService.GetVocabStemList());
                break;
        }
        return list;
    }

    /// <summary>
    /// Gets counts for the status bar display.
    /// </summary>
    public string GetStatusText(int tabIndex) {
        switch (tabIndex) {
            case 0:
                var booksCount = Clippings.Select(r => r.BookName).Distinct().Count();
                var clippingsCount = Clippings.Count;
                var originClippingsCount = OriginClippings.Count;
                var diff = Math.Abs(originClippingsCount - clippingsCount);
                return Strings.Space + Strings.Totally + Strings.Space + booksCount + Strings.Space + Strings.X_Books +
                       Strings.Symbol_Comma + clippingsCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
                       Strings.Deleted_X + Strings.Space + diff + Strings.Space + Strings.X_Rows;
            case 1:
                var vocabCount = Vocabs.Count;
                var lookupsCount = Lookups.Count;
                return Strings.Space + Strings.Totally + Strings.Space + vocabCount + Strings.Space + Strings.X_Vocabs +
                       Strings.Symbol_Comma + Strings.Quried_X + Strings.Space + lookupsCount + Strings.Space + Strings.X_Times;
            default:
                return string.Empty;
        }
    }
}
