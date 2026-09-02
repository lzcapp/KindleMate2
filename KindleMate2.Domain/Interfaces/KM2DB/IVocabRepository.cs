using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IVocabRepository {
        Vocab? GetById(string id);

        List<Vocab> GetAll();

        List<Vocab> GetByFuzzySearch(string searchText, AppEntities.SearchType searchType);

        List<string> GetWordsList();

        int GetCount();

        bool Add(Vocab vocab);

        int Add(List<Vocab> vocabs);

        bool Update(Vocab vocab);

        bool UpdateFrequencyByWordKey(Vocab vocab);

        bool Delete(string id);

        bool DeleteByWordKey(string wordKey);

        bool DeleteAll();
    }
}