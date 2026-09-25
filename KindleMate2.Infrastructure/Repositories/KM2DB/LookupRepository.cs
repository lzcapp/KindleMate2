using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB {
    public class LookupRepository(string connectionString) : ILookupRepository {
        public Lookup? GetByWordKey(string wordKey) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE word_key = @word_key", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                return new Lookup {
                    WordKey = DatabaseHelper.GetSafeString(reader, 0),
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                };
            }
            return null;
        }

        public List<Lookup> GetAll() {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(new Lookup {
                    WordKey = wordKey,
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                });
            }
            return results;
        }

        public List<Lookup> GetByTimestamp(string timeStamp) {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE timestamp = @timeStamp", connection);
            cmd.Parameters.AddWithValue("@timeStamp", timeStamp);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(new Lookup {
                    WordKey = wordKey,
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                });
            }
            return results;
        }

        public bool ExistsByWordKeyAndTimestamp(string wordKey, string timestamp) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM lookups WHERE word_key = @word_key AND timestamp = @timestamp", connection);
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            cmd.Parameters.AddWithValue("@timestamp", timestamp);

            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public List<Lookup> GetByTitle(string title) {
            var results = new List<Lookup>();
            
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT word_key, usage, title, authors, timestamp FROM lookups WHERE title = @title", connection);
            cmd.Parameters.AddWithValue("@title", title);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(new Lookup {
                    WordKey = wordKey,
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                });
            }
            return results;
        }

        public List<Lookup> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            var results = new List<Lookup>();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var sql = string.Empty;
            switch (type) {
                case AppEntities.SearchType.BookTitle:
                    sql = "WHERE title LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.Author:
                    sql = "WHERE authors LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.Content:
                    sql = "WHERE usage LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.Vocabulary:
                case AppEntities.SearchType.Stem:
                    sql = "WHERE word_key LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
                case AppEntities.SearchType.All:
                    sql = "WHERE word_key LIKE '%' || @strSearch || '%' ESCAPE '\\' OR usage LIKE '%' || @strSearch || '%' ESCAPE '\\' OR title LIKE '%' || @strSearch || '%' ESCAPE '\\' OR authors LIKE '%' || @strSearch || '%' ESCAPE '\\'";
                    break;
            }
            var query = "SELECT DISTINCT * FROM lookups " + sql;
            var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@strSearch", DatabaseHelper.EscapeLikePattern(search));

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(new Lookup {
                    WordKey = wordKey,
                    Usage = DatabaseHelper.GetSafeString(reader, 1),
                    Title = DatabaseHelper.GetSafeString(reader, 2),
                    Authors = DatabaseHelper.GetSafeString(reader, 3),
                    Timestamp = DatabaseHelper.GetSafeString(reader, 4)
                });
            }
            return results;
        }

        public List<string> GetWordKeysList() {
            var results = new List<string>();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT DISTINCT word_key FROM lookups ORDER BY word_key", connection);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                var wordKey = DatabaseHelper.GetSafeString(reader, 0);
                if (string.IsNullOrWhiteSpace(wordKey)) {
                    continue;
                }
                results.Add(wordKey);
            }
            return results;
        }

        public int GetCount() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM lookups", connection);
            var result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }

        public bool Add(Lookup lookup) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES (@word_key, @usage, @title, @authors, @timestamp)", connection);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public int Add(List<Lookup> lookups) {
            var count = 0;
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try {
                using var cmd = new SqliteCommand("INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES (@word_key, @usage, @title, @authors, @timestamp)", connection, transaction);
                foreach (Lookup lookup in lookups) {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
                    cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
                    if (cmd.ExecuteNonQuery() > 0) {
                        count++;
                    }
                }
                transaction.Commit();
            } catch {
                transaction.Rollback();
                throw;
            }
            return count;
        }

        public bool Update(Lookup lookup) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // lookups 没有主键,身份是 (word_key, timestamp)(同一词可被查多次)。
            // 之前只按 word_key 定位,会把同键**所有行**一并改写,并把它们的 timestamp
            // 压成同一个值 —— 直接撞 UNIQUE(word_key, timestamp) 且误改无关行。
            // 这里用 null 安全的 IS 对齐 Delete(wordKey, timestamp) 的定位口径。
            var cmd = new SqliteCommand("UPDATE lookups SET usage = @usage, title = @title, authors = @authors, timestamp = @timestamp WHERE word_key = @word_key AND timestamp IS @timestamp", connection);
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string wordKey) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM lookups WHERE word_key = @word_key", connection);
            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(string wordKey, string timestamp) {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM lookups WHERE word_key = @word_key AND timestamp = @timestamp", connection);
            if (string.IsNullOrWhiteSpace(wordKey)) {
                throw new InvalidOperationException();
            }
            if (string.IsNullOrWhiteSpace(timestamp)) {
                throw new InvalidOperationException();
            }
            cmd.Parameters.AddWithValue("@word_key", wordKey);
            cmd.Parameters.AddWithValue("@timestamp", timestamp);
            return cmd.ExecuteNonQuery() > 0;
        }

        public int MergeWordKey(string oldWordKey, string newWordKey) {
            if (string.IsNullOrWhiteSpace(oldWordKey)) {
                throw new ArgumentException("旧 word_key 不能为空", nameof(oldWordKey));
            }
            if (string.IsNullOrWhiteSpace(newWordKey)) {
                throw new ArgumentException("新 word_key 不能为空", nameof(newWordKey));
            }
            if (string.Equals(oldWordKey, newWordKey, StringComparison.Ordinal)) {
                return 0;
            }

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // ① 先删"会撞唯一约束 (word_key, timestamp)"的那批:它们与目标键下同 timestamp 的行
            //    本就是一回事(同一个词、同一次阅读),搬过去只会撞上。
            //    这一步必须在 UPDATE 之前 —— 否则 UPDATE 会因约束失败而整条回滚,一条都搬不过去。
            //    timestamp 为 NULL 的行不算冲突(与 SQLite 对 UNIQUE 中 NULL 的语义一致:彼此不相等),
            //    所以它们不会被删,也不会让 UPDATE 失败。
            var dropCmd = new SqliteCommand(
                "DELETE FROM lookups WHERE word_key = @old_key " +
                "AND EXISTS (SELECT 1 FROM lookups b WHERE b.word_key = @new_key AND b.timestamp = lookups.timestamp)",
                connection, transaction);
            dropCmd.Parameters.AddWithValue("@old_key", oldWordKey);
            dropCmd.Parameters.AddWithValue("@new_key", newWordKey);
            dropCmd.ExecuteNonQuery();

            // ② 剩下的整批搬过去。
            var moveCmd = new SqliteCommand(
                "UPDATE lookups SET word_key = @new_key WHERE word_key = @old_key", connection, transaction);
            moveCmd.Parameters.AddWithValue("@old_key", oldWordKey);
            moveCmd.Parameters.AddWithValue("@new_key", newWordKey);
            var moved = moveCmd.ExecuteNonQuery();

            // 两步放在一个事务里:要么整件事都成,要么一行都不动。
            transaction.Commit();
            return moved;
        }

        public bool DeleteAll() {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var cmd = new SqliteCommand("DELETE FROM lookups", connection);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
