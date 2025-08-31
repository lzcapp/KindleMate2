using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IWordRepository {
        Word? GetById(string id);
        List<Word> GetAll();
        int GetCount();
        void Add(Word word);
        void Update(Word word);
        void Delete(string id);
    }
}