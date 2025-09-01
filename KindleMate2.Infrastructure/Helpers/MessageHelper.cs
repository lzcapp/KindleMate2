namespace KindleMate2.Infrastructure.Helpers {
    public class MessageHelper {
        public static string BuildMessage(string content, Exception? exception) {
            if (exception != null && !string.IsNullOrWhiteSpace(exception.Message)) {
                content += Environment.NewLine + exception.Message;
            }
            return content;
        }
    }
}
