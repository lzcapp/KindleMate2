using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IWordRepository {
        Word? GetById(string id);

        List<Word> GetAll();

        int GetCount();

        bool Add(Word word);

        bool Update(Word word);

        bool Delete(string id);
    }
}