using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnergyBalanceSolver
{
    public partial class Progress : Form
    {
        private readonly System.Threading.CancellationTokenSource userCts;

        public Progress(System.Threading.CancellationTokenSource userCts)
        {
            InitializeComponent();
            this.userCts = userCts;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            userCts.Cancel();
        }

        internal void UpdateLabel(string v)
        {
            lblStep.BeginInvoke(new Action(() => lblStep.Text = v));
        }
    }
}
