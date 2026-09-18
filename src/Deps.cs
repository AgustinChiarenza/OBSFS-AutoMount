using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using Microsoft.Win32;

namespace ObsfsAutoMount
{
    /// <summary>Deteccion, descarga e instalacion de las dos dependencias: rclone y WinFsp.</summary>
    internal static class Deps
    {
        public const string WinFspVersion = "2.1";
        public const string WinFspMsiUrl =
            "https://github.com/winfsp/winfsp/releases/download/v2.1/winfsp-2.1.25156.msi";

        // ---------------------------------------------------------------- rclone

        public static bool RcloneInstalled()
        {
            return File.Exists(AppPaths.RcloneExe);
        }

        public static string RcloneVersion()
        {
            if (!RcloneInstalled()) return null;
            try
            {
                ProcResult r = Mounter.RunRclone("version", null, 15000);
                if (!r.Ok) return null;
                string[] lines = r.StdOut.Replace("\r", "").Split('\n');
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (t.StartsWith("rclone v", StringComparison.OrdinalIgnoreCase))
                        return t.Substring(7).Trim();
                }
                return "instalado";
            }
            catch { return null; }
        }

        private static string RcloneArchSuffix()
        {
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
            if (string.IsNullOrEmpty(arch)) arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if (arch == null) arch = "";
            arch = arch.ToUpperInvariant();
            if (arch.Contains("ARM64")) return "arm64";
            if (arch == "X86") return "386";
            return "amd64";
        }

        public static string RcloneZipUrl()
        {
            return "https://downloads.rclone.org/rclone-current-windows-" + RcloneArchSuffix() + ".zip";
        }

        /// <summary>Descarga rclone y extrae unicamente rclone.exe. Lanza excepcion si falla.</summary>
        public static void InstallRclone(Action<string, int> progress)
        {
            AppPaths.EnsureDirs();
            string zip = Path.Combine(AppPaths.DownloadDir, "rclone.zip");
            Report(progress, "Descargando rclone...", 0);
            Download(RcloneZipUrl(), zip, progress, "Descargando rclone");

            Report(progress, "Extrayendo rclone...", 95);
            string tmp = AppPaths.RcloneExe + ".new";
            if (File.Exists(tmp)) File.Delete(tmp);

            using (ZipArchive archive = ZipFile.OpenRead(zip))
            {
                ZipArchiveEntry entry = null;
                foreach (ZipArchiveEntry e in archive.Entries)
                {
                    if (e.Name.Equals("rclone.exe", StringComparison.OrdinalIgnoreCase)) { entry = e; break; }
                }
                if (entry == null) throw new Exception("El paquete descargado no contiene rclone.exe.");
                entry.ExtractToFile(tmp, true);
            }

            if (File.Exists(AppPaths.RcloneExe))
            {
                try
                {
                    File.Delete(AppPaths.RcloneExe);
                }
                catch (Exception ex)
                {
                    throw new Exception("No se pudo reemplazar rclone.exe. Desmontar la unidad e intentar de nuevo. " + ex.Message);
                }
            }
            File.Move(tmp, AppPaths.RcloneExe);
            try { File.Delete(zip); }
            catch { }
            Report(progress, "rclone instalado.", 100);
        }

        // ---------------------------------------------------------------- WinFsp

        public static bool WinFspInstalled()
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey k = hklm.OpenSubKey("SOFTWARE\\WinFsp"))
                {
                    if (k != null)
                    {
                        object dir = k.GetValue("InstallDir");
                        if (dir != null && Directory.Exists(dir.ToString())) return true;
                    }
                }
            }
            catch { }

            string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (string.IsNullOrEmpty(pf86)) pf86 = "C:\\Program Files (x86)";
            string binDir = Path.Combine(pf86, "WinFsp\\bin");
            if (Directory.Exists(binDir))
            {
                if (File.Exists(Path.Combine(binDir, "winfsp-x64.dll"))) return true;
                if (File.Exists(Path.Combine(binDir, "winfsp-a64.dll"))) return true;
                if (File.Exists(Path.Combine(binDir, "winfsp-x86.dll"))) return true;
            }

            // El driver tambien queda instalado como servicio del sistema.
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return File.Exists(Path.Combine(sys, "drivers\\winfsp-x64.sys"))
                || File.Exists(Path.Combine(sys, "drivers\\winfsp-a64.sys"));
        }

        /// <summary>
        /// Descarga el MSI oficial de WinFsp y lo instala. Requiere elevacion: dispara el aviso
        /// de UAC una unica vez. Devuelve false si el usuario cancela ese aviso.
        /// </summary>
        public static bool InstallWinFsp(Action<string, int> progress)
        {
            AppPaths.EnsureDirs();
            string msi = Path.Combine(AppPaths.DownloadDir, "winfsp.msi");
            Report(progress, "Descargando WinFsp " + WinFspVersion + "...", 0);
            Download(WinFspMsiUrl, msi, progress, "Descargando WinFsp");

            Report(progress, "Instalando WinFsp (aceptar el aviso de Windows)...", 96);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "msiexec.exe";
            psi.Arguments = "/i \"" + msi + "\" /qb /norestart";
            psi.UseShellExecute = true;
            psi.Verb = "runas";            // eleva -> UAC

            try
            {
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    if (p.ExitCode != 0 && p.ExitCode != 3010)
                        throw new Exception("El instalador de WinFsp devolvio el codigo " + p.ExitCode + ".");
                }
            }
            catch (Win32Exception w)
            {
                if (w.NativeErrorCode == 1223)   // ERROR_CANCELLED: el usuario rechazo el UAC
                {
                    Report(progress, "Instalacion de WinFsp cancelada.", 0);
                    return false;
                }
                throw;
            }

            try { File.Delete(msi); }
            catch { }
            Report(progress, "WinFsp instalado.", 100);
            return true;
        }

        // ---------------------------------------------------------------- helpers

        private static void Report(Action<string, int> progress, string text, int pct)
        {
            if (progress != null) progress(text, pct);
        }

        /// <summary>Descarga sincronica con reporte de progreso (se invoca desde un hilo de fondo).</summary>
        private static void Download(string url, string dest, Action<string, int> progress, string label)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = AppPaths.AppName;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 120000;
            req.AllowAutoRedirect = true;

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (FileStream dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
            {
                long total = resp.ContentLength;
                byte[] buf = new byte[81920];
                long done = 0;
                int read;
                int lastPct = -1;
                while ((read = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, read);
                    done += read;
                    int pct = total > 0 ? (int)(done * 92 / total) : 0;
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        string mb = (done / 1048576.0).ToString("0.0");
                        string totMb = total > 0 ? (total / 1048576.0).ToString("0.0") : "?";
                        Report(progress, label + "   " + mb + " / " + totMb + " MB", pct);
                    }
                }
            }
        }
    }
}
