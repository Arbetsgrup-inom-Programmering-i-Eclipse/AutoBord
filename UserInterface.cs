using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VMS.TPS.Common.Model.API;

namespace MoveUOTable
{
    public partial class UserInterface : Form
    {
        private CouchType couchThickness;

        public CouchType CouchThickness
        {
            get { return couchThickness; }
            set { couchThickness = value; }
        }

        public UserInterface()
        {
            InitializeComponent();
            InitilizeUI();
        }
        private void InitilizeUI()
        {
            cbCouchSelection.DataSource = Enum.GetValues(typeof(CouchType));
            cbCouchSelection.SelectedIndex = 0;
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            CouchThickness = (CouchType)cbCouchSelection.SelectedItem;
        }
    }
}
