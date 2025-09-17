namespace KindleMate2.Domain.Entities.VocabDB {
    public class MetaData {
        public required string Id { get; set; } = null!;
        public string? Dsname { get; set; }
        public long? Sscnt { get; set; }
        public string? Profileid { get; set; }
    }
}
