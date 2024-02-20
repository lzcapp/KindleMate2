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
            menuFile = new ToolStripMenuItem();
            menuRefresh = new ToolStripMenuItem();
            menuExit = new ToolStripMenuItem();
            menuManage = new ToolStripMenuItem();
            menuImportKindle = new ToolStripMenuItem();
            menuImportKindleMate = new ToolStripMenuItem();
            menuSyncFromKindle = new ToolStripMenuItem();
            menuBackup = new ToolStripMenuItem();
            menuClear = new ToolStripMenuItem();
            menuHelp = new ToolStripMenuItem();
            toolStripMenuAbout = new ToolStripMenuItem();
            menuRepo = new ToolStripMenuItem();
            menuKindle = new ToolStripMenuItem();
            splitContainerMain = new SplitContainer();
            tabControl1 = new TabControl();
            tabPageBooks = new TabPage();
            treeViewBooks = new TreeView();
            imageListBooks = new ImageList(components);
            tabPageWords = new TabPage();
            treeViewWords = new TreeView();
            imageListWords = new ImageList(components);
            splitContainerDetail = new SplitContainer();
            dataGridView = new DataGridView();
            tableLayoutPanel = new TableLayoutPanel();
            lblLocation = new Label();
            flowLayoutPanel = new FlowLayoutPanel();
            lblBook = new Label();
            lblAuthor = new Label();
            lblContent = new TextBox();
            menuClippings = new ContextMenuStrip(components);
            menuClippingsRefresh = new ToolStripMenuItem();
            ClippingMenuCopy = new ToolStripMenuItem();
            ClippingMenuDelete = new ToolStripMenuItem();
            menuBooks = new ContextMenuStrip(components);
            menuBookRefresh = new ToolStripMenuItem();
            booksMenuDelete = new ToolStripMenuItem();
            menuCombine = new ToolStripMenuItem();
            menuRename = new ToolStripMenuItem();
            openFileDialog = new OpenFileDialog();
            statusStrip = new StatusStrip();
            lblCount = new ToolStripStatusLabel();
            lblBookCount = new ToolStripStatusLabel();
            timer = new System.Windows.Forms.Timer(components);
            menuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPageBooks.SuspendLayout();
            tabPageWords.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerDetail).BeginInit();
            splitContainerDetail.Panel1.SuspendLayout();
            splitContainerDetail.Panel2.SuspendLayout();
            splitContainerDetail.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView).BeginInit();
            tableLayoutPanel.SuspendLayout();
            flowLayoutPanel.SuspendLayout();
            menuClippings.SuspendLayout();
            menuBooks.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip
            // 
            menuStrip.ImageScalingSize = new Size(32, 32);
            menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuManage, menuHelp, menuKindle });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.RenderMode = ToolStripRenderMode.System;
            menuStrip.Size = new Size(1620, 39);
            menuStrip.TabIndex = 1;
            menuStrip.Text = "menuStrip2";
            // 
            // menuFile
            // 
            menuFile.DisplayStyle = ToolStripItemDisplayStyle.Text;
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuRefresh, menuExit });
            menuFile.Name = "menuFile";
            menuFile.ShortcutKeyDisplayString = "";
            menuFile.ShortcutKeys = Keys.Alt | Keys.F;
            menuFile.Size = new Size(111, 35);
            menuFile.Text = "文件(&F)";
            // 
            // menuRefresh
            // 
            menuRefresh.Image = Properties.Resources.counterclockwise_arrows_button;
            menuRefresh.Name = "menuRefresh";
            menuRefresh.Size = new Size(195, 44);
            menuRefresh.Text = "刷新";
            menuRefresh.Click += MenuRefresh_Click;
            // 
            // menuExit
            // 
            menuExit.Image = Properties.Resources.cross_mark_button;
            menuExit.Name = "menuExit";
            menuExit.Size = new Size(195, 44);
            menuExit.Text = "退出";
            menuExit.Click += MenuExit_Click;
            // 
            // menuManage
            // 
            menuManage.DropDownItems.AddRange(new ToolStripItem[] { menuImportKindle, menuImportKindleMate, menuSyncFromKindle, menuBackup, menuClear });
            menuManage.Name = "menuManage";
            menuManage.Size = new Size(128, 35);
            menuManage.Text = "管理 (&M)";
            // 
            // menuImportKindle
            // 
            menuImportKindle.Image = Properties.Resources.memo;
            menuImportKindle.Name = "menuImportKindle";
            menuImportKindle.Size = new Size(404, 44);
            menuImportKindle.Text = "导入Kindle标注";
            menuImportKindle.Click += MenuImportKindle_Click;
            // 
            // menuImportKindleMate
            // 
            menuImportKindleMate.Image = Properties.Resources.bookmark_tabs;
            menuImportKindleMate.Name = "menuImportKindleMate";
            menuImportKindleMate.Size = new Size(404, 44);
            menuImportKindleMate.Text = "导入Kindle Mate数据库";
            menuImportKindleMate.Click += MenuImportKindleMate_Click;
            // 
            // menuSyncFromKindle
            // 
            menuSyncFromKindle.Image = Properties.Resources.mobile_phone_with_arrow;
            menuSyncFromKindle.Name = "menuSyncFromKindle";
            menuSyncFromKindle.Size = new Size(404, 44);
            menuSyncFromKindle.Text = "从Kindle设备导入标注";
            menuSyncFromKindle.Click += MenuSyncFromKindle_Click;
            // 
            // menuBackup
            // 
            menuBackup.Image = Properties.Resources.card_file_box;
            menuBackup.Name = "menuBackup";
            menuBackup.Size = new Size(404, 44);
            menuBackup.Text = "备份";
            menuBackup.Click += MenuBackup_Click;
            // 
            // menuClear
            // 
            menuClear.Image = Properties.Resources.wastebasket;
            menuClear.Name = "menuClear";
            menuClear.Size = new Size(404, 44);
            menuClear.Text = "清空数据";
            menuClear.Click += MenuClear_Click;
            // 
            // menuHelp
            // 
            menuHelp.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuAbout, menuRepo });
            menuHelp.Name = "menuHelp";
            menuHelp.Size = new Size(117, 35);
            menuHelp.Text = "帮助(&H)";
            // 
            // toolStripMenuAbout
            // 
            toolStripMenuAbout.Image = Properties.Resources.information;
            toolStripMenuAbout.Name = "toolStripMenuAbout";
            toolStripMenuAbout.Size = new Size(277, 44);
            toolStripMenuAbout.Text = "关于(&A)";
            toolStripMenuAbout.Click += ToolStripMenuAbout_Click;
            // 
            // menuRepo
            // 
            menuRepo.Image = Properties.Resources.star;
            menuRepo.Name = "menuRepo";
            menuRepo.Size = new Size(277, 44);
            menuRepo.Text = "GitHub仓库";
            menuRepo.Click += MenuRepo_Click;
            // 
            // menuKindle
            // 
            menuKindle.Image = Properties.Resources.mobile_phone_with_arrow;
            menuKindle.Name = "menuKindle";
            menuKindle.Size = new Size(257, 36);
            menuKindle.Text = "Kindle设备已连接";
            menuKindle.Visible = false;
            menuKindle.Click += MenuKindle_Click;
            // 
            // splitContainerMain
            // 
            splitContainerMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            splitContainerMain.Location = new Point(0, 40);
            splitContainerMain.Margin = new Padding(0);
            splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            splitContainerMain.Panel1.Controls.Add(tabControl1);
            splitContainerMain.Panel1MinSize = 200;
            // 
            // splitContainerMain.Panel2
            // 
            splitContainerMain.Panel2.Controls.Add(splitContainerDetail);
            splitContainerMain.Panel2MinSize = 100;
            splitContainerMain.Size = new Size(1620, 974);
            splitContainerMain.SplitterDistance = 428;
            splitContainerMain.TabIndex = 2;
            // 
            // tabControl1
            // 
            tabControl1.Alignment = TabAlignment.Bottom;
            tabControl1.Controls.Add(tabPageBooks);
            tabControl1.Controls.Add(tabPageWords);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Multiline = true;
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(428, 974);
            tabControl1.TabIndex = 0;
            // 
            // tabPageBooks
            // 
            tabPageBooks.Controls.Add(treeViewBooks);
            tabPageBooks.Location = new Point(8, 8);
            tabPageBooks.Margin = new Padding(0);
            tabPageBooks.Name = "tabPageBooks";
            tabPageBooks.Padding = new Padding(3);
            tabPageBooks.Size = new Size(412, 921);
            tabPageBooks.TabIndex = 0;
            tabPageBooks.Text = "标注";
            tabPageBooks.UseVisualStyleBackColor = true;
            // 
            // treeViewBooks
            // 
            treeViewBooks.BorderStyle = BorderStyle.None;
            treeViewBooks.Dock = DockStyle.Fill;
            treeViewBooks.HideSelection = false;
            treeViewBooks.ImageIndex = 0;
            treeViewBooks.ImageList = imageListBooks;
            treeViewBooks.Location = new Point(3, 3);
            treeViewBooks.Margin = new Padding(0);
            treeViewBooks.Name = "treeViewBooks";
            treeViewBooks.SelectedImageIndex = 1;
            treeViewBooks.ShowLines = false;
            treeViewBooks.ShowNodeToolTips = true;
            treeViewBooks.ShowPlusMinus = false;
            treeViewBooks.ShowRootLines = false;
            treeViewBooks.Size = new Size(406, 915);
            treeViewBooks.StateImageList = imageListBooks;
            treeViewBooks.TabIndex = 0;
            treeViewBooks.NodeMouseClick += TreeViewBooks_NodeMouseClick;
            treeViewBooks.MouseDown += TreeViewBooks_MouseDown;
            // 
            // imageListBooks
            // 
            imageListBooks.ColorDepth = ColorDepth.Depth32Bit;
            imageListBooks.ImageStream = (ImageListStreamer)resources.GetObject("imageListBooks.ImageStream");
            imageListBooks.TransparentColor = Color.Transparent;
            imageListBooks.Images.SetKeyName(0, "closed-book.png");
            imageListBooks.Images.SetKeyName(1, "open-book.png");
            imageListBooks.Images.SetKeyName(2, "books.png");
            // 
            // tabPageWords
            // 
            tabPageWords.Controls.Add(treeViewWords);
            tabPageWords.Location = new Point(8, 8);
            tabPageWords.Margin = new Padding(0);
            tabPageWords.Name = "tabPageWords";
            tabPageWords.Padding = new Padding(3);
            tabPageWords.Size = new Size(412, 921);
            tabPageWords.TabIndex = 1;
            tabPageWords.Text = "查词";
            tabPageWords.UseVisualStyleBackColor = true;
            // 
            // treeViewWords
            // 
            treeViewWords.BorderStyle = BorderStyle.None;
            treeViewWords.Dock = DockStyle.Fill;
            treeViewWords.ImageIndex = 0;
            treeViewWords.ImageList = imageListWords;
            treeViewWords.Location = new Point(3, 3);
            treeViewWords.Name = "treeViewWords";
            treeViewWords.SelectedImageIndex = 0;
            treeViewWords.ShowLines = false;
            treeViewWords.ShowPlusMinus = false;
            treeViewWords.ShowRootLines = false;
            treeViewWords.Size = new Size(406, 915);
            treeViewWords.TabIndex = 0;
            // 
            // imageListWords
            // 
            imageListWords.ColorDepth = ColorDepth.Depth32Bit;
            imageListWords.ImageStream = (ImageListStreamer)resources.GetObject("imageListWords.ImageStream");
            imageListWords.TransparentColor = Color.Transparent;
            imageListWords.Images.SetKeyName(0, "input-latin-uppercase.png");
            imageListWords.Images.SetKeyName(1, "input-latin-letters.png");
            imageListWords.Images.SetKeyName(2, "books.png");
            // 
            // splitContainerDetail
            // 
            splitContainerDetail.Dock = DockStyle.Fill;
            splitContainerDetail.Location = new Point(0, 0);
            splitContainerDetail.Margin = new Padding(0);
            splitContainerDetail.Name = "splitContainerDetail";
            splitContainerDetail.Orientation = Orientation.Horizontal;
            // 
            // splitContainerDetail.Panel1
            // 
            splitContainerDetail.Panel1.Controls.Add(dataGridView);
            // 
            // splitContainerDetail.Panel2
            // 
            splitContainerDetail.Panel2.Controls.Add(tableLayoutPanel);
            splitContainerDetail.Panel2MinSize = 200;
            splitContainerDetail.Size = new Size(1188, 974);
            splitContainerDetail.SplitterDistance = 576;
            splitContainerDetail.TabIndex = 1;
            // 
            // dataGridView
            // 
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.AllowUserToResizeRows = false;
            dataGridView.BackgroundColor = SystemColors.Control;
            dataGridView.BorderStyle = BorderStyle.None;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView.Dock = DockStyle.Fill;
            dataGridView.Location = new Point(0, 0);
            dataGridView.Margin = new Padding(0);
            dataGridView.Name = "dataGridView";
            dataGridView.ReadOnly = true;
            dataGridView.RowHeadersVisible = false;
            dataGridView.RowHeadersWidth = 82;
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView.Size = new Size(1188, 576);
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
            tableLayoutPanel.Size = new Size(1188, 394);
            tableLayoutPanel.TabIndex = 0;
            tableLayoutPanel.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblLocation
            // 
            lblLocation.AutoSize = true;
            lblLocation.BackColor = SystemColors.Window;
            lblLocation.Dock = DockStyle.Fill;
            lblLocation.Font = new Font("Microsoft YaHei UI Light", 10F);
            lblLocation.Location = new Point(5, 55);
            lblLocation.Margin = new Padding(0, 11, 0, 11);
            lblLocation.Name = "lblLocation";
            lblLocation.Size = new Size(1178, 35);
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
            flowLayoutPanel.Size = new Size(1178, 38);
            flowLayoutPanel.TabIndex = 3;
            flowLayoutPanel.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblBook
            // 
            lblBook.AutoSize = true;
            lblBook.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblBook.Location = new Point(1, 1);
            lblBook.Margin = new Padding(1);
            lblBook.Name = "lblBook";
            lblBook.Size = new Size(0, 36);
            lblBook.TabIndex = 0;
            lblBook.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblAuthor
            // 
            lblAuthor.AutoSize = true;
            lblAuthor.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblAuthor.Location = new Point(5, 0);
            lblAuthor.Name = "lblAuthor";
            lblAuthor.Size = new Size(0, 35);
            lblAuthor.TabIndex = 1;
            lblAuthor.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblContent
            // 
            lblContent.BackColor = SystemColors.Window;
            lblContent.BorderStyle = BorderStyle.None;
            lblContent.Dock = DockStyle.Fill;
            lblContent.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblContent.Location = new Point(8, 104);
            lblContent.Multiline = true;
            lblContent.Name = "lblContent";
            lblContent.ReadOnly = true;
            lblContent.ScrollBars = ScrollBars.Vertical;
            lblContent.Size = new Size(1172, 291);
            lblContent.TabIndex = 4;
            // 
            // menuClippings
            // 
            menuClippings.ImageScalingSize = new Size(28, 28);
            menuClippings.Items.AddRange(new ToolStripItem[] { menuClippingsRefresh, ClippingMenuCopy, ClippingMenuDelete });
            menuClippings.Name = "menuClippings";
            menuClippings.Size = new Size(137, 118);
            // 
            // menuClippingsRefresh
            // 
            menuClippingsRefresh.Name = "menuClippingsRefresh";
            menuClippingsRefresh.ShortcutKeyDisplayString = "";
            menuClippingsRefresh.Size = new Size(136, 38);
            menuClippingsRefresh.Text = "刷新";
            menuClippingsRefresh.Click += MenuClippingsRefresh_Click;
            // 
            // ClippingMenuCopy
            // 
            ClippingMenuCopy.Name = "ClippingMenuCopy";
            ClippingMenuCopy.ShortcutKeyDisplayString = "";
            ClippingMenuCopy.Size = new Size(136, 38);
            ClippingMenuCopy.Text = "复制";
            ClippingMenuCopy.Click += ClippingMenuCopy_Click;
            // 
            // ClippingMenuDelete
            // 
            ClippingMenuDelete.Name = "ClippingMenuDelete";
            ClippingMenuDelete.ShortcutKeyDisplayString = "";
            ClippingMenuDelete.Size = new Size(136, 38);
            ClippingMenuDelete.Text = "删除";
            ClippingMenuDelete.Click += ClippingMenuDelete_Click;
            // 
            // menuBooks
            // 
            menuBooks.ImageScalingSize = new Size(28, 28);
            menuBooks.Items.AddRange(new ToolStripItem[] { menuBookRefresh, booksMenuDelete, menuCombine, menuRename });
            menuBooks.Name = "contextMenuStrip1";
            menuBooks.Size = new Size(161, 156);
            // 
            // menuBookRefresh
            // 
            menuBookRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text;
            menuBookRefresh.Name = "menuBookRefresh";
            menuBookRefresh.ShortcutKeyDisplayString = "";
            menuBookRefresh.Size = new Size(160, 38);
            menuBookRefresh.Text = "刷新";
            menuBookRefresh.Click += MenuBookRefresh_Click;
            // 
            // booksMenuDelete
            // 
            booksMenuDelete.DisplayStyle = ToolStripItemDisplayStyle.Text;
            booksMenuDelete.Name = "booksMenuDelete";
            booksMenuDelete.ShortcutKeyDisplayString = "";
            booksMenuDelete.Size = new Size(160, 38);
            booksMenuDelete.Text = "删除";
            booksMenuDelete.Click += BooksMenuDelete_Click;
            // 
            // menuCombine
            // 
            menuCombine.Name = "menuCombine";
            menuCombine.ShortcutKeyDisplayString = "";
            menuCombine.Size = new Size(160, 38);
            menuCombine.Text = "合并到";
            menuCombine.Click += menuCombine_Click;
            // 
            // menuRename
            // 
            menuRename.Name = "menuRename";
            menuRename.ShortcutKeyDisplayString = "";
            menuRename.Size = new Size(160, 38);
            menuRename.Text = "重命名";
            menuRename.Click += MenuRename_Click;
            // 
            // openFileDialog
            // 
            openFileDialog.FileName = "openFileDialog";
            openFileDialog.ReadOnlyChecked = true;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.ShowReadOnly = true;
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new Size(28, 28);
            statusStrip.Items.AddRange(new ToolStripItem[] { lblCount, lblBookCount });
            statusStrip.Location = new Point(0, 1020);
            statusStrip.Name = "statusStrip";
            statusStrip.Padding = new Padding(1, 0, 15, 0);
            statusStrip.Size = new Size(1620, 28);
            statusStrip.SizingGrip = false;
            statusStrip.TabIndex = 3;
            statusStrip.Text = "statusStrip1";
            // 
            // lblCount
            // 
            lblCount.Image = Properties.Resources.keycap_number_sign;
            lblCount.Margin = new Padding(0);
            lblCount.Name = "lblCount";
            lblCount.Size = new Size(28, 28);
            // 
            // lblBookCount
            // 
            lblBookCount.DisplayStyle = ToolStripItemDisplayStyle.Text;
            lblBookCount.Margin = new Padding(0);
            lblBookCount.Name = "lblBookCount";
            lblBookCount.Size = new Size(0, 0);
            // 
            // timer
            // 
            timer.Enabled = true;
            timer.Interval = 5000;
            timer.Tick += Timer_Tick;
            // 
            // FrmMain
            // 
            AutoScaleDimensions = new SizeF(14F, 31F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1620, 1048);
            Controls.Add(statusStrip);
            Controls.Add(splitContainerMain);
            Controls.Add(menuStrip);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "FrmMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Kindle Mate 2";
            Load += FrmMain_Load;
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            splitContainerMain.Panel1.ResumeLayout(false);
            splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
            splitContainerMain.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPageBooks.ResumeLayout(false);
            tabPageWords.ResumeLayout(false);
            splitContainerDetail.Panel1.ResumeLayout(false);
            splitContainerDetail.Panel2.ResumeLayout(false);
            splitContainerDetail.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerDetail).EndInit();
            splitContainerDetail.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView).EndInit();
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            flowLayoutPanel.ResumeLayout(false);
            flowLayoutPanel.PerformLayout();
            menuClippings.ResumeLayout(false);
            menuBooks.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private MenuStrip menuStrip;
        private ToolStripMenuItem menuFile;
        private ToolStripMenuItem menuHelp;
        private SplitContainer splitContainerMain;
        private DataGridView dataGridView;
        private OpenFileDialog openFileDialog;
        private SplitContainer splitContainerDetail;
        private TableLayoutPanel tableLayoutPanel;
        private Label lblBook;
        private Label lblLocation;
        private FlowLayoutPanel flowLayoutPanel;
        private Label lblAuthor;
        private ContextMenuStrip menuBooks;
        private ToolStripMenuItem booksMenuDelete;
        private ToolStripMenuItem menuRename;
        private ImageList imageListBooks;
        private ContextMenuStrip menuClippings;
        private ToolStripMenuItem ClippingMenuDelete;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblCount;
        private ToolStripMenuItem ClippingMenuCopy;
        private ToolStripMenuItem menuExit;
        private ToolStripMenuItem toolStripMenuAbout;
        private ToolStripStatusLabel lblBookCount;
        private ToolStripMenuItem menuRepo;
        private System.Windows.Forms.Timer timer;
        private ToolStripMenuItem menuKindle;
        private ToolStripMenuItem menuClippingsRefresh;
        private ToolStripMenuItem menuBookRefresh;
        private ToolStripMenuItem menuCombine;
        private TextBox lblContent;
        private ToolStripMenuItem menuManage;
        private ToolStripMenuItem menuBackup;
        private ToolStripMenuItem menuImportKindleMate;
        private ToolStripMenuItem menuSyncFromKindle;
        private ToolStripMenuItem menuImportKindle;
        private ToolStripMenuItem menuRefresh;
        private ToolStripMenuItem menuClear;
        private TabControl tabControl1;
        private TabPage tabPageBooks;
        private TabPage tabPageWords;
        private TreeView treeViewBooks;
        private TreeView treeViewWords;
        private ImageList imageListWords;
    }
}
