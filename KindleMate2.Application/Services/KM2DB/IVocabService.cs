using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB;

public interface IVocabService {
    Vocab? GetVocabByName(string id);
    List<Vocab> GetAllVocabs();
    List<Vocab> GetByFuzzySearch(string searchText, AppEntities.SearchType searchType);
    List<string> GetWordsList();
    int GetCount();
    void AddVocab(Vocab vocab);
    void UpdateVocab(Vocab vocab);
    void DeleteVocab(string id);
    bool DeleteVocabByWordKey(string wordKey);

    /// <summary>批量删除全部生词(左栏「全部生词」节点)—— 软删,重导入不复活;整库重来用「清空全部数据」。</summary>
    void DeleteAllVocabs();
    List<string> GetVocabWordList();
    List<string> GetVocabStemList();
}
