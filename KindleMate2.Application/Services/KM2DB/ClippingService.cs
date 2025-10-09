using System.Text;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;
using Markdig;

namespace KindleMate2.Application.Services.KM2DB {
    public class ClippingService(IClippingRepository repository) {
        public Clipping? GetClippingByKey(string key) {
            return repository.GetByKey(key);
        }

        public Clipping? GetClippingByKeyAndContent(string key, string content) {
            return repository.GetByKeyAndContent(key, content);
        }

        public List<Clipping> GetClippingsByBookNameAndPageNumberAndBriefType(string bookName, int pageNumber, BriefType briefType) {
            return repository.GetByBookNameAndPageNumberAndBriefType(bookName, pageNumber, briefType);
        }

        public List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            return repository.GetByFuzzySearch(search, type);
        }

        public List<Clipping> GetAllClippings() {
            return repository.GetAll();
        }

        public List<Clipping> GetClippingsByBookName(string bookname) {
            return repository.GetByBookName(bookname);
        }

        public List<string> GetBookNamesList() {
            return repository.GetBookNamesList();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddClipping(Clipping clipping) {
            if (string.IsNullOrWhiteSpace(clipping.Key)) {
                throw new ArgumentException("[key] cannot be empty");
            }

            repository.Add(clipping);
        }

        public bool UpdateClipping(Clipping clipping) {
            return repository.Update(clipping);
        }

        public bool DeleteClipping(string key) {
            return repository.Delete(key);
        }

        public void DeleteAllClippings() {
            repository.DeleteAll();
        }

        private List<Clipping> GetByBookName(string bookname) {
            return repository.GetByBookName(bookname);
        }

        public List<string> GetClippingsBookTitleList() {
            var list = new List<string>();
            var clippings = repository.GetAll();
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
            var clippings = repository.GetAll();
            if (clippings.Count <= 0) {
                return list;
            }
            foreach (var bookTitle in clippings.Select(clipping => clipping.AuthorName).Where(bookTitle => !string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle))) {
                if (!string.IsNullOrWhiteSpace(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        public bool RenameBook(string originBookname, string bookname, string authorname) {
            var clippings = GetByBookName(originBookname);
            var result = 0;
            foreach (Clipping clipping in clippings) {
                clipping.BookName = bookname;
                if (!string.IsNullOrWhiteSpace(authorname)) {
                    clipping.AuthorName = authorname;
                }
                if (repository.Update(clipping)) {
                    result++;
                }
            }
            return result > 0;
        }

        public bool ClippingsToMarkdown(string filePath, string bookName = "") {
            try {
                string filename;

                var listClippings = GetAllClippings();

                var markdown = new StringBuilder();

                markdown.AppendLine("# \ud83d\udcda " + Strings.Books);

                markdown.AppendLine();

                if (string.IsNullOrWhiteSpace(bookName) || bookName.Equals(Strings.Select_All)) {
                    filename = "Clippings";
                    
                    markdown.AppendLine("[TOC]");

                    markdown.AppendLine();

                    foreach (var name in GetBookNamesList()) {
                        bookName = name;
                        var clippings = listClippings.Where(row => row.BookName != null && row.BookName.Equals(bookName)).ToList();
                        markdown.Append(StringHelper.BuildMarkdownWithClippings(clippings));
                    }
                } else {
                    filename = StringHelper.SanitizeFilename(bookName);

                    var clippings = listClippings.Where(row => row.BookName != null && row.BookName.Equals(bookName)).ToList();
                    markdown.Append(StringHelper.BuildMarkdownWithClippings(clippings));
                }

                if (!Directory.Exists(filePath)) {
                    Directory.CreateDirectory(filePath);
                }

                File.WriteAllText(Path.Combine(filePath, filename + FileExtension.MD), markdown.ToString(), Encoding.UTF8);

                var htmlContent = AppConstants.HtmlBegin;
                MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTableOfContent().Build();
                htmlContent += Markdown.ToHtml(markdown.ToString(), pipeline);
                htmlContent += AppConstants.HtmlEnd;

                File.WriteAllText(Path.Combine(filePath, filename + FileExtension.HTML), htmlContent, Encoding.UTF8);

                File.WriteAllText(Path.Combine(filePath, AppConstants.CSSFileName), AppConstants.Css);

                return true;
            } catch (Exception) {
                return false;
            }
        }
    }
}