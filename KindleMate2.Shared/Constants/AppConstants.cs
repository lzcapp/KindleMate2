namespace KindleMate2.Shared.Constants {
    public static class AppConstants {
        public const string AppName = "KindleMate2";
        public const string DateFormat = "yyyy-MM-dd";
        public const int DefaultPageSize = 50;
        
        public const string Exception = "Exception";

        public const string LookupCount = "LookupCount";
        public const string InsertedLookupCount = "InsertedLookupCount";
        public const string InsertedVocabCount = "InsertedVocabCount";
        public const string ParsedCount = "ParsedCount";
        public const string InsertedCount = "InsertedCount";
        public const string FileSizeDelta = "FileSizeDelta";
        public const string EmptyCount = "EmptyCount";
        public const string TrimmedCount = "TrimmedCount";
        public const string DuplicatedCount = "DuplicatedCount";
        public const string SkippedDateCount = "SkippedDateCount";
        public const string SkippedPageCount = "SkippedPageCount";
        public const string SkippedLimitCount = "SkippedLimitCount";

        public const string SettingTheme = "theme";
        public const string SettingLanguage = "lang";
        
        public const string DatabaseNoNeedCleaning = "Database_No_Need_Cleaning";
        
        public const string Css = "@import url(https://fonts.googleapis.com/css2?family=Noto+Color+Emoji&family=Noto+Emoji:wght@300..700&display=swap);*{font-family:-apple-system,\"Noto Sans\",\"Helvetica Neue\",Helvetica,\"Nimbus Sans L\",Arial,\"Liberation Sans\",\"PingFang SC\",\"Hiragino Sans GB\",\"Noto Sans CJK SC\",\"Source Han Sans SC\",\"Source Han Sans CN\",\"Microsoft YaHei UI\",\"Microsoft YaHei\",\"Wenquanyi Micro Hei\",\"WenQuanYi Zen Hei\",\"ST Heiti\",SimHei,\"WenQuanYi Zen Hei Sharp\",\"Noto Emoji\",sans-serif}body{font-family:Arial,sans-serif;background-color:#f9f9f9;color:#333;line-height:1.6;align-items:center;width:80vw;margin:20px auto}h1{font-size:30px;text-align:center;margin:30px auto;color:#333}h2{font-size:24px;margin:30px auto;color:#333}p{font-size:16px;margin:20px auto}code{background-color:#faebd7;border-radius:10px;padding:2px 6px}";
        public const string HtmlBegin = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";
        public const string HtmlEnd = "\r\n</body></html>";

        public const string Kindle = "Kindle";
        public const string SystemPathName = "system";
        public const string DocumentsPathName = "documents";
        public const string VocabularyPathName = "vocabulary";
        public const string ImportsPathName = "Imports";
        public const string TempPathName = "Temp";
        public const string BackupsPathName = "Backups";

        /// <summary>
        /// 退出自动备份的专用子目录名,位于 <see cref="BackupsPathName"/> 之下。
        ///
        /// 退出备份**每次关闭都会产生一份**,而手动备份与「清洗前保护性备份」都落在 Backups 根下 ——
        /// 混在一起时根目录很快被一串时间戳文件淹没,用户真正主动要的那几份反而找不着。
        /// 分到子目录后:根目录只留用户自己要的,自动产物集中在一处、由保留策略统一收敛。
        /// </summary>
        public const string ExitBackupsPathName = "OnExit";

        /// <summary>
        /// 退出备份保留份数。超出部分按文件名时间戳从旧到新删除
        /// (见 <c>DatabaseHelper.PruneBackups</c>)。
        /// 退出备份的价值只在"最近几次",攒几十份既无用又占地方。
        /// </summary>
        public const int ExitBackupKeepCount = 3;

        public const string ExportsPathName = "Exports";
        public const string StatisticsPathName = "Statistics";
        
        public const string DatabaseFileName = "KM2.dat";
        public const string ClippingsFileName = "My Clippings.txt";
        public const string VocabFileName = "vocab.db";
        public const string VersionFileName = "version.txt";
        public const string CSSFileName = "styles.css";

        public const string SpaceForNewLine = " 　　";

        // 原先这里放着 ExplorerFileName("explorer.exe") 与 ExplorerSelect("/select,"):
        // 那是 Windows 专有的 shell 命令名,却在跨平台的 Shared 层对外暴露,调用方直接拿去
        // Process.Start,于是 macOS / Linux 上必然抛异常(统计页「打开截图所在位置」即受害者)。
        // 现已收进 KindleMate2.Infrastructure.Helpers.ShellHelper,按平台构造命令,勿再于本层新增此类常量。
        
        public const string RepoUrl = "https://github.com/lzcapp/KindleMate2";

        // 原先这里还放着 ConnectionString("Data Source=KM2.dat;Cache=Shared;Mode=ReadWrite;"):
        // 那是条**相对路径**连接串,实际指向哪个库取决于进程当前目录,只在"cwd 恰好等于库目录"
        // 时才成立。壳已改为按「当前打开的库」构造连接串(DatabaseHelper.GetConnectionString),
        // 该常量早已无人引用,故删除 —— 留着只会诱使新代码拿它去连一个说不清是哪个的库。

        public const string BookTitleFormat = " ——《{0}》";

        public const string LocationFormat = "{0} - {1}";
        
        // Common regex patterns
        public const string LocationRangePattern = @"(\d+)-(\d+)";
        public const string SingleNumberPattern = @"(\d+)";
        
        // File operation constants
        public const int DefaultStringBuilderCapacity = 512;
        /// <summary>
        /// **生成的**文件名里的时间戳格式:定长、零填充、只到秒。
        ///
        /// 两条不变量,别处依赖它们:
        /// <list type="number">
        /// <item>**字典序 = 时间序**。<c>PruneBackups</c> 的保留策略按文件名字典序判定新旧
        ///   (刻意不用 mtime —— 复制 / 云同步 / 解压都会重写 mtime),定长零填充正是这个前提。</item>
        /// <item>**必须配 <c>InvariantCulture</c>**。非公历日历会把年份换成别的历法
        ///   (th-TH → 2569、fa-IR → 1405、ar-SA → 1448),文件名就读不出真实日期了。</item>
        /// </list>
        ///
        /// 2026-09-22 收敛:此前是**两个常量 + 三处硬编码字面量**,同一个格式散在五个地方
        /// (库备份 / 维护清单 / 导入文件 / 清空前保底备份 / 统计截图)。代价已经付过一次 ——
        /// 那条 InvariantCulture 修复当年是**分头改的**(见 <c>ClearAllDataAsync</c> 与
        /// <c>WriteMaintenanceManifest</c> 里各自的注释,两处把同一个坑各写了一遍)。
        /// 现在只留这一个,并由 CI 断言守住:**除本文件外,源码里不许再有地方直接把
        /// 这个格式串喂给 <c>ToString</c>**。
        /// </summary>
        public const string FileTimestampFormat = "yyyyMMdd_HHmmss";
        
        // Character constants
        public const char ByteOrderMark = (char)65279;
        public const int BytesInKilobyte = 1024;

        public const string Zero = "0";
    }
}