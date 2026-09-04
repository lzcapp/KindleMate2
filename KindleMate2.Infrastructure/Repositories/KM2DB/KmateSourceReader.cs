using KindleMate2.Domain.Entities.KM2DB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB;

/// <summary>
/// Read-only mapper for KMate (kmate.io) <c>km3.dat</c> (SQLite). It maps the four tables that share
/// their lineage with the legacy KM2 format (<c>clippings</c>, <c>original_clipping_lines</c>,
/// <c>lookups</c>, <c>vocab</c>) onto the KM2 domain entities used by the target repositories.
/// </summary>
/// <remarks>
/// The KMate source schema is NOT identical to the KM2 target schema, so the shared repositories
/// cannot be pointed at it. Handled differences:
/// <list type="bullet">
/// <item>extra source columns (source / note_parent_key / ck_synced / created_at / updated_at / …) are ignored;</item>
/// <item><c>vocab.id</c> is an INTEGER identity in km3 — a TEXT primary key is derived from <c>word_key</c>
/// (word_key is unique in practice; rows without a usable key are skipped);</item>
/// <item>numeric-ish TEXT columns (<c>colorRGB</c>, <c>category</c>, <c>frequency</c>, <c>pagenumber</c>) are
/// parsed defensively (empty / non-numeric fall back to -1 / 0);</item>
/// <item>km3 has no <c>settings</c> table and extra tables (<c>books</c>, <c>tags_*</c>, <c>pending_deletions</c>)
/// are out of scope — they are ignored.</item>
/// </list>
/// The source file is opened with <c>Mode=ReadOnly</c> and never written to.
/// </remarks>
public static class KmateSourceReader {
    public sealed record KmateData(
        List<Clipping> Clippings,
        List<OriginalClippingLine> OriginalClippingLines,
        List<Lookup> Lookups,
        List<Vocab> Vocabs);

    /// <summary>
    /// Reads a KMate km3.dat into KM2 entity candidates.
    /// </summary>
    /// <returns>True when the file exists and at least one of <c>clippings</c>/<c>vocab</c> is present.</returns>
    public static bool TryRead(string sourcePath, out KmateData data, out string error) {
        data = null!;
        error = string.Empty;

        if (!File.Exists(sourcePath)) {
            error = "KMate database file not found: " + sourcePath;
            return false;
        }

        try {
            using var connection = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly;");
            connection.Open();

            var tables = GetTableNames(connection);
            if (!tables.Contains("clippings") && !tables.Contains("vocab")) {
                error = "Not a KMate (km3.dat) database — no clippings/vocab table found.";
                return false;
            }

            var clippings = tables.Contains("clippings")
                ? ReadClippings(connection)
                : new List<Clipping>();
            var lines = tables.Contains("original_clipping_lines")
                ? ReadOriginalClippingLines(connection)
                : new List<OriginalClippingLine>();
            var lookups = tables.Contains("lookups")
                ? ReadLookups(connection)
                : new List<Lookup>();
            var vocabs = tables.Contains("vocab")
                ? ReadVocabs(connection)
                : new List<Vocab>();

            data = new KmateData(clippings, lines, lookups, vocabs);
            return true;
        } catch (Exception e) {
            error = e.Message;
            return false;
        }
    }

    private static HashSet<string> GetTableNames(SqliteConnection connection) {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static List<Clipping> ReadClippings(SqliteConnection connection) {
        var results = new List<Clipping>();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT key, content, bookname, authorname, brieftype, clippingdate, colorRGB, pagenumber FROM clippings";
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var key = GetString(reader, 0);
            if (string.IsNullOrEmpty(key)) {
                continue;
            }
            results.Add(new Clipping {
                Key = key,
                Content = GetString(reader, 1) ?? string.Empty,
                BookName = GetString(reader, 2),
                AuthorName = GetString(reader, 3),
                BriefType = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                ClippingDate = GetString(reader, 5),
                Read = 0,
                Sync = 0,
                ColorRgb = ParseLong(reader, 6, -1),
                PageNumber = ParseInt(reader, 7, 0)
            });
        }
        return results;
    }

    private static List<OriginalClippingLine> ReadOriginalClippingLines(SqliteConnection connection) {
        var results = new List<OriginalClippingLine>();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT key, line1, line2, line3, line4, line5 FROM original_clipping_lines";
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var key = GetString(reader, 0);
            if (string.IsNullOrEmpty(key)) {
                continue;
            }
            results.Add(new OriginalClippingLine {
                Key = key,
                Line1 = GetString(reader, 1),
                Line2 = GetString(reader, 2),
                Line3 = GetString(reader, 3),
                Line4 = GetString(reader, 4),
                Line5 = GetString(reader, 5)
            });
        }
        return results;
    }

    private static List<Lookup> ReadLookups(SqliteConnection connection) {
        var results = new List<Lookup>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT word_key, usage, title, authors, timestamp FROM lookups";
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var wordKey = GetString(reader, 0);
            if (string.IsNullOrWhiteSpace(wordKey)) {
                continue;
            }
            results.Add(new Lookup {
                WordKey = wordKey,
                Usage = GetString(reader, 1),
                Title = GetString(reader, 2),
                Authors = GetString(reader, 3),
                Timestamp = GetString(reader, 4)
            });
        }
        return results;
    }

    private static List<Vocab> ReadVocabs(SqliteConnection connection) {
        var results = new List<Vocab>();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT word_key, word, stem, category, translation, timestamp, frequency, colorRGB FROM vocab";
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var wordKey = GetString(reader, 0);
            var word = GetString(reader, 1);
            if (string.IsNullOrWhiteSpace(word)) {
                continue;
            }
            // km3 vocab.id is an INTEGER identity; derive a stable TEXT primary key from word_key.
            var id = !string.IsNullOrWhiteSpace(wordKey)
                ? wordKey
                : word;
            results.Add(new Vocab {
                Id = id,
                WordKey = wordKey,
                Word = word,
                Stem = GetString(reader, 2),
                Category = ParseLong(reader, 3, 0),
                Translation = GetString(reader, 4),
                Timestamp = GetString(reader, 5),
                Frequency = ParseInt(reader, 6, 0),
                Sync = 0,
                ColorRgb = ParseLong(reader, 7, -1)
            });
        }
        return results;
    }

    private static string? GetString(SqliteDataReader reader, int ordinal) {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long ParseLong(SqliteDataReader reader, int ordinal, long fallback) {
        if (reader.IsDBNull(ordinal)) {
            return fallback;
        }
        var raw = Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
        return long.TryParse(raw, out var value) ? value : fallback;
    }

    private static int ParseInt(SqliteDataReader reader, int ordinal, int fallback) {
        if (reader.IsDBNull(ordinal)) {
            return fallback;
        }
        var raw = Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
        return int.TryParse(raw, out var value) ? value : fallback;
    }
}
