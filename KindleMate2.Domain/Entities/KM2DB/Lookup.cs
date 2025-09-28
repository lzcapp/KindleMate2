namespace KindleMate2.Domain.Entities.KM2DB {
    public class Lookup {
        private string? _wordKey;

        public string? WordKey {
            get => _wordKey;
            set => _wordKey = value;
        }

        public string? Usage { get; set; }
        public string? Title { get; set; }
        public string? Authors { get; set; }
        public string? Timestamp { get; set; }

        public string Word {
            get {
                if (_wordKey == null) {
                    return string.Empty;
                }
                var index = _wordKey.IndexOf(':');
                return index >= 0 ? _wordKey[(index + 1)..] : _wordKey;
            }
        }

        public string? Stem { get; set; }
        public string? Frequency { get; set; }
    }
}
