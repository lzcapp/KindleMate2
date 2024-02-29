namespace KindleMate2 {
    public partial class FrmEdit : Form {
        private string _content = "";

        public FrmEdit() {
            InitializeComponent();
        }

        public string LblBook {
            get => lblBook.Text.Trim();
            set => lblBook.Text = value.Trim();
        }

        public string TxtContent {
            get => txtContent.Text;
            set {
                txtContent.Text = value.Trim();
                _content = value.Trim();
            }
        }

        private void FrmEdit_Load(object sender, EventArgs e) {
            txtContent.Focus();
            btnOK.Enabled = false;
        }

        private void BtnOK_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void TxtContent_TextChanged(object sender, EventArgs e) {
            btnOK.Enabled = _content.Trim() != txtContent.Text.Trim();
        }
    }
}