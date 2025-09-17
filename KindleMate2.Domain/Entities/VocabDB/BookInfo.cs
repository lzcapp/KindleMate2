namespace KindleMate2.Domain.Entities.VocabDB {
    public class BookInfo {
        public required string Id { get; set; } = null!;
        public string? Asin { get; set; }
        public string? Guid { get; set; }
        public string? Lang { get; set; }
        public string? Title { get; set; }
        public string? Authors { get; set; }
    }
}
