using KindleMate2.Domain.Interfaces.VocabDB;
using Version = KindleMate2.Domain.Entities.VocabDB.Version;

namespace KindleMate2.Application.Services.VocabDB {
    public class VersionService(IVersionRepository repository) {
        public Version? GetVersionByKey(string id) {
            return repository.GetById(id);
        }

        public IEnumerable<Version> GetAllVersions() {
            return repository.GetAll();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddVersion(Version version) {
            if (string.IsNullOrWhiteSpace(version.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            repository.Add(version);
        }

        public void UpdateVersion(Version version) {
            repository.Update(version);
        }

        public void DeleteVersion(string id) {
            repository.Delete(id);
        }
    }
}