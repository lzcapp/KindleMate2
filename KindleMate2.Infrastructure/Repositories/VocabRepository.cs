using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;
using Microsoft.Data.Sqlite;
using System.Xml.Linq;

namespace KindleMate2.Infrastructure.Repositories {
    public class VocabRepository : IVocabRepository {
        private readonly string _connectionString;

        public VocabRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Vocab? GetById(string id) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB FROM vocab WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Vocab {
                    id = reader.GetString(0),
                    word_key = reader.GetString(1),
                    word = reader.GetString(2),
                    stem = reader.GetString(3),
                    category = reader.GetInt32(4),
                    translation = reader.GetString(5),
                    timestamp = reader.GetString(6),
                    frequency = reader.GetInt32(7),
                    sync = reader.GetInt32(8),
                    colorRGB = reader.GetInt32(9)
                };
            }
            return null;
        }

        public IEnumerable<Vocab> GetAll() {
            var results = new List<Vocab>();

            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB FROM vocab", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Vocab {
                    id = reader.GetString(0),
                    word_key = reader.GetString(1),
                    word = reader.GetString(2),
                    stem = reader.GetString(3),
                    category = reader.GetInt32(4),
                    translation = reader.GetString(5),
                    timestamp = reader.GetString(6),
                    frequency = reader.GetInt32(7),
                    sync = reader.GetInt32(8),
                    colorRGB = reader.GetInt32(9)
                });
            }
            return results;
        }
        public void Add(Vocab vocab) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO vocab (id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB) VALUES (@id, @word_key, @word, @stem, @category, @translation, @timestamp, @frequency, @sync, @colorRGB)", connection);
            cmd.Parameters.AddWithValue("@id", vocab.id);
            cmd.Parameters.AddWithValue("@word_key", vocab.word_key);
            cmd.Parameters.AddWithValue("@word", vocab.word);
            cmd.Parameters.AddWithValue("@stem", vocab.stem);
            cmd.Parameters.AddWithValue("@category", vocab.category);
            cmd.Parameters.AddWithValue("@translation", vocab.translation);
            cmd.Parameters.AddWithValue("@timestamp", vocab.timestamp);
            cmd.Parameters.AddWithValue("@frequency", vocab.frequency);
            cmd.Parameters.AddWithValue("@sync", vocab.sync);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.colorRGB);
            cmd.ExecuteNonQuery();
        }
        public void Update(Vocab vocab) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE vocab SET word_key = @word_key, word = @word, stem = @stem, category = @category, translation = @translation, timestamp = @timestamp, frequency = @frequency, sync = @sync, colorRGB = @colorRGB WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", vocab.id);
            cmd.Parameters.AddWithValue("@word_key", vocab.word_key);
            cmd.Parameters.AddWithValue("@word", vocab.word);
            cmd.Parameters.AddWithValue("@stem", vocab.stem);
            cmd.Parameters.AddWithValue("@category", vocab.category);
            cmd.Parameters.AddWithValue("@translation", vocab.translation);
            cmd.Parameters.AddWithValue("@timestamp", vocab.timestamp);
            cmd.Parameters.AddWithValue("@frequency", vocab.frequency);
            cmd.Parameters.AddWithValue("@sync", vocab.sync);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.colorRGB);
            cmd.ExecuteNonQuery();
        }
        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
