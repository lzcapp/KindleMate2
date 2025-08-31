namespace KindleMate2.Domain.Entities.MyClippings {
    public class MyClipping {
        public string Header { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Delimiter { get; set; } = "==========";
    }
}