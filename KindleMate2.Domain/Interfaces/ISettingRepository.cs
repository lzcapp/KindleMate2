using KindleMate2.Domain.Entities;

namespace KindleMate2.Domain.Interfaces {
    public interface ISettingRepository {
        Setting? GetByName(string name);
        IEnumerable<Setting> GetAll();
        void Add(Setting setting);
        void Update(Setting setting);
        void Delete(string name);
    }
}