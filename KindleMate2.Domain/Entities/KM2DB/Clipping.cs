namespace KindleMate2.Domain.Entities.KM2DB {
    public class Clipping {
        public required string Key { get; set; }
        public required string Content { get; set; }
        public required string BookName { get; set; }
        public required string AuthorName { get; set; }
        public BriefType BriefType { get; set; }
        public required string ClippingTypeLocation { get; set; }
        public required string ClippingDate { get; set; }
        public int? Read { get; set; }
        public required string ClippingImportDate { get; set; }
        public required string Tag { get; set; }
        public int? Sync { get; set; }
        public required string NewBookName { get; set; }
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
