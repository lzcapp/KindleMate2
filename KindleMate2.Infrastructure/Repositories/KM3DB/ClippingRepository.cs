using KindleMate2.Domain.Entities.KM3DB;
using KindleMate2.Domain.Interfaces.KM3DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;
using Type = KindleMate2.Domain.Entities.KM3DB.Type;

namespace KindleMate2.Infrastructure.Repositories.KM3DB {
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
                if (!string.IsNullOrWhiteSpace(key)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@key", key);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new Clipping {
                        Id = DatabaseHelper.GetLong(reader, 0),
                        Content = DatabaseHelper.GetSafeString(reader, 1) ?? throw new InvalidOperationException()
                    };
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return null;
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
                        Content = DatabaseHelper.GetString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    };
                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
            return null;
        }

        public List<Clipping> GetByBookNameAndPageNumber(string bookname, int pagenumber) {
            var results = new List<Clipping>();
            try {
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
                        Key = key,
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public List<Clipping> GetByBookNameAndPageNumberAndBriefType(string bookname, int pagenumber, Type brieftype) {
            var results = new List<Clipping>();
            try {
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
                        Key = key,
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Clipping>();
            try {
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
                        Key = key,
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public List<Clipping> GetByContent(string content) {
            var results = new List<Clipping>();
            try {
                SqliteConnection connection = new(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand(
                    "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE content = @content", connection);
                cmd.Parameters.AddWithValue("@content", content);

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var key = DatabaseHelper.GetSafeString(reader, 0);
                    if (key == null) {
                        continue;
                    }
                    results.Add(new Clipping {
                        Key = key,
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = DatabaseHelper.GetSafeLong(reader, 4) ?? 0,
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public List<Clipping> GetAll() {
            var results = new List<Clipping>();
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings ORDER BY bookname, pagenumber, clippingdate", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var key = DatabaseHelper.GetSafeString(reader, 0);
                    if (key == null) {
                        continue;
                    }
                    results.Add(new Clipping {
                        Key = key,
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = DatabaseHelper.GetSafeLong(reader, 4) ?? 0,
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public List<Clipping> GetByBookName(string bookname) {
            var results = new List<Clipping>();
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand(
                    "SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE bookname = @bookname", connection);
                cmd.Parameters.AddWithValue("@bookname", bookname);

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var key = DatabaseHelper.GetSafeString(reader, 0);
                    if (key == null) {
                        continue;
                    }
                    results.Add(new Clipping {
                        Key = key,
                        Content = DatabaseHelper.GetSafeString(reader, 1),
                        BookName = DatabaseHelper.GetSafeString(reader, 2),
                        AuthorName = DatabaseHelper.GetSafeString(reader, 3),
                        BriefType = DatabaseHelper.GetSafeLong(reader, 4),
                        ClippingTypeLocation = DatabaseHelper.GetSafeString(reader, 5),
                        ClippingDate = DatabaseHelper.GetSafeString(reader, 6),
                        Read = DatabaseHelper.GetSafeInt(reader, 7),
                        ClippingImportDate = DatabaseHelper.GetSafeString(reader, 8),
                        Tag = DatabaseHelper.GetSafeString(reader, 9),
                        Sync = DatabaseHelper.GetSafeInt(reader, 10),
                        NewBookName = DatabaseHelper.GetSafeString(reader, 11),
                        ColorRgb = DatabaseHelper.GetSafeLong(reader, 12),
                        PageNumber = DatabaseHelper.GetSafeInt(reader, 13)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public List<string> GetBookNamesList() {
            var results = new List<string>();
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT DISTINCT bookname FROM clippings", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var bookName = DatabaseHelper.GetSafeString(reader, 0);
                    if (string.IsNullOrWhiteSpace(bookName)) {
                        continue;
                    }
                    results.Add(bookName);
                }
                results = results.OrderBy(x => x).ToList();
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public int GetCount() {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM clippings", connection);
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(Clipping clipping) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand(
                    "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)",
                    connection);
                cmd.Parameters.AddWithValue("@key", clipping.Key ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@content", clipping.Content ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@bookname", clipping.BookName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@read", clipping.Read ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@tag", clipping.Tag ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@sync", clipping.Sync ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public int Add(List<Clipping> listClippings) {
            var count = 0;
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                foreach (Clipping clipping in listClippings) {
                    var cmd = new SqliteCommand(
                        "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)",
                        connection);
                    cmd.Parameters.AddWithValue("@key", clipping.Key ?? throw new InvalidOperationException());
                    cmd.Parameters.AddWithValue("@content", clipping.Content ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@bookname", clipping.BookName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@read", clipping.Read ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@tag", clipping.Tag ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@sync", clipping.Sync ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber ?? (object)DBNull.Value);
                    if (cmd.ExecuteNonQuery() > 0) {
                        count++;
                    }
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return count;
        }

        public bool Update(Clipping clipping) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand(
                    "UPDATE clippings SET content = @content, bookname = @bookname, authorname = @authorname, brieftype = @brieftype, clippingtypelocation = @clippingtypelocation, clippingdate = @clippingdate, read = @read, clipping_importdate = @clipping_importdate, tag = @tag, sync = @sync, newbookname = @newbookname, colorRGB = @colorRGB, pagenumber = @pagenumber WHERE key = @key",
                    connection);
                cmd.Parameters.AddWithValue("@key", clipping.Key ?? throw new InvalidOperationException());
                cmd.Parameters.AddWithValue("@content", clipping.Content ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@bookname", clipping.BookName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@read", clipping.Read ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@tag", clipping.Tag ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@sync", clipping.Sync ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(Update), e));
                return false;
            }
        }

        public bool UpdateBriefTypeByKey(Clipping clipping) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE clippings SET brieftype = @brieftype WHERE key = @key", connection);
                cmd.Parameters.AddWithValue("@key", clipping.Key);
                cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Delete(string key) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM clippings WHERE key = @key", connection);
                if (string.IsNullOrWhiteSpace(key)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@key", key);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public int Delete(List<Clipping> listClippings) {
            var count = 0;
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                foreach (Clipping clipping in listClippings) {
                    var cmd = new SqliteCommand("DELETE FROM clippings WHERE key = @key", connection);
                    var key = clipping.Key;
                    if (string.IsNullOrWhiteSpace(key)) {
                        throw new InvalidOperationException();
                    }
                    cmd.Parameters.AddWithValue("@key", key);
                    if (cmd.ExecuteNonQuery() > 0) {
                        count++;
                    }
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return count;
        }

        public bool DeleteAll() {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM clippings", connection);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}