namespace KindleMate2.Shared.Constants {
    public static class AppConstants {
        public const string AppName = "KindleMate2";
        public const string DateFormat = "yyyy-MM-dd";
        public const string BackupDateFormat = "yyyyMMdd_HHmmss";
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
        public const string BackupTimestampFormat = "yyyyMMdd_HHmmss";
        
        // Character constants
        public const char ByteOrderMark = (char)65279;
        public const int BytesInKilobyte = 1024;

        public const string Zero = "0";
    }
}