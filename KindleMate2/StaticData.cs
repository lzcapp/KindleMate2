using System.Data;
using System.Data.SQLite;

namespace KindleMate2 {
    public class StaticData {
        private const string ConnectionString = "Data Source=KM2.dat;Version=3;";

        private readonly SQLiteConnection _connection = new(ConnectionString);

        private SQLiteTransaction? _trans;

        public StaticData() {
            _connection.Open();

            using var command = new SQLiteCommand("PRAGMA synchronous=OFF", _connection);
            command.ExecuteNonQuery();
        }

        public void OpenConnection() {
            _connection.Open();
        }

        public void CloseConnection() {
            _connection.Close();
        }

        public void DisposeConnection() {
            _connection.Dispose();
        }

        public void BeginTransaction() {
            _trans = _connection.BeginTransaction();
        }

        public void CommitTransaction() {
            _trans?.Commit();
        }

        public void RollbackTransaction() {
            _trans?.Rollback();
        }

        // ReSharper disable once IdentifierTypo
        public DataTable GetClipingsDataTable() {
            var dataTable = new DataTable();

            const string queryClippings = "SELECT * FROM clippings;";
            using var command = new SQLiteCommand(queryClippings, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        public bool IsExistOriginalClippings(string? key) {
            switch (key) {
                case null:
                case "":
                    return true;
            }

            const string queryCount = "SELECT COUNT(1) FROM original_clipping_lines WHERE key = @key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@key", key);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        public bool IsExistClippings(string? key) {
            switch (key) {
                case null:
                case "":
                    return true;
            }

            const string queryCount = "SELECT COUNT(1) FROM clippings WHERE key = @key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@key", key);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        public bool IsExistClippingsOfContent(string? content) {
            switch (content) {
                case null:
                case "":
                    return true;
            }

            const string queryCount = "SELECT COUNT(1) FROM clippings WHERE content = @content";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@content", content);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        /*
        public int GetOriginClippingsCount() {
            const string queryCount = "SELECT COUNT(1) FROM original_clipping_lines";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            return count;
        }
        */

        public DataTable GetOriginClippingsDataTable() {
            var dataTable = new DataTable();

            const string queryClippings = "SELECT * FROM original_clipping_lines;";
            using var command = new SQLiteCommand(queryClippings, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        public int InsertOriginClippings(string key, string line1, string line2, string line3, string line4, string line5) {
            if (key == string.Empty || line4 == string.Empty) {
                return 0;
            }

            const string queryInsert = "INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)";
            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@line1", DbType.String);
            command.Parameters.Add("@line2", DbType.String);
            command.Parameters.Add("@line3", DbType.String);
            command.Parameters.Add("@line4", DbType.String);
            command.Parameters.Add("@line5", DbType.String);

            command.Parameters["@key"].Value = key;
            command.Parameters["@line1"].Value = line1;
            command.Parameters["@line2"].Value = line2;
            command.Parameters["@line3"].Value = line3;
            command.Parameters["@line4"].Value = line4;
            command.Parameters["@line5"].Value = line5;

            var result = command.ExecuteNonQuery();

            return result;
        }

        public bool DeleteClippingsByKey(string key) {
            if (key == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM clippings WHERE key = @key";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@key", key);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        public bool DeleteClippingsByBook(string bookname) {
            if (bookname == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM clippings WHERE bookname = @bookname";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@bookname", bookname);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        public int InsertClippings(string key, string content, string bookname, string authorname, int brieftype, string clippingtypelocation, string clippingdate, int pagenumber) {
            if (key == string.Empty || content == string.Empty) {
                return 0;
            }

            const string queryInsert = "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @pagenumber)";

            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@content", DbType.String);
            command.Parameters.Add("@bookname", DbType.String);
            command.Parameters.Add("@authorname", DbType.String);
            command.Parameters.Add("@brieftype", DbType.Int64);
            command.Parameters.Add("@clippingtypelocation", DbType.String);
            command.Parameters.Add("@clippingdate", DbType.String);
            command.Parameters.Add("@pagenumber", DbType.Int64);

            command.Parameters["@key"].Value = key;
            command.Parameters["@content"].Value = content;
            command.Parameters["@bookname"].Value = bookname;
            command.Parameters["@authorname"].Value = authorname;
            command.Parameters["@brieftype"].Value = brieftype;
            command.Parameters["@clippingtypelocation"].Value = clippingtypelocation;
            command.Parameters["@clippingdate"].Value = clippingdate;
            command.Parameters["@pagenumber"].Value = pagenumber;

            var result = command.ExecuteNonQuery();

            return result;
        }

        public int InsertClippings(string key, string content, string bookname, string authorname, int brieftype, string clippingtypelocation, string clippingdate, int read, string clipping_importdate, string tag, int sync, string newbookname, int colorRGB, int pagenumber) {
            if (key == string.Empty || content == string.Empty) {
                return 0;
            }

            const string queryInsert = "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)";

            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@content", DbType.String);
            command.Parameters.Add("@bookname", DbType.String);
            command.Parameters.Add("@authorname", DbType.String);
            command.Parameters.Add("@brieftype", DbType.Int64);
            command.Parameters.Add("@clippingtypelocation", DbType.String);
            command.Parameters.Add("@clippingdate", DbType.String);
            command.Parameters.Add("@read", DbType.String);
            command.Parameters.Add("@clipping_importdate", DbType.String);
            command.Parameters.Add("@tag", DbType.String);
            command.Parameters.Add("@sync", DbType.Int64);
            command.Parameters.Add("@newbookname", DbType.String);
            command.Parameters.Add("@colorRGB", DbType.Int64);
            command.Parameters.Add("@pagenumber", DbType.Int64);

            command.Parameters["@key"].Value = key;
            command.Parameters["@content"].Value = content;
            command.Parameters["@bookname"].Value = bookname;
            command.Parameters["@authorname"].Value = authorname;
            command.Parameters["@brieftype"].Value = brieftype;
            command.Parameters["@clippingtypelocation"].Value = clippingtypelocation;
            command.Parameters["@clippingdate"].Value = clippingdate;
            command.Parameters["@read"].Value = read;
            command.Parameters["@clipping_importdate"].Value = clipping_importdate;
            command.Parameters["@tag"].Value = tag;
            command.Parameters["@sync"].Value = sync;
            command.Parameters["@newbookname"].Value = newbookname;
            command.Parameters["@colorRGB"].Value = colorRGB;
            command.Parameters["@pagenumber"].Value = pagenumber;

            var result = command.ExecuteNonQuery();

            return result;
        }

        public bool UpdateClippings(string originBookname, string bookname, string authorname) {
            if (string.IsNullOrWhiteSpace(originBookname) || string.IsNullOrWhiteSpace(bookname)) {
                return false;
            }

            var queryUpdate = "UPDATE clippings SET bookname = @bookname";
            if (!string.IsNullOrWhiteSpace(authorname)) {
                queryUpdate += ", authorname = @authorname";
            } else {
                authorname = string.Empty;
            }

            queryUpdate += " WHERE bookname = @originBookname";

            using var command = new SQLiteCommand(queryUpdate, _connection);
            command.Parameters.Add("@bookname", DbType.String);
            command.Parameters.Add("@authorname", DbType.String);
            command.Parameters.Add("@originBookname", DbType.String);

            command.Parameters["@bookname"].Value = bookname;
            command.Parameters["@authorname"].Value = authorname;
            command.Parameters["@originBookname"].Value = originBookname;

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        public bool UpdateClippings(string key, string content) {
            if (key == string.Empty || content == string.Empty) {
                return false;
            }

            const string queryUpdate = "UPDATE clippings SET content = @content WHERE key = @key";

            using var command = new SQLiteCommand(queryUpdate, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@content", DbType.String);

            command.Parameters["@key"].Value = key;
            command.Parameters["@content"].Value = content;

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        public int InsertLookups(string word_key, string usage, string title, string authors, string timestamp) {
            if (word_key == string.Empty || timestamp == string.Empty) {
                return 0;
            }

            const string queryInsert = "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES (@word_key, @usage, @title, @authors, @timestamp)";
            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@word_key", DbType.String);
            command.Parameters.Add("@usage", DbType.String);
            command.Parameters.Add("@title", DbType.String);
            command.Parameters.Add("@authors", DbType.String);
            command.Parameters.Add("@timestamp", DbType.String);

            command.Parameters["@word_key"].Value = word_key;
            command.Parameters["@usage"].Value = usage;
            command.Parameters["@title"].Value = title;
            command.Parameters["@authors"].Value = authors;
            command.Parameters["@timestamp"].Value = timestamp;

            var result = command.ExecuteNonQuery();

            return result;
        }

        public void UpdateLookups(string origintitle, string title, string authors) {
            if (origintitle == string.Empty || title == string.Empty) {
                return;
            }

            var queryUpdate = "UPDATE lookups SET title = @title";

            if (!string.IsNullOrWhiteSpace(authors)) {
                queryUpdate += ", authors = @authors";
            }

            queryUpdate += " WHERE title = @origintitle";

            using var command = new SQLiteCommand(queryUpdate, _connection);
            command.Parameters.Add("@origintitle", DbType.String);
            command.Parameters.Add("@title", DbType.String);
            command.Parameters.Add("@authors", DbType.String);

            command.Parameters["@origintitle"].Value = origintitle;
            command.Parameters["@title"].Value = title;
            command.Parameters["@authors"].Value = authors;

            command.ExecuteNonQuery();
        }

        /*
        public bool InsertVocab(string id, string word_key, string word, string stem, int category, string translation, string timestamp, int frequency, int sync, int colorRGB) {
            if (id == string.Empty || word == string.Empty) {
                return false;
            }

            const string queryInsert = "INSERT INTO vocab (id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB) VALUES (@id, @word_key, @word, @stem, @category, @translation, @timestamp, @frequency, @sync, @colorRGB)";
            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@id", DbType.String);
            command.Parameters.Add("@word_key", DbType.String);
            command.Parameters.Add("@word", DbType.String);
            command.Parameters.Add("@stem", DbType.String);
            command.Parameters.Add("@category", DbType.UInt64);
            command.Parameters.Add("@translation", DbType.String);
            command.Parameters.Add("@timestamp", DbType.String);
            command.Parameters.Add("@frequency", DbType.Int64);
            command.Parameters.Add("@sync", DbType.Int64);
            command.Parameters.Add("@colorRGB", DbType.Int64);

            command.Parameters["@id"].Value = id;
            command.Parameters["@word_key"].Value = word_key;
            command.Parameters["@word"].Value = word;
            command.Parameters["@stem"].Value = stem;
            command.Parameters["@category"].Value = category;
            command.Parameters["@translation"].Value = translation;
            command.Parameters["@timestamp"].Value = timestamp;
            command.Parameters["@frequency"].Value = frequency;
            command.Parameters["@sync"].Value = sync;
            command.Parameters["@colorRGB"].Value = colorRGB;

            var result = command.ExecuteNonQuery();



            return result > 0;
        }
        */

        public int InsertVocab(string id, string word_key, string word, string stem, int category, string timestamp, int frequency) {
            if (id == string.Empty || word == string.Empty) {
                return 0;
            }

            const string queryInsert = "INSERT INTO vocab (id, word_key, word, stem, category, timestamp, frequency) VALUES (@id, @word_key, @word, @stem, @category, @timestamp, @frequency)";
            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@id", DbType.String);
            command.Parameters.Add("@word_key", DbType.String);
            command.Parameters.Add("@word", DbType.String);
            command.Parameters.Add("@stem", DbType.String);
            command.Parameters.Add("@category", DbType.UInt64);
            command.Parameters.Add("@timestamp", DbType.String);
            command.Parameters.Add("@frequency", DbType.Int64);

            command.Parameters["@id"].Value = id;
            command.Parameters["@word_key"].Value = word_key;
            command.Parameters["@word"].Value = word;
            command.Parameters["@stem"].Value = stem;
            command.Parameters["@category"].Value = category;
            command.Parameters["@timestamp"].Value = timestamp;
            command.Parameters["@frequency"].Value = frequency;

            var result = command.ExecuteNonQuery();

            return result;
        }

        /*
                public bool UpdateVocab(string word_key, string word, string stem, int category, string timestamp, int frequency) {
                    if (word == string.Empty) {
                        return false;
                    }



                    const string query = "UPDATE vocab SET word = @word, stem = @stem, category = @category, timestamp = @timestamp, frequency = @frequency WHERE word_key = @word_key";
                    using var command = new SQLiteCommand(query, _connection);
                    command.Parameters.Add("@word_key", DbType.String);
                    command.Parameters.Add("@word", DbType.String);
                    command.Parameters.Add("@stem", DbType.String);
                    command.Parameters.Add("@category", DbType.UInt64);
                    command.Parameters.Add("@timestamp", DbType.String);
                    command.Parameters.Add("@frequency", DbType.Int64);

                    command.Parameters["@word_key"].Value = word_key;
                    command.Parameters["@word"].Value = word;
                    command.Parameters["@stem"].Value = stem;
                    command.Parameters["@category"].Value = category;
                    command.Parameters["@timestamp"].Value = timestamp;
                    command.Parameters["@frequency"].Value = frequency;

                    var result = command.ExecuteNonQuery();



                    return result > 0;
                }
        */

        public void UpdateVocab(string word_key, int frequency) {
            if (word_key == string.Empty) {
                return;
            }

            const string query = "UPDATE vocab SET frequency = @frequency WHERE word_key = @word_key";
            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.Add("@word_key", DbType.String);
            command.Parameters.Add("@frequency", DbType.Int64);

            command.Parameters["@word_key"].Value = word_key;
            command.Parameters["@frequency"].Value = frequency;

            command.ExecuteNonQuery();
        }

        public DataTable GetVocabDataTable() {
            var dataTable = new DataTable();

            const string query = "SELECT * FROM vocab;";
            using var command = new SQLiteCommand(query, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        public DataTable GetLookupsDataTable() {
            var dataTable = new DataTable();

            const string query = "SELECT * FROM lookups;";
            using var command = new SQLiteCommand(query, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        public bool IsExistVocab(string word_key) {
            if (word_key == string.Empty) {
                return false;
            }

            const string queryCount = "SELECT COUNT(1) FROM vocab WHERE word_key = @word_key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@word_key", word_key);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        public bool IsExistVocabById(string id) {
            if (id == string.Empty) {
                return false;
            }

            const string queryCount = "SELECT COUNT(1) FROM vocab WHERE id = @id";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@id", id);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        public bool IsExistLookups(string timestamp) {
            if (timestamp == string.Empty) {
                return true;
            }

            const string queryCount = "SELECT COUNT(1) FROM lookups WHERE timestamp = @timestamp";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@timestamp", timestamp);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        public bool DeleteVocab(string word) {
            if (word == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM vocab WHERE word = @word";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@word", word);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        public bool DeleteLookupsByTimeStamp(string timestamp) {
            if (timestamp == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM lookups WHERE timestamp = @timestamp";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@timestamp", timestamp);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        public bool DeleteLookupsByWordKey(string word_key) {
            if (word_key == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM lookups WHERE word_key = @word_key";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@word_key", word_key);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        private string GetSettings(string name) {
            if (name == string.Empty) {
                return string.Empty;
            }

            var query = "SELECT * FROM settings ";
            if (name != string.Empty) {
                query += "WHERE name = @name";
            }
            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@name", name);

            using SQLiteDataReader? reader = command.ExecuteReader();
            if (reader.Read()) {
                return reader["value"].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private void SetSettings(string name, string value) {
            if (string.IsNullOrEmpty(name)) {
                return;
            }

            const string query = "INSERT OR REPLACE INTO settings (name, value) VALUES (@name, @value)";
            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@value", value);

            command.ExecuteNonQuery();
        }

        public string GetTheme() {
            return GetSettings("theme");
        }

        public void SetTheme(string value) {
            SetSettings("theme", value);
        }

        public string GetLanguage() {
            return GetSettings("lang");
        }

        public void SetLanguage(string value) {
            SetSettings("lang", value);
        }

        public bool IsDarkTheme() {
            return GetTheme().Equals("dark", StringComparison.OrdinalIgnoreCase);
        }

        public void VacuumDatabase() {
            if (IsConnectionOpen()) {
                CloseConnection();

                using var vacuumConnection = new SQLiteConnection(ConnectionString);
                vacuumConnection.Open();

                using (var command = new SQLiteCommand("VACUUM;", vacuumConnection)) {
                    command.ExecuteNonQuery();
                }

                vacuumConnection.Close();

                OpenConnection();
            } else {
                using var vacuumConnection = new SQLiteConnection(ConnectionString);
                vacuumConnection.Open();

                using (var command = new SQLiteCommand("VACUUM;", vacuumConnection)) {
                    command.ExecuteNonQuery();
                }

                vacuumConnection.Close();
            }
        }

        private bool IsConnectionOpen() {
            return _connection.State == ConnectionState.Open;
        }

        public bool EmptyTables() {
            var result = 0;

            var tableNames = new List<string>() {
                "clippings", "lookups", "original_clipping_lines", "vocab"
            };

            foreach (var queryDelete in tableNames.Select(tableName => "DELETE FROM " + tableName)) {
                using var command = new SQLiteCommand(queryDelete, _connection);
                result += command.ExecuteNonQuery();
            }

            return result > 0;
        }

        public bool IsDatabaseEmpty() {
            var result = 0;

            var tableNames = new List<string>() {
                "clippings", "lookups", "original_clipping_lines", "vocab"
            };

            foreach (var queryCount in tableNames.Select(tableName => "SELECT COUNT(1) FROM " + tableName)) {
                using var commandCount = new SQLiteCommand(queryCount, _connection);
                result += Convert.ToInt32(commandCount.ExecuteScalar());
            }

            return result <= 0;
        }

        internal static string FormatFileSize(long fileSize) {
            string[] sizes = [
                "B", "KB", "MB", "GB", "TB"
            ];

            var order = 0;
            double size = fileSize;

            while (size >= 1024 && order < sizes.Length - 1) {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private static readonly Dictionary<char, int> romanMap = new() {
            {'I', 1},
            {'V', 5},
            {'X', 10},
            {'L', 50},
            {'C', 100},
            {'D', 500},
            {'M', 1000}
        };

        public int RomanToInteger(string roman) {
            var result = 0;
            var prevValue = 0;

            roman = roman.ToUpper();

            for (var i = roman.Length - 1; i >= 0; i--) {
                var value = romanMap[roman[i]];

                if (value < prevValue) {
                    result -= value;
                } else {
                    result += value;
                }

                prevValue = value;
            }

            return result;
        }
    }
}