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
            menuRestart = new ToolStripMenuItem();
            menuExit = new ToolStripMenuItem();
            menuManage = new ToolStripMenuItem();
            menuImportKindle = new ToolStripMenuItem();
            menuImportKindleWords = new ToolStripMenuItem();
            menuImportKindleMate = new ToolStripMenuItem();
            menuSyncFromKindle = new ToolStripMenuItem();
            menuBackup = new ToolStripMenuItem();
            menuClear = new ToolStripMenuItem();
            menuHelp = new ToolStripMenuItem();
            menuAbout = new ToolStripMenuItem();
            menuRepo = new ToolStripMenuItem();
            menuKindle = new ToolStripMenuItem();
            menuLang = new ToolStripMenuItem();
            splitContainerMain = new SplitContainer();
            tabControl = new TabControl();
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
            lblContent = new RichTextBox();
            menuClippings = new ContextMenuStrip(components);
            menuClippingsRefresh = new ToolStripMenuItem();
            menuClippingsCopy = new ToolStripMenuItem();
            menuClippingsDelete = new ToolStripMenuItem();
            menuBooks = new ContextMenuStrip(components);
            menuBookRefresh = new ToolStripMenuItem();
            menuBooksDelete = new ToolStripMenuItem();
            menuRename = new ToolStripMenuItem();
            openFileDialog = new OpenFileDialog();
            statusStrip = new StatusStrip();
            lblCount = new ToolStripStatusLabel();
            lblBookCount = new ToolStripStatusLabel();
            progressBar = new ToolStripProgressBar();
            timer = new System.Windows.Forms.Timer(components);
            menuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            tabControl.SuspendLayout();
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
            menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuManage, menuHelp, menuKindle, menuLang });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.RenderMode = ToolStripRenderMode.System;
            menuStrip.Size = new Size(1504, 36);
            menuStrip.TabIndex = 1;
            // 
            // menuFile
            // 
            menuFile.DisplayStyle = ToolStripItemDisplayStyle.Text;
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuRefresh, menuRestart, menuExit });
            menuFile.Name = "menuFile";
            menuFile.ShortcutKeyDisplayString = "";
            menuFile.ShortcutKeys = Keys.Alt | Keys.F;
            menuFile.Size = new Size(97, 32);
            menuFile.Text = "文件(&F)";
            // 
            // menuRefresh
            // 
            menuRefresh.Image = Properties.Resources.counterclockwise_arrows_button;
            menuRefresh.Name = "menuRefresh";
            menuRefresh.Size = new Size(171, 40);
            menuRefresh.Text = Strings.Refresh;
            menuRefresh.Click += MenuRefresh_Click;
            // 
            // menuRestart
            // 
            menuRestart.Image = Properties.Resources.eight_spoked_asterisk;
            menuRestart.Name = "menuRestart";
            menuRestart.Size = new Size(171, 40);
            menuRestart.Text = Strings.Restart;
            menuRestart.Click += MenuRestart_Click;
            // 
            // menuExit
            // 
            menuExit.Image = Properties.Resources.cross_mark_button;
            menuExit.Name = "menuExit";
            menuExit.Size = new Size(171, 40);
            menuExit.Text = Strings.Exit;
            menuExit.Click += MenuExit_Click;
            // 
            // menuManage
            // 
            menuManage.DropDownItems.AddRange(new ToolStripItem[] { menuImportKindle, menuImportKindleWords, menuImportKindleMate, menuSyncFromKindle, menuBackup, menuClear });
            menuManage.Name = "menuManage";
            menuManage.Size = new Size(107, 32);
            menuManage.Text = "管理(&M)";
            // 
            // menuImportKindle
            // 
            menuImportKindle.Image = Properties.Resources.memo;
            menuImportKindle.Name = "menuImportKindle";
            menuImportKindle.Size = new Size(356, 40);
            menuImportKindle.Text = Strings.Import_Kindle_Clippings;
            menuImportKindle.Click += MenuImportKindle_Click;
            // 
            // menuImportKindleWords
            // 
            menuImportKindleWords.Image = Properties.Resources.memo;
            menuImportKindleWords.Name = "menuImportKindleWords";
            menuImportKindleWords.Size = new Size(356, 40);
            menuImportKindleWords.Text = Strings.Import_Kindle_Vocabs;
            menuImportKindleWords.Click += MenuImportKindleWords_Click;
            // 
            // menuImportKindleMate
            // 
            menuImportKindleMate.Image = Properties.Resources.bookmark_tabs;
            menuImportKindleMate.Name = "menuImportKindleMate";
            menuImportKindleMate.Size = new Size(356, 40);
            menuImportKindleMate.Text = Strings.Import_Kindle_Mate_Database;
            menuImportKindleMate.Click += MenuImportKindleMate_Click;
            // 
            // menuSyncFromKindle
            // 
            menuSyncFromKindle.Image = Properties.Resources.mobile_phone_with_arrow;
            menuSyncFromKindle.Name = "menuSyncFromKindle";
            menuSyncFromKindle.Size = new Size(356, 40);
            menuSyncFromKindle.Text = "从Kindle设备导入";
            menuSyncFromKindle.Visible = false;
            menuSyncFromKindle.Click += MenuSyncFromKindle_Click;
            // 
            // menuBackup
            // 
            menuBackup.Image = Properties.Resources.card_file_box;
            menuBackup.Name = "menuBackup";
            menuBackup.Size = new Size(356, 40);
            menuBackup.Text = Strings.Backup;
            menuBackup.Visible = false;
            menuBackup.Click += MenuBackup_Click;
            // 
            // menuClear
            // 
            menuClear.Image = Properties.Resources.wastebasket;
            menuClear.Name = "menuClear";
            menuClear.Size = new Size(356, 40);
            menuClear.Text = Strings.Clear_Data;
            menuClear.Click += MenuClear_Click;
            // 
            // menuHelp
            // 
            menuHelp.DropDownItems.AddRange(new ToolStripItem[] { menuAbout, menuRepo });
            menuHelp.Name = "menuHelp";
            menuHelp.Size = new Size(102, 32);
            menuHelp.Text = "帮助(&H)";
            // 
            // menuAbout
            // 
            menuAbout.Image = Properties.Resources.information;
            menuAbout.Name = "menuAbout";
            menuAbout.Size = new Size(243, 40);
            menuAbout.Text = Strings.About;
            menuAbout.Click += MenuAbout_Click;
            // 
            // menuRepo
            // 
            menuRepo.Image = Properties.Resources.star;
            menuRepo.Name = "menuRepo";
            menuRepo.Size = new Size(243, 40);
            menuRepo.Text = Strings.GitHub_Repo;
            menuRepo.Click += MenuRepo_Click;
            // 
            // menuKindle
            // 
            menuKindle.Image = Properties.Resources.mobile_phone_with_arrow;
            menuKindle.Margin = new Padding(10, 0, 0, 0);
            menuKindle.Name = "menuKindle";
            menuKindle.Size = new Size(236, 36);
            menuKindle.Text = " Kindle设备已连接";
            menuKindle.Visible = false;
            menuKindle.Click += MenuKindle_Click;
            // 
            // menuLang
            // 
            menuLang.Alignment = ToolStripItemAlignment.Right;
            menuLang.Name = "menuLang";
            menuLang.Size = new Size(72, 32);
            menuLang.Text = Strings.Language;
            menuLang.Visible = false;
            menuLang.Click += MenuLang_Click;
            // 
            // splitContainerMain
            // 
            splitContainerMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            splitContainerMain.Location = new Point(0, 36);
            splitContainerMain.Margin = new Padding(0);
            splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            splitContainerMain.Panel1.Controls.Add(tabControl);
            splitContainerMain.Panel1MinSize = 200;
            // 
            // splitContainerMain.Panel2
            // 
            splitContainerMain.Panel2.Controls.Add(splitContainerDetail);
            splitContainerMain.Panel2MinSize = 100;
            splitContainerMain.Size = new Size(1504, 880);
            splitContainerMain.SplitterDistance = 395;
            splitContainerMain.TabIndex = 2;
            // 
            // tabControl
            // 
            tabControl.Alignment = TabAlignment.Bottom;
            tabControl.Controls.Add(tabPageBooks);
            tabControl.Controls.Add(tabPageWords);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Multiline = true;
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(395, 880);
            tabControl.TabIndex = 0;
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            // 
            // tabPageBooks
            // 
            tabPageBooks.Controls.Add(treeViewBooks);
            tabPageBooks.Location = new Point(4, 4);
            tabPageBooks.Margin = new Padding(0);
            tabPageBooks.Name = "tabPageBooks";
            tabPageBooks.Padding = new Padding(3);
            tabPageBooks.Size = new Size(387, 839);
            tabPageBooks.TabIndex = 0;
            tabPageBooks.Text = Strings.Clippings;
            tabPageBooks.UseVisualStyleBackColor = true;
            // 
            // treeViewBooks
            // 
            treeViewBooks.BorderStyle = BorderStyle.None;
            treeViewBooks.Dock = DockStyle.Fill;
            treeViewBooks.FullRowSelect = true;
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
            treeViewBooks.Size = new Size(381, 833);
            treeViewBooks.StateImageList = imageListBooks;
            treeViewBooks.TabIndex = 0;
            treeViewBooks.NodeMouseClick += TreeViewBooks_NodeMouseClick;
            treeViewBooks.NodeMouseDoubleClick += TreeViewBooks_NodeMouseDoubleClick;
            treeViewBooks.KeyDown += TreeViewBooks_KeyDown;
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
            tabPageWords.Location = new Point(4, 4);
            tabPageWords.Margin = new Padding(0);
            tabPageWords.Name = "tabPageWords";
            tabPageWords.Padding = new Padding(3);
            tabPageWords.Size = new Size(387, 839);
            tabPageWords.TabIndex = 1;
            tabPageWords.Text = Strings.Vocabulary_List;
            tabPageWords.UseVisualStyleBackColor = true;
            // 
            // treeViewWords
            // 
            treeViewWords.BorderStyle = BorderStyle.None;
            treeViewWords.Dock = DockStyle.Fill;
            treeViewWords.FullRowSelect = true;
            treeViewWords.HideSelection = false;
            treeViewWords.ImageIndex = 1;
            treeViewWords.ImageList = imageListWords;
            treeViewWords.Location = new Point(3, 3);
            treeViewWords.Name = "treeViewWords";
            treeViewWords.SelectedImageIndex = 0;
            treeViewWords.ShowLines = false;
            treeViewWords.ShowPlusMinus = false;
            treeViewWords.ShowRootLines = false;
            treeViewWords.Size = new Size(381, 833);
            treeViewWords.TabIndex = 0;
            treeViewWords.NodeMouseClick += TreeViewWords_NodeMouseClick;
            treeViewWords.MouseDown += TreeViewWords_MouseDown;
            // 
            // imageListWords
            // 
            imageListWords.ColorDepth = ColorDepth.Depth32Bit;
            imageListWords.ImageStream = (ImageListStreamer)resources.GetObject("imageListWords.ImageStream");
            imageListWords.TransparentColor = Color.Transparent;
            imageListWords.Images.SetKeyName(0, "input-latin-uppercase.png");
            imageListWords.Images.SetKeyName(1, "input-latin-lowercase.png");
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
            splitContainerDetail.Size = new Size(1105, 880);
            splitContainerDetail.SplitterDistance = 518;
            splitContainerDetail.TabIndex = 1;
            // 
            // dataGridView
            // 
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.AllowUserToResizeColumns = false;
            dataGridView.AllowUserToResizeRows = false;
            dataGridView.BackgroundColor = SystemColors.Control;
            dataGridView.BorderStyle = BorderStyle.None;
            dataGridView.ColumnHeadersHeight = 46;
            dataGridView.Dock = DockStyle.Fill;
            dataGridView.EditMode = DataGridViewEditMode.EditProgrammatically;
            dataGridView.Location = new Point(0, 0);
            dataGridView.Margin = new Padding(0);
            dataGridView.Name = "dataGridView";
            dataGridView.ReadOnly = true;
            dataGridView.RowHeadersVisible = false;
            dataGridView.RowHeadersWidth = 82;
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView.Size = new Size(1105, 518);
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
            tableLayoutPanel.Padding = new Padding(5);
            tableLayoutPanel.RowCount = 4;
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.Size = new Size(1105, 358);
            tableLayoutPanel.TabIndex = 0;
            tableLayoutPanel.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblLocation
            // 
            lblLocation.AutoSize = true;
            lblLocation.BackColor = SystemColors.Window;
            lblLocation.Dock = DockStyle.Fill;
            lblLocation.Font = new Font("Microsoft YaHei UI Light", 10F);
            lblLocation.Location = new Point(5, 48);
            lblLocation.Margin = new Padding(0, 10, 0, 10);
            lblLocation.Name = "lblLocation";
            lblLocation.Size = new Size(1095, 31);
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
            flowLayoutPanel.Location = new Point(5, 5);
            flowLayoutPanel.Margin = new Padding(0);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Size = new Size(1095, 33);
            flowLayoutPanel.TabIndex = 3;
            flowLayoutPanel.MouseDoubleClick += FlowLayoutPanel_MouseDoubleClick;
            // 
            // lblBook
            // 
            lblBook.AutoSize = true;
            lblBook.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Bold, GraphicsUnit.Point, 134);
            lblBook.Location = new Point(1, 1);
            lblBook.Margin = new Padding(1);
            lblBook.Name = "lblBook";
            lblBook.Size = new Size(0, 31);
            lblBook.TabIndex = 0;
            lblBook.MouseDoubleClick += LblBook_MouseDoubleClick;
            // 
            // lblAuthor
            // 
            lblAuthor.AutoSize = true;
            lblAuthor.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblAuthor.Location = new Point(5, 0);
            lblAuthor.Name = "lblAuthor";
            lblAuthor.Size = new Size(0, 31);
            lblAuthor.TabIndex = 1;
            lblAuthor.MouseDoubleClick += LblAuthor_MouseDoubleClick;
            // 
            // lblContent
            // 
            lblContent.BackColor = SystemColors.Window;
            lblContent.BorderStyle = BorderStyle.None;
            lblContent.ContextMenuStrip = menuClippings;
            lblContent.Dock = DockStyle.Fill;
            lblContent.Font = new Font("Microsoft YaHei UI", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblContent.Location = new Point(10, 94);
            lblContent.Margin = new Padding(5);
            lblContent.Name = "lblContent";
            lblContent.ReadOnly = true;
            lblContent.ScrollBars = RichTextBoxScrollBars.Vertical;
            lblContent.Size = new Size(1085, 262);
            lblContent.TabIndex = 4;
            lblContent.Text = "";
            lblContent.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // menuClippings
            // 
            menuClippings.ImageScalingSize = new Size(28, 28);
            menuClippings.Items.AddRange(new ToolStripItem[] { menuClippingsRefresh, menuClippingsCopy, menuClippingsDelete });
            menuClippings.Name = "menuClippings";
            menuClippings.Size = new Size(127, 106);
            // 
            // menuClippingsRefresh
            // 
            menuClippingsRefresh.Name = "menuClippingsRefresh";
            menuClippingsRefresh.ShortcutKeyDisplayString = "";
            menuClippingsRefresh.Size = new Size(126, 34);
            menuClippingsRefresh.Text = Strings.Refresh;
            menuClippingsRefresh.Click += MenuClippingsRefresh_Click;
            // 
            // menuClippingsCopy
            // 
            menuClippingsCopy.Name = "menuClippingsCopy";
            menuClippingsCopy.ShortcutKeyDisplayString = "";
            menuClippingsCopy.Size = new Size(126, 34);
            menuClippingsCopy.Text = Strings.Copy;
            menuClippingsCopy.Click += ClippingMenuCopy_Click;
            // 
            // menuClippingsDelete
            // 
            menuClippingsDelete.Name = "menuClippingsDelete";
            menuClippingsDelete.ShortcutKeyDisplayString = "";
            menuClippingsDelete.Size = new Size(126, 34);
            menuClippingsDelete.Text = Strings.Delete;
            menuClippingsDelete.Click += ClippingMenuDelete_Click;
            // 
            // menuBooks
            // 
            menuBooks.ImageScalingSize = new Size(28, 28);
            menuBooks.Items.AddRange(new ToolStripItem[] { menuBookRefresh, menuBooksDelete, menuRename });
            menuBooks.Name = "contextMenuStrip1";
            menuBooks.Size = new Size(148, 106);
            // 
            // menuBookRefresh
            // 
            menuBookRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text;
            menuBookRefresh.Name = "menuBookRefresh";
            menuBookRefresh.ShortcutKeyDisplayString = "";
            menuBookRefresh.Size = new Size(147, 34);
            menuBookRefresh.Text = Strings.Refresh;
            menuBookRefresh.Click += MenuBookRefresh_Click;
            // 
            // menuBooksDelete
            // 
            menuBooksDelete.DisplayStyle = ToolStripItemDisplayStyle.Text;
            menuBooksDelete.Name = "menuBooksDelete";
            menuBooksDelete.ShortcutKeyDisplayString = "";
            menuBooksDelete.Size = new Size(147, 34);
            menuBooksDelete.Text = Strings.Delete;
            menuBooksDelete.Click += BooksMenuDelete_Click;
            // 
            // menuRename
            // 
            menuRename.Name = "menuRename";
            menuRename.ShortcutKeyDisplayString = "";
            menuRename.Size = new Size(147, 34);
            menuRename.Text = Strings.Rename;
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
            statusStrip.Items.AddRange(new ToolStripItem[] { lblCount, lblBookCount, progressBar });
            statusStrip.Location = new Point(0, 919);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1504, 28);
            statusStrip.SizingGrip = false;
            statusStrip.TabIndex = 3;
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
            lblBookCount.Image = Properties.Resources.input_latin_uppercase;
            lblBookCount.Margin = new Padding(20, 0, 0, 0);
            lblBookCount.Name = "lblBookCount";
            lblBookCount.Size = new Size(28, 28);
            lblBookCount.Visible = false;
            // 
            // progressBar
            // 
            progressBar.Alignment = ToolStripItemAlignment.Right;
            progressBar.Enabled = false;
            progressBar.Margin = new Padding(100, 5, 100, 5);
            progressBar.MarqueeAnimationSpeed = 50;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(200, 18);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = false;
            // 
            // timer
            // 
            timer.Enabled = true;
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            // 
            // FrmMain
            // 
            AutoScaleDimensions = new SizeF(13F, 28F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1504, 947);
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
            tabControl.ResumeLayout(false);
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
        private ToolStripMenuItem menuBooksDelete;
        private ToolStripMenuItem menuRename;
        private ImageList imageListBooks;
        private ContextMenuStrip menuClippings;
        private ToolStripMenuItem menuClippingsDelete;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblCount;
        private ToolStripMenuItem menuClippingsCopy;
        private ToolStripMenuItem menuExit;
        private ToolStripMenuItem menuAbout;
        private ToolStripStatusLabel lblBookCount;
        private ToolStripMenuItem menuRepo;
        private System.Windows.Forms.Timer timer;
        private ToolStripMenuItem menuKindle;
        private ToolStripMenuItem menuClippingsRefresh;
        private ToolStripMenuItem menuBookRefresh;
        private ToolStripMenuItem menuManage;
        private ToolStripMenuItem menuBackup;
        private ToolStripMenuItem menuImportKindleMate;
        private ToolStripMenuItem menuSyncFromKindle;
        private ToolStripMenuItem menuImportKindle;
        private ToolStripMenuItem menuRefresh;
        private ToolStripMenuItem menuClear;
        private TabControl tabControl;
        private TabPage tabPageBooks;
        private TabPage tabPageWords;
        private TreeView treeViewBooks;
        private TreeView treeViewWords;
        private ImageList imageListWords;
        private ToolStripProgressBar progressBar;
        private ToolStripMenuItem menuImportKindleWords;
        private RichTextBox lblContent;
        private ToolStripMenuItem menuRestart;
        private ToolStripMenuItem menuLang;
    }
}
