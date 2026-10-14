using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class WordRepository(string connectionString, VocabSourceSchema? schema = null) : IWordRepository {
        // 源库(设备 vocab.db)的表/列由设备决定,可能缺表缺列。探测一次并缓存:
        // 调用方(工厂)已探好的快照直接传进来;直接构造(测试)时懒探测。
        private readonly VocabSourceSchema _schema = schema ?? VocabSourceSchema.Probe(connectionString);
        public Word? GetById(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word, stem, lang, category, timestamp, profileid FROM WORDS WHERE id = @id", connection);
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Word {
                    Id = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                    WordText = DatabaseHelper.GetSafeString(reader, 1),
                    Stem = DatabaseHelper.GetSafeString(reader, 2),
                    Lang = DatabaseHelper.GetSafeString(reader, 3),
                    Category = DatabaseHelper.GetSafeLong(reader, 4),
                    Timestamp = DatabaseHelper.GetSafeLong(reader, 5),
                    ProfileId = DatabaseHelper.GetSafeString(reader, 6)
                };
            }
            return null;
        }

        public List<Word> GetAll() {
            var results = new List<Word>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 按源库**实际列**构造 SELECT:缺表/缺必需列 → 该表逐表跳过(返回空),不阻断其它表。
            var selectList = _schema.BuildSelectList(VocabSourceSchema.WordsTable,
                VocabSourceSchema.WordsColumns, VocabSourceSchema.WordsRequiredColumns);
            if (selectList is null) {
                return results;
            }
            var cmd = new SqliteCommand($"SELECT {selectList} FROM WORDS", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var id = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(id)) {
                    continue;
                }
                results.Add(new Word {
                    Id = id,
                    WordText = DatabaseHelper.GetSafeString(reader, 1),
                    Stem = DatabaseHelper.GetSafeString(reader, 2),
                    Lang = DatabaseHelper.GetSafeString(reader, 3),
                    Category = DatabaseHelper.GetSafeLong(reader, 4),
                    Timestamp = DatabaseHelper.GetSafeLong(reader, 5),
                    ProfileId = DatabaseHelper.GetSafeString(reader, 6)
                });
            }

            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM WORDS", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Word word) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO WORDS (id, word, stem, lang, category, timestamp, profileid) VALUES (@id, @word, @stem, @lang, @category, @timestamp, @profileid)", connection);
            var id = word.Id;
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@word", word.WordText ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@stem", word.Stem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lang", word.Lang ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@category", word.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", word.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@profileid", word.ProfileId ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Update(Word word) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE WORDS SET word = @word, stem = @stem, lang = @lang, category = @category, timestamp = @timestamp, profileid = @profileid WHERE id = @id", connection);
            var id = word.Id;
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@word", word.WordText ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@stem", word.Stem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lang", word.Lang ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@category", word.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", word.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@profileid", word.ProfileId ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM WORDS WHERE id = @id", connection);
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
