using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class OriginalClippingLineRepository(string connectionString) : IOriginalClippingLineRepository {
        public OriginalClippingLine? GetByKey(string key) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", key);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new OriginalClippingLine {
                    Key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                    Line1 = DatabaseHelper.GetSafeString(reader, 1),
                    Line2 = DatabaseHelper.GetSafeString(reader, 2),
                    Line3 = DatabaseHelper.GetSafeString(reader, 3),
                    Line4 = DatabaseHelper.GetSafeString(reader, 4),
                    Line5 = DatabaseHelper.GetSafeString(reader, 5)
                };
            }
            return null;
        }

        public List<OriginalClippingLine> GetAll() {
            var results = new List<OriginalClippingLine>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new OriginalClippingLine {
                    Key = key,
                    Line1 = DatabaseHelper.GetSafeString(reader, 1),
                    Line2 = DatabaseHelper.GetSafeString(reader, 2),
                    Line3 = DatabaseHelper.GetSafeString(reader, 3),
                    Line4 = DatabaseHelper.GetSafeString(reader, 4),
                    Line5 = DatabaseHelper.GetSafeString(reader, 5)
                });
            }
            return results;
        }

        public HashSet<string> GetAllKeys() {
            var results = new HashSet<string>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key FROM original_clipping_lines", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(key);
            }
            return results;
        }

        public List<OriginalClippingLine> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<OriginalClippingLine>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var sql = type switch {
                AppEntities.SearchType.BookTitle or AppEntities.SearchType.Author => "WHERE line1 LIKE '%' || @strSearch || '%'",
                AppEntities.SearchType.Content => "WHERE line4 LIKE '%' || @strSearch || '%'",
                AppEntities.SearchType.All => "WHERE line1 LIKE '%' || @strSearch || '%' OR line4 LIKE '%' || @strSearch || '%'",
                _ => string.Empty
            };
            sql = "SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines " + sql;
            var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@strSearch", search);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new OriginalClippingLine {
                    Key = key,
                    Line1 = DatabaseHelper.GetSafeString(reader, 1),
                    Line2 = DatabaseHelper.GetSafeString(reader, 2),
                    Line3 = DatabaseHelper.GetSafeString(reader, 3),
                    Line4 = DatabaseHelper.GetSafeString(reader, 4),
                    Line5 = DatabaseHelper.GetSafeString(reader, 5)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM original_clipping_lines", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(OriginalClippingLine originalClippingLine) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)", connection);
            cmd.Parameters.AddWithValue("@key", originalClippingLine.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@line1", originalClippingLine.Line1 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line2", originalClippingLine.Line2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line3", originalClippingLine.Line3 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line4", originalClippingLine.Line4 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line5", originalClippingLine.Line5 ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public int Add(List<OriginalClippingLine> listOriginalClippings) {
            var count = 0;
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            foreach (OriginalClippingLine originalClippingLine in listOriginalClippings) {
                var cmd = new SqliteCommand("INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)", connection);
                cmd.Parameters.AddWithValue("@key", originalClippingLine.Key ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@line1", originalClippingLine.Line1 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line2", originalClippingLine.Line2 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line3", originalClippingLine.Line3 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line4", originalClippingLine.Line4 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line5", originalClippingLine.Line5 ?? (object)DBNull.Value);
                if (cmd.ExecuteNonQuery() > 0) {
                    count++;
                }
            }

            transaction.Commit();
            return count;
        }

        public bool Update(OriginalClippingLine originalClippingLine) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE original_clipping_lines SET line1 = @line1, line2 = @line2, line3 = @line3, line4 = @line4, line5 = @line5 WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", originalClippingLine.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@line1", originalClippingLine.Line1 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line2", originalClippingLine.Line2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line3", originalClippingLine.Line3 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line4", originalClippingLine.Line4 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line5", originalClippingLine.Line5 ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string key) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM original_clipping_lines WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", key);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteAll() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM original_clipping_lines", connection);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
