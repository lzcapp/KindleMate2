using KindleMate2.DarkModeForms;

namespace KindleMate2 {
    public partial class FrmCombine : Form {
        // ReSharper disable once NotAccessedField.Local
        #pragma warning disable IDE0052 // 删除未读的私有成员
        private readonly DarkModeCS _dm = null!;
        #pragma warning restore IDE0052 // 删除未读的私有成员

        private readonly StaticData _staticData = new();

        public FrmCombine() {
            InitializeComponent();

            if (_staticData.IsDarkTheme()) {
                _dm = new DarkModeCS(this);
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