using KindleMate2.Domain.Entities;

namespace KindleMate2.Domain.Interfaces {
    public interface ILookupRepository {
        Lookup? GetByWordKey(string wordKey);
        IEnumerable<Lookup> GetAll();
        void Add(Lookup lookup);
        void Update(Lookup lookup);
        void Delete(string wordKey);
    }
}