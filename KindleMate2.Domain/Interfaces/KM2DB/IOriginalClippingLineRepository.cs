using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IOriginalClippingLineRepository {
        OriginalClippingLine? GetByKey(string key);

        List<OriginalClippingLine> GetAll();

        List<OriginalClippingLine> GetByFuzzySearch(string search, AppEntities.SearchType type);

        int GetCount();

        bool Add(OriginalClippingLine originalClippingLine);

        bool Update(OriginalClippingLine originalClippingLine);

        bool Delete(string key);

        bool DeleteAll();
    }
}