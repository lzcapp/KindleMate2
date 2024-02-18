using System.ComponentModel;
using System.Data;

namespace KindleMate2 {
    public partial class FrmMain : Form {
        private DataTable _dataTable = new();

        private readonly StaticData _staticData = new();

        public FrmMain() {
            InitializeComponent();
        }

        private void FrmMain_Load(object? sender, EventArgs e) {
            if (!FileHandler()) {
                return;
            }

            _dataTable = _staticData.GetClipingsDataTable();

            CountRows();

            dataGridView.DataSource = _dataTable;

            dataGridView.Columns["key"].Visible = false;
            dataGridView.Columns["content"].HeaderText = "Content";
            dataGridView.Columns["bookname"].HeaderText = "Book";
            dataGridView.Columns["authorname"].HeaderText = "Author";
            dataGridView.Columns["brieftype"].Visible = false;
            dataGridView.Columns["clippingtypelocation"].HeaderText = "Location";
            dataGridView.Columns["clippingdate"].HeaderText = "Date";
            dataGridView.Columns["read"].Visible = false;
            dataGridView.Columns["clipping_importdate"].Visible = false;
            dataGridView.Columns["tag"].Visible = false;
            dataGridView.Columns["sync"].Visible = false;
            dataGridView.Columns["newbookname"].Visible = false;
            dataGridView.Columns["colorRGB"].Visible = false;
            dataGridView.Columns["pagenumber"].HeaderText = "Page";

            foreach (DataGridViewColumn column in dataGridView.Columns) {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            dataGridView.Sort(dataGridView.Columns["clippingdate"], ListSortDirection.Descending);

            var bookNames = _dataTable.AsEnumerable().Select(row => row.Field<string>("bookname")).Distinct();

            var rootNode = new TreeNode("全部") {
                ImageIndex = 2, SelectedImageIndex = 2
            };

            treeView.Nodes.Add(rootNode);

            foreach (var bookName in bookNames) {
                rootNode.Nodes.Add(bookName);
            }

            treeView.ExpandAll();
        }

        private void CountRows() {
            lblCount.Text = "共 " + _staticData.GetClippingsCount() + " 条记录";
        }

        private static bool FileHandler() {
            var programsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(programsDirectory, "KM2.dat");

            if (File.Exists(filePath)) {
                return true;
            }

            var fileDialog = new OpenFileDialog {
                InitialDirectory = programsDirectory,
                Title = "Import Kindle Mate Data File (KM2.dat)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "dat",
                Filter = @"KM2 Data Files (*.dat)|*.dat",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) return false;

            var selectedFilePath = fileDialog.FileName;
            try {
                File.Copy(selectedFilePath, filePath, true);
            } catch (Exception) {
                return false;
            }

            return true;
        }

        private void DataGridView_SelectionChanged(object sender, EventArgs e) {
            if (dataGridView.CurrentRow == null) {
                return;
            }

            DataGridViewRow selectedRow = dataGridView.CurrentRow;

            if (selectedRow == null) {
                return;
            }

            var bookname = selectedRow.Cells["bookname"].Value.ToString();
            var authorname = selectedRow.Cells["authorname"].Value.ToString();
            var clippinglocation = selectedRow.Cells["clippingtypelocation"].Value.ToString();
            var pagenumber = selectedRow.Cells["pagenumber"].Value.ToString();
            var content = selectedRow.Cells["content"].Value.ToString();

            lblBook.Text = bookname;
            lblAuthor.Text = "（" + authorname + "）";
            lblLocation.Text = clippinglocation + " （第" + pagenumber + "页）";
            lblContent.Text = content;
        }

        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text is "Select All" or "全部") {
                dataGridView.DataSource = _dataTable;
                dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                dataGridView.Sort(dataGridView.Columns["clippingdate"], ListSortDirection.Descending);
            } else {
                var selectedBookName = e.Node.Text;
                DataTable filteredBooks = _dataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == selectedBookName).CopyToDataTable();
                dataGridView.DataSource = filteredBooks;
                dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                dataGridView.Sort(dataGridView.Columns["clippingtypelocation"], ListSortDirection.Ascending);
            }
        }

        private void TreeView_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            var clickPoint = new Point(e.X, e.Y);
            TreeNode currentNode = treeView.GetNodeAt(clickPoint);
            if (currentNode == null) {
                return;
            }

            currentNode.ContextMenuStrip = menuBooks;
            treeView.SelectedNode = currentNode;
        }

        private void LblContent_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowContentEditDialog();
        }

        private void ShowContentEditDialog() {
            using var dialog = new FrmEdit();
            dialog.LblBook = lblBook.Text;
            dialog.TxtContent = lblContent.Text;
            if (dialog.ShowDialog() == DialogResult.OK) {
                lblContent.Text = dialog.TxtContent;
            }
        }

        private void DataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            ShowContentEditDialog();
        }

        private void DataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            dataGridView.CurrentCell = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
            Point location = dataGridView.PointToClient(Cursor.Position);
            menuClippings.Show(dataGridView, location);
        }

        private void ClippingMenuDelete_Click(object sender, EventArgs e) {
            if (dataGridView.SelectedRows.Count <= 0) {
                return;
            }

            DialogResult result = MessageBox.Show("Are you sure you want to delete the selected row(s)?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) {
                return;
            }

            foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                if (_staticData.DeleteClippingsByKey(row.Cells["key"].Value.ToString() ?? string.Empty)) {
                    dataGridView.Rows.Remove(row);
                }
            }

            CountRows();
        }

        private void ClippingMenuCopy_Click(object sender, EventArgs e) {
            if (dataGridView.CurrentRow is null) {
                return;
            }

            var content = dataGridView.CurrentRow.Cells["content"].Value.ToString() ?? string.Empty;
            Clipboard.SetText(content != string.Empty ? content : lblContent.Text);
        }

        private void DataGridView_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode != Keys.Enter) {
                return;
            }

            ShowContentEditDialog();
            e.Handled = true;
        }

        private void BooksMenuDelete_Click(object sender, EventArgs e) {
            if (treeView.SelectedNode is null) {
                return;
            }

            DialogResult result = MessageBox.Show("Are you sure you want to delete the selected book?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) {
                return;
            }

            var bookname = treeView.SelectedNode.Text;
            if (_staticData.DeleteClippingsByBook(bookname)) {
                CountRows();
            }
        }

        private void ToolStripMenuExit_Click(object sender, EventArgs e) {
            Close();
        }

        private void ToolStripMenuAbout_Click(object sender, EventArgs e) {
            using var dialog = new FrmAboutBox();
            dialog.ShowDialog();
        }
    }
}