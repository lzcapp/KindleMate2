namespace KindleMate2.Domain.Entities.KM2DB {
    public class Setting {
        private string? _value;
        public required string Name { get; set; }

        public string? value {
            get => _value;
            set => _value = value ?? string.Empty;
        }
    }
}
