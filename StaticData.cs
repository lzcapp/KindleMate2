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

        public int GetClippingsCount() {
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

        public int GetOriginClippingsCount() {
            _connection.Open();

            const string queryCount = "SELECT COUNT(*) FROM original_clipping_lines";
            using var commandCount = new SQLiteCommand(queryCount, _connection);
            var count = Convert.ToInt32(commandCount.ExecuteScalar());

            _connection.Close();

            return count;
        }

        public DataTable GetOriginClippingsDataTable() {
            var dataTable = new DataTable();

            _connection.Open();

            const string queryClippings = "SELECT * FROM original_clipping_lines;";
            using var command = new SQLiteCommand(queryClippings, _connection);
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);

            _connection.Close();

            return dataTable;
        }

        public int InsertOriginClippingsDataTable(DataTable dataTable) {
            if (dataTable.Rows.Count == 0) {
                return 0;
            }

            var result = 0;

            _connection.Open();

            const string queryInsert = "INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)";
            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@line1", DbType.String);
            command.Parameters.Add("@line2", DbType.String);
            command.Parameters.Add("@line3", DbType.String);
            command.Parameters.Add("@line4", DbType.String);
            command.Parameters.Add("@line5", DbType.String);

            foreach (DataRow row in dataTable.Rows) {
                command.Parameters["@key"].Value = row["key"];
                command.Parameters["@line1"].Value = row["line1"];
                command.Parameters["@line2"].Value = row["line2"];
                command.Parameters["@line3"].Value = row["line3"];
                command.Parameters["@line4"].Value = row["line4"];
                command.Parameters["@line5"].Value = row["line5"];
                result += command.ExecuteNonQuery();
            }

            _connection.Close();

            return result;
        }

        public bool DeleteClippingsByKey(string key) {
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

        public bool DeleteClippingsByBook(string bookname) {
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

        public bool InsertClippingsDataTable(DataTable clippingsTable) {
            if (clippingsTable.Rows.Count == 0) {
                return false;
            }

            var result = 0;

            _connection.Open();

            const string queryInsert = "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)";
            using var command = new SQLiteCommand(queryInsert, _connection);
            command.Parameters.Add("@key", DbType.String);
            command.Parameters.Add("@content", DbType.String);
            command.Parameters.Add("@bookname", DbType.String);
            command.Parameters.Add("@authorname", DbType.String);
            command.Parameters.Add("@brieftype", DbType.Int16);
            command.Parameters.Add("@clippingtypelocation", DbType.String);
            command.Parameters.Add("@clippingdate", DbType.String);
            command.Parameters.Add("@read", DbType.String);
            command.Parameters.Add("@clipping_importdate", DbType.String);
            command.Parameters.Add("@tag", DbType.String);
            command.Parameters.Add("@sync", DbType.Int16);
            command.Parameters.Add("@newbookname", DbType.String);
            command.Parameters.Add("@colorRGB", DbType.Int16);
            command.Parameters.Add("@pagenumber", DbType.Int16);

            foreach (DataRow row in clippingsTable.Rows) {
                command.Parameters["@key"].Value = row["key"];
                command.Parameters["@content"].Value = row["content"];
                command.Parameters["@bookname"].Value = row["bookname"];
                command.Parameters["@authorname"].Value = row["authorname"];
                command.Parameters["@brieftype"].Value = row["brieftype"];
                command.Parameters["@clippingtypelocation"].Value = row["clippingtypelocation"];
                command.Parameters["@clippingdate"].Value = row["clippingdate"];
                command.Parameters["@read"].Value = row["read"];
                command.Parameters["@clipping_importdate"].Value = row["clipping_importdate"];
                command.Parameters["@tag"].Value = row["tag"];
                command.Parameters["@sync"].Value = row["sync"];
                command.Parameters["@newbookname"].Value = row["newbookname"];
                command.Parameters["@colorRGB"].Value = row["colorRGB"];
                command.Parameters["@pagenumber"].Value = row["pagenumber"];
                result += command.ExecuteNonQuery();
            }

            _connection.Close();

            return result > 0;
        }
    }
}