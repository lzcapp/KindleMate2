using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class BookInfoRepository(string connectionString) : IBookInfoRepository {
        public BookInfo? GetById(string id) {
            try {
                SqliteConnection connection = new(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, asin, guid, lang, title, authors FROM BOOK_INFO WHERE id = @id", connection);
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new BookInfo {
                        Id = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        Asin = DatabaseHelper.GetSafeString(reader, 1),
                        Guid = DatabaseHelper.GetSafeString(reader, 2),
                        Lang = DatabaseHelper.GetSafeString(reader, 3),
                        Title = DatabaseHelper.GetSafeString(reader, 4),
                        Authors = DatabaseHelper.GetSafeString(reader, 5)
                    };
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return null;
        }

        public List<BookInfo> GetAll() {
            var results = new List<BookInfo>();

            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, asin, guid, lang, title, authors FROM BOOK_INFO", connection);
            
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var id = DatabaseHelper.GetSafeString(reader, 0);
                    if (string.IsNullOrWhiteSpace(id)) {
                        continue;
                    }
                    results.Add(new BookInfo {
                        Id = id,
                        Asin = DatabaseHelper.GetSafeString(reader, 1),
                        Guid = DatabaseHelper.GetSafeString(reader, 2),
                        Lang = DatabaseHelper.GetSafeString(reader, 3),
                        Title = DatabaseHelper.GetSafeString(reader, 4),
                        Authors = DatabaseHelper.GetSafeString(reader, 5)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public int GetCount() {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM BOOK_INFO", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(BookInfo bookInfo) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO BOOK_INFO (id, asin, guid, lang, title, authors) VALUES (@id, @asin, @guid, @lang, @title, @authors)", connection);
                cmd.Parameters.AddWithValue("@id", bookInfo.Id ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@asin", bookInfo.Asin ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@guid", bookInfo.Guid ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lang", bookInfo.Lang ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@title", bookInfo.Title ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@authors", bookInfo.Authors ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(BookInfo bookInfo) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE BOOK_INFO SET asin = @asin, guid = @guid, lang = @lang, title = @title, authors = @authors WHERE id = @id", connection);
                cmd.Parameters.AddWithValue("@id", bookInfo.Id ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@asin", bookInfo.Asin ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@guid", bookInfo.Guid ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lang", bookInfo.Lang ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@title", bookInfo.Title ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@authors", bookInfo.Authors ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Delete(string id) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM BOOK_INFO WHERE id = @id", connection);
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}
