using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class BookInfoService(IBookInfoRepository repository) {
        public BookInfo? GetBookInfoById(string key) {
            return repository.GetById(key);
        }

        public IEnumerable<BookInfo> GetAllBookInfos() {
            return repository.GetAll();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddBookInfo(BookInfo bookInfo) {
            if (string.IsNullOrWhiteSpace(bookInfo.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            repository.Add(bookInfo);
        }

        public void UpdateBookInfo(BookInfo bookInfo) {
            repository.Update(bookInfo);
        }

        public void DeleteBookInfo(string id) {
            repository.Delete(id);
        }
    }
}
