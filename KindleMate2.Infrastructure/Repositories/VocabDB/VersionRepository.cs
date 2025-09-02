using KindleMate2.Domain.Interfaces.VocabDB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    using Version = Domain.Entities.VocabDB.Version;

    public class VersionRepository : IVersionRepository {
        private readonly string _connectionString;

        public VersionRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public Version? GetById(string id) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, dsname, value FROM VERSION WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Version {
                    Id = reader.GetString(0),
                    Dsname = reader.GetString(1),
                    Value = reader.GetInt32(2)
                };
            }
            return null;
        }

        public List<Version> GetAll() {
            var results = new List<Version>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, dsname, value FROM VERSION", connection);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new Version {
                    Id = reader.GetString(0),
                    Dsname = reader.GetString(1),
                    Value = reader.GetInt32(2)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM VERSION", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(Version Version) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO VERSION (id, dsname, value) VALUES (@id, @dsname, @value)", connection);
            cmd.Parameters.AddWithValue("@id", Version.Id);
            cmd.Parameters.AddWithValue("@dsname", Version.Dsname);
            cmd.Parameters.AddWithValue("@value", Version.Value);
            cmd.ExecuteNonQuery();
        }

        public void Update(Version Version) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE VERSION SET dsname = @dsname, value = @value WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", Version.Id);
            cmd.Parameters.AddWithValue("@dsname", Version.Dsname);
            cmd.Parameters.AddWithValue("@value", Version.Value);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM VERSION WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
