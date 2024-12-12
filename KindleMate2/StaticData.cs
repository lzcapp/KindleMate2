using System.Data;
using System.Data.SQLite;
using System.Net.NetworkInformation;
using System.Reflection;
using DarkModeForms;
using KindleMate2.Entities;
using Newtonsoft.Json;

namespace KindleMate2 {
    public class StaticData {
        private const string ConnectionString = "Data Source=KM2.dat;Version=3;";

        private readonly SQLiteConnection _connection = new(ConnectionString);

        private SQLiteTransaction? _trans;

        internal static bool CreateDatabase() {
            SQLiteConnection.CreateFile("KM2.dat");

            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();

            var createClippings = "CREATE TABLE [clippings] ([key] TEXT PRIMARY KEY NOT NULL UNIQUE, [content] TEXT DEFAULT(''), [bookname] TEXT DEFAULT(''), [authorname] TEXT, [brieftype] INTEGER, [clippingtypelocation] TEXT, [clippingdate] TEXT, [read] INT DEFAULT(0), [clipping_importdate] TEXT, [tag] TEXT, [sync] INT DEFAULT(0), [newbookname] TEXT, [colorRGB] INTEGER DEFAULT(-1), pagenumber INT DEFAULT(0));";
            using (var command = new SQLiteCommand(createClippings, connection)) {
                command.ExecuteNonQuery();
            }

            var createLookups = "CREATE TABLE [lookups] ([word_key] TEXT, [usage] TEXT, [title] TEXT, [authors] TEXT, [timestamp] TEXT UNIQUE);";
            using (var command = new SQLiteCommand(createLookups, connection)) {
                command.ExecuteNonQuery();
            }

            var createOriginalClippings = "CREATE TABLE [original_clipping_lines] ([key] TEXT PRIMARY KEY NOT NULL UNIQUE, [line1] TEXT DEFAULT(''), [line2] TEXT DEFAULT(''), [line3] TEXT DEFAULT(''), [line4] TEXT DEFAULT(''), [line5] TEXT DEFAULT(''));";
            using (var command = new SQLiteCommand(createOriginalClippings, connection)) {
                command.ExecuteNonQuery();
            }

            var createSettings = "CREATE TABLE [settings] ([name] TEXT PRIMARY KEY UNIQUE, [value] TEXT);";
            using (var command = new SQLiteCommand(createSettings, connection)) {
                command.ExecuteNonQuery();
            }

            var createVocab = "CREATE TABLE [vocab] ([id] TEXT PRIMARY KEY NOT NULL UNIQUE, [word_key] TEXT, [word] TEXT NOT NULL, [stem] TEXT, [category] INTEGER DEFAULT '0', [translation] TEXT, [timestamp] TEXT, [frequency] INT DEFAULT(0), [sync] INT DEFAULT(0), [colorRGB] INTEGER DEFAULT(-1));";
            using (var command = new SQLiteCommand(createVocab, connection)) {
                command.ExecuteNonQuery();
            }

            return true;
        }

        internal StaticData() {
            _connection.Open();

            using var command = new SQLiteCommand("PRAGMA synchronous=OFF", _connection);
            command.ExecuteNonQuery();
        }

        private void OpenConnection() {
            _connection.Open();
        }

        internal void CloseConnection() {
            _connection.Close();
        }

        internal void DisposeConnection() {
            _connection.Dispose();
        }

        internal void BeginTransaction() {
            _trans = _connection.BeginTransaction();
        }

        internal void CommitTransaction() {
            if (_trans != null) {
                _trans.Commit();
            }
        }

        internal void RollbackTransaction() {
            if (_trans != null) {
                _trans.Rollback();
            }
        }

        // ReSharper disable once IdentifierTypo
        internal DataTable GetClipingsDataTable() {
            var dataTable = new DataTable();

            const string queryClippings = "SELECT * FROM clippings WHERE brieftype <> -1;";
            using var command = new SQLiteCommand(queryClippings, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal DataTable GetClipingsDataTableFuzzySearch(string strSearch, string type) {
            var dataTable = new DataTable();

            var sql = string.Empty;
            if (type.Equals(Strings.Book_Title)) {
                sql = "WHERE bookname LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Author)) {
                sql = "WHERE authorname LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Content)) {
                sql = "WHERE content LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Select_All)) {
                sql = "WHERE content LIKE '%' || @strSearch || '%' OR bookname LIKE '%' || @strSearch || '%' OR authorname LIKE '%' || @strSearch || '%'";
            }
            var queryClippings = "SELECT DISTINCT * FROM clippings " + sql;
            using var command = new SQLiteCommand(queryClippings, _connection);
            command.Parameters.AddWithValue("@strSearch", strSearch);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal List<string> GetClippingsBookTitleList() {
            var list = new List<string>();
            DataTable dt = GetClipingsDataTable();
            if (dt.Rows.Count <= 0) {
                return list;
            }
            foreach (DataRow row in dt.Rows) {
                var bookTitle = row["bookname"].ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        internal List<string> GetClippingsAuthorList() {
            var list = new List<string>();
            DataTable dt = GetClipingsDataTable();
            if (dt.Rows.Count <= 0) {
                return list;
            }
            foreach (DataRow row in dt.Rows) {
                var bookTitle = row["authorname"].ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        internal bool SetClippingsBriefTypeHide(string bookname, string pagenumber) {
            switch (bookname) {
                case null:
                case "":
                    return true;
            }
            switch (pagenumber) {
                case null:
                case "":
                    return true;
            }

            const string query = "SELECT * FROM clippings WHERE bookname = @bookname AND pagenumber = @pagenumber";
            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@bookname", bookname);
            command.Parameters.AddWithValue("@pagenumber", pagenumber);
            using var adapter = new SQLiteDataAdapter(command);
            
            var dataTable = new DataTable();
            adapter.Fill(dataTable);

            if (dataTable.Rows.Count > 0) {
                DataRow row = dataTable.Rows[0];
                var book = row["bookname"].ToString() ?? string.Empty;
                var page = row["pagenumber"].ToString() ?? string.Empty;
                if (bookname.Equals(book) && pagenumber.Equals(page) ) {
                    const string queryCount = "UPDATE clippings SET brieftype = -1 WHERE key = @key";
                    using var commandCount = new SQLiteCommand(queryCount, _connection);
                    commandCount.Parameters.AddWithValue("@key", row["key"]);
                    var result = Convert.ToInt32(commandCount.ExecuteScalar());
                    return result > 0;
                }
            }
            return false;
        }

        internal string GetClippingsBriefTypeHide(string bookname, string pagenumber) {
            switch (bookname) {
                case null:
                case "":
                    return string.Empty;
            }

            const string queryCount = "SELECT * FROM clippings WHERE brieftype = -1 AND bookname = @bookname AND pagenumber = @pagenumber";
            using var command = new SQLiteCommand(queryCount, _connection);
            command.Parameters.AddWithValue("@bookname", bookname);
            command.Parameters.AddWithValue("@pagenumber", pagenumber);

            using var adapter = new SQLiteDataAdapter(command);
            
            var dataTable = new DataTable();
            adapter.Fill(dataTable);

            if (dataTable.Rows.Count > 0) {
                return dataTable.Rows[0]["content"].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        internal bool IsExistOriginalClippings(string? key) {
            switch (key) {
                case null:
                case "":
                    return true;
            }

            const string queryCount = "SELECT COUNT(*) FROM original_clipping_lines WHERE key = @key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@key", key);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        internal bool IsExistClippings(string? key) {
            switch (key) {
                case null:
                case "":
                    return true;
            }

            const string queryCount = "SELECT COUNT(*) FROM clippings WHERE key = @key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@key", key);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        internal bool IsExistClippingsOfContent(string? content) {
            switch (content) {
                case null:
                case "":
                    return true;
            }

            const string queryCount = "SELECT COUNT(*) FROM clippings WHERE content = @content";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@content", content);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        internal bool IsExistClippingsContainingContent(string? content) {
            if (content == null) {
                return false;
            }
            content = content.Trim();
            if (string.IsNullOrWhiteSpace(content)) {
                return false;
            }

            const string queryCount = "SELECT COUNT(*) FROM clippings WHERE content LIKE '%' || @content || '%'";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@content", content);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 1;
        }

        /*
        internal int GetOriginClippingsCount() {
            const string queryCount = "SELECT COUNT(1) FROM original_clipping_lines";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            return count;
        }
        */

        internal DataTable GetOriginClippingsDataTableFuzzySearch(string strSearch, string type) {
            var dataTable = new DataTable();

            var sql = string.Empty;
            if (type.Equals(Strings.Book_Title) || type.Equals(Strings.Author)) {
                sql = "WHERE line1 LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Content)) {
                sql = "WHERE line4 LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Select_All)) {
                sql = "WHERE line1 LIKE '%' || @strSearch || '%' OR line4 LIKE '%' || @strSearch || '%'";
            }
            var queryClippings = "SELECT DISTINCT * FROM original_clipping_lines " + sql;
            using var command = new SQLiteCommand(queryClippings, _connection);
            command.Parameters.AddWithValue("@strSearch", strSearch);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal DataTable GetOriginClippingsDataTable() {
            var dataTable = new DataTable();

            const string queryClippings = "SELECT * FROM original_clipping_lines;";
            using var command = new SQLiteCommand(queryClippings, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal bool InsertOriginClippings(string key, string line1, string line2, string line3, string line4, string line5) {
            if (key == string.Empty || line4 == string.Empty) {
                return false;
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

            return result > 0;
        }

        internal bool DeleteClippingsByKey(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            const string queryDelete = "DELETE FROM clippings WHERE key = @key";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@key", key);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        internal bool DeleteClippingsByBook(string bookname) {
            if (bookname == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM clippings WHERE bookname = @bookname";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@bookname", bookname);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        internal bool InsertClippings(Clipping entityClipping) {
            var result = Insert(entityClipping, "clippings", "key", true);
            return result > 0;
        }

        internal bool RenameBook(string originBookname, string bookname, string authorname) {
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

        internal bool UpdateClippings(string key, string content, string bookname) {
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            string sql;
            if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(bookname)) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(content)) {
                sql = "bookname = @bookname";
            } else if (string.IsNullOrWhiteSpace(bookname)) {
                sql = "content = @content";
            } else {
                sql = "content = @content, bookname = @bookname";
            }

            var queryUpdate = "UPDATE clippings SET " + sql + " WHERE key = @key";

            using var command = new SQLiteCommand(queryUpdate, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@content", DbType.String);
            command.Parameters.Add("@bookname", DbType.String);

            command.Parameters["@key"].Value = key;
            command.Parameters["@content"].Value = content;
            command.Parameters["@bookname"].Value = bookname;

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        internal int InsertLookups(string word_key, string usage, string title, string authors, string timestamp) {
            if (string.IsNullOrWhiteSpace(word_key) || string.IsNullOrWhiteSpace(timestamp)) {
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

        internal void UpdateLookups(string origintitle, string title, string authors) {
            if (string.IsNullOrWhiteSpace(origintitle) || string.IsNullOrWhiteSpace(title)) {
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
        internal bool InsertVocab(string id, string word_key, string word, string stem, int category, string translation, string timestamp, int frequency, int sync, int colorRGB) {
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

        internal int InsertVocab(string id, string word_key, string word, string stem, int category, string timestamp, int frequency) {
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
                internal bool UpdateVocab(string word_key, string word, string stem, int category, string timestamp, int frequency) {
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

        internal void UpdateVocab(string word_key, int frequency) {
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

        internal DataTable GetVocabDataTable() {
            var dataTable = new DataTable();

            const string query = "SELECT * FROM vocab;";
            using var command = new SQLiteCommand(query, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal DataTable GetVocabDataTableFuzzySearch(string strSearch, string type) {
            var dataTable = new DataTable();

            var sql = string.Empty;
            if (type.Equals(Strings.Vocabulary)) {
                sql = "WHERE word LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Stem)) {
                sql = "WHERE stem LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Select_All)) {
                sql = "WHERE word LIKE '%' || @strSearch || '%' OR stem LIKE '%' || @strSearch || '%'";
            }
            var query = "SELECT DISTINCT * FROM vocab " + sql;
            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@strSearch", strSearch);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal List<string> GetVocabWordList() {
            var list = new List<string>();
            DataTable dt = GetVocabDataTable();
            if (dt.Rows.Count <= 0) {
                return list;
            }
            foreach (DataRow row in dt.Rows) {
                var bookTitle = row["word"].ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        internal List<string> GetVocabStemList() {
            var list = new List<string>();
            DataTable dt = GetVocabDataTable();
            if (dt.Rows.Count <= 0) {
                return list;
            }
            foreach (DataRow row in dt.Rows) {
                var bookTitle = row["stem"].ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(bookTitle) && !list.Contains(bookTitle)) {
                    list.Add(bookTitle);
                }
            }
            return list;
        }

        internal DataTable GetLookupsDataTable() {
            var dataTable = new DataTable();

            const string query = "SELECT DISTINCT * FROM lookups";
            using var command = new SQLiteCommand(query, _connection);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal DataTable GetLookupsDataTableFuzzySearch(string strSearch, string type) {
            var dataTable = new DataTable();

            var sql = string.Empty;
            if (type.Equals(Strings.Book_Title)) {
                sql = "WHERE title LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Author)) {
                sql = "WHERE authors LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Content)) {
                sql = "WHERE usage LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Vocabulary) || type.Equals(Strings.Stem)) {
                sql = "WHERE word_key LIKE '%' || @strSearch || '%'";
            } else if (type.Equals(Strings.Select_All)) {
                sql = "WHERE word_key LIKE '%' || @strSearch || '%' OR usage LIKE '%' || @strSearch || '%' OR title LIKE '%' || @strSearch || '%' OR authors LIKE '%' || @strSearch || '%'";
            }
            var query = "SELECT DISTINCT * FROM lookups " + sql;
            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@strSearch", strSearch);
            using var adapter = new SQLiteDataAdapter(command);

            adapter.Fill(dataTable);

            return dataTable;
        }

        internal bool IsExistVocab(string word_key) {
            if (word_key == string.Empty) {
                return false;
            }

            const string queryCount = "SELECT COUNT(*) FROM vocab WHERE word_key = @word_key";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@word_key", word_key);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        internal bool IsExistVocabById(string id) {
            if (id == string.Empty) {
                return false;
            }

            const string queryCount = "SELECT COUNT(*) FROM vocab WHERE id = @id";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@id", id);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        internal bool IsExistLookups(string timestamp) {
            if (timestamp == string.Empty) {
                return true;
            }

            const string queryCount = "SELECT COUNT(*) FROM lookups WHERE timestamp = @timestamp";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            commandCount.Parameters.AddWithValue("@timestamp", timestamp);

            var result = Convert.ToInt32(commandCount.ExecuteScalar());

            return result > 0;
        }

        internal bool DeleteVocab(string word_key) {
            if (word_key == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM vocab WHERE word_key = @word_key";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@word_key", word_key);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        internal bool DeleteLookupsByTimeStamp(string timestamp) {
            if (timestamp == string.Empty) {
                return false;
            }

            const string queryDelete = "DELETE FROM lookups WHERE timestamp = @timestamp";
            using var command = new SQLiteCommand(queryDelete, _connection);
            command.Parameters.AddWithValue("@timestamp", timestamp);

            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        internal bool DeleteLookupsByWordKey(string word_key) {
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

        private string GetTheme() {
            return GetSettings("theme");
        }

        internal bool IsDarkTheme() {
            var theme = GetTheme();
            bool isWindowsDarkTheme;
            if (string.IsNullOrWhiteSpace(theme)) {
                isWindowsDarkTheme = IsWindowsDarkTheme();
                SetTheme(isWindowsDarkTheme);
            }
            if (theme.Equals("dark", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (theme.Equals("light", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            isWindowsDarkTheme = IsWindowsDarkTheme();
            SetTheme(isWindowsDarkTheme);
            return isWindowsDarkTheme;
        }

        private static bool IsWindowsDarkTheme() {
            var isWindowsDarkTheme = DarkModeCS.GetWindowsColorMode() <= 0;
            return isWindowsDarkTheme;
        }

        internal void SetTheme(string value) {
            SetSettings("theme", value);
        }

        private void SetTheme(bool isDarkTheme) {
            SetTheme(isDarkTheme ? "dark" : "light");
        }

        internal string GetLanguage() {
            return GetSettings("lang");
        }

        internal void SetLanguage(string value) {
            SetSettings("lang", value);
        }

        internal void VacuumDatabase() {
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

        internal bool EmptyTables() {
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

        internal bool EmptyTable(string tableName) {
            using var command = new SQLiteCommand("DELETE FROM " + tableName, _connection);
            var result = command.ExecuteNonQuery();

            return result > 0;
        }

        internal bool IsDatabaseEmpty() {
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
            { 'I', 1 }, 
            { 'V', 5 }, 
            { 'X', 10 }, 
            { 'L', 50 }, 
            { 'C', 100 }, 
            { 'D', 500 }, 
            { 'M', 1000 }
        };

        internal static int RomanToInteger(string roman) {
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

        private int Insert<T>(T entity, string tableName, string primaryKey, bool isAddPrimaryKey) {
            if (entity != null) {
                var properties = entity.GetType().GetProperties();

                var col = string.Empty;
                var val = string.Empty;
                var parameters = new List<SQLiteParameter>();
                
                foreach (PropertyInfo property in properties) {
                    var parameterName = property.Name;
                    if (parameterName.Equals("Ctypes", StringComparison.CurrentCultureIgnoreCase)) {
                        parameterName = "Ctype";
                    }

                    var parameter = new SQLiteParameter();
                    var obj = property.GetValue(entity, null);

                    if (!isAddPrimaryKey && parameterName.Equals(primaryKey, StringComparison.CurrentCultureIgnoreCase)) {
                        continue;
                    }

                    col += parameterName + ",";
                    val += "@" + parameterName + ",";
                    parameter.ParameterName = "@" + parameterName;

                    switch (obj) {
                        case null:
                        case DateTime dateTime when dateTime < new DateTime(1753, 1, 1) || dateTime > new DateTime(9999, 12, 31):
                            parameter.Value = null;
                            break;
                        default:
                            parameter.Value = obj;
                            break;
                    }
                    parameters.Add(parameter);
                }

                col = col.TrimEnd(',');
                val = val.TrimEnd(',');
                var sql = $"INSERT INTO {tableName} ({col}) VALUES ({val})";
                using var command = new SQLiteCommand(sql, _connection);
                foreach (SQLiteParameter parameter in parameters) {
                    command.Parameters.Add(parameter);
                }
                var result = command.ExecuteNonQuery();
                return result;
            }
            return 0;
        }

        public static bool IsUpdate(string tagname) {
            var assemblyVersion = GetAssemblyName();
            return IsUpdate(assemblyVersion, tagname);
        }

        public static bool IsUpdate(string current, string tagname) {
            DateTime normalizeVersion = NormalizeVersion(current);
            DateTime normalizeTagName = NormalizeVersion(tagname);
            if (normalizeVersion != DateTime.MinValue || normalizeTagName != DateTime.MinValue) {
                return normalizeVersion < normalizeTagName;
            }
            var splitVersion = current.Split('.');
            var splitTagname = current.Split('.');
            for (var i = 0; i < 3; i++) {
                if (!int.TryParse(splitVersion[i], out var intVersion) || !int.TryParse(splitTagname[i], out var intTagname)) {
                    continue;
                }
                if (intVersion < intTagname) {
                    return true;
                }
            }
            return false;
        }

        private static DateTime NormalizeVersion(string version) {
            var date = DateTime.MinValue;
            var parts = version.Split('.');
            if (int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month) && int.TryParse(parts[2], out var day)) {
                date = new DateTime(year, month, day);
            }
            return date;
        }

        public static GitHubRelease GetRepoInfo() {
            try {
                const string url = "https://api.github.com/repos/lzcapp/KindleMate2/releases";
                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(1);
                httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
                var response = httpClient.GetStringAsync(url).Result;
                return JsonConvert.DeserializeObject<GitHubRelease[]>(response)?[0] ?? new GitHubRelease();
            } catch (Exception e) {
                Console.WriteLine(e);
                return new GitHubRelease();
            }
        }

        private static string GetAssemblyName() {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        }

        public static string GetVersionName() {
            var name = string.Empty;
            if (Environment.Is64BitProcess) {
                name += "_x64";
            } else {
                name += "_x86";
            }
            var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var coreClrPath = Path.Combine(exeDirectory, "coreclr.dll");
            if (File.Exists(coreClrPath)) {
                name += "_runtime";
            }
            return name;
        }

        internal static bool IsInternetAvailable() {
            try {
                if (NetworkInterface.GetIsNetworkAvailable()) {
                    if (NetworkInterface.GetAllNetworkInterfaces().Any(ni => ni.OperationalStatus == OperationalStatus.Up)) {
                        return true;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Error checking internet connection: " + ex.Message);
            }
            return false;
        }
    }
}