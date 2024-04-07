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
            lblBook = new Label();
            flowLayoutPanel = new FlowLayoutPanel();
            btnOK = new Button();
            btnCancel = new Button();
            txtContent = new TextBox();
            tableLayoutPanel.SuspendLayout();
            flowLayoutPanel.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel.Controls.Add(lblBook, 0, 0);
            tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 2);
            tableLayoutPanel.Controls.Add(txtContent, 0, 1);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            tableLayoutPanel.Location = new Point(10, 10);
            tableLayoutPanel.Margin = new Padding(10, 5, 10, 5);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 3;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 80F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 51F));
            tableLayoutPanel.Size = new Size(532, 213);
            tableLayoutPanel.TabIndex = 0;
            // 
            // lblBook
            // 
            lblBook.AutoEllipsis = true;
            lblBook.Dock = DockStyle.Fill;
            lblBook.Font = new Font("微软雅黑", 9F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblBook.Location = new Point(0, 0);
            lblBook.Margin = new Padding(0);
            lblBook.Name = "lblBook";
            lblBook.Padding = new Padding(0, 0, 0, 10);
            lblBook.Size = new Size(532, 32);
            lblBook.TabIndex = 2;
            lblBook.Text = "书名";
            lblBook.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // flowLayoutPanel
            // 
            flowLayoutPanel.AutoSize = true;
            flowLayoutPanel.Controls.Add(btnOK);
            flowLayoutPanel.Controls.Add(btnCancel);
            flowLayoutPanel.Dock = DockStyle.Right;
            flowLayoutPanel.Location = new Point(290, 161);
            flowLayoutPanel.Margin = new Padding(0);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Padding = new Padding(0, 10, 0, 0);
            flowLayoutPanel.Size = new Size(242, 52);
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
            // txtContent
            // 
            txtContent.Dock = DockStyle.Fill;
            txtContent.Location = new Point(3, 35);
            txtContent.Multiline = true;
            txtContent.Name = "txtContent";
            txtContent.ScrollBars = ScrollBars.Vertical;
            txtContent.Size = new Size(526, 123);
            txtContent.TabIndex = 5;
            txtContent.TextChanged += TxtContent_TextChanged;
            // 
            // FrmEdit
            // 
            AutoScaleDimensions = new SizeF(192F, 192F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            ClientSize = new Size(552, 233);
            Controls.Add(tableLayoutPanel);
            Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
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
            flowLayoutPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel;
        private Label lblBook;
        private FlowLayoutPanel flowLayoutPanel;
        private Button btnOK;
        private Button btnCancel;
        private TextBox txtContent;
    }
}