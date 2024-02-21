using System.Data;
using System.Data.SQLite;

namespace KindleMate2 {
    public class StaticData {
        private readonly SQLiteConnection _connection = new("Data Source=KM2.dat;") {
            Site = null,
            DefaultTimeout = 0,
            DefaultMaximumSleepTime = 0,
            BusyTimeout = 0,
            WaitTimeout = 0,
            PrepareRetries = 0,
            StepRetries = 0,
            ProgressOps = 0,
            ParseViaFramework = false,
            Flags = SQLiteConnectionFlags.None,
            DefaultDbType = null,
            DefaultTypeName = null,
            VfsName = null,
            TraceFlags = SQLiteTraceFlags.SQLITE_TRACE_NONE
        };

        internal int GetClippingsCount() {
            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM clippings";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count;
        }

        public DataTable GetClipingsDataTable() {
            var dataTable = new DataTable();

            _connection.Open();

            const string queryClippings = "SELECT * FROM clippings;";
            using var command = new SQLiteCommand(queryClippings, _connection);
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);

            _connection.Close();

            return dataTable;
        }

        internal bool IsExistOriginalClippings(string? key) {
            switch (key) {
                case null:
                case "":
                    return true;
            }

            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM original_clipping_lines WHERE key = @key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@key", key);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count > 0;
        }

        internal bool IsExistClippings(string? key) {
            switch (key) {
                case null:
                case "":
                    return true;
            }

            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM clippings WHERE key = @key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@key", key);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count > 0;
        }

        internal bool IsExistClippingsOfContent(string? content) {
            switch (content) {
                case null:
                case "":
                    return true;
            }

            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM clippings WHERE content = @content";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@content", content);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count > 0;
        }

        internal int GetOriginClippingsCount() {
            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM original_clipping_lines";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count;
        }

        internal DataTable GetOriginClippingsDataTable() {
            var dataTable = new DataTable();

            _connection.Open();

            const string queryClippings = "SELECT * FROM original_clipping_lines;";
            using var command = new SQLiteCommand(queryClippings, _connection);
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);

            _connection.Close();

            return dataTable;
        }

        internal int InsertOriginClippings(string key, string line1, string line2, string line3, string line4, string line5) {
            if (key == string.Empty || line4 == string.Empty) {
                return 0;
            }

            _connection.Open();

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

            _connection.Close();

            return result;
        }

        internal bool DeleteClippingsByKey(string key) {
            if (key == string.Empty) {
                return false;
            }

            _connection.Open();

            const string queryDelete = "DELETE FROM clippings WHERE key = @key";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@key", key);
            var result = command.ExecuteNonQuery();

            _connection.Close();

            return result > 0;
        }

        internal bool DeleteClippingsByBook(string bookname) {
            if (bookname == string.Empty) {
                return false;
            }

            _connection.Open();

            const string queryDelete = "DELETE FROM clippings WHERE bookname = @bookname";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@bookname", bookname);
            var result = command.ExecuteNonQuery();

            _connection.Close();

            return result > 0;
        }

        internal int InsertClippings(string key, string content, string bookname, string authorname, int brieftype, string clippingtypelocation, string clippingdate, int pagenumber) {
            if (key == string.Empty || content == string.Empty) {
                return 0;
            }

            _connection.Open();

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

            _connection.Close();

            return result;
        }

        internal int InsertClippings(string key, string content, string bookname, string authorname, int brieftype, string clippingtypelocation, string clippingdate, int read, string clipping_importdate, string tag, int sync, string newbookname, int colorRGB, int pagenumber) {
            if (key == string.Empty || content == string.Empty) {
                return 0;
            }

            _connection.Open();

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

            _connection.Close();

            return result;
        }

        internal bool UpdateClippings(string originBookname, string bookname, string authorname) {
            if (string.IsNullOrWhiteSpace(originBookname) || string.IsNullOrWhiteSpace(bookname)) {
                return false;
            }

            _connection.Open();

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

            _connection.Close();

            return result > 0;
        }

        internal bool UpdateClippings(string key, string content) {
            if (key == string.Empty || content == string.Empty) {
                return false;
            }

            _connection.Open();

            const string queryUpdate = "UPDATE clippings SET content = @content WHERE key = @key";

            using var command = new SQLiteCommand(queryUpdate, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@content", DbType.String);

            command.Parameters["@key"].Value = key;
            command.Parameters["@content"].Value = content;

            var result = command.ExecuteNonQuery();

            _connection.Close();

            return result > 0;
        }

        internal void VacuumDatabase() {
            _connection.Open();

            const string queryVacuum = "VACUUM";
            using var command = new SQLiteCommand(queryVacuum, _connection);
            command.ExecuteNonQuery();

            _connection.Close();
        }

        internal int InsertLookups(string word_key, string usage, string title, string authors, string timestamp) {
            if (word_key == string.Empty || timestamp == string.Empty) {
                return 0;
            }

            _connection.Open();

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

            _connection.Close();

            return result;
        }

        internal int InsertVocab(string id, string word_key, string word, string stem, int category, string translation, string timestamp, int frequency, int sync, int colorRGB) {
            if (id == string.Empty || word == string.Empty) {
                return 0;
            }

            _connection.Open();

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

            _connection.Close();

            return result;
        }

        internal int InsertVocab(string id, string word_key, string word, string stem, int category, string timestamp, int frequency) {
            if (id == string.Empty || word == string.Empty) {
                return 0;
            }

            _connection.Open();

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

            _connection.Close();

            return result;
        }

        internal int UpdateVocab(string word_key, string word, string stem, int category, string timestamp, int frequency) {
            if (word == string.Empty) {
                return 0;
            }

            _connection.Open();

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

            _connection.Close();

            return result;
        }

        internal int UpdateVocab(string word_key, int frequency) {
            if (word_key == string.Empty) {
                return 0;
            }

            _connection.Open();

            const string query = "UPDATE vocab SET frequency = @frequency WHERE word_key = @word_key";
            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.Add("@word_key", DbType.String);
            command.Parameters.Add("@frequency", DbType.Int64);

            command.Parameters["@word_key"].Value = word_key;
            command.Parameters["@frequency"].Value = frequency;

            var result = command.ExecuteNonQuery();

            _connection.Close();

            return result;
        }

        internal DataTable GetVocabDataTable() {
            var dataTable = new DataTable();

            _connection.Open();

            const string query = "SELECT * FROM vocab;";
            using var command = new SQLiteCommand(query, _connection);
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);

            _connection.Close();

            return dataTable;
        }
        
        internal DataTable GetLookupsDataTable() {
            var dataTable = new DataTable();

            _connection.Open();

            const string query = "SELECT * FROM lookups;";
            using var command = new SQLiteCommand(query, _connection);
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);

            _connection.Close();

            return dataTable;
        }

        internal bool IsExistVocab(string id) {
            if (id == string.Empty) {
                return true;
            }

            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM vocab WHERE id = @id";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@id", id);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count > 0;
        }

        internal bool IsExistLookups(string timestamp) {
            if (timestamp == string.Empty) {
                return true;
            }

            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM lookups WHERE timestamp = @timestamp";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@timestamp", timestamp);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count > 0;
        }
    }
}