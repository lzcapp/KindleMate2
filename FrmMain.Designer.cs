namespace KindleMate2 {
    partial class FrmMain {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmMain));
            menuStrip = new MenuStrip();
            toolStripMenuFile = new ToolStripMenuItem();
            toolStripMenuExit = new ToolStripMenuItem();
            toolStripMenuHelp = new ToolStripMenuItem();
            toolStripMenuAbout = new ToolStripMenuItem();
            splitContainer1 = new SplitContainer();
            treeView = new TreeView();
            imageList = new ImageList(components);
            splitContainer2 = new SplitContainer();
            dataGridView = new DataGridView();
            tableLayoutPanel = new TableLayoutPanel();
            lblLocation = new Label();
            flowLayoutPanel = new FlowLayoutPanel();
            lblBook = new Label();
            lblAuthor = new Label();
            lblContent = new Label();
            menuClippings = new ContextMenuStrip(components);
            ClippingMenuCopy = new ToolStripMenuItem();
            ClippingMenuDelete = new ToolStripMenuItem();
            menuBooks = new ContextMenuStrip(components);
            booksMenuDelete = new ToolStripMenuItem();
            toolStripMenuItem1 = new ToolStripMenuItem();
            openFileDialog = new OpenFileDialog();
            statusStrip1 = new StatusStrip();
            lblCount = new ToolStripStatusLabel();
            menuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView).BeginInit();
            tableLayoutPanel.SuspendLayout();
            flowLayoutPanel.SuspendLayout();
            menuClippings.SuspendLayout();
            menuBooks.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip
            // 
            menuStrip.ImageScalingSize = new Size(32, 32);
            menuStrip.Items.AddRange(new ToolStripItem[] { toolStripMenuFile, toolStripMenuHelp });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.RenderMode = ToolStripRenderMode.System;
            menuStrip.Size = new Size(1620, 39);
            menuStrip.TabIndex = 1;
            menuStrip.Text = "menuStrip2";
            // 
            // toolStripMenuFile
            // 
            toolStripMenuFile.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripMenuFile.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuExit });
            toolStripMenuFile.Name = "toolStripMenuFile";
            toolStripMenuFile.ShortcutKeyDisplayString = "";
            toolStripMenuFile.ShortcutKeys = Keys.Alt | Keys.F;
            toolStripMenuFile.Size = new Size(111, 35);
            toolStripMenuFile.Text = "文件(&F)";
            // 
            // toolStripMenuExit
            // 
            toolStripMenuExit.Image = Properties.Resources.cross_mark_button;
            toolStripMenuExit.Name = "toolStripMenuExit";
            toolStripMenuExit.Size = new Size(224, 44);
            toolStripMenuExit.Text = "退出(&E)";
            toolStripMenuExit.Click += ToolStripMenuExit_Click;
            // 
            // toolStripMenuHelp
            // 
            toolStripMenuHelp.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuAbout });
            toolStripMenuHelp.Name = "toolStripMenuHelp";
            toolStripMenuHelp.Size = new Size(117, 35);
            toolStripMenuHelp.Text = "帮助(&H)";
            // 
            // toolStripMenuAbout
            // 
            toolStripMenuAbout.Image = Properties.Resources.information;
            toolStripMenuAbout.Name = "toolStripMenuAbout";
            toolStripMenuAbout.Size = new Size(228, 44);
            toolStripMenuAbout.Text = "关于(&A)";
            toolStripMenuAbout.Click += ToolStripMenuAbout_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 39);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(treeView);
            splitContainer1.Panel1MinSize = 200;
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(splitContainer2);
            splitContainer1.Panel2MinSize = 100;
            splitContainer1.Size = new Size(1620, 1009);
            splitContainer1.SplitterDistance = 430;
            splitContainer1.TabIndex = 2;
            // 
            // treeView
            // 
            treeView.Dock = DockStyle.Fill;
            treeView.FullRowSelect = true;
            treeView.HideSelection = false;
            treeView.HotTracking = true;
            treeView.ImageIndex = 0;
            treeView.ImageList = imageList;
            treeView.Location = new Point(0, 0);
            treeView.Name = "treeView";
            treeView.SelectedImageIndex = 1;
            treeView.ShowNodeToolTips = true;
            treeView.ShowRootLines = false;
            treeView.Size = new Size(430, 1009);
            treeView.StateImageList = imageList;
            treeView.TabIndex = 0;
            treeView.NodeMouseClick += TreeView_NodeMouseClick;
            treeView.MouseDown += TreeView_MouseDown;
            // 
            // imageList
            // 
            imageList.ColorDepth = ColorDepth.Depth32Bit;
            imageList.ImageStream = (ImageListStreamer)resources.GetObject("imageList.ImageStream");
            imageList.TransparentColor = Color.Transparent;
            imageList.Images.SetKeyName(0, "closed-book.png");
            imageList.Images.SetKeyName(1, "open-book.png");
            imageList.Images.SetKeyName(2, "books.png");
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            splitContainer2.Orientation = Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(dataGridView);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(tableLayoutPanel);
            splitContainer2.Panel2MinSize = 200;
            splitContainer2.Size = new Size(1186, 1009);
            splitContainer2.SplitterDistance = 605;
            splitContainer2.TabIndex = 1;
            // 
            // dataGridView
            // 
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.AllowUserToResizeRows = false;
            dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dataGridView.BackgroundColor = SystemColors.Control;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView.Dock = DockStyle.Fill;
            dataGridView.Location = new Point(0, 0);
            dataGridView.Name = "dataGridView";
            dataGridView.ReadOnly = true;
            dataGridView.RowHeadersVisible = false;
            dataGridView.RowHeadersWidth = 82;
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView.Size = new Size(1186, 605);
            dataGridView.TabIndex = 0;
            dataGridView.CellDoubleClick += DataGridView_CellDoubleClick;
            dataGridView.CellMouseDown += DataGridView_CellMouseDown;
            dataGridView.SelectionChanged += DataGridView_SelectionChanged;
            dataGridView.KeyDown += DataGridView_KeyDown;
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.AutoSize = true;
            tableLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel.BackColor = SystemColors.Window;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel.Controls.Add(lblLocation, 0, 2);
            tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
            tableLayoutPanel.Controls.Add(lblContent, 0, 3);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Margin = new Padding(0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.Padding = new Padding(5, 6, 5, 6);
            tableLayoutPanel.RowCount = 4;
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.Size = new Size(1186, 400);
            tableLayoutPanel.TabIndex = 0;
            tableLayoutPanel.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblLocation
            // 
            lblLocation.AutoSize = true;
            lblLocation.BackColor = SystemColors.Window;
            lblLocation.Dock = DockStyle.Fill;
            lblLocation.Font = new Font("Microsoft YaHei UI Light", 10F);
            lblLocation.Location = new Point(5, 53);
            lblLocation.Margin = new Padding(0, 11, 0, 11);
            lblLocation.Name = "lblLocation";
            lblLocation.Size = new Size(1176, 35);
            lblLocation.TabIndex = 1;
            lblLocation.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // flowLayoutPanel
            // 
            flowLayoutPanel.AutoSize = true;
            flowLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowLayoutPanel.BackColor = SystemColors.Window;
            flowLayoutPanel.Controls.Add(lblBook);
            flowLayoutPanel.Controls.Add(lblAuthor);
            flowLayoutPanel.Dock = DockStyle.Fill;
            flowLayoutPanel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            flowLayoutPanel.Location = new Point(5, 6);
            flowLayoutPanel.Margin = new Padding(0);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Size = new Size(1176, 36);
            flowLayoutPanel.TabIndex = 3;
            flowLayoutPanel.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblBook
            // 
            lblBook.AutoSize = true;
            lblBook.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblBook.Location = new Point(0, 0);
            lblBook.Margin = new Padding(0);
            lblBook.Name = "lblBook";
            lblBook.Size = new Size(0, 36);
            lblBook.TabIndex = 0;
            lblBook.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblAuthor
            // 
            lblAuthor.AutoSize = true;
            lblAuthor.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblAuthor.Location = new Point(3, 0);
            lblAuthor.Name = "lblAuthor";
            lblAuthor.Size = new Size(0, 35);
            lblAuthor.TabIndex = 1;
            lblAuthor.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblContent
            // 
            lblContent.AutoSize = true;
            lblContent.ContextMenuStrip = menuClippings;
            lblContent.Dock = DockStyle.Fill;
            lblContent.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblContent.Location = new Point(8, 99);
            lblContent.Name = "lblContent";
            lblContent.Size = new Size(1170, 295);
            lblContent.TabIndex = 4;
            lblContent.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // menuClippings
            // 
            menuClippings.ImageScalingSize = new Size(28, 28);
            menuClippings.Items.AddRange(new ToolStripItem[] { ClippingMenuCopy, ClippingMenuDelete });
            menuClippings.Name = "menuClippings";
            menuClippings.Size = new Size(226, 80);
            // 
            // ClippingMenuCopy
            // 
            ClippingMenuCopy.Name = "ClippingMenuCopy";
            ClippingMenuCopy.ShortcutKeyDisplayString = "&Copy";
            ClippingMenuCopy.Size = new Size(225, 38);
            ClippingMenuCopy.Text = "复制";
            ClippingMenuCopy.Click += ClippingMenuCopy_Click;
            // 
            // ClippingMenuDelete
            // 
            ClippingMenuDelete.Name = "ClippingMenuDelete";
            ClippingMenuDelete.ShortcutKeyDisplayString = "&Delete";
            ClippingMenuDelete.Size = new Size(225, 38);
            ClippingMenuDelete.Text = "删除";
            ClippingMenuDelete.Click += ClippingMenuDelete_Click;
            // 
            // menuBooks
            // 
            menuBooks.ImageScalingSize = new Size(28, 28);
            menuBooks.Items.AddRange(new ToolStripItem[] { booksMenuDelete, toolStripMenuItem1 });
            menuBooks.Name = "contextMenuStrip1";
            menuBooks.Size = new Size(269, 80);
            // 
            // booksMenuDelete
            // 
            booksMenuDelete.DisplayStyle = ToolStripItemDisplayStyle.Text;
            booksMenuDelete.Name = "booksMenuDelete";
            booksMenuDelete.ShortcutKeyDisplayString = "Delete";
            booksMenuDelete.Size = new Size(268, 38);
            booksMenuDelete.Text = "删除";
            booksMenuDelete.Click += BooksMenuDelete_Click;
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.ShortcutKeyDisplayString = "Rename";
            toolStripMenuItem1.Size = new Size(268, 38);
            toolStripMenuItem1.Text = "重命名";
            // 
            // openFileDialog
            // 
            openFileDialog.FileName = "openFileDialog";
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(28, 28);
            statusStrip1.Items.AddRange(new ToolStripItem[] { lblCount });
            statusStrip1.Location = new Point(0, 1010);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Padding = new Padding(1, 0, 15, 0);
            statusStrip1.Size = new Size(1620, 38);
            statusStrip1.SizingGrip = false;
            statusStrip1.TabIndex = 3;
            statusStrip1.Text = "statusStrip1";
            // 
            // lblCount
            // 
            lblCount.DisplayStyle = ToolStripItemDisplayStyle.Text;
            lblCount.Name = "lblCount";
            lblCount.Size = new Size(0, 28);
            // 
            // FrmMain
            // 
            AutoScaleDimensions = new SizeF(14F, 31F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1620, 1048);
            Controls.Add(statusStrip1);
            Controls.Add(splitContainer1);
            Controls.Add(menuStrip);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "FrmMain";
            Text = " Kindle Mate 2";
            Load += FrmMain_Load;
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView).EndInit();
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            flowLayoutPanel.ResumeLayout(false);
            flowLayoutPanel.PerformLayout();
            menuClippings.ResumeLayout(false);
            menuBooks.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private MenuStrip menuStrip;
        private ToolStripMenuItem toolStripMenuFile;
        private ToolStripMenuItem toolStripMenuHelp;
        private SplitContainer splitContainer1;
        private DataGridView dataGridView;
        private TreeView treeView;
        private OpenFileDialog openFileDialog;
        private SplitContainer splitContainer2;
        private TableLayoutPanel tableLayoutPanel;
        private Label lblBook;
        private Label lblLocation;
        private FlowLayoutPanel flowLayoutPanel;
        private Label lblAuthor;
        private ContextMenuStrip menuBooks;
        private ToolStripMenuItem booksMenuDelete;
        private ToolStripMenuItem toolStripMenuItem1;
        private ImageList imageList;
        private ContextMenuStrip menuClippings;
        private ToolStripMenuItem ClippingMenuDelete;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel lblCount;
        private Label lblContent;
        private ToolStripMenuItem ClippingMenuCopy;
        private ToolStripMenuItem toolStripMenuExit;
        private ToolStripMenuItem toolStripMenuAbout;
    }
}
