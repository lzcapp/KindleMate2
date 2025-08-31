using System.Text;
using Markdig.Helpers;

namespace KindleMate2.Infrastructure.Helpers {
    public class StringHelper {
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
    }
}