namespace KindleMate2.Domain.Entities.VocabDB {
    public class Version {
        public required string Id { get; set; } = null!;
        public string? DsName { get; set; }
        public long? Value { get; set; }
    }
}
