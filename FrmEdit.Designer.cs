namespace KindleMate2
{
    partial class FrmEdit
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null))
            {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmEdit));
            tableLayoutPanel = new TableLayoutPanel();
            panel = new Panel();
            btnCancel = new Button();
            btnOK = new Button();
            lblBook = new Label();
            txtContent = new RichTextBox();
            tableLayoutPanel.SuspendLayout();
            panel.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanel.Controls.Add(panel, 0, 2);
            tableLayoutPanel.Controls.Add(lblBook, 0, 0);
            tableLayoutPanel.Controls.Add(txtContent, 0, 1);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(10, 10);
            tableLayoutPanel.Margin = new Padding(10, 5, 10, 5);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 1;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 15F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 65F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel.Size = new Size(780, 430);
            tableLayoutPanel.TabIndex = 0;
            // 
            // panel
            // 
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Controls.Add(btnCancel);
            panel.Controls.Add(btnOK);
            panel.Dock = DockStyle.Fill;
            panel.Location = new Point(0, 343);
            panel.Margin = new Padding(0);
            panel.Name = "panel";
            panel.Size = new Size(780, 87);
            panel.TabIndex = 0;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(503, 22);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(131, 40);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "取消";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // btnOK
            // 
            btnOK.Location = new Point(155, 22);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(131, 40);
            btnOK.TabIndex = 0;
            btnOK.Text = "保存";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += BtnOK_Click;
            // 
            // lblBook
            // 
            lblBook.AutoSize = true;
            lblBook.Dock = DockStyle.Fill;
            lblBook.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblBook.Location = new Point(0, 0);
            lblBook.Margin = new Padding(0);
            lblBook.Name = "lblBook";
            lblBook.Padding = new Padding(0, 0, 0, 10);
            lblBook.Size = new Size(780, 64);
            lblBook.TabIndex = 2;
            lblBook.Text = "书名";
            lblBook.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtContent
            // 
            txtContent.BorderStyle = BorderStyle.FixedSingle;
            txtContent.Dock = DockStyle.Fill;
            txtContent.Location = new Point(0, 64);
            txtContent.Margin = new Padding(0);
            txtContent.Name = "txtContent";
            txtContent.ShowSelectionMargin = true;
            txtContent.Size = new Size(780, 279);
            txtContent.TabIndex = 3;
            txtContent.Text = "";
            txtContent.TextChanged += TxtContent_TextChanged;
            // 
            // FrmEdit
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new SizeF(13F, 28F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(800, 450);
            Controls.Add(tableLayoutPanel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmEdit";
            Padding = new Padding(10);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "编辑标注";
            Load += FrmEdit_Load;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            panel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel;
        private Panel panel;
        private Button btnCancel;
        private Button btnOK;
        private Label lblBook;
        private RichTextBox txtContent;
    }
}