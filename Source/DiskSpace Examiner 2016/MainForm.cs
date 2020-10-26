
// TODO: Allow the display to be relative to disk or relative to ScanRoot.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace DiskSpace_Examiner_2016
{
    public partial class MainForm : Form
    {
        DiskScan CurrentScan;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {            
            btnSelectFolder.Width = ClientSize.Width - 2 * btnSelectFolder.Left;

            lblFolderName.Width = btnSelectFolder.Width;
            lblFolderName.Left = btnSelectFolder.Left;

            PieChart.Width = ClientSize.Width - 2 * PieChart.Left;
            PieChart.Height = ClientSize.Height - PieChart.Top - btnSelectFolder.Top - 3 * lblScanStatus.Height - lblInstructions.Height - btnSelectFolder.Top / 2;

            lblScanStatus.Top = PieChart.Bottom + lblScanStatus.Height;
            lblInstructions.Top = ClientSize.Height - lblInstructions.Height - lblScanStatus.Height;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            MainForm_Resize(null, null);

            PieChart.Series[0].Points.Clear();

            GUITimer_Tick(null, null);          // Perform initial GUI update
            Show();

            lblScanStatus.Text = "Loading previous disk scan results...";
            ScanResultFile.Load();
            
            btnSelectFolder_Click(null, null);
        }

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            if (CurrentScan != null) CurrentScan.Dispose();
            CurrentScan = new DiskScan(fbd.SelectedPath);
            
            GUITimer_Tick(null, null);          // Perform initial GUI update                       
        }

        class PieEntry { 
            public string Label; 
            public double GB;

            public PieEntry(string _Label, double _GB) { Label = _Label; GB = _GB; }
        }

        string SizeString(long bytes)
        {
            return " (" + (new DataSize(bytes).ToFriendlyString(DataSize.Gigabyte)) + ")";
        }

        private void GUITimer_Tick(object sender, EventArgs e)
        {
            if (CurrentScan == null) { PieChart.Visible = false; lblFolderName.Text = "No folder selected."; return; }
            PieChart.Visible = true;

            CurrentScan.CheckHealth();
            lock (CurrentScan)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(CurrentScan.FilesScanned + " files and " + CurrentScan.FoldersScanned + " folders scanned.  ");
                switch (CurrentScan.CurrentActivity)
                {
                    case DiskScan.Activities.ScanningFolders: sb.Append("Scanning folders..."); break;
                    case DiskScan.Activities.ScanningNewFolders: sb.Append("Scanning files in new folders..."); break;
                    case DiskScan.Activities.RescanningOldFolders: sb.Append("Rescanning files in folders from previous scans..."); break;
                    case DiskScan.Activities.CommittingPartialResults: sb.Append("Committing partial scan results to database..."); break;
                    case DiskScan.Activities.CommittingFinalResults: sb.Append("Committing final scan results to database..."); break;
                    case DiskScan.Activities.ScanComplete: sb.Append("  Scan complete."); break;
                }
                lblScanStatus.Text = sb.ToString();
            }
            
            PieChart.Series[0].Points.Clear();

            DirectorySummary TopSummary = CurrentScan.ScanRoot;
            if (TopSummary == null) { lblFolderName.Text = "Preparing scan..."; return; }

            List<PieEntry> Entries = new List<PieEntry>();

            const double GB = 1073741824;
            const double TooSmallThresholdFraction = 0.05;          // As a fraction of total size (this number sets the minimum pie slice size, which prevents overlapping text).            

            long Unaccounted;
            long TooSmall = 0;
            lock (TopSummary)
            {
                lblFolderName.Text = TopSummary.FullName;

                long TotalPieSize = 0;
                if (cbRelativeToDisk.Checked && cbShowFreeSpace.Checked) TotalPieSize = TopSummary.Drive.TotalSize;
                else if (cbRelativeToDisk.Checked) TotalPieSize = TopSummary.Drive.TotalSize - TopSummary.Drive.TotalFreeSpace;
                else
                {
                    foreach (DirectorySummary ds in TopSummary.Subfolders)
                    {
                        lock (ds) { TotalPieSize += ds.Size; }
                    }
                }

                Unaccounted = TopSummary.Drive.TotalSize;
                long TooSmallThreshold = (long)(TooSmallThresholdFraction * TotalPieSize);

                if (cbRelativeToDisk.Checked && cbShowFreeSpace.Checked)
                    Entries.Add(new PieEntry("Available Free Space" + SizeString(TopSummary.Drive.AvailableFreeSpace), TopSummary.Drive.AvailableFreeSpace / GB));
                Unaccounted -= TopSummary.Drive.AvailableFreeSpace;
                
                foreach (DirectorySummary ds in TopSummary.Subfolders)
                {
                    lock (ds)
                    {
                        if (ds.Size < TooSmallThreshold) { TooSmall += ds.Size; continue; }
                        Entries.Add(new PieEntry(ds.Name + SizeString(ds.Size), ds.Size / GB));
                        Unaccounted -= ds.Size;
                    }
                }                                
            }

            DataPoint dp;
            dp = new DataPoint(2.0, TooSmall / GB); dp.Label = "Other (Smaller Folders)" + SizeString(TooSmall); PieChart.Series[0].Points.Add(dp); 
            Unaccounted -= TooSmall;
            
            if (cbRelativeToDisk.Checked)
            {
                dp = new DataPoint(3.0, Unaccounted / GB);
                if (CurrentScan.IsScanComplete)
                    dp.Label = "Outside this directory or inaccessible" + SizeString(Unaccounted);
                else
                    dp.Label = "Still Scanning...";
                PieChart.Series[0].Points.Add(dp);
            }            

            foreach (PieEntry pe in Entries)
            {
                dp = new DataPoint(0.0, pe.GB); 
                dp.Label = pe.Label; 
                PieChart.Series[0].Points.Add(dp); 
            }
        }

        private void cbRelativeToDisk_CheckedChanged(object sender, EventArgs e)
        {
            GUITimer_Tick(null, null);
            cbShowFreeSpace.Enabled = cbRelativeToDisk.Checked;
        }

        private void cbShowFreeSpace_CheckedChanged(object sender, EventArgs e)
        {
            GUITimer_Tick(null, null);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (CurrentScan != null) CurrentScan.Dispose();
        }
    }
}
