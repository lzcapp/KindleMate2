using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Helpers {
    public static class DatabaseHelper {
        /// <summary>
        /// Creates a new SQLite database with required tables.
        /// </summary>
        /// <param name="filePath">Path where the database file will be created</param>
        /// <param name="exception">Output parameter containing any exception that occurred</param>
        /// <returns>True if database creation was successful, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null</exception>
        /// <exception cref="ArgumentException">Thrown when filePath is empty or whitespace</exception>
        public static bool CreateDatabase(string filePath, out Exception exception) {
            if (filePath == null) {
                throw new ArgumentNullException(nameof(filePath));
            }
            
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path cannot be empty or whitespace.", nameof(filePath));
            }

            exception = new Exception();
            
            try {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                using var connection = new SqliteConnection($"Data Source={filePath};Cache=Shared;Mode=ReadWriteCreate;");
                connection.Open();

                foreach (var script in GetTableCreationScripts()) {
                    using var command = new SqliteCommand(script, connection);
                    command.ExecuteNonQuery();
                }

                return true;
            } catch (Exception e) {
                exception = e;
                // Remove console logging - let the caller handle the exception
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

        /// <summary>
        /// Backs up a database file to a specified backup location.
        /// </summary>
        /// <param name="databasePath">Path to the database directory</param>
        /// <param name="backupPath">Path to the backup directory</param>
        /// <param name="databaseFileName">Name of the database file</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
        /// <exception cref="ArgumentException">Thrown when any parameter is empty or whitespace</exception>
        public static void BackupDatabase(string databasePath, string backupPath, string databaseFileName) {
            if (databasePath == null) {
                throw new ArgumentNullException(nameof(databasePath));
            }
            if (backupPath == null) {
                throw new ArgumentNullException(nameof(backupPath));
            }
            if (databaseFileName == null) {
                throw new ArgumentNullException(nameof(databaseFileName));
            }

            if (string.IsNullOrWhiteSpace(databasePath)) {
                throw new ArgumentException("Database path cannot be empty or whitespace.", nameof(databasePath));
            }
            if (string.IsNullOrWhiteSpace(backupPath)) {
                throw new ArgumentException("Backup path cannot be empty or whitespace.", nameof(backupPath));
            }
            if (string.IsNullOrWhiteSpace(databaseFileName)) {
                throw new ArgumentException("Database file name cannot be empty or whitespace.", nameof(databaseFileName));
            }

            var databaseFilePath = Path.Combine(databasePath, databaseFileName);
            
            if (!File.Exists(databaseFilePath)) {
                throw new FileNotFoundException($"Database file not found: {databaseFilePath}");
            }

            if (!Directory.Exists(backupPath)) {
                Directory.CreateDirectory(backupPath);
            }

            // Create timestamped backup filename to avoid overwrites
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = Path.GetFileNameWithoutExtension(databaseFileName) + 
                                $"_backup_{timestamp}" + 
                                Path.GetExtension(databaseFileName);
            var backupFilePath = Path.Combine(backupPath, backupFileName);

            File.Copy(databaseFilePath, backupFilePath, overwrite: false);
        }

        /// <summary>
        /// Vacuums (optimizes) a SQLite database to reclaim space and defragment.
        /// </summary>
        /// <param name="filePath">Path to the SQLite database file</param>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null</exception>
        /// <exception cref="ArgumentException">Thrown when filePath is empty or whitespace</exception>
        /// <exception cref="FileNotFoundException">Thrown when database file doesn't exist</exception>
        public static void VacuumDatabase(string filePath) {
            if (filePath == null) {
                throw new ArgumentNullException(nameof(filePath));
            }
            
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path cannot be empty or whitespace.", nameof(filePath));
            }
            
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException($"Database file not found: {filePath}");
            }

            try {
                using var connection = new SqliteConnection($"Data Source={filePath};Cache=Shared;Mode=ReadWrite;");
                connection.Open();
                using var command = new SqliteCommand("VACUUM;", connection);
                command.ExecuteNonQuery();
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to vacuum database '{filePath}': {ex.Message}", ex);
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
