using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IBookInfoRepository {
        BookInfo? GetById(string id);

        List<BookInfo> GetAll();

        int GetCount();

        bool Add(BookInfo bookInfo);

        bool Update(BookInfo bookInfo);

        bool Delete(string id);
    }
}