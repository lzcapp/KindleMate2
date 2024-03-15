namespace KindleMate2 {
    partial class FrmStatistic {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea2 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend2 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea3 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend3 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series3 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea4 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend4 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series4 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea5 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend5 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series5 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea6 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend6 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series6 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmStatistic));
            tabControl = new TabControl();
            tabPageBooks = new TabPage();
            tableLayoutPanelBooks = new TableLayoutPanel();
            chartBooksTime = new System.Windows.Forms.DataVisualization.Charting.Chart();
            chartBooksWeek = new System.Windows.Forms.DataVisualization.Charting.Chart();
            chartBooksHistory = new System.Windows.Forms.DataVisualization.Charting.Chart();
            tabPageVocabs = new TabPage();
            tableLayoutPanelVocabs = new TableLayoutPanel();
            chartVocabsTime = new System.Windows.Forms.DataVisualization.Charting.Chart();
            chartVocabsWeek = new System.Windows.Forms.DataVisualization.Charting.Chart();
            chartVocabsHistory = new System.Windows.Forms.DataVisualization.Charting.Chart();
            tabControl.SuspendLayout();
            tabPageBooks.SuspendLayout();
            tableLayoutPanelBooks.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)chartBooksTime).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chartBooksWeek).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chartBooksHistory).BeginInit();
            tabPageVocabs.SuspendLayout();
            tableLayoutPanelVocabs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)chartVocabsTime).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chartVocabsWeek).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chartVocabsHistory).BeginInit();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Alignment = TabAlignment.Bottom;
            tabControl.Controls.Add(tabPageBooks);
            tabControl.Controls.Add(tabPageVocabs);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Margin = new Padding(2);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(724, 394);
            tabControl.TabIndex = 0;
            // 
            // tabPageBooks
            // 
            tabPageBooks.BackColor = SystemColors.Window;
            tabPageBooks.Controls.Add(tableLayoutPanelBooks);
            tabPageBooks.Location = new Point(4, 4);
            tabPageBooks.Margin = new Padding(2);
            tabPageBooks.Name = "tabPageBooks";
            tabPageBooks.Padding = new Padding(2);
            tabPageBooks.Size = new Size(716, 361);
            tabPageBooks.TabIndex = 0;
            // 
            // tableLayoutPanelBooks
            // 
            tableLayoutPanelBooks.ColumnCount = 2;
            tableLayoutPanelBooks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66.6666641F));
            tableLayoutPanelBooks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333359F));
            tableLayoutPanelBooks.Controls.Add(chartBooksTime, 0, 1);
            tableLayoutPanelBooks.Controls.Add(chartBooksWeek, 1, 1);
            tableLayoutPanelBooks.Controls.Add(chartBooksHistory, 0, 0);
            tableLayoutPanelBooks.Dock = DockStyle.Fill;
            tableLayoutPanelBooks.Location = new Point(2, 2);
            tableLayoutPanelBooks.Margin = new Padding(2);
            tableLayoutPanelBooks.Name = "tableLayoutPanelBooks";
            tableLayoutPanelBooks.RowCount = 2;
            tableLayoutPanelBooks.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            tableLayoutPanelBooks.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            tableLayoutPanelBooks.Size = new Size(712, 357);
            tableLayoutPanelBooks.TabIndex = 0;
            // 
            // chartBooksTime
            // 
            chartBooksTime.BorderlineWidth = 0;
            chartArea1.AxisX.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.VariableCount;
            chartArea1.AxisX.IsLabelAutoFit = false;
            chartArea1.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea1.AxisX.Maximum = 24D;
            chartArea1.AxisX.Minimum = 0D;
            chartArea1.AxisY.IsLabelAutoFit = false;
            chartArea1.AxisY.LabelStyle.Enabled = false;
            chartArea1.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea1.Name = "ChartArea";
            chartBooksTime.ChartAreas.Add(chartArea1);
            chartBooksTime.Dock = DockStyle.Fill;
            legend1.Enabled = false;
            legend1.Name = "Legend";
            chartBooksTime.Legends.Add(legend1);
            chartBooksTime.Location = new Point(0, 142);
            chartBooksTime.Margin = new Padding(0);
            chartBooksTime.Name = "chartBooksTime";
            chartBooksTime.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series1.ChartArea = "ChartArea";
            series1.Legend = "Legend";
            series1.Name = "Series";
            series1.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series1.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            series1.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            chartBooksTime.Series.Add(series1);
            chartBooksTime.Size = new Size(474, 215);
            chartBooksTime.TabIndex = 2;
            // 
            // chartBooksWeek
            // 
            chartBooksWeek.BorderlineWidth = 0;
            chartArea2.AxisX.IsLabelAutoFit = false;
            chartArea2.AxisX.LabelAutoFitMinFontSize = 5;
            chartArea2.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea2.AxisX.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea2.AxisX2.LabelAutoFitMinFontSize = 5;
            chartArea2.AxisY.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.VariableCount;
            chartArea2.AxisY.IsLabelAutoFit = false;
            chartArea2.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea2.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea2.AxisY.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea2.Name = "ChartArea";
            chartBooksWeek.ChartAreas.Add(chartArea2);
            chartBooksWeek.Dock = DockStyle.Fill;
            legend2.Enabled = false;
            legend2.Name = "Legend";
            chartBooksWeek.Legends.Add(legend2);
            chartBooksWeek.Location = new Point(474, 142);
            chartBooksWeek.Margin = new Padding(0);
            chartBooksWeek.Name = "chartBooksWeek";
            chartBooksWeek.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series2.BorderWidth = 0;
            series2.ChartArea = "ChartArea";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Radar;
            series2.Font = new Font("Microsoft Sans Serif", 8F);
            series2.Legend = "Legend";
            series2.Name = "Series";
            series2.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series2.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.String;
            series2.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            chartBooksWeek.Series.Add(series2);
            chartBooksWeek.Size = new Size(238, 215);
            chartBooksWeek.TabIndex = 3;
            // 
            // chartBooksHistory
            // 
            chartBooksHistory.BorderlineWidth = 0;
            chartArea3.AxisX.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.VariableCount;
            chartArea3.AxisX.IsLabelAutoFit = false;
            chartArea3.AxisX.LabelAutoFitMinFontSize = 5;
            chartArea3.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea3.AxisX2.IsLabelAutoFit = false;
            chartArea3.AxisX2.LabelStyle.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Pixel);
            chartArea3.AxisY.IsLabelAutoFit = false;
            chartArea3.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea3.AxisY.LabelStyle.Enabled = false;
            chartArea3.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea3.Name = "ChartArea";
            chartBooksHistory.ChartAreas.Add(chartArea3);
            tableLayoutPanelBooks.SetColumnSpan(chartBooksHistory, 2);
            chartBooksHistory.Dock = DockStyle.Fill;
            legend3.Enabled = false;
            legend3.Name = "Legend";
            chartBooksHistory.Legends.Add(legend3);
            chartBooksHistory.Location = new Point(0, 0);
            chartBooksHistory.Margin = new Padding(0);
            chartBooksHistory.Name = "chartBooksHistory";
            chartBooksHistory.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series3.BorderWidth = 0;
            series3.ChartArea = "ChartArea";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Renko;
            series3.Legend = "Legend";
            series3.Name = "Series";
            series3.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series3.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.String;
            series3.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            chartBooksHistory.Series.Add(series3);
            chartBooksHistory.Size = new Size(712, 142);
            chartBooksHistory.TabIndex = 4;
            // 
            // tabPageVocabs
            // 
            tabPageVocabs.BackColor = SystemColors.Window;
            tabPageVocabs.Controls.Add(tableLayoutPanelVocabs);
            tabPageVocabs.Location = new Point(4, 4);
            tabPageVocabs.Margin = new Padding(2);
            tabPageVocabs.Name = "tabPageVocabs";
            tabPageVocabs.Padding = new Padding(2);
            tabPageVocabs.Size = new Size(716, 361);
            tabPageVocabs.TabIndex = 1;
            // 
            // tableLayoutPanelVocabs
            // 
            tableLayoutPanelVocabs.ColumnCount = 2;
            tableLayoutPanelVocabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66.6666641F));
            tableLayoutPanelVocabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333359F));
            tableLayoutPanelVocabs.Controls.Add(chartVocabsTime, 0, 1);
            tableLayoutPanelVocabs.Controls.Add(chartVocabsWeek, 1, 1);
            tableLayoutPanelVocabs.Controls.Add(chartVocabsHistory, 0, 0);
            tableLayoutPanelVocabs.Dock = DockStyle.Fill;
            tableLayoutPanelVocabs.Location = new Point(2, 2);
            tableLayoutPanelVocabs.Margin = new Padding(2);
            tableLayoutPanelVocabs.Name = "tableLayoutPanelVocabs";
            tableLayoutPanelVocabs.RowCount = 2;
            tableLayoutPanelVocabs.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            tableLayoutPanelVocabs.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            tableLayoutPanelVocabs.Size = new Size(712, 357);
            tableLayoutPanelVocabs.TabIndex = 1;
            // 
            // chartVocabsTime
            // 
            chartVocabsTime.BorderlineWidth = 0;
            chartArea4.AxisX.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.VariableCount;
            chartArea4.AxisX.IsLabelAutoFit = false;
            chartArea4.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea4.AxisX.Maximum = 24D;
            chartArea4.AxisX.Minimum = 0D;
            chartArea4.AxisY.IsLabelAutoFit = false;
            chartArea4.AxisY.LabelStyle.Enabled = false;
            chartArea4.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea4.Name = "ChartArea";
            chartVocabsTime.ChartAreas.Add(chartArea4);
            chartVocabsTime.Dock = DockStyle.Fill;
            legend4.Enabled = false;
            legend4.Name = "Legend";
            chartVocabsTime.Legends.Add(legend4);
            chartVocabsTime.Location = new Point(0, 142);
            chartVocabsTime.Margin = new Padding(0);
            chartVocabsTime.Name = "chartVocabsTime";
            chartVocabsTime.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series4.ChartArea = "ChartArea";
            series4.Legend = "Legend";
            series4.Name = "Series";
            series4.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series4.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            series4.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            chartVocabsTime.Series.Add(series4);
            chartVocabsTime.Size = new Size(474, 215);
            chartVocabsTime.TabIndex = 2;
            // 
            // chartVocabsWeek
            // 
            chartVocabsWeek.BorderlineWidth = 0;
            chartArea5.AxisX.IsLabelAutoFit = false;
            chartArea5.AxisX.LabelAutoFitMinFontSize = 5;
            chartArea5.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea5.AxisX.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea5.AxisX2.LabelAutoFitMinFontSize = 5;
            chartArea5.AxisY.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.VariableCount;
            chartArea5.AxisY.IsLabelAutoFit = false;
            chartArea5.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea5.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea5.AxisY.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea5.Name = "ChartArea";
            chartVocabsWeek.ChartAreas.Add(chartArea5);
            chartVocabsWeek.Dock = DockStyle.Fill;
            legend5.Enabled = false;
            legend5.Name = "Legend";
            chartVocabsWeek.Legends.Add(legend5);
            chartVocabsWeek.Location = new Point(474, 142);
            chartVocabsWeek.Margin = new Padding(0);
            chartVocabsWeek.Name = "chartVocabsWeek";
            chartVocabsWeek.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series5.BorderWidth = 0;
            series5.ChartArea = "ChartArea";
            series5.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Radar;
            series5.Font = new Font("Microsoft Sans Serif", 8F);
            series5.Legend = "Legend";
            series5.Name = "Series";
            series5.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series5.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.String;
            series5.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            chartVocabsWeek.Series.Add(series5);
            chartVocabsWeek.Size = new Size(238, 215);
            chartVocabsWeek.TabIndex = 3;
            // 
            // chartVocabsHistory
            // 
            chartVocabsHistory.BorderlineWidth = 0;
            chartArea6.AxisX.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.VariableCount;
            chartArea6.AxisX.IsLabelAutoFit = false;
            chartArea6.AxisX.LabelAutoFitMinFontSize = 5;
            chartArea6.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea6.AxisX2.IsLabelAutoFit = false;
            chartArea6.AxisX2.LabelStyle.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Pixel);
            chartArea6.AxisY.IsLabelAutoFit = false;
            chartArea6.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea6.AxisY.LabelStyle.Enabled = false;
            chartArea6.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea6.Name = "ChartArea";
            chartVocabsHistory.ChartAreas.Add(chartArea6);
            tableLayoutPanelVocabs.SetColumnSpan(chartVocabsHistory, 2);
            chartVocabsHistory.Dock = DockStyle.Fill;
            legend6.Enabled = false;
            legend6.Name = "Legend";
            chartVocabsHistory.Legends.Add(legend6);
            chartVocabsHistory.Location = new Point(0, 0);
            chartVocabsHistory.Margin = new Padding(0);
            chartVocabsHistory.Name = "chartVocabsHistory";
            chartVocabsHistory.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series6.BorderWidth = 0;
            series6.ChartArea = "ChartArea";
            series6.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Renko;
            series6.Legend = "Legend";
            series6.Name = "Series";
            series6.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Pastel;
            series6.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.String;
            series6.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            chartVocabsHistory.Series.Add(series6);
            chartVocabsHistory.Size = new Size(712, 142);
            chartVocabsHistory.TabIndex = 4;
            // 
            // FrmStatistic
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(724, 394);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(2);
            MinimizeBox = false;
            Name = "FrmStatistic";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = Strings.Statistics;
            TopMost = true;
            Load += FrmStatistic_Load;
            tabControl.ResumeLayout(false);
            tabPageBooks.ResumeLayout(false);
            tableLayoutPanelBooks.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)chartBooksTime).EndInit();
            ((System.ComponentModel.ISupportInitialize)chartBooksWeek).EndInit();
            ((System.ComponentModel.ISupportInitialize)chartBooksHistory).EndInit();
            tabPageVocabs.ResumeLayout(false);
            tableLayoutPanelVocabs.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)chartVocabsTime).EndInit();
            ((System.ComponentModel.ISupportInitialize)chartVocabsWeek).EndInit();
            ((System.ComponentModel.ISupportInitialize)chartVocabsHistory).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabPageVocabs;
        private TabPage tabPageBooks;
        private TableLayoutPanel tableLayoutPanelBooks;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartBooksTime;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartBooksWeek;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartBooksHistory;
        private TableLayoutPanel tableLayoutPanelVocabs;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartVocabsTime;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartVocabsWeek;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartVocabsHistory;
    }
}