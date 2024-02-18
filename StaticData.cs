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
    }
}