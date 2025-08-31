using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface ILookupRepository {
        Lookup? GetById(string id);
        List<Lookup> GetAll();
        int GetCount();
        void Add(Lookup lookup);
        void Update(Lookup lookup);
        void Delete(string id);
    }
}