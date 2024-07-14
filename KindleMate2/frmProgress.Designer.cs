namespace KindleMate2 {
    partial class frmProgress {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            progressBar = new ProgressBar();
            SuspendLayout();
            // 
            // progressBar
            // 
            progressBar.Dock = DockStyle.Fill;
            progressBar.Location = new Point(0, 0);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(612, 29);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 0;
            // 
            // frmProgress
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(612, 29);
            Controls.Add(progressBar);
            FormBorderStyle = FormBorderStyle.None;
            Name = "frmProgress";
            StartPosition = FormStartPosition.CenterParent;
            Text = "frmProgress";
            TopMost = true;
            ResumeLayout(false);
        }

        #endregion

        private ProgressBar progressBar;
    }
}