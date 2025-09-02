namespace KindleMate2.Domain.Entities.VocabDB {
    public class DictInfo {
        public required string Id { get; set; } = null!;
        public string? Asin { get; set; }
        public string? Langin { get; set; }
        public string? Langout { get; set; }
    }
}
