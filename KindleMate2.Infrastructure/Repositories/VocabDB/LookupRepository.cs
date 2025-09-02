using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class LookupRepository : ILookupRepository{
        private readonly string _connectionString;

        public LookupRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Lookup? GetById(string id) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word_key, book_key, dict_key, pos, usage, timestamp FROM LOOKUPS WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Lookup {
                    Id = reader.GetString(0),
                    WordKey = reader.GetString(1),
                    BookKey = reader.GetString(2),
                    DictKey = reader.GetString(3),
                    Pos = reader.GetString(4),
                    Usage = reader.GetString(5),
                    Timestamp = reader.GetInt32(6)
                };
            }
            return null;
        }

        public List<Lookup> GetAll() {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word_key, book_key, dict_key, pos, usage, timestamp FROM LOOKUPS", connection);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Lookup {
                    Id = reader.GetString(0),
                    WordKey = reader.GetString(1),
                    BookKey = reader.GetString(2),
                    DictKey = reader.GetString(3),
                    Pos = reader.GetString(4),
                    Usage = reader.GetString(5),
                    Timestamp = reader.GetInt32(6)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM LOOKUPS", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(Lookup lookup) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO LOOKUPS (id, word_key, book_key, dict_key, pos, usage, timestamp) VALUES (@id, @word_key, @book_key, @dict_key, @pos, @usage, @timestamp)", connection);
            cmd.Parameters.AddWithValue("@id", lookup.Id);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey);
            cmd.Parameters.AddWithValue("@book_key", lookup.BookKey);
            cmd.Parameters.AddWithValue("@dict_key", lookup.DictKey);
            cmd.Parameters.AddWithValue("@pos", lookup.Pos);
            cmd.Parameters.AddWithValue("@usage", lookup.Usage);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp);
            cmd.ExecuteNonQuery();
        }

        public void Update(Lookup lookup) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE LOOKUPS SET word_key = @word_key, book_key = @book_key, dict_key = @dict_key, pos = @pos, usage = @usage, timestamp = @timestamp WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", lookup.Id);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey);
            cmd.Parameters.AddWithValue("@book_key", lookup.BookKey);
            cmd.Parameters.AddWithValue("@dict_key", lookup.DictKey);
            cmd.Parameters.AddWithValue("@pos", lookup.Pos);
            cmd.Parameters.AddWithValue("@usage", lookup.Usage);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM LOOKUPS WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
