using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class LookupRepository : ILookupRepository {
        private readonly string _connectionString;

        public LookupRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Lookup? GetByWordKey(string wordKey) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Lookup {
                    WordKey = reader.GetString(0),
                    Usage = reader.GetString(1),
                    Title = reader.GetString(2),
                    Authors = reader.GetString(3),
                    Timestamp = reader.GetString(4)
                };
            }
            return null;
        }

        public List<Lookup> GetAll() {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Lookup {
                    WordKey = reader.GetString(0),
                    Usage = reader.GetString(1),
                    Title = reader.GetString(2),
                    Authors = reader.GetString(3),
                    Timestamp = reader.GetString(4)
                });
            }
            return results;
        }

        public List<Lookup> GetByTimestamp(string timeStamp) {
            var results = new List<Lookup>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE timestamp = @timeStamp", connection);
            cmd.Parameters.AddWithValue("@timeStamp", timeStamp);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                results.Add(new Lookup {
                    WordKey = reader.GetString(0),
                    Usage = reader.GetString(1),
                    Title = reader.GetString(2),
                    Authors = reader.GetString(3),
                    Timestamp = reader.GetString(4)
                });
            }
            return results;
        }

        public List<Lookup> GetByTitle(string title) {
            var results = new List<Lookup>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE title = @title", connection);
            cmd.Parameters.AddWithValue("@title", title);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                results.Add(new Lookup {
                    WordKey = reader.GetString(0),
                    Usage = reader.GetString(1),
                    Title = reader.GetString(2),
                    Authors = reader.GetString(3),
                    Timestamp = reader.GetString(4)
                });
            }
            return results;
        }

        public List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = string.Empty;
            if (type == AppEntities.SearchType.BookTitle) {
                sql = "WHERE title LIKE '%' || @strSearch || '%'";
            } else if (type == AppEntities.SearchType.Author) {
                sql = "WHERE authors LIKE '%' || @strSearch || '%'";
            } else if (type == AppEntities.SearchType.Content) {
                sql = "WHERE usage LIKE '%' || @strSearch || '%'";
            } else if (type == AppEntities.SearchType.Vocabulary || type == AppEntities.SearchType.Stem) {
                sql = "WHERE word_key LIKE '%' || @strSearch || '%'";
            } else if (type == AppEntities.SearchType.All) {
                sql = "WHERE word_key LIKE '%' || @strSearch || '%' OR usage LIKE '%' || @strSearch || '%' OR title LIKE '%' || @strSearch || '%' OR authors LIKE '%' || @strSearch || '%'";
            }
            var query = "SELECT DISTINCT * FROM lookups " + sql;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", search);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Lookup {
                    WordKey = reader.GetString(0),
                    Usage = reader.GetString(1),
                    Title = reader.GetString(2),
                    Authors = reader.GetString(3),
                    Timestamp = reader.GetString(4)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM lookups", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(Lookup lookup) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES (@word_key, @usage, @title, @authors, @timestamp)", connection);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey);
            cmd.Parameters.AddWithValue("@usage", lookup.Usage);
            cmd.Parameters.AddWithValue("@title", lookup.Title);
            cmd.Parameters.AddWithValue("@authors", lookup.Authors);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp);
            cmd.ExecuteNonQuery();
        }

        public bool Update(Lookup lookup) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE lookups SET usage = @usage, title = @title, authors = @authors, timestamp = @timestamp WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey);
            cmd.Parameters.AddWithValue("@usage", lookup.Usage);
            cmd.Parameters.AddWithValue("@title", lookup.Title);
            cmd.Parameters.AddWithValue("@authors", lookup.Authors);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string wordKey) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM lookups WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            return cmd.ExecuteNonQuery() > 0;
        }

        public void DeleteAll() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM lookups", connection);
            cmd.ExecuteNonQuery();
        }
    }
}
