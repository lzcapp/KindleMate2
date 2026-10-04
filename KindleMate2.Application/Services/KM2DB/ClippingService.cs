using System.Text;
using KindleMate2.Application.Services;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;
using Markdig;

namespace KindleMate2.Application.Services.KM2DB {
    public class ClippingService(IClippingRepository repository) : IClippingService {
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
                var clippings = listClippings.Where(row => row.BookName != null && row.BookName.Equals(bookName)).ToList();

                // 文件名消歧必须**看目标目录**:单书导出看不到同批的其他书,只能靠"目录里已有的同名文件
                // 是不是同一本"来判定 —— 否则「A/B」与「A:B」净化后同为 A_B,第二次会静默覆盖第一次
                // (.md 与 .html 一起丢)。作者用于撞名时退化成「作者 - 书名」,与 Obsidian 侧同一候选序。
                var author = clippings.Count > 0 ? clippings[0].AuthorName : null;
                filename = ExportFileNameAllocator.AssignSingleFileName(filePath, bookName, author);

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
        }
    }
}