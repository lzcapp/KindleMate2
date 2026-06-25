namespace KindleMate2.Domain.Entities.VocabDB {
    public class MetaData {
        public required string Id { get; set; } = null!;
        public string? DsName { get; set; }
        public long? SsCnt { get; set; }
        public string? ProfileId { get; set; }
    }
}
