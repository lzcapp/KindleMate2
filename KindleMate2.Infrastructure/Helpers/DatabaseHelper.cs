using KindleMate2.Shared.Constants;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace KindleMate2.Infrastructure.Helpers {
    public static class DatabaseHelper {
        /// <summary>
        /// 本程序认识的最高 schema 版本。老库低于它则迁移;高于它则拒绝打开
        /// (拒绝的判定在 <c>MainWindowViewModel.PrepareDatabaseAsync</c> —— DB 层只负责迁移,不负责拒绝)。
        /// </summary>
        /// <remarks>
        /// 版本号存在 <c>PRAGMA user_version</c> 里(权威依据),<c>schema_migration</c> 表作为审计日志。
        /// <para>
        /// ⚠️ **<c>PRAGMA user_version</c> 会随事务回滚。** 本项目实际依赖的引擎
        /// (e_sqlite3 3.53.3)上实测:<c>BEGIN; CREATE TABLE s(v); PRAGMA user_version=1; ROLLBACK;</c>
        /// 之后读回 <c>user_version</c> 仍是 0、表 <c>s</c> 也没了;<c>BEGIN; PRAGMA user_version=1; COMMIT;</c>
        /// 才留住 1。因此设置版本号必须作为迁移事务的**最后一步**,与建表、写审计行**一起原子提交** ——
        /// **不要**写成"先提交事务、再单独设 PRAGMA",那会留下"表建好了但版本没设"的中间态,下次启动会重跑。
        /// </para>
        /// </remarks>
        public const int CurrentSchemaVersion = 1;

        /// <summary>
        /// <c>[schema_migration]</c> 审计表的建表脚本 —— 建库脚本与老库迁移**共用这一份**
        /// (与 <see cref="GetIndexScripts"/> 同样的理由:两处写死就会漂移)。
        /// </summary>
        private const string SchemaMigrationTableScript =
            @"CREATE TABLE IF NOT EXISTS [schema_migration] (
                [version]     INTEGER PRIMARY KEY,
                [applied_at]  TEXT NOT NULL,
                [description] TEXT NOT NULL
            );";

        /// <summary>
        /// VACUUM INTO 等待写事务释放锁的默认毫秒数。连接串未设 busy_timeout
        /// (SQLite 默认 0,即立即失败),导入收尾这类短暂持锁场景给一个等待窗口即可;
        /// 长时间导入/清理仍应靠 UI 层互斥来避免并发,不能只指望这个等待。
        /// </summary>
        private const int DefaultSnapshotBusyTimeoutMs = 10_000;

        /// <summary>
        /// 连接串上的统一命令超时(秒),经 <c>SqliteConnectionStringBuilder.DefaultTimeout</c> 落到
        /// <see cref="SqliteCommand.CommandTimeout"/>:锁竞争时命令会在该窗口内**重试等待**而不是立即失败。
        /// 集中在 <see cref="BuildConnectionString"/> 一处设置,避免各调用点各写一份而漂移。
        /// </summary>
        /// <remarks>
        /// ⚠️ 实测(Microsoft.Data.Sqlite 10.0.12,本项目实际依赖的引擎):
        /// <list type="bullet">
        /// <item>该库**默认**<c>CommandTimeout</c> 已是 30 秒 —— 不设 <c>Default Timeout</c> 时命令同样会
        /// 在锁上等待约 30 秒才报 <c>SQLITE_BUSY</c>,并非"立即失败"(旧注释/审计里"连接串没设
        /// busy_timeout、默认 0"的说法与本库行为不符)。</item>
        /// <item>真正生效的是 <c>CommandTimeout</c>;SQLite 层的 <c>PRAGMA busy_timeout</c>
        /// **管不住**它(实测:即便把 PRAGMA 设成 500ms,未设 <c>Default Timeout</c> 时仍等满 30 秒)。
        /// 因此这里显式取 30 秒 = 与既有隐式默认一致,只把"依赖库默认值"变成"本程序显式声明",
        /// **不改变并发行为**,也便于日后统一调整。</item>
        /// </list>
        /// 若日后要容忍更久持锁的导入/清理,只需调大此值;要更早失败则调小 —— 改一处即可。
        /// </remarks>
        private const int ConnectionDefaultTimeoutSeconds = 30;

        /// <summary>
        /// 快照落盘时的临时后缀。VACUUM INTO 只能写到不存在的路径,因此先写
        /// <c>目标路径 + 此后缀</c>,成功后再原子替换到目标 —— 中途失败不会毁掉上一份快照。
        /// </summary>
        private const string SnapshotTempSuffix = ".snapshot-tmp";

        /// <summary>
        /// 备份文件名里的固定中缀,成品形如 <c>KM2_backup_20260921_221834.dat</c>。
        /// 提成常量是因为它有两个消费者:<see cref="BackupDatabase"/> 负责**生成**,<see cref="PruneBackups"/>
        /// 负责按同一形状**找回**。写死两处的话,改一处就会让清理逻辑静默地匹配不到任何文件
        /// (表现是"保留 3 份"悄无声息地失效,而不是报错)。
        /// </summary>
        private const string BackupFileNameInfix = "_backup_";

        /// <summary>
        /// <c>[clippings]</c> 的查询索引。建库脚本与老库迁移共用 <see cref="GetIndexScripts"/>,
        /// 避免两处 SQL 漂移。详见 <see cref="EnsureIndexesIfNeeded"/>。
        /// </summary>
        private const string ClippingsBookIndexScript =
            "CREATE INDEX IF NOT EXISTS [ix_clippings_book_page_date] ON [clippings]([bookname], [pagenumber], [clippingdate]);";

        /// <summary>
        /// <c>[vocab]</c> 的 <c>word_key</c> 索引。
        /// </summary>
        /// <remarks>
        /// 收益是**数量级**的,不是锦上添花:词频重算会按 <c>word_key</c> 逐行回写 <c>vocab</c>,
        /// 没有索引时每条 <c>UPDATE … WHERE word_key = ?</c> 都要全表扫一遍 vocab,整体退化成
        /// O(vocab²)。实测(20000 lookups / 5000 vocab,同一事务内):
        /// 无索引 576ms → 有索引 5ms。
        /// 它同时服务 <c>GetByWordKey</c> / <c>DeleteByWordKey</c> / <c>UpdateFrequencyByWordKey</c>。
        ///
        /// **不要再给 <c>[lookups].[word_key]</c> 加索引。** 现有
        /// <c>UNIQUE([word_key], [timestamp])</c> 会自动建出 <c>sqlite_autoindex_lookups_1</c>,
        /// 其最左前缀已经覆盖 <c>WHERE word_key = ?</c> —— 已用 <c>EXPLAIN QUERY PLAN</c> 实测:
        /// 等值查询、<c>DELETE … WHERE word_key = ?</c>、<c>DISTINCT word_key ORDER BY word_key</c>
        /// 全部命中它。再加一条只会白付写入与磁盘开销。
        /// </remarks>
        private const string VocabWordKeyIndexScript =
            "CREATE INDEX IF NOT EXISTS [ix_vocab_word_key] ON [vocab]([word_key]);";

        /// <summary>
        /// <c>[lookups].[timestamp]</c> 的索引。服务 <c>LookupRepository.GetByTimestamp</c>
        /// (<c>WHERE timestamp = @timeStamp AND deleted_at IS NULL</c>)。
        /// </summary>
        /// <remarks>
        /// 现有 <c>UNIQUE([word_key], [timestamp])</c> 的最左前缀是 <c>word_key</c>,
        /// 单按 <c>timestamp</c> 过滤命中不了它 —— 没有本索引时该查询是整表扫。<c>timestamp</c>
        /// 是短文本列,索引体积远小于数据本身,写入开销可控,故按需补上。
        /// </remarks>
        private const string LookupsTimestampIndexScript =
            "CREATE INDEX IF NOT EXISTS [ix_lookups_timestamp] ON [lookups]([timestamp]);";

        /// <summary>
        /// <c>[import_batches]</c> 的建表脚本。建库(<see cref="GetTableCreationScripts"/>)与
        /// 老库迁移(<see cref="EnsureSoftDeleteSchemaIfNeeded"/>)共用,避免两处 SQL 漂移。
        /// </summary>
        internal const string ImportBatchTableScript =
            @"
            CREATE TABLE IF NOT EXISTS [import_batches] (
                [id] INTEGER PRIMARY KEY AUTOINCREMENT, 
                [source_type] TEXT NOT NULL, 
                [source_path] TEXT, 
                [imported_at] TEXT NOT NULL, 
                [parsed_count] INTEGER DEFAULT(0), 
                [inserted_count] INTEGER DEFAULT(0)
            );";

        /// <summary>
        /// <c>[vocab]</c> / <c>[lookups]</c> 两表共有的软删除与溯源列 —— **唯一来源**:
        /// 建库 DDL(<see cref="GetTableCreationScripts"/>)与老库补列
        /// (<see cref="EnsureSoftDeleteSchemaIfNeeded"/>)都从这里取,改列名或类型只改这一处。
        /// 两条路径产出的列集是否真的一致,由 SchemaIntegrityTests 的列集断言兜底。
        /// </summary>
        internal static readonly (string Column, string Type)[] SoftDeleteColumns = [
            ("deleted_at", "TEXT"),
            ("origin_batch", "INTEGER"),
        ];

        /// <summary>
        /// <see cref="SoftDeleteColumns"/> 的建表 DDL 片段(<c>"a, b"</c>,不含前导逗号)——
        /// 拼进两段 <c>CREATE TABLE</c> 用,避免列定义在 DDL 与 EnsureColumn 各写一遍。
        /// </summary>
        internal static string SoftDeleteColumnsDdl =>
            string.Join(", ", SoftDeleteColumns.Select(c => $"[{c.Column}] {c.Type}"));

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

                using var connection = new SqliteConnection(BuildConnectionString(filePath, SqliteOpenMode.ReadWriteCreate, sharedCache: true));
                connection.Open();

                foreach (var script in GetTableCreationScripts()) {
                    using var command = new SqliteCommand(script, connection);
                    command.ExecuteNonQuery();
                }

                // 新库建成后直接标记为当前版本,于是它一出生就是 v1。
                //
                // 刻意**不**给这里包显式事务:CreateDatabase 现有 20+ 处测试直接调用,包事务是额外
                // 风险面,而收益有限 —— 建表脚本全是 CREATE TABLE IF NOT EXISTS,即便"表建好了但版本
                // 没设"这一瞬间被打断,下次启动的 MigrateSchemaIfNeeded 也会把版本补齐(它同样幂等)。
                // 因此"最小改动"在此是更稳妥的选择,见 MigrateSchemaIfNeeded。
                MarkSchemaVersion(connection, null, CurrentSchemaVersion, "initial schema");

                return true;
            } catch (Exception e) {
                exception = e;
                // Remove console logging - let the caller handle the exception
                return false;
            }
        }

        /// <summary>
        /// 新库的建表脚本集合。新库由这里一次建全(含 <c>[schema_migration]</c> 审计表)。
        /// </summary>
        private static List<string> GetTableCreationScripts() {
            return [
                // schema 版本审计表放最前:它是版本机制的载体,先于业务表建立。
                SchemaMigrationTableScript,

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

                // 软删除两列从 SoftDeleteColumnsDdl 拼入(与 EnsureSoftDeleteSchemaIfNeeded 同源)
                $@"
                CREATE TABLE IF NOT EXISTS [lookups] (
                    [word_key] TEXT, 
                    [usage] TEXT, 
                    [title] TEXT, 
                    [authors] TEXT, 
                    [timestamp] TEXT,
                    {SoftDeleteColumnsDdl}, 
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

                $@"
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
                    [colorRGB] INTEGER DEFAULT(-1), 
                    {SoftDeleteColumnsDdl}
                );",

                // 导入批次留底:每次导入记一行(来源 + 时间 + 计数)。vocab/lookups 的 origin_batch
                // 指回这里 —— 既能事后对账"这批词是哪次导进来的",也是「按批次撤销导入」的抓手。
                // 老库由 EnsureSoftDeleteSchemaIfNeeded 补建,与本表同样幂等。
                ImportBatchTableScript,

                // 查询索引:新建的库在这里直接带上,老库由 EnsureIndexesIfNeeded 补。
                // 两处共用 GetIndexScripts(),只维护一份 SQL。
                .. GetIndexScripts()
            ];
        }

        /// <summary>
        /// 全部查询索引脚本的唯一来源 —— 建库脚本与老库迁移都从这里取,避免两处 SQL 漂移
        /// (漂移的后果是"新建的库有索引、老库永远没有",而且不会有任何报错)。
        /// </summary>
        private static IEnumerable<string> GetIndexScripts() {
            yield return ClippingsBookIndexScript;
            yield return VocabWordKeyIndexScript;
            yield return LookupsTimestampIndexScript;
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
            // 格式与 culture 的约定集中在 AppConstants.FileTimestampFormat ——
            // 备份文件名读不出日期,用户就再也按名字找不回旧备份。
            var timestamp = DateTime.Now.ToString(AppConstants.FileTimestampFormat, CultureInfo.InvariantCulture);
            var backupFileName = Path.GetFileNameWithoutExtension(databaseFileName) + 
                                BackupFileNameInfix + timestamp + 
                                Path.GetExtension(databaseFileName);
            var backupFilePath = Path.Combine(backupPath, backupFileName);

            // 同一秒内再来一次备份就会撞名(时间戳只到秒)。这里**自动加序号另存**,
            // 而不是让整次备份失败:
            //
            // 撞名时拒绝覆盖是对的(不能毁掉上一份),但"拒绝"的正确代价是**换个名字存下来**,
            // 不是把用户的备份请求整个打回。此前没有这一层,于是:
            // 「维护数据库」在改数据前会无条件备份一次,若恰好与刚才的手动备份落在同一秒,
            // 整条维护就以「维护失败」收场、还不给原因(2026-09-22 实测 6 次里挂 2 次)。
            // 手动连点两次备份同理。
            //
            // 后缀用 `_2`/`_3`… 而不是 `-2`:下划线(0x5F)排在 `.`(0x2E)之后,
            // 而 PruneBackups 按**文件名字典序**判定新旧 —— 这样同一秒内的多份也保持
            // "越晚越靠后",不会让清理先删掉较新的那一份。
            var uniqueBackupPath = backupFilePath;
            for (var suffix = 2; File.Exists(uniqueBackupPath); suffix++) {
                uniqueBackupPath = Path.Combine(backupPath,
                    Path.GetFileNameWithoutExtension(backupFileName) + "_" + suffix +
                    Path.GetExtension(backupFileName));
            }

            // 走 VACUUM INTO 而不是 File.Copy。文件名带时间戳(必要时再加序号),目标必然不存在;
            // 不传 overwrite,语义与原先的 File.Copy(overwrite: false) 一致 ——
            // **任何情况下都不静默覆盖上一份备份**(拒绝覆盖这条由 CreateConsistentSnapshot 保证,
            // 且有用例钉住;这里只是保证不会走到那个冲突上去)。
            CreateConsistentSnapshot(databaseFilePath, uniqueBackupPath);
        }

        /// <summary>
        /// 只保留最新的 <paramref name="keepCount"/> 份备份,把更旧的删掉。用于退出备份这类
        /// **每次关闭都产生一份**的自动产物 —— 攒几十份没有意义,只留最近几次即可回滚。
        /// </summary>
        /// <remarks>
        /// ① 排序按**文件名**而不是修改时间:名字里的时间戳是 <c>yyyyMMdd_HHmmss</c> 定长零填充,
        /// 字典序即时间序,且不会因为复制 / 云同步 / 解压重写 mtime 而错判先后
        /// (那条 InvariantCulture 修复正是这个前提的一部分,见 BackupFilenameCultureTests)。
        /// 比较器显式用 <see cref="StringComparer.Ordinal"/> —— 默认的 <c>Comparer&lt;string&gt;.Default</c>
        /// 走 CurrentCulture,这里没必要把"保留哪几份"交给区域设置决定。
        ///
        /// ② 只匹配**同一个库**的备份(<c>KM2_backup_*.dat</c>)。目录里可能同时躺着其它库、
        /// 手写笔记、<c>.snapshot-tmp</c> 残留等,一律不动。
        ///
        /// ③ **刻意不抛异常**:它在退出路径上被调用,进程正在退出,抛出去只会变成日志里一条
        /// 没人能处理的噪音。单份删不掉就跳过(Windows 上文件被占用是常态),下次退出还会再试。
        /// </remarks>
        /// <param name="backupDirectory">备份目录;不存在则直接返回 0。</param>
        /// <param name="keepCount">保留份数;<c>&lt;= 0</c> 按 1 处理 —— 避免调用方传 0 时把备份全删光。</param>
        /// <param name="databaseFileName">库文件名(如 <c>KM2.dat</c>),用于界定"本库的备份"。</param>
        /// <returns>实际删除的份数。</returns>
        public static int PruneBackups(string backupDirectory, int keepCount, string databaseFileName) {
            if (string.IsNullOrWhiteSpace(backupDirectory) || string.IsNullOrWhiteSpace(databaseFileName)) {
                return 0;
            }
            if (!Directory.Exists(backupDirectory)) {
                return 0;
            }

            var keep = Math.Max(1, keepCount);
            var pattern = Path.GetFileNameWithoutExtension(databaseFileName) +
                          BackupFileNameInfix + "*" +
                          Path.GetExtension(databaseFileName);

            string[] files;
            try {
                files = Directory.GetFiles(backupDirectory, pattern);
            } catch {
                return 0;   // 目录不可读(权限等):当作没有可清理的,不影响主流程
            }

            Array.Sort(files, StringComparer.Ordinal);
            var deleted = 0;
            // 升序排列 → 末尾 keep 份是最新的,从头删到倒数第 keep 份为止
            for (var i = 0; i < files.Length - keep; i++) {
                try {
                    File.Delete(files[i]);
                    deleted++;
                } catch { /* 单份删不掉就跳过,其余继续 */ }
            }
            return deleted;
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
        /// 并发:写入方持有独占锁时 VACUUM 会 SQLITE_BUSY。**总等待窗口由连接上的命令超时决定**
        /// (见 <see cref="BuildConnectionString"/> 统一设置的 <c>Default Timeout</c>;Microsoft.Data.Sqlite
        /// 的托管层按 <c>CommandTimeout</c> 重试)。SQLite 层的 <c>PRAGMA busy_timeout</c>
        /// (即 <paramref name="busyTimeoutMs"/>)只决定**单次**内部等待的长短,管不住总窗口 ——
        /// 把它设小并不会让这里更早失败。但**长时间导入/清理过程中仍可能超时失败** ——
        /// 调用方应在 UI 层避免与这些操作并发触发,不要只依赖这里的等待。
        /// </remarks>
        /// <param name="sourcePath">源数据库文件路径</param>
        /// <param name="destPath">快照输出路径;父目录必须已存在</param>
        /// <param name="overwrite">目标已存在时是否先删除。默认 false(抛错),避免静默覆盖</param>
        /// <param name="busyTimeoutMs">
        /// SQLite 层 <c>PRAGMA busy_timeout</c> 的毫秒数(单次锁等待);<c>0</c> 表示关闭 SQLite 内部的
        /// busy handler。**注意**:总等待窗口由连接的命令超时(<c>Default Timeout</c> = 30s)决定,
        /// 本参数设小不会让调用更早失败(托管层仍会重试到命令超时)。
        /// </param>
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
                using var connection = new SqliteConnection(BuildConnectionString(fullSourcePath, SqliteOpenMode.ReadOnly, sharedCache: false));
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
                using var connection = new SqliteConnection(BuildConnectionString(filePath, SqliteOpenMode.ReadWrite, sharedCache: true));
                connection.Open();
                using var command = new SqliteCommand("VACUUM;", connection);
                command.ExecuteNonQuery();
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to vacuum database '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 确保全部查询索引(<see cref="GetIndexScripts"/>)存在。幂等
        /// (<c>CREATE INDEX IF NOT EXISTS</c>),每次启动调用都无副作用。
        /// </summary>
        /// <remarks>
        /// <list type="number">
        /// <item><c>[clippings]</c> 原本只有 <c>[key]</c> 主键,而按书名取书摘
        /// (<c>WHERE bookname = @bookname</c>,仓储里有 6 处)与主列表排序
        /// (<c>ORDER BY bookname, pagenumber, clippingdate</c>)都命中不了索引 —— 几万条的库上
        /// 就是全表扫描加临时排序。这里建一个覆盖这三列的复合索引:前缀等值查询与按序扫描都能用上它。</item>
        /// <item><c>[vocab].[word_key]</c> —— 见 <see cref="VocabWordKeyIndexScript"/>,
        /// 它是词频重算从 O(vocab²) 降到 O(vocab·log) 的关键。</item>
        /// <item><c>[lookups].[timestamp]</c> —— 见 <see cref="LookupsTimestampIndexScript"/>,
        /// 服务 <c>GetByTimestamp</c>;复合唯一键的最左前缀不含 <c>timestamp</c>,缺它即整表扫。</item>
        /// </list>
        /// 老库靠本方法补齐;新建的库由 <see cref="GetTableCreationScripts"/> 直接带上。
        /// **两条路径必须始终取同一份脚本**,否则会出现"新库有索引、老库没有"这种静默的错配。
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
                foreach (var script in GetIndexScripts()) {
                    using var command = new SqliteCommand(script, connection);
                    command.ExecuteNonQuery();
                }
            } catch (Exception e) {
                throw new InvalidOperationException($"Failed to ensure indexes in '{filePath}': {e.Message}", e);
            }
        }

        /// <summary>
        /// 报告库里**缺失的表**(结构完整性检查,只读,**不做任何修补**)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 用途单一:库打不开时把「结构损坏」这一种成因识别出来,好让上层给用户一条看得懂、能照着做的
        /// 提示,而不是把 <c>SQLite Error 1: 'no such table: vocab'.</c> 原样甩出去 —— 那是给开发者看的。
        /// </para>
        /// <para>
        /// <b>刻意只读</b>:不补表、不动版本号。要不要修、怎么修由用户决定(见 issue #148 的取舍说明)——
        /// 自动补出来的是**空表**,原数据已经丢了,不该静默发生。
        /// </para>
        /// <para>
        /// 期望清单**取自建表脚本本身**(<see cref="GetTableCreationScripts"/>),不另维护一份表名列表 ——
        /// 另写一份必然与脚本漂移。索引脚本**不在**清单里:索引由 <see cref="EnsureIndexesIfNeeded"/>
        /// 每次启动补齐,缺索引不算「结构损坏」。
        /// </para>
        /// </remarks>
        /// <param name="filePath">SQLite 数据库文件路径。</param>
        /// <param name="missingTables">缺的表名(按建表脚本顺序);不缺表时为空集合。</param>
        /// <returns>
        /// 确实缺表时为 <c>true</c>。库正常、路径为空、文件不存在,或**读不出**(页损坏 / 不是 SQLite
        /// 文件 / 被占用)时均为 <c>false</c> —— 后几种不属于「缺表」这条路径,交给调用方沿用原来的报错。
        /// </returns>
        public static bool TryDescribeMissingTables(string filePath, out IReadOnlyList<string> missingTables) {
            missingTables = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return false;
            }

            try {
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var connection = new SqliteConnection(GetConnectionString(filePath))) {
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT [name] FROM [sqlite_master] WHERE [type] = 'table';";
                    using var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        existing.Add(reader.GetString(0));
                    }
                }

                var absent = ExpectedTableNames().Where(name => !existing.Contains(name)).ToList();
                if (absent.Count == 0) {
                    return false;
                }

                missingTables = absent;
                return true;
            } catch {
                // 读不出(页损坏 / 不是 SQLite / 被占用):识别不了就不硬猜 —— 让调用方走原来的报错。
                return false;
            }
        }

        /// <summary>建表脚本里声明要建的表名,按脚本顺序。</summary>
        /// <remarks>
        /// 脚本形态固定为 <c>CREATE TABLE IF NOT EXISTS [名字] (</c>,按这个前缀取名字即可 ——
        /// 不需要正则,也不会误取到索引脚本(<c>CREATE INDEX …</c> 不含该前缀)。
        /// 若将来新增的建表脚本换了写法,<c>SchemaIntegrityTests</c> 里那条「期望表名集合」用例会红,
        /// 从而逼着人回来看这里。
        /// </remarks>
        private static IEnumerable<string> ExpectedTableNames() {
            const string prefix = "CREATE TABLE IF NOT EXISTS [";
            foreach (var script in GetTableCreationScripts()) {
                var start = script.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (start < 0) {
                    continue;
                }
                start += prefix.Length;
                var end = script.IndexOf(']', start);
                if (end > start) {
                    yield return script[start..end];
                }
            }
        }

        /// <summary>
        /// 读取库的 schema 版本(<c>PRAGMA user_version</c>)。
        /// **读不出来时返回 0,不抛** —— 库损坏 / 非 SQLite 文件交给后续流程去报错,
        /// 不在这里引入新的崩溃点。
        /// </summary>
        /// <param name="filePath">SQLite 数据库文件路径。</param>
        /// <returns>版本号;路径为空、文件不存在或读取失败时返回 0。</returns>
        public static int GetSchemaVersion(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return 0;
            }

            try {
                using var connection = new SqliteConnection(GetConnectionString(filePath));
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA user_version;";
                var result = command.ExecuteScalar();
                return result is null or DBNull ? 0 : Convert.ToInt32(result);
            } catch {
                // 库被占用 / 文件不是 SQLite / 权限不足…… 一律当作"读不出",交给上层继续按原版方式报错。
                return 0;
            }
        }

        /// <summary>
        /// 在**给定连接**上读 <c>PRAGMA user_version</c>;读不出就抛。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="GetSchemaVersion"/> 的区别是**不吞异常**:
        /// <list type="bullet">
        /// <item><see cref="GetSchemaVersion"/> 返回 0 表示"读不出来",调用方(VM 的前向保护)据此
        /// 继续走既有的报错路径即可 —— 那是"读不到就当作最老"的宽松口径。</item>
        /// <item>迁移路径上则必须严格:把"读不出来"当成 v0 会把高版本库**降级**(见
        /// <see cref="MigrateSchemaIfNeeded"/> 里的说明),故这里失败即抛、中止迁移。</item>
        /// </list>
        /// </remarks>
        /// <exception cref="InvalidOperationException">读取失败或结果为 NULL 时抛出。</exception>
        private static int ReadUserVersion(SqliteConnection connection) {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var result = command.ExecuteScalar();
            if (result is null or DBNull) {
                throw new InvalidOperationException("无法读取 PRAGMA user_version(结果为 NULL)。");
            }
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// 幂等的 schema 迁移:把老库(阶段 0 之前建的库,即 v0)升到 <see cref="CurrentSchemaVersion"/>,
        /// 并**补齐缺失的表**(见下方能力边界)。
        /// </summary>
        /// <remarks>
        /// 全过程在**单事务**内完成(建全表 + 写审计行 + 设 <c>user_version</c>)——
        /// 因为 <c>PRAGMA user_version</c> 随事务回滚(见 <see cref="CurrentSchemaVersion"/>),
        /// 这几步要么全成、要么全不动,不会留下"表建好了但版本没设"的中间态。
        /// <para>
        /// **能力边界**(重要,别以为它万能):
        /// </para>
        /// <list type="bullet">
        /// <item>✅ **能修**:<c>user_version</c> 落后于当前版本(含 0)+ 表缺失 / 不全。
        /// 遍历 <see cref="GetTableCreationScripts"/> 把缺的表补上,再标记版本。典型场景是
        /// 「建库中途被打断」留下的**残缺库** —— <c>CreateDatabase</c> 不是原子的(逐脚本执行、
        /// 无事务),建到一半被打断后文件已在磁盘上,下次启动 <c>File.Exists==true</c> 会跳过
        /// <c>CreateDatabase</c>,这里就是唯一的补救点。</item>
        /// <item>❌ **不能修**:<c>user_version</c> 已是当前版本、但表缺失(例如文件损坏)。
        /// 下面的早退判断会直接返回,不做任何检查。那种情况需要单独的完整性检查,**不在本方法职责内**。</item>
        /// </list>
        /// <para>
        /// 全部脚本都是 <c>CREATE TABLE / INDEX IF NOT EXISTS</c>,对**正常库是彻底的 no-op** ——
        /// 因此这是纯粹的自愈,不会改写任何既有数据。顺带也会补索引(脚本集合末尾展开了
        /// <see cref="GetIndexScripts"/>);与 <see cref="EnsureIndexesIfNeeded"/> 的重叠**无害**
        /// (两者都幂等),故不必去动那个方法。
        /// </para>
        /// <para>
        /// 已是当前版本或更高 → 直接返回,不做任何事。**注意:版本过高不在这里报错** ——
        /// 拒绝打开过高版本是 VM 层的职责(<c>MainWindowViewModel.PrepareDatabaseAsync</c> 的前向保护),
        /// DB 层只保证"不破坏"(参见 <c>SchemaVersionTests</c> 的不降级用例)。
        /// </para>
        /// </remarks>
        /// <param name="filePath">SQLite 数据库文件路径;为空或文件不存在时直接返回。</param>
        /// <exception cref="InvalidOperationException">迁移失败(与 <see cref="MigrateLookupsSchemaIfNeeded"/> 的既有约定一致)。</exception>
        public static void MigrateSchemaIfNeeded(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return;
            }

            try {
                using var connection = new SqliteConnection(GetConnectionString(filePath));
                connection.Open();

                // 版本号在**本方法自己的连接**上读,且读不出就抛 —— 这里刻意不用 GetSchemaVersion:
                // 那个方法的契约是"读不出来返回 0,不抛"(供 VM 的前向保护用),于是把「读失败」与
                // 「v0 老库」归成了同一个 0。若在这里沿用它,一次瞬时读失败(例如库被短暂占用)就会让
                // 一个**更高版本**的库被当成 v0:建表脚本全是 IF NOT EXISTS 故无结构破坏,但最后会把
                // user_version 写成当前值 —— 即把库**降级**,前向保护随之失效。这里失败即中止,绝不下写版本。
                if (ReadUserVersion(connection) >= CurrentSchemaVersion) {
                    return;
                }

                using var transaction = connection.BeginTransaction();
                try {
                    // 建全建表脚本(不只是审计表)—— 全部是 CREATE TABLE/INDEX IF NOT EXISTS,
                    // 对正常库是彻底的 no-op;对「建库中途被打断」留下的残缺库则是自愈:
                    // 缺哪张补哪张。此前只建 schema_migration,那类库的缺失业务表永远不会被补上
                    // (下次启动 File.Exists==true 会跳过 CreateDatabase,届时已无补救点)。
                    // 注意 GetTableCreationScripts() 已包含 SchemaMigrationTableScript,
                    // 故这里**不要**再单独跑一遍审计表脚本。
                    foreach (var script in GetTableCreationScripts()) {
                        using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = script;
                        command.ExecuteNonQuery();
                    }

                    // 设版本号是事务的**最后一步** —— 与建表、写审计一起原子提交。
                    MarkSchemaVersion(connection, transaction, CurrentSchemaVersion, "initial schema");

                    transaction.Commit();
                } catch {
                    transaction.Rollback();
                    throw;
                }
            } catch (Exception e) {
                throw new InvalidOperationException($"Failed to migrate schema in '{filePath}': {e.Message}", e);
            }
        }

        /// <summary>
        /// 把库标记为指定 schema 版本,并写一条审计记录。
        /// </summary>
        /// <remarks>
        /// <paramref name="transaction"/> 为 <c>null</c> 表示不在显式事务里(新库建库路径)。
        /// <para>
        /// ⚠️ 在事务里调用时,**必须把本方法放在事务的最后一步** —— 见 <see cref="CurrentSchemaVersion"/>:
        /// <c>PRAGMA user_version</c> 随事务回滚,先提交再设版本会留下不一致的中间态。
        /// </para>
        /// <para>
        /// ⚠️ **内部顺序固定为「先写审计行、后设 <c>PRAGMA</c>」,不要对调。** 建库路径
        /// (<paramref name="transaction"/> 为 <c>null</c>)是自动提交、没有事务可回滚,顺序决定了
        /// 「被打断」时留下的是哪种中间态:
        /// <list type="bullet">
        /// <item>本顺序 → 中间态是「审计行已落、版本仍是 0」。下次启动
        /// <see cref="MigrateSchemaIfNeeded"/> 的早退判断不成立,会重跑一遍并
        /// <c>INSERT OR REPLACE</c> 覆盖那行 ⇒ **自愈**。</item>
        /// <item>反过来(先设版本) → 中间态是「版本已是当前值、审计行缺失」。下次启动迁移会因
        /// <c>version &gt;= CurrentSchemaVersion</c> **早退**,那条审计行**永远补不回来**。</item>
        /// </list>
        /// 也就是说:让"版本号"成为提交点 —— 它写成功才算这一步完成。
        /// </para>
        /// <para>
        /// <c>PRAGMA</c> 不支持参数绑定,版本号只能插值 —— 它是内部常量,安全。
        /// 审计行用 <c>INSERT OR REPLACE</c>:幂等地重复标记同一版本时不会撞主键。
        /// </para>
        /// </remarks>
        private static void MarkSchemaVersion(SqliteConnection connection, SqliteTransaction? transaction,
            int version, string description) {
            // 顺序见上:审计行先写,PRAGMA 最后设 —— 让版本号成为"这一步已完成"的提交点。
            using (var auditCommand = connection.CreateCommand()) {
                auditCommand.Transaction = transaction;
                auditCommand.CommandText =
                    "INSERT OR REPLACE INTO [schema_migration] ([version], [applied_at], [description]) VALUES (@version, @appliedAt, @description);";
                auditCommand.Parameters.AddWithValue("@version", version);
                // 时间戳格式必须配 InvariantCulture:否则泰历/回历等区域会写出非公历日期。
                auditCommand.Parameters.AddWithValue("@appliedAt",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                auditCommand.Parameters.AddWithValue("@description", description);
                auditCommand.ExecuteNonQuery();
            }

            using var versionCommand = connection.CreateCommand();
            versionCommand.Transaction = transaction;
            versionCommand.CommandText = $"PRAGMA user_version = {version};";
            versionCommand.ExecuteNonQuery();
        }
        
        /// <summary>
        /// 软删除 + 导入批次留底的幂等补丁(「就地演进」第一步,2026-10-11):
        /// <list type="bullet">
        /// <item>给 <c>[vocab]</c> / <c>[lookups]</c> 补 <c>deleted_at</c>(软删除时间,NULL=活跃)
        /// 与 <c>origin_batch</c>(来自哪次导入,NULL=未知)两列;</item>
        /// <item>补建 <c>[import_batches]</c> 表(每次导入记一行来源与计数)。</item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// <para>
        /// 为什么删除要留底:此前生词/查询是硬删,判重集合又全靠**活表现读**
        /// (<c>GetAll()</c> 进内存 HashSet),于是"删掉的词再导一次"会被原样插回来 ——
        /// 标注侧正是靠 original_clipping_lines 留底避免了这个问题,生词侧一直缺位。
        /// 现在删除只置 <c>deleted_at</c>,行不消失 ⇒ 判重键仍占位 ⇒ 重导入被跳过;
        /// 且粒度天然正确:被删的 (词+时间戳) 拦下,同词**新时间戳**的查询照样导入。
        /// </para>
        /// <para>
        /// <b>调用顺序有要求:必须在 <see cref="MigrateLookupsSchemaIfNeeded"/> 之后</b> ——
        /// 那个旧迁移重建 lookups 表时用的是老的 5 列脚本,会把这两列抹掉;放在它之后跑,
        /// 缺了就再补回来。本方法按「列是否存在」判定(而非版本号),因此对新库、老库、
        /// 半迁移库都幂等,重复调用是 no-op。
        /// </para>
        /// </remarks>
        /// <param name="filePath">SQLite 数据库文件路径;为空或文件不存在时直接返回。</param>
        /// <exception cref="InvalidOperationException">迁移失败。</exception>
        public static void EnsureSoftDeleteSchemaIfNeeded(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return;
            }

            try {
                using var connection = new SqliteConnection(GetConnectionString(filePath));
                connection.Open();

                using var transaction = connection.BeginTransaction();
                try {
                    // 列定义与建库 DDL 同源(SoftDeleteColumns),两处不再各写一遍
                    foreach (var table in new[] { "vocab", "lookups" }) {
                        foreach (var (column, columnType) in SoftDeleteColumns) {
                            EnsureColumn(connection, transaction, table, column, columnType);
                        }
                    }

                    using (var command = connection.CreateCommand()) {
                        command.Transaction = transaction;
                        command.CommandText = ImportBatchTableScript;
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                } catch {
                    transaction.Rollback();
                    throw;
                }
            } catch (Exception e) {
                throw new InvalidOperationException($"Failed to ensure soft-delete schema in '{filePath}': {e.Message}", e);
            }
        }

        /// <summary>
        /// 给已存在的表补一列(已存在则 no-op)。用 <c>PRAGMA table_info</c> 探测而不是直接
        /// <c>ALTER TABLE</c> —— SQLite 对重复列名是硬错误,探测后跳过才能真正幂等。
        /// </summary>
        private static void EnsureColumn(SqliteConnection connection, SqliteTransaction transaction,
            string table, string column, string columnType) {
            var exists = false;
            using (var pragma = connection.CreateCommand()) {
                pragma.Transaction = transaction;
                pragma.CommandText = $"PRAGMA table_info([{table}]);";
                using var reader = pragma.ExecuteReader();
                while (reader.Read()) {
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) {
                        exists = true;
                        break;
                    }
                }
            }
            if (exists) {
                return;
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"ALTER TABLE [{table}] ADD COLUMN [{column}] {columnType};";
            command.ExecuteNonQuery();
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

                    // 先清掉可能残留的临时表再建:lookups_migrated 是本次迁移**自用的草稿表**,
                    // 名字固定、App 从不读它 —— 若上一次迁移在"建表后、改名(或回滚)前"被打断
                    // 而留下同名表,不加处理会让下面的 INSERT 往旧草稿表里塞数据、RENAME 又覆盖
                    // 真正的 lookups,轻则迁移永久失败、重则串数据。DROP 掉重建最稳(草稿表内
                    // 绝无用户数据 —— 用户数据始终在 lookups 里)。
                    command.CommandText = "DROP TABLE IF EXISTS [lookups_migrated];";
                    command.ExecuteNonQuery();

                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS [lookups_migrated] (
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
        /// <remarks>
        /// **探测失败即抛,绝不吞异常返回 false。** 旧实现把 <c>catch (Exception)</c> 一律当
        /// 「无需迁移」,于是 PRAGMA 一旦读不出来(库损坏 / 非 SQLite / 被占用),方法就判定
        /// 「表已是新形态」并静默跳过 —— legacy lookups(单列 <c>UNIQUE(timestamp)</c>)永远得不到
        /// 重建,之后同秒多词插入会撞唯一约束失败且无任何告警。这里区分两种结局:
        /// <list type="bullet">
        /// <item><b>已确认无需迁移</b>:能正常读到索引且不满足 legacy 特征 → 返回 <c>false</c>;</item>
        /// <item><b>无法判定</b>:探测过程本身抛错 → 让异常向上传播,由
        /// <see cref="MigrateLookupsSchemaIfNeeded"/> 包装成 <see cref="InvalidOperationException"/>,
        /// 最终经启动流程的 <c>MigrationWarning</c> 让用户知晓(与既有错误处理风格一致)。</item>
        /// </list>
        /// </remarks>
        private static bool NeedsLookupsSchemaMigration(SqliteConnection connection) {
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
        }

        public static string? GetSafeString(SqliteDataReader reader, int ordinal) {
            if (reader.IsDBNull(ordinal)) {
                return null;
            }
            var s = reader.GetString(ordinal);
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        /// <summary>
        /// 探测某表是否已有某列(<c>PRAGMA table_info</c>)。
        /// 用途:扁平 KM 导入用**同一套仓储**读旧格式源库(只读),那些库没有后来加的列
        /// (如 vocab/lookups 的 deleted_at / origin_batch)—— 读取 SQL 必须按源库实际
        /// schema 分支,否则一条 <c>no such column</c> 会把整份导入打死。
        /// </summary>
        public static bool HasColumn(SqliteConnection connection, string table, string column) {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info([{table}]);";
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 迁移后置条件探测(启动时调用):<c>vocab</c> / <c>lookups</c> **表在**,却缺
        /// <c>deleted_at</c> / <c>origin_batch</c> 任一列 → 软删除迁移失败了,返回 true。
        /// </summary>
        /// <remarks>
        /// 为什么失败要升级成硬失败而不是"仅告警继续":仓储的**读**路径按 <see cref="HasColumn"/>
        /// 分支还能读,**写**路径(插入 / 编辑 / 删除 / 词频)却会直接
        /// <c>no such column: deleted_at</c> —— 半迁移库"能打开、看得见,但什么都写不了",
        /// 用户只会撞上含糊的 SQLite 报错。由调用方(启动流程)按 Fatal 拒绝打开,
        /// 提示清楚成因,修复(磁盘 / 占用)后重启即可 —— 迁移按列探测幂等,重跑安全。
        /// <para>
        /// **表本身缺失**(结构损坏)不归这里管:返回 false,交给既有的缺表提示 /
        /// 库打不开错误框,免得把"结构损坏"误诊成"迁移失败"。读不出来同理返回 false ——
        /// 契约是"确认缺列才报",与 <c>GetSchemaVersion</c> 的前向保护同思路。
        /// </para>
        /// </remarks>
        public static bool NeedsSoftDeleteColumns(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return false;
            }
            try {
                using var connection = new SqliteConnection(GetConnectionString(filePath));
                connection.Open();
                foreach (var table in new[] { "vocab", "lookups" }) {
                    if (!TableExists(connection, table)) {
                        return false;
                    }
                    foreach (var (column, _) in SoftDeleteColumns) {
                        if (!HasColumn(connection, table, column)) {
                            return true;
                        }
                    }
                }
                return false;
            } catch {
                return false;
            }
        }

        /// <summary>表是否存在于 <c>sqlite_master</c>。</summary>
        private static bool TableExists(SqliteConnection connection, string table) {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name;";
            command.Parameters.AddWithValue("@name", table);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// 批量插入「逐条降级」兜底时,判定某条失败是否可以安全跳过:
        /// **只有 SQLite 约束冲突**(<c>SQLITE_CONSTRAINT = 19</c>,含重复主键 / 唯一约束撞车)算可跳过。
        /// </summary>
        /// <remarks>
        /// 旧实现把**任意** <see cref="InvalidOperationException"/> 也当可跳过,范围过宽 ——
        /// 那会把真实故障(数据层内部状态错乱等)一并吞成"导入成功、只少几条"。现已收窄:
        /// <list type="bullet">
        /// <item>"数据非法(如 key 为空)"这一类**不再靠异常**判定,而由各仓储的逐条降级循环
        /// <b>显式跳过空 key 的行</b>(见 <c>ClippingRepository.Add(List)</c> 等);</item>
        /// <item>磁盘满(<c>SQLITE_FULL</c>)、库被锁(<c>SQLITE_BUSY</c>)、I/O / 损坏等系统性问题
        /// 一律 <c>false</c> —— 必须向上抛,绝不能伪装成「导入成功、只是少了几条」。</item>
        /// </list>
        /// 判定取 <c>code &amp; 0xFF</c>:正常路径下 <see cref="SqliteException.SqliteErrorCode"/> 就是主码
        /// (19);掩码只是**防御性**地容忍某些来源把扩展码(如 <c>SQLITE_CONSTRAINT_UNIQUE</c>)放进该
        /// 属性,不必据此认为修好了真实漏判。
        /// </remarks>
        public static bool IsSkippableInsertFailure(Exception ex) {
            return ex is SqliteException { SqliteErrorCode: var code } && (code & 0xFF) == 19;
        }

        /// <summary>
        /// 容错读取整数列:NULL → <c>null</c>;非 NULL 但**不是整数**(如历史库把数值存成 TEXT、
        /// 存了非数字文本或 REAL)时也返回 <c>null</c>,而不是让调用方拿到一个静默转换出来的值。
        /// </summary>
        /// <remarks>
        /// 旧实现直接 <c>reader.GetInt32/GetInt64</c>。对本项目依赖的 Microsoft.Data.Sqlite,
        /// 这两个 API 最终调到 <c>sqlite3_column_int64</c>,**不会**对 TEXT/REAL 抛
        /// <c>InvalidCastException</c> —— 它会静默转换(<c>'abc'</c>→0、<c>'3.5'</c>/REAL 3.5→3)。
        /// 真正的失败模式是:
        /// <list type="bullet">
        /// <item>脏值被静默当成 0,与"缺值"不可区分,读路径无从发现;</item>
        /// <item><c>GetInt32</c> 对超出 int32 范围的值抛 <c>OverflowException</c>,整条读取被打死。</item>
        /// </list>
        /// 现在改为 <c>Convert.ToString + TryParse</c> 兜底为 <c>null</c>,由实体 setter 决定默认值;
        /// 口径与 <c>KmateSourceReader.ParseLong/ParseInt</c> 一致(那边早已如此)。
        /// </remarks>
        public static int? GetSafeInt(SqliteDataReader reader, int ordinal) {
            if (reader.IsDBNull(ordinal)) {
                return null;
            }
            var raw = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        /// <inheritdoc cref="GetSafeInt(SqliteDataReader, int)"/>
        public static long? GetSafeLong(SqliteDataReader reader, int ordinal) {
            if (reader.IsDBNull(ordinal)) {
                return null;
            }
            var raw = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        /// <summary>容错读取整数列,无法解析时返回 <paramref name="defaultValue"/>(语义同 <see cref="GetSafeInt(SqliteDataReader, int)"/>)。</summary>
        public static int GetSafeInt(SqliteDataReader reader, int ordinal, int defaultValue) {
            if (reader.IsDBNull(ordinal)) {
                return defaultValue;
            }
            var raw = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
        }
        
        /// <summary>
        /// 用 <see cref="SqliteConnectionStringBuilder"/> 构造连接串 —— 不要用字符串插值拼
        /// <c>Data Source=…</c>:路径里含 <c>;</c>(Unix 与 Windows 都合法)会被解析成额外关键字而报错。
        /// </summary>
        /// <summary>
        /// 转义 LIKE 模式里的通配符(<c>%</c>、<c>_</c>)与转义符本身(<c>\</c>),让用户输入的
        /// 搜索词按字面匹配。必须配合 SQL 里的 <c>ESCAPE '\'</c> 使用:否则搜 <c>a_b</c> 会命中
        /// <c>aXb</c>,搜 <c>%</c>/<c>_</c> 会命中几乎全部。
        /// </summary>
        public static string EscapeLikePattern(string? value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }
            return value
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        public static string BuildConnectionString(string dbFile, SqliteOpenMode mode, bool sharedCache) {
            return new SqliteConnectionStringBuilder {
                DataSource = dbFile,
                Mode = mode,
                Cache = sharedCache ? SqliteCacheMode.Shared : SqliteCacheMode.Default,
                // 统一锁等待窗口:所有连接都从这里取,避免各调用点各写一份而漂移(见常量说明)。
                DefaultTimeout = ConnectionDefaultTimeoutSeconds
            }.ToString();
        }

        public static string GetConnectionString(string dbFile) {
            return BuildConnectionString(dbFile, SqliteOpenMode.ReadWrite, sharedCache: true);
        }
    }
}
