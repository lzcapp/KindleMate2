using System.Diagnostics.CodeAnalysis;

namespace KindleMate2.Entities {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    internal class Clipping {
        public string key { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
        public string bookname { get; set; } = string.Empty;
        public string authorname { get; set; } = string.Empty;
        public BriefType briefType { get; set; } = BriefType.Highlight;
        public string clippingtypelocation { get; set; } = string.Empty;
        public string clippingdate { get; set; } = string.Empty;
        public int read { get; set; }
        public string clipping_importdate { get; set; } = string.Empty;
        public string tag { get; set; } = string.Empty;
        public int sync { get; set; }
        public string newbookname { get; set; } = string.Empty;
        public int colorRGB { get; set; } = -1;
        public int pagenumber { get; set; }
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal enum BriefType {
        Hide = -1,
        Highlight = 0,
        Note = 1,
        Bookmark = 2,
        Cut = 3
    }
}