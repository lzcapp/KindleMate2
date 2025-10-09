namespace KindleMate2.Shared.Constants {
    public static class AppConstants {
        public const string AppName = "MyApp";
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
        public const string ExplorerFileName = "explorer.exe";
        public const string CSSFileName = "styles.css";

        public const string SpaceForNewLine = " 　　";

        public const string ExplorerSelect = "/select,";
        
        public const string RepoUrl = "https://github.com/lzcapp/KindleMate2";

        public const string ConnectionString = "Data Source=KM2.dat;Cache=Shared;Mode=ReadWrite;";

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