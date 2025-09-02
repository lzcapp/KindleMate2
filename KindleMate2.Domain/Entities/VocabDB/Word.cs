namespace KindleMate2.Domain.Entities.VocabDB {
    public class Word {
        public required string Id { get; set; } = null!;
        public string? word { get; set; }
        public string? Stem { get; set; }
        public string? Lang { get; set; }
        public int? Category { get; set; } = 0;
        public int? Timestamp { get; set; } = 0;
        public string? Profileid { get; set; }
    }
}
