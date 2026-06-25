using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB;

public interface ILookupService {
    Lookup? GetLookupByWordKey(string wordKey);
    List<Lookup> GetAllLookups();
    List<Lookup> GetLookupsByTimestamp(string timestamp);
    List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type);
    List<string> GetWordKeysList();
    int GetCount();
    void AddLookup(Lookup lookup);
    void UpdateLookup(Lookup lookup);
    bool DeleteLookup(string wordKey);
    bool RenameBook(string originBookname, string bookname, string authorName);
    bool LookupsToMarkdown(string filePath, string word = "");
}
