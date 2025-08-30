using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;

namespace KindleMate2.Application.Services {
    public class LookupService {
        private readonly ILookupRepository _repository;

        public LookupService(ILookupRepository repository) {
            _repository = repository;
        }

        public IEnumerable<Lookup> GetAllLookups() {
            return _repository.GetAll();
        }

        public Lookup? GetLookupByWordKey(string wordKey) {
            return _repository.GetByWordKey(wordKey);
        }

        public void AddLookup(Lookup lookup) {
            if (string.IsNullOrWhiteSpace(lookup.word_key)) {
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
