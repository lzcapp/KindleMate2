using KindleMate2.Shared.Constants;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace KindleMate2.Infrastructure.Helpers {
    public static class DatabaseHelper {
        /// <summary>
        /// VACUUM INTO 等待写事务释放锁的默认毫秒数。连接串未设 busy_timeout
        /// (SQLite 默认 0,即立即失败),导入收尾这类短暂持锁场景给一个等待窗口即可;
        /// 长时间导入/清理仍应靠 UI 层互斥来避免并发,不能只指望这个等待。
        /// </summary>
        private const int DefaultSnapshotBusyTimeoutMs = 10_000;

        /// <summary>
        /// 快照落盘时的临时后缀。VACUUM INTO 只能写到不存在的路径,因此先写
        /// <c>目标路径 + 此后缀</c>,成功后再原子替换到目标 —— 中途失败不会毁掉上一份快照。
        /// </summary>
        private const string SnapshotTempSuffix = ".snapshot-tmp";

        /// <summary>
        /// <c>[clippings]</c> 的查询索引。建库脚本与老库迁移共用这一条,避免两处 SQL 漂移。
        /// 详见 <see cref="EnsureIndexesIfNeeded"/>。
        /// </summary>
        private const string ClippingsBookIndexScript =
            "CREATE INDEX IF NOT EXISTS [ix_clippings_book_page_date] ON [clippings]([bookname], [pagenumber], [clippingdate]);";

        /// <summary>
        /// Creates a new SQLite database with required tables.
        /// </summary>
        /// <param name="filePath">Path where the database file will be created</param>
        /// <param name="exception">Output parameter containing any exception that occurred</param>
        /// <returns>True if database creation was successful, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null</exception>
        /// <exception cref="ArgumentException">Thrown when filePath is empty or whitespace</exception>
        public static bool CreateDatabase(string filePath, out Exception exception) {
            ArgumentNullException.ThrowIfNull(filePath);

            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path cannot be empty or whitespace.", nameof(filePath));
            }

            exception = new Exception();
            
            try {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                using var connection = new SqliteConnection($"Data Source={filePath};Cache=Shared;Mode=ReadWriteCreate;");
                connection.Open();

                foreach (var script in GetTableCreationScripts()) {
                    using var command = new SqliteCommand(script, connection);
                    command.ExecuteNonQuery();
                }

                return true;
            } catch (Exception e) {
                exception = e;
                // Remove console logging - let the caller handle the exception
                return false;
            }
        }

        private static List<string> GetTableCreationScripts() {
            return [
                @"
                CREATE TABLE IF NOT EXISTS [clippings] (
                    [key] TEXT PRIMARY KEY NOT NULL UNIQUE, 
                    [content] TEXT DEFAULT(''), 
                    [bookname] TEXT DEFAULT(''), 
                    [authorname] TEXT, 
                    [brieftype] INTEGER, 
                    [clippingtypelocation] TEXT, 
                    [clippingdate] TEXT, 
                    [read] INT DEFAULT(0), 
                    [clipping_importdate] TEXT, 
                    [tag] TEXT, 
                    [sync] INT DEFAULT(0), 
                    [newbookname] TEXT, 
                    [colorRGB] INTEGER DEFAULT(-1), 
                    pagenumber INT DEFAULT(0)
                );",

                @"
                CREATE TABLE IF NOT EXISTS [lookups] (
                    [word_key] TEXT, 
                    [usage] TEXT, 
                    [title] TEXT, 
                    [authors] TEXT, 
                    [timestamp] TEXT,
                    CONSTRAINT [uq_lookups_word_key_timestamp] UNIQUE ([word_key], [timestamp])
                );",

                @"
                CREATE TABLE IF NOT EXISTS [original_clipping_lines] (
                    [key] TEXT PRIMARY KEY NOT NULL UNIQUE, 
                    [line1] TEXT DEFAULT(''), 
                    [line2] TEXT DEFAULT(''), 
                    [line3] TEXT DEFAULT(''), 
                    [line4] TEXT DEFAULT(''), 
                    [line5] TEXT DEFAULT('')
                );",

                @"
                CREATE TABLE IF NOT EXISTS [settings] (
                    [name] TEXT PRIMARY KEY UNIQUE, 
                    [value] TEXT
                );",

                @"
                CREATE TABLE IF NOT EXISTS [vocab] (
                    [id] TEXT PRIMARY KEY NOT NULL UNIQUE, 
                    [word_key] TEXT, 
                    [word] TEXT NOT NULL, 
                    [stem] TEXT, 
                    [category] INTEGER DEFAULT '0', 
                    [translation] TEXT, 
                    [timestamp] TEXT, 
                    [frequency] INT DEFAULT(0), 
                    [sync] INT DEFAULT(0), 
                    [colorRGB] INTEGER DEFAULT(-1)
                );",

                // [clippings] 的查询索引:新建的库在这里直接带上,老库由 EnsureIndexesIfNeeded 补。
                ClippingsBookIndexScript
            ];
        }

        /// <summary>
        /// Backs up a database file to a specified backup location.
        /// </summary>
        /// <param name="databasePath">Path to the database directory</param>
        /// <param name="backupPath">Path to the backup directory</param>
        /// <param name="databaseFileName">Name of the database file</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
        /// <exception cref="ArgumentException">Thrown when any parameter is empty or whitespace</exception>
        public static void BackupDatabase(string databasePath, string backupPath, string databaseFileName) {
            ArgumentNullException.ThrowIfNull(databasePath);
            ArgumentNullException.ThrowIfNull(backupPath);
            ArgumentNullException.ThrowIfNull(databaseFileName);

            if (string.IsNullOrWhiteSpace(databasePath)) {
                throw new ArgumentException("Database path cannot be empty or whitespace.", nameof(databasePath));
            }
            if (string.IsNullOrWhiteSpace(backupPath)) {
                throw new ArgumentException("Backup path cannot be empty or whitespace.", nameof(backupPath));
            }
            if (string.IsNullOrWhiteSpace(databaseFileName)) {
                throw new ArgumentException("Database file name cannot be empty or whitespace.", nameof(databaseFileName));
            }

            var databaseFilePath = Path.Combine(databasePath, databaseFileName);
            
            if (!File.Exists(databaseFilePath)) {
                throw new FileNotFoundException($"Database file not found: {databaseFilePath}");
            }

            if (!Directory.Exists(backupPath)) {
                Directory.CreateDirectory(backupPath);
            }

            // Create timestamped backup filename to avoid overwrites
            // 必须显式传 InvariantCulture:不带 culture 的 ToString 走 CurrentCulture,而
            // 非公历日历会把这个"时间戳"变成别的年份(th-TH → 2569、fa-IR → 1405、ar-SA → 1448),
            // 备份文件名就再也读不出真实日期、也无法按名称排序。
            var timestamp = DateTime.Now.ToString(AppConstants.BackupTimestampFormat, CultureInfo.InvariantCulture);
            var backupFileName = Path.GetFileNameWithoutExtension(databaseFileName) + 
                                $"_backup_{timestamp}" + 
                                Path.GetExtension(databaseFileName);
            var backupFilePath = Path.Combine(backupPath, backupFileName);

            // 走 VACUUM INTO 而不是 File.Copy。文件名带时间戳,天然满足"目标必须不存在";
            // 不传 overwrite,语义与原先的 File.Copy(overwrite: false) 一致 —— 同一时间戳
            // 撞车时同样抛错,而不是静默覆盖掉上一份备份。
            CreateConsistentSnapshot(databaseFilePath, backupFilePath);
        }

        /// <summary>
        /// 用 SQLite 的 <c>VACUUM INTO</c> 抽出一份**事务一致**且紧凑的库快照。备份与
        /// (未来的)多机同步快照共用这一个出口。
        /// </summary>
        /// <remarks>
        /// 为什么不用 <c>File.Copy</c>:直接复制数据库文件会**静默**产出不可用的结果。
        /// 已在本项目实际依赖的引擎(e_sqlite3 3.53.3)上实测:
        /// <list type="number">
        /// <item>rollback journal 模式下,别处事务进行中主文件**已被部分改写**,此时复制出来的库
        /// <c>integrity_check</c> 报 <c>wrong # of entries in index ...</c>,行数也与源库不符
        /// (实测 3272 / 应 11001)。不主动跑 integrity_check 的话,SQLite 打开它不会报错,
        /// 只表现为查询漏行 —— 也就是说,备份"看起来成功了"。</item>
        /// <item>若将来改用 WAL,最近的提交只存在于 <c>-wal</c> 里,只复制主文件会丢掉这些提交
        /// (实测 2001 行只剩 1 行);而主文件自身是自洽的,<c>integrity_check</c> 仍返回 ok,
        /// 连自检都发现不了。</item>
        /// </list>
        /// <c>VACUUM INTO</c> 只有两种结局:给出一致快照,或以 SQLITE_BUSY 明确失败 ——
        /// 不会产出损坏文件。
        ///
        /// 三条限制(同样经过实测),调用方需知:
        /// <list type="number">
        /// <item><c>VACUUM INTO</c> 只能写到**不存在**的路径,否则报 <c>output file already exists</c>。
        /// 本方法改为先写"目标路径 + <see cref="SnapshotTempSuffix"/>"再原子替换,既可覆盖已有目标,
        /// 中途失败也不会毁掉上一份快照;<paramref name="overwrite"/> 控制的是能否替换已存在的目标。</item>
        /// <item>不能在持有活动事务的连接上执行(报 <c>cannot VACUUM from within a transaction</c>),
        /// 因此这里像 <see cref="VacuumDatabase"/> 一样**自开独立连接**,不复用调用方的连接。</item>
        /// <item>目标目录必须已存在,否则报 <c>unable to open database</c>。</item>
        /// </list>
        ///
        /// 并发:写入方持有独占锁时 VACUUM 会 SQLITE_BUSY。连接串没有 busy_timeout,
        /// 默认值为 0(立即失败),所以这里显式设一个等待窗口。但**长时间导入/清理过程中仍可能
        /// 超时失败** —— 调用方应在 UI 层避免与这些操作并发触发,不要只依赖这里的等待。
        /// </remarks>
        /// <param name="sourcePath">源数据库文件路径</param>
        /// <param name="destPath">快照输出路径;父目录必须已存在</param>
        /// <param name="overwrite">目标已存在时是否先删除。默认 false(抛错),避免静默覆盖</param>
        /// <param name="busyTimeoutMs">等待写事务释放锁的毫秒数;0 表示立即失败</param>
        /// <exception cref="ArgumentNullException">参数为 null</exception>
        /// <exception cref="ArgumentException">参数为空或空白</exception>
        /// <exception cref="FileNotFoundException">源数据库文件不存在</exception>
        /// <exception cref="DirectoryNotFoundException">目标目录不存在</exception>
        /// <exception cref="IOException">目标文件已存在且 <paramref name="overwrite"/> 为 false</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="busyTimeoutMs"/> 为负数</exception>
        /// <exception cref="InvalidOperationException">SQLite 执行失败(锁超时、磁盘空间不足等)</exception>
        public static void CreateConsistentSnapshot(string sourcePath, string destPath, bool overwrite = false, int busyTimeoutMs = DefaultSnapshotBusyTimeoutMs) {
            ArgumentNullException.ThrowIfNull(sourcePath);
            ArgumentNullException.ThrowIfNull(destPath);

            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path cannot be empty or whitespace.", nameof(sourcePath));
            }
            if (string.IsNullOrWhiteSpace(destPath)) {
                throw new ArgumentException("Destination path cannot be empty or whitespace.", nameof(destPath));
            }
            // PRAGMA 不支持参数绑定,只能插值,所以负值必须在这里挡住 —— 否则 SQLite 的行为
            // 依版本而异(有的当 0 处理,有的直接报语法错)。
            if (busyTimeoutMs < 0) {
                throw new ArgumentOutOfRangeException(nameof(busyTimeoutMs), busyTimeoutMs, "Busy timeout cannot be negative.");
            }
            // 源路径也取全路径:连接串里的相对路径按进程工作目录解析,工作目录一变,
            // 同一个调用就会静默指向另一个库 —— 此前只规范化了目标,源漏了这一道。
            var fullSourcePath = Path.GetFullPath(sourcePath);
            if (!File.Exists(fullSourcePath)) {
                throw new FileNotFoundException($"Database file not found: {fullSourcePath}");
            }

            // 先把目标路径规范化:VACUUM INTO 拿到什么就写什么,相对路径会随工作目录漂移。
            var fullDestPath = Path.GetFullPath(destPath);
            var destDirectory = Path.GetDirectoryName(fullDestPath);
            if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory)) {
                throw new DirectoryNotFoundException($"Snapshot directory not found: {destDirectory}");
            }

            if (File.Exists(fullDestPath) && !overwrite) {
                throw new IOException($"Snapshot target already exists: {fullDestPath}. Pass overwrite: true to replace it.");
            }

            // 先写到同目录的临时文件,成功后再替换目标。两个理由:
            // ① VACUUM INTO 只能写到不存在的路径,写临时名天然满足;
            // ② 「最后才替换」意味着中途失败(锁超时/磁盘满)不会毁掉上一份成功的快照。
            // 同目录是刻意的 —— 跨卷改名不是原子操作。
            var tempPath = fullDestPath + SnapshotTempSuffix;
            TryDeleteFile(tempPath);

            try {
                // 独立连接:VACUUM 不能在调用方的事务里执行。这里也不带 Cache=Shared —— 
                // 快照连接没有理由加入调用方的共享缓存。
                using var connection = new SqliteConnection($"Data Source={fullSourcePath};Mode=ReadOnly;");
                connection.Open();

                using (var busyCommand = connection.CreateCommand()) {
                    busyCommand.CommandText = $"PRAGMA busy_timeout = {busyTimeoutMs};";
                    busyCommand.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand()) {
                    // 用参数绑定而不是字符串拼接:目标路径可能含单引号(实测未转义拼接会直接 syntax error)。
                    command.CommandText = "VACUUM INTO @destination;";
                    command.Parameters.AddWithValue("@destination", tempPath);
                    command.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                TryDeleteFile(tempPath);
                // 刻意**不**退回 File.Copy:宁可明确报告失败,也不交付一份可能不一致的备份。
                throw new InvalidOperationException(
                    $"Failed to create a consistent snapshot of '{fullSourcePath}' at '{fullDestPath}': {ex.Message}", ex);
            }

            ReplaceFile(tempPath, fullDestPath);
        }

        /// <summary>
        /// 把临时快照替换到目标位置。
        /// </summary>
        /// <remarks>
        /// 之所以要专门处理:Windows 上被占用的文件无法替换,而 SQLite 的连接池会让**已归还**连接的
        /// 文件句柄继续存活(<c>Dispose</c> 只是把连接还给池,并不关文件)。于是只要本进程曾经打开过
        /// 这个目标(例如上一轮同步读过这份快照),这一轮的覆盖就会失败。故遇 IO 失败时先清一次连接池
        /// 再重试 —— <c>ClearAllPools</c> 只关闭空闲连接,不影响正在使用的连接。
        ///
        /// 注意异常类型:目标被占用时 <c>File.Move</c> 在 Windows 上抛的是
        /// <see cref="UnauthorizedAccessException"/>("Access to the path is denied"),它**不继承**
        /// <see cref="IOException"/> —— 两者都要接住,否则这个重试逻辑形同虚设(实测踩过)。
        /// 两次尝试都失败时,先删掉临时快照再抛出 —— 否则它会在用户的 Backups 目录里永久残留
        /// (名字带时间戳,下一次备份不会复用同一路径,也就永远轮不到清理)。
        /// </remarks>
        private static void ReplaceFile(string tempPath, string destPath) {
            try {
                File.Move(tempPath, destPath, overwrite: true);
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                SqliteConnection.ClearAllPools();
                try {
                    File.Move(tempPath, destPath, overwrite: true);
                } catch {
                    // 两次都失败:临时快照不能留在用户的 Backups 目录里 —— 它的名字带时间戳,
                    // 下一次备份不会复用同一路径,也就永远不会替用户清掉它。
                    TryDeleteFile(tempPath);
                    throw;
                }
            }
        }

        /// <summary>尽最大努力删除临时文件;失败不抛 —— 残留物带固定后缀,下次调用会先清理它。</summary>
        private static void TryDeleteFile(string path) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            } catch { /* 清理是尽最大努力,不该影响主流程 */ }
        }

        /// <summary>
        /// Vacuums (optimizes) a SQLite database to reclaim space and defragment.
        /// </summary>
        /// <param name="filePath">Path to the SQLite database file</param>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null</exception>
        /// <exception cref="ArgumentException">Thrown when filePath is empty or whitespace</exception>
        /// <exception cref="FileNotFoundException">Thrown when database file doesn't exist</exception>
        public static void VacuumDatabase(string filePath) {
            ArgumentNullException.ThrowIfNull(filePath);

            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path cannot be empty or whitespace.", nameof(filePath));
            }
            
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException($"Database file not found: {filePath}");
            }

            try {
                using var connection = new SqliteConnection($"Data Source={filePath};Cache=Shared;Mode=ReadWrite;");
                connection.Open();
                using var command = new SqliteCommand("VACUUM;", connection);
                command.ExecuteNonQuery();
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to vacuum database '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 确保 <c>[clippings]</c> 的查询索引存在。幂等(<c>CREATE INDEX IF NOT EXISTS</c>),
        /// 每次启动调用都无副作用。
        /// </summary>
        /// <remarks>
        /// <c>[clippings]</c> 原本只有 <c>[key]</c> 主键,而按书名取书摘(<c>WHERE bookname = @bookname</c>,
        /// 仓储里有 6 处)与主列表排序(<c>ORDER BY bookname, pagenumber, clippingdate</c>)都命中不了索引 ——
        /// 几万条的库上就是全表扫描加临时排序。这里建一个覆盖这三列的复合索引:
        /// 前缀等值查询与按序扫描都能用上它。
        /// 老库靠本方法补齐;新建的库由 <see cref="GetTableCreationScripts"/> 直接带上。
        /// </remarks>
        /// <param name="filePath">Path to the SQLite database file</param>
        /// <exception cref="InvalidOperationException">建索引失败</exception>
        public static void EnsureIndexesIfNeeded(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return;
            }

            try {
                using var connection = new SqliteConnection(GetConnectionString(filePath));
                connection.Open();
                using var command = new SqliteCommand(ClippingsBookIndexScript, connection);
                command.ExecuteNonQuery();
            } catch (Exception e) {
                throw new InvalidOperationException($"Failed to ensure indexes in '{filePath}': {e.Message}", e);
            }
        }
        
        /// <summary>
        /// Idempotent one-time schema migration for the [lookups] table.
        /// Legacy databases were created with a single-column UNIQUE on [timestamp],
        /// which rejects legitimate duplicates (e.g. several words looked up within
        /// the same second). SQLite cannot drop a column constraint via ALTER TABLE,
        /// so the table is rebuilt with uniqueness moved to (word_key, timestamp).
        /// Safe to call on every startup: it no-ops once the new schema is in place.
        /// </summary>
        /// <param name="filePath">Path to the SQLite database file</param>
        public static void MigrateLookupsSchemaIfNeeded(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return;
            }

            try {
                using var connection = new SqliteConnection(GetConnectionString(filePath));
                connection.Open();

                if (!NeedsLookupsSchemaMigration(connection)) {
                    return;
                }

                using var transaction = connection.BeginTransaction();
                try {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    command.CommandText = @"
                        CREATE TABLE [lookups_migrated] (
                            [word_key] TEXT, 
                            [usage] TEXT, 
                            [title] TEXT, 
                            [authors] TEXT, 
                            [timestamp] TEXT,
                            CONSTRAINT [uq_lookups_word_key_timestamp] UNIQUE ([word_key], [timestamp])
                        );";
                    command.ExecuteNonQuery();

                    command.CommandText = @"INSERT INTO [lookups_migrated] ([word_key], [usage], [title], [authors], [timestamp])
                                            SELECT [word_key], [usage], [title], [authors], [timestamp] FROM [lookups];";
                    command.ExecuteNonQuery();

                    command.CommandText = "DROP TABLE [lookups];";
                    command.ExecuteNonQuery();

                    command.CommandText = "ALTER TABLE [lookups_migrated] RENAME TO [lookups];";
                    command.ExecuteNonQuery();

                    transaction.Commit();
                } catch {
                    transaction.Rollback();
                    throw;
                }
            } catch (Exception e) {
                throw new InvalidOperationException($"Failed to migrate lookups schema in '{filePath}': {e.Message}", e);
            }
        }

        /// <summary>
        /// Detects the legacy lookups schema: a UNIQUE index on the single [timestamp] column.
        /// </summary>
        private static bool NeedsLookupsSchemaMigration(SqliteConnection connection) {
            try {
                // Collect unique index names first so index_info can be queried after the reader is disposed.
                var uniqueIndexNames = new List<string>();
                using (var listCommand = connection.CreateCommand()) {
                    listCommand.CommandText = "PRAGMA index_list('lookups');";
                    using var reader = listCommand.ExecuteReader();
                    while (reader.Read()) {
                        var isUnique = !reader.IsDBNull(2) && reader.GetInt64(2) == 1;
                        if (isUnique) {
                            uniqueIndexNames.Add(reader.GetString(1));
                        }
                    }
                }

                foreach (var indexName in uniqueIndexNames) {
                    using var infoCommand = connection.CreateCommand();
                    infoCommand.CommandText = $"PRAGMA index_info('{indexName}');";
                    using var reader = infoCommand.ExecuteReader();

                    var columns = new List<string>();
                    while (reader.Read()) {
                        if (!reader.IsDBNull(2)) {
                            columns.Add(reader.GetString(2));
                        }
                    }

                    // Legacy: exactly one unique column "timestamp" → must rebuild.
                    if (columns.Count == 1 && columns[0].Equals("timestamp", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }

                return false;
            } catch (Exception) {
                // If the schema cannot be inspected, leave the table untouched.
                return false;
            }
        }

        public static string? GetSafeString(SqliteDataReader reader, int ordinal) {
            if (reader.IsDBNull(ordinal)) {
                return null;
            }
            var s = reader.GetString(ordinal);
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        public static int? GetSafeInt(SqliteDataReader reader, int ordinal) {
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }

        public static long? GetSafeLong(SqliteDataReader reader, int ordinal) {
            return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
        }

        public static int GetSafeInt(SqliteDataReader reader, int ordinal, int defaultValue) {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
        }
        
        public static string GetConnectionString(string dbFile) {
            return $"Data Source={dbFile};Cache=Shared;Mode=ReadWrite;";
        }
    }
}
