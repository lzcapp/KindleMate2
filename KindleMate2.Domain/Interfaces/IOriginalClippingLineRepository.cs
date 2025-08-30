using KindleMate2.Domain.Entities;

namespace KindleMate2.Domain.Interfaces {
    public interface IOriginalClippingLineRepository {
        OriginalClippingLine? GetByKey(string key);
        IEnumerable<OriginalClippingLine> GetAll();
        void Add(OriginalClippingLine originalClippingLine);
        void Update(OriginalClippingLine originalClippingLine);
        void Delete(string key);
    }
}