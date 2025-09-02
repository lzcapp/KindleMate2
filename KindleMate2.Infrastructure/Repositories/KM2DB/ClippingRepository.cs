using Microsoft.Data.Sqlite;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class ClippingRepository : IClippingRepository {
        private readonly string _connectionString;

        public ClippingRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Clipping? GetByKey(string key) {
            try {
                SqliteConnection connection = new(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE key = @key",
                    connection);
                cmd.Parameters.AddWithValue("@key", key);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new Clipping {
                        Key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    };
                }
                return null;
            } catch (Exception e) {
                Console.WriteLine(e);
                return null;
            }
        }

        public Clipping? GetByKeyAndContent(string key, string content) {
            try {
                SqliteConnection connection = new(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand(
                    "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE key = @key AND content = @content",
                    connection);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@content", content);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new Clipping {
                        Key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    };
                }
                return null;
            } catch (Exception ex) {
                Console.WriteLine(ex);
                return null;
            }
        }

        public List<Clipping> GetByBookNameAndPageNumber(string bookname, int pagenumber) {
            var results = new List<Clipping>();

            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE bookname = @bookname AND pagenumber = @pagenumber",
                connection);
            cmd.Parameters.AddWithValue("@bookname", bookname);
            cmd.Parameters.AddWithValue("@pagenumber", pagenumber);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = DatabaseHelper.GetSafeString(reader, 1),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByBookNameAndPageNumberAndBriefType(string bookname, int pagenumber, BriefType brieftype) {
            var results = new List<Clipping>();

            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE bookname = @bookname AND pagenumber = @pagenumber AND brieftype = @brieftype",
                connection);
            cmd.Parameters.AddWithValue("@bookname", bookname);
            cmd.Parameters.AddWithValue("@pagenumber", pagenumber);
            cmd.Parameters.AddWithValue("@brieftype", brieftype);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = DatabaseHelper.GetSafeString(reader, 1),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Clipping>();

            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var sql = type switch {
                AppEntities.SearchType.BookTitle => "WHERE bookname LIKE '%' || @strSearch || '%'",
                AppEntities.SearchType.Author => "WHERE authorname LIKE '%' || @strSearch || '%'",
                AppEntities.SearchType.Content => "WHERE content LIKE '%' || @strSearch || '%'",
                AppEntities.SearchType.All => "WHERE content LIKE '%' || @strSearch || '%' OR bookname LIKE '%' || @strSearch || '%' OR authorname LIKE '%' || @strSearch || '%'",
                _ => string.Empty
            };
            var query = "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings " + sql;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", search);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = DatabaseHelper.GetSafeString(reader, 1),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByContent(string content) {
            var results = new List<Clipping>();

            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE content = @content",
                connection);
            cmd.Parameters.AddWithValue("@content", content);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = DatabaseHelper.GetSafeString(reader, 1),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetAll() {
            var results = new List<Clipping>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = DatabaseHelper.GetSafeString(reader, 1),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public List<Clipping> GetByBookName(string bookname) {
            var results = new List<Clipping>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE bookname = @bookname", connection);
            cmd.Parameters.AddWithValue("@bookname", bookname);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var key = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                results.Add(new Clipping {
                    Key = key,
                    Content = DatabaseHelper.GetSafeString(reader, 1),
                    BookName = DatabaseHelper.GetSafeString(reader, 2),
                    AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                    BriefType = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                    ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                    Read = DatabaseHelper.GetSafeInt(reader, 7),
                    ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                    Tag = DatabaseHelper.GetSafeString(reader, 9),
                    Sync = DatabaseHelper.GetSafeInt(reader, 10),
                    NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                    ColorRgb = DatabaseHelper.GetSafeInt(reader, 12),
                    PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM clippings", connection);

            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public bool Add(Clipping clipping) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand(
                    "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)",
                    connection);
                cmd.Parameters.AddWithValue("@key", clipping.Key);
                cmd.Parameters.AddWithValue("@content", clipping.Content);
                cmd.Parameters.AddWithValue("@bookname", clipping.BookName);
                cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName);
                cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType);
                cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation);
                cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate);
                cmd.Parameters.AddWithValue("@read", clipping.Read);
                cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate);
                cmd.Parameters.AddWithValue("@tag", clipping.Tag);
                cmd.Parameters.AddWithValue("@sync", clipping.Sync);
                cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName);
                cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb);
                cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber);
                cmd.ExecuteNonQuery();
                return true;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(Clipping clipping) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "UPDATE clippings SET content = @content, bookname = @bookname, authorname = @authorname, brieftype = @brieftype, clippingtypelocation = @clippingtypelocation, clippingdate = @clippingdate, read = @read, clipping_importdate = @clipping_importdate, tag = @tag, sync = @sync, newbookname = @newbookname, colorRGB = @colorRGB, pagenumber = @pagenumber WHERE key = @key",
                connection);
            cmd.Parameters.AddWithValue("@key", clipping.Key);
            cmd.Parameters.AddWithValue("@content", clipping.Content);
            cmd.Parameters.AddWithValue("@bookname", clipping.BookName);
            cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName);
            cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType);
            cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation);
            cmd.Parameters.AddWithValue("@read", clipping.Read);
            cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate);
            cmd.Parameters.AddWithValue("@tag", clipping.Tag);
            cmd.Parameters.AddWithValue("@sync", clipping.Sync);
            cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName);
            cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb);
            cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber);
            var result = cmd.ExecuteNonQuery() > 0;
            return result;
        }

        public void UpdateBriefTypeByKey(Clipping clipping) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE clippings SET brieftype = @brieftype WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", clipping.Key);
            cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType);
            cmd.ExecuteNonQuery();
        }

        public bool Delete(string key) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM clippings WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", key);
            return cmd.ExecuteNonQuery() > 0;
        }

        public void DeleteAll() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM clippings", connection);
            cmd.ExecuteNonQuery();
        }
    }
}