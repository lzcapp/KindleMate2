namespace KindleMate2.Domain.Entities.MyClippings {
    public class MyClipping {
        private string _delimiter = "==========";
        public string? Header { get; set; } = string.Empty;
        public string? Metadata { get; set; } = string.Empty;
        public string? Content { get; set; } = string.Empty;

        public string Delimiter {
            get => _delimiter ?? "==========";
            set => _delimiter = value;
        }
    }
}