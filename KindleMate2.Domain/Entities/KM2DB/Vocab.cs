namespace KindleMate2.Domain.Entities.KM2DB {
    public class Vocab {
        private long? _category;
        private int? _frequency;
        private int? _sync;
        private long? _colorRgb;
        public required string Id { get; set; } = null!;
        public string? WordKey { get; set; }
        public required string Word { get; set; } = null!;
        public string? Stem { get; set; }

        public long? Category {
            get => _category;
            set => _category = value ?? 0;
        }

        public string? Translation { get; set; }
        public string? Timestamp { get; set; }

        public int? Frequency {
            get => _frequency;
            set => _frequency = value ?? 0;
        }

        public int? Sync {
            get => _sync;
            set => _sync = value ?? 0;
        }

        public long? ColorRgb {
            get => _colorRgb;
            set => _colorRgb = value ?? -1;
        }
    }
}