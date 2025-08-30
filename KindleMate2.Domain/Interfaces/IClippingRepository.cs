using KindleMate2.Domain.Entities;

namespace KindleMate2.Domain.Interfaces {
    public interface IClippingRepository {
        Clipping? GetByKey(string key);
        IEnumerable<Clipping> GetAll();
        void Add(Clipping clipping);
        void Update(Clipping clipping);
        void Delete(string key);
    }
}
