using KindleMate2.Domain.Entities.KM3DB;
using KindleMate2.Shared.Entities;
using Type = KindleMate2.Domain.Entities.KM3DB.Type;

namespace KindleMate2.Domain.Interfaces.KM3DB {
    public interface IClippingRepository {
        Clipping? GetByKey(string key);

        Clipping? GetByKeyAndContent(string key, string content);

        List<Clipping> GetByBookNameAndPageNumber(string bookname, int pagenumber);

        List<Clipping> GetByBookNameAndPageNumberAndBriefType(string bookname, int pagenumber, Type brieftype);

        List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type);

        List<Clipping> GetByContent(string content);

        List<Clipping> GetAll();

        List<Clipping> GetByBookName(string bookname);
        
        List<string> GetBookNamesList();

        int GetCount();

        bool Add(Clipping clipping);

        int Add(List<Clipping> listClippings);

        bool Update(Clipping clipping);

        bool UpdateBriefTypeByKey(Clipping clipping);

        bool Delete(string key);

        int Delete(List<Clipping> listClippings);
        
        bool DeleteAll();
    }
}