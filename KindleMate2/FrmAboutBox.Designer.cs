using System.ComponentModel;

namespace KindleMate2
{
    partial class FrmAboutBox
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private IContainer components = null;

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
            lblPath = new LinkLabel();
            lblVersionText = new Label();
            lblCopyrightText = new Label();
            lblCopyright = new Label();
            lblPathText = new Label();
            lblDatabaseText = new Label();
            okButton = new Button();
            pictureBox = new PictureBox();
            lblDatabase = new Label();
            flowLayoutPanel = new FlowLayoutPanel();
            lblProductName = new Label();
            lblGen = new Label();
            flowLayoutPanel1 = new FlowLayoutPanel();
            lblVersion = new Label();
            pictureBox1 = new PictureBox();
            tableLayoutPanel.SuspendLayout();
            ((ISupportInitialize)pictureBox).BeginInit();
            flowLayoutPanel.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            ((ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.AutoSize = true;
            tableLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel.BackColor = Color.Transparent;
            tableLayoutPanel.ColumnCount = 3;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            tableLayoutPanel.Controls.Add(lblPath, 2, 4);
            tableLayoutPanel.Controls.Add(lblVersionText, 1, 1);
            tableLayoutPanel.Controls.Add(lblCopyrightText, 1, 2);
            tableLayoutPanel.Controls.Add(lblCopyright, 2, 2);
            tableLayoutPanel.Controls.Add(lblPathText, 1, 4);
            tableLayoutPanel.Controls.Add(lblDatabaseText, 1, 5);
            tableLayoutPanel.Controls.Add(okButton, 2, 6);
            tableLayoutPanel.Controls.Add(pictureBox, 0, 0);
            tableLayoutPanel.Controls.Add(lblDatabase, 2, 5);
            tableLayoutPanel.Controls.Add(flowLayoutPanel, 1, 0);
            tableLayoutPanel.Controls.Add(flowLayoutPanel1, 2, 1);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Margin = new Padding(19, 18, 19, 18);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.Padding = new Padding(19, 18, 19, 18);
            tableLayoutPanel.RowCount = 7;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20.0031986F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.Size = new Size(776, 423);
            tableLayoutPanel.TabIndex = 0;
            // 
            // lblPath
            // 
            lblPath.AutoEllipsis = true;
            lblPath.AutoSize = true;
            lblPath.LinkBehavior = LinkBehavior.AlwaysUnderline;
            lblPath.LinkColor = Color.Black;
            lblPath.Location = new Point(304, 220);
            lblPath.MaximumSize = new Size(464, 0);
            lblPath.Name = "lblPath";
            lblPath.Size = new Size(0, 31);
            lblPath.TabIndex = 26;
            lblPath.LinkClicked += LblPath_LinkClicked;
            // 
            // lblVersionText
            // 
            lblVersionText.AutoSize = true;
            lblVersionText.Location = new Point(169, 80);
            lblVersionText.Margin = new Padding(20, 0, 6, 0);
            lblVersionText.MaximumSize = new Size(0, 37);
            lblVersionText.Name = "lblVersionText";
            lblVersionText.Size = new Size(62, 31);
            lblVersionText.TabIndex = 1;
            lblVersionText.Text = "版本";
            lblVersionText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblCopyrightText
            // 
            lblCopyrightText.AutoSize = true;
            lblCopyrightText.Location = new Point(169, 142);
            lblCopyrightText.Margin = new Padding(20, 0, 6, 0);
            lblCopyrightText.MaximumSize = new Size(0, 37);
            lblCopyrightText.Name = "lblCopyrightText";
            lblCopyrightText.Size = new Size(62, 31);
            lblCopyrightText.TabIndex = 29;
            lblCopyrightText.Text = "版权";
            lblCopyrightText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblCopyright
            // 
            lblCopyright.AutoSize = true;
            lblCopyright.Location = new Point(304, 142);
            lblCopyright.Margin = new Padding(3, 0, 0, 0);
            lblCopyright.MaximumSize = new Size(0, 37);
            lblCopyright.Name = "lblCopyright";
            lblCopyright.Size = new Size(0, 31);
            lblCopyright.TabIndex = 21;
            lblCopyright.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblPathText
            // 
            lblPathText.AutoSize = true;
            lblPathText.Location = new Point(169, 220);
            lblPathText.Margin = new Padding(20, 0, 6, 0);
            lblPathText.Name = "lblPathText";
            lblPathText.Size = new Size(110, 31);
            lblPathText.TabIndex = 25;
            lblPathText.Text = "程序路径";
            // 
            // lblDatabaseText
            // 
            lblDatabaseText.AutoSize = true;
            lblDatabaseText.Location = new Point(169, 282);
            lblDatabaseText.Margin = new Padding(20, 0, 6, 0);
            lblDatabaseText.Name = "lblDatabaseText";
            lblDatabaseText.Size = new Size(86, 31);
            lblDatabaseText.TabIndex = 26;
            lblDatabaseText.Text = "数据库";
            // 
            // okButton
            // 
            okButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            okButton.DialogResult = DialogResult.Cancel;
            okButton.Dock = DockStyle.Right;
            okButton.Location = new Point(646, 354);
            okButton.Margin = new Padding(15, 10, 15, 10);
            okButton.Name = "okButton";
            okButton.Size = new Size(96, 41);
            okButton.TabIndex = 24;
            okButton.Text = Strings.Confirm_Button;
            // 
            // pictureBox
            // 
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Image = Properties.Resources.bookmark;
            pictureBox.Location = new Point(22, 21);
            pictureBox.Name = "pictureBox";
            tableLayoutPanel.SetRowSpan(pictureBox, 4);
            pictureBox.Size = new Size(124, 196);
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 1;
            pictureBox.TabStop = false;
            // 
            // lblDatabase
            // 
            lblDatabase.AutoSize = true;
            lblDatabase.Location = new Point(304, 282);
            lblDatabase.Name = "lblDatabase";
            lblDatabase.Size = new Size(0, 31);
            lblDatabase.TabIndex = 32;
            // 
            // flowLayoutPanel
            // 
            flowLayoutPanel.AutoSize = true;
            flowLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel.SetColumnSpan(flowLayoutPanel, 2);
            flowLayoutPanel.Controls.Add(lblProductName);
            flowLayoutPanel.Controls.Add(lblGen);
            flowLayoutPanel.Location = new Point(149, 18);
            flowLayoutPanel.Margin = new Padding(0);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Size = new Size(268, 42);
            flowLayoutPanel.TabIndex = 33;
            // 
            // lblProductName
            // 
            lblProductName.AutoSize = true;
            lblProductName.BackColor = Color.Transparent;
            lblProductName.Font = new Font("微软雅黑", 12F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblProductName.Location = new Point(18, 0);
            lblProductName.Margin = new Padding(18, 0, 0, 0);
            lblProductName.MaximumSize = new Size(0, 37);
            lblProductName.Name = "lblProductName";
            lblProductName.Size = new Size(212, 37);
            lblProductName.TabIndex = 19;
            lblProductName.Text = "Kindle Mate";
            lblProductName.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblGen
            // 
            lblGen.AutoSize = true;
            lblGen.Font = new Font("微软雅黑", 12F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblGen.ForeColor = Color.Red;
            lblGen.Location = new Point(230, 0);
            lblGen.Margin = new Padding(0);
            lblGen.Name = "lblGen";
            lblGen.Size = new Size(38, 42);
            lblGen.TabIndex = 20;
            lblGen.Text = "2";
            lblGen.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(lblVersion);
            flowLayoutPanel1.Controls.Add(pictureBox1);
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.Location = new Point(301, 80);
            flowLayoutPanel1.Margin = new Padding(0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(456, 62);
            flowLayoutPanel1.TabIndex = 34;
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.Location = new Point(3, 0);
            lblVersion.Margin = new Padding(3, 0, 0, 0);
            lblVersion.MaximumSize = new Size(0, 37);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(0, 31);
            lblVersion.TabIndex = 0;
            lblVersion.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox1
            // 
            pictureBox1.Cursor = Cursors.Hand;
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Image = Properties.Resources.new_button;
            pictureBox1.Location = new Point(6, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(25, 25);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 1;
            pictureBox1.TabStop = false;
            pictureBox1.Visible = false;
            pictureBox1.Click += pictureBox1_Click;
            // 
            // FrmAboutBox
            // 
            AcceptButton = okButton;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(776, 423);
            Controls.Add(tableLayoutPanel);
            Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(6);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmAboutBox";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "FrmAboutBox";
            Load += FrmAboutBox_Load;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            ((ISupportInitialize)pictureBox).EndInit();
            flowLayoutPanel.ResumeLayout(false);
            flowLayoutPanel.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            ((ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel;
        private Label lblProductName;
        private Label lblVersion;
        private Label lblCopyright;
        private Button okButton;
        private Label lblPathText;
        private Label lblDatabaseText;
        private LinkLabel lblPath;
        private Label lblVersionText;
        private Label lblCopyrightText;
        private PictureBox pictureBox;
        private Label lblGen;
        private Label lblDatabase;
        private FlowLayoutPanel flowLayoutPanel;
        private FlowLayoutPanel flowLayoutPanel1;
        private PictureBox pictureBox1;
    }
}
