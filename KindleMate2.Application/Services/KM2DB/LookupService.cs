using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

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

        public void DeleteLookup(string wordKey) {
            _repository.Delete(wordKey);
        }
    }
}
