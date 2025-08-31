using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class OriginalClippingLineRepository : IOriginalClippingLineRepository {
        private readonly string _connectionString;

        public OriginalClippingLineRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public OriginalClippingLine? GetByKey(string key) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", key);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new OriginalClippingLine {
                    key = reader.GetString(0),
                    line1 = reader.GetString(1),
                    line2 = reader.GetString(2),
                    line3 = reader.GetString(3),
                    line4 = reader.GetString(4),
                    line5 = reader.GetString(5)
                };
            }
            return null;
        }
        public List<OriginalClippingLine> GetAll() {
            var results = new List<OriginalClippingLine>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new OriginalClippingLine {
                    key = reader.GetString(0),
                    line1 = reader.GetString(1),
                    line2 = reader.GetString(2),
                    line3 = reader.GetString(3),
                    line4 = reader.GetString(4),
                    line5 = reader.GetString(5)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM original_clipping_lines", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(OriginalClippingLine originalClippingLine) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)", connection);
            cmd.Parameters.AddWithValue("@key", originalClippingLine.key);
            cmd.Parameters.AddWithValue("@line1", originalClippingLine.line1);
            cmd.Parameters.AddWithValue("@line2", originalClippingLine.line2);
            cmd.Parameters.AddWithValue("@line3", originalClippingLine.line3);
            cmd.Parameters.AddWithValue("@line4", originalClippingLine.line4);
            cmd.Parameters.AddWithValue("@line5", originalClippingLine.line5);
            cmd.ExecuteNonQuery();
        }
        public void Update(OriginalClippingLine originalClippingLine) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE original_clipping_lines SET line1 = @line1, line2 = @line2, line3 = @line3, line4 = @line4, line5 = @line5 WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", originalClippingLine.key);
            cmd.Parameters.AddWithValue("@line1", originalClippingLine.line1);
            cmd.Parameters.AddWithValue("@line2", originalClippingLine.line2);
            cmd.Parameters.AddWithValue("@line3", originalClippingLine.line3);
            cmd.Parameters.AddWithValue("@line4", originalClippingLine.line4);
            cmd.Parameters.AddWithValue("@line5", originalClippingLine.line5);
            cmd.ExecuteNonQuery();
        }
        public void Delete(string key) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM original_clipping_lines WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.ExecuteNonQuery();
        }
    }
}
