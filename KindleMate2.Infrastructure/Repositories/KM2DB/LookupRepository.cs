using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class LookupRepository(string connectionString) : ILookupRepository {
        public Lookup? GetByWordKey(string wordKey) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Lookup {
                    WordKey = DatabaseHelper.GetSafeString(reader, 0),
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                };
            }
            return null;
        }

        public List<Lookup> GetAll() {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(new Lookup {
                    WordKey = wordKey,
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                });
            }
            return results;
        }

        public List<Lookup> GetByTimestamp(string timeStamp) {
            var results = new List<Lookup>();
            
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE timestamp = @timeStamp", connection);
            cmd.Parameters.AddWithValue("@timeStamp", timeStamp);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) {
                return results;
            }
            var wordKey = DatabaseHelper.GetSafeString(reader, 0);
            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            results.Add(new Lookup {
                WordKey = wordKey,
                Usage = DatabaseHelper.GetSafeString(reader, 1),
                Title = DatabaseHelper.GetSafeString(reader, 2),
                Authors = DatabaseHelper.GetSafeString(reader, 3),
                Timestamp = DatabaseHelper.GetSafeString(reader, 4)
            });
            return results;
        }

        public List<Lookup> GetByTitle(string title) {
            var results = new List<Lookup>();
            
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE title = @title", connection);
            cmd.Parameters.AddWithValue("@title", title);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) {
                return results;
            }
            var wordKey = DatabaseHelper.GetSafeString(reader, 0);
            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            results.Add(new Lookup {
                WordKey = wordKey,
                Usage = DatabaseHelper.GetSafeString(reader, 1),
                Title = DatabaseHelper.GetSafeString(reader, 2),
                Authors = DatabaseHelper.GetSafeString(reader, 3),
                Timestamp = DatabaseHelper.GetSafeString(reader, 4)
            });
            return results;
        }

        public List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var sql = string.Empty;
            switch (type) {
                case AppEntities.SearchType.BookTitle:
                    sql = "WHERE title LIKE '%' || @strSearch || '%'";
                    break;
                case AppEntities.SearchType.Author:
                    sql = "WHERE authors LIKE '%' || @strSearch || '%'";
                    break;
                case AppEntities.SearchType.Content:
                    sql = "WHERE usage LIKE '%' || @strSearch || '%'";
                    break;
                case AppEntities.SearchType.Vocabulary:
                case AppEntities.SearchType.Stem:
                    sql = "WHERE word_key LIKE '%' || @strSearch || '%'";
                    break;
                case AppEntities.SearchType.All:
                    sql = "WHERE word_key LIKE '%' || @strSearch || '%' OR usage LIKE '%' || @strSearch || '%' OR title LIKE '%' || @strSearch || '%' OR authors LIKE '%' || @strSearch || '%'";
                    break;
            }
            var query = "SELECT DISTINCT * FROM lookups " + sql;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", search);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(new Lookup {
                    WordKey = wordKey,
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                });
            }
            return results;
        }

        public List<string> GetWordKeysList() {
            var results = new List<string>();
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT DISTINCT word_key FROM lookups ORDER BY word_key", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                    if (string.IsNullOrWhiteSpace(wordKey)) {
                        continue;
                    }
                    results.Add(wordKey);
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

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM lookups", connection);
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(Lookup lookup) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES (@word_key, @usage, @title, @authors, @timestamp)", connection);
                cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(Lookup lookup) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE lookups SET usage = @usage, title = @title, authors = @authors, timestamp = @timestamp WHERE word_key = @word_key", connection);
                cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Delete(string wordKey) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM lookups WHERE word_key = @word_key", connection);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@word_key", wordKey);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool DeleteAll() {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM lookups", connection);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}
