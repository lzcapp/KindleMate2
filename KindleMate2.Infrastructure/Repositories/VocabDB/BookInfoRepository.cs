using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class BookInfoRepository(string connectionString, VocabSourceSchema? schema = null) : IBookInfoRepository {
        // 源库(设备 vocab.db)的表/列由设备决定,可能缺表缺列。探测一次并缓存。
        private readonly VocabSourceSchema _schema = schema ?? VocabSourceSchema.Probe(connectionString);
        // 注意:逐表降级只覆盖 GetAll()(导入链路的唯一入口);GetById / GetCount 仍按固定 schema
        // 直查,畸形源库上会抛 —— 不要把它们当成"已加固"的读接口。
        public BookInfo? GetById(string id) {
            using var connection = new SqliteConnection(connectionString);
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
            return null;
        }

        public List<BookInfo> GetAll() {
            var results = new List<BookInfo>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 按源库**实际列**构造 SELECT:缺表/缺必需列 → 该表逐表跳过(返回空),不阻断其它表。
            // BOOK_INFO 被跳过时,查询记录的书名/作者只能是空 —— 属可接受的降级。
            var selectList = _schema.BuildSelectList(VocabSourceSchema.BookInfoTable,
                VocabSourceSchema.BookInfoColumns, VocabSourceSchema.BookInfoRequiredColumns);
            if (selectList is null) {
                return results;
            }
            var cmd = new SqliteCommand($"SELECT {selectList} FROM BOOK_INFO", connection);
        
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
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM BOOK_INFO", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(BookInfo bookInfo) {
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
        }

        public bool Update(BookInfo bookInfo) {
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
        }

        public bool Delete(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM BOOK_INFO WHERE id = @id", connection);
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
