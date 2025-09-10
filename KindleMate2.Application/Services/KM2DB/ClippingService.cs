using System.Data;
using System.Text;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;
using Markdig;

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

        public List<Clipping> GetClippingsByBookNameAndPageNumberAndBriefType(string bookname, int pagenumber, BriefType brieftype) {
            return _repository.GetByBookNameAndPageNumberAndBriefType(bookname, pagenumber, brieftype);
        }

        public List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            return _repository.GetByFuzzySearch(search, type);
        }

        public List<Clipping> GetAllClippings() {
            return _repository.GetAll();
        }

        public List<Clipping> GetClippingsByBookName(string bookname) {
            return _repository.GetByBookName(bookname);
        }

        public List<string> GetBookNamesList() {
            return _repository.GetBookNamesList();
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
        
        public bool ClippingsToMarkdown(string filePath, string bookname = "") {
            try {
                string filename;

                var listClippings = GetAllClippings();

                var markdown = new StringBuilder();

                markdown.AppendLine("# \ud83d\udcda " + Strings.Books);

                markdown.AppendLine();

                if (string.IsNullOrWhiteSpace(bookname) || bookname.Equals(Strings.Select_All)) {
                    filename = "Clippings";
                    
                    markdown.AppendLine("[TOC]");

                    markdown.AppendLine();

                    foreach (var bookName in GetBookNamesList()) {
                        var clippings = listClippings.AsEnumerable().Where(row => row.BookName != null && row.BookName.Equals(bookName)).ToList();
                        markdown.Append(StringHelper.BuildMarkdownWithClippings(clippings));
                    }
                } else {
                    filename = StringHelper.SanitizeFilename(bookname);

                    var clippings = listClippings.AsEnumerable().Where(row => row.BookName != null && row.BookName.Equals(bookname)).ToList();
                    var filteredBooks = DataTableHelper.ToDataTable(clippings);

                    if (filteredBooks.Rows.Count <= 0) {
                        return false;
                    }

                    markdown.AppendLine("## \ud83d\udcd6 " + bookname.Trim());

                    markdown.AppendLine();

                    foreach (DataRow row in filteredBooks.Rows) {
                        var clippingLocation = row[Columns.ClippingTypeLocation].ToString();
                        var content = row[Columns.Content].ToString();

                        markdown.AppendLine("**" + clippingLocation + "**");

                        markdown.AppendLine();

                        markdown.AppendLine(content);

                        markdown.AppendLine();
                    }
                }

                if (!Directory.Exists(filePath)) {
                    Directory.CreateDirectory(filePath);
                }

                File.WriteAllText(Path.Combine(filePath, filename + ".md"), markdown.ToString(), Encoding.UTF8);

                var htmlContent = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";

                MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTableOfContent().Build();
                htmlContent += Markdown.ToHtml(markdown.ToString(), pipeline);

                htmlContent += "\r\n</body>\r\n</html>";

                File.WriteAllText(Path.Combine(filePath, filename + ".html"), htmlContent, Encoding.UTF8);

                File.WriteAllText(Path.Combine(filePath, "styles.css"), AppConstants.Css);

                return true;
            } catch (Exception) {
                return false;
            }
        }
    }
}