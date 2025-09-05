namespace KindleMate2.Infrastructure.Helpers {
    public class WebClientHelper {
        private readonly HttpClient _httpClient;

        public WebClientHelper() {
            _httpClient = new HttpClient();
        }

        public async Task<string> GetHtmlAsync(string url) {
            try {
                return await _httpClient.GetStringAsync(url);
            } catch {
                return string.Empty; // Fails gracefully
            }
        }
    }
}