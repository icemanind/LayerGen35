using System;
using System.Windows.Forms;

namespace LayerGen35
{
    public partial class DeleteProfileDialog : Form
    {
        public DeleteProfileDialog()
        {
            InitializeComponent();
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Yes;
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.No;
        }
    }
}
