using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IOriginalClippingLineRepository {
        OriginalClippingLine? GetByKey(string key);

        List<OriginalClippingLine> GetAll();

        List<OriginalClippingLine> GetByFuzzySearch(string search, AppEntities.SearchType type);

        int GetCount();

        void Add(OriginalClippingLine originalClippingLine);

        void Update(OriginalClippingLine originalClippingLine);

        void Delete(string key);

        void DeleteAll();
    }
}