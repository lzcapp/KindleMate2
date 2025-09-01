using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Helpers {
    public static class DatabaseHelper {
        public static bool CreateDatabase(string filePath, out Exception? exception) {
            exception = null;
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

        public static void ImportKMDatabase(string sourceDbPath, string targetDbPath) {
            using var connection = new SqliteConnection($"Data Source={targetDbPath};");
            connection.Open();

            using var command = new SqliteCommand($"ATTACH DATABASE '{sourceDbPath}' AS SourceDb;", connection);
            command.ExecuteNonQuery();

            // Copy rows
            using var copyCmd = new SqliteCommand(
                "INSERT INTO clippings SELECT * FROM SourceDb.clippings;", connection);
            copyCmd.ExecuteNonQuery();

            // Detach
            using var detachCmd = new SqliteCommand("DETACH DATABASE SourceDb;", connection);
            detachCmd.ExecuteNonQuery();
        }

        public static void ImportVocabDatabase(string sourceDbPath, string targetDbPath) {
            using var connection = new SqliteConnection($"Data Source={targetDbPath};");
            connection.Open();

            using var command = new SqliteCommand($"ATTACH DATABASE '{sourceDbPath}' AS SourceDb;", connection);
            command.ExecuteNonQuery();

            // Copy rows
            using var copyCmd = new SqliteCommand("INSERT INTO lookups SELECT * FROM SourceDb.lookups;", connection);
            copyCmd.ExecuteNonQuery();

            using var copyCmd2 = new SqliteCommand("INSERT INTO vocab SELECT * FROM SourceDb.vocab;", connection);
            copyCmd2.ExecuteNonQuery();

            // Detach
            using var detachCmd = new SqliteCommand("DETACH DATABASE SourceDb;", connection);
            detachCmd.ExecuteNonQuery();
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
    }
}
