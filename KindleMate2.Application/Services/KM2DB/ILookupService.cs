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

    /// <summary>
    /// Deletes exactly one lookup row identified by (word_key, timestamp).
    /// </summary>
    bool DeleteLookup(string wordKey, string timestamp);
    bool RenameBook(string originBookname, string bookname, string authorName);

    /// <summary>
    /// 把某个词的**全部**查询行改挂到新的 word_key 上(「重命名生词」用)。
    /// 语义与拒绝条件见 <see cref="KindleMate2.Domain.Interfaces.KM2DB.ILookupRepository.RenameWordKey"/>。
    /// </summary>
    /// <returns>被改写的行数;因键冲突而拒绝时为 -1;新旧键相同为 0。</returns>
    int RenameWordKey(string oldWordKey, string newWordKey);
    bool LookupsToMarkdown(string filePath, string word = "");
}
