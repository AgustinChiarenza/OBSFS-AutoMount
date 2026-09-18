using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace ObsfsAutoMount
{
    internal static class Program
    {
        private const string MutexName = "Local\\OBSFS-AutoMount-SingleInstance-v1";

        [STAThread]
        private static int Main(string[] args)
        {
            // Windows 10/11 negocia TLS 1.2/1.3, pero .NET Framework no siempre lo toma por defecto.
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288;   // TLS 1.3
            }
            catch { }
            ServicePointManager.DefaultConnectionLimit = 8;
            ServicePointManager.Expect100Continue = false;

            bool autoMount = false;
            bool cliUnmount = false;
            foreach (string a in args)
            {
                string s = (a ?? "").Trim().ToLowerInvariant();
                if (s == Startup.AutoStartArg || s == "/autostart") autoMount = true;
                else if (s == "--unmount" || s == "/unmount") cliUnmount = true;
            }

            AppPaths.EnsureDirs();

            // Modo consola: desmonta y sale (util para scripts o para un acceso directo).
            if (cliUnmount)
            {
                MountState st = MountState.Load();
                if (st.IsAlive()) Mounter.Unmount(st, null);
                else Mounter.KillOrphanRclone();
                return 0;
            }

            bool isFirstInstance;
            using (Mutex mutex = new Mutex(true, MutexName, out isFirstInstance))
            {
                if (!isFirstInstance)
                {
                    Native.BroadcastShowMe();   // la instancia viva se trae al frente
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    AppPaths.Log("ThreadException: " + e.Exception);
                    MessageBox.Show("Ocurrio un error inesperado:\r\n\r\n" + e.Exception.Message,
                        "OBSFS AutoMount", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    AppPaths.Log("UnhandledException: " + e.ExceptionObject);
                };

                Application.Run(new MainForm(autoMount));
                GC.KeepAlive(mutex);
            }
            return 0;
        }
    }
}
