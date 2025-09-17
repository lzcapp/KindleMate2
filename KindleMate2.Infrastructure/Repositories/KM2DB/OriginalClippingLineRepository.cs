using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class OriginalClippingLineRepository : IOriginalClippingLineRepository {
        private readonly string _connectionString;
        private IOriginalClippingLineRepository _originalClippingLineRepositoryImplementation;

        public OriginalClippingLineRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public OriginalClippingLine? GetByKey(string key) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines WHERE key = @key", connection);
                cmd.Parameters.AddWithValue("@key", key);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new OriginalClippingLine {
                        key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        line1 = DatabaseHelper.GetSafeString(reader, 1),
                        line2 = DatabaseHelper.GetSafeString(reader, 2),
                        line3 = DatabaseHelper.GetSafeString(reader, 3),
                        line4 = DatabaseHelper.GetSafeString(reader, 4),
                        line5 = DatabaseHelper.GetSafeString(reader, 5)
                    };
                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
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
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new OriginalClippingLine {
                    key = key,
                    line1 = DatabaseHelper.GetSafeString(reader, 1),
                    line2 = DatabaseHelper.GetSafeString(reader, 2),
                    line3 = DatabaseHelper.GetSafeString(reader, 3),
                    line4 = DatabaseHelper.GetSafeString(reader, 4),
                    line5 = DatabaseHelper.GetSafeString(reader, 5)
                });
            }
            return results;
        }

        public HashSet<string> GetAllKeys() {
            var results = new HashSet<string>();

            using var connection = new SqliteConnection(_connectionString);
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

            using var connection = new SqliteConnection(_connectionString);
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
                    key = key,
                    line1 = DatabaseHelper.GetSafeString(reader, 1),
                    line2 = DatabaseHelper.GetSafeString(reader, 2),
                    line3 = DatabaseHelper.GetSafeString(reader, 3),
                    line4 = DatabaseHelper.GetSafeString(reader, 4),
                    line5 = DatabaseHelper.GetSafeString(reader, 5)
                });
            }
            return results;
        }

        public int GetCount() {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM original_clipping_lines", connection);
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(OriginalClippingLine originalClippingLine) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)", connection);
                cmd.Parameters.AddWithValue("@key", originalClippingLine.key ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@line1", originalClippingLine.line1 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line2", originalClippingLine.line2 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line3", originalClippingLine.line3 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line4", originalClippingLine.line4 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line5", originalClippingLine.line5 ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public int Add(List<OriginalClippingLine> listOriginalClippings) {
            var count = 0;
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                foreach (OriginalClippingLine originalClippingLine in listOriginalClippings) {
                    var cmd = new SqliteCommand("INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)", connection);
                    cmd.Parameters.AddWithValue("@key", originalClippingLine.key ?? throw new InvalidOperationException());
                    cmd.Parameters.AddWithValue("@line1", originalClippingLine.line1 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@line2", originalClippingLine.line2 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@line3", originalClippingLine.line3 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@line4", originalClippingLine.line4 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@line5", originalClippingLine.line5 ?? (object)DBNull.Value);
                    if (cmd.ExecuteNonQuery() > 0) {
                        count++;
                    }
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return count;
        }

        public bool Update(OriginalClippingLine originalClippingLine) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE original_clipping_lines SET line1 = @line1, line2 = @line2, line3 = @line3, line4 = @line4, line5 = @line5 WHERE key = @key", connection);
                cmd.Parameters.AddWithValue("@key", originalClippingLine.key ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@line1", originalClippingLine.line1 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line2", originalClippingLine.line2 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line3", originalClippingLine.line3 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line4", originalClippingLine.line4 ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@line5", originalClippingLine.line5 ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Delete(string key) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM original_clipping_lines WHERE key = @key", connection);
                cmd.Parameters.AddWithValue("@key", key);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool DeleteAll() {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM original_clipping_lines", connection);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}