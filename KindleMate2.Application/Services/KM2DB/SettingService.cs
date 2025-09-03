using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Application.Services.KM2DB {
    public class SettingService {
        private readonly ISettingRepository _repository;

        public SettingService(ISettingRepository repository) {
            _repository = repository;
        }

        public Setting? GetSettingByName(string name) {
            return _repository.GetByName(name);
        }

        public IEnumerable<Setting> GetAllSettings() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddSetting(Setting setting) {
            if (string.IsNullOrWhiteSpace(setting.Name)) {
                throw new ArgumentException("[name] cannot be empty");
            }

            _repository.Add(setting);
        }

        public void UpdateSetting(Setting setting) {
            if (GetSettingByName(setting.Name) == null) {
                _repository.Add(setting);
            }
            _repository.Update(setting);
        }

        public void DeleteSetting(string name) {
            _repository.Delete(name);
        }
    }
}
