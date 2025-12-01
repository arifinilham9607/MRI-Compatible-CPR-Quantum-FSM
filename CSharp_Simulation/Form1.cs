using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Linq;

namespace CprMicroSignalSim
{
    public static class MicroController
    {
        public enum State { IDLE, COMPRESS, VENTILATE, SHOCK_STANDBY }
        public static State CurrentState = State.IDLE;
        public static int CompCount = 0;
        public static int VentCount = 0;

        public static string Sensor_D = "00"; 
        public static string Sensor_R = "10"; 
        public static bool Sensor_RC = true;  
        public static string Sensor_P = "00"; 
        public static bool Sensor_AMSA = false;

        // Variabel untuk Simulasi Waveform FSM
        public class StatePoint
        {
            public int Time;
            public State FSMState;
        }
        public static List<StatePoint> WaveformHistory = new List<StatePoint>();
        public static int CurrentTimeTick = 0;

        public static bool Out_Metro = false;
        public static bool Out_VentGuide = false;
        public static bool Out_Quality = false;
        public static bool Out_Shock = false;

        public static void Initialize()
        {
            CurrentState = State.IDLE;
            CompCount = 0;
            VentCount = 0;
            CurrentTimeTick = 0;
            WaveformHistory.Clear();
            WaveformHistory.Add(new StatePoint { Time = 0, FSMState = State.IDLE });
        }

        public static void ClockTick()
        {
            Out_Metro = (CurrentState == State.COMPRESS);
            Out_VentGuide = (CurrentState == State.VENTILATE);
            Out_Quality = (Sensor_D == "10" && Sensor_R == "10" && Sensor_RC);
            Out_Shock = ((Sensor_P == "01" || Sensor_P == "10") && Sensor_AMSA);

            CurrentTimeTick++;
        }

        private static void UpdateState(State newState)
        {
            if (newState != CurrentState)
            {
                // Merekam transisi state di tick saat ini (garis vertikal)
                WaveformHistory.Add(new StatePoint { Time = CurrentTimeTick, FSMState = CurrentState });

                CurrentState = newState;

                // Merekam state baru di tick saat ini (garis horizontal baru)
                WaveformHistory.Add(new StatePoint { Time = CurrentTimeTick, FSMState = CurrentState });
            }
            // Merekam kelanjutan state di tick berikutnya
            WaveformHistory.Add(new StatePoint { Time = CurrentTimeTick + 1, FSMState = CurrentState });
        }

        public static void Input_Compression(string type)
        {
            // Atur Input Sensor berdasarkan Jenis Kompresi
            switch (type)
            {
                case "BAIK": Sensor_D = "10"; Sensor_RC = true; break;      
                case "DANGKAL": Sensor_D = "01"; Sensor_RC = true; break;  
                case "LEANING": Sensor_D = "10"; Sensor_RC = false; break;  
            }

            // PRIORITAS 1: SHOCK INTERRUPT
            if (Out_Shock)
            {
                UpdateState(State.SHOCK_STANDBY); // Transisi ke SHOCK_STANDBY jika kondisi terpenuhi
                return;
            }

            // PRIORITAS 2: LOGIKA SEKUENSIAL
            if (CurrentState == State.IDLE)
            {
                UpdateState(State.COMPRESS);
                CompCount = 1;
                VentCount = 0;
            }
            else if (CurrentState == State.COMPRESS)
            {
                CompCount++;
                if (CompCount >= 30)
                {
                    UpdateState(State.VENTILATE);
                    CompCount = 0;
                    VentCount = 0; // Mulai ventilasi di hitungan 1
                }
            }
            else if (CurrentState == State.VENTILATE)
            {
                // Input Kompresi di state VENTILATE diabaikan (hanya ventilasi yang dihitung)
            }
        }

        public static void Input_Ventilation()
        {
            // Prioritas Shock di sini tetap berlaku, tetapi tombol ini tidak mengubah Sensor_D/RC
            if (Out_Shock) return;

            if (CurrentState == State.VENTILATE)
            {
                VentCount++;
                if (VentCount >= 2)
                {
                    UpdateState(State.COMPRESS);
                    VentCount = 0;
                    CompCount = 0; // Kompresi berikutnya akan dihitung saat Input_Compression selanjutnya
                }
            }

            // Atur ulang kedalaman/recoil ke status aman (Full Recoil) setelah ventilasi
            Sensor_D = "00";
            Sensor_RC = true;
        }

        public static void Input_ShockButton()
        {
            if (CurrentState == State.SHOCK_STANDBY)
            {
                // Setelah SHOCK (Tombol Shock ditekan), sistem kembali ke COMPRESS
                UpdateState(State.COMPRESS);
                CompCount = 1;
                Sensor_P = "00"; Sensor_AMSA = false; // Reset kondisi Shockable
            }
        }

        public static void Toggle_SimulateVF()
        {
            // Mengubah kondisi VF/VT dan AMSA untuk memicu Out_Shock
            if (Sensor_P == "00")
            {
                Sensor_P = "10"; // Mengubah ke VF (Shockable)
                Sensor_AMSA = true; // Menganggap AMSA Tinggi
                if (CurrentState != State.SHOCK_STANDBY) UpdateState(State.SHOCK_STANDBY);
            }
            else
            {
                Sensor_P = "00";
                Sensor_AMSA = false;
                if (CurrentState == State.SHOCK_STANDBY) UpdateState(State.COMPRESS); // Kembali ke COMPRESS jika di-reset
            }
        }
    }

    // =========================================================
    // BAGIAN 2: UI DAN VISUALISASI GRAFIK (Waveform Digital)
    // =========================================================
    public partial class Form1 : Form
    {
        private Timer guiTimer;
        private int tick = 0;
        private Dictionary<MicroController.State, float> stateYMap;

        // Komponen Layout Utama
        private TableLayoutPanel mainLayout;
        private PictureBox graphPanel;
        private PictureBox infoPanel;
        private FlowLayoutPanel buttonPanel;

        public Form1()
        {
            MicroController.Initialize();
            InitializeComponentManual();
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        }

        private void InitializeComponentManual()
        {
            this.Text = "MRI-Compatible CPR RFSM Simulator";
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

            // -> Grafik (Hanya untuk Digital Waveform FSM)
            graphPanel = new PictureBox();
            graphPanel.Dock = DockStyle.Fill;
            graphPanel.BackColor = Color.White;
            graphPanel.BorderStyle = BorderStyle.FixedSingle;
            graphPanel.Paint += GraphPanel_Paint;
            midSection.Controls.Add(graphPanel, 0, 0);

            // -> Info Panel
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
            AddButton("COMPRESS (GOOD)", Color.SeaGreen, () => MicroController.Input_Compression("BAIK"));
            AddButton("COMPRESS (SHORT)", Color.IndianRed, () => MicroController.Input_Compression("DANGKAL"));
            AddButton("COMPRESS (LEANING)", Color.DarkOrange, () => MicroController.Input_Compression("LEANING"));
            AddButton("VENTILATE (V)", Color.DodgerBlue, () => MicroController.Input_Ventilation());
            AddButton("SIMULATE VF", Color.Purple, () => MicroController.Toggle_SimulateVF());
            AddButton("SHOCK!", Color.Black, () => MicroController.Input_ShockButton());

            // Tombol Reset Penuh
            Button btnReset = new Button();
            btnReset.Text = "FULL RESET";
            btnReset.Size = new Size(180, 60);
            btnReset.BackColor = Color.Gray;
            btnReset.ForeColor = Color.White;
            btnReset.FlatStyle = FlatStyle.Flat;
            btnReset.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnReset.Margin = new Padding(10);
            btnReset.Click += (s, e) => MicroController.Initialize();
            buttonPanel.Controls.Add(btnReset);

            // 5. TIMER
            guiTimer = new Timer();
            guiTimer.Interval = 30; // Kecepatan update UI (tick/30ms)
            guiTimer.Tick += GuiTimer_Tick;
            guiTimer.Start();

            // Inisialisasi State Y Map untuk Grafik (0.2f=Paling Atas, 0.9f=Paling Bawah)
            stateYMap = new Dictionary<MicroController.State, float>
            {
                { MicroController.State.IDLE, 0.2f },
                { MicroController.State.COMPRESS, 0.45f },
                { MicroController.State.VENTILATE, 0.7f },
                { MicroController.State.SHOCK_STANDBY, 0.9f }
            };
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
            tick++;
            graphPanel.Invalidate();
            infoPanel.Invalidate();
        }

        // === MENGGAMBAR GRAFIK DIGITAL FSM ===
        private void GraphPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = graphPanel.Width;
            int h = graphPanel.Height;

            // 1. Setup Area Gambar
            int marginX = 100;
            int marginY = 30;
            int graphWidth = w - marginX - 10;
            int graphHeight = h - marginY * 2;

            // Skala X: Total waktu yang akan ditampilkan
            // Sesuaikan agar seluruh riwayat terlihat
            int maxTicks = Math.Max(100, MicroController.CurrentTimeTick + 10);
            float xStep = (float)graphWidth / maxTicks;

            // Bersihkan Area
            g.Clear(Color.White);

            // 2. Garis Referensi Horizontal (State)
            using (Pen refPen = new Pen(Color.LightGray, 1))
            using (Font labelFont = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                foreach (var kvp in stateYMap)
                {
                    float y = marginY + (1 - kvp.Value) * graphHeight;
                    g.DrawLine(refPen, marginX, y, graphWidth + marginX, y);
                    g.DrawString(kvp.Key.ToString() + " (" + GetStateBinary(kvp.Key) + ")",
                                 labelFont,
                                 Brushes.Black,
                                 5, y - 8);
                }
            }

            // 3. Menggambar Waveform Digital (Seperti Vivado)
            if (MicroController.WaveformHistory.Count < 2) return;

            using (Pen wavePen = new Pen(Color.Green, 2)) // Menggunakan warna hijau terang (seperti Vivado)
            {
                for (int i = 0; i < MicroController.WaveformHistory.Count - 1; i++)
                {
                    MicroController.StatePoint p1 = MicroController.WaveformHistory[i];
                    MicroController.StatePoint p2 = MicroController.WaveformHistory[i + 1];

                    // Posisi Y (tinggi) untuk State p1 dan p2
                    float y1 = marginY + (1 - stateYMap[p1.FSMState]) * graphHeight;
                    float y2 = marginY + (1 - stateYMap[p2.FSMState]) * graphHeight;

                    // Posisi X di layar (berdasarkan tick)
                    float xScreen1 = marginX + p1.Time * xStep;
                    float xScreen2 = marginX + p2.Time * xStep;

                    // Garis Horizontal (Hold State)
                    g.DrawLine(wavePen, xScreen1, y1, xScreen2, y1);

                    // Garis Vertikal (Transition) - Hanya jika ada perubahan state
                    if (p1.FSMState != p2.FSMState)
                    {
                        g.DrawLine(wavePen, xScreen1, y1, xScreen1, y2);
                    }
                }

                // Tambahkan titik akhir agar waveform terlihat lengkap hingga CurrentTimeTick
                MicroController.StatePoint lastP = MicroController.WaveformHistory.Last();
                float lastY = marginY + (1 - stateYMap[lastP.FSMState]) * graphHeight;
                float lastX = marginX + lastP.Time * xStep;

                // Garis horizontal dari titik terakhir hingga waktu saat ini
                g.DrawLine(wavePen, lastX, lastY, marginX + MicroController.CurrentTimeTick * xStep, lastY);
            }

            // 4. Time Marker (Indikator Waktu Saat Ini)
            int currentX = marginX + MicroController.CurrentTimeTick * (int)xStep;
            using (Pen timePen = new Pen(Color.Red, 1) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(timePen, currentX, marginY, currentX, graphHeight + marginY);
            }
            g.DrawString($"T={MicroController.CurrentTimeTick}", new Font("Segoe UI", 8), Brushes.Red, currentX, graphHeight + marginY + 5);

            g.DrawString("FSM STATE WAVEFORM", new Font("Segoe UI", 10, FontStyle.Bold), Brushes.Black, 10, 10);
        }

        private string GetStateBinary(MicroController.State state)
        {
            switch (state)
            {
                case MicroController.State.IDLE: return "00";
                case MicroController.State.COMPRESS: return "01";
                case MicroController.State.VENTILATE: return "10";
                case MicroController.State.SHOCK_STANDBY: return "";
                default: return "XX";
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
            DrawBox(g, "SYSTEM STATE (S1S0)", MicroController.CurrentState.ToString() + " (" + GetStateBinary(MicroController.CurrentState) + ")", xCenter, yPos, Color.White, Color.Black);
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
            string qText = MicroController.Out_Quality ? "GOOD" : (MicroController.CurrentState == MicroController.State.COMPRESS ? "POOR" : "N/A");
            Color qColor = MicroController.Out_Quality ? Color.SeaGreen : Color.Firebrick;
            if (MicroController.CurrentState == MicroController.State.IDLE || MicroController.CurrentState == MicroController.State.VENTILATE) qColor = Color.Gray;

            DrawStatusFlagResponsive(g, "QUALITY", qText, xCenter, yPos, qColor);
            yPos += 50;

            bool s = MicroController.Out_Shock;
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
