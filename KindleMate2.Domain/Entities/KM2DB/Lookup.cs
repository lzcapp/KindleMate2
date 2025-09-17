namespace KindleMate2.Domain.Entities.KM2DB {
    public class Lookup {
        private string? _wordKey;
        private string? _word;

        public string? WordKey {
            get => _wordKey;
            set {
                _wordKey = value;
                if (value == null) {
                    return;
                }
                var index = value.IndexOf(':');
                Word = index >= 0 ? value[(index + 1)..] : value;
            }
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
            set => _word = value;
        }

        public string? Stem { get; set; }
        public string? Frequency { get; set; }
    }
}
