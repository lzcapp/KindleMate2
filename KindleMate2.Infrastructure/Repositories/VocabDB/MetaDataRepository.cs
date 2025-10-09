using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class MetaDataRepository(string connectionString) : IMetaDataRepository {
        public MetaData? GetById(string id) {
            try {
                SqliteConnection connection = new(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, dsname, sscnt, profileid FROM METADATA WHERE id = @id", connection);
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new MetaData {
                        Id = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        Dsname = DatabaseHelper.GetSafeString(reader, 1),
                        Sscnt = DatabaseHelper.GetSafeLong(reader, 2),
                        Profileid = DatabaseHelper.GetSafeString(reader, 3)
                    };
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return null;
        }

        public List<MetaData> GetAll() {
            var results = new List<MetaData>();
            
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, dsname, sscnt, profileid FROM METADATA", connection);
            
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var id = DatabaseHelper.GetSafeString(reader, 0);
                    if (string.IsNullOrWhiteSpace(id)) {
                        continue;
                    }
                    results.Add(new MetaData {
                        Id = id,
                        Dsname = DatabaseHelper.GetSafeString(reader, 1),
                        Sscnt = DatabaseHelper.GetSafeLong(reader, 2),
                        Profileid = DatabaseHelper.GetSafeString(reader, 3)
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

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM METADATA", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(MetaData metaData) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO METADATA (id, dsname, sscnt, profileid) VALUES (@id, @dsname, @sscnt, @profileid)", connection);
                var id = metaData.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@dsname", metaData.Dsname ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@sscnt", metaData.Sscnt ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@profileid", metaData.Profileid ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(MetaData metaData) {
            try {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE METADATA SET dsname = @dsname, sscnt = @sscnt, profileid = @profileid WHERE id = @id", connection);
                var id = metaData.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@dsname", metaData.Dsname ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@sscnt", metaData.Sscnt ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@profileid", metaData.Profileid ?? (object)DBNull.Value);
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

                var cmd = new SqliteCommand("DELETE FROM METADATA WHERE id = @id", connection);
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
