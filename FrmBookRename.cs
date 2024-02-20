namespace KindleMate2 {
    public partial class FrmBookRename : Form {
        private string _bookname = "";
        private string _authorname = "";

        public FrmBookRename() {
            InitializeComponent();
        }

        public string TxtBook {
            get => txtBook.Text.Trim();
            set {
                txtBook.Text = value.Trim();
                _bookname = value.Trim();
            }
        }

        public string TxtAuthor {
            get => txtAuthor.Text;
            set {
                txtAuthor.Text = value.Trim();
                _authorname = value.Trim();
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void BtnOK_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void TxtBook_TextChanged(object sender, EventArgs e) {
            btnOK.Enabled = IsRevised();
        }

        private void TxtAuthor_TextChanged(object sender, EventArgs e) {
            btnOK.Enabled = IsRevised();
        }

        private void FrmBookRename_Load(object sender, EventArgs e) {
            txtBook.Focus();
            btnOK.Enabled = false;
        }

        private bool IsRevised() {
            return _bookname.Trim() != txtBook.Text.Trim() || _authorname.Trim() != txtAuthor.Text.Trim();
        }
    }
}