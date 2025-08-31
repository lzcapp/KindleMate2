using Microsoft.Data.Sqlite;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class ClippingRepository : IClippingRepository {
        private readonly string _connectionString;

        public ClippingRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Clipping? GetByKey(string key) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", key);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Clipping {
                    key = reader.GetString(0),
                    content = reader.GetString(1),
                    bookname = reader.GetString(2),
                    authorname = reader.GetString(3),
                    brieftype = reader.GetInt32(4),
                    clippingtypelocation = reader.GetString(5),
                    clippingdate = reader.GetString(6),
                    read = reader.GetInt32(7),
                    clipping_importdate = reader.GetString(8),
                    tag = reader.GetString(9),
                    sync = reader.GetInt32(10),
                    newbookname = reader.GetString(11),
                    colorRGB = reader.GetInt32(12),
                    pagenumber = reader.GetInt32(13)
                };
            }
            return null;
        }

        public Clipping? GetByKeyAndContent(string key, string content) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings WHERE key = @key AND content = @content", connection);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@content", content);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Clipping {
                    key = reader.GetString(0),
                    content = reader.GetString(1),
                    bookname = reader.GetString(2),
                    authorname = reader.GetString(3),
                    brieftype = reader.GetInt32(4),
                    clippingtypelocation = reader.GetString(5),
                    clippingdate = reader.GetString(6),
                    read = reader.GetInt32(7),
                    clipping_importdate = reader.GetString(8),
                    tag = reader.GetString(9),
                    sync = reader.GetInt32(10),
                    newbookname = reader.GetString(11),
                    colorRGB = reader.GetInt32(12),
                    pagenumber = reader.GetInt32(13)
                };
            }
            return null;
        }

        public List<Clipping> GetAll() {
            var results = new List<Clipping>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber FROM clippings", connection);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Clipping {
                    key = reader.GetString(0),
                    content = reader.GetString(1),
                    bookname = reader.GetString(2),
                    authorname = reader.GetString(3),
                    brieftype = reader.GetInt32(4),
                    clippingtypelocation = reader.GetString(5),
                    clippingdate = reader.GetString(6),
                    read = reader.GetInt32(7),
                    clipping_importdate = reader.GetString(8),
                    tag = reader.GetString(9),
                    sync = reader.GetInt32(10),
                    newbookname = reader.GetString(11),
                    colorRGB = reader.GetInt32(12),
                    pagenumber = reader.GetInt32(13)
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

            var cmd = new SqliteCommand("INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)", connection);
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

        public void Update(Clipping clipping) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE clippings SET content = @content, bookname = @bookname, authorname = @authorname, brieftype = @brieftype, clippingtypelocation = @clippingtypelocation, clippingdate = @clippingdate, read = @read, clipping_importdate = @clipping_importdate, tag = @tag, sync = @sync, newbookname = @newbookname, colorRGB = @colorRGB, pagenumber = @pagenumber WHERE key = @key", connection);
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
            cmd.ExecuteNonQuery();
        }

        public void Delete(string key) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM clippings WHERE key = @key", connection);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.ExecuteNonQuery();
        }
    }
}
