namespace KindleMate2.Domain.Entities {
    public class Vocab {
        public required string id { get; set; } = null!;
        public string word_key { get; set; }
        public string word { get; set; } = null!;
        public string stem { get; set; }
        public int category { get; set; }
        public string translation { get; set; }
        public string timestamp { get; set; }
        public int frequency { get; set; }
        public int sync { get; set; }
        public int colorRGB { get; set; }
    }
}
