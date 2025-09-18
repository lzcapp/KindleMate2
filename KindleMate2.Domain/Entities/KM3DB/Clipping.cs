namespace KindleMate2.Domain.Entities.KM3DB {
    public class Clipping {
        public long Id { get; set; }
        public long BookId { get; set; }
        public required string Content { get; set; }
        public int PositionFrom { get; set; }
        public int PositionTo { get; set; }
        public int PositionPage { get; set; }
        public Type Type { get; set; }
        public long Timestamp { get; set; }
    }

    public enum Type {
        Unknown = -2,
        Hide = -1,
        Highlight = 0,
        Note = 1,
        Bookmark = 2,
        Cut = 3
    }
}