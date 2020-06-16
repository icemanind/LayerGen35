using System.Windows.Forms;

namespace LayerGen35
{
    public partial class PleaseWaitForm : Form
    {
        public ProgressBar ProgressBar
        {
            get { return pbProgress; }
        }

        public PleaseWaitForm()
        {
            InitializeComponent();
        }
    }
}
