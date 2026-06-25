namespace KindleMate2.Domain.Entities.KM2DB {
    public class OriginalClippingLine {
        private string? _line4;
        private string? _line5 = "==========";
        private string? _line3;
        private string? _line2;
        private string? _line1;
        public required string Key { get; set; } = null!;

        public string? Line1 {
            get => _line1;
            set => _line1 = value ?? string.Empty;
        }

        public string? Line2 {
            get => _line2;
            set => _line2 = value ?? string.Empty;
        }

        public string? Line3 {
            get => _line3;
            set => _line3 = value ?? string.Empty;
        }

        public string? Line4 {
            get => _line4;
            set => _line4 = value ?? string.Empty;
        }

        public string? Line5 {
            get => _line5;
            set => _line5 = value ?? "==========";
        }
    }
}
