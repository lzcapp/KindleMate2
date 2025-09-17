namespace KindleMate2.Domain.Entities.KM2DB {
    public class Clipping {
        private string? _bookName;
        private long _colorRgb;
        private string? _content;
        private int _pageNumber;
        private int? _read;
        private int? _sync;
        public required string Key { get; set; } = null!;

        public string Content {
            get => _content ?? string.Empty;
            set => _content = value;
        }

        public string? BookName {
            get => _bookName;
            set => _bookName = value ?? string.Empty;
        }

        public string? AuthorName { get; set; }
        public long? BriefType { get; set; }
        public string? ClippingTypeLocation { get; set; }
        public string? ClippingDate { get; set; }

        public int? Read {
            get => _read;
            set => _read = value ?? 0;
        }

        public string? ClippingImportDate { get; set; }
        public string? Tag { get; set; }

        public int? Sync {
            get => _sync;
            set => _sync = value ?? 0;
        }

        public string? NewBookName { get; set; }

        public long? ColorRgb {
            get => _colorRgb;
            set => _colorRgb = value ?? -1;
        }

        public int? PageNumber {
            get => _pageNumber;
            set => _pageNumber = value ?? 0;
        }
    }

    public enum BriefType {
        Unknown = -2,
        Hide = -1,
        Highlight = 0,
        Note = 1,
        Bookmark = 2,
        Cut = 3
    }
}