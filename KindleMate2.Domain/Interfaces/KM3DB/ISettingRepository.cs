using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM3DB {
    public interface ISettingRepository {
        Setting? GetByName(string name);

        List<Setting> GetAll();

        int GetCount();

        bool Add(Setting setting);

        bool Update(Setting setting);

        bool Delete(string name);

        bool DeleteAll();
    }
}