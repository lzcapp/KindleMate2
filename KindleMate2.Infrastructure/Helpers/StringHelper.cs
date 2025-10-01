using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Constants;
using Markdig.Helpers;
using System.Runtime.InteropServices;
using System.Text;
using Lookup = KindleMate2.Domain.Entities.KM2DB.Lookup;

namespace KindleMate2.Infrastructure.Helpers {
    public static class StringHelper {
        /// <summary>
        /// Removes control characters, line feeds, and BOM (65279) from the input string.
        /// </summary>
        /// <param name="input">The input string to process</param>
        /// <returns>A cleaned string with control characters removed</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null</exception>
        public static string RemoveControlChar(string input) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            if (string.IsNullOrEmpty(input)) {
                return string.Empty;
            }

            const char BOM = AppConstants.ByteOrderMark; // Use constant from AppConstants
            var output = new StringBuilder(input.Length); // Pre-allocate capacity for better performance
            
            foreach (var c in input) {
                if (!c.IsControl() && !c.IsNewLineOrLineFeed() && c != BOM) {
                    output.Append(c);
                }
            }
            return output.ToString();
        }

        /// <summary>
        /// Converts a Roman numeral string to its integer equivalent.
        /// </summary>
        /// <param name="roman">The Roman numeral string to convert</param>
        /// <returns>The integer value of the Roman numeral</returns>
        /// <exception cref="ArgumentNullException">Thrown when roman is null</exception>
        /// <exception cref="ArgumentException">Thrown when roman contains invalid characters</exception>
        public static int RomanToInteger(string roman) {
            if (roman == null) {
                throw new ArgumentNullException(nameof(roman));
            }

            if (string.IsNullOrWhiteSpace(roman)) {
                return 0;
            }

            roman = roman.ToUpper();
            
            // Validate that all characters are valid Roman numerals
            if (roman.Any(c => !RomanMap.ContainsKey(c))) {
                throw new ArgumentException($"Invalid Roman numeral character found in: {roman}", nameof(roman));
            }

            var result = 0;
            var prevValue = 0;

            for (var i = roman.Length - 1; i >= 0; i--) {
                var value = RomanMap[roman[i]];

                if (value < prevValue) {
                    result -= value;
                } else {
                    result += value;
                }

                prevValue = value;
            }

            return result;
        }

        private static readonly Dictionary<char, int> RomanMap = new() {
            { 'I', 1 },
            { 'V', 5 },
            { 'X', 10 },
            { 'L', 50 },
            { 'C', 100 },
            { 'D', 500 },
            { 'M', 1000 }
        };

        /// <summary>
        /// Trims content by removing leading spaces, dots, and specific punctuation, then trims whitespace.
        /// </summary>
        /// <param name="content">The content to trim</param>
        /// <returns>Trimmed content or empty string if input is null</returns>
        public static string TrimContent(string? content) {
            if (content == null) {
                return string.Empty;
            }

            // Use more readable character array for trimming
            char[] leadingCharsToTrim = [' ', '.', '，', '。'];
            return content.TrimStart(leadingCharsToTrim).Trim();
        }

        /// <summary>
        /// Sanitizes a filename by replacing invalid characters with underscores.
        /// </summary>
        /// <param name="filename">The filename to sanitize</param>
        /// <returns>A sanitized filename safe for file system use</returns>
        /// <exception cref="ArgumentNullException">Thrown when filename is null</exception>
        public static string SanitizeFilename(string filename) {
            if (filename == null) {
                throw new ArgumentNullException(nameof(filename));
            }

            if (string.IsNullOrEmpty(filename)) {
                return string.Empty;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = invalidChars.Aggregate(filename, (current, c) => current.Replace(c, '_'));
            
            // Also sanitize reserved names on Windows
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
            
            if (reservedNames.Contains(nameWithoutExtension.ToUpperInvariant())) {
                sanitized = "_" + sanitized;
            }
            
            return sanitized.Trim();
        }

        /// <summary>
        /// Formats a file size in bytes to a human-readable string with appropriate unit.
        /// </summary>
        /// <param name="fileSize">The file size in bytes</param>
        /// <returns>A formatted string with size and unit (e.g., "1.5 MB")</returns>
        public static string FormatFileSize(long fileSize) {
            if (fileSize < 0) {
                return "0 B";
            }

            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            const int BytesInKilobyte = AppConstants.BytesInKilobyte;

            var order = 0;
            double size = fileSize;

            while (size >= BytesInKilobyte && order < sizes.Length - 1) {
                order++;
                size /= BytesInKilobyte;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Parses Kindle version text to extract version number.
        /// </summary>
        /// <param name="versionText">The raw version text from Kindle</param>
        /// <returns>Parsed version string</returns>
        /// <exception cref="ArgumentNullException">Thrown when versionText is null</exception>
        public static string ParseKindleVersion(string versionText) {
            if (versionText == null) {
                throw new ArgumentNullException(nameof(versionText));
            }

            return versionText.Trim().Split('(')[0].Replace(AppConstants.Kindle, "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        /// <summary>
        /// Formats an exception message with context information.
        /// </summary>
        /// <param name="className">The class/method name where the exception occurred</param>
        /// <param name="e">The exception to format</param>
        /// <returns>A formatted exception message string</returns>
        /// <exception cref="ArgumentNullException">Thrown when className or exception is null</exception>
        public static string GetExceptionMessage(string className, Exception e) {
            if (className == null) {
                throw new ArgumentNullException(nameof(className));
            }
            if (e == null) {
                throw new ArgumentNullException(nameof(e));
            }

            return $"{className}: {Environment.NewLine}{e.Message}{Environment.NewLine}Stack Trace: {e.StackTrace}";
        }

        public static string GetRuntimeArchitecture() {
            var is64Bit = Environment.Is64BitProcess;

            var runtime = RuntimeInformation.FrameworkDescription.Contains(".NET") ? "runtime" : string.Empty;

            return is64Bit switch {
                true when runtime == "runtime" => "x64_runtime",
                false when runtime == "runtime" => "x86_runtime",
                true => "x64",
                _ => "x86"
            };
        }

        /// <summary>
        /// Builds markdown content from a list of clippings for a specific book.
        /// </summary>
        /// <param name="clippings">List of clippings to process</param>
        /// <returns>StringBuilder containing the formatted markdown</returns>
        /// <exception cref="ArgumentNullException">Thrown when clippings is null</exception>
        public static StringBuilder BuildMarkdownWithClippings(List<Clipping> clippings) {
            if (clippings == null) {
                throw new ArgumentNullException(nameof(clippings));
            }

            var markdown = new StringBuilder();

            try {
                if (clippings.Count == 0) {
                    // Return empty markdown instead of throwing exception for empty list
                    return markdown;
                }

                var bookName = clippings[0].BookName;

                if (!string.IsNullOrWhiteSpace(bookName)) {
                    markdown.AppendLine("## \ud83d\udcd6 " + bookName.Trim());
                } else {
                    markdown.AppendLine("## \ud83d\udcd6 Unknown Book");
                }

                markdown.AppendLine();

                foreach (var clipping in clippings) {
                    var clippingLocation = clipping.ClippingTypeLocation;
                    var content = clipping.Content;

                    if (string.IsNullOrWhiteSpace(clippingLocation) || string.IsNullOrWhiteSpace(content)) {
                        continue;
                    }

                    markdown.AppendLine("**" + clippingLocation + "**");
                    markdown.AppendLine();
                    markdown.AppendLine(content);
                    markdown.AppendLine();
                }
                
                return markdown;
            } catch (Exception e) {
                // Instead of Console.WriteLine, let the caller handle the exception
                throw new InvalidOperationException($"Failed to build markdown from clippings: {e.Message}", e);
            }
        }

        /// <summary>
        /// Builds markdown content from a list of vocabulary lookups.
        /// </summary>
        /// <param name="lookups">List of lookups to process</param>
        /// <returns>StringBuilder containing the formatted markdown</returns>
        /// <exception cref="ArgumentNullException">Thrown when lookups is null</exception>
        public static StringBuilder BuildMarkdownWithLookups(List<Lookup> lookups) {
            if (lookups == null) {
                throw new ArgumentNullException(nameof(lookups));
            }

            var markdown = new StringBuilder();

            try {
                if (lookups.Count == 0) {
                    // Return empty markdown instead of throwing exception for empty list
                    return markdown;
                }

                var word = lookups[0].Word;

                if (!string.IsNullOrWhiteSpace(word)) {
                    markdown.AppendLine("## \ud83d\udcd6 " + word.Trim());
                } else {
                    markdown.AppendLine("## \ud83d\udcd6 Unknown Word");
                }

                markdown.AppendLine();

                foreach (var lookup in lookups) {
                    var title = lookup.Title;
                    var usage = lookup.Usage;

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(usage)) {
                        continue;
                    }

                    markdown.AppendLine("**" + title + "**");
                    markdown.AppendLine();
                    
                    // Safely replace word occurrences with better error handling
                    var formattedUsage = !string.IsNullOrWhiteSpace(word) 
                        ? usage.Replace(word, " **`" + word + "`** ", StringComparison.OrdinalIgnoreCase) 
                        : usage;
                    
                    markdown.AppendLine(formattedUsage);
                    markdown.AppendLine();
                }
                
                return markdown;
            } catch (Exception e) {
                // Instead of Console.WriteLine, let the caller handle the exception
                throw new InvalidOperationException($"Failed to build markdown from lookups: {e.Message}", e);
            }
        }

        /// <summary>
        /// Formats statistics text with proper spacing and punctuation.
        /// </summary>
        /// <param name="parts">Array of text parts to join</param>
        /// <returns>A formatted string with proper spacing</returns>
        public static string FormatStatisticsText(params string[] parts) {
            if (parts == null || parts.Length == 0) {
                return string.Empty;
            }

            return string.Join("", parts);
        }

        /// <summary>
        /// Creates a markdown header with an emoji and title.
        /// </summary>
        /// <param name="emoji">The emoji to use</param>
        /// <param name="title">The title text</param>
        /// <returns>A formatted markdown header</returns>
        public static string CreateMarkdownHeader(string emoji, string title) {
            if (string.IsNullOrWhiteSpace(title)) {
                return string.Empty;
            }

            var emojiPart = string.IsNullOrWhiteSpace(emoji) ? "" : emoji + " ";
            return $"# {emojiPart}{title}";
        }
    }
}