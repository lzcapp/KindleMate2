using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IOriginalClippingLineRepository {
        OriginalClippingLine? GetByKey(string key);
        List<OriginalClippingLine> GetAll();
        int GetCount();
        void Add(OriginalClippingLine originalClippingLine);
        void Update(OriginalClippingLine originalClippingLine);
        void Delete(string key);
    }
}