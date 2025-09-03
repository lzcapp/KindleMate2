using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface ILookupRepository {
        Lookup? GetById(string id);

        List<Lookup> GetAll();

        int GetCount();

        bool Add(Lookup lookup);

        bool Update(Lookup lookup);

        bool Delete(string id);
    }
}