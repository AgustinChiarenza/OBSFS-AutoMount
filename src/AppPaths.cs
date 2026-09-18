using System;
using System.IO;
using System.Reflection;

namespace ObsfsAutoMount
{
    /// <summary>Rutas fijas de la aplicacion. Todo vive bajo %LOCALAPPDATA% para no requerir admin.</summary>
    internal static class AppPaths
    {
        public const string AppName = "OBSFS-AutoMount";

        public static string Root
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            }
        }

        public static string Bin          { get { return Path.Combine(Root, "bin"); } }
        public static string RcloneExe    { get { return Path.Combine(Bin, "rclone.exe"); } }
        public static string RcloneConf   { get { return Path.Combine(Root, "rclone.conf"); } }
        public static string CacheDir     { get { return Path.Combine(Root, "cache"); } }
        public static string LogDir       { get { return Path.Combine(Root, "logs"); } }
        public static string MountLog     { get { return Path.Combine(LogDir, "mount.log"); } }
        public static string AppLog       { get { return Path.Combine(LogDir, "app.log"); } }
        public static string SettingsFile { get { return Path.Combine(Root, "settings.ini"); } }
        public static string StateFile    { get { return Path.Combine(Root, "state.ini"); } }
        public static string DownloadDir  { get { return Path.Combine(Root, "download"); } }

        /// <summary>Ruta completa del propio ejecutable (se usa para la clave Run de inicio automatico).</summary>
        public static string SelfExe
        {
            get
            {
                try
                {
                    Assembly a = Assembly.GetEntryAssembly();
                    if (a != null && !string.IsNullOrEmpty(a.Location)) return a.Location;
                }
                catch { }
                return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            }
        }

        public static void EnsureDirs()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Bin);
            Directory.CreateDirectory(CacheDir);
            Directory.CreateDirectory(LogDir);
            Directory.CreateDirectory(DownloadDir);

            // rclone exige un archivo de configuracion valido aunque el remoto se defina por variables
            // de entorno. Se crea vacio para no tocar la configuracion personal del usuario (~/.config/rclone).
            if (!File.Exists(RcloneConf))
            {
                File.WriteAllText(RcloneConf,
                    "# Archivo gestionado por OBSFS-AutoMount." + Environment.NewLine +
                    "# El remoto se inyecta por variables de entorno; las credenciales NO se guardan aca." + Environment.NewLine);
            }
        }

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(AppLog,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
