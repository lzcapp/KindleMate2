namespace KindleMate2.Domain.Entities.VocabDB {
    public class Lookup {
        private long _timestamp;
        public required string Id { get; set; } = null!;
        public string? WordKey { get; set; }
        public string? BookKey { get; set; }
        public string? DictKey { get; set; }
        public string? Pos { get; set; }
        public string? Usage { get; set; }

        public long? Timestamp {
            get => _timestamp;
            set => _timestamp = value ?? 0;
        }
    }
}
