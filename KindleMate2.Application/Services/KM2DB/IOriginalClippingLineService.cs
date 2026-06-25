using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB;

public interface IOriginalClippingLineService {
    OriginalClippingLine? GetOriginalClippingLineByKey(string key);
    List<OriginalClippingLine> GetAllOriginalClippingLines();
    List<OriginalClippingLine> GetByFuzzySearch(string search, AppEntities.SearchType type);
    int GetCount();
    void AddOriginalClippingLine(OriginalClippingLine originalClippingLine);
    void UpdateOriginalClippingLine(OriginalClippingLine originalClippingLine);
    void DeleteOriginalClippingLine(string key);
    bool Export(string filePath, string fileName, out Exception? exception);
}
