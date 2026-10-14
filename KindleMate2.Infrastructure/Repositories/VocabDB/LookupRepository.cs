using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class LookupRepository(string connectionString, VocabSourceSchema? schema = null) : ILookupRepository {
        // 源库(设备 vocab.db)的表/列由设备决定,可能缺表缺列。探测一次并缓存。
        private readonly VocabSourceSchema _schema = schema ?? VocabSourceSchema.Probe(connectionString);
        public Lookup? GetById(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word_key, book_key, dict_key, pos, usage, timestamp FROM LOOKUPS WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Lookup {
                    Id = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                    WordKey = DatabaseHelper.GetSafeString(reader, 1),
                    BookKey = DatabaseHelper.GetSafeString(reader, 2),
                    DictKey = DatabaseHelper.GetSafeString(reader, 3),
                    Pos = DatabaseHelper.GetSafeString(reader, 4),
                    Usage = DatabaseHelper.GetSafeString(reader, 5),
                    Timestamp = DatabaseHelper.GetSafeLong(reader, 6)
                };
            }
            return null;
        }

        public List<Lookup> GetAll() {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 按源库**实际列**构造 SELECT:缺表/缺必需列 → 该表逐表跳过(返回空),不阻断其它表。
            var selectList = _schema.BuildSelectList(VocabSourceSchema.LookupsTable,
                VocabSourceSchema.LookupsColumns, VocabSourceSchema.LookupsRequiredColumns);
            if (selectList is null) {
                return results;
            }
            var cmd = new SqliteCommand($"SELECT {selectList} FROM LOOKUPS", connection);
        
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var id = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(id)) {
                    continue;
                }
                results.Add(new Lookup {
                    Id = id,
                    WordKey = DatabaseHelper.GetSafeString(reader, 1),
                    BookKey = DatabaseHelper.GetSafeString(reader, 2),
                    DictKey = DatabaseHelper.GetSafeString(reader, 3),
                    Pos = DatabaseHelper.GetSafeString(reader, 4),
                    Usage = DatabaseHelper.GetSafeString(reader, 5),
                    Timestamp = DatabaseHelper.GetSafeLong(reader, 6)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM LOOKUPS", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Lookup lookup) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO LOOKUPS (id, word_key, book_key, dict_key, pos, usage, timestamp) VALUES (@id, @word_key, @book_key, @dict_key, @pos, @usage, @timestamp)", connection);
            var id = lookup.Id;
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@book_key", lookup.BookKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@dict_key", lookup.DictKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pos", lookup.Pos ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Update(Lookup lookup) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE LOOKUPS SET word_key = @word_key, book_key = @book_key, dict_key = @dict_key, pos = @pos, usage = @usage, timestamp = @timestamp WHERE id = @id", connection);
            var id = lookup.Id;
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@book_key", lookup.BookKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@dict_key", lookup.DictKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pos", lookup.Pos ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM LOOKUPS WHERE id = @id", connection);
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
