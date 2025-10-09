using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class WordRepository(string connectionString) : IWordRepository {
        public Word? GetById(string id) {
            try {
                SqliteConnection connection = new(connectionString);
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
                        word = DatabaseHelper.GetSafeString(reader, 1),
                        Stem = DatabaseHelper.GetSafeString(reader, 2),
                        Lang = DatabaseHelper.GetSafeString(reader, 3),
                        Category = DatabaseHelper.GetSafeLong(reader, 4),
                        Timestamp = DatabaseHelper.GetSafeLong(reader, 5),
                        ProfileId = DatabaseHelper.GetSafeString(reader, 6)
                    };
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return null;
        }

        public List<Word> GetAll() {
            var results = new List<Word>();

            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, word, stem, lang, category, timestamp, profileid FROM WORDS", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var id = DatabaseHelper.GetSafeString(reader, 0);
                    if (string.IsNullOrWhiteSpace(id)) {
                        continue;
                    }
                    results.Add(new Word {
                        Id = id,
                        word = DatabaseHelper.GetSafeString(reader, 1),
                        Stem = DatabaseHelper.GetSafeString(reader, 2),
                        Lang = DatabaseHelper.GetSafeString(reader, 3),
                        Category = DatabaseHelper.GetSafeLong(reader, 4),
                        Timestamp = DatabaseHelper.GetSafeLong(reader, 5),
                        ProfileId = DatabaseHelper.GetSafeString(reader, 6)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }

            return results;
        }

        public int GetCount() {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM WORDS", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(Word word) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO WORDS (id, word, stem, lang, category, timestamp, profileid) VALUES (@id, @word, @stem, @lang, @category, @timestamp, @profileid)", connection);
                var id = word.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@word", word.word ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@stem", word.Stem ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lang", word.Lang ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@category", word.Category ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@timestamp", word.Timestamp ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@profileid", word.ProfileId ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(Word word) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE WORDS SET word = @word, stem = @stem, lang = @lang, category = @category, timestamp = @timestamp, profileid = @profileid WHERE id = @id", connection);
                var id = word.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@word", word.word ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@stem", word.Stem ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lang", word.Lang ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@category", word.Category ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@timestamp", word.Timestamp ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@profileid", word.ProfileId ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Delete(string id) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM WORDS WHERE id = @id", connection);
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}