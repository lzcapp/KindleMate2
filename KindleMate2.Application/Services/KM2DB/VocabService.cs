using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB {
    public class VocabService {
        private readonly IVocabRepository _repository;

        public VocabService(IVocabRepository repository) {
            _repository = repository;
        }

        public Vocab? GetVocabByName(string id) {
            return _repository.GetById(id);
        }

        public List<Vocab> GetAllVocabs() {
            return _repository.GetAll();
        }

        public List<Vocab> GetByFuzzySearch(string searchText, AppEntities.SearchType searchType) {
            return _repository.GetByFuzzySearch(searchText, searchType);
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddVocab(Vocab vocab) {
            if (string.IsNullOrWhiteSpace(vocab.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(vocab);
        }

        public void UpdateVocab(Vocab vocab) {
            _repository.Update(vocab);
        }

        public void DeleteVocab(string id) {
            _repository.Delete(id);
        }

        public bool DeleteVocabByWordKey(string word_key) {
            return _repository.DeleteByWordKey(word_key);
        }

        public void DeleteAllVocabs() {
            _repository.DeleteAll();
        }

        public List<string> GetVocabWordList() {
            var list = new List<string>();
            var vocabs = _repository.GetAll();
            if (vocabs.Count <= 0) {
                return list;
            }
            foreach (Vocab vocab in vocabs) {
                var bookTitle = vocab.Word;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        public List<string> GetVocabStemList() {
            var list = new List<string>();
            var vocabs = _repository.GetAll();
            if (vocabs.Count <= 0) {
                return list;
            }
            foreach (Vocab vocab in vocabs) {
                var bookTitle = vocab.Stem;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }
    }
}
