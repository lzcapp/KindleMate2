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

        bool DeleteAll();
    }
}