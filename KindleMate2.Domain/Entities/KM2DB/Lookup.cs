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

        /// <summary>软删除时间(NULL=活跃)。与 vocab 同理:删掉的查询记录留在表里占住
        /// (word_key, timestamp) 唯一键,重新导入同一份库时不会被插回来。</summary>
        public string? DeletedAt { get; set; }

        /// <summary>来自哪次导入(import_batches.id;NULL=旧数据/来源不可考)。</summary>
        public long? OriginBatch { get; set; }
    }
}
