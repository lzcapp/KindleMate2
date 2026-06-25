using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class VocabRepository(string connectionString) : IVocabRepository {
        public Vocab? GetById(string id) {
            using var connection = new SqliteConnection(connectionString);
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
                    Category = DatabaseHelper.GetSafeLong(reader, 4),
                    Translation = DatabaseHelper.GetSafeString(reader, 5),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 6),
                    Frequency = DatabaseHelper.GetSafeInt(reader, 7),
                    Sync = DatabaseHelper.GetSafeInt(reader, 8),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 9)
                };
            }
            return null;
        }

        public List<Vocab> GetAll() {
            var results = new List<Vocab>();
            
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB FROM vocab", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var id = DatabaseHelper.GetSafeString(reader, 0);
                var word = DatabaseHelper.GetSafeString(reader, 2);
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(word)) {
                    continue;
                }
                var vocab = new Vocab {
                    Id = id,
                    WordKey = DatabaseHelper.GetSafeString(reader, 1),
                    Word = word,
                    Stem = DatabaseHelper.GetSafeString(reader, 3),
                    Category = DatabaseHelper.GetSafeLong(reader, 4),
                    Translation = DatabaseHelper.GetSafeString(reader, 5),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 6),
                    Frequency = DatabaseHelper.GetSafeInt(reader, 7),
                    Sync = DatabaseHelper.GetSafeInt(reader, 8),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 9)
                };
                results.Add(vocab);
            }
            return results;
        }

        public List<Vocab> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Vocab>();

            using var connection = new SqliteConnection(connectionString);
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
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(word)) {
                    continue;
                }
                results.Add(new Vocab {
                    Id = id,
                    WordKey = DatabaseHelper.GetSafeString(reader, 1),
                    Word = word,
                    Stem = DatabaseHelper.GetSafeString(reader, 3),
                    Category = DatabaseHelper.GetSafeLong(reader, 4),
                    Translation = DatabaseHelper.GetSafeString(reader, 5),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 6),
                    Frequency = DatabaseHelper.GetSafeInt(reader, 7),
                    Sync = DatabaseHelper.GetSafeInt(reader, 8),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 9)
                });
            }
            return results;
        }

        public List<string> GetWordsList() {
            var results = new List<string>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT DISTINCT word FROM vocab", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var word = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(word)) {
                    continue;
                }
                results.Add(word);
            }
            results = results.OrderBy(x => x).ToList();
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM vocab", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Vocab vocab) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO vocab (id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB) VALUES (@id, @word_key, @word, @stem, @category, @translation, @timestamp, @frequency, @sync, @colorRGB)", connection);
            cmd.Parameters.AddWithValue("@id", vocab.Id ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@word", vocab.Word ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@stem", vocab.Stem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@category", vocab.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@translation", vocab.Translation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", vocab.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public int Add(List<Vocab> vocabs) {
            var count = 0;
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try {
                foreach (Vocab vocab in vocabs) {
                    var cmd = new SqliteCommand("INSERT INTO vocab (id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB) VALUES (@id, @word_key, @word, @stem, @category, @translation, @timestamp, @frequency, @sync, @colorRGB)", connection, transaction);
                    cmd.Parameters.AddWithValue("@id", vocab.Id ?? throw new InvalidOperationException());
                    cmd.Parameters.AddWithValue("@word_key", vocab.WordKey ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@word", vocab.Word ?? throw new InvalidOperationException());
                    cmd.Parameters.AddWithValue("@stem", vocab.Stem ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@category", vocab.Category ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@translation", vocab.Translation ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@frequency", vocab.Frequency ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@sync", vocab.Sync ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb ?? (object)DBNull.Value);
                    if (cmd.ExecuteNonQuery() > 0) {
                        count++;
                    }
                }
                transaction.Commit();
            } catch {
                transaction.Rollback();
                throw;
            }
            return count;
        }

        public bool Update(Vocab vocab) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE vocab SET word_key = @word_key, word = @word, stem = @stem, category = @category, translation = @translation, timestamp = @timestamp, frequency = @frequency, sync = @sync, colorRGB = @colorRGB WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", vocab.Id ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@word", vocab.Word ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@stem", vocab.Stem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@category", vocab.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@translation", vocab.Translation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", vocab.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UpdateFrequencyByWordKey(Vocab vocab) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE vocab SET frequency = @frequency WHERE word_key = @word_key", connection);
            var wordKey = vocab.WordKey;
            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab WHERE id = @id", connection);
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteByWordKey(string wordKey) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab WHERE word_key = @word_key", connection);
            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteAll() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab", connection);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
