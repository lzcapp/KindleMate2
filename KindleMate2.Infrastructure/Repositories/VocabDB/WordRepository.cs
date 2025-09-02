using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class WordRepository : IWordRepository {
        private readonly string _connectionString;

        public WordRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Word? GetById(string id) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word, stem, lang, category, timestamp, profileid FROM WORDS WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Word {
                    Id = reader.GetString(0),
                    word = reader.GetString(1),
                    Stem = reader.GetString(2),
                    Lang = reader.GetString(3),
                    Category = reader.GetInt32(4),
                    Timestamp = reader.GetInt32(5),
                    Profileid = reader.GetString(6)
                };
            }
            return null;
        }

        public List<Word> GetAll() {
            var results = new List<Word>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word, stem, lang, category, timestamp, profileid FROM WORDS", connection);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Word {
                    Id = reader.GetString(0),
                    word = reader.GetString(1),
                    Stem = reader.GetString(2),
                    Lang = reader.GetString(3),
                    Category = reader.GetInt32(4),
                    Timestamp = reader.GetInt32(5),
                    Profileid = reader.GetString(6)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM WORDS", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(Word Word) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO WORDS (id, word, stem, lang, category, timestamp, profileid) VALUES (@id, @word, @stem, @lang, @category, @timestamp, @profileid)", connection);
            cmd.Parameters.AddWithValue("@id", Word.Id);
            cmd.Parameters.AddWithValue("@word", Word.word);
            cmd.Parameters.AddWithValue("@stem", Word.Stem);
            cmd.Parameters.AddWithValue("@lang", Word.Lang);
            cmd.Parameters.AddWithValue("@category", Word.Category);
            cmd.Parameters.AddWithValue("@timestamp", Word.Timestamp);
            cmd.Parameters.AddWithValue("@profileid", Word.Profileid);
            cmd.ExecuteNonQuery();
        }

        public void Update(Word Word) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE WORDS SET word = @word, stem = @stem, lang = @lang, category = @category, timestamp = @timestamp, profileid = @profileid WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", Word.Id);
            cmd.Parameters.AddWithValue("@word", Word.word);
            cmd.Parameters.AddWithValue("@stem", Word.Stem);
            cmd.Parameters.AddWithValue("@lang", Word.Lang);
            cmd.Parameters.AddWithValue("@category", Word.Category);
            cmd.Parameters.AddWithValue("@timestamp", Word.Timestamp);
            cmd.Parameters.AddWithValue("@profileid", Word.Profileid);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM WORDS WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
