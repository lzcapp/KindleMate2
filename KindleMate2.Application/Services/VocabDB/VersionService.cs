using KindleMate2.Domain.Interfaces.VocabDB;
using Version = KindleMate2.Domain.Entities.VocabDB.Version;

namespace KindleMate2.Application.Services.VocabDB {
    public class VersionService {
        private readonly IVersionRepository _repository;

        public VersionService(IVersionRepository repository) {
            _repository = repository;
        }

        public Version? GetVersionByKey(string id) {
            return _repository.GetById(id);
        }

        public IEnumerable<Version> GetAllVersions() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddVersion(Version version) {
            if (string.IsNullOrWhiteSpace(version.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(version);
        }

        public void UpdateVersion(Version version) {
            _repository.Update(version);
        }

        public void DeleteVersion(string id) {
            _repository.Delete(id);
        }
    }
}