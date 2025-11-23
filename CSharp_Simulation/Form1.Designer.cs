namespace CprMicroSignalSim
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "MRI Patient Monitor";
            // Kita akan memanggil SetupMedicalUI() dari Form1.cs
            // untuk menggambar isinya, jadi file ini tetap bersih.
        }

        #endregion
    }
}