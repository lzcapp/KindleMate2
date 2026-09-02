using KindleMate2.Shared.Constants;
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
            ArgumentNullException.ThrowIfNull(filePath);

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
                    [timestamp] TEXT,
                    CONSTRAINT [uq_lookups_word_key_timestamp] UNIQUE ([word_key], [timestamp])
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
            ArgumentNullException.ThrowIfNull(databasePath);
            ArgumentNullException.ThrowIfNull(backupPath);
            ArgumentNullException.ThrowIfNull(databaseFileName);

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
            var timestamp = DateTime.Now.ToString(AppConstants.BackupTimestampFormat);
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
            ArgumentNullException.ThrowIfNull(filePath);

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
        
        /// <summary>
        /// Idempotent one-time schema migration for the [lookups] table.
        /// Legacy databases were created with a single-column UNIQUE on [timestamp],
        /// which rejects legitimate duplicates (e.g. several words looked up within
        /// the same second). SQLite cannot drop a column constraint via ALTER TABLE,
        /// so the table is rebuilt with uniqueness moved to (word_key, timestamp).
        /// Safe to call on every startup: it no-ops once the new schema is in place.
        /// </summary>
        /// <param name="filePath">Path to the SQLite database file</param>
        public static void MigrateLookupsSchemaIfNeeded(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return;
            }

            try {
                using var connection = new SqliteConnection(GetConnectionString(filePath));
                connection.Open();

                if (!NeedsLookupsSchemaMigration(connection)) {
                    return;
                }

                using var transaction = connection.BeginTransaction();
                try {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    command.CommandText = @"
                        CREATE TABLE [lookups_migrated] (
                            [word_key] TEXT, 
                            [usage] TEXT, 
                            [title] TEXT, 
                            [authors] TEXT, 
                            [timestamp] TEXT,
                            CONSTRAINT [uq_lookups_word_key_timestamp] UNIQUE ([word_key], [timestamp])
                        );";
                    command.ExecuteNonQuery();

                    command.CommandText = @"INSERT INTO [lookups_migrated] ([word_key], [usage], [title], [authors], [timestamp])
                                            SELECT [word_key], [usage], [title], [authors], [timestamp] FROM [lookups];";
                    command.ExecuteNonQuery();

                    command.CommandText = "DROP TABLE [lookups];";
                    command.ExecuteNonQuery();

                    command.CommandText = "ALTER TABLE [lookups_migrated] RENAME TO [lookups];";
                    command.ExecuteNonQuery();

                    transaction.Commit();
                } catch {
                    transaction.Rollback();
                    throw;
                }
            } catch (Exception e) {
                throw new InvalidOperationException($"Failed to migrate lookups schema in '{filePath}': {e.Message}", e);
            }
        }

        /// <summary>
        /// Detects the legacy lookups schema: a UNIQUE index on the single [timestamp] column.
        /// </summary>
        private static bool NeedsLookupsSchemaMigration(SqliteConnection connection) {
            try {
                // Collect unique index names first so index_info can be queried after the reader is disposed.
                var uniqueIndexNames = new List<string>();
                using (var listCommand = connection.CreateCommand()) {
                    listCommand.CommandText = "PRAGMA index_list('lookups');";
                    using var reader = listCommand.ExecuteReader();
                    while (reader.Read()) {
                        var isUnique = !reader.IsDBNull(2) && reader.GetInt64(2) == 1;
                        if (isUnique) {
                            uniqueIndexNames.Add(reader.GetString(1));
                        }
                    }
                }

                foreach (var indexName in uniqueIndexNames) {
                    using var infoCommand = connection.CreateCommand();
                    infoCommand.CommandText = $"PRAGMA index_info('{indexName}');";
                    using var reader = infoCommand.ExecuteReader();

                    var columns = new List<string>();
                    while (reader.Read()) {
                        if (!reader.IsDBNull(2)) {
                            columns.Add(reader.GetString(2));
                        }
                    }

                    // Legacy: exactly one unique column "timestamp" → must rebuild.
                    if (columns.Count == 1 && columns[0].Equals("timestamp", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }

                return false;
            } catch (Exception) {
                // If the schema cannot be inspected, leave the table untouched.
                return false;
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
