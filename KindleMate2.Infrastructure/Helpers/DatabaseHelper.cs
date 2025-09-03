using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Helpers {
    public static class DatabaseHelper {
        public static bool CreateDatabase(string filePath, out Exception exception) {
            exception = new Exception();
            try {
                using var connection = new SqliteConnection($"Data Source={filePath};Cache=Shared;Mode=ReadWriteCreate;");
                connection.Open();

                foreach (var script in GetTableCreationScripts()) {
                    using var command = new SqliteCommand(script, connection);
                    command.ExecuteNonQuery();
                }

                return true;
            } catch (Exception e) {
                exception = e;
                Console.WriteLine($"[DatabaseHelper] Database creation failed: {e.Message}");
                return false;
            }
        }

        private static List<string> GetTableCreationScripts() {
            return [
                @"
                CREATE TABLE IF NOT EXISTS [clippings] (
                    [key] TEXT PRIMARY KEY NOT NULL UNIQUE, 
                    [content] TEXT DEFAULT(''), 
                    [bookname] TEXT DEFAULT(''), 
                    [authorname] TEXT, 
                    [brieftype] INTEGER, 
                    [clippingtypelocation] TEXT, 
                    [clippingdate] TEXT, 
                    [read] INT DEFAULT(0), 
                    [clipping_importdate] TEXT, 
                    [tag] TEXT, 
                    [sync] INT DEFAULT(0), 
                    [newbookname] TEXT, 
                    [colorRGB] INTEGER DEFAULT(-1), 
                    pagenumber INT DEFAULT(0)
                );",

                @"
                CREATE TABLE IF NOT EXISTS [lookups] (
                    [word_key] TEXT, 
                    [usage] TEXT, 
                    [title] TEXT, 
                    [authors] TEXT, 
                    [timestamp] TEXT UNIQUE
                );",

                @"
                CREATE TABLE IF NOT EXISTS [original_clipping_lines] (
                    [key] TEXT PRIMARY KEY NOT NULL UNIQUE, 
                    [line1] TEXT DEFAULT(''), 
                    [line2] TEXT DEFAULT(''), 
                    [line3] TEXT DEFAULT(''), 
                    [line4] TEXT DEFAULT(''), 
                    [line5] TEXT DEFAULT('')
                );",

                @"
                CREATE TABLE IF NOT EXISTS [settings] (
                    [name] TEXT PRIMARY KEY UNIQUE, 
                    [value] TEXT
                );",

                @"
                CREATE TABLE IF NOT EXISTS [vocab] (
                    [id] TEXT PRIMARY KEY NOT NULL UNIQUE, 
                    [word_key] TEXT, 
                    [word] TEXT NOT NULL, 
                    [stem] TEXT, 
                    [category] INTEGER DEFAULT '0', 
                    [translation] TEXT, 
                    [timestamp] TEXT, 
                    [frequency] INT DEFAULT(0), 
                    [sync] INT DEFAULT(0), 
                    [colorRGB] INTEGER DEFAULT(-1)
                );"
            ];
        }

        public static void BackupDatabase(string databasePath, string backupPath, string databaseFileName) {
            var databaseFilePath = Path.Combine(databasePath, databaseFileName);
            var backupFilePath = Path.Combine(backupPath, databaseFileName);

            if (!Directory.Exists(backupPath)) {
                Directory.CreateDirectory(backupPath);
            }

            File.Copy(databaseFilePath, backupFilePath, true);
        }

        public static void VacuumDatabase(string filePath) {
            try {
                using var connection = new SqliteConnection($"Data Source={filePath};Cache=Shared;Mode=ReadWrite;");
                connection.Open();
                using var command = new SqliteCommand("VACUUM;", connection);
                command.ExecuteNonQuery();
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }
        
        public static string? GetSafeString(SqliteDataReader reader, int ordinal) {
            if (reader.IsDBNull(ordinal)) {
                return null;
            }
            var s = reader.GetString(ordinal);
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        public static int? GetSafeInt(SqliteDataReader reader, int ordinal) {
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }

        public static long? GetSafeLong(SqliteDataReader reader, int ordinal) {
            return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
        }

        public static int GetSafeInt(SqliteDataReader reader, int ordinal, int defaultValue) {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
        }
        
        public static string GetConnectionString(string dbFile) {
            return $"Data Source={dbFile};Cache=Shared;Mode=ReadWrite;";
        }
    }
}
