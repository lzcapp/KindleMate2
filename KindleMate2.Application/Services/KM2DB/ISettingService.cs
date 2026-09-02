using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Application.Services.KM2DB;

public interface ISettingService {
    Setting? GetSettingByName(string name);
    IEnumerable<Setting> GetAllSettings();
    int GetCount();
    void AddSetting(Setting setting);
    void UpdateSetting(Setting setting);
    void DeleteSetting(string name);
}
