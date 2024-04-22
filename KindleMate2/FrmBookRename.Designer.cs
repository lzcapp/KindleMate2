namespace KindleMate2 {
    partial class FrmBookRename {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmBookRename));
            tableLayoutPanel = new TableLayoutPanel();
            lblBook = new Label();
            flowLayoutPanel = new FlowLayoutPanel();
            btnOK = new Button();
            btnCancel = new Button();
            label1 = new Label();
            txtAuthor = new TextBox();
            txtBook = new TextBox();
            tableLayoutPanel.SuspendLayout();
            flowLayoutPanel.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel.Controls.Add(lblBook, 0, 0);
            tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 4);
            tableLayoutPanel.Controls.Add(label1, 0, 2);
            tableLayoutPanel.Controls.Add(txtAuthor, 0, 3);
            tableLayoutPanel.Controls.Add(txtBook, 0, 1);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(10, 10);
            tableLayoutPanel.Margin = new Padding(10, 5, 10, 5);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 5;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 51F));
            tableLayoutPanel.Size = new Size(532, 213);
            tableLayoutPanel.TabIndex = 1;
            // 
            // lblBook
            // 
            lblBook.AutoSize = true;
            lblBook.Dock = DockStyle.Fill;
            lblBook.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblBook.Location = new Point(0, 0);
            lblBook.Margin = new Padding(0, 0, 0, 10);
            lblBook.Name = "lblBook";
            lblBook.Size = new Size(532, 22);
            lblBook.TabIndex = 2;
            lblBook.Text = "书名：";
            lblBook.TextAlign = ContentAlignment.BottomLeft;
            // 
            // flowLayoutPanel
            // 
            flowLayoutPanel.AutoSize = true;
            flowLayoutPanel.Controls.Add(btnOK);
            flowLayoutPanel.Controls.Add(btnCancel);
            flowLayoutPanel.Dock = DockStyle.Right;
            flowLayoutPanel.Location = new Point(290, 160);
            flowLayoutPanel.Margin = new Padding(0);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Padding = new Padding(0, 10, 0, 0);
            flowLayoutPanel.Size = new Size(242, 53);
            flowLayoutPanel.TabIndex = 4;
            flowLayoutPanel.WrapContents = false;
            // 
            // btnOK
            // 
            btnOK.Location = new Point(15, 10);
            btnOK.Margin = new Padding(15, 0, 10, 0);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(96, 31);
            btnOK.TabIndex = 0;
            btnOK.Text = Strings.Save;
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += BtnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(131, 10);
            btnCancel.Margin = new Padding(10, 0, 15, 0);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(96, 31);
            btnCancel.TabIndex = 1;
            btnCancel.Text = Strings.Cancel;
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = DockStyle.Fill;
            label1.Location = new Point(0, 80);
            label1.Margin = new Padding(0, 0, 0, 10);
            label1.Name = "label1";
            label1.Size = new Size(532, 22);
            label1.TabIndex = 5;
            label1.Text = "作者：";
            label1.TextAlign = ContentAlignment.BottomLeft;
            // 
            // txtAuthor
            // 
            txtAuthor.Dock = DockStyle.Fill;
            txtAuthor.Location = new Point(3, 115);
            txtAuthor.Name = "txtAuthor";
            txtAuthor.Size = new Size(526, 27);
            txtAuthor.TabIndex = 6;
            txtAuthor.TextChanged += TxtAuthor_TextChanged;
            // 
            // txtBook
            // 
            txtBook.Dock = DockStyle.Fill;
            txtBook.Location = new Point(3, 35);
            txtBook.Name = "txtBook";
            txtBook.Size = new Size(526, 27);
            txtBook.TabIndex = 7;
            txtBook.TextChanged += TxtBook_TextChanged;
            // 
            // FrmBookRename
            // 
            AcceptButton = btnOK;
            AutoScaleMode = AutoScaleMode.Inherit;
            AutoSize = true;
            CancelButton = btnCancel;
            ClientSize = new Size(552, 233);
            Controls.Add(tableLayoutPanel);
            Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmBookRename";
            Padding = new Padding(10);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "重命名书籍";
            Load += FrmBookRename_Load;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            flowLayoutPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel;
        private Label lblBook;
        private FlowLayoutPanel flowLayoutPanel;
        private Button btnOK;
        private Button btnCancel;
        private Label label1;
        private TextBox txtAuthor;
        private TextBox txtBook;
    }
}