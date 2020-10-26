namespace DiskSpace_Examiner
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.btnSelectFolder = new System.Windows.Forms.Button();
            this.lblFolderName = new System.Windows.Forms.Label();
            this.PieChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.GUITimer = new System.Windows.Forms.Timer(this.components);
            this.lblScanStatus = new System.Windows.Forms.Label();
            this.cbRelativeToDisk = new System.Windows.Forms.CheckBox();
            this.cbShowFreeSpace = new System.Windows.Forms.CheckBox();
            this.lblInstructions = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.PieChart)).BeginInit();
            this.SuspendLayout();
            // 
            // btnSelectFolder
            // 
            this.btnSelectFolder.Location = new System.Drawing.Point(16, 15);
            this.btnSelectFolder.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnSelectFolder.Name = "btnSelectFolder";
            this.btnSelectFolder.Size = new System.Drawing.Size(1340, 33);
            this.btnSelectFolder.TabIndex = 0;
            this.btnSelectFolder.Text = "Select &Folder...";
            this.btnSelectFolder.UseVisualStyleBackColor = true;
            this.btnSelectFolder.Click += new System.EventHandler(this.btnSelectFolder_Click);
            // 
            // lblFolderName
            // 
            this.lblFolderName.AutoSize = true;
            this.lblFolderName.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFolderName.Location = new System.Drawing.Point(16, 52);
            this.lblFolderName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblFolderName.Name = "lblFolderName";
            this.lblFolderName.Size = new System.Drawing.Size(138, 25);
            this.lblFolderName.TabIndex = 1;
            this.lblFolderName.Text = "lblFolderName";
            this.lblFolderName.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // PieChart
            // 
            chartArea1.Name = "MainChartArea";
            this.PieChart.ChartAreas.Add(chartArea1);
            legend1.DockedToChartArea = "MainChartArea";
            legend1.Name = "Legend1";
            this.PieChart.Legends.Add(legend1);
            this.PieChart.Location = new System.Drawing.Point(16, 118);
            this.PieChart.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.PieChart.Name = "PieChart";
            series1.ChartArea = "MainChartArea";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Doughnut;
            series1.Legend = "Legend1";
            series1.Name = "PieSeries";
            this.PieChart.Series.Add(series1);
            this.PieChart.Size = new System.Drawing.Size(1340, 469);
            this.PieChart.TabIndex = 2;
            this.PieChart.Text = "chart1";
            // 
            // GUITimer
            // 
            this.GUITimer.Enabled = true;
            this.GUITimer.Interval = 2000;
            this.GUITimer.Tick += new System.EventHandler(this.GUITimer_Tick);
            // 
            // lblScanStatus
            // 
            this.lblScanStatus.AutoSize = true;
            this.lblScanStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblScanStatus.Location = new System.Drawing.Point(16, 601);
            this.lblScanStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblScanStatus.Name = "lblScanStatus";
            this.lblScanStatus.Size = new System.Drawing.Size(34, 17);
            this.lblScanStatus.TabIndex = 3;
            this.lblScanStatus.Text = "Idle.";
            this.lblScanStatus.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // cbRelativeToDisk
            // 
            this.cbRelativeToDisk.AutoSize = true;
            this.cbRelativeToDisk.Checked = true;
            this.cbRelativeToDisk.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbRelativeToDisk.Location = new System.Drawing.Point(21, 90);
            this.cbRelativeToDisk.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cbRelativeToDisk.Name = "cbRelativeToDisk";
            this.cbRelativeToDisk.Size = new System.Drawing.Size(199, 21);
            this.cbRelativeToDisk.TabIndex = 4;
            this.cbRelativeToDisk.Text = "Show relative to entire disk";
            this.cbRelativeToDisk.UseVisualStyleBackColor = true;
            this.cbRelativeToDisk.CheckedChanged += new System.EventHandler(this.cbRelativeToDisk_CheckedChanged);
            // 
            // cbShowFreeSpace
            // 
            this.cbShowFreeSpace.AutoSize = true;
            this.cbShowFreeSpace.Checked = true;
            this.cbShowFreeSpace.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbShowFreeSpace.Location = new System.Drawing.Point(324, 90);
            this.cbShowFreeSpace.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cbShowFreeSpace.Name = "cbShowFreeSpace";
            this.cbShowFreeSpace.Size = new System.Drawing.Size(135, 21);
            this.cbShowFreeSpace.TabIndex = 5;
            this.cbShowFreeSpace.Text = "Show free space";
            this.cbShowFreeSpace.UseVisualStyleBackColor = true;
            this.cbShowFreeSpace.CheckedChanged += new System.EventHandler(this.cbShowFreeSpace_CheckedChanged);
            // 
            // lblInstructions
            // 
            this.lblInstructions.AutoSize = true;
            this.lblInstructions.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblInstructions.Location = new System.Drawing.Point(24, 652);
            this.lblInstructions.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(1333, 34);
            this.lblInstructions.TabIndex = 6;
            this.lblInstructions.Text = resources.GetString("lblInstructions.Text");
            this.lblInstructions.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1372, 695);
            this.Controls.Add(this.lblInstructions);
            this.Controls.Add(this.cbShowFreeSpace);
            this.Controls.Add(this.cbRelativeToDisk);
            this.Controls.Add(this.lblScanStatus);
            this.Controls.Add(this.PieChart);
            this.Controls.Add(this.lblFolderName);
            this.Controls.Add(this.btnSelectFolder);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "MainForm";
            this.Text = "DiskSpace-Examiner";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.PieChart)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnSelectFolder;
        private System.Windows.Forms.Label lblFolderName;
        private System.Windows.Forms.DataVisualization.Charting.Chart PieChart;
        private System.Windows.Forms.Timer GUITimer;
        private System.Windows.Forms.Label lblScanStatus;
        private System.Windows.Forms.CheckBox cbRelativeToDisk;
        private System.Windows.Forms.CheckBox cbShowFreeSpace;
        private System.Windows.Forms.Label lblInstructions;
    }
}

