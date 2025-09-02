namespace KindleMate2.Domain.Entities.KM2DB {
    public class Setting {
        private string? _value;
        public required string name { get; set; }

        public string? value {
            get => _value;
            set => _value = value ?? string.Empty;
        }
    }
}
