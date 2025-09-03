namespace KindleMate2.Domain.Entities.VocabDB {
    public class Word {
        private long? _category = 0;
        private long? _timestamp = 0;
        public required string Id { get; set; } = null!;
        public string? word { get; set; }
        public string? Stem { get; set; }
        public string? Lang { get; set; }

        public long? Category {
            get => _category;
            set => _category = value ?? 0;
        }

        public long? Timestamp {
            get => _timestamp;
            set => _timestamp = value ?? 0;
        }

        public string? ProfileId { get; set; }
    }
}