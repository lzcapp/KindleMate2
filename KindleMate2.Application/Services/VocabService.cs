using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;

namespace KindleMate2.Application.Services {
    public class VocabService {
        private readonly IVocabRepository _repository;

        public VocabService(IVocabRepository repository) {
            _repository = repository;
        }

        public IEnumerable<Vocab> GetAllSettings() {
            return _repository.GetAll();
        }

        public Vocab? GetSettingByName(string id) {
            return _repository.GetById(id);
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
