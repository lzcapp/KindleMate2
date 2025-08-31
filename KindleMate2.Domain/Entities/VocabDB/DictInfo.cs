namespace KindleMate2.Domain.Entities.VocabDB {
    public class DictInfo {
        public required string id { get; set; } = null!;
        public string asin { get; set; }
        public string langin { get; set; }
        public string langout { get; set; }
    }
}
