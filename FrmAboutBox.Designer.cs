namespace KindleMate2
{
    partial class FrmAboutBox
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent() {
            tableLayoutPanel = new TableLayoutPanel();
            logoPictureBox = new PictureBox();
            labelProductName = new Label();
            labelCopyright = new Label();
            okButton = new Button();
            flowLayoutPanel1 = new FlowLayoutPanel();
            lblPathText = new Label();
            lblPath = new LinkLabel();
            flowLayoutPanel2 = new FlowLayoutPanel();
            lblDatabaseText = new Label();
            lblDatabase = new Label();
            flowLayoutPanel3 = new FlowLayoutPanel();
            lblVersionText = new Label();
            lblVersion = new Label();
            tableLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)logoPictureBox).BeginInit();
            flowLayoutPanel1.SuspendLayout();
            flowLayoutPanel2.SuspendLayout();
            flowLayoutPanel3.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.AutoSize = true;
            tableLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel.ColumnCount = 2;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            tableLayoutPanel.Controls.Add(logoPictureBox, 0, 0);
            tableLayoutPanel.Controls.Add(labelProductName, 1, 0);
            tableLayoutPanel.Controls.Add(labelCopyright, 1, 2);
            tableLayoutPanel.Controls.Add(okButton, 1, 6);
            tableLayoutPanel.Controls.Add(flowLayoutPanel1, 1, 4);
            tableLayoutPanel.Controls.Add(flowLayoutPanel2, 1, 5);
            tableLayoutPanel.Controls.Add(flowLayoutPanel3, 1, 1);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Margin = new Padding(20);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.Padding = new Padding(20);
            tableLayoutPanel.RowCount = 7;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9991989F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20.0031986F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            tableLayoutPanel.Size = new Size(1016, 378);
            tableLayoutPanel.TabIndex = 0;
            // 
            // logoPictureBox
            // 
            logoPictureBox.Dock = DockStyle.Top;
            logoPictureBox.Image = Properties.Resources.bookmark;
            logoPictureBox.Location = new Point(20, 20);
            logoPictureBox.Margin = new Padding(0, 0, 10, 10);
            logoPictureBox.Name = "logoPictureBox";
            tableLayoutPanel.SetRowSpan(logoPictureBox, 6);
            logoPictureBox.Size = new Size(282, 270);
            logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            logoPictureBox.TabIndex = 12;
            logoPictureBox.TabStop = false;
            // 
            // labelProductName
            // 
            labelProductName.Dock = DockStyle.Fill;
            labelProductName.Location = new Point(326, 20);
            labelProductName.Margin = new Padding(14, 0, 6, 0);
            labelProductName.MaximumSize = new Size(0, 41);
            labelProductName.Name = "labelProductName";
            labelProductName.Size = new Size(664, 41);
            labelProductName.TabIndex = 19;
            labelProductName.Text = "产品名称";
            labelProductName.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelCopyright
            // 
            labelCopyright.Dock = DockStyle.Fill;
            labelCopyright.Location = new Point(326, 124);
            labelCopyright.Margin = new Padding(14, 0, 6, 0);
            labelCopyright.MaximumSize = new Size(0, 41);
            labelCopyright.Name = "labelCopyright";
            labelCopyright.Size = new Size(664, 41);
            labelCopyright.TabIndex = 21;
            labelCopyright.Text = "版权";
            labelCopyright.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // okButton
            // 
            okButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            okButton.DialogResult = DialogResult.Cancel;
            okButton.Dock = DockStyle.Right;
            okButton.Location = new Point(849, 307);
            okButton.Margin = new Padding(6, 7, 6, 7);
            okButton.Name = "okButton";
            okButton.Size = new Size(141, 44);
            okButton.TabIndex = 24;
            okButton.Text = "确定(&O)";
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.AutoSize = true;
            flowLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowLayoutPanel1.Controls.Add(lblPathText);
            flowLayoutPanel1.Controls.Add(lblPath);
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.Location = new Point(312, 196);
            flowLayoutPanel1.Margin = new Padding(0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(684, 52);
            flowLayoutPanel1.TabIndex = 27;
            flowLayoutPanel1.WrapContents = false;
            // 
            // lblPathText
            // 
            lblPathText.AutoSize = true;
            lblPathText.Dock = DockStyle.Fill;
            lblPathText.Location = new Point(14, 0);
            lblPathText.Margin = new Padding(14, 0, 6, 0);
            lblPathText.Name = "lblPathText";
            lblPathText.Size = new Size(110, 31);
            lblPathText.TabIndex = 25;
            lblPathText.Text = "程序路径";
            // 
            // lblPath
            // 
            lblPath.AutoEllipsis = true;
            lblPath.LinkBehavior = LinkBehavior.HoverUnderline;
            lblPath.Location = new Point(133, 0);
            lblPath.MaximumSize = new Size(500, 0);
            lblPath.Name = "lblPath";
            lblPath.Size = new Size(486, 31);
            lblPath.TabIndex = 26;
            lblPath.LinkClicked += LblPath_LinkClicked;
            // 
            // flowLayoutPanel2
            // 
            flowLayoutPanel2.Controls.Add(lblDatabaseText);
            flowLayoutPanel2.Controls.Add(lblDatabase);
            flowLayoutPanel2.Dock = DockStyle.Fill;
            flowLayoutPanel2.Location = new Point(312, 248);
            flowLayoutPanel2.Margin = new Padding(0);
            flowLayoutPanel2.Name = "flowLayoutPanel2";
            flowLayoutPanel2.Size = new Size(684, 52);
            flowLayoutPanel2.TabIndex = 28;
            // 
            // lblDatabaseText
            // 
            lblDatabaseText.AutoSize = true;
            lblDatabaseText.Dock = DockStyle.Fill;
            lblDatabaseText.Location = new Point(14, 0);
            lblDatabaseText.Margin = new Padding(14, 0, 6, 0);
            lblDatabaseText.Name = "lblDatabaseText";
            lblDatabaseText.Size = new Size(86, 31);
            lblDatabaseText.TabIndex = 26;
            lblDatabaseText.Text = "数据库";
            // 
            // lblDatabase
            // 
            lblDatabase.AutoSize = true;
            lblDatabase.Dock = DockStyle.Fill;
            lblDatabase.Location = new Point(109, 0);
            lblDatabase.Name = "lblDatabase";
            lblDatabase.Size = new Size(0, 31);
            lblDatabase.TabIndex = 27;
            // 
            // flowLayoutPanel3
            // 
            flowLayoutPanel3.Controls.Add(lblVersionText);
            flowLayoutPanel3.Controls.Add(lblVersion);
            flowLayoutPanel3.Dock = DockStyle.Fill;
            flowLayoutPanel3.Location = new Point(312, 72);
            flowLayoutPanel3.Margin = new Padding(0);
            flowLayoutPanel3.Name = "flowLayoutPanel3";
            flowLayoutPanel3.Size = new Size(684, 52);
            flowLayoutPanel3.TabIndex = 29;
            // 
            // lblVersionText
            // 
            lblVersionText.AutoSize = true;
            lblVersionText.Location = new Point(14, 0);
            lblVersionText.Margin = new Padding(14, 0, 6, 0);
            lblVersionText.MaximumSize = new Size(0, 41);
            lblVersionText.Name = "lblVersionText";
            lblVersionText.Size = new Size(62, 31);
            lblVersionText.TabIndex = 1;
            lblVersionText.Text = "版本";
            lblVersionText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.Location = new Point(85, 0);
            lblVersion.Margin = new Padding(3, 0, 0, 0);
            lblVersion.MaximumSize = new Size(0, 41);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(62, 31);
            lblVersion.TabIndex = 0;
            lblVersion.Text = "版本";
            lblVersion.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // FrmAboutBox
            // 
            AcceptButton = okButton;
            AutoScaleDimensions = new SizeF(14F, 31F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1016, 378);
            Controls.Add(tableLayoutPanel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(6, 7, 6, 7);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmAboutBox";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "FrmAboutBox";
            Load += FrmAboutBox_Load;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)logoPictureBox).EndInit();
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            flowLayoutPanel2.ResumeLayout(false);
            flowLayoutPanel2.PerformLayout();
            flowLayoutPanel3.ResumeLayout(false);
            flowLayoutPanel3.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.PictureBox logoPictureBox;
        private System.Windows.Forms.Label labelProductName;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label labelCopyright;
        private System.Windows.Forms.Button okButton;
        private Label lblPathText;
        private Label lblDatabaseText;
        private FlowLayoutPanel flowLayoutPanel1;
        private LinkLabel lblPath;
        private FlowLayoutPanel flowLayoutPanel2;
        private Label lblDatabase;
        private FlowLayoutPanel flowLayoutPanel3;
        private Label lblVersionText;
    }
}
