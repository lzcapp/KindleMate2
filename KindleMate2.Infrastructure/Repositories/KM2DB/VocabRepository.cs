using System.Globalization;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class VocabRepository(string connectionString) : IVocabRepository {
        /// <summary>全部业务列(顺序即 SELECT 序号;新增列时只改这一处)。</summary>
        private const string Columns =
            "id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB, deleted_at, origin_batch";

        /// <summary>旧格式源库(软删除改造前的 KM 库,只读)的列清单 —— 没有 deleted_at / origin_batch。</summary>
        private const string LegacyColumns =
            "id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB";

        /// <summary>软删除时间戳格式 —— 与其余时间列同款(InvariantCulture,防泰历/回历写出非公历年)。</summary>
        private static string NowTimestamp() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        public Vocab? GetById(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand($"SELECT {Columns} FROM vocab WHERE id = @id AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            return reader.Read() ? MapVocab(reader) : null;
        }

        public List<Vocab> GetAll() => Query(includeDeleted: false);

        public List<Vocab> GetAllIncludingDeleted() => Query(includeDeleted: true);

        /// <summary>
        /// 该表是否已带软删除两列。按连接探测一次后缓存到实例 ——
        /// 扁平 KM 导入会拿本仓储读**旧格式源库**(没有这两列),读取 SQL 必须随之分支。
        /// </summary>
        private bool? _hasSoftDeleteColumns;

        /// <summary>
        /// 共享查询:唯一区别是 WHERE 里有没有 <c>deleted_at IS NULL</c>(映射逻辑只写这一份)。
        /// 旧格式源库没有这两列:退化为全表读(旧库本就没有 tombstone,语义不变),行按缺列映射。
        /// </summary>
        private List<Vocab> Query(bool includeDeleted) {
            var results = new List<Vocab>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            _hasSoftDeleteColumns ??= DatabaseHelper.HasColumn(connection, "vocab", "deleted_at");
            var hasSoftDelete = _hasSoftDeleteColumns.Value;
            var columns = hasSoftDelete ? Columns : LegacyColumns;

            var sql = $"SELECT {columns} FROM vocab";
            if (hasSoftDelete && !includeDeleted) {
                sql += " WHERE deleted_at IS NULL";
            }
            var cmd = new SqliteCommand(sql, connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var vocab = MapVocab(reader);
                if (vocab == null) {
                    continue;
                }
                results.Add(vocab);
            }
            return results;
        }

        /// <summary>行 → 实体。id/word 任一为空的行视为脏数据跳过(返回 null,调用方过滤)。
        /// 尾部两列按 <c>FieldCount</c> 探测:旧格式源库只有前 10 列,缺则为 null。</summary>
        private static Vocab? MapVocab(SqliteDataReader reader) {
            var id = DatabaseHelper.GetSafeString(reader, 0);
            var word = DatabaseHelper.GetSafeString(reader, 2);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(word)) {
                return null;
            }
            return new Vocab {
                Id = id,
                WordKey = DatabaseHelper.GetSafeString(reader, 1),
                Word = word,
                Stem = DatabaseHelper.GetSafeString(reader, 3),
                Category = DatabaseHelper.GetSafeLong(reader, 4),
                Translation = DatabaseHelper.GetSafeString(reader, 5),
                Timestamp = DatabaseHelper.GetSafeString(reader, 6),
                Frequency = DatabaseHelper.GetSafeInt(reader, 7),
                Sync = DatabaseHelper.GetSafeInt(reader, 8),
                ColorRgb = DatabaseHelper.GetSafeLong(reader, 9),
                DeletedAt = reader.FieldCount > 10 ? DatabaseHelper.GetSafeString(reader, 10) : null,
                OriginBatch = reader.FieldCount > 11 ? DatabaseHelper.GetSafeLong(reader, 11) : null
            };
        }

        public List<Vocab> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Vocab>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 谓词不含 WHERE,最后统一拼装 —— 关键是给谓词**包一层括号**:
            // SearchType.All 是 "A OR B",若直接接 " AND deleted_at IS NULL",
            // AND 优先级高于 OR,拼出来是 "A OR (B AND deleted_at IS NULL)" ——
            // 命中 word 分支就绕过软删过滤,已删的词会被搜出来。
            // (与 LookupRepository.GetByFuzzySearch 的写法对齐。)
            var predicate = type switch {
                AppEntities.SearchType.Vocabulary => "word LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                AppEntities.SearchType.Stem => "stem LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                AppEntities.SearchType.All => "word LIKE '%' || @strSearch || '%' ESCAPE '\\' OR stem LIKE '%' || @strSearch || '%' ESCAPE '\\'",
                _ => string.Empty
            };
            var sql = string.IsNullOrEmpty(predicate)
                ? "WHERE deleted_at IS NULL"
                : "WHERE (" + predicate + ") AND deleted_at IS NULL";
            var query = $"SELECT {Columns} FROM vocab " + sql;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", DatabaseHelper.EscapeLikePattern(search));

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var vocab = MapVocab(reader);
                if (vocab == null) {
                    continue;
                }
                results.Add(vocab);
            }
            return results;
        }

        public List<string> GetWordsList() {
            var results = new List<string>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT DISTINCT word FROM vocab WHERE deleted_at IS NULL", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var word = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(word)) {
                    continue;
                }
                results.Add(word);
            }
            results = results.OrderBy(x => x).ToList();
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 只数活跃行:状态栏 / 导入前后差值都按"用户看得见的条目"算,tombstone 不该掺进来。
            var cmd = new SqliteCommand("SELECT COUNT(*) FROM vocab WHERE deleted_at IS NULL", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Vocab vocab) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(InsertSql(orIgnore: false), connection);
            AddParameters(cmd, vocab);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 批量插入。**先走整批单事务(最快);整批失败则降级为逐条插入** —— 与
        /// <c>ClippingRepository.Add(List&lt;Clipping&gt;)</c> 同一策略:逐条时跳过违反约束或数据非法的行,
        /// 使"判重漏网 / 脏数据"的后果从**整份文件全部导入失败**降为**只少几条**。
        /// 实际插入的条数由返回值给出。
        /// </summary>
        /// <remarks>
        /// 判重的最终裁判是数据库约束(vocab 主键 <c>id</c>):批量一律 <c>OR IGNORE</c>,
        /// 与 UNIQUE 冲突的行(含 tombstone)静默跳过 —— 不再靠"应用层 HashSet 现读活表",
        /// 那正是"删过再导被复活"的根源。快路径会因**数据非法**(<c>id</c>/<c>word</c> 为空,
        /// <c>AddParameters</c> 里 <c>?? throw</c>)整批回滚,故补上逐条降级兜底。
        /// <para>
        /// **代价**:逐条阶段每条各自提交,中途遇到非可跳过的系统性失败(磁盘满 / 只读 / IO 错)时,
        /// 已提交的行不会回滚 —— 调用方仍会收到异常,但库内可能留下"部分导入"的结果(与 clippings
        /// 的既有语义一致,重跑导入靠主键幂等)。
        /// </para>
        /// <para>
        /// 逐条循环里的 <c>when(IsSkippableInsertFailure)</c> 目前基本不可达(逐条 INSERT 带
        /// <c>OR IGNORE</c>,约束冲突不会抛;数据非法行已被显式跳过),保留作对称防御。
        /// </para>
        /// </remarks>
        public int Add(List<Vocab> vocabs) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // ① 快路径:整批单事务(OR IGNORE,唯一约束冲突静默跳过)
            using (var transaction = connection.BeginTransaction()) {
                try {
                    var count = 0;
                    using var cmd = new SqliteCommand(InsertSql(orIgnore: true), connection, transaction);
                    foreach (Vocab vocab in vocabs) {
                        AddParameters(cmd, vocab);
                        if (cmd.ExecuteNonQuery() > 0) {
                            count++;
                        }
                    }
                    transaction.Commit();
                    return count;
                } catch {
                    try { transaction.Rollback(); } catch { /* 回滚失败也不能击穿兜底承诺 */ }
                }
            }

            // ② 兜底:逐条插入,每条独立事务;显式跳过数据非法(无 id / word)的行,
            //    只吞可跳过项(约束冲突),系统性问题向上抛。
            var inserted = 0;
            foreach (Vocab vocab in vocabs) {
                if (string.IsNullOrWhiteSpace(vocab.Id) || string.IsNullOrWhiteSpace(vocab.Word)) {
                    continue;
                }
                using var rowTransaction = connection.BeginTransaction();
                try {
                    using var cmd = new SqliteCommand(InsertSql(orIgnore: true), connection, rowTransaction);
                    AddParameters(cmd, vocab);
                    if (cmd.ExecuteNonQuery() > 0) {
                        inserted++;
                    }
                    rowTransaction.Commit();
                } catch (Exception ex) when (DatabaseHelper.IsSkippableInsertFailure(ex)) {
                    // 防御性:OR IGNORE 下约束冲突本不会抛;保留以便将来去掉 OR IGNORE 时仍不吞坏数据。
                    try { rowTransaction.Rollback(); } catch { /* 回滚失败也不能击穿兜底承诺 */ }
                }
            }
            return inserted;
        }

        private static string InsertSql(bool orIgnore) =>
            $"INSERT {(orIgnore ? "OR IGNORE " : string.Empty)}INTO vocab ({Columns}) VALUES " +
            "(@id, @word_key, @word, @stem, @category, @translation, @timestamp, @frequency, @sync, @colorRGB, @deleted_at, @origin_batch)";

        private static void AddParameters(SqliteCommand cmd, Vocab vocab) {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", vocab.Id ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@word", vocab.Word ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@stem", vocab.Stem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@category", vocab.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@translation", vocab.Translation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", vocab.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@deleted_at", vocab.DeletedAt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@origin_batch", vocab.OriginBatch ?? (object)DBNull.Value);
        }

        public bool Update(Vocab vocab) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 刻意不更新 deleted_at / origin_batch:Update 是"改内容",软删除状态与来源
            // 是导入/删除流程写死的事实,不该被一次普通编辑顺手抹掉。
            var cmd = new SqliteCommand("UPDATE vocab SET word_key = @word_key, word = @word, stem = @stem, category = @category, translation = @translation, timestamp = @timestamp, frequency = @frequency, sync = @sync, colorRGB = @colorRGB WHERE id = @id AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@id", vocab.Id ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@word", vocab.Word ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@stem", vocab.Stem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@category", vocab.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@translation", vocab.Translation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", vocab.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// Bulk-updates frequency for many vocabs in a single transaction with one reused
        /// command. Replaces the previous per-vocab call pattern that opened a new
        /// connection (and auto-committed) once per row — the dominant cost when an
        /// import triggers a full frequency recompute over thousands of vocab rows.
        ///
        /// **按主键 <c>id</c> 定位**(而非 <c>word_key</c>):<c>word_key</c> 读取时经
        /// <see cref="DatabaseHelper.GetSafeString"/> 被 <c>Trim</c> 过,而库里存的是原始值 ——
        /// 词键带前后空白时,用读回来的(已 Trim 的)word_key 去 <c>WHERE word_key = ?</c> 会**匹配不到**
        /// 那一行,词频写不进去(UpdateFrequency 的统计因此对这类行恒为 0)。<c>id</c> 是
        /// <c>PRIMARY KEY</c>,由本程序生成、不含首尾空白(读取时虽然同样经
        /// <see cref="DatabaseHelper.GetSafeString"/> Trim,但 Trim 前后值一致),按它定位既精确又走主键索引。
        /// Rows with a null/blank id are skipped, which keeps the batch from aborting on junk rows.
        /// </summary>
        public int UpdateFrequencyByWordKey(List<Vocab> vocabs) {
            var count = 0;
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try {
                using var cmd = new SqliteCommand("UPDATE vocab SET frequency = @frequency WHERE id = @id AND deleted_at IS NULL", connection, transaction);
                foreach (Vocab vocab in vocabs) {
                    var id = vocab.Id;
                    if (string.IsNullOrWhiteSpace(id)) {
                        continue;
                    }
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@frequency", vocab.Frequency);
                    count += cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            } catch {
                transaction.Rollback();
                throw;
            }
            return count;
        }

        /// <summary>
        /// 软删除(2026-10-11):置 deleted_at 而非 DELETE。行留着占住主键 id ——
        /// 重新导入同一份库时 OR IGNORE 撞键跳过,被删的词不会"复活"。
        /// </summary>
        public bool Delete(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            var cmd = new SqliteCommand("UPDATE vocab SET deleted_at = @deleted_at WHERE id = @id AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@deleted_at", NowTimestamp());
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>按词软删除整词(词条 + 它的全部发生记录语义上一起消失,见 DeleteByWordKey 的调用方)。</summary>
        public bool DeleteByWordKey(string wordKey) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            var cmd = new SqliteCommand("UPDATE vocab SET deleted_at = @deleted_at WHERE word_key = @word_key AND deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@deleted_at", NowTimestamp());
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 批量软删除全部生词(左栏「全部生词」节点)—— 与单删同语义:只置 deleted_at,
        /// 行留着占键,重导入同一份库不会把整批词原样复活(那正是本改造要修的缺陷,
        /// 只是规模是全部)。想"整库重来"的出口是「清空全部数据」(硬删 + 先备份,
        /// 见 <see cref="DeleteAll"/>)—— 那条路会连 tombstone 一起清掉,重新导入才会放行。
        /// </summary>
        /// <returns>实际置为已删的行数(已是 tombstone 的行不重复计)。</returns>
        public int SoftDeleteAll() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                "UPDATE vocab SET deleted_at = @deleted_at WHERE deleted_at IS NULL", connection);
            cmd.Parameters.AddWithValue("@deleted_at", NowTimestamp());
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 真删(含 tombstone)—— 只用于「清空全部数据」:那条路径先整库备份再清空,
        /// 语义是"我要全新的库",留着 tombstone 反而会挡住随后的重新导入。
        /// (左栏「全部生词」的批量删除走 <see cref="SoftDeleteAll"/>,别接错。)
        /// </summary>
        public bool DeleteAll() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM vocab", connection);
            // 0 行受影响也是成功(空表):false 只应代表执行失败,而失败会抛异常由上层 catch。
            cmd.ExecuteNonQuery();
            return true;
        }
    }
}
