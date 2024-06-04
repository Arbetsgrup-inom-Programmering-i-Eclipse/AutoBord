using VMS.TPS.Common.Model.API;

namespace FormProgressBar
{
    public partial class ProgressBar : System.Windows.Forms.Form
    {
        public ProgressBar(Image CT)
        {
            InitializeComponent();
            progressBar1.Maximum = CT.ZSize + 1; //One extra steps for moving User Origin
        }

        public void ChangeProgressBarValue ()
        {
            progressBar1.Value = progressBar1.Value + 1;
            this.Update();
        }

        public void ChangeLabel(string newtext)
        {
            label1.Text = newtext;
            this.Update();
        }
    }
}
