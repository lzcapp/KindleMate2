using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class SettingRepository(string connectionString) : ISettingRepository {
        public Setting? GetByName(string name) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT name, value FROM settings WHERE name = @name", connection);
                cmd.Parameters.AddWithValue("@name", name);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new Setting {
                        Name = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        value = DatabaseHelper.GetSafeString(reader, 1)
                    };
                }
                return null;
            } catch (Exception ex) {
                Console.WriteLine(ex);
                return null;
            }
        }

        public List<Setting> GetAll() {
            var results = new List<Setting>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT name, value FROM settings", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var name = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(name)) {
                    continue;
                }
                results.Add(new Setting {
                    Name = name,
                    value = DatabaseHelper.GetSafeString(reader, 1)
                });
            }
            return results;
        }

        public int GetCount() {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM settings", connection);
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(Setting setting) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO settings (name, value) VALUES (@name, @value)", connection);
                cmd.Parameters.AddWithValue("@name", setting.Name ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@value", setting.value ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(Setting setting) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE settings SET value = @value WHERE name = @name", connection);
                cmd.Parameters.AddWithValue("@name", setting.Name ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@value", setting.value ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Delete(string name) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM settings WHERE name = @name", connection);
                cmd.Parameters.AddWithValue("@name", name);
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

                var cmd = new SqliteCommand("DELETE FROM settings", connection);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}
