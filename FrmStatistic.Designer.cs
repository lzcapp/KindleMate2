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
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            tableLayoutPanel1 = new TableLayoutPanel();
            chartBooksDoughnut = new System.Windows.Forms.DataVisualization.Charting.Chart();
            flowLayoutPanel = new FlowLayoutPanel();
            lblBooksCount = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            label5 = new Label();
            label6 = new Label();
            label7 = new Label();
            chartBooksTime = new System.Windows.Forms.DataVisualization.Charting.Chart();
            chartBooksWeek = new System.Windows.Forms.DataVisualization.Charting.Chart();
            tabPage2 = new TabPage();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)chartBooksDoughnut).BeginInit();
            flowLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)chartBooksTime).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chartBooksWeek).BeginInit();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Alignment = TabAlignment.Bottom;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1126, 610);
            tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.BackColor = SystemColors.Window;
            tabPage1.Controls.Add(tableLayoutPanel1);
            tabPage1.Location = new Point(8, 8);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1110, 557);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "tabPage1";
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(chartBooksDoughnut, 0, 0);
            tableLayoutPanel1.Controls.Add(flowLayoutPanel, 1, 0);
            tableLayoutPanel1.Controls.Add(chartBooksTime, 0, 1);
            tableLayoutPanel1.Controls.Add(chartBooksWeek, 1, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(3, 3);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 64.99303F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 35.0069733F));
            tableLayoutPanel1.Size = new Size(1104, 551);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // chartBooksDoughnut
            // 
            chartArea1.Name = "ChartArea1";
            chartBooksDoughnut.ChartAreas.Add(chartArea1);
            chartBooksDoughnut.Dock = DockStyle.Fill;
            legend1.Name = "Legend1";
            chartBooksDoughnut.Legends.Add(legend1);
            chartBooksDoughnut.Location = new Point(3, 3);
            chartBooksDoughnut.Name = "chartBooksDoughnut";
            series1.ChartArea = "ChartArea1";
            series1.Legend = "Legend1";
            series1.Name = "Series1";
            chartBooksDoughnut.Series.Add(series1);
            chartBooksDoughnut.Size = new Size(546, 352);
            chartBooksDoughnut.TabIndex = 0;
            // 
            // flowLayoutPanel
            // 
            flowLayoutPanel.Controls.Add(lblBooksCount);
            flowLayoutPanel.Controls.Add(label2);
            flowLayoutPanel.Controls.Add(label3);
            flowLayoutPanel.Controls.Add(label4);
            flowLayoutPanel.Controls.Add(label5);
            flowLayoutPanel.Controls.Add(label6);
            flowLayoutPanel.Controls.Add(label7);
            flowLayoutPanel.Dock = DockStyle.Fill;
            flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel.Location = new Point(572, 20);
            flowLayoutPanel.Margin = new Padding(20);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Size = new Size(512, 318);
            flowLayoutPanel.TabIndex = 1;
            // 
            // lblBooksCount
            // 
            lblBooksCount.AutoSize = true;
            lblBooksCount.Font = new Font("微软雅黑", 9F);
            lblBooksCount.Location = new Point(6, 6);
            lblBooksCount.Margin = new Padding(6);
            lblBooksCount.Name = "lblBooksCount";
            lblBooksCount.Size = new Size(108, 31);
            lblBooksCount.TabIndex = 0;
            lblBooksCount.Text = "X 本书籍";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("微软雅黑", 9F);
            label2.Location = new Point(6, 49);
            label2.Margin = new Padding(6);
            label2.Name = "label2";
            label2.Size = new Size(108, 31);
            label2.TabIndex = 1;
            label2.Text = "X 位作者";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("微软雅黑", 9F);
            label3.Location = new Point(6, 92);
            label3.Margin = new Padding(6);
            label3.Name = "label3";
            label3.Size = new Size(82, 31);
            label3.TabIndex = 2;
            label3.Text = "label3";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("微软雅黑", 9F);
            label4.Location = new Point(6, 135);
            label4.Margin = new Padding(6);
            label4.Name = "label4";
            label4.Size = new Size(82, 31);
            label4.TabIndex = 3;
            label4.Text = "label4";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("微软雅黑", 9F);
            label5.Location = new Point(6, 178);
            label5.Margin = new Padding(6);
            label5.Name = "label5";
            label5.Size = new Size(82, 31);
            label5.TabIndex = 4;
            label5.Text = "label5";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("微软雅黑", 9F);
            label6.Location = new Point(6, 221);
            label6.Margin = new Padding(6);
            label6.Name = "label6";
            label6.Size = new Size(82, 31);
            label6.TabIndex = 5;
            label6.Text = "label6";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("微软雅黑", 9F);
            label7.Location = new Point(5, 263);
            label7.Margin = new Padding(5, 5, 5, 0);
            label7.Name = "label7";
            label7.Size = new Size(82, 31);
            label7.TabIndex = 6;
            label7.Text = "label7";
            // 
            // chartBooksTime
            // 
            chartArea2.Name = "ChartArea1";
            chartBooksTime.ChartAreas.Add(chartArea2);
            legend2.Name = "Legend1";
            chartBooksTime.Legends.Add(legend2);
            chartBooksTime.Location = new Point(3, 361);
            chartBooksTime.Name = "chartBooksTime";
            series2.ChartArea = "ChartArea1";
            series2.Legend = "Legend1";
            series2.Name = "Series1";
            chartBooksTime.Series.Add(series2);
            chartBooksTime.Size = new Size(546, 187);
            chartBooksTime.TabIndex = 2;
            // 
            // chartBooksWeek
            // 
            chartArea3.Name = "ChartArea1";
            chartBooksWeek.ChartAreas.Add(chartArea3);
            legend3.Name = "Legend1";
            chartBooksWeek.Legends.Add(legend3);
            chartBooksWeek.Location = new Point(555, 361);
            chartBooksWeek.Name = "chartBooksWeek";
            series3.ChartArea = "ChartArea1";
            series3.Legend = "Legend1";
            series3.Name = "Series1";
            chartBooksWeek.Series.Add(series3);
            chartBooksWeek.Size = new Size(546, 187);
            chartBooksWeek.TabIndex = 3;
            // 
            // tabPage2
            // 
            tabPage2.BackColor = SystemColors.Window;
            tabPage2.Location = new Point(8, 8);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(1110, 557);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "tabPage2";
            // 
            // FrmStatistic
            // 
            AutoScaleDimensions = new SizeF(14F, 31F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1126, 610);
            Controls.Add(tabControl1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmStatistic";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "FrmStatistic";
            TopMost = true;
            Load += FrmStatistic_Load;
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)chartBooksDoughnut).EndInit();
            flowLayoutPanel.ResumeLayout(false);
            flowLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)chartBooksTime).EndInit();
            ((System.ComponentModel.ISupportInitialize)chartBooksWeek).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl1;
        private TabPage tabPage2;
        private TabPage tabPage1;
        private TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartBooksDoughnut;
        private FlowLayoutPanel flowLayoutPanel;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartBooksTime;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartBooksWeek;
        private Label lblBooksCount;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        private Label label6;
        private Label label7;
    }
}