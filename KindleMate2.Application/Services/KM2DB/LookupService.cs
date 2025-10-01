using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;
using Markdig;
using System.Text;
using KindleMate2.Shared;

namespace KindleMate2.Application.Services.KM2DB {
    public class LookupService {
        private readonly ILookupRepository _repository;

        public LookupService(ILookupRepository repository) {
            _repository = repository;
        }

        public Lookup? GetLookupByWordKey(string wordKey) {
            return _repository.GetByWordKey(wordKey);
        }

        public List<Lookup> GetAllLookups() {
            return _repository.GetAll();
        }

        public List<Lookup> GetLookupsByTimestamp(string timestamp) {
            return _repository.GetByTimestamp(timestamp);
        }

        public List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            return _repository.GetByFuzzySearch(search, type);
        }

        public List<string> GetWordKeysList() {
            return _repository.GetWordKeysList();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        /// <summary>
        /// Adds a new lookup to the repository.
        /// </summary>
        /// <param name="lookup">The lookup to add</param>
        /// <exception cref="ArgumentNullException">Thrown when lookup is null</exception>
        /// <exception cref="ArgumentException">Thrown when required fields are missing</exception>
        public void AddLookup(Lookup lookup) {
            if (lookup == null) {
                throw new ArgumentNullException(nameof(lookup));
            }

            if (string.IsNullOrWhiteSpace(lookup.WordKey)) {
                throw new ArgumentException("WordKey cannot be null or empty", nameof(lookup));
            }

            _repository.Add(lookup);
        }

        /// <summary>
        /// Updates an existing lookup in the repository.
        /// </summary>
        /// <param name="lookup">The lookup to update</param>
        /// <exception cref="ArgumentNullException">Thrown when lookup is null</exception>
        public void UpdateLookup(Lookup lookup) {
            if (lookup == null) {
                throw new ArgumentNullException(nameof(lookup));
            }

            _repository.Update(lookup);
        }

        /// <summary>
        /// Deletes a lookup by word key.
        /// </summary>
        /// <param name="wordKey">The word key of the lookup to delete</param>
        /// <returns>True if deletion was successful, false otherwise</returns>
        /// <exception cref="ArgumentException">Thrown when wordKey is null or empty</exception>
        public bool DeleteLookup(string wordKey) {
            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new ArgumentException("WordKey cannot be null or empty", nameof(wordKey));
            }

            return _repository.Delete(wordKey);
        }

        public bool RenameBook(string originBookname, string bookname, string authorname) {
            var lookups = _repository.GetByTitle(originBookname);
            var result = 0;
            foreach (Lookup lookup in lookups) {
                lookup.Title = bookname;
                if (!string.IsNullOrWhiteSpace(authorname)) {
                    lookup.Authors = authorname;
                }
                if (_repository.Update(lookup)) {
                    result++;
                }
            }
            return result > 0;
        }
        
        public bool LookupsToMarkdown(string filePath, string word = "") {
            string filename;

            var listLookups = GetAllLookups();

            var markdown = new StringBuilder();

            markdown.AppendLine("# \ud83d\udcda " + Strings.Vocabulary_List);

            markdown.AppendLine();

            if (string.IsNullOrWhiteSpace(word) || word.Equals(Strings.Select_All)) {
                filename = "Vocabs";
                    
                markdown.AppendLine("[TOC]");

                markdown.AppendLine();

                foreach (var wordKey in GetWordKeysList()) {
                    var lookups = listLookups.Where(row => row.WordKey != null && row.WordKey.Equals(wordKey)).ToList();
                    markdown.Append(StringHelper.BuildMarkdownWithLookups(lookups));
                }
            } else {
                filename = StringHelper.SanitizeFilename(word);

                var lookups = listLookups.Where(row => row.WordKey != null && row.Word.Equals(word)).ToList();
                markdown.Append(StringHelper.BuildMarkdownWithLookups(lookups));
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
