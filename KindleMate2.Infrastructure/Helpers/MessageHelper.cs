namespace KindleMate2.Infrastructure.Helpers {
    public class MessageHelper {
        public static string BuildMessage(string content, Exception? exception) {
            if (exception != null) {
                content += exception.Message;
            }
            return content;
        }
    }
}
