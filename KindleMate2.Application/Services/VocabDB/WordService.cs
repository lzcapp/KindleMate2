using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class WordService(IWordRepository repository) {
        public Word? GetWordByName(string id) {
            return repository.GetById(id);
        }

        public IEnumerable<Word> GetAllWords() {
            return repository.GetAll();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddWord(Word word) {
            if (string.IsNullOrWhiteSpace(word.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            repository.Add(word);
        }

        public void UpdateWord(Word word) {
            repository.Update(word);
        }

        public void DeleteWord(string id) {
            repository.Delete(id);
        }
    }
}
