namespace KindleMate2.Domain.Entities.KM2DB {
    public class OriginalClippingLine {
        private string? _line5 = "==========";
        private string? _line3;

        public required string key { get; set; } = null!;
        public string? line1 { get; set; }
        public string? line2 { get; set; }

        public string? line3 {
            get => _line3 ?? "";
            set => _line3 = value ?? "";
        }

        public string? line4 { get; set; }

        public string line5 {
            get => _line5 ?? "==========";
            set => _line5 = value;
        }
    }
}