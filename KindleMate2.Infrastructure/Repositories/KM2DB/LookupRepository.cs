using System.Globalization;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Diagnostics;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class LookupRepository(string connectionString) : ILookupRepository {
        /// <summary>全部业务列(顺序即 SELECT 序号;新增列时只改这一处)。</summary>
        private const string Columns =
            "word_key, usage, title, authors, timestamp, deleted_at, origin_batch";

        /// <summary>旧格式源库(软删除改造前的 KM 库,只读)的列清单 —— 没有 deleted_at / origin_batch。</summary>
        private const string LegacyColumns =
            "word_key, usage, title, authors, timestamp";

        /// <summary>软删除时间戳格式 —— 与其余时间列同款(InvariantCulture,防泰历/回历写出非公历年)。</summary>
        private static string NowTimestamp() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        public Lookup? GetByWordKey(string wordKey) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand($"SELECT {Columns} FROM lookups WHERE word_key = @word_key AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return MapLookup(reader);
            }
            return null;
        }

        public List<Lookup> GetAll() => Query(includeDeleted: false);

        public List<Lookup> GetAllIncludingDeleted() => Query(includeDeleted: true);

        /// <summary>
        /// 该表是否已带软删除两列。按连接探测一次后缓存到实例 ——
        /// 扁平 KM 导入会拿本仓储读**旧格式源库**(没有这两列),读取 SQL 必须随之分支。
        /// </summary>
        private bool? _hasSoftDeleteColumns;

        /// <summary>
        /// 共享查询:唯一区别是 WHERE 里有没有 <c>deleted_at IS NULL</c>(映射逻辑只写这一份)。
        /// 旧格式源库没有这两列:退化为全表读(旧库本就没有 tombstone,语义不变),行按缺列映射。
        /// </summary>
        private List<Lookup> Query(bool includeDeleted) {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            _hasSoftDeleteColumns ??= DatabaseHelper.HasColumn(connection, "lookups", "deleted_at");
            var hasSoftDelete = _hasSoftDeleteColumns.Value;
            var columns = hasSoftDelete ? Columns : LegacyColumns;

            var sql = $"SELECT {columns} FROM lookups";
            if (hasSoftDelete && !includeDeleted) {
                sql += " WHERE deleted_at IS NULL";
            }
            var cmd = new SqliteCommand(sql, connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var lookup = MapLookup(reader);
                if (lookup == null) {
                    continue;
                }
                results.Add(lookup);
            }
            return results;
        }

        /// <summary>行 → 实体。word_key 为空的行视为脏数据跳过(返回 null,调用方过滤)。
        /// 尾部两列按 <c>FieldCount</c> 探测:旧格式源库只有前 5 列,缺则为 null。</summary>
        private static Lookup? MapLookup(SqliteDataReader reader) {
            var wordKey = DatabaseHelper.GetSafeString(reader, 0);
            if (string.IsNullOrWhiteSpace(wordKey)) {
                return null;
            }
            return new Lookup {
                WordKey = wordKey,
                Usage = DatabaseHelper.GetSafeString(reader, 1),
                Title = DatabaseHelper.GetSafeString(reader, 2),
                Authors = DatabaseHelper.GetSafeString(reader, 3),
                Timestamp = DatabaseHelper.GetSafeString(reader, 4),
                DeletedAt = reader.FieldCount > 5 ? DatabaseHelper.GetSafeString(reader, 5) : null,
                OriginBatch = reader.FieldCount > 6 ? DatabaseHelper.GetSafeLong(reader, 6) : null
            };
        }

        public List<Lookup> GetByTimestamp(string timeStamp) {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand($"SELECT {Columns} FROM lookups WHERE timestamp = @timeStamp AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@timeStamp", timeStamp);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var lookup = MapLookup(reader);
                if (lookup == null) {
                    continue;
                }
                results.Add(lookup);
            }
            return results;
        }

        /// <summary>
        /// 判定 (word_key, timestamp) 是否已存在 —— **含 tombstone**:导入幂等性检查里
        /// "被用户删掉的那条"也算存在,否则删过的记录会被重新导回来。
        /// </summary>
        public bool ExistsByWordKeyAndTimestamp(string wordKey, string timestamp) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM lookups WHERE word_key = @word_key AND timestamp = @timestamp", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            cmd.Parameters.AddWithValue("@timestamp", timestamp);

            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public List<Lookup> GetByTitle(string title) {
            var results = new List<Lookup>();
            
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand($"SELECT {Columns} FROM lookups WHERE title = @title AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@title", title);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var lookup = MapLookup(reader);
                if (lookup == null) {
                    continue;
                }
                results.Add(lookup);
            }
            return results;
        }

        public List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var sql = string.Empty;
            switch (type) {
                case AppEntities.SearchType.BookTitle:
                    sql = "title LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.Author:
                    sql = "authors LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.Content:
                    sql = "usage LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.Vocabulary:
                case AppEntities.SearchType.Stem:
                    sql = "word_key LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.All:
                    sql = "word_key LIKE '%' || @strSearch || '%' ESCAPE '\\' OR usage LIKE '%' || @strSearch || '%' ESCAPE '\\' OR title LIKE '%' || @strSearch || '%' ESCAPE '\\' OR authors LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
            }
            var where = string.IsNullOrEmpty(sql)
                ? "WHERE deleted_at IS NULL"
                : "WHERE (" + sql + ") AND deleted_at IS NULL";
            var query = $"SELECT DISTINCT {Columns} FROM lookups " + where;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", DatabaseHelper.EscapeLikePattern(search));

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var lookup = MapLookup(reader);
                if (lookup == null) {
                    continue;
                }
                results.Add(lookup);
            }
            return results;
        }

        public List<string> GetWordKeysList() {
            var results = new List<string>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT DISTINCT word_key FROM lookups WHERE deleted_at IS NULL ORDER BY word_key", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(wordKey);
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 只数活跃行:状态栏 / 导入前后差值都按"用户看得见的条目"算。
            var cmd = new SqliteCommand("SELECT COUNT(*) FROM lookups WHERE deleted_at IS NULL", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Lookup lookup) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(InsertSql(orIgnore: false), connection);
            AddParameters(cmd, lookup);
            return cmd.ExecuteNonQuery() > 0;
        }

        public int Add(List<Lookup> lookups) {
            var count = 0;
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try {
                // 批量插入一律 OR IGNORE:判重的最终裁判是 UNIQUE(word_key, timestamp),
                // 不再依赖"应用层 HashSet 现读活表"—— 已删行不在活表里,那套判重会把
                // 删过的记录重新插回来。与唯一约束冲突的行(含 tombstone)静默跳过。
                using var cmd = new SqliteCommand(InsertSql(orIgnore: true), connection, transaction);
                foreach (Lookup lookup in lookups) {
                    AddParameters(cmd, lookup);
                    if (cmd.ExecuteNonQuery() > 0) {
                        count++;
                    }
                }
                transaction.Commit();
            } catch {
                transaction.Rollback();
                throw;
            }
            return count;
        }

        private static string InsertSql(bool orIgnore) =>
            $"INSERT {(orIgnore ? "OR IGNORE " : string.Empty)}INTO lookups ({Columns}) VALUES " +
            "(@word_key, @usage, @title, @authors, @timestamp, @deleted_at, @origin_batch)";

        private static void AddParameters(SqliteCommand cmd, Lookup lookup) {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@deleted_at", lookup.DeletedAt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@origin_batch", lookup.OriginBatch ?? (object)DBNull.Value);
        }

        public bool Update(Lookup lookup) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // lookups 没有主键,身份是 (word_key, timestamp)(同一词可被查多次)。
            // 之前只按 word_key 定位,会把同键**所有行**一并改写,并把它们的 timestamp
            // 压成同一个值 —— 直接撞 UNIQUE(word_key, timestamp) 且误改无关行。
            // 这里用 null 安全的 IS 对齐 Delete(wordKey, timestamp) 的定位口径。
            var cmd = new SqliteCommand("UPDATE lookups SET usage = @usage, title = @title, authors = @authors, timestamp = @timestamp WHERE word_key = @word_key AND timestamp IS @timestamp AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>软删除该词的全部查询行(2026-10-11)。行留着占住唯一键,重导入不会复活。</summary>
        public bool Delete(string wordKey) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            var cmd = new SqliteCommand("UPDATE lookups SET deleted_at = @deleted_at WHERE word_key = @word_key AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@deleted_at", NowTimestamp());
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>软删除单条 (word_key, timestamp) 查询行。</summary>
        public bool Delete(string wordKey, string timestamp) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            if (string.IsNullOrWhiteSpace(timestamp)) {
                throw new InvalidOperationException();
            }
            var cmd = new SqliteCommand("UPDATE lookups SET deleted_at = @deleted_at WHERE word_key = @word_key AND timestamp = @timestamp AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@deleted_at", NowTimestamp());
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            cmd.Parameters.AddWithValue("@timestamp", timestamp);
            return cmd.ExecuteNonQuery() > 0;
        }

        public int MergeWordKey(string oldWordKey, string newWordKey) {
            if (string.IsNullOrWhiteSpace(oldWordKey)) {
                throw new ArgumentException("旧 word_key 不能为空", nameof(oldWordKey));
            }
            if (string.IsNullOrWhiteSpace(newWordKey)) {
                throw new ArgumentException("新 word_key 不能为空", nameof(newWordKey));
            }
            if (string.Equals(oldWordKey, newWordKey, StringComparison.Ordinal)) {
                return 0;
            }

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // ① 删三类"搬过去就重复"的源行,必须在 UPDATE 之前 —— 否则 UPDATE 撞唯一约束
            //    整条回滚,一条都搬不过去(空句、元数据对不上的行一律保留,合并不是清理):
            //    a) 与目标行**同 timestamp**:撞唯一约束 (word_key, timestamp),搬必失败。
            //       timestamp NULL 不算冲突(与 SQLite UNIQUE 对 NULL 的语义一致),不会被删;
            //    b) 与目标行**同句 + 同书 + 同作者**(句子非空):两个同名词条把同一次阅读
            //       各记了一条,timestamp 差几秒,并入后会在中栏显示成两条肉眼完全相同的行
            //       (2026-09-25 用户报的"合并后有重复");
            //    c) **源键自己**就有同句 + 同书 + 同作者的重复(同一个词、同一句读过两次):
            //       只留 rowid 最小的一条,其余丢弃 —— 否则并入后中栏同样是两条相同的行。
            //       "谁算更早"要排除**会被 a) 丢掉**的那些行:否则最早那条若恰好与目标同
            //       timestamp(被 a 删)、次早的又会因"前面还有同句行"被 c 跟着删掉,整条阅读就没了。
            //       句子为空(NULL/空串)时无从判断是不是同一条 —— 不参与 b/c 两类,
            //       只可能被 a) 按 timestamp 删。
            //    注意:这里刻意**不筛 deleted_at** —— 唯一约束对 tombstone 同样生效,
            //    搬过去撞的就是它,所以判重必须把已删行也算进"目标已占用"。
            var dropCmd = new SqliteCommand(
                "DELETE FROM lookups WHERE word_key = @old_key AND (" +
                " EXISTS (SELECT 1 FROM lookups b WHERE b.word_key = @new_key AND b.timestamp = lookups.timestamp)" +
                " OR (COALESCE(lookups.usage, '') <> '' AND (" +
                "  EXISTS (SELECT 1 FROM lookups b WHERE b.word_key = @new_key" +
                "   AND COALESCE(b.usage, '') = COALESCE(lookups.usage, '')" +
                "   AND COALESCE(b.title, '') = COALESCE(lookups.title, '')" +
                "   AND COALESCE(b.authors, '') = COALESCE(lookups.authors, ''))" +
                "  OR EXISTS (SELECT 1 FROM lookups s WHERE s.word_key = @old_key AND s.rowid < lookups.rowid" +
                "   AND COALESCE(s.usage, '') = COALESCE(lookups.usage, '')" +
                "   AND COALESCE(s.title, '') = COALESCE(lookups.title, '')" +
                "   AND COALESCE(s.authors, '') = COALESCE(lookups.authors, '')" +
                "   AND NOT EXISTS (SELECT 1 FROM lookups c WHERE c.word_key = @new_key AND c.timestamp = s.timestamp)))))",
                connection, transaction);
            dropCmd.Parameters.AddWithValue("@old_key", oldWordKey);
            dropCmd.Parameters.AddWithValue("@new_key", newWordKey);
            var dropped = dropCmd.ExecuteNonQuery();
            if (dropped > 0) {
                // 静默删数据也留个痕:合并后行数对不上时,这是唯一能事后对账的线索。
                AppLog.Write($"[LookupRepository.MergeWordKey] {oldWordKey} → {newWordKey}:丢弃 {dropped} 条重复查询行");
            }

            // ② 剩下的整批搬过去(含 tombstone:软删除状态是行的事实,跟着词走)。
            var moveCmd = new SqliteCommand(
                "UPDATE lookups SET word_key = @new_key WHERE word_key = @old_key", connection, transaction);
            moveCmd.Parameters.AddWithValue("@old_key", oldWordKey);
            moveCmd.Parameters.AddWithValue("@new_key", newWordKey);
            var moved = moveCmd.ExecuteNonQuery();

            // 两步放在一个事务里:要么整件事都成,要么一行都不动。
            transaction.Commit();
            return moved;
        }

        /// <summary>
        /// 真删(含 tombstone)—— 只用于「清空全部数据」:那条路径先整库备份再清空,
        /// 语义是"我要全新的库",留着 tombstone 反而会挡住随后的重新导入。
        /// </summary>
        public bool DeleteAll() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM lookups", connection);
            // 0 行受影响也是成功(空表):false 只应代表执行失败,而失败会抛异常由上层 catch。
            cmd.ExecuteNonQuery();
            return true;
        }
    }
}
