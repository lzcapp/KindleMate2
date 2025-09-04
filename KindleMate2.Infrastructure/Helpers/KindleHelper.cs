namespace KindleMate2.Infrastructure.Helpers {
    public class KindleHelper {
        private static readonly List<string> ClippingLimitReachedWarning = [
            "You have reached the clipping limit for this item",
            "您已达到本内容的剪贴上限"
        ];
        
        public static bool IsClippingLimitReached(string content) {
            return ClippingLimitReachedWarning.Any(content.Contains);
        }
    }
}