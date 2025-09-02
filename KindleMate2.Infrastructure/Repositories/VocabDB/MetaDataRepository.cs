using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class MetaDataRepository : IMetaDataRepository {
        private readonly string _connectionString;

        public MetaDataRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public MetaData? GetById(string id) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, dsname, sscnt, profileid FROM METADATA WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new MetaData {
                    Id = reader.GetString(0),
                    Dsname = reader.GetString(1),
                    Sscnt = reader.GetInt32(2),
                    Profileid = reader.GetString(3)
                };
            }
            return null;
        }

        public List<MetaData> GetAll() {
            var results = new List<MetaData>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, dsname, sscnt, profileid FROM METADATA", connection);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new MetaData {
                    Id = reader.GetString(0),
                    Dsname = reader.GetString(1),
                    Sscnt = reader.GetInt32(2),
                    Profileid = reader.GetString(3)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM METADATA", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(MetaData MetaData) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO METADATA (id, dsname, sscnt, profileid) VALUES (@id, @dsname, @sscnt, @profileid)", connection);
            cmd.Parameters.AddWithValue("@id", MetaData.Id);
            cmd.Parameters.AddWithValue("@dsname", MetaData.Dsname);
            cmd.Parameters.AddWithValue("@sscnt", MetaData.Sscnt);
            cmd.Parameters.AddWithValue("@profileid", MetaData.Profileid);
            cmd.ExecuteNonQuery();
        }

        public void Update(MetaData MetaData) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE METADATA SET dsname = @dsname, sscnt = @sscnt, profileid = @profileid WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", MetaData.Id);
            cmd.Parameters.AddWithValue("@dsname", MetaData.Dsname);
            cmd.Parameters.AddWithValue("@sscnt", MetaData.Sscnt);
            cmd.Parameters.AddWithValue("@profileid", MetaData.Profileid);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM METADATA WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
