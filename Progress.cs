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

        public double TotalSolutions;
        
        public double CompletedCount;

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
           
            if (TotalSolutions > 0)
            {
                var percent = (CompletedCount / TotalSolutions) * 100;
                if (percent <= 100) //skip this update if it seems out of whack...
                {
                    progressBar1.Value = (int)percent;
                    lblPercent.Text = $"{Math.Round(percent, 2)}%";
                }
            } else
            {
                lblPercent.Text = "0%";
                progressBar1.Value = 0;
            }
        }

        const int EasyThreshold = 125_000_000;
        const int MediumThreshold = 250_000_000;
        const int HardThreshold = 325_000_000;
        const int HarderThreshold = 400_000_000;
        const int ImpossibleThreshold = 500_000_000;

        internal void SetDificulty(int v)
        {
            //Rudimentary difficulty levels
            // 0 -> 125M - Easy
            // 125 -> 250M - Medium
            // 250 -> 325M - Hard
            // 325 -> 400M - Harder
            //> 500M - Impossible

            lblDifficulty.BeginInvoke(new Action(() =>
            {
                if(v == -1)
                {
                    lblDifficulty.Text = "Calculating";
                    lblDifficulty.ForeColor = Color.Black;
                }
                else if( v >= MediumThreshold )
                {
                    lblDifficulty.ForeColor = Color.DarkRed;
                    if (v >= ImpossibleThreshold)
                    {
                        lblDifficulty.Text = "Insane";
                    }
                    else if(v >= HarderThreshold)
                    {
                        lblDifficulty.Text = "Even Harder";
                    }
                    else if(v >= HardThreshold)
                    {
                        lblDifficulty.Text = "Harder";
                    }
                    else
                    {
                        lblDifficulty.Text = "Hard";
                    }
                } else
                {
                    lblDifficulty.ForeColor = Color.DarkGreen;
                    if(v >= EasyThreshold)
                    {
                        lblDifficulty.Text = "Medium";
                    }
                    else
                    {
                        lblDifficulty.Text = "Easy";
                    }
                }
            }));
        }
        
    }
}
