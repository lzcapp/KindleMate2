using System.Text;
using System.Text.RegularExpressions;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Application.Services;

/// <summary>
/// Prepares content detail display data for the bottom detail panel.
/// Handles lookup/clipping aggregation, word highlighting, and book title formatting.
/// </summary>
public class ContentDetailService : IContentDetailService {
    private readonly IClippingService _clippingService;

    public ContentDetailService(IClippingService clippingService) {
        _clippingService = clippingService;
    }

    /// <summary>
    /// Builds detail info for a selected clipping row.
    /// </summary>
    public ClippingDetailInfo BuildClippingDetail(string bookName, string authorName, int pageNumber,
        string content, string briefType, string key) {
        var info = new ClippingDetailInfo {
            BookName = bookName,
            AuthorName = authorName,
            PageNumber = pageNumber,
            Content = content,
            IsNote = briefType.Equals(((int)BriefType.Note).ToString())
        };

        if (info.IsNote) {
            var noteClippings = _clippingService.GetClippingsByBookNameAndPageNumberAndBriefType(bookName, pageNumber, BriefType.Highlight);
            if (noteClippings.Count > 0) {
                info.NoteClippingContent = noteClippings[0].Content;
            }
        }

        return info;
    }

    /// <summary>
    /// Builds detail info for a selected vocabulary/lookup row.
    /// Aggregates lookups and clippings that contain the target word.
    /// </summary>
    public VocabDetailInfo BuildVocabDetail(string wordKey, string word, string stem, string frequency,
        List<Lookup> allLookups, List<Clipping> allClippings) {
        var info = new VocabDetailInfo {
            Word = word,
            Stem = stem,
            Frequency = frequency,
            BookTitles = [],
            LookupEntries = [],
            ClippingEntries = []
        };

        // Non-ASCII characters (CJK, accented letters, etc.) don't have reliable word boundaries,
        // so we use substring matching instead of \b regex.
        var isNonAscii = Encoding.UTF8.GetBytes(word).Length > word.Length;

        var lookups = allLookups.OrderBy(x => x.Timestamp);
        foreach (Lookup lookup in lookups) {
            var title = string.Format(AppConstants.BookTitleFormat, lookup.Title);
            var usage = lookup.Usage?.Replace(AppConstants.SpaceForNewLine, Environment.NewLine);
            if (string.IsNullOrWhiteSpace(lookup.WordKey) || string.IsNullOrWhiteSpace(usage)) {
                continue;
            }

            if (!isNonAscii) {
                if (lookup.Word.Equals(stem, StringComparison.InvariantCultureIgnoreCase)) {
                    info.BookTitles.Add(title);
                    info.LookupEntries.Add(usage + title + Environment.NewLine);
                    continue;
                }
            }
            if (!string.Equals(lookup.WordKey, wordKey, StringComparison.InvariantCultureIgnoreCase)) {
                if (!isNonAscii) {
                    if (!Regex.IsMatch(usage, $"\\b{word}\\b", RegexOptions.IgnoreCase)) {
                        continue;
                    }
                } else if (!usage.Contains(word, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }
            }
            info.BookTitles.Add(title);
            info.LookupEntries.Add(usage + title + Environment.NewLine);
        }

        if (word.Length > 1) {
            foreach (Clipping clipping in allClippings.OrderBy(x => x.PageNumber)) {
                var title = string.Format(AppConstants.BookTitleFormat, clipping.BookName);
                var strContent = clipping.Content.Replace(AppConstants.SpaceForNewLine, Environment.NewLine);
                if (string.IsNullOrWhiteSpace(strContent)) {
                    continue;
                }
                if (!isNonAscii) {
                    if (!Regex.IsMatch(strContent, $"\\b{word}\\b", RegexOptions.IgnoreCase)) {
                        continue;
                    }
                } else {
                    if (!strContent.Contains(word, StringComparison.InvariantCultureIgnoreCase)) {
                        continue;
                    }
                }
                info.BookTitles.Add(title);
                info.ClippingEntries.Add(strContent + title + Environment.NewLine);
            }
        }

        return info;
    }
}

public class ClippingDetailInfo {
    public string BookName { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsNote { get; set; }
    public string? NoteClippingContent { get; set; }
}

public class VocabDetailInfo {
    public string Word { get; set; } = string.Empty;
    public string Stem { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public HashSet<string> BookTitles { get; set; } = [];
    public List<string> LookupEntries { get; set; } = [];
    public List<string> ClippingEntries { get; set; } = [];
}
