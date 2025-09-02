using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class BookInfoRepository : IBookInfoRepository {
        private readonly string _connectionString;

        public BookInfoRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public BookInfo? GetById(string id) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, asin, guid, lang, title, authors FROM BOOK_INFO WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new BookInfo {
                    Id = reader.GetString(0),
                    Asin = reader.GetString(1),
                    Guid = reader.GetString(2),
                    Lang = reader.GetString(3),
                    Title = reader.GetString(4),
                    Authors = reader.GetString(5)
                };
            }
            return null;
        }

        public List<BookInfo> GetAll() {
            var results = new List<BookInfo>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, asin, guid, lang, title, authors FROM BOOK_INFO", connection);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new BookInfo {
                    Id = reader.GetString(0),
                    Asin = reader.GetString(1),
                    Guid = reader.GetString(2),
                    Lang = reader.GetString(3),
                    Title = reader.GetString(4),
                    Authors = reader.GetString(5)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM BOOK_INFO", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(BookInfo bookInfo) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO BOOK_INFO (id, asin, guid, lang, title, authors) VALUES (@id, @asin, @guid, @lang, @title, @authors)", connection);
            cmd.Parameters.AddWithValue("@id", bookInfo.Id);
            cmd.Parameters.AddWithValue("@asin", bookInfo.Asin);
            cmd.Parameters.AddWithValue("@guid", bookInfo.Guid);
            cmd.Parameters.AddWithValue("@lang", bookInfo.Lang);
            cmd.Parameters.AddWithValue("@title", bookInfo.Title);
            cmd.Parameters.AddWithValue("@authors", bookInfo.Authors);
            cmd.ExecuteNonQuery();
        }

        public void Update(BookInfo bookInfo) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE BOOK_INFO SET asin = @asin, guid = @guid, lang = @lang, title = @title, authors = @authors WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", bookInfo.Id);
            cmd.Parameters.AddWithValue("@asin", bookInfo.Asin);
            cmd.Parameters.AddWithValue("@guid", bookInfo.Guid);
            cmd.Parameters.AddWithValue("@lang", bookInfo.Lang);
            cmd.Parameters.AddWithValue("@title", bookInfo.Title);
            cmd.Parameters.AddWithValue("@authors", bookInfo.Authors);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM BOOK_INFO WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
