using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface ILookupRepository {
        Lookup? GetByWordKey(string wordKey);
        Lookup? GetByTimestamp(string timeStamp);
        List<Lookup> GetAll();
        int GetCount();
        void Add(Lookup lookup);
        void Update(Lookup lookup);
        void Delete(string wordKey);
    }
}