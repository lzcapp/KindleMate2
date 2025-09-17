using System.ComponentModel;
using System.Windows.Forms.DataVisualization.Charting;
using KindleMate2.Properties;

namespace KindleMate2 {
    partial class FrmStatistics {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

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
            ChartArea chartArea1 = new ChartArea();
            Legend legend1 = new Legend();
            Series series1 = new Series();
            ChartArea chartArea2 = new ChartArea();
            Legend legend2 = new Legend();
            Series series2 = new Series();
            ChartArea chartArea3 = new ChartArea();
            Legend legend3 = new Legend();
            Series series3 = new Series();
            ChartArea chartArea4 = new ChartArea();
            Legend legend4 = new Legend();
            Series series4 = new Series();
            ChartArea chartArea5 = new ChartArea();
            Legend legend5 = new Legend();
            Series series5 = new Series();
            ChartArea chartArea6 = new ChartArea();
            Legend legend6 = new Legend();
            Series series6 = new Series();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(FrmStatistics));
            tabControl = new TabControl();
            tabPageBooks = new TabPage();
            tableLayoutPanelBooks = new TableLayoutPanel();
            chartBooksTime = new Chart();
            chartBooksWeek = new Chart();
            chartBooksHistory = new Chart();
            tabPageVocabs = new TabPage();
            tableLayoutPanelVocabs = new TableLayoutPanel();
            chartVocabsTime = new Chart();
            chartVocabsWeek = new Chart();
            chartVocabsHistory = new Chart();
            toolStrip = new ToolStrip();
            btnSave = new ToolStripButton();
            lblStatistics = new ToolStripLabel();
            tabControl.SuspendLayout();
            tabPageBooks.SuspendLayout();
            tableLayoutPanelBooks.SuspendLayout();
            ((ISupportInitialize)chartBooksTime).BeginInit();
            ((ISupportInitialize)chartBooksWeek).BeginInit();
            ((ISupportInitialize)chartBooksHistory).BeginInit();
            tabPageVocabs.SuspendLayout();
            tableLayoutPanelVocabs.SuspendLayout();
            ((ISupportInitialize)chartVocabsTime).BeginInit();
            ((ISupportInitialize)chartVocabsWeek).BeginInit();
            ((ISupportInitialize)chartVocabsHistory).BeginInit();
            toolStrip.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Alignment = TabAlignment.Bottom;
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl.Controls.Add(tabPageBooks);
            tabControl.Controls.Add(tabPageVocabs);
            tabControl.Location = new Point(0, 27);
            tabControl.Margin = new Padding(0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1316, 713);
            tabControl.TabIndex = 0;
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            // 
            // tabPageBooks
            // 
            tabPageBooks.BackColor = SystemColors.Window;
            tabPageBooks.Controls.Add(tableLayoutPanelBooks);
            tabPageBooks.Location = new Point(8, 8);
            tabPageBooks.Margin = new Padding(2);
            tabPageBooks.Name = "tabPageBooks";
            tabPageBooks.Padding = new Padding(2);
            tabPageBooks.Size = new Size(1300, 660);
            tabPageBooks.TabIndex = 0;
            // 
            // tableLayoutPanelBooks
            // 
            tableLayoutPanelBooks.ColumnCount = 2;
            tableLayoutPanelBooks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            tableLayoutPanelBooks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
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
            tableLayoutPanelBooks.Size = new Size(1296, 656);
            tableLayoutPanelBooks.TabIndex = 0;
            // 
            // chartBooksTime
            // 
            chartBooksTime.BorderlineWidth = 0;
            chartArea1.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea1.AxisX.IsLabelAutoFit = false;
            chartArea1.AxisX.LabelAutoFitMaxFontSize = 7;
            chartArea1.AxisX.LabelAutoFitMinFontSize = 7;
            chartArea1.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 7F);
            chartArea1.AxisX.LabelStyle.Interval = 0D;
            chartArea1.AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Auto;
            chartArea1.AxisX.LineWidth = 0;
            chartArea1.AxisX.MajorGrid.Enabled = false;
            chartArea1.AxisX.MajorTickMark.Enabled = false;
            chartArea1.AxisX.Maximum = 23D;
            chartArea1.AxisX.Minimum = 0D;
            chartArea1.AxisY.IsLabelAutoFit = false;
            chartArea1.AxisY.LabelStyle.Enabled = false;
            chartArea1.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea1.AxisY.LineWidth = 0;
            chartArea1.AxisY.MajorGrid.Enabled = false;
            chartArea1.AxisY.MajorTickMark.Enabled = false;
            chartArea1.BackColor = Color.Transparent;
            chartArea1.Name = "ChartArea";
            chartBooksTime.ChartAreas.Add(chartArea1);
            chartBooksTime.Dock = DockStyle.Fill;
            legend1.Enabled = false;
            legend1.Name = "Legend";
            chartBooksTime.Legends.Add(legend1);
            chartBooksTime.Location = new Point(0, 262);
            chartBooksTime.Margin = new Padding(0);
            chartBooksTime.Name = "chartBooksTime";
            chartBooksTime.Palette = ChartColorPalette.Pastel;
            series1.ChartArea = "ChartArea";
            series1.Legend = "Legend";
            series1.Name = "Series";
            series1.Palette = ChartColorPalette.Pastel;
            series1.XValueType = ChartValueType.Int32;
            series1.YValueType = ChartValueType.Int32;
            chartBooksTime.Series.Add(series1);
            chartBooksTime.Size = new Size(907, 394);
            chartBooksTime.TabIndex = 2;
            // 
            // chartBooksWeek
            // 
            chartBooksWeek.BorderlineWidth = 0;
            chartArea2.AxisX.IsLabelAutoFit = false;
            chartArea2.AxisX.IsMarginVisible = false;
            chartArea2.AxisX.IsStartedFromZero = false;
            chartArea2.AxisX.LabelAutoFitMaxFontSize = 7;
            chartArea2.AxisX.LabelAutoFitMinFontSize = 7;
            chartArea2.AxisX.LabelStyle.Font = new Font("微软雅黑", 7F);
            chartArea2.AxisX.LabelStyle.Interval = 0D;
            chartArea2.AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Auto;
            chartArea2.AxisX.LineWidth = 0;
            chartArea2.AxisX.MajorGrid.Enabled = false;
            chartArea2.AxisX.MajorTickMark.Enabled = false;
            chartArea2.AxisX.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea2.AxisX2.LabelAutoFitMinFontSize = 5;
            chartArea2.AxisY.Enabled = AxisEnabled.False;
            chartArea2.AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea2.AxisY.IsLabelAutoFit = false;
            chartArea2.AxisY.IsMarginVisible = false;
            chartArea2.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea2.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea2.AxisY.LineWidth = 0;
            chartArea2.AxisY.MajorGrid.Enabled = false;
            chartArea2.AxisY.MajorTickMark.Enabled = false;
            chartArea2.AxisY.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea2.BackColor = Color.Transparent;
            chartArea2.Name = "ChartArea";
            chartBooksWeek.ChartAreas.Add(chartArea2);
            chartBooksWeek.Dock = DockStyle.Fill;
            legend2.Enabled = false;
            legend2.Name = "Legend";
            chartBooksWeek.Legends.Add(legend2);
            chartBooksWeek.Location = new Point(907, 262);
            chartBooksWeek.Margin = new Padding(0);
            chartBooksWeek.Name = "chartBooksWeek";
            chartBooksWeek.Palette = ChartColorPalette.Pastel;
            series2.BorderWidth = 0;
            series2.ChartArea = "ChartArea";
            series2.ChartType = SeriesChartType.Radar;
            series2.Font = new Font("Microsoft Sans Serif", 8F);
            series2.Legend = "Legend";
            series2.Name = "Series";
            series2.Palette = ChartColorPalette.Pastel;
            series2.XValueType = ChartValueType.String;
            series2.YValueType = ChartValueType.Int32;
            chartBooksWeek.Series.Add(series2);
            chartBooksWeek.Size = new Size(389, 394);
            chartBooksWeek.TabIndex = 3;
            // 
            // chartBooksHistory
            // 
            chartBooksHistory.BorderlineWidth = 0;
            chartArea3.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea3.AxisX.IsLabelAutoFit = false;
            chartArea3.AxisX.IsMarginVisible = false;
            chartArea3.AxisX.IsStartedFromZero = false;
            chartArea3.AxisX.LabelAutoFitMaxFontSize = 7;
            chartArea3.AxisX.LabelAutoFitMinFontSize = 7;
            chartArea3.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 7F);
            chartArea3.AxisX.LabelStyle.Interval = 0D;
            chartArea3.AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Auto;
            chartArea3.AxisX.LineWidth = 0;
            chartArea3.AxisX.MajorGrid.Enabled = false;
            chartArea3.AxisX.MajorTickMark.Enabled = false;
            chartArea3.AxisX2.IsLabelAutoFit = false;
            chartArea3.AxisX2.LabelStyle.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Pixel);
            chartArea3.AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea3.AxisY.IsLabelAutoFit = false;
            chartArea3.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea3.AxisY.LabelStyle.Enabled = false;
            chartArea3.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea3.AxisY.LineWidth = 0;
            chartArea3.AxisY.MajorGrid.Enabled = false;
            chartArea3.AxisY.MajorTickMark.Enabled = false;
            chartArea3.BackColor = Color.Transparent;
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
            chartBooksHistory.Palette = ChartColorPalette.Pastel;
            series3.BorderWidth = 0;
            series3.ChartArea = "ChartArea";
            series3.ChartType = SeriesChartType.Bubble;
            series3.IsXValueIndexed = true;
            series3.Legend = "Legend";
            series3.Name = "Series";
            series3.Palette = ChartColorPalette.Pastel;
            series3.XValueType = ChartValueType.String;
            series3.YValuesPerPoint = 2;
            chartBooksHistory.Series.Add(series3);
            chartBooksHistory.Size = new Size(1296, 262);
            chartBooksHistory.TabIndex = 4;
            // 
            // tabPageVocabs
            // 
            tabPageVocabs.BackColor = SystemColors.Window;
            tabPageVocabs.Controls.Add(tableLayoutPanelVocabs);
            tabPageVocabs.Location = new Point(8, 8);
            tabPageVocabs.Margin = new Padding(2);
            tabPageVocabs.Name = "tabPageVocabs";
            tabPageVocabs.Padding = new Padding(2);
            tabPageVocabs.Size = new Size(666, 273);
            tabPageVocabs.TabIndex = 1;
            // 
            // tableLayoutPanelVocabs
            // 
            tableLayoutPanelVocabs.ColumnCount = 2;
            tableLayoutPanelVocabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            tableLayoutPanelVocabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            tableLayoutPanelVocabs.Controls.Add(chartVocabsTime, 0, 1);
            tableLayoutPanelVocabs.Controls.Add(chartVocabsWeek, 1, 1);
            tableLayoutPanelVocabs.Controls.Add(chartVocabsHistory, 0, 0);
            tableLayoutPanelVocabs.Dock = DockStyle.Fill;
            tableLayoutPanelVocabs.Location = new Point(2, 2);
            tableLayoutPanelVocabs.Margin = new Padding(0);
            tableLayoutPanelVocabs.Name = "tableLayoutPanelVocabs";
            tableLayoutPanelVocabs.RowCount = 2;
            tableLayoutPanelVocabs.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            tableLayoutPanelVocabs.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            tableLayoutPanelVocabs.Size = new Size(662, 269);
            tableLayoutPanelVocabs.TabIndex = 1;
            // 
            // chartVocabsTime
            // 
            chartVocabsTime.BorderlineWidth = 0;
            chartArea4.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea4.AxisX.IsLabelAutoFit = false;
            chartArea4.AxisX.IsMarksNextToAxis = false;
            chartArea4.AxisX.LabelAutoFitMaxFontSize = 7;
            chartArea4.AxisX.LabelAutoFitMinFontSize = 7;
            chartArea4.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 7F);
            chartArea4.AxisX.LabelStyle.Interval = 0D;
            chartArea4.AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Auto;
            chartArea4.AxisX.LineWidth = 0;
            chartArea4.AxisX.MajorGrid.Enabled = false;
            chartArea4.AxisX.MajorTickMark.Enabled = false;
            chartArea4.AxisX.Maximum = 23D;
            chartArea4.AxisX.Minimum = 0D;
            chartArea4.AxisY.IsLabelAutoFit = false;
            chartArea4.AxisY.IsMarksNextToAxis = false;
            chartArea4.AxisY.LabelStyle.Enabled = false;
            chartArea4.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea4.AxisY.LineWidth = 0;
            chartArea4.AxisY.MajorGrid.Enabled = false;
            chartArea4.AxisY.MajorTickMark.Enabled = false;
            chartArea4.BackColor = Color.Transparent;
            chartArea4.Name = "ChartArea";
            chartVocabsTime.ChartAreas.Add(chartArea4);
            chartVocabsTime.Dock = DockStyle.Fill;
            legend4.Enabled = false;
            legend4.Name = "Legend";
            chartVocabsTime.Legends.Add(legend4);
            chartVocabsTime.Location = new Point(0, 107);
            chartVocabsTime.Margin = new Padding(0);
            chartVocabsTime.Name = "chartVocabsTime";
            chartVocabsTime.Palette = ChartColorPalette.Pastel;
            series4.ChartArea = "ChartArea";
            series4.Legend = "Legend";
            series4.Name = "Series";
            series4.Palette = ChartColorPalette.Pastel;
            series4.XValueType = ChartValueType.Int32;
            series4.YValueType = ChartValueType.Int32;
            chartVocabsTime.Series.Add(series4);
            chartVocabsTime.Size = new Size(463, 162);
            chartVocabsTime.TabIndex = 2;
            // 
            // chartVocabsWeek
            // 
            chartVocabsWeek.BorderlineWidth = 0;
            chartArea5.AxisX.IsLabelAutoFit = false;
            chartArea5.AxisX.IsMarginVisible = false;
            chartArea5.AxisX.IsMarksNextToAxis = false;
            chartArea5.AxisX.IsStartedFromZero = false;
            chartArea5.AxisX.LabelAutoFitMaxFontSize = 7;
            chartArea5.AxisX.LabelAutoFitMinFontSize = 7;
            chartArea5.AxisX.LabelStyle.Font = new Font("微软雅黑", 7F);
            chartArea5.AxisX.LabelStyle.Interval = 0D;
            chartArea5.AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Auto;
            chartArea5.AxisX.LineWidth = 0;
            chartArea5.AxisX.MajorGrid.Enabled = false;
            chartArea5.AxisX.MajorTickMark.Enabled = false;
            chartArea5.AxisX.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea5.AxisX2.LabelAutoFitMinFontSize = 5;
            chartArea5.AxisY.Enabled = AxisEnabled.False;
            chartArea5.AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea5.AxisY.IsLabelAutoFit = false;
            chartArea5.AxisY.IsMarginVisible = false;
            chartArea5.AxisY.IsMarksNextToAxis = false;
            chartArea5.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea5.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea5.AxisY.LineWidth = 0;
            chartArea5.AxisY.MajorGrid.Enabled = false;
            chartArea5.AxisY.MajorTickMark.Enabled = false;
            chartArea5.AxisY.TitleFont = new Font("Microsoft Sans Serif", 6F);
            chartArea5.BackColor = Color.Transparent;
            chartArea5.IsSameFontSizeForAllAxes = true;
            chartArea5.Name = "ChartArea";
            chartVocabsWeek.ChartAreas.Add(chartArea5);
            chartVocabsWeek.Dock = DockStyle.Fill;
            legend5.Enabled = false;
            legend5.Name = "Legend";
            chartVocabsWeek.Legends.Add(legend5);
            chartVocabsWeek.Location = new Point(463, 107);
            chartVocabsWeek.Margin = new Padding(0);
            chartVocabsWeek.Name = "chartVocabsWeek";
            chartVocabsWeek.Palette = ChartColorPalette.Pastel;
            series5.BorderWidth = 0;
            series5.ChartArea = "ChartArea";
            series5.ChartType = SeriesChartType.Radar;
            series5.Font = new Font("Microsoft Sans Serif", 8F);
            series5.Legend = "Legend";
            series5.Name = "Series";
            series5.Palette = ChartColorPalette.Pastel;
            series5.XValueType = ChartValueType.String;
            series5.YValueType = ChartValueType.Int32;
            chartVocabsWeek.Series.Add(series5);
            chartVocabsWeek.Size = new Size(199, 162);
            chartVocabsWeek.TabIndex = 3;
            // 
            // chartVocabsHistory
            // 
            chartVocabsHistory.BorderlineWidth = 0;
            chartArea6.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea6.AxisX.IsLabelAutoFit = false;
            chartArea6.AxisX.IsMarginVisible = false;
            chartArea6.AxisX.IsStartedFromZero = false;
            chartArea6.AxisX.LabelAutoFitMaxFontSize = 7;
            chartArea6.AxisX.LabelAutoFitMinFontSize = 7;
            chartArea6.AxisX.LabelStyle.Font = new Font("Microsoft Sans Serif", 7F);
            chartArea6.AxisX.LabelStyle.Interval = 0D;
            chartArea6.AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Auto;
            chartArea6.AxisX.LineWidth = 0;
            chartArea6.AxisX.MajorGrid.Enabled = false;
            chartArea6.AxisX.MajorTickMark.Enabled = false;
            chartArea6.AxisX2.IsLabelAutoFit = false;
            chartArea6.AxisX2.LabelStyle.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Pixel);
            chartArea6.AxisY.Enabled = AxisEnabled.False;
            chartArea6.AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea6.AxisY.IsLabelAutoFit = false;
            chartArea6.AxisY.LabelAutoFitMinFontSize = 5;
            chartArea6.AxisY.LabelStyle.Enabled = false;
            chartArea6.AxisY.LabelStyle.Font = new Font("Microsoft Sans Serif", 6F);
            chartArea6.AxisY.LineWidth = 0;
            chartArea6.AxisY.MajorGrid.Enabled = false;
            chartArea6.AxisY.MajorTickMark.Enabled = false;
            chartArea6.BackColor = Color.Transparent;
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
            chartVocabsHistory.Palette = ChartColorPalette.Pastel;
            series6.BorderWidth = 0;
            series6.ChartArea = "ChartArea";
            series6.ChartType = SeriesChartType.Bubble;
            series6.IsXValueIndexed = true;
            series6.Legend = "Legend";
            series6.Name = "Series";
            series6.Palette = ChartColorPalette.Pastel;
            series6.XValueType = ChartValueType.Date;
            series6.YValuesPerPoint = 2;
            chartVocabsHistory.Series.Add(series6);
            chartVocabsHistory.Size = new Size(662, 107);
            chartVocabsHistory.TabIndex = 4;
            // 
            // toolStrip
            // 
            toolStrip.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.ImageScalingSize = new Size(20, 20);
            toolStrip.Items.AddRange(new ToolStripItem[] { btnSave, lblStatistics });
            toolStrip.Location = new Point(0, 0);
            toolStrip.Name = "toolStrip";
            toolStrip.Size = new Size(1316, 27);
            toolStrip.TabIndex = 1;
            toolStrip.Text = "toolStrip1";
            // 
            // btnSave
            // 
            btnSave.Alignment = ToolStripItemAlignment.Right;
            btnSave.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnSave.Image = Resources.floppy_disk;
            btnSave.ImageTransparentColor = Color.Transparent;
            btnSave.Margin = new Padding(0, 1, 10, 2);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(46, 24);
            btnSave.Click += BtnSave_Click;
            btnSave.MouseEnter += BtnSave_MouseEnter;
            btnSave.MouseLeave += BtnSave_MouseLeave;
            // 
            // lblStatistics
            // 
            lblStatistics.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lblStatistics.Margin = new Padding(10, 1, 0, 2);
            lblStatistics.Name = "lblStatistics";
            lblStatistics.Size = new Size(0, 24);
            // 
            // FrmStatistics
            // 
            AutoScaleMode = AutoScaleMode.Inherit;
            BackColor = SystemColors.Window;
            ClientSize = new Size(1316, 740);
            Controls.Add(toolStrip);
            Controls.Add(tabControl);
            Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(2);
            MinimizeBox = false;
            MinimumSize = new Size(700, 400);
            Name = "FrmStatistics";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "统计";
            Load += FrmStatistic_Load;
            tabControl.ResumeLayout(false);
            tabPageBooks.ResumeLayout(false);
            tableLayoutPanelBooks.ResumeLayout(false);
            ((ISupportInitialize)chartBooksTime).EndInit();
            ((ISupportInitialize)chartBooksWeek).EndInit();
            ((ISupportInitialize)chartBooksHistory).EndInit();
            tabPageVocabs.ResumeLayout(false);
            tableLayoutPanelVocabs.ResumeLayout(false);
            ((ISupportInitialize)chartVocabsTime).EndInit();
            ((ISupportInitialize)chartVocabsWeek).EndInit();
            ((ISupportInitialize)chartVocabsHistory).EndInit();
            toolStrip.ResumeLayout(false);
            toolStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabPageVocabs;
        private TabPage tabPageBooks;
        private TableLayoutPanel tableLayoutPanelBooks;
        private Chart chartBooksTime;
        private Chart chartBooksWeek;
        private Chart chartBooksHistory;
        private TableLayoutPanel tableLayoutPanelVocabs;
        private Chart chartVocabsTime;
        private Chart chartVocabsWeek;
        private Chart chartVocabsHistory;
        private ToolStrip toolStrip;
        private ToolStripButton btnSave;
        private ToolStripLabel lblStatistics;
    }
}