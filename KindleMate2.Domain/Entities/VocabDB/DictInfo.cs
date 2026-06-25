namespace KindleMate2.Domain.Entities.VocabDB {
    public class DictInfo {
        public required string Id { get; set; } = null!;
        public string? Asin { get; set; }
        public string? LangIn { get; set; }
        public string? LangOut { get; set; }
    }
}
