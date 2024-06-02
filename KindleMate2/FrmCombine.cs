using KindleMate2.DarkModeForms;

namespace KindleMate2 {
    public partial class FrmCombine : Form {
        private readonly StaticData _staticData = new();

        public FrmCombine() {
            InitializeComponent();

            if (_staticData.IsDarkTheme()) {
                _ = new DarkModeCS(this, false);
            }
        }

        public List<string> BookNames {
            set {
                comboBox.Items.Clear();
                foreach (var bookName in value) {
                    comboBox.Items.Add(bookName);
                }
            }
        }

        public string SelectedName {
            get => comboBox.SelectedItem?.ToString() ?? string.Empty;
        }

        private void FrmCombine_Load(object sender, EventArgs e) {
            comboBox.Focus();
        }

        private void BtnOK_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ComboBox_SelectedValueChanged(object sender, EventArgs e) {
            btnOK.Enabled = !string.IsNullOrWhiteSpace(comboBox.Text);
        }
    }
}