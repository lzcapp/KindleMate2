using KindleMate2.Domain.Entities.KM2DB;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Infrastructure.Repositories.KM2DB;

/// <summary>
/// All-or-nothing batched writer for KMate (km3.dat) imports.
/// Inserts the four candidate sets (clippings / original_clipping_lines / lookups / vocab) inside a
/// single connection and a single transaction: if any insert fails, everything is rolled back, so an
/// import can never leave a partially migrated database behind.
/// </summary>
/// <remarks>
/// Column lists and parameter mapping intentionally mirror the single/Add and Add(List) overloads of the
/// KM2 repositories (ClippingRepository / OriginalClippingLineRepository / LookupRepository /
/// VocabRepository). Keep them in sync if the KM2 schema evolves.
/// </remarks>
public static class KmateAtomicWriter {
    public static void WriteAll(
        string connectionString,
        IReadOnlyList<Clipping> clippings,
        IReadOnlyList<OriginalClippingLine> originalClippingLines,
        IReadOnlyList<Lookup> lookups,
        IReadOnlyList<Vocab> vocabs) {
        if (clippings.Count == 0 && originalClippingLines.Count == 0 && lookups.Count == 0 && vocabs.Count == 0) {
            return;
        }

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try {
            InsertClippings(connection, transaction, clippings);
            InsertOriginalClippingLines(connection, transaction, originalClippingLines);
            InsertLookups(connection, transaction, lookups);
            InsertVocabs(connection, transaction, vocabs);
            transaction.Commit();
        } catch {
            transaction.Rollback();
            throw;
        }
    }

    private static void InsertClippings(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Clipping> clippings) {
        if (clippings.Count == 0) {
            return;
        }
        using var cmd = new SqliteCommand(
            "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, read, clipping_importdate, tag, sync, newbookname, colorRGB, pagenumber) VALUES (@key, @content, @bookname, @authorname, @brieftype, @clippingtypelocation, @clippingdate, @read, @clipping_importdate, @tag, @sync, @newbookname, @colorRGB, @pagenumber)",
            connection, transaction);
        foreach (Clipping clipping in clippings) {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@key", clipping.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@content", clipping.Content);
            cmd.Parameters.AddWithValue("@bookname", clipping.BookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authorname", clipping.AuthorName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@brieftype", clipping.BriefType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingtypelocation", clipping.ClippingTypeLocation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clippingdate", clipping.ClippingDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@read", clipping.Read ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@clipping_importdate", clipping.ClippingImportDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tag", clipping.Tag ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", clipping.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@newbookname", clipping.NewBookName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", clipping.ColorRgb ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pagenumber", clipping.PageNumber ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertOriginalClippingLines(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<OriginalClippingLine> lines) {
        if (lines.Count == 0) {
            return;
        }
        using var cmd = new SqliteCommand(
            "INSERT INTO original_clipping_lines (key, line1, line2, line3, line4, line5) VALUES (@key, @line1, @line2, @line3, @line4, @line5)",
            connection, transaction);
        foreach (OriginalClippingLine line in lines) {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@key", line.Key ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@line1", line.Line1 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line2", line.Line2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line3", line.Line3 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line4", line.Line4 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@line5", line.Line5 ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertLookups(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Lookup> lookups) {
        if (lookups.Count == 0) {
            return;
        }
        using var cmd = new SqliteCommand(
            "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES (@word_key, @usage, @title, @authors, @timestamp)",
            connection, transaction);
        foreach (Lookup lookup in lookups) {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@word_key", lookup.WordKey ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@usage", lookup.Usage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@title", lookup.Title ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@authors", lookup.Authors ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", lookup.Timestamp ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertVocabs(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Vocab> vocabs) {
        if (vocabs.Count == 0) {
            return;
        }
        using var cmd = new SqliteCommand(
            "INSERT INTO vocab (id, word_key, word, stem, category, translation, timestamp, frequency, sync, colorRGB) VALUES (@id, @word_key, @word, @stem, @category, @translation, @timestamp, @frequency, @sync, @colorRGB)",
            connection, transaction);
        foreach (Vocab vocab in vocabs) {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", vocab.Id ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@word_key", vocab.WordKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@word", vocab.Word ?? throw new InvalidOperationException());
            cmd.Parameters.AddWithValue("@stem", vocab.Stem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@category", vocab.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@translation", vocab.Translation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", vocab.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@frequency", vocab.Frequency ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sync", vocab.Sync ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@colorRGB", vocab.ColorRgb ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }
}
