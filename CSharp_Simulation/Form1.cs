using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CprMicroSignalSim
{
    // =========================================================
    // BAGIAN 1: LOGIKA FSM & SENSOR FISIKA
    // =========================================================
    public static class MicroController
    {
        public enum State { IDLE, COMPRESS, VENTILATE, SHOCK_STANDBY }
        public static State CurrentState = State.IDLE;
        public static int CompCount = 0;
        public static int VentCount = 0;

        // Data Sensor
        public static string Sensor_D = "00";
        public static string Sensor_R = "10";
        public static bool Sensor_RC = true;
        public static string Sensor_P = "00";
        public static bool Sensor_AMSA = false;

        // Variabel Fisika untuk Grafik
        public static float CurrentDepthVal = 0;
        public static float TargetDepth = 0;

        // Output Aktuator
        public static bool Out_Metro = false;
        public static bool Out_VentGuide = false;
        public static bool Out_Quality = false;
        public static bool Out_Shock = false;

        public static void ClockTick()
        {
            // 1. Logika Output Aktuator
            Out_Metro = (CurrentState == State.COMPRESS);
            Out_VentGuide = (CurrentState == State.VENTILATE);
            Out_Quality = (Sensor_D == "10" && Sensor_R == "10" && Sensor_RC);
            Out_Shock = ((Sensor_P == "01" || Sensor_P == "10") && Sensor_AMSA);

            // 2. Simulasi Fisika Dada
            if (CurrentDepthVal > 0)
                CurrentDepthVal -= 5f;

            if (CurrentDepthVal < 0) CurrentDepthVal = 0;
        }

        public static void Input_Compression(string type)
        {
            switch (type)
            {
                case "BAIK": TargetDepth = 55; break;
                case "DANGKAL": TargetDepth = 30; break;
                case "LEANING": TargetDepth = 55; break;
            }
            CurrentDepthVal = TargetDepth;

            if (CurrentState == State.SHOCK_STANDBY) return;

            if (CurrentState == State.IDLE) CurrentState = State.COMPRESS;

            if (CurrentState == State.COMPRESS)
            {
                CompCount++;
                switch (type)
                {
                    case "BAIK": Sensor_D = "10"; Sensor_RC = true; break;
                    case "DANGKAL": Sensor_D = "01"; Sensor_RC = true; break;
                    case "LEANING": Sensor_D = "10"; Sensor_RC = false; break;
                }

                if (Out_Shock) { CurrentState = State.SHOCK_STANDBY; return; }

                if (CompCount >= 30)
                {
                    CurrentState = State.VENTILATE;
                    CompCount = 0; VentCount = 0;
                }
            }
        }

        public static void Input_Ventilation()
        {
            if (CurrentState == State.VENTILATE)
            {
                VentCount++;
                if (VentCount >= 2)
                {
                    CurrentState = State.COMPRESS;
                    VentCount = 0; CompCount = 0;
                }
            }
            Sensor_D = "00"; Sensor_RC = true;
        }

        public static void Input_ShockButton()
        {
            if (CurrentState == State.SHOCK_STANDBY)
            {
                CurrentState = State.COMPRESS;
                CompCount = 1;
                Sensor_P = "00"; Sensor_AMSA = false;
            }
        }

        public static void Toggle_SimulateVF()
        {
            if (Sensor_P == "00") { Sensor_P = "10"; Sensor_AMSA = true; }
            else { Sensor_P = "00"; Sensor_AMSA = false; }
        }
    }

    // =========================================================
    // BAGIAN 2: UI RESPONSIVE (LAYOUT ADAPTIF)
    // =========================================================
    public partial class Form1 : Form
    {
        private Timer guiTimer;
        private List<float> waveEKG = new List<float>();
        private List<float> waveComp = new List<float>();
        private int tick = 0;

        // Komponen Layout Utama
        private TableLayoutPanel mainLayout;
        private PictureBox graphPanel;
        // PERBAIKAN: Menggunakan PictureBox, bukan Panel, agar tidak berkedip
        private PictureBox infoPanel;
        private FlowLayoutPanel buttonPanel;

        public Form1()
        {
            InitializeComponentManual();
            // Double buffer agar grafik tidak kedip saat resize
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        }

        private void InitializeComponentManual()
        {
            this.Text = "MRI-Compatible Patient Monitor (Responsive)";
            this.Size = new Size(1024, 768);
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(240, 240, 240);

            // 1. MAIN LAYOUT
            mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowCount = 3;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));  // Grafik & Info
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));  // Tombol
            this.Controls.Add(mainLayout);

            // 2. HEADER
            Label header = new Label();
            header.Text = "MRI-COMPATIBLE PATIENT MONITOR SYSTEM";
            header.TextAlign = ContentAlignment.MiddleCenter;
            header.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            header.ForeColor = Color.White;
            header.BackColor = Color.FromArgb(0, 122, 204);
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            mainLayout.Controls.Add(header, 0, 0);

            // 3. AREA TENGAH
            TableLayoutPanel midSection = new TableLayoutPanel();
            midSection.Dock = DockStyle.Fill;
            midSection.ColumnCount = 2;
            midSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F)); // Grafik 70%
            midSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // Info 30%
            mainLayout.Controls.Add(midSection, 0, 1);

            // -> Grafik
            graphPanel = new PictureBox();
            graphPanel.Dock = DockStyle.Fill;
            graphPanel.BackColor = Color.White;
            graphPanel.BorderStyle = BorderStyle.FixedSingle;
            graphPanel.Paint += GraphPanel_Paint;
            midSection.Controls.Add(graphPanel, 0, 0);

            // -> Info Panel (PERBAIKAN: Menggunakan PictureBox)
            infoPanel = new PictureBox();
            infoPanel.Dock = DockStyle.Fill;
            infoPanel.BackColor = Color.FromArgb(230, 230, 230);
            infoPanel.Paint += InfoPanel_Paint;
            midSection.Controls.Add(infoPanel, 1, 0);

            // 4. AREA TOMBOL
            buttonPanel = new FlowLayoutPanel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.FlowDirection = FlowDirection.LeftToRight;
            buttonPanel.Padding = new Padding(20);
            buttonPanel.BackColor = Color.FromArgb(50, 50, 50);
            mainLayout.Controls.Add(buttonPanel, 0, 2);

            // Tambah Tombol
            AddButton("KOMPRESI (BAIK)", Color.SeaGreen, () => MicroController.Input_Compression("BAIK"));
            AddButton("KOMPRESI (DANGKAL)", Color.IndianRed, () => MicroController.Input_Compression("DANGKAL"));
            AddButton("KOMPRESI (LEANING)", Color.DarkOrange, () => MicroController.Input_Compression("LEANING"));
            AddButton("VENTILASI (V)", Color.DodgerBlue, () => MicroController.Input_Ventilation());
            AddButton("SIMULASI VF", Color.Gray, () => MicroController.Toggle_SimulateVF());
            AddButton("TOMBOL SHOCK", Color.Black, () => MicroController.Input_ShockButton());

            // 5. TIMER
            guiTimer = new Timer();
            guiTimer.Interval = 30;
            guiTimer.Tick += GuiTimer_Tick;
            guiTimer.Start();
        }

        private void AddButton(string text, Color bg, Action onClick)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Size = new Size(180, 60);
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btn.Margin = new Padding(10);
            btn.Click += (s, e) => onClick();
            buttonPanel.Controls.Add(btn);
        }

        private void GuiTimer_Tick(object sender, EventArgs e)
        {
            MicroController.ClockTick();

            // --- DATA GRAFIK ---
            // EKG
            float ekgVal = 0;
            if (MicroController.Sensor_P == "10") ekgVal = new Random().Next(-50, 50);
            else ekgVal = (tick % 30 == 0) ? -10 : (tick % 30 == 2) ? 100 : (tick % 30 == 5) ? -20 : 0;

            waveEKG.Add(ekgVal);
            if (waveEKG.Count > 300) waveEKG.RemoveAt(0);

            // Kompresi
            waveComp.Add(MicroController.CurrentDepthVal);
            if (waveComp.Count > 300) waveComp.RemoveAt(0);

            tick++;
            graphPanel.Invalidate();
            infoPanel.Invalidate();
        }

        // === MENGGAMBAR GRAFIK ===
        private void GraphPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = graphPanel.Width;
            int h = graphPanel.Height;

            // Grid
            Pen gridPen = new Pen(Color.FromArgb(220, 220, 220));
            for (int i = 0; i < w; i += 30) g.DrawLine(gridPen, i, 0, i, h);
            for (int i = 0; i < h; i += 30) g.DrawLine(gridPen, 0, i, w, i);

            // Grafik EKG
            float midEKG = h * 0.25f;
            Color ekgColor = MicroController.Out_Shock ? Color.Red : Color.Green;
            DrawResponsiveLine(g, waveEKG, ekgColor, midEKG, w);
            g.DrawString("ECG LEAD II", new Font("Segoe UI", 9, FontStyle.Bold), Brushes.Black, 10, 10);

            // Grafik Kompresi
            float midComp = h * 0.75f;
            DrawResponsiveLine(g, waveComp, Color.Blue, midComp, w);
            g.DrawString("COMPRESSION WAVEFORM", new Font("Segoe UI", 9, FontStyle.Bold), Brushes.Black, 10, midComp - 80);
        }

        private void DrawResponsiveLine(Graphics g, List<float> data, Color c, float yOffset, int width)
        {
            if (data.Count < 2) return;
            Pen p = new Pen(c, 2);

            float xStep = (float)width / 300f;

            for (int i = 0; i < data.Count - 1; i++)
            {
                g.DrawLine(p,
                    i * xStep, yOffset - data[i],
                    (i + 1) * xStep, yOffset - data[i + 1]);
            }
        }

        // === MENGGAMBAR PANEL INFO ===
        private void InfoPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = infoPanel.Width;

            int yPos = 20;
            int xCenter = w / 2;

            // 1. State Box
            DrawBox(g, "SYSTEM STATE", MicroController.CurrentState.ToString(), xCenter, yPos, Color.White, Color.Black);
            yPos += 90;

            // 2. Counter
            DrawBox(g, "COMPRESSION", MicroController.CompCount + " / 30", xCenter, yPos, Color.White, Color.Blue);
            yPos += 90;
            DrawBox(g, "VENTILATION", MicroController.VentCount + " / 2", xCenter, yPos, Color.White, Color.Teal);
            yPos += 100;

            // 3. LED Indicators
            DrawLedResponsive(g, "METRONOME", MicroController.Out_Metro, xCenter - 50, yPos);
            DrawLedResponsive(g, "VENT GUIDE", MicroController.Out_VentGuide, xCenter + 50, yPos);
            yPos += 60;

            // 4. Quality & Shock
            bool q = MicroController.Out_Quality;
            bool s = MicroController.Out_Shock;

            DrawStatusFlagResponsive(g, "QUALITY", q ? "GOOD" : "POOR", xCenter, yPos, q ? Color.SeaGreen : Color.Firebrick);
            yPos += 50;
            DrawStatusFlagResponsive(g, "SHOCK", s ? "ADVISED" : "NO", xCenter, yPos, s ? Color.Red : Color.Gray);
        }

        private void DrawBox(Graphics g, string title, string val, int x, int y, Color bg, Color textC)
        {
            int boxW = 200;
            int boxH = 70;
            Rectangle r = new Rectangle(x - (boxW / 2), y, boxW, boxH);
            g.FillRectangle(new SolidBrush(bg), r);
            g.DrawRectangle(Pens.Gray, r);

            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(title, new Font("Segoe UI", 8), Brushes.Gray, x, y + 10, sf);
            g.DrawString(val, new Font("Segoe UI", 16, FontStyle.Bold), new SolidBrush(textC), x, y + 40, sf);
        }

        private void DrawLedResponsive(Graphics g, string label, bool on, int x, int y)
        {
            Brush b = on ? Brushes.LimeGreen : Brushes.LightGray;
            g.FillEllipse(b, x - 10, y, 20, 20);
            g.DrawEllipse(Pens.Gray, x - 10, y, 20, 20);

            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(label, new Font("Segoe UI", 7, FontStyle.Bold), Brushes.DimGray, x, y + 25, sf);
        }

        private void DrawStatusFlagResponsive(Graphics g, string label, string val, int x, int y, Color c)
        {
            int boxW = 200;
            Rectangle r = new Rectangle(x - (boxW / 2), y, boxW, 40);
            g.FillRectangle(new SolidBrush(c), r);

            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(label + ": " + val, new Font("Segoe UI", 10, FontStyle.Bold), Brushes.White, r, sf);
        }
    }
}