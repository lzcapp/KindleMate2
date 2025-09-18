namespace KindleMate2.Domain.Entities.KM3DB {
    public class Word {
        public long Id { get; set; }
        public required string Name { get; set; }
        public required string Stem { get; set; }
        public string? Language { get; set; }
        public long Timestamp { get; set; }
    }
}