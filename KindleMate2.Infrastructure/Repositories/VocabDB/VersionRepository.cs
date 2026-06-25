using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;
using Version = KindleMate2.Domain.Entities.VocabDB.Version;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class VersionRepository(string connectionString) : IVersionRepository {
        public Version? GetById(string id) {
            using var connection = new SqliteConnection(connectionString);
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
            return null;
        }

        public List<Version> GetAll() {
            var results = new List<Version>();

            using var connection = new SqliteConnection(connectionString);
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
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM VERSION", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Version version) {
            using var connection = new SqliteConnection(connectionString);
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
        }

        public bool Update(Version version) {
            using var connection = new SqliteConnection(connectionString);
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
        }

        public bool Delete(string id) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM VERSION WHERE id = @id", connection);
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
