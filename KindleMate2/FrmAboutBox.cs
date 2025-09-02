﻿using DarkModeForms;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using System.Diagnostics;
using System.Reflection;

namespace KindleMate2 {
    internal partial class FrmAboutBox : Form {
        private readonly ThemeService _themeService;
        
        private bool _isDarkTheme;

        private const string ConnectionString = "Data Source=KM2.dat;Cache=Shared;Mode=ReadWrite;";
        
        public FrmAboutBox() {
            InitializeComponent();

            var settingRepository = new SettingRepository(ConnectionString);
            _themeService = new ThemeService(settingRepository);

            _isDarkTheme = _themeService.IsDarkTheme();
            if (_themeService.IsDarkTheme()) {
                _ = new DarkModeCS(this, false);
                lblPath.LinkColor = Color.White;
            }
        }

        private static string AssemblyVersion {
            get => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        }

        private static string AssemblyProduct {
            get {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);

                return attributes.Length == 0 ? "" : ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        /*
        private static string AssemblyTitle {
            get {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);

                return attributes.Length == 0 ? "" : ((AssemblyTitleAttribute)attributes[0]).Title;
            }
        }
        */

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
            Text = Strings.About + Strings.Space + AssemblyProduct;

            //lblProductName.Text = AssemblyTitle;
            var assemblyVersion = string.IsNullOrWhiteSpace(AssemblyVersion) ? string.Empty : AssemblyVersion;
            var assemblyCopyright = string.IsNullOrWhiteSpace(AssemblyCopyright) ? string.Empty : AssemblyCopyright;
            lblVersion.Text = assemblyVersion;
            lblCopyright.Text = assemblyCopyright;

            var programsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            lblPath.Text = programsDirectory;
            var filePath = Path.Combine(programsDirectory, "KM2.dat");
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length;
            lblDatabase.Text = @"KM2.dat" + Strings.Left_Parenthesis + StringHelper.FormatFileSize(fileSize) + Strings.Right_Parenthesis;

            lblVersionText.Text = Strings.Version;
            lblCopyrightText.Text = Strings.Copyright;
            lblPathText.Text = Strings.Program_Path;
            lblDatabaseText.Text = Strings.Database;
            okButton.Text = Strings.Confirm_Button;

            /*var bw = new BackgroundWorker();
            bw.DoWork += (_, workEventArgs) => { workEventArgs.Result = StaticData.GetRepoInfo(); };
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result == null) {
                    return;
                }
                var release = (GitHubRelease)workerCompletedEventArgs.Result;
                if (string.IsNullOrWhiteSpace(assemblyVersion)) {
                    return;
                }
                var tagName = string.IsNullOrWhiteSpace(release.tag_name) ? string.Empty : release.tag_name;
                var toolTip = new ToolTip();
                toolTip.SetToolTip(pictureBox1, Strings.New_Version + tagName);
                pictureBox1.Visible = StaticData.IsUpdate(assemblyVersion, tagName);
            };
            if (StaticData.IsInternetAvailable()) {
                bw.RunWorkerAsync();
            }*/
        }

        private void pictureBox1_Click(object sender, EventArgs e) {
            /*StaticData.CheckUpdate();*/
        }
    }
}