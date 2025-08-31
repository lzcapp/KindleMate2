using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface ISettingRepository {
        Setting? GetByName(string name);
        List<Setting> GetAll();
        int GetCount();
        void Add(Setting setting);
        void Update(Setting setting);
        void Delete(string name);
    }
}