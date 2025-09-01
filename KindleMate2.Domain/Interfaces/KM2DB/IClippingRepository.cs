using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IClippingRepository {
        Clipping? GetByKey(string key);
        Clipping? GetByKeyAndContent(string key, string content);
        List<Clipping> GetByBookNameAndPageNumber(string bookname, int pagenumber);
        List<Clipping> GetByFuzzySearch(string search, SearchType type);
        List<Clipping> GetAll();
        int GetCount();
        void Add(Clipping clipping);
        void Update(Clipping clipping);
        void UpdateBriefTypeByKey(Clipping clipping);
        void Delete(string key);
    }
    
    
}
