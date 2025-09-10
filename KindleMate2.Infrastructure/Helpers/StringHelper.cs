using System.Runtime.InteropServices;
using System.Text;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Constants;
using Markdig.Helpers;

namespace KindleMate2.Infrastructure.Helpers {
    public static class StringHelper {
        public static string RemoveControlChar(string input) {
            var output = new StringBuilder();
            foreach (var c in input.Where(c => !c.IsControl() && !c.IsNewLineOrLineFeed() && c != 65279)) {
                output.Append(c);
            }
            return output.ToString();
        }

        public static int RomanToInteger(string roman) {
            var result = 0;
            var prevValue = 0;

            roman = roman.ToUpper();

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

        public static string TrimContent(string? content) {
            if (content == null) {
                return string.Empty;
            }
            var contentTrimmed = content.TrimStart(' ', '.', '，', '。').Trim();
            return contentTrimmed;
        }

        public static string SanitizeFilename(string filename) {
            var invalidChars = Path.GetInvalidFileNameChars();
            filename = invalidChars.Aggregate(filename, (current, c) => current.Replace(c, '_'));
            filename = filename.Trim();
            return filename;
        }

        public static string FormatFileSize(long fileSize) {
            string[] sizes = [
                "B", "KB", "MB", "GB", "TB"
            ];

            var order = 0;
            double size = fileSize;

            while (size >= 1024 && order < sizes.Length - 1) {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        public static string ParseKindleVersion(string versionText) {
            return versionText.Trim().Split('(')[0].Replace(AppConstants.Kindle, "").Trim();
        }
        
        public static string GetExceptionMessage(string className, Exception e) {
            return className + ": " + Environment.NewLine + e;
        }
        
        public static string GetRuntimeArchitecture(){
            var is64Bit = Environment.Is64BitProcess;
            
            var runtime = RuntimeInformation.FrameworkDescription.Contains(".NET") ? "runtime" : string.Empty;

            return is64Bit switch {
                true when runtime == "runtime" => "x64_runtime",
                false when runtime == "runtime" => "x86_runtime",
                true => "x64",
                _ => "x86"
            };
        }

        public static StringBuilder BuildMarkdownWithClippings(List<Clipping> clippings) {
            var markdown = new StringBuilder();

            try {
                if (clippings.Count <= 0) {
                    throw new Exception("No clippings found.");
                }
                
                var bookName = clippings[0].BookName;

                if (bookName != null) {
                    markdown.AppendLine("## \ud83d\udcd6 " + bookName.Trim());
                } else {
                    throw new Exception("No book name found.");
                }

                markdown.AppendLine();

                foreach (Clipping clipping in clippings) {
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
                Console.WriteLine(GetExceptionMessage(nameof(BuildMarkdownWithClippings), e));
                return markdown;
            }
        }
    }
}