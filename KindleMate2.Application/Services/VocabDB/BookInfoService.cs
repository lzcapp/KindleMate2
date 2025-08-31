using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class BookInfoService {
        private readonly IBookInfoRepository _repository;

        public BookInfoService(IBookInfoRepository repository) {
            _repository = repository;
        }

        public BookInfo? GetBookInfoById(string key) {
            return _repository.GetById(key);
        }

        public IEnumerable<BookInfo> GetAllBookInfos() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddBookInfo(BookInfo bookInfo) {
            if (string.IsNullOrWhiteSpace(bookInfo.id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(bookInfo);
        }

        public void UpdateBookInfo(BookInfo bookInfo) {
            _repository.Update(bookInfo);
        }

        public void DeleteBookInfo(string id) {
            _repository.Delete(id);
        }
    }
}
