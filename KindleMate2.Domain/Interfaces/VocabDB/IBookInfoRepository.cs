using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IBookInfoRepository {
        BookInfo? GetById(string id);
        List<BookInfo> GetAll();
        int GetCount();
        void Add(BookInfo bookInfo);
        void Update(BookInfo bookInfo);
        void Delete(string id);
    }
}
