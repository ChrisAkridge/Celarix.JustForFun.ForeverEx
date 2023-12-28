namespace Celarix.JustForFun.ForeverExMemoryView
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            memoryBitmap.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            LabelWordViewer = new Label();
            PanelMemoryView = new Panel();
            SuspendLayout();
            // 
            // LabelWordViewer
            // 
            LabelWordViewer.AutoSize = true;
            LabelWordViewer.Font = new Font("Consolas", 14F, FontStyle.Regular, GraphicsUnit.Point);
            LabelWordViewer.Location = new Point(8, 5);
            LabelWordViewer.Margin = new Padding(2, 0, 2, 0);
            LabelWordViewer.Name = "LabelWordViewer";
            LabelWordViewer.Size = new Size(370, 22);
            LabelWordViewer.TabIndex = 0;
            LabelWordViewer.Text = "Hover over a pixel to see its value.";
            // 
            // PanelMemoryView
            // 
            PanelMemoryView.Location = new Point(8, 27);
            PanelMemoryView.Margin = new Padding(2, 2, 2, 2);
            PanelMemoryView.Name = "PanelMemoryView";
            PanelMemoryView.Size = new Size(1024, 1024);
            PanelMemoryView.TabIndex = 1;
            PanelMemoryView.Paint += PanelMemoryView_Paint;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1038, 1061);
            Controls.Add(PanelMemoryView);
            Controls.Add(LabelWordViewer);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(2, 2, 2, 2);
            MaximizeBox = false;
            Name = "MainForm";
            ShowIcon = false;
            Text = "Memory Viewer";
            FormClosed += MainForm_FormClosed;
            Load += MainForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }


        #endregion

        private Label LabelWordViewer;
        private Panel PanelMemoryView;
    }
}