using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class WordService {
        private readonly IWordRepository _repository;

        public WordService(IWordRepository repository) {
            _repository = repository;
        }

        public Word? GetWordByName(string id) {
            return _repository.GetById(id);
        }

        public IEnumerable<Word> GetAllWords() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddWord(Word word) {
            if (string.IsNullOrWhiteSpace(word.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(word);
        }

        public void UpdateWord(Word word) {
            _repository.Update(word);
        }

        public void DeleteWord(string id) {
            _repository.Delete(id);
        }
    }
}
