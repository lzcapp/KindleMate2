using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories {
    public class SettingRepository : ISettingRepository {
        private readonly string _connectionString;

        public SettingRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Setting? GetByName(string name) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT name, value FROM settings WHERE name = @name", connection);
            cmd.Parameters.AddWithValue("@name", name);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Setting {
                    name = reader.GetString(0),
                    value = reader.GetString(1)
                };
            }
            return null;
        }

        public IEnumerable<Setting> GetAll() {
            var results = new List<Setting>();

            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT name, value FROM settings", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Setting {
                    name = reader.GetString(0),
                    value = reader.GetString(1)
                });
            }
            return results;
        }

        public void Add(Setting setting) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO settings (name, value) VALUES (@name, @value)", connection);
            cmd.Parameters.AddWithValue("@name", setting.name);
            cmd.Parameters.AddWithValue("@value", setting.value);
            cmd.ExecuteNonQuery();
        }

        public void Update(Setting setting) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE settings SET value = @value WHERE name = @name", connection);
            cmd.Parameters.AddWithValue("@name", setting.name);
            cmd.Parameters.AddWithValue("@value", setting.value);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string name) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM settings WHERE name = @name", connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }
    }
}
