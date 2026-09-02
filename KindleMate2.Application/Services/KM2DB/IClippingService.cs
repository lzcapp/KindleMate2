using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB;

public interface IClippingService {
    Clipping? GetClippingByKey(string key);
    Clipping? GetClippingByKeyAndContent(string key, string content);
    List<Clipping> GetClippingsByBookNameAndPageNumberAndBriefType(string bookName, int pageNumber, BriefType briefType);
    List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type);
    List<Clipping> GetAllClippings();
    List<Clipping> GetClippingsByBookName(string bookname);
    List<string> GetBookNamesList();
    int GetCount();
    void AddClipping(Clipping clipping);
    bool UpdateClipping(Clipping clipping);
    bool DeleteClipping(string key);
    void DeleteAllClippings();
    List<string> GetClippingsBookTitleList();
    List<string> GetClippingsAuthorList();
    bool RenameBook(string originBookname, string bookname, string authorname);
    bool ClippingsToMarkdown(string filePath, string bookName = "");
}
