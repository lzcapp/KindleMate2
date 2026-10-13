using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface IVocabRepository {
        Vocab? GetById(string id);

        /// <summary>活跃行(未软删除)。UI / 导出 / 词频重算都用它 —— 删掉的词不该再出现在任何列表里。</summary>
        List<Vocab> GetAll();

        /// <summary>
        /// 全部行,含软删除的 tombstone。**只给导入判重用**:判重集合必须包含已删行,
        /// 否则"删过的词再导一次"会被原样插回来(此前的复活 bug 正是这么来的)。
        /// 其余调用方(GetAll)一律看活跃行,别用错。
        /// </summary>
        List<Vocab> GetAllIncludingDeleted();

        List<Vocab> GetByFuzzySearch(string searchText, AppEntities.SearchType searchType);

        List<string> GetWordsList();

        int GetCount();

        bool Add(Vocab vocab);

        int Add(List<Vocab> vocabs);

        bool Update(Vocab vocab);

        bool UpdateFrequencyByWordKey(Vocab vocab);

        int UpdateFrequencyByWordKey(List<Vocab> vocabs);

        bool Delete(string id);

        bool DeleteByWordKey(string wordKey);

        /// <summary>
        /// 批量软删除全部生词(左栏「全部生词」节点)—— 与单删一致置 deleted_at 而非删行,
        /// 重导入不会把整批词复活。整库重来用 <see cref="DeleteAll"/>(硬删,清空全部数据那条路)。
        /// </summary>
        int SoftDeleteAll();

        /// <summary>真删(含 tombstone)。只用于「清空全部数据」—— 先整库备份,语义是"我要全新的库"。</summary>
        bool DeleteAll();
    }
}