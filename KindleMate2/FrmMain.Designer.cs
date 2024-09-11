using DarkModeForms;

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
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            menuStrip = new MenuStrip();
            menuFile = new ToolStripMenuItem();
            menuRefresh = new ToolStripMenuItem();
            menuStatistic = new ToolStripMenuItem();
            menuRestart = new ToolStripMenuItem();
            menuExit = new ToolStripMenuItem();
            menuManage = new ToolStripMenuItem();
            menuImportKindle = new ToolStripMenuItem();
            menuImportKindleWords = new ToolStripMenuItem();
            menuImportKindleMate = new ToolStripMenuItem();
            menuSyncFromKindle = new ToolStripMenuItem();
            menuExportMd = new ToolStripMenuItem();
            menuClean = new ToolStripMenuItem();
            menuBackup = new ToolStripMenuItem();
            menuClear = new ToolStripMenuItem();
            menuHelp = new ToolStripMenuItem();
            menuAbout = new ToolStripMenuItem();
            menuRepo = new ToolStripMenuItem();
            menuKindle = new ToolStripMenuItem();
            menuTheme = new ToolStripMenuItem();
            menuLang = new ToolStripMenuItem();
            menuLangSC = new ToolStripMenuItem();
            menuLangTC = new ToolStripMenuItem();
            menuLangEN = new ToolStripMenuItem();
            menuLangAuto = new ToolStripMenuItem();
            toolStripMenuItem1 = new ToolStripMenuItem();
            splitContainerMain = new SplitContainer();
            tabControl = new FlatTabControl();
            tabPageBooks = new TabPage();
            treeViewBooks = new TreeView();
            menuClippings = new ContextMenuStrip(components);
            menuClippingsRefresh = new ToolStripMenuItem();
            menuClippingsCopy = new ToolStripMenuItem();
            menuClippingsDelete = new ToolStripMenuItem();
            imageListBooks = new ImageList(components);
            tabPageWords = new TabPage();
            treeViewWords = new TreeView();
            menuBooks = new ContextMenuStrip(components);
            menuBookRefresh = new ToolStripMenuItem();
            menuBooksDelete = new ToolStripMenuItem();
            menuRename = new ToolStripMenuItem();
            imageListWords = new ImageList(components);
            splitContainerDetail = new SplitContainer();
            dataGridView = new DataGridView();
            tableLayoutPanel = new TableLayoutPanel();
            lblLocation = new Label();
            flowLayoutPanel = new FlowLayoutPanel();
            lblBook = new Label();
            lblAuthor = new Label();
            lblContent = new RichTextBox();
            menu = new ContextMenuStrip(components);
            menuListRefresh = new ToolStripMenuItem();
            openFileDialog = new OpenFileDialog();
            statusStrip = new StatusStrip();
            lblCount = new ToolStripStatusLabel();
            lblBookCount = new ToolStripStatusLabel();
            progressBar = new ToolStripProgressBar();
            timer = new System.Windows.Forms.Timer(components);
            menuContent = new ContextMenuStrip(components);
            menuContentCopy = new ToolStripMenuItem();
            menuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            tabControl.SuspendLayout();
            tabPageBooks.SuspendLayout();
            menuClippings.SuspendLayout();
            tabPageWords.SuspendLayout();
            menuBooks.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerDetail).BeginInit();
            splitContainerDetail.Panel1.SuspendLayout();
            splitContainerDetail.Panel2.SuspendLayout();
            splitContainerDetail.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView).BeginInit();
            tableLayoutPanel.SuspendLayout();
            flowLayoutPanel.SuspendLayout();
            menu.SuspendLayout();
            statusStrip.SuspendLayout();
            menuContent.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip
            // 
            menuStrip.BackColor = Color.Transparent;
            menuStrip.BackgroundImageLayout = ImageLayout.None;
            menuStrip.Font = new Font("微软雅黑", 9F);
            menuStrip.GripMargin = new Padding(0);
            menuStrip.ImageScalingSize = new Size(32, 32);
            menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuManage, menuHelp, menuKindle, menuTheme, menuLang, toolStripMenuItem1 });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.RenderMode = ToolStripRenderMode.System;
            menuStrip.Size = new Size(1096, 40);
            menuStrip.Stretch = false;
            menuStrip.TabIndex = 1;
            // 
            // menuFile
            // 
            menuFile.DisplayStyle = ToolStripItemDisplayStyle.Text;
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuRefresh, menuStatistic, menuRestart, menuExit });
            menuFile.Name = "menuFile";
            menuFile.ShortcutKeyDisplayString = "";
            menuFile.ShortcutKeys = Keys.Alt | Keys.F;
            menuFile.Size = new Size(97, 36);
            menuFile.Text = "文件(&F)";
            // 
            // menuRefresh
            // 
            menuRefresh.Image = Properties.Resources.counterclockwise_arrows_button;
            menuRefresh.Name = "menuRefresh";
            menuRefresh.Size = new Size(175, 44);
            menuRefresh.Text = Strings.Refresh;
            menuRefresh.Click += MenuRefresh_Click;
            // 
            // menuStatistic
            // 
            menuStatistic.Image = Properties.Resources.bar_chart;
            menuStatistic.Name = "menuStatistic";
            menuStatistic.Size = new Size(175, 44);
            menuStatistic.Text = "统计";
            menuStatistic.Click += MenuStatistic_Click;
            // 
            // menuRestart
            // 
            menuRestart.Image = Properties.Resources.eight_spoked_asterisk;
            menuRestart.Name = "menuRestart";
            menuRestart.Size = new Size(175, 44);
            menuRestart.Text = Strings.Restart;
            menuRestart.Click += MenuRestart_Click;
            // 
            // menuExit
            // 
            menuExit.Image = Properties.Resources.cross_mark_button;
            menuExit.Name = "menuExit";
            menuExit.Size = new Size(175, 44);
            menuExit.Text = Strings.Exit;
            menuExit.Click += MenuExit_Click;
            // 
            // menuManage
            // 
            menuManage.DropDownItems.AddRange(new ToolStripItem[] { menuImportKindle, menuImportKindleWords, menuImportKindleMate, menuSyncFromKindle, menuExportMd, menuClean, menuBackup, menuClear });
            menuManage.Name = "menuManage";
            menuManage.Size = new Size(107, 36);
            menuManage.Text = "管理(&M)";
            // 
            // menuImportKindle
            // 
            menuImportKindle.Image = Properties.Resources.memo;
            menuImportKindle.Name = "menuImportKindle";
            menuImportKindle.Size = new Size(360, 44);
            menuImportKindle.Text = Strings.Import_Kindle_Clippings;
            menuImportKindle.Click += MenuImportKindle_Click;
            // 
            // menuImportKindleWords
            // 
            menuImportKindleWords.Image = Properties.Resources.memo;
            menuImportKindleWords.Name = "menuImportKindleWords";
            menuImportKindleWords.Size = new Size(360, 44);
            menuImportKindleWords.Text = Strings.Import_Kindle_Vocabs;
            menuImportKindleWords.Click += MenuImportKindleWords_Click;
            // 
            // menuImportKindleMate
            // 
            menuImportKindleMate.Image = Properties.Resources.page_facing_up;
            menuImportKindleMate.Name = "menuImportKindleMate";
            menuImportKindleMate.Size = new Size(360, 44);
            menuImportKindleMate.Text = Strings.Import_Kindle_Mate_Database;
            menuImportKindleMate.Click += MenuImportKindleMate_Click;
            // 
            // menuSyncFromKindle
            // 
            menuSyncFromKindle.Image = Properties.Resources.mobile_phone_with_arrow;
            menuSyncFromKindle.Name = "menuSyncFromKindle";
            menuSyncFromKindle.Size = new Size(360, 44);
            menuSyncFromKindle.Text = "从Kindle设备导入";
            menuSyncFromKindle.Visible = false;
            menuSyncFromKindle.Click += MenuSyncFromKindle_Click;
            // 
            // menuExportMd
            // 
            menuExportMd.Image = Properties.Resources.bookmark_tabs;
            menuExportMd.Name = "menuExportMd";
            menuExportMd.Size = new Size(360, 44);
            menuExportMd.Text = "导出为Markdown";
            menuExportMd.Click += MenuExportMd_Click;
            // 
            // menuClean
            // 
            menuClean.Image = Properties.Resources.broom;
            menuClean.Name = "menuClean";
            menuClean.Size = new Size(360, 44);
            menuClean.Text = "清理数据库";
            menuClean.Click += MenuClean_Click;
            // 
            // menuBackup
            // 
            menuBackup.Image = Properties.Resources.card_file_box;
            menuBackup.Name = "menuBackup";
            menuBackup.Size = new Size(360, 44);
            menuBackup.Text = Strings.Backup;
            menuBackup.Click += MenuBackup_Click;
            // 
            // menuClear
            // 
            menuClear.Image = Properties.Resources.wastebasket;
            menuClear.Name = "menuClear";
            menuClear.Size = new Size(360, 44);
            menuClear.Text = Strings.Clear_Data;
            menuClear.Click += MenuClear_Click;
            // 
            // menuHelp
            // 
            menuHelp.DropDownItems.AddRange(new ToolStripItem[] { menuAbout, menuRepo });
            menuHelp.Name = "menuHelp";
            menuHelp.Size = new Size(102, 36);
            menuHelp.Text = "帮助(&H)";
            // 
            // menuAbout
            // 
            menuAbout.Image = Properties.Resources.information;
            menuAbout.Name = "menuAbout";
            menuAbout.Size = new Size(247, 44);
            menuAbout.Text = Strings.About;
            menuAbout.Click += MenuAbout_Click;
            // 
            // menuRepo
            // 
            menuRepo.Image = Properties.Resources.star;
            menuRepo.Name = "menuRepo";
            menuRepo.Size = new Size(247, 44);
            menuRepo.Text = Strings.GitHub_Repo;
            menuRepo.Click += MenuRepo_Click;
            // 
            // menuKindle
            // 
            menuKindle.Image = Properties.Resources.mobile_phone_with_arrow;
            menuKindle.Margin = new Padding(10, 0, 0, 0);
            menuKindle.Name = "menuKindle";
            menuKindle.Padding = new Padding(0);
            menuKindle.Size = new Size(222, 36);
            menuKindle.Text = " Kindle设备已连接";
            menuKindle.Visible = false;
            menuKindle.Click += MenuKindle_Click;
            menuKindle.MouseEnter += MenuKindle_MouseEnter;
            menuKindle.MouseLeave += MenuKindle_MouseLeave;
            // 
            // menuTheme
            // 
            menuTheme.Alignment = ToolStripItemAlignment.Right;
            menuTheme.AutoToolTip = true;
            menuTheme.CheckOnClick = true;
            menuTheme.DisplayStyle = ToolStripItemDisplayStyle.Image;
            menuTheme.Image = Properties.Resources.new_moon;
            menuTheme.Name = "menuTheme";
            menuTheme.Size = new Size(50, 36);
            menuTheme.Click += MenuTheme_Click;
            menuTheme.MouseEnter += MenuTheme_MouseEnter;
            menuTheme.MouseLeave += MenuTheme_MouseLeave;
            // 
            // menuLang
            // 
            menuLang.Alignment = ToolStripItemAlignment.Right;
            menuLang.BackgroundImageLayout = ImageLayout.Zoom;
            menuLang.DisplayStyle = ToolStripItemDisplayStyle.Image;
            menuLang.DropDownItems.AddRange(new ToolStripItem[] { menuLangSC, menuLangTC, menuLangEN, menuLangAuto });
            menuLang.Image = Properties.Resources.globe_with_meridians;
            menuLang.ImageTransparentColor = Color.Transparent;
            menuLang.Name = "menuLang";
            menuLang.Size = new Size(50, 36);
            menuLang.Text = Strings.Language;
            // 
            // menuLangSC
            // 
            menuLangSC.Name = "menuLangSC";
            menuLangSC.Size = new Size(213, 40);
            menuLangSC.Text = "简体中文";
            menuLangSC.Click += MenuLangSC_Click;
            // 
            // menuLangTC
            // 
            menuLangTC.Name = "menuLangTC";
            menuLangTC.Size = new Size(213, 40);
            menuLangTC.Text = "繁体中文";
            menuLangTC.Click += MenuLangTC_Click;
            // 
            // menuLangEN
            // 
            menuLangEN.Name = "menuLangEN";
            menuLangEN.Size = new Size(213, 40);
            menuLangEN.Text = "英文";
            menuLangEN.Click += MenuLangEN_Click;
            // 
            // menuLangAuto
            // 
            menuLangAuto.Name = "menuLangAuto";
            menuLangAuto.Size = new Size(213, 40);
            menuLangAuto.Text = "自动";
            menuLangAuto.Click += MenuLangAuto_Click;
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Alignment = ToolStripItemAlignment.Right;
            toolStripMenuItem1.Enabled = false;
            toolStripMenuItem1.Image = Properties.Resources.empty;
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(50, 36);
            // 
            // splitContainerMain
            // 
            splitContainerMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            splitContainerMain.Location = new Point(0, 39);
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
            splitContainerMain.Size = new Size(1096, 611);
            splitContainerMain.SplitterDistance = 286;
            splitContainerMain.TabIndex = 2;
            // 
            // tabControl
            // 
            tabControl.Alignment = TabAlignment.Bottom;
            tabControl.Appearance = TabAppearance.Buttons;
            tabControl.BorderColor = SystemColors.ControlDark;
            tabControl.Controls.Add(tabPageBooks);
            tabControl.Controls.Add(tabPageWords);
            tabControl.Dock = DockStyle.Fill;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.LineColor = SystemColors.Highlight;
            tabControl.Location = new Point(0, 0);
            tabControl.Margin = new Padding(0);
            tabControl.Name = "tabControl";
            tabControl.SelectedForeColor = SystemColors.HighlightText;
            tabControl.SelectedIndex = 0;
            tabControl.SelectTabColor = SystemColors.ControlLight;
            tabControl.ShowTabCloseButton = true;
            tabControl.Size = new Size(286, 611);
            tabControl.SizeMode = TabSizeMode.FillToRight;
            tabControl.TabCloseColor = SystemColors.ControlText;
            tabControl.TabColor = SystemColors.ControlLight;
            tabControl.TabIndex = 0;
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            // 
            // tabPageBooks
            // 
            tabPageBooks.BackColor = SystemColors.ControlLight;
            tabPageBooks.Controls.Add(treeViewBooks);
            tabPageBooks.Location = new Point(4, 4);
            tabPageBooks.Margin = new Padding(0);
            tabPageBooks.Name = "tabPageBooks";
            tabPageBooks.Size = new Size(278, 0);
            tabPageBooks.TabIndex = 0;
            tabPageBooks.Text = Strings.Clippings;
            // 
            // treeViewBooks
            // 
            treeViewBooks.BorderStyle = BorderStyle.None;
            treeViewBooks.ContextMenuStrip = menuClippings;
            treeViewBooks.Dock = DockStyle.Fill;
            treeViewBooks.FullRowSelect = true;
            treeViewBooks.HideSelection = false;
            treeViewBooks.ImageIndex = 0;
            treeViewBooks.ImageList = imageListBooks;
            treeViewBooks.Location = new Point(0, 0);
            treeViewBooks.Margin = new Padding(0);
            treeViewBooks.Name = "treeViewBooks";
            treeViewBooks.SelectedImageIndex = 1;
            treeViewBooks.ShowLines = false;
            treeViewBooks.ShowNodeToolTips = true;
            treeViewBooks.ShowPlusMinus = false;
            treeViewBooks.ShowRootLines = false;
            treeViewBooks.Size = new Size(278, 0);
            treeViewBooks.StateImageList = imageListBooks;
            treeViewBooks.TabIndex = 0;
            treeViewBooks.NodeMouseClick += TreeViewBooks_NodeMouseClick;
            treeViewBooks.NodeMouseDoubleClick += TreeViewBooks_NodeMouseDoubleClick;
            treeViewBooks.KeyDown += TreeViewBooks_KeyDown;
            treeViewBooks.MouseDown += TreeViewBooks_MouseDown;
            // 
            // menuClippings
            // 
            menuClippings.BackColor = Color.Transparent;
            menuClippings.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
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
            tabPageWords.BackColor = SystemColors.ControlLight;
            tabPageWords.Controls.Add(treeViewWords);
            tabPageWords.Location = new Point(4, 4);
            tabPageWords.Margin = new Padding(0);
            tabPageWords.Name = "tabPageWords";
            tabPageWords.Size = new Size(278, 0);
            tabPageWords.TabIndex = 1;
            tabPageWords.Text = Strings.Vocabulary_List;
            // 
            // treeViewWords
            // 
            treeViewWords.BorderStyle = BorderStyle.None;
            treeViewWords.ContextMenuStrip = menuBooks;
            treeViewWords.Dock = DockStyle.Fill;
            treeViewWords.FullRowSelect = true;
            treeViewWords.HideSelection = false;
            treeViewWords.ImageIndex = 1;
            treeViewWords.ImageList = imageListWords;
            treeViewWords.Location = new Point(0, 0);
            treeViewWords.Name = "treeViewWords";
            treeViewWords.SelectedImageIndex = 0;
            treeViewWords.ShowLines = false;
            treeViewWords.ShowPlusMinus = false;
            treeViewWords.ShowRootLines = false;
            treeViewWords.Size = new Size(278, 0);
            treeViewWords.TabIndex = 0;
            treeViewWords.NodeMouseClick += TreeViewWords_NodeMouseClick;
            treeViewWords.MouseDown += TreeViewWords_MouseDown;
            // 
            // menuBooks
            // 
            menuBooks.BackColor = Color.Transparent;
            menuBooks.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
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
            splitContainerDetail.Panel1MinSize = 100;
            // 
            // splitContainerDetail.Panel2
            // 
            splitContainerDetail.Panel2.Controls.Add(tableLayoutPanel);
            splitContainerDetail.Panel2MinSize = 200;
            splitContainerDetail.Size = new Size(806, 611);
            splitContainerDetail.SplitterDistance = 308;
            splitContainerDetail.TabIndex = 1;
            // 
            // dataGridView
            // 
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.AllowUserToResizeRows = false;
            dataGridView.BackgroundColor = SystemColors.Window;
            dataGridView.BorderStyle = BorderStyle.None;
            dataGridView.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            dataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle5.BackColor = SystemColors.Window;
            dataGridViewCellStyle5.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            dataGridViewCellStyle5.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = DataGridViewTriState.False;
            dataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            dataGridView.ColumnHeadersHeight = 46;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridView.Dock = DockStyle.Fill;
            dataGridView.EditMode = DataGridViewEditMode.EditProgrammatically;
            dataGridView.GridColor = SystemColors.Control;
            dataGridView.Location = new Point(0, 0);
            dataGridView.Margin = new Padding(0);
            dataGridView.Name = "dataGridView";
            dataGridView.ReadOnly = true;
            dataGridView.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridView.RowHeadersVisible = false;
            dataGridView.RowHeadersWidth = 82;
            dataGridView.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridView.RowTemplate.ReadOnly = true;
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView.ShowCellErrors = false;
            dataGridView.ShowEditingIcon = false;
            dataGridView.ShowRowErrors = false;
            dataGridView.Size = new Size(806, 308);
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
            tableLayoutPanel.Controls.Add(lblLocation, 0, 1);
            tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
            tableLayoutPanel.Controls.Add(lblContent, 0, 2);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Margin = new Padding(0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 3;
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanel.Size = new Size(806, 299);
            tableLayoutPanel.TabIndex = 0;
            tableLayoutPanel.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // lblLocation
            // 
            lblLocation.AutoSize = true;
            lblLocation.BackColor = Color.Transparent;
            lblLocation.Font = new Font("微软雅黑", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblLocation.Location = new Point(2, 51);
            lblLocation.Margin = new Padding(2, 10, 0, 10);
            lblLocation.Name = "lblLocation";
            lblLocation.Size = new Size(0, 31);
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
            flowLayoutPanel.Font = new Font("微软雅黑", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            flowLayoutPanel.Location = new Point(0, 10);
            flowLayoutPanel.Margin = new Padding(0, 10, 0, 0);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Size = new Size(806, 31);
            flowLayoutPanel.TabIndex = 3;
            flowLayoutPanel.MouseDoubleClick += FlowLayoutPanel_MouseDoubleClick;
            // 
            // lblBook
            // 
            lblBook.AutoSize = true;
            lblBook.Font = new Font("Microsoft YaHei UI", 10.125F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblBook.Location = new Point(2, 0);
            lblBook.Margin = new Padding(2, 0, 0, 0);
            lblBook.Name = "lblBook";
            lblBook.Size = new Size(0, 31);
            lblBook.TabIndex = 0;
            lblBook.MouseDoubleClick += LblBook_MouseDoubleClick;
            // 
            // lblAuthor
            // 
            lblAuthor.AutoSize = true;
            lblAuthor.Font = new Font("微软雅黑", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblAuthor.Location = new Point(5, 0);
            lblAuthor.Name = "lblAuthor";
            lblAuthor.Size = new Size(0, 31);
            lblAuthor.TabIndex = 1;
            lblAuthor.MouseDoubleClick += LblAuthor_MouseDoubleClick;
            // 
            // lblContent
            // 
            lblContent.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblContent.BackColor = SystemColors.Window;
            lblContent.BorderStyle = BorderStyle.None;
            lblContent.ContextMenuStrip = menuContent;
            lblContent.Font = new Font("微软雅黑", 9.857143F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblContent.Location = new Point(5, 97);
            lblContent.Margin = new Padding(5, 5, 5, 20);
            lblContent.Name = "lblContent";
            lblContent.ReadOnly = true;
            lblContent.ScrollBars = RichTextBoxScrollBars.Vertical;
            lblContent.Size = new Size(796, 212);
            lblContent.TabIndex = 4;
            lblContent.Text = "";
            lblContent.MouseDoubleClick += LblContent_MouseDoubleClick;
            // 
            // menu
            // 
            menu.BackColor = Color.Transparent;
            menu.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            menu.ImageScalingSize = new Size(32, 32);
            menu.Items.AddRange(new ToolStripItem[] { menuListRefresh });
            menu.Name = "menu";
            menu.Size = new Size(127, 38);
            // 
            // menuListRefresh
            // 
            menuListRefresh.Name = "menuListRefresh";
            menuListRefresh.Size = new Size(126, 34);
            menuListRefresh.Text = "刷新";
            menuListRefresh.Click += MenuListRefresh_Click;
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
            statusStrip.AllowMerge = false;
            statusStrip.AutoSize = false;
            statusStrip.BackgroundImageLayout = ImageLayout.None;
            statusStrip.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            statusStrip.GripMargin = new Padding(0);
            statusStrip.ImageScalingSize = new Size(28, 28);
            statusStrip.Items.AddRange(new ToolStripItem[] { lblCount, lblBookCount, progressBar });
            statusStrip.Location = new Point(0, 650);
            statusStrip.Name = "statusStrip";
            statusStrip.RenderMode = ToolStripRenderMode.Professional;
            statusStrip.Size = new Size(1096, 36);
            statusStrip.SizingGrip = false;
            statusStrip.TabIndex = 3;
            // 
            // lblCount
            // 
            lblCount.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblCount.Image = Properties.Resources.keycap_number_sign;
            lblCount.Margin = new Padding(0);
            lblCount.Name = "lblCount";
            lblCount.Size = new Size(28, 36);
            // 
            // lblBookCount
            // 
            lblBookCount.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblBookCount.Image = Properties.Resources.input_latin_uppercase;
            lblBookCount.Margin = new Padding(20, 0, 0, 0);
            lblBookCount.Name = "lblBookCount";
            lblBookCount.Size = new Size(28, 36);
            lblBookCount.Visible = false;
            // 
            // progressBar
            // 
            progressBar.Alignment = ToolStripItemAlignment.Right;
            progressBar.AutoSize = false;
            progressBar.Enabled = false;
            progressBar.Margin = new Padding(100, 5, 100, 5);
            progressBar.MarqueeAnimationSpeed = 50;
            progressBar.Name = "progressBar";
            progressBar.Overflow = ToolStripItemOverflow.Never;
            progressBar.Size = new Size(300, 26);
            progressBar.Step = 1;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TextDirection = ToolStripTextDirection.Vertical270;
            progressBar.Visible = false;
            // 
            // timer
            // 
            timer.Enabled = true;
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            // 
            // menuContent
            // 
            menuContent.BackColor = Color.Transparent;
            menuContent.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            menuContent.ImageScalingSize = new Size(28, 28);
            menuContent.Items.AddRange(new ToolStripItem[] { menuContentCopy });
            menuContent.Name = "menuClippings";
            menuContent.Size = new Size(127, 38);
            // 
            // menuContentCopy
            // 
            menuContentCopy.Name = "menuContentCopy";
            menuContentCopy.ShortcutKeyDisplayString = "";
            menuContentCopy.Size = new Size(270, 34);
            menuContentCopy.Text = Strings.Copy;
            menuContentCopy.Click += menuContentCopy_Click;
            // 
            // FrmMain
            // 
            AutoScaleMode = AutoScaleMode.None;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = SystemColors.Window;
            ClientSize = new Size(1096, 686);
            Controls.Add(statusStrip);
            Controls.Add(splitContainerMain);
            Controls.Add(menuStrip);
            DoubleBuffered = true;
            Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "FrmMain";
            SizeGripStyle = SizeGripStyle.Show;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Kindle Mate 2";
            FormClosing += FrmMain_FormClosing;
            Load += FrmMain_Load;
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            splitContainerMain.Panel1.ResumeLayout(false);
            splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
            splitContainerMain.ResumeLayout(false);
            tabControl.ResumeLayout(false);
            tabPageBooks.ResumeLayout(false);
            menuClippings.ResumeLayout(false);
            tabPageWords.ResumeLayout(false);
            menuBooks.ResumeLayout(false);
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
            menu.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            menuContent.ResumeLayout(false);
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
        private FlatTabControl tabControl;
        private TabPage tabPageBooks;
        private TabPage tabPageWords;
        private TreeView treeViewBooks;
        private TreeView treeViewWords;
        private ImageList imageListWords;
        private ToolStripProgressBar progressBar;
        private ToolStripMenuItem menuImportKindleWords;
        private ToolStripMenuItem menuRestart;
        private ToolStripMenuItem menuLang;
        private RichTextBox lblContent;
        private ToolStripMenuItem menuClean;
        private ToolStripMenuItem menuExportMd;
        private ToolStripMenuItem menuStatistic;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem menuTheme;
        private ToolStripMenuItem menuLangEN;
        private ToolStripMenuItem menuLangSC;
        private ToolStripMenuItem menuLangTC;
        private ToolStripMenuItem menuLangAuto;
        private ContextMenuStrip menu;
        private ToolStripMenuItem menuListRefresh;
        private ContextMenuStrip menuContent;
        private ToolStripMenuItem menuContentCopy;
    }
}
