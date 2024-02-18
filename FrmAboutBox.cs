using System.Diagnostics;
using System.Reflection;

namespace KindleMate2 {
    internal partial class FrmAboutBox : Form {
        public FrmAboutBox() {
            InitializeComponent();
        }

        private static string FormatFileSize(long fileSize) {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            var order = 0;
            double size = fileSize;

            while (size >= 1024 && order < sizes.Length - 1) {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private static string AssemblyVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

        private static string AssemblyProduct {
            get {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        private static string AssemblyCopyright {
            get {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        private void LblPath_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start("explorer.exe", lblPath.Text);
        }

        private void FrmAboutBox_Load(object sender, EventArgs e) {
            Text = "关于 " + AssemblyProduct;

            labelProductName.Text = AssemblyProduct;
            lblVersion.Text = AssemblyVersion;
            labelCopyright.Text = AssemblyCopyright;

            var programsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            lblPath.Text = programsDirectory;
            var filePath = Path.Combine(programsDirectory, "KM2.dat");
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length;
            lblDatabase.Text = "KM2.dat (" + FormatFileSize(fileSize) + ")";
        }
    }
}