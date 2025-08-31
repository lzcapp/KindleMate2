namespace KindleMate2.Domain.Entities.VocabDB {
    public class BookInfo {
        public required string id { get; set; } = null!;
        public string asin { get; set; }
        public string guid { get; set; }
        public string lang { get; set; }
        public string title { get; set; }
        public string authors { get; set; }
    }
}
