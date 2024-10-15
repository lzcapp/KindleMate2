using KindleMate2;
using KindleMate2.Entities;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;

namespace Updater {
    public partial class FrmMain : Form {
        public FrmMain() {
            InitializeComponent();
        }

        private void FrmMain_Load(object sender, EventArgs e) {
            var bw = new BackgroundWorker();
            bw.DoWork += (_, workEventArgs) => {
                workEventArgs.Result = StaticData.GetRepoInfo();
            };
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result == null) {
                    return;
                }
                var release = (GitHubRelease)workerCompletedEventArgs.Result;
                var tagName = string.IsNullOrWhiteSpace(release.tag_name) ? string.Empty : release.tag_name;
                var isUpdate = StaticData.IsUpdate(tagName);
                if (isUpdate) {
                    var name = StaticData.GetVersionName();
                    var url = "https://github.com/lzcapp/KindleMate2/releases/download/" + tagName + "/KindleMate2" + name + ".zip";
                    DownloadLatestRelease(url);
                }
            };
            bw.RunWorkerAsync();
        }

        private static void DownloadLatestRelease(string url) {
            var path = Path.Combine(Environment.CurrentDirectory, Path.GetFileName(url));
            var bw = new BackgroundWorker();
            bw.DoWork += (_, workEventArgs) => {
                using var client = new HttpClient();
                var task = client.GetAsync(url);
                HttpResponseMessage response = task.Result;
                response.EnsureSuccessStatusCode();
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                response.Content.CopyToAsync(fileStream);
                workEventArgs.Result = true;
            };
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result == null) {
                    return;
                }
                var result = (bool)workerCompletedEventArgs.Result;
                if (result) {
                    if (File.Exists(path) && Directory.Exists(Environment.CurrentDirectory)) {
                        ZipFile.ExtractToDirectory(path, Environment.CurrentDirectory, true);
                        var km2 = Path.Combine(Environment.CurrentDirectory, "KindleMate2.exe");
                        Process.Start(km2);
                    }
                }
            };
            bw.RunWorkerAsync();
        }
    }
}
