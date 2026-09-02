using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Application.Services;

public interface IContentDetailService {
    ClippingDetailInfo BuildClippingDetail(string bookName, string authorName, int pageNumber,
        string content, string briefType, string key);
    VocabDetailInfo BuildVocabDetail(string wordKey, string word, string stem, string frequency,
        List<Lookup> allLookups, List<Clipping> allClippings);
}
