namespace KindleMate2 {
    public partial class FrmEdit : Form {
        private string _content = "";

        public FrmEdit() {
            InitializeComponent();
        }

        public string LblBook {
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
            Text = Strings.Edit_Clippings;
            btnOK.Text = Strings.Save;
            btnCancel.Text = Strings.Cancel;

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