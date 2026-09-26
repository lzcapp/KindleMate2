using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    public interface ILookupRepository {
        Lookup? GetByWordKey(string wordKey);

        List<Lookup> GetAll();

        List<Lookup> GetByTimestamp(string timeStamp);

        /// <summary>
        /// True when a lookup with the given word key and formatted timestamp already exists.
        /// Used as the idempotency check during vocab.db import — (word_key, timestamp) is
        /// the semantic identity of a lookup, unlike timestamp alone.
        /// </summary>
        bool ExistsByWordKeyAndTimestamp(string wordKey, string timestamp);

        List<Lookup> GetByTitle(string title);

        List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type);
        
        List<string> GetWordKeysList();

        int GetCount();

        bool Add(Lookup lookup);

        int Add(List<Lookup> lookups);

        /// <summary>
        /// Updates the single lookup identified by (word_key, timestamp) — lookups have no primary
        /// key and word_key alone is not unique, so (word_key, timestamp) is the row identity
        /// (mirrors <see cref="Delete(string, string)"/>). <paramref name="lookup"/>.Timestamp is
        /// therefore both the locator and the value written back (callers that rename books keep it
        /// unchanged, so this is always the original timestamp).
        /// </summary>
        bool Update(Lookup lookup);

        bool Delete(string wordKey);

        /// <summary>
        /// Deletes the single lookup identified by (word_key, timestamp).
        /// lookups have no primary key and word_key alone is not unique (the same word can be
        /// looked up many times), so deleting by the exact pair targets just the selected row.
        /// </summary>
        bool Delete(string wordKey, string timestamp);

        /// <summary>
        /// 把某个词的**全部**查询行并到另一个键上(「重命名生词」撞名时用)。
        ///
        /// lookups **没有主键**,身份就是 (word_key, timestamp) —— 没有"逐行改键"的余地,只能整批改。
        ///
        /// 分两步:
        /// <list type="number">
        /// <item>删掉两类搬不过去(或搬过去就重复)的源行:
        ///   与目标行**同 timestamp** 的(撞唯一约束,本就是同一次阅读事件);
        ///   以及**同句 + 同书 + 同作者**(句子非空)的 —— 两个同名词条各记一条的同一次阅读,
        ///   并入后会在中栏显示成两条肉眼完全相同的行。句子为空时无从判断,不删;</item>
        /// <item>其余的全部 UPDATE 到目标键。</item>
        /// </list>
        ///
        /// 刻意**不用** <c>UPDATE OR REPLACE</c>:它删掉哪些行由唯一约束隐式决定,读代码时看不出来;
        /// 这里把"删哪一批"写成显式的 DELETE,一眼可查。
        /// </summary>
        /// <returns>被改写(搬走)的行数。</returns>
        int MergeWordKey(string oldWordKey, string newWordKey);

        bool DeleteAll();
    }
}