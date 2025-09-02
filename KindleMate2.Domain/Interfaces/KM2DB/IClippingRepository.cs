using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IClippingRepository {
        Clipping? GetByKey(string key);

        Clipping? GetByKeyAndContent(string key, string content);

        List<Clipping> GetByBookNameAndPageNumber(string bookname, int pagenumber);

        List<Clipping> GetByBookNameAndPageNumberAndBriefType(string bookname, int pagenumber, BriefType brieftype);

        List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type);

        List<Clipping> GetByContent(string content);

        List<Clipping> GetAll();

        List<Clipping> GetByBookName(string bookname);

        int GetCount();

        bool Add(Clipping clipping);

        bool Update(Clipping clipping);

        bool UpdateBriefTypeByKey(Clipping clipping);

        bool Delete(string key);
        
        bool DeleteAll();
    }
}