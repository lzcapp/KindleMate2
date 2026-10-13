using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class ImportBatchRepository(string connectionString) : IImportBatchRepository {
        private const string Columns = "id, source_type, source_path, imported_at, parsed_count, inserted_count";

        public long Add(ImportBatch batch) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                $"INSERT INTO import_batches (source_type, source_path, imported_at, parsed_count, inserted_count) VALUES (@source_type, @source_path, @imported_at, @parsed_count, @inserted_count); SELECT last_insert_rowid();",
                connection);
            cmd.Parameters.AddWithValue("@source_type", batch.SourceType ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@source_path", batch.SourcePath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@imported_at", batch.ImportedAt ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@parsed_count", batch.ParsedCount);
            cmd.Parameters.AddWithValue("@inserted_count", batch.InsertedCount);

            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public bool UpdateCounts(long id, int parsedCount, int insertedCount) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand(
                "UPDATE import_batches SET parsed_count = @parsed_count, inserted_count = @inserted_count WHERE id = @id",
                connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@parsed_count", parsedCount);
            cmd.Parameters.AddWithValue("@inserted_count", insertedCount);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<ImportBatch> GetAll() {
            var results = new List<ImportBatch>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand($"SELECT {Columns} FROM import_batches ORDER BY id", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var sourceType = DatabaseHelper.GetSafeString(reader, 1);
                var importedAt = DatabaseHelper.GetSafeString(reader, 3);
                if (sourceType == null || importedAt == null) {
                    continue;
                }
                results.Add(new ImportBatch {
                    Id = reader.GetInt64(0),
                    SourceType = sourceType,
                    SourcePath = DatabaseHelper.GetSafeString(reader, 2),
                    ImportedAt = importedAt,
                    ParsedCount = DatabaseHelper.GetSafeInt(reader, 4) ?? 0,
                    InsertedCount = DatabaseHelper.GetSafeInt(reader, 5) ?? 0
                });
            }
            return results;
        }
    }
}
