﻿namespace KindleMate2
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
            lblPath = new LinkLabel();
            lblVersion = new Label();
            lblVersionText = new Label();
            lblCopyrightText = new Label();
            lblCopyright = new Label();
            lblPathText = new Label();
            lblDatabaseText = new Label();
            okButton = new Button();
            flowLayoutPanel2 = new FlowLayoutPanel();
            lblProductName = new Label();
            label2 = new Label();
            flowLayoutPanel3 = new FlowLayoutPanel();
            lblDatabase = new Label();
            lblCleanDatabase = new LinkLabel();
            pictureBox = new PictureBox();
            tableLayoutPanel.SuspendLayout();
            flowLayoutPanel2.SuspendLayout();
            flowLayoutPanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.AutoSize = true;
            tableLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel.ColumnCount = 3;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            tableLayoutPanel.Controls.Add(lblPath, 2, 4);
            tableLayoutPanel.Controls.Add(lblVersion, 2, 1);
            tableLayoutPanel.Controls.Add(lblVersionText, 1, 1);
            tableLayoutPanel.Controls.Add(lblCopyrightText, 1, 2);
            tableLayoutPanel.Controls.Add(lblCopyright, 2, 2);
            tableLayoutPanel.Controls.Add(lblPathText, 1, 4);
            tableLayoutPanel.Controls.Add(lblDatabaseText, 1, 5);
            tableLayoutPanel.Controls.Add(okButton, 2, 6);
            tableLayoutPanel.Controls.Add(flowLayoutPanel2, 1, 0);
            tableLayoutPanel.Controls.Add(flowLayoutPanel3, 2, 5);
            tableLayoutPanel.Controls.Add(pictureBox, 0, 0);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Margin = new Padding(20);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.Padding = new Padding(20);
            tableLayoutPanel.RowCount = 7;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9991989F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20.0031986F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 19.9992F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            tableLayoutPanel.Size = new Size(1016, 378);
            tableLayoutPanel.TabIndex = 0;
            // 
            // lblPath
            // 
            lblPath.AutoEllipsis = true;
            lblPath.AutoSize = true;
            lblPath.LinkBehavior = LinkBehavior.HoverUnderline;
            lblPath.Location = new Point(432, 197);
            lblPath.MaximumSize = new Size(500, 0);
            lblPath.Name = "lblPath";
            lblPath.Size = new Size(0, 31);
            lblPath.TabIndex = 26;
            lblPath.LinkClicked += LblPath_LinkClicked;
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.Location = new Point(432, 73);
            lblVersion.Margin = new Padding(3, 0, 0, 0);
            lblVersion.MaximumSize = new Size(0, 41);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(0, 31);
            lblVersion.TabIndex = 0;
            lblVersion.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblVersionText
            // 
            lblVersionText.AutoSize = true;
            lblVersionText.Location = new Point(294, 73);
            lblVersionText.Margin = new Padding(54, 0, 6, 0);
            lblVersionText.MaximumSize = new Size(0, 41);
            lblVersionText.Name = "lblVersionText";
            lblVersionText.Size = new Size(62, 31);
            lblVersionText.TabIndex = 1;
            lblVersionText.Text = "版本";
            lblVersionText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblCopyrightText
            // 
            lblCopyrightText.AutoSize = true;
            lblCopyrightText.Location = new Point(294, 126);
            lblCopyrightText.Margin = new Padding(54, 0, 6, 0);
            lblCopyrightText.MaximumSize = new Size(0, 41);
            lblCopyrightText.Name = "lblCopyrightText";
            lblCopyrightText.Size = new Size(62, 31);
            lblCopyrightText.TabIndex = 29;
            lblCopyrightText.Text = "版权";
            lblCopyrightText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblCopyright
            // 
            lblCopyright.AutoSize = true;
            lblCopyright.Location = new Point(432, 126);
            lblCopyright.Margin = new Padding(3, 0, 0, 0);
            lblCopyright.MaximumSize = new Size(0, 41);
            lblCopyright.Name = "lblCopyright";
            lblCopyright.Size = new Size(0, 31);
            lblCopyright.TabIndex = 21;
            lblCopyright.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblPathText
            // 
            lblPathText.AutoSize = true;
            lblPathText.Location = new Point(294, 197);
            lblPathText.Margin = new Padding(54, 0, 6, 0);
            lblPathText.Name = "lblPathText";
            lblPathText.Size = new Size(110, 31);
            lblPathText.TabIndex = 25;
            lblPathText.Text = "程序路径";
            // 
            // lblDatabaseText
            // 
            lblDatabaseText.AutoSize = true;
            lblDatabaseText.Location = new Point(294, 250);
            lblDatabaseText.Margin = new Padding(54, 0, 6, 0);
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
            okButton.Location = new Point(849, 310);
            okButton.Margin = new Padding(6, 7, 6, 7);
            okButton.Name = "okButton";
            okButton.Size = new Size(141, 41);
            okButton.TabIndex = 24;
            okButton.Text = Strings.Confirm_Button;
            // 
            // flowLayoutPanel2
            // 
            tableLayoutPanel.SetColumnSpan(flowLayoutPanel2, 2);
            flowLayoutPanel2.Controls.Add(lblProductName);
            flowLayoutPanel2.Controls.Add(label2);
            flowLayoutPanel2.Dock = DockStyle.Fill;
            flowLayoutPanel2.Location = new Point(240, 20);
            flowLayoutPanel2.Margin = new Padding(0);
            flowLayoutPanel2.Name = "flowLayoutPanel2";
            flowLayoutPanel2.Size = new Size(756, 53);
            flowLayoutPanel2.TabIndex = 31;
            // 
            // lblProductName
            // 
            lblProductName.AutoSize = true;
            lblProductName.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblProductName.Location = new Point(54, 0);
            lblProductName.Margin = new Padding(54, 0, 0, 0);
            lblProductName.MaximumSize = new Size(0, 41);
            lblProductName.Name = "lblProductName";
            lblProductName.Size = new Size(161, 31);
            lblProductName.TabIndex = 19;
            lblProductName.Text = "Kindle Mate";
            lblProductName.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label2.ForeColor = Color.Red;
            label2.Location = new Point(215, 0);
            label2.Margin = new Padding(0);
            label2.Name = "label2";
            label2.Size = new Size(29, 31);
            label2.TabIndex = 20;
            label2.Text = "2";
            label2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // flowLayoutPanel3
            // 
            flowLayoutPanel3.Controls.Add(lblDatabase);
            flowLayoutPanel3.Controls.Add(lblCleanDatabase);
            flowLayoutPanel3.Dock = DockStyle.Fill;
            flowLayoutPanel3.Location = new Point(429, 250);
            flowLayoutPanel3.Margin = new Padding(0);
            flowLayoutPanel3.Name = "flowLayoutPanel3";
            flowLayoutPanel3.Size = new Size(567, 53);
            flowLayoutPanel3.TabIndex = 32;
            // 
            // lblDatabase
            // 
            lblDatabase.AutoSize = true;
            lblDatabase.Location = new Point(3, 0);
            lblDatabase.Name = "lblDatabase";
            lblDatabase.Size = new Size(0, 31);
            lblDatabase.TabIndex = 27;
            // 
            // lblCleanDatabase
            // 
            lblCleanDatabase.AutoSize = true;
            lblCleanDatabase.Location = new Point(9, 0);
            lblCleanDatabase.Name = "lblCleanDatabase";
            lblCleanDatabase.Size = new Size(182, 31);
            lblCleanDatabase.TabIndex = 28;
            lblCleanDatabase.TabStop = true;
            lblCleanDatabase.Text = "（清理数据库）";
            lblCleanDatabase.LinkClicked += LblCleanDatabase_LinkClicked;
            // 
            // pictureBox
            // 
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Image = Properties.Resources.bookmark;
            pictureBox.Location = new Point(23, 23);
            pictureBox.Name = "pictureBox";
            tableLayoutPanel.SetRowSpan(pictureBox, 5);
            pictureBox.Size = new Size(214, 224);
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 1;
            pictureBox.TabStop = false;
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
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "FrmAboutBox";
            Load += FrmAboutBox_Load;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            flowLayoutPanel2.ResumeLayout(false);
            flowLayoutPanel2.PerformLayout();
            flowLayoutPanel3.ResumeLayout(false);
            flowLayoutPanel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label lblProductName;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblCopyright;
        private System.Windows.Forms.Button okButton;
        private Label lblPathText;
        private Label lblDatabaseText;
        private LinkLabel lblPath;
        private Label lblDatabase;
        private Label lblVersionText;
        private Label lblCopyrightText;
        private FlowLayoutPanel flowLayoutPanel1;
        private PictureBox pictureBox;
        private Label label1;
        private FlowLayoutPanel flowLayoutPanel2;
        private Label label2;
        private FlowLayoutPanel flowLayoutPanel3;
        private LinkLabel lblCleanDatabase;
    }
}
