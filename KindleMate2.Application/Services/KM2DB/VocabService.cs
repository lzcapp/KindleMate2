using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB {
    public class VocabService(IVocabRepository repository) : IVocabService {
        public Vocab? GetVocabByName(string id) {
            return repository.GetById(id);
        }

        public List<Vocab> GetAllVocabs() {
            return repository.GetAll();
        }

        public List<Vocab> GetByFuzzySearch(string searchText, AppEntities.SearchType searchType) {
            return repository.GetByFuzzySearch(searchText, searchType);
        }

        public List<string> GetWordsList() {
            return repository.GetWordsList();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddVocab(Vocab vocab) {
            if (string.IsNullOrWhiteSpace(vocab.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            repository.Add(vocab);
        }

        public void UpdateVocab(Vocab vocab) {
            repository.Update(vocab);
        }

        public void DeleteVocab(string id) {
            repository.Delete(id);
        }

        public bool DeleteVocabByWordKey(string wordKey) {
            return repository.DeleteByWordKey(wordKey);
        }

        /// <summary>
        /// 批量删除全部生词(左栏「全部生词」节点)—— **软删**:与单个删除同语义,行留着占键,
        /// 之后重新导入同一份库不会把整批词复活。整库重来走「清空全部数据」(硬删 + 先备份)。
        /// </summary>
        public void DeleteAllVocabs() {
            repository.SoftDeleteAll();
        }

        public List<string> GetVocabWordList() {
            var list = new List<string>();
            var vocabs = repository.GetAll();
            if (vocabs.Count <= 0) {
                return list;
            }
            foreach (var bookTitle in vocabs.Select(vocab => vocab.Word).Where(bookTitle => !string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle))) {
                list.Add(bookTitle);
            }
            return list;
        }

        public List<string> GetVocabStemList() {
            var list = new List<string>();
            var vocabs = repository.GetAll();
            if (vocabs.Count <= 0) {
                return list;
            }
            foreach (var bookTitle in vocabs.Select(vocab => vocab.Stem).Where(bookTitle => !string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle))) {
                if (!string.IsNullOrWhiteSpace(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }
    }
}
