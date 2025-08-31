namespace KindleMate2.Domain.Entities.VocabDB {
    public class Word {
        public string id { get; set; }
        public string word { get; set; }
        public string stem { get; set; }
        public string lang { get; set; }
        public int category { get; set; } = 0;
        public int timestamp { get; set; } = 0;
        public string profileid { get; set; }
    }
}
