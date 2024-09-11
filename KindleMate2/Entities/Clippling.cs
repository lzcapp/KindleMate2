using System.Diagnostics.CodeAnalysis;

namespace KindleMate2.Entities {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Clipping {
        public string key { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
        public string bookname { get; set; } = string.Empty;
        public string authorname { get; set; } = string.Empty;
        public int briefType { get; set; } = 0;
        public string clippingtypelocation { get; set; } = string.Empty;
        public string clippingdate { get; set; } = string.Empty;
        public int read { get; set; } = 0;
        public string clipping_importdate { get; set; } = string.Empty;
        public string tag { get; set; } = string.Empty;
        public int sync { get; set; } = 0;
        public string newbookname { get; set; } = string.Empty;
        public int colorRGB { get; set; } = -1;
        public int pagenumber { get; set; } = 0;
    }
}