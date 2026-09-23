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

        bool Update(Lookup lookup);

        bool Delete(string wordKey);

        /// <summary>
        /// Deletes the single lookup identified by (word_key, timestamp).
        /// lookups have no primary key and word_key alone is not unique (the same word can be
        /// looked up many times), so deleting by the exact pair targets just the selected row.
        /// </summary>
        bool Delete(string wordKey, string timestamp);

        /// <summary>
        /// 把某个词的**全部**查询行改挂到新的 word_key 上(「重命名生词」用)。
        ///
        /// lookups **没有主键**,身份就是 (word_key, timestamp) —— 没有"逐行改键"的余地,只能整批改。
        /// 若新键会与既有行的 (word_key, timestamp) 相撞(说明这个新名字其实已经有查询记录了),
        /// **原样返回 -1 且一行都不改**;调用方据此拒绝改名并提示。
        /// 这里刻意**不用** <c>UPDATE OR REPLACE</c>:SQLite 的 OR REPLACE 会静默**删掉**撞上的那些行 ——
        /// 改名不该有删数据的副作用。
        /// </summary>
        /// <returns>被改写的行数;因键冲突而拒绝时为 -1;新旧键相同为 0。</returns>
        int RenameWordKey(string oldWordKey, string newWordKey);

        bool DeleteAll();
    }
}