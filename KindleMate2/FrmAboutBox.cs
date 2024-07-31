﻿using System.Diagnostics;
using System.Reflection;
using DarkModeForms;

namespace KindleMate2 {
    internal partial class FrmAboutBox : Form {
        private readonly StaticData _staticData = new();

        public FrmAboutBox() {
            InitializeComponent();

            if (_staticData.IsDarkTheme()) {
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
            lblVersion.Text = AssemblyVersion;
            lblCopyright.Text = AssemblyCopyright;

            var programsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            lblPath.Text = programsDirectory;
            var filePath = Path.Combine(programsDirectory, "KM2.dat");
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length;
            lblDatabase.Text = @"KM2.dat" + Strings.Left_Parenthesis + StaticData.FormatFileSize(fileSize) + Strings.Right_Parenthesis;

            lblVersionText.Text = Strings.Version;
            lblCopyrightText.Text = Strings.Copyright;
            lblPathText.Text = Strings.Program_Path;
            lblDatabaseText.Text = Strings.Database;
            okButton.Text = Strings.Confirm_Button;
        }
    }
}