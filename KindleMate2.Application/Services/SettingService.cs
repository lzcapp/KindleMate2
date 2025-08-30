using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;

namespace KindleMate2.Application.Services {
    public class SettingService {
        private readonly ISettingRepository _repository;

        public SettingService(ISettingRepository repository) {
            _repository = repository;
        }

        public IEnumerable<Setting> GetAllSettings() {
            return _repository.GetAll();
        }

        public Setting? GetSettingByName(string name) {
            return _repository.GetByName(name);
        }

        public void AddSetting(Setting setting) {
            if (string.IsNullOrWhiteSpace(setting.name)) {
                throw new ArgumentException("[name] cannot be empty");
            }

            _repository.Add(setting);
        }

        public void UpdateSetting(Setting setting) {
            _repository.Update(setting);
        }

        public void DeleteSetting(string name) {
            _repository.Delete(name);
        }
    }
}
