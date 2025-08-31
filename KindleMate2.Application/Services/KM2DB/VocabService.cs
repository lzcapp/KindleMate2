using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Application.Services.KM2DB {
    public class VocabService {
        private readonly IVocabRepository _repository;

        public VocabService(IVocabRepository repository) {
            _repository = repository;
        }

        public Vocab? GetSettingByName(string id) {
            return _repository.GetById(id);
        }

        public IEnumerable<Vocab> GetAllSettings() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddSetting(Vocab vocab) {
            if (string.IsNullOrWhiteSpace(vocab.id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(vocab);
        }

        public void UpdateSetting(Vocab vocab) {
            _repository.Update(vocab);
        }

        public void DeleteSetting(string id) {
            _repository.Delete(id);
        }
    }
}
