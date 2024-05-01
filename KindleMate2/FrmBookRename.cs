using KindleMate2.DarkModeForms;

namespace KindleMate2 {
    public partial class FrmBookRename : Form {
        // ReSharper disable once NotAccessedField.Local
        #pragma warning disable IDE0052 // 删除未读的私有成员
        private readonly DarkModeCS _dm = null!;
        #pragma warning restore IDE0052 // 删除未读的私有成员

        private readonly StaticData _staticData = new();

        private string _bookname = "";
        private string _authorname = "";

        public FrmBookRename() {
            InitializeComponent();

            if (_staticData.IsDarkTheme()) {
                _dm = new DarkModeCS(this);
            }
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
            Text = Strings.Rename;
            btnOK.Text = Strings.Save;
            btnCancel.Text = Strings.Cancel;

            txtBook.Focus();
            btnOK.Enabled = false;
        }

        private bool IsRevised() {
            return (_bookname.Trim() != txtBook.Text.Trim() || _authorname.Trim() != txtAuthor.Text.Trim()) && !string.IsNullOrWhiteSpace(txtBook.Text);
        }
    }
}