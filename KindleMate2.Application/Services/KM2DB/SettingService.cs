using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Application.Services.KM2DB {
    public class SettingService(ISettingRepository repository) {
        public Setting? GetSettingByName(string name) {
            return repository.GetByName(name);
        }

        public IEnumerable<Setting> GetAllSettings() {
            return repository.GetAll();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddSetting(Setting setting) {
            if (string.IsNullOrWhiteSpace(setting.Name)) {
                throw new ArgumentException("[name] cannot be empty");
            }

            repository.Add(setting);
        }

        public void UpdateSetting(Setting setting) {
            if (GetSettingByName(setting.Name) == null) {
                repository.Add(setting);
            }
            repository.Update(setting);
        }

        public void DeleteSetting(string name) {
            repository.Delete(name);
        }
    }
}
