using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IVocabRepository {
        Vocab? GetById(string id);
        List<Vocab> GetAll();
        int GetCount();
        void Add(Vocab vocab);
        void Update(Vocab vocab);
        void UpdateFrequencyByWordKey(Vocab vocab);
        void Delete(string id);
    }
}