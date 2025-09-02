namespace KindleMate2.Domain.Entities.KM2DB {
    public class Vocab {
        public required string Id { get; set; }
        public required string WordKey { get; set; }
        public required string Word { get; set; }
        public required string Stem { get; set; }
        public int? Category { get; set; }
        public required string Translation { get; set; }
        public required string Timestamp { get; set; }
        public int? Frequency { get; set; }
        public int? Sync { get; set; }
        public int? ColorRgb { get; set; }
    }
}
