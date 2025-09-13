using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface ILookupRepository {
        Lookup? GetByWordKey(string wordKey);

        List<Lookup> GetAll();

        List<Lookup> GetByTimestamp(string timeStamp);

        List<Lookup> GetByTitle(string title);

        List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type);
        
        List<string> GetWordKeysList();

        int GetCount();

        bool Add(Lookup lookup);

        bool Update(Lookup lookup);

        bool Delete(string wordKey);

        bool DeleteAll();
    }
}