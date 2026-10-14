using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    /// <summary>
    /// 设备 <c>vocab.db</c>(只读数据源)的**实表/实列快照**。
    ///
    /// 为什么需要它:源库是设备自己写的另一套 schema,列名/列集合**不由本程序保证** ——
    /// 不同固件版本、越狱插件、或干脆是一份被截断的库,都可能少表少列。此前仓储拿固定列名直查,
    /// 一条 <c>no such column</c> 就会冒到导入编排层,**整份导入失败**(连本可以导入的表也一起赔进去)。
    ///
    /// 现在先探测实表/实列:
    /// <list type="bullet">
    /// <item>表存在且**必需列**齐全 —— 按实际存在的列构造 SELECT(缺失的期望列补 <c>NULL</c>),
    ///   仍然导入该表;</item>
    /// <item>表缺失或必需列不全 —— 该表**逐表跳过**,不影响其它表,并计入跳过明细向用户呈现。</item>
    /// </list>
    ///
    /// ⚠ 只服务于**源库**。目标库(KM2.dat)的 schema 由本程序自己的建库脚本保证,不在此放宽。
    /// </summary>
    public sealed class VocabSourceSchema {
        public const string WordsTable = "WORDS";
        public const string LookupsTable = "LOOKUPS";
        public const string BookInfoTable = "BOOK_INFO";

        /// <summary>
        /// WORDS 的必需列。缺任一列则整表跳过 —— 少了 <c>word</c> 无从建词条,少了 <c>timestamp</c>
        /// 无从判重与排序(id = lang + ':' + word,timestamp 是持久化键的一半)。
        /// </summary>
        public static readonly string[] WordsRequiredColumns = ["id", "word", "timestamp"];

        /// <summary>WORDS 期望映射的全部列;源库缺哪列就在 SELECT 里补 <c>NULL AS col</c>。</summary>
        public static readonly string[] WordsColumns = ["id", "word", "stem", "lang", "category", "timestamp", "profileid"];

        /// <summary>LOOKUPS 的必需列(id/word_key/timestamp);缺任一列则整表跳过。</summary>
        public static readonly string[] LookupsRequiredColumns = ["id", "word_key", "timestamp"];

        /// <summary>LOOKUPS 期望映射的全部列。</summary>
        public static readonly string[] LookupsColumns = ["id", "word_key", "book_key", "dict_key", "pos", "usage", "timestamp"];

        /// <summary>BOOK_INFO 的必需列(id/title/authors);缺任一列则整表跳过(查询记录的书名会留空)。</summary>
        public static readonly string[] BookInfoRequiredColumns = ["id", "title", "authors"];

        /// <summary>BOOK_INFO 期望映射的全部列。</summary>
        public static readonly string[] BookInfoColumns = ["id", "asin", "guid", "lang", "title", "authors"];

        private readonly Dictionary<string, HashSet<string>> _columnsByTable;

        private VocabSourceSchema(Dictionary<string, HashSet<string>> columnsByTable) {
            _columnsByTable = columnsByTable;
        }

        /// <summary>探测源库的实表与实列。表名/列名一律不区分大小写(源库表名是全大写)。</summary>
        public static VocabSourceSchema Probe(string connectionString) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            return Probe(connection);
        }

        /// <summary>在已打开的连接上探测(调用方负责连接生命周期)。</summary>
        public static VocabSourceSchema Probe(SqliteConnection connection) {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand()) {
                command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
                using var reader = command.ExecuteReader();
                while (reader.Read()) {
                    tables.Add(reader.GetString(0));
                }
            }

            var columnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in tables) {
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var command = connection.CreateCommand()) {
                    command.CommandText = $"PRAGMA table_info([{table}])";
                    using var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        // table_info 的列序:cid / name / type / notnull / dflt_value / pk
                        columns.Add(reader.GetString(1));
                    }
                }
                columnsByTable[table] = columns;
            }

            return new VocabSourceSchema(columnsByTable);
        }

        /// <summary>表存在且必需列齐全,才认为该表可导入。</summary>
        public bool IsUsable(string table, string[] requiredColumns) =>
            _columnsByTable.TryGetValue(table, out var columns) && requiredColumns.All(columns.Contains);

        /// <summary>
        /// 按源库**实际存在的列**构造 SELECT 列表;缺失的期望列补 <c>NULL AS col</c>,
        /// 于是上层仍可按固定序位读取。表缺失或必需列不全时返回 <c>null</c>(= 该表跳过)。
        /// </summary>
        public string? BuildSelectList(string table, string[] expectedColumns, string[] requiredColumns) {
            if (!IsUsable(table, requiredColumns)) {
                return null;
            }
            var columns = _columnsByTable[table];
            return string.Join(", ", expectedColumns.Select(column => columns.Contains(column) ? column : $"NULL AS {column}"));
        }

        /// <summary>本次导入里因缺表 / 缺必需列而被逐表跳过的表名(供向用户呈现跳过明细)。</summary>
        public IReadOnlyList<string> UnusableTables() {
            var skipped = new List<string>();
            if (!IsUsable(WordsTable, WordsRequiredColumns)) {
                skipped.Add(WordsTable);
            }
            if (!IsUsable(LookupsTable, LookupsRequiredColumns)) {
                skipped.Add(LookupsTable);
            }
            if (!IsUsable(BookInfoTable, BookInfoRequiredColumns)) {
                skipped.Add(BookInfoTable);
            }
            return skipped;
        }
    }
}
