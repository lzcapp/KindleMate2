namespace KindleMate2.Domain.Entities.KM2DB {
    public class Clipping {
        public string key { get; set; } = null!;
        public string content { get; set; }
        public string bookname { get; set; }
        public string authorname { get; set; }
        public BriefType brieftype { get; set; }
        public string clippingtypelocation { get; set; }
        public string clippingdate { get; set; }
        public int read { get; set; }
        public string clipping_importdate { get; set; }
        public string tag { get; set; }
        public int sync { get; set; }
        public string newbookname { get; set; }
        public int colorRGB { get; set; }
        public int pagenumber { get; set; }
    }

    public enum BriefType {
        Hide = -1,
        Highlight = 0,
        Note = 1,
        Bookmark = 2,
        Cut = 3
    }

    public enum SearchType {
        BookTitle,
        Author,
        Content,
        All
    }
}
