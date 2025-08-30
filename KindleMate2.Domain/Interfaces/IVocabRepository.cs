using KindleMate2.Domain.Entities;

namespace KindleMate2.Domain.Interfaces {
    public interface IVocabRepository {
        Vocab? GetById(string id);
        IEnumerable<Vocab> GetAll();
        void Add(Vocab vocab);
        void Update(Vocab vocab);
        void Delete(string id);
    }
}