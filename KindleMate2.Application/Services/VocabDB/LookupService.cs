using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    using Lookup = Lookup;
    using ILookupRepository = ILookupRepository;

    public class LookupService(ILookupRepository repository) {
        public Lookup? GetLookupByKey(string id) {
            return repository.GetById(id);
        }

        public IEnumerable<Lookup> GetAllLookups() {
            return repository.GetAll();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddLookup(Lookup lookup) {
            if (string.IsNullOrWhiteSpace(lookup.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            repository.Add(lookup);
        }

        public void UpdateLookup(Lookup lookup) {
            repository.Update(lookup);
        }

        public void DeleteLookup(string id) {
            repository.Delete(id);
        }
    }
}
