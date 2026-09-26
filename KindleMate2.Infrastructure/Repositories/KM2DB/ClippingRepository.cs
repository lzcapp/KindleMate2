using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class ClippingRepository(string connectionString) : IClippingRepository {
        public Clipping? GetByKey(string key) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE key = @key",
                connection);
            if (string.IsNullOrWhiteSpace(key)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@key", key);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Clipping {
                    Key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                    Content = DatabaseHelper.GetSafeString(reader, 1) ?? throw new InvalidOperationException(),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                };
            }
            return null;
        }

        public Clipping? GetByKeyAndContent(string key, string content) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE key = @key AND content = @content",
                connection);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@content", content);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Clipping {
                    Key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                    Content = DatabaseHelper.GetSafeString(reader, 1) ?? throw new InvalidOperationException(),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                };
            }
            return null;
        }

        public List<Clipping> GetByBookNameAndPageNumber(string bookname, int pagenumber) {
            var results = new List<Clipping>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE bookname = @bookname AND pagenumber = @pagenumber",
                connection);
            cmd.Parameters.AddWithValue("@bookname", bookname);
            cmd.Parameters.AddWithValue("@pagenumber", pagenumber);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (key == null) {
                    continue;
                }
                var content = DatabaseHelper.GetSafeString(reader, 1);
                if (content == null) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = content,
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByBookNameAndPageNumberAndBriefType(string bookname, int pagenumber, BriefType brieftype) {
            var results = new List<Clipping>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE bookname = @bookname AND pagenumber = @pagenumber AND brieftype = @brieftype",
                connection);
            cmd.Parameters.AddWithValue("@bookname", bookname);
            cmd.Parameters.AddWithValue("@pagenumber", pagenumber);
            cmd.Parameters.AddWithValue("@brieftype", brieftype);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (key == null) {
                    continue;
                }
                var content = DatabaseHelper.GetSafeString(reader, 1);
                if (content == null) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = content,
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Clipping>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var sql = type switch {
                AppEntities.SearchType.BookTitle => "WHERE bookname LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                AppEntities.SearchType.Author => "WHERE authorname LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                AppEntities.SearchType.Content => "WHERE content LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                AppEntities.SearchType.All => "WHERE content LIKE '%' || @strSearch || '%' ESCAPE '\\' OR bookname LIKE '%' || @strSearch || '%' ESCAPE '\\' OR authorname LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                _ => string.Empty
            };
            var query = "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings " + sql;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", DatabaseHelper.EscapeLikePattern(search));

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (key == null) {
                    continue;
                }
                var content = DatabaseHelper.GetSafeString(reader, 1);
                if (content == null) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = content,
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByContent(string content) {
            var results = new List<Clipping>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE content = @content", connection);
            cmd.Parameters.AddWithValue("@content", content);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (key == null) {
                    continue;
                }
                content = DatabaseHelper.GetSafeString(reader, 1) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(content)) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = content,
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4) ?? 0,
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetAll() {
            var results = new List<Clipping>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings ORDER BY bookname, pagenumber, clippingdate", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (key == null) {
                    continue;
                }
                var content = DatabaseHelper.GetSafeString(reader, 1);
                if (content == null) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = content,
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4) ?? 0,
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByBookName(string bookname) {
            var results = new List<Clipping>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE bookname = @bookname", connection);
            cmd.Parameters.AddWithValue("@bookname", bookname);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (key == null) {
                    continue;
                }
                var content = DatabaseHelper.GetSafeString(reader, 1);
                if (content == null) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = content,
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<string> GetBookNamesList() {
            var results = new List<string>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT DISTINCT bookname FROM clippings", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var bookName = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(bookName)) {
                    continue;
                }
                results.Add(bookName);
            }
            results = results.OrderBy(x => x).ToList();
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM clippings", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Clipping clipping) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)",
                connection);
            cmd.Parameters.AddWithValue("@key", clipping.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@content", clipping.Content);
            cmd.Parameters.AddWithValue("@bookname", clipping.BookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@read", clipping.Read ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tag", clipping.Tag ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", clipping.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 批量插入。**先走整批单事务(最快);整批失败则降级为逐条插入** ——
        /// 逐条时跳过违反约束(如同一源文件里的重复 key)或数据非法的行,
        /// 使"判重可能还有漏网之鱼"的后果从**整份文件全部导入失败**降为**只少几条**。
        /// 实际插入的条数由返回值给出,调用方据此展示"导入 N 条"。
        /// </summary>
        public int Add(List<Clipping> listClippings) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // ① 快路径:整批单事务
            using (var transaction = connection.BeginTransaction()) {
                try {
                    var count = 0;
                    foreach (Clipping clipping in listClippings) {
                        if (InsertOne(connection, transaction, clipping)) {
                            count++;
                        }
                    }
                    transaction.Commit();
                    return count;
                } catch {
                    try { transaction.Rollback(); } catch { /* 回滚失败也不能击穿兜底承诺 */ }
                }
            }

            // ② 兜底:逐条插入,每条独立事务,单条失败只跳过该条而不影响其余
            //    但**只跳过可跳过项**(重复 key / key 为空);磁盘满、库被锁等系统性问题向上抛,
            //    否则会被误报成「导入成功、只少几条」。
            var inserted = 0;
            foreach (Clipping clipping in listClippings) {
                using var rowTransaction = connection.BeginTransaction();
                try {
                    if (InsertOne(connection, rowTransaction, clipping)) {
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
        private static bool InsertOne(SqliteConnection connection, SqliteTransaction transaction, Clipping clipping) {
            using var cmd = new SqliteCommand(
                "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)",
                connection, transaction);
            cmd.Parameters.AddWithValue("@key", clipping.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@content", clipping.Content);
            cmd.Parameters.AddWithValue("@bookname", clipping.BookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@read", clipping.Read ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tag", clipping.Tag ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", clipping.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Update(Clipping clipping) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "UPDATE clippings SET content = @content, bookname = @bookname, authorname = @authorname, brieftype = @brieftype, clippingtypelocation = @clippingtypelocation, clippingdate = @clippingdate, read = @read, clipping_importdate = @clipping_importdate, tag = @tag, sync = @sync, newbookname = @newbookname, colorRGB = @colorRGB, pagenumber = @pagenumber WHERE key = @key",
                connection);
            cmd.Parameters.AddWithValue("@key", clipping.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@content", clipping.Content);
            cmd.Parameters.AddWithValue("@bookname", clipping.BookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@read", clipping.Read ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tag", clipping.Tag ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", clipping.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UpdateBriefTypeByKey(Clipping clipping) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE clippings SET brieftype = @brieftype WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", clipping.Key);
            cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string key) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM clippings WHERE key = @key", connection);
            if (string.IsNullOrWhiteSpace(key)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@key", key);
            return cmd.ExecuteNonQuery() > 0;
        }

        public int Delete(List<Clipping> listClippings) {
            var count = 0;
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // One transaction + one reused command instead of an auto-committed DELETE
            // per row — SQLite fsyncs on every standalone commit, which made bulk
            // cleanup (CleanDatabase over thousands of rows) orders of magnitude
            // slower than necessary.
            using var transaction = connection.BeginTransaction();
            try {
                using var cmd = new SqliteCommand("DELETE FROM clippings WHERE key = @key", connection, transaction);
                foreach (Clipping clipping in listClippings) {
                    var key = clipping.Key;
                    if (string.IsNullOrWhiteSpace(key)) {
                        throw new InvalidOperationException();
                    }
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@key", key);
                    if (cmd.ExecuteNonQuery() > 0) {
                        count++;
                    }
                }
                transaction.Commit();
            } catch {
                try { transaction.Rollback(); } catch { /* 回滚失败也不能击穿兜底承诺 */ }
                throw;
            }
            return count;
        }

        public bool DeleteAll() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM clippings", connection);
            // 0 行受影响也是成功(空表):false 只应代表执行失败,而失败会抛异常由上层 catch。
            cmd.ExecuteNonQuery();
            return true;
        }
    }
}
