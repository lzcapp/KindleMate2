﻿using DarkModeForms;

namespace KindleMate2 {
    public partial class FrmBookRename : Form {
        private readonly StaticData _staticData = new();

        private string _bookname = "";
        private string _authorname = "";

        public FrmBookRename() {
            InitializeComponent();

            if (_staticData.IsDarkTheme()) {
                _ = new DarkModeCS(this, false);
            }

            //Console.Write(_dm);
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

            txtBook.AutoCompleteCustomSource.AddRange(_staticData.GetClippingsBookTitleList().ToArray());
            txtAuthor.AutoCompleteCustomSource.AddRange(_staticData.GetClippingsAuthorList().ToArray());
        }

        private bool IsRevised() {
            return (_bookname.Trim() != txtBook.Text.Trim() || _authorname.Trim() != txtAuthor.Text.Trim()) && !string.IsNullOrWhiteSpace(txtBook.Text);
        }
    }
}