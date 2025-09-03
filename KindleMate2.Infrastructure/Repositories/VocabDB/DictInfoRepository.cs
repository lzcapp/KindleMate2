using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.VocabDB {
    public class DictInfoRepository : IDictInfoRepository {
        private readonly string _connectionString;

        public DictInfoRepository(string connectionString) {
            _connectionString = connectionString;
        }

        public DictInfo? GetById(string id) {
            try {
                SqliteConnection connection = new(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, asin, langin, langout FROM DICT_INFO WHERE id = @id", connection);
                if (string.IsNullOrEmpty(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    return new DictInfo {
                        Id = DatabaseHelper.GetSafeString(reader, 0) ?? throw new InvalidOperationException(),
                        Asin = DatabaseHelper.GetSafeString(reader, 1),
                        Langin = DatabaseHelper.GetSafeString(reader, 2),
                        Langout = DatabaseHelper.GetSafeString(reader, 3)
                    };
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return null;
        }

        public List<DictInfo> GetAll() {
            var results = new List<DictInfo>();

            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("SELECT id, asin, langin, langout FROM DICT_INFO", connection);
            
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var id = DatabaseHelper.GetSafeString(reader, 0);
                    if (string.IsNullOrWhiteSpace(id)) {
                        continue;
                    }
                    results.Add(new DictInfo {
                        Id = id,
                        Asin = DatabaseHelper.GetSafeString(reader, 1),
                        Langin = DatabaseHelper.GetSafeString(reader, 2),
                        Langout = DatabaseHelper.GetSafeString(reader, 3)
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

                var cmd = new SqliteCommand("SELECT COUNT(*) FROM DICT_INFO", connection);

                using SqliteDataReader reader = cmd.ExecuteReader();
                var result = cmd.ExecuteScalar();

                // ExecuteScalar returns object, so convert to int
                return Convert.ToInt32(result);
            } catch (Exception e) {
                Console.WriteLine(e);
                return 0;
            }
        }

        public bool Add(DictInfo dictInfo) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("INSERT INTO DICT_INFO (id, asin, langin, langout) VALUES (@id, @asin, @langin, @langout)", connection);
                var id = dictInfo.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@asin", dictInfo.Asin ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@langin", dictInfo.Langin ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@langout", dictInfo.Langout ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(DictInfo dictInfo) {
            try {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = new SqliteCommand("UPDATE DICT_INFO SET asin = @asin, langin = @langin, langout = @langout WHERE id = @id", connection);
                var id = dictInfo.Id;
                if (string.IsNullOrWhiteSpace(id)) {
                    throw new InvalidOperationException();
                }
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@asin", dictInfo.Asin ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@langin", dictInfo.Langin ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@langout", dictInfo.Langout ?? (object)DBNull.Value);
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

                var cmd = new SqliteCommand("DELETE FROM DICT_INFO WHERE id = @id", connection);
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
