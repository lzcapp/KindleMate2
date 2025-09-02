using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class VocabRepository : IVocabRepository {
        private readonly string _connectionString;

        public VocabRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Vocab? GetById(string id) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB FROM vocab WHERE id = @id", connection);
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new Vocab {
                        Id = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        WordKey = DatabaseHelper.GetSafeString(reader, 1),
                        Word = DatabaseHelper.GetSafeString(reader, 2) ?? throw new InvalidOperationException(),
                        Stem = DatabaseHelper.GetSafeString(reader, 3),
                        Category = DatabaseHelper.GetSafeInt(reader, 4),
                        Translation = DatabaseHelper.GetSafeString(reader, 5),
                        Timestamp = DatabaseHelper.GetSafeString(reader, 6),
                        Frequency = DatabaseHelper.GetSafeInt(reader, 7),
                        Sync = DatabaseHelper.GetSafeInt(reader, 8),
                        ColorRgb = DatabaseHelper.GetSafeInt(reader, 9)
                    };
                }
                return null;
            } catch (Exception e) {
                Console.WriteLine(e);
                return null;
            }
        }

        public List<Vocab> GetAll() {
            var results = new List<Vocab>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB FROM vocab", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var id = DatabaseHelper.GetSafeString(reader, 0);
                var word = DatabaseHelper.GetSafeString(reader, 2);
                if (id == null || word == null) {
                    continue;
                }
                var vocab = new Vocab {
                    Id = id,
                    WordKey = DatabaseHelper.GetSafeString(reader, 1),
                    Word = word,
                    Stem = DatabaseHelper.GetSafeString(reader, 3),
                    Category = DatabaseHelper.GetSafeInt(reader, 4),
                    Translation = DatabaseHelper.GetSafeString(reader, 5),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 6),
                    Frequency = DatabaseHelper.GetSafeInt(reader, 7),
                    Sync = DatabaseHelper.GetSafeInt(reader, 8),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 9)
                };
                results.Add(vocab);
            }
            return results;
        }

        public List<Vocab> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Vocab>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = type switch {
                AppEntities.SearchType.Vocabulary => "WHERE word LIKE '%' || @strSearch || '%'",
                AppEntities.SearchType.Stem => "WHERE stem LIKE '%' || @strSearch || '%'",
                AppEntities.SearchType.All => "WHERE word LIKE '%' || @strSearch || '%' OR stem LIKE '%' || @strSearch || '%'",
                _ => string.Empty
            };
            var query = "SELECT id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB FROM vocab " + sql;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", search);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var id = DatabaseHelper.GetSafeString(reader, 0);
                var word = DatabaseHelper.GetSafeString(reader, 2);
                if (id == null || word == null) {
                    continue;
                }
                results.Add(new Vocab {
                    Id = id,
                    WordKey = DatabaseHelper.GetSafeString(reader, 1),
                    Word = word,
                    Stem = DatabaseHelper.GetSafeString(reader, 3),
                    Category = DatabaseHelper.GetSafeInt(reader, 4),
                    Translation = DatabaseHelper.GetSafeString(reader, 5),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 6),
                    Frequency = DatabaseHelper.GetSafeInt(reader, 7),
                    Sync = DatabaseHelper.GetSafeInt(reader, 8),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 9)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM vocab", connection);
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(Vocab vocab) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO vocab (id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB) VALUES (@id, @word_key, @word, @stem, @category, @translation, @timestamp, @frequency, @sync, @colorRGB)", connection);
            cmd.Parameters.AddWithValue("@id", vocab.Id);
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey);
            cmd.Parameters.AddWithValue("@word", vocab.Word);
            cmd.Parameters.AddWithValue("@stem", vocab.Stem);
            cmd.Parameters.AddWithValue("@category", vocab.Category);
            cmd.Parameters.AddWithValue("@translation", vocab.Translation);
            cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency);
            cmd.Parameters.AddWithValue("@sync", vocab.Sync);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb);
            cmd.ExecuteNonQuery();
        }

        public void Update(Vocab vocab) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE vocab SET word_key = @word_key, word = @word, stem = @stem, category = @category, translation = @translation, timestamp = @timestamp, frequency = @frequency, sync = @sync, colorRGB = @colorRGB WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", vocab.Id);
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey);
            cmd.Parameters.AddWithValue("@word", vocab.Word);
            cmd.Parameters.AddWithValue("@stem", vocab.Stem);
            cmd.Parameters.AddWithValue("@category", vocab.Category);
            cmd.Parameters.AddWithValue("@translation", vocab.Translation);
            cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency);
            cmd.Parameters.AddWithValue("@sync", vocab.Sync);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb);
            cmd.ExecuteNonQuery();
        }

        public void UpdateFrequencyByWordKey(Vocab vocab) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE vocab SET frequency = @frequency WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public bool DeleteByWordKey(string wordKey) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            return cmd.ExecuteNonQuery() > 0;
        }

        public void DeleteAll() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab", connection);
            cmd.ExecuteNonQuery();
        }
    }
}
