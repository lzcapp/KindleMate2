using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;
using Version = KindleMate2.Domain.Entities.VocabDB.Version;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    using Version = Version;

    public class VersionRepository : IVersionRepository {
        private readonly string _connectionString;

        public VersionRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Version? GetById(string id) {
            try {
                SqliteConnection connection = new(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, dsname, value FROM VERSION WHERE id = @id", connection);
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new Version {
                        Id = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        DsName = DatabaseHelper.GetSafeString(reader, 1),
                        Value = DatabaseHelper.GetSafeLong(reader, 2)
                    };
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return null;
        }

        public List<Version> GetAll() {
            var results = new List<Version>();

            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, dsname, value FROM VERSION", connection);
            
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var id = DatabaseHelper.GetSafeString(reader, 0);
                    if (string.IsNullOrWhiteSpace(id)) {
                        continue;
                    }
                    results.Add(new Version {
                        Id = id,
                        DsName = DatabaseHelper.GetSafeString(reader, 1),
                        Value = DatabaseHelper.GetSafeLong(reader, 2)
                    });
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return results;
        }

        public int GetCount() {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM VERSION", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(Version version) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO VERSION (id, dsname, value) VALUES (@id, @dsname, @value)", connection);
                var id = version.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@dsname", version.DsName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@value", version.Value ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(Version version) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE VERSION SET dsname = @dsname, value = @value WHERE id = @id", connection);
                var id = version.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@dsname", version.DsName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@value", version.Value ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Delete(string id) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("DELETE FROM VERSION WHERE id = @id", connection);
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
