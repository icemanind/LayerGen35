using System;
using System.Windows.Forms;

namespace LayerGen35
{
    public partial class SaveProfileDialog : Form
    {
        public string ProfileName { get { return txtProfileName.Text; } set { txtProfileName.Text = value; } }

        public SaveProfileDialog()
        {
            InitializeComponent();
        }

        private void SaveProfileDialog_Load(object sender, EventArgs e)
        {
            txtProfileName.Focus();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
