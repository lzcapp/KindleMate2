using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Application.Services.KM2DB;

/// <summary>
/// Shared deduplication keys for KM/KMate database imports (both the legacy KM2.dat import
/// <see cref="KmDatabaseService"/> and the KMate km3.dat import <see cref="KmateDatabaseService"/>
/// use the SAME scope so that the two import paths behave consistently).
/// </summary>
internal static class KmateDedup {
    /// <summary>
    /// Clipping dedup scope is per (book, author, content), not global content: the same sentence
    /// highlighted in two different books are two distinct highlights and must both be kept. A true
    /// duplicate import (same book re-imported from a device/cloud/source) still shares
    /// book+author+content and is skipped. Empty author degrades to (book, "", content).
    /// </summary>
    public static string BookContentKey(Clipping clipping) {
        return (clipping.BookName ?? string.Empty) + "\u0001" +
               (clipping.AuthorName ?? string.Empty) + "\u0001" +
               clipping.Content;
    }
}
