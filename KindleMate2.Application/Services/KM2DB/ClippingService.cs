using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB {
    public class ClippingService {
        private readonly IClippingRepository _repository;

        public ClippingService(IClippingRepository repository) {
            _repository = repository;
        }

        public Clipping? GetClippingByKey(string key) {
            return _repository.GetByKey(key);
        }

        public Clipping? GetClippingByKeyAndContent(string key, string content) {
            return _repository.GetByKeyAndContent(key, content);
        }

        public List<Clipping> GetClippingByBookNameAndPageNumberAndBriefType(string bookname, int pagenumber, BriefType brieftype) {
            return _repository.GetByBookNameAndPageNumberAndBriefType(bookname, pagenumber, brieftype);
        }

        public List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            return _repository.GetByFuzzySearch(search, type);
        }

        public List<Clipping> GetAllClippings() {
            return _repository.GetAll();
        }

        public List<Clipping> GetClippingByBookName(string bookname) {
            return _repository.GetByBookName(bookname);
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddClipping(Clipping clipping) {
            if (string.IsNullOrWhiteSpace(clipping.Key)) {
                throw new ArgumentException("[key] cannot be empty");
            }

            _repository.Add(clipping);
        }

        public bool UpdateClipping(Clipping clipping) {
            return _repository.Update(clipping);
        }

        public bool DeleteClipping(string key) {
            return _repository.Delete(key);
        }

        public void DeleteAllClippings() {
            _repository.DeleteAll();
        }

        public List<string> GetClippingsBookTitleList() {
            var list = new List<string>();
            var clippings = _repository.GetAll();
            if (clippings.Count <= 0) {
                return list;
            }
            foreach (Clipping clipping in clippings) {
                var bookTitle = clipping.BookName;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        public List<string> GetClippingsAuthorList() {
            var list = new List<string>();
            var clippings = _repository.GetAll();
            if (clippings.Count <= 0) {
                return list;
            }
            foreach (Clipping clipping in clippings) {
                var bookTitle = clipping.AuthorName;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        public bool RenameBook(string originBookname, string bookname, string authorname) {
            var clippings = _repository.GetByBookName(originBookname);
            var result = 0;
            foreach (Clipping clipping in clippings) {
                clipping.BookName = bookname;
                if (!string.IsNullOrWhiteSpace(authorname)) {
                    clipping.AuthorName = authorname;
                }
                if (_repository.Update(clipping)) {
                    result++;
                }
            }
            return result > 0;
        }
    }
}