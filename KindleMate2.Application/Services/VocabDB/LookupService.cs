namespace KindleMate2.Application.Services.VocabDB {
    using Lookup = Domain.Entities.VocabDB.Lookup;
    using ILookupRepository = Domain.Interfaces.VocabDB.ILookupRepository;

    public class LookupService {
        private readonly ILookupRepository _repository;

        public LookupService(ILookupRepository repository) {
            _repository = repository;
        }

        public Lookup? GetLookupByKey(string id) {
            return _repository.GetById(id);
        }

        public IEnumerable<Lookup> GetAllLookups() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddLookup(Lookup lookup) {
            if (string.IsNullOrWhiteSpace(lookup.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(lookup);
        }

        public void UpdateLookup(Lookup lookup) {
            _repository.Update(lookup);
        }

        public void DeleteLookup(string id) {
            _repository.Delete(id);
        }
    }
}
