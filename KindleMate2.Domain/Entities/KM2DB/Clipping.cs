namespace KindleMate2.Domain.Entities.KM2DB {
    public class Clipping {
        public string Key { get; set; } = null!;
        public string? Content { get; set; }
        public string? BookName { get; set; }
        public string? AuthorName { get; set; }
        public BriefType? BriefType { get; set; }
        public string? ClippingTypeLocation { get; set; }
        public string? ClippingDate { get; set; }
        public int? Read { get; set; }
        public string? ClippingImportDate { get; set; }
        public string? Tag { get; set; }
        public int? Sync { get; set; }
        public string? NewBookName { get; set; }
        public int? ColorRgb { get; set; }
        public int? PageNumber { get; set; }
    }

    public enum BriefType {
        Hide = -1,
        Highlight = 0,
        Note = 1,
        Bookmark = 2,
        Cut = 3
    }
}
