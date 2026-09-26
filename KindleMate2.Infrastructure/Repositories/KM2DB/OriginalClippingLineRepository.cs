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
                AppEntities.SearchType.BookTitle or AppEntities.SearchType.Author => "WHERE line1 LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                AppEntities.SearchType.Content => "WHERE line4 LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                AppEntities.SearchType.All => "WHERE line1 LIKE '%' || @strSearch || '%' ESCAPE '\\' OR line4 LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                _ => string.Empty
            };
            sql = "SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines " + sql;
            var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@strSearch", DatabaseHelper.EscapeLikePattern(search));

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

        /// <summary>
        /// 批量插入。与 <c>ClippingRepository.Add(List&lt;Clipping&gt;)</c> 同样的策略:
        /// **先走整批单事务,失败则降级为逐条插入并跳过冲突项** —— 避免一条重复 key
        /// 让整批回滚(否则"判重漏网"的代价就是整份文件导不进来)。
        /// </summary>
        public int Add(List<OriginalClippingLine> listOriginalClippings) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // ① 快路径:整批单事务
            using (var transaction = connection.BeginTransaction()) {
                try {
                    var count = 0;
                    foreach (OriginalClippingLine originalClippingLine in listOriginalClippings) {
                        if (InsertOne(connection, transaction, originalClippingLine)) {
                            count++;
                        }
                    }
                    transaction.Commit();
                    return count;
                } catch {
                    try { transaction.Rollback(); } catch { /* 回滚失败也不能击穿兜底承诺 */ }
                }
            }

            // ② 兜底:逐条插入,每条独立事务,单条失败只跳过该条
            //    但**只跳过可跳过项**(重复 key / key 为空);磁盘满、库被锁等系统性问题向上抛。
            var inserted = 0;
            foreach (OriginalClippingLine originalClippingLine in listOriginalClippings) {
                using var rowTransaction = connection.BeginTransaction();
                try {
                    if (InsertOne(connection, rowTransaction, originalClippingLine)) {
                        inserted++;
                    }
                    rowTransaction.Commit();
                } catch (Exception ex) when (DatabaseHelper.IsSkippableInsertFailure(ex)) {
                    try { rowTransaction.Rollback(); } catch { /* 回滚失败也不能击穿兜底承诺 */ }
                }
            }
            return inserted;
        }

        /// <summary>插入一行 —— 供"整批"与"逐条降级"两条路径复用。</summary>
        private static bool InsertOne(SqliteConnection connection, SqliteTransaction transaction,
            OriginalClippingLine originalClippingLine) {
            using var cmd = new SqliteCommand("INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)", connection, transaction);
            cmd.Parameters.AddWithValue("@key", originalClippingLine.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@line1", originalClippingLine.Line1 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line2", originalClippingLine.Line2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line3", originalClippingLine.Line3 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line4", originalClippingLine.Line4 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line5", originalClippingLine.Line5 ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
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
            // 0 行受影响也是成功(空表):false 只应代表执行失败,而失败会抛异常由上层 catch。
            cmd.ExecuteNonQuery();
            return true;
        }
    }
}
