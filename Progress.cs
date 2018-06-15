using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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

        private Stopwatch timer = new Stopwatch();

        public int TotalSolutions { get
            {
                return totalSolutions;
            }
            set
            {
                totalSolutions = value;
                progressBar1.BeginInvoke(new Action(() => progressBar1.Maximum = totalSolutions));
            }
        }

        private int totalSolutions;
        public int CompletedCount;

        public Progress(System.Threading.CancellationTokenSource userCts)
        {
            InitializeComponent();
            this.userCts = userCts;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            timer.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            userCts.Cancel();
        }

        internal void UpdateLabel(string v)
        {
            lblStep.BeginInvoke(new Action(() => lblStep.Text = v));
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            lblElapsed.Text = timer.Elapsed.ToString("hh\\:mm\\:ss");
            progressBar1.Value = CompletedCount;
            if (totalSolutions > 0)
            {
                lblPercent.Text = $"{Math.Round((CompletedCount / (double)TotalSolutions) * 100, 2)}%";
            }
        }
    }
}
