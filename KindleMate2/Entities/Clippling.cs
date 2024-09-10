using System.Diagnostics.CodeAnalysis;

namespace KindleMate2.Entities {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Clipping {
        public string key { get; set; }
        public string content { get; set; }
        public string bookname { get; set; }
        public string authorname { get; set; }
        public int briefType { get; set; }
        public string clippingtypelocation { get; set; }
        public string clippingdate { get; set; }
        public int read { get; set; }
        public string clipping_importdate { get; set; }
        public string tag { get; set; }
        public int sync { get; set; }
        public string newbookname { get; set; }
        public int colorRGB { get; set; }
        public int pagenumber { get; set; }

        public Clipping(string key, string content, string bookName, string authorName, int briefType, string clippingTypeLocation, string clippingDate, int read, string clippingImportDate, string tag, int sync, string newBookName, int colorRGB, int pageNumber) {
            this.key = key;
            this.content = content;
            bookname = bookName;
            authorname = authorName;
            this.briefType = briefType;
            clippingtypelocation = clippingTypeLocation;
            clippingdate = clippingDate;
            this.read = read;
            clipping_importdate = clippingImportDate;
            this.tag = tag;
            this.sync = sync;
            newbookname = newBookName;
            this.colorRGB = colorRGB;
            pagenumber = pageNumber;
        }

        public Clipping() {
            key = string.Empty;
            content = string.Empty;
            bookname = string.Empty;
            authorname = string.Empty;
            briefType = 0;
            clippingtypelocation = string.Empty;
            clippingdate = string.Empty;
            read = 0;
            clipping_importdate = string.Empty;
            tag = string.Empty;
            sync = 0;
            newbookname = string.Empty;
            colorRGB = -1;
            pagenumber = 0;
        }
    }
}