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
                        key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        content = DatabaseHelper.GetSafeString(reader, 1),
                        bookname = DatabaseHelper.GetSafeString(reader, 2),
                        authorname = DatabaseHelper.GetSafeString(reader, 3),
                        brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                        clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                        clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                        read = DatabaseHelper.GetSafeInt(reader, 7),
                        clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                        tag = DatabaseHelper.GetSafeString(reader, 9),
                        sync = DatabaseHelper.GetSafeInt(reader, 10),
                        newbookname = DatabaseHelper.GetSafeString(reader, 11),
                        colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                        pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
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
                        key = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        content = DatabaseHelper.GetSafeString(reader, 1),
                        bookname = DatabaseHelper.GetSafeString(reader, 2),
                        authorname = DatabaseHelper.GetSafeString(reader, 3),
                        brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                        clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                        clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                        read = DatabaseHelper.GetSafeInt(reader, 7),
                        clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                        tag = DatabaseHelper.GetSafeString(reader, 9),
                        sync = DatabaseHelper.GetSafeInt(reader, 10),
                        newbookname = DatabaseHelper.GetSafeString(reader, 11),
                        colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                        pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
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
                if (key == null) {
                    continue;
                }
                results.Add(new Clipping {
                    key = key,
                    content = DatabaseHelper.GetSafeString(reader, 1),
                    bookname = DatabaseHelper.GetSafeString(reader, 2),
                    authorname = DatabaseHelper.GetSafeString(reader, 3),
                    brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                    clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                    read = DatabaseHelper.GetSafeInt(reader, 7),
                    clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                    tag = DatabaseHelper.GetSafeString(reader, 9),
                    sync = DatabaseHelper.GetSafeInt(reader, 10),
                    newbookname = DatabaseHelper.GetSafeString(reader, 11),
                    colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                    pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
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
                if (key == null) {
                    continue;
                }
                results.Add(new Clipping {
                    key = key,
                    content = DatabaseHelper.GetSafeString(reader, 1),
                    bookname = DatabaseHelper.GetSafeString(reader, 2),
                    authorname = DatabaseHelper.GetSafeString(reader, 3),
                    brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                    clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                    read = DatabaseHelper.GetSafeInt(reader, 7),
                    clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                    tag = DatabaseHelper.GetSafeString(reader, 9),
                    sync = DatabaseHelper.GetSafeInt(reader, 10),
                    newbookname = DatabaseHelper.GetSafeString(reader, 11),
                    colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                    pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
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
                if (key == null) {
                    continue;
                }
                results.Add(new Clipping {
                    key = key,
                    content = DatabaseHelper.GetSafeString(reader, 1),
                    bookname = DatabaseHelper.GetSafeString(reader, 2),
                    authorname = DatabaseHelper.GetSafeString(reader, 3),
                    brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                    clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                    read = DatabaseHelper.GetSafeInt(reader, 7),
                    clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                    tag = DatabaseHelper.GetSafeString(reader, 9),
                    sync = DatabaseHelper.GetSafeInt(reader, 10),
                    newbookname = DatabaseHelper.GetSafeString(reader, 11),
                    colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                    pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
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
                if (key == null) {
                    continue;
                }
                results.Add(new Clipping {
                    key = key,
                    content = DatabaseHelper.GetSafeString(reader, 1),
                    bookname = DatabaseHelper.GetSafeString(reader, 2),
                    authorname = DatabaseHelper.GetSafeString(reader, 3),
                    brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                    clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                    read = DatabaseHelper.GetSafeInt(reader, 7),
                    clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                    tag = DatabaseHelper.GetSafeString(reader, 9),
                    sync = DatabaseHelper.GetSafeInt(reader, 10),
                    newbookname = DatabaseHelper.GetSafeString(reader, 11),
                    colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                    pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
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
                if (key == null) {
                    continue;
                }
                results.Add(new Clipping {
                    key = key,
                    content = DatabaseHelper.GetSafeString(reader, 1),
                    bookname = DatabaseHelper.GetSafeString(reader, 2),
                    authorname = DatabaseHelper.GetSafeString(reader, 3),
                    brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                    clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                    read = DatabaseHelper.GetSafeInt(reader, 7),
                    clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                    tag = DatabaseHelper.GetSafeString(reader, 9),
                    sync = DatabaseHelper.GetSafeInt(reader, 10),
                    newbookname = DatabaseHelper.GetSafeString(reader, 11),
                    colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                    pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
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
                if (key == null) {
                    continue;
                }
                results.Add(new Clipping {
                    key = key,
                    content = DatabaseHelper.GetSafeString(reader, 1),
                    bookname = DatabaseHelper.GetSafeString(reader, 2),
                    authorname = DatabaseHelper.GetSafeString(reader, 3),
                    brieftype = (BriefType)(DatabaseHelper.GetSafeInt(reader, 4) ?? 0),
                    clippingtypelocation = DatabaseHelper.GetSafeString(reader, 5),
                    clippingdate = DatabaseHelper.GetSafeString(reader, 6),
                    read = DatabaseHelper.GetSafeInt(reader, 7),
                    clipping_importdate = DatabaseHelper.GetSafeString(reader, 8),
                    tag = DatabaseHelper.GetSafeString(reader, 9),
                    sync = DatabaseHelper.GetSafeInt(reader, 10),
                    newbookname = DatabaseHelper.GetSafeString(reader, 11),
                    colorRGB = DatabaseHelper.GetSafeInt(reader, 12),
                    pagenumber = DatabaseHelper.GetSafeInt(reader, 13)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM clippings", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(Clipping clipping) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)",
                connection);
            cmd.Parameters.AddWithValue("@key", clipping.key);
            cmd.Parameters.AddWithValue("@content", clipping.content);
            cmd.Parameters.AddWithValue("@bookname", clipping.bookname);
            cmd.Parameters.AddWithValue("@authorname", clipping.authorname);
            cmd.Parameters.AddWithValue("@brieftype", clipping.brieftype);
            cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.clippingtypelocation);
            cmd.Parameters.AddWithValue("@clippingdate", clipping.clippingdate);
            cmd.Parameters.AddWithValue("@read", clipping.read);
            cmd.Parameters.AddWithValue("@clipping_importdate", clipping.clipping_importdate);
            cmd.Parameters.AddWithValue("@tag", clipping.tag);
            cmd.Parameters.AddWithValue("@sync", clipping.sync);
            cmd.Parameters.AddWithValue("@newbookname", clipping.newbookname);
            cmd.Parameters.AddWithValue("@colorRGB", clipping.colorRGB);
            cmd.Parameters.AddWithValue("@pagenumber", clipping.pagenumber);
            cmd.ExecuteNonQuery();
        }

        public bool Update(Clipping clipping) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "UPDATE clippings SET content = @content, bookname = @bookname, authorname = @authorname, brieftype = @brieftype, clippingtypelocation = @clippingtypelocation, clippingdate = @clippingdate, read = @read, clipping_importdate = @clipping_importdate, tag = @tag, sync = @sync, newbookname = @newbookname, colorRGB = @colorRGB, pagenumber = @pagenumber WHERE key = @key",
                connection);
            cmd.Parameters.AddWithValue("@key", clipping.key);
            cmd.Parameters.AddWithValue("@content", clipping.content);
            cmd.Parameters.AddWithValue("@bookname", clipping.bookname);
            cmd.Parameters.AddWithValue("@authorname", clipping.authorname);
            cmd.Parameters.AddWithValue("@brieftype", clipping.brieftype);
            cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.clippingtypelocation);
            cmd.Parameters.AddWithValue("@read", clipping.read);
            cmd.Parameters.AddWithValue("@clipping_importdate", clipping.clipping_importdate);
            cmd.Parameters.AddWithValue("@tag", clipping.tag);
            cmd.Parameters.AddWithValue("@sync", clipping.sync);
            cmd.Parameters.AddWithValue("@newbookname", clipping.newbookname);
            cmd.Parameters.AddWithValue("@colorRGB", clipping.colorRGB);
            cmd.Parameters.AddWithValue("@pagenumber", clipping.pagenumber);
            var result = cmd.ExecuteNonQuery() > 0;
            return result;
        }

        public void UpdateBriefTypeByKey(Clipping clipping) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE clippings SET brieftype = @brieftype WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", clipping.key);
            cmd.Parameters.AddWithValue("@brieftype", clipping.brieftype);
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