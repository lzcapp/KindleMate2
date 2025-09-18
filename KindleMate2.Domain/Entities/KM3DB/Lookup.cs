namespace KindleMate2.Domain.Entities.KM3DB {
    public class Lookup {
        public long Id { get; set; }
        public long BookId { get; set; }
        public long WordId { get; set; }
        public required string Usage { get; set; }
        public int Position { get; set; }
        public long Timestamp { get; set; }
    }
}
