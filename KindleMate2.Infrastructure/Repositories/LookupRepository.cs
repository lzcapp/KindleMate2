using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace KindleMate2.Infrastructure.Repositories {
    public class LookupRepository : ILookupRepository {
        private readonly string _connectionString;

        public LookupRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Lookup? GetByWordKey(string wordKey) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Lookup {
                    word_key = reader.GetString(0),
                    usage = reader.GetString(1),
                    title = reader.GetString(2),
                    authors = reader.GetString(3),
                    timestamp = reader.GetString(4)
                };
            }
            return null;
        }

        public IEnumerable<Lookup> GetAll() {
            var results = new List<Lookup>();

            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Lookup {
                    word_key = reader.GetString(0),
                    usage = reader.GetString(1),
                    title = reader.GetString(2),
                    authors = reader.GetString(3),
                    timestamp = reader.GetString(4)
                });
            }
            return results;
        }

        public void Add(Lookup lookup) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES (@word_key, @usage, @title, @authors, @timestamp)", connection);
            cmd.Parameters.AddWithValue("@word_key", lookup.word_key);
            cmd.Parameters.AddWithValue("@usage", lookup.usage);
            cmd.Parameters.AddWithValue("@title", lookup.title);
            cmd.Parameters.AddWithValue("@authors", lookup.authors);
            cmd.Parameters.AddWithValue("@timestamp", lookup.timestamp);
            cmd.ExecuteNonQuery();
        }

        public void Update(Lookup lookup) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE lookups SET usage = @usage, title = @title, authors = @authors, timestamp = @timestamp WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", lookup.word_key);
            cmd.Parameters.AddWithValue("@usage", lookup.usage);
            cmd.Parameters.AddWithValue("@title", lookup.title);
            cmd.Parameters.AddWithValue("@authors", lookup.authors);
            cmd.Parameters.AddWithValue("@timestamp", lookup.timestamp);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string wordKey) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM lookups WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            cmd.ExecuteNonQuery();
        }
    }
}
