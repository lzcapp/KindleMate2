using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Entities.MyClippings {
    public class MyClipping {
        public string Header { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Delimiter { get; set; } = "==========";
    }
    
    public class Header {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
    }
    
    public class Metadata {
        public BriefType Type { get; set; }
        public int Page { get; set; } = -1;
        public Location Location { get; set; }
        public DateTime? DateOfCreation { get; set; }
    }
    
    public class Location {
        public int From { get; set; } = -1;
        public int To { get; set; } = -1;
        public int Page { get; set; } = -1;
    }
}