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
    /// 把某个词的**全部**查询行并到另一个键上(「重命名生词」撞名时用)——
    /// 与目标键重号的行会被丢掉(它们本就是同一条记录)。
    /// 详细语义见 <see cref="KindleMate2.Domain.Interfaces.KM2DB.ILookupRepository.MergeWordKey"/>。
    /// </summary>
    /// <returns>被改写(搬走)的行数。</returns>
    int MergeWordKey(string oldWordKey, string newWordKey);
    bool LookupsToMarkdown(string filePath, string word = "");
}
