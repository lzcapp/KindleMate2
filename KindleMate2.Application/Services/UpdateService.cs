using HtmlAgilityPack;
using KindleMate2.Domain.Entities;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Application.Services {
    public class UpdateService {
        private readonly WebClientHelper _webClient;

        public UpdateService(WebClientHelper webClient) {
            _webClient = webClient;
        }

        /// <summary>
        /// Fetches and parses Kindle firmware update data from Amazon.
        /// </summary>
        public async Task<List<FirmwareInfo>> GetLatestFirmwareAsync() {
            var html = await _webClient.GetHtmlAsync(URLs.FirmwareUpdateUrl);
            if (string.IsNullOrEmpty(html))
                return [];

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var result = new List<FirmwareInfo>();

            // Find each <section class="landing_section">
            HtmlNodeCollection? sections = doc.DocumentNode.SelectNodes("//section[contains(@class, 'landing_section')]");

            foreach (HtmlNode section in sections) {
                HtmlNode? deviceNameNode = section.SelectSingleNode(".//h4[@class='sectiontitle']");
                HtmlNode? versionNode = section.SelectSingleNode(".//ul/li[1]/span/span");
                HtmlNode? downloadLinkNode = section.SelectSingleNode(".//ul/li[2]//a");
                HtmlNode? releaseNotesLinkNode = section.SelectSingleNode(".//ul/li[3]//a");

                result.Add(new FirmwareInfo {
                    DeviceName = deviceNameNode.InnerText.Trim(),
                    Version = versionNode.InnerText.Trim(),
                    DownloadUrl = downloadLinkNode?.GetAttributeValue("href", string.Empty) ?? "",
                    ReleaseNotesUrl = releaseNotesLinkNode?.GetAttributeValue("href", string.Empty) ?? ""
                });
            }

            return result;
        }
    }
}