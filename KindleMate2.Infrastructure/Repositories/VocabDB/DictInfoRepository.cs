using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Domain.Entities.VocabDB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class DictInfoRepository : IDictInfoRepository {
        private readonly string _connectionString;

        public DictInfoRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public DictInfo? GetById(string id) {
            SqliteConnection connection = new(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, asin, langin, langout FROM DICT_INFO WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new DictInfo {
                    Id = reader.GetString(0),
                    Asin = reader.GetString(1),
                    Langin = reader.GetString(2),
                    Langout = reader.GetString(3)
                };
            }
            return null;
        }

        public List<DictInfo> GetAll() {
            var results = new List<DictInfo>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT id, asin, langin, langout FROM DICT_INFO", connection);
            
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new DictInfo {
                    Id = reader.GetString(0),
                    Asin = reader.GetString(1),
                    Langin = reader.GetString(2),
                    Langout = reader.GetString(3)
                });
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM DICT_INFO", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            var result = cmd.ExecuteScalar();

            // ExecuteScalar returns object, so convert to int
            return Convert.ToInt32(result);
        }

        public void Add(DictInfo dictInfo) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO DICT_INFO (id, asin, langin, langout) VALUES (@id, @asin, @langin, @langout)", connection);
            cmd.Parameters.AddWithValue("@id", dictInfo.Id);
            cmd.Parameters.AddWithValue("@asin", dictInfo.Asin);
            cmd.Parameters.AddWithValue("@langin", dictInfo.Langin);
            cmd.Parameters.AddWithValue("@langout", dictInfo.Langout);
            cmd.ExecuteNonQuery();
        }

        public void Update(DictInfo DictInfo) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("UPDATE DICT_INFO SET asin = @asin, langin = @langin, langout = @langout WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", DictInfo.Id);
            cmd.Parameters.AddWithValue("@asin", DictInfo.Asin);
            cmd.Parameters.AddWithValue("@langin", DictInfo.Langin);
            cmd.Parameters.AddWithValue("@langout", DictInfo.Langout);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM DICT_INFO WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
