using System;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("AutoShorts")]
[assembly: AssemblyProduct("AutoShorts")]
[assembly: AssemblyDescription("Автоматична нарізка відео у вертикальні шортси з субтитрами")]
[assembly: AssemblyVersion(AutoShorts.AppVersion.Text + ".0")]
[assembly: AssemblyFileVersion(AutoShorts.AppVersion.Text + ".0")]

namespace AutoShorts
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            ServicePointManager.DefaultConnectionLimit = 16;
            try { if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware(); } catch { }

            bool created;
            using (var mutex = new Mutex(true, "AutoShorts_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("AutoShorts вже запущено.", "AutoShorts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => ShowError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);
                Application.Run(new MainForm());
            }
        }

        static void ShowError(Exception ex)
        {
            try
            {
                MessageBox.Show(ex == null ? "Невідома помилка" : ex.Message, "AutoShorts — помилка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
