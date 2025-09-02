namespace KindleMate2.Domain.Entities.KM2DB {
    public class Lookup {
        public required string WordKey { get; set; }
        public required string Usage { get; set; }
        public required string Title { get; set; }
        public required string Authors { get; set; }
        public required string Timestamp { get; set; }
        public string Word { get; set; }
        public string Stem { get; set; }
        public string Frequency { get; set; }
    }
}
