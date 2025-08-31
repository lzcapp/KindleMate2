using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IClippingRepository {
        Clipping? GetByKey(string key);
        Clipping? GetByKeyAndContent(string key, string content);
        List<Clipping> GetAll();
        int GetCount();
        void Add(Clipping clipping);
        void Update(Clipping clipping);
        void Delete(string key);
    }
}
