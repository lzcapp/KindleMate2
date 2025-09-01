using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IClippingRepository {
        Clipping? GetByKey(string key);
        Clipping? GetByKeyAndContent(string key, string content);
        List<Clipping> GetByBookNameAndPageNumber(string bookname, int pagenumber);
        List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type);
        List<Clipping> GetAll();
        int GetCount();
        void Add(Clipping clipping);
        void Update(Clipping clipping);
        void UpdateBriefTypeByKey(Clipping clipping);
        void Delete(string key);
    }
    
    
}
