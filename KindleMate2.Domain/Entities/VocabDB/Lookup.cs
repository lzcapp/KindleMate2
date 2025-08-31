namespace KindleMate2.Domain.Entities.VocabDB {
    public class Lookup {
        public required string id { get; set; } = null!;
        public string word_key { get; set; }
        public string book_key { get; set; }
        public string dict_key { get; set; }
        public string pos { get; set; }
        public string usage { get; set; }
        public int timestamp { get; set; } = 0;
    }
}
