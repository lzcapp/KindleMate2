using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IOriginalClippingLineRepository {
        OriginalClippingLine? GetByKey(string key);

        List<OriginalClippingLine> GetAll();

        HashSet<string> GetAllKeys();

        List<OriginalClippingLine> GetByFuzzySearch(string search, AppEntities.SearchType type);

        int GetCount();

        bool Add(OriginalClippingLine originalClippingLine);

        int Add(List<OriginalClippingLine> listOriginalClippings);

        bool Update(OriginalClippingLine originalClippingLine);

        bool Delete(string key);

        bool DeleteAll();
    }
}