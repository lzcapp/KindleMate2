namespace KindleMate2.Domain.Entities {
    public class FirmwareInfo {
        public string DeviceName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotesUrl { get; set; } = string.Empty;
    }
}