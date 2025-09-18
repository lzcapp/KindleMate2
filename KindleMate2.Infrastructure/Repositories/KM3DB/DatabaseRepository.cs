using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class DatabaseRepository {
        private readonly string _connectionString;

        public DatabaseRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public bool IsDatabaseEmpty() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Get all user-defined tables
            var tableNames = new List<string>();
            using (var cmdTables = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';", connection))
            using (SqliteDataReader reader = cmdTables.ExecuteReader()) {
                while (reader.Read()) {
                    var name = DatabaseHelper.GetSafeString(reader, 0);
                    if (name == null) {
                        continue;
                    }
                    tableNames.Add(name);
                }
            }

            // Check each table for rows
            foreach (var table in tableNames) {
                using var cmdCount = new SqliteCommand($"SELECT COUNT(*) FROM {table};", connection);
                if (Convert.ToInt32(cmdCount.ExecuteScalar()) > 0) {
                    return false; // Found data in a table, database is not empty
                }
            }

            return true; // All tables are empty
        }
    }
}
