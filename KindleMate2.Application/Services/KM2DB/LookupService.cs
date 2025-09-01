using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB {
    public class LookupService {
        private readonly ILookupRepository _repository;

        public LookupService(ILookupRepository repository) {
            _repository = repository;
        }

        public Lookup? GetLookupByWordKey(string wordKey) {
            return _repository.GetByWordKey(wordKey);
        }

        public List<Lookup> GetAllLookups() {
            return _repository.GetAll();
        }

        public List<Lookup> GetLookupsByTimestamp(string timestamp) {
            return _repository.GetByTimestamp(timestamp);
        }

        public List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            return _repository.GetByFuzzySearch(search, type);
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddLookup(Lookup lookup) {
            if (string.IsNullOrWhiteSpace(lookup.WordKey)) {
                throw new ArgumentException("[word_key] cannot be empty");
            }

            _repository.Add(lookup);
        }

        public void UpdateLookup(Lookup lookup) {
            _repository.Update(lookup);
        }

        public bool DeleteLookup(string wordKey) {
            return _repository.Delete(wordKey);
        }

        public bool RenameBook(string originBookname, string bookname, string authorname) {
            var lookups = _repository.GetByTitle(originBookname);
            var result = 0;
            foreach (Lookup lookup in lookups) {
                lookup.Title = bookname;
                if (!string.IsNullOrWhiteSpace(authorname)) {
                    lookup.Authors = authorname;
                }
                if (_repository.Update(lookup)) {
                    result++;
                }
            }
            return result > 0;
        }
    }
}
