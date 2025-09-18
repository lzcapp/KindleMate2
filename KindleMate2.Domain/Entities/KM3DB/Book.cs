namespace KindleMate2.Domain.Entities.KM3DB {
    public class Book {
        public long Id { get; set; }
        public required string Title { get; set; }
        public string? Authors { get; set; }
        public long Timestamp { get; set; }
    }
}