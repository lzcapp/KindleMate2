namespace KindleMate2.Domain.Entities.KM2DB {
    public class Vocab {
        public string Id { get; set; } = null!;
        public string? WordKey { get; set; }
        public string Word { get; set; } = null!;
        public string? Stem { get; set; }
        public int? Category { get; set; }
        public string? Translation { get; set; }
        public string? Timestamp { get; set; }
        public int? Frequency { get; set; }
        public int? Sync { get; set; }
        public int? ColorRgb { get; set; }
    }
}
