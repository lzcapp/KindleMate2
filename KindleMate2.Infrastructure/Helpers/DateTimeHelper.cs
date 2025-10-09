namespace KindleMate2.Infrastructure.Helpers {
    public static class DateTimeHelper {
        public static string GetCurrentTimestamp() {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var unixTimestampInSeconds = now.ToUnixTimeSeconds();
            return unixTimestampInSeconds.ToString();
        }
    }
}