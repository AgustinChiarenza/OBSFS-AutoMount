using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ObsfsAutoMount
{
    internal class ProcResult
    {
        public int ExitCode = -1;
        public string StdOut = "";
        public string StdErr = "";
        public bool TimedOut = false;
        public bool Ok { get { return ExitCode == 0 && !TimedOut; } }

        /// <summary>Mensaje de error legible extraido de la salida de rclone.</summary>
        public string FriendlyError()
        {
            if (TimedOut) return "La operacion supero el tiempo de espera.";
            string blob = (StdErr + "\n" + StdOut).Replace("\r", "");
            string[] lines = blob.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string t = lines[i].Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("Usage:") || t.StartsWith("Flags:")) continue;
                return Mounter.Humanize(t);
            }
            return "rclone termino con el codigo " + ExitCode + ".";
        }
    }

    internal class MountState
    {
        public int Pid;
        public int RcPort;
        public string Drive = "";
        public string Bucket = "";
        public string StartedUtc = "";

        public bool IsAlive()
        {
            if (Pid <= 0) return false;
            try
            {
                Process p = Process.GetProcessById(Pid);
                if (p.HasExited) return false;
                return p.ProcessName.Equals("rclone", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public bool DriveVisible()
        {
            if (string.IsNullOrEmpty(Drive)) return false;
            try { return Directory.Exists(Drive.TrimEnd('\\') + "\\"); }
            catch { return false; }
        }

        public static MountState Load()
        {
            Dictionary<string, string> d = IniStore.Read(AppPaths.StateFile);
            MountState s = new MountState();
            s.Pid = IniStore.GetInt(d, "pid", 0);
            s.RcPort = IniStore.GetInt(d, "rc_port", 0);
            s.Drive = IniStore.Get(d, "drive", "");
            s.Bucket = IniStore.Get(d, "bucket", "");
            s.StartedUtc = IniStore.Get(d, "started_utc", "");
            return s;
        }

        public void Save()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            d["pid"] = Pid.ToString();
            d["rc_port"] = RcPort.ToString();
            d["drive"] = Drive;
            d["bucket"] = Bucket;
            d["started_utc"] = StartedUtc;
            IniStore.Write(AppPaths.StateFile, d);
        }

        public static void Clear()
        {
            try { if (File.Exists(AppPaths.StateFile)) File.Delete(AppPaths.StateFile); }
            catch { }
        }
    }

    /// <summary>
    /// Toda la interaccion con rclone. El remoto NO se persiste en rclone.conf: se inyecta como
    /// variables de entorno del proceso hijo, asi la Secret Key nunca queda en disco en claro
    /// ni aparece en la linea de comandos (visible para otros procesos).
    /// </summary>
    internal static class Mounter
    {
        private static string EnvPrefix
        {
            get { return "RCLONE_CONFIG_" + AppConfig.RemoteName.ToUpperInvariant() + "_"; }
        }

        private static void ApplyRemoteEnv(ProcessStartInfo psi, AppConfig cfg)
        {
            if (cfg == null) return;
            psi.EnvironmentVariables[EnvPrefix + "TYPE"] = "s3";
            psi.EnvironmentVariables[EnvPrefix + "PROVIDER"] = "HuaweiOBS";
            psi.EnvironmentVariables[EnvPrefix + "ENV_AUTH"] = "false";
            psi.EnvironmentVariables[EnvPrefix + "ACCESS_KEY_ID"] = (cfg.AccessKey ?? "").Trim();
            psi.EnvironmentVariables[EnvPrefix + "SECRET_ACCESS_KEY"] = (cfg.SecretKey ?? "").Trim();
            psi.EnvironmentVariables[EnvPrefix + "ENDPOINT"] = cfg.NormalizedEndpoint();
            psi.EnvironmentVariables[EnvPrefix + "ACL"] = "private";
            // Sin color ANSI en la salida capturada.
            psi.EnvironmentVariables["RCLONE_COLOR"] = "NEVER";
        }

        public static string Q(string s)
        {
            if (s == null) s = "";
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        private static string ConfigArg()
        {
            return " --config " + Q(AppPaths.RcloneConf);
        }

        /// <summary>Ejecuta rclone capturando la salida. cfg puede ser null (p.ej. para "version").</summary>
        public static ProcResult RunRclone(string args, AppConfig cfg, int timeoutMs)
        {
            ProcResult r = new ProcResult();
            if (!Deps.RcloneInstalled())
            {
                r.StdErr = "rclone no esta instalado.";
                return r;
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = AppPaths.RcloneExe;
            psi.Arguments = args + ConfigArg();
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.WorkingDirectory = AppPaths.Root;
            ApplyRemoteEnv(psi, cfg);

            StringBuilder so = new StringBuilder();
            StringBuilder se = new StringBuilder();

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) so.AppendLine(e.Data);
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) se.AppendLine(e.Data);
                };

                try
                {
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        r.TimedOut = true;
                        try { p.Kill(); }
                        catch { }
                    }
                    else
                    {
                        r.ExitCode = p.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    r.StdErr = ex.Message;
                    return r;
                }
            }

            r.StdOut = so.ToString();
            r.StdErr = se.ToString();
            return r;
        }

        // ------------------------------------------------------------ prueba de conexion

        /// <summary>
        /// Valida credenciales y endpoint. Devuelve la lista de buckets visibles cuando la clave
        /// tiene permiso para listarlos; si la clave esta acotada a un bucket, valida ese bucket.
        /// </summary>
        public static bool TestConnection(AppConfig cfg, out List<string> buckets, out string error)
        {
            buckets = new List<string>();
            error = null;

            string netArgs = " --contimeout 15s --timeout 30s --retries 1 --low-level-retries 2";
            ProcResult r = RunRclone("lsd " + AppConfig.RemoteName + ":" + netArgs, cfg, 60000);

            if (r.Ok)
            {
                // Formato de lsd:  "          -1 2024-01-01 12:00:00        -1 nombre-bucket"
                string[] lines = r.StdOut.Replace("\r", "").Split('\n');
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (t.Length == 0) continue;
                    string[] parts = t.Split(new char[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5) buckets.Add(parts[4].Trim());
                    else if (parts.Length > 0) buckets.Add(parts[parts.Length - 1].Trim());
                }
                return true;
            }

            // La clave puede no tener permiso de ListAllMyBuckets: probamos directo contra el bucket.
            string bucket = (cfg.Bucket ?? "").Trim();
            if (bucket.Length > 0)
            {
                ProcResult r2 = RunRclone(
                    "lsd " + AppConfig.RemoteName + ":" + bucket + " --max-depth 1" + netArgs, cfg, 60000);
                if (r2.Ok)
                {
                    buckets.Add(bucket);
                    return true;
                }
                error = r2.FriendlyError();
                return false;
            }

            error = r.FriendlyError();
            return false;
        }

        // ------------------------------------------------------------ montaje

        private static int FreeTcpPort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static string BuildMountArgs(AppConfig cfg, int rcPort)
        {
            StringBuilder a = new StringBuilder();
            a.Append("mount ");
            a.Append(AppConfig.RemoteName).Append(":").Append(cfg.Bucket.Trim());
            a.Append(" ").Append(cfg.MountPoint());

            string mode = string.IsNullOrEmpty(cfg.CacheMode) ? "writes" : cfg.CacheMode;
            a.Append(" --vfs-cache-mode ").Append(mode);
            if (!mode.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(cfg.CacheMaxSize))
                    a.Append(" --vfs-cache-max-size ").Append(cfg.CacheMaxSize);
            }
            if (!string.IsNullOrEmpty(cfg.DirCacheTime))
                a.Append(" --dir-cache-time ").Append(cfg.DirCacheTime);

            a.Append(" --cache-dir ").Append(Q(AppPaths.CacheDir));

            string label = string.IsNullOrEmpty(cfg.VolumeLabel) ? "OBS" : cfg.VolumeLabel;
            a.Append(" --volname ").Append(Q(label));

            if (cfg.NetworkMode) a.Append(" --network-mode");
            if (cfg.ReadOnly) a.Append(" --read-only");

            // Control remoto local: permite desmontar de forma limpia (vacia la cola de subida).
            a.Append(" --rc --rc-addr 127.0.0.1:").Append(rcPort).Append(" --rc-no-auth");

            a.Append(" --log-file ").Append(Q(AppPaths.MountLog));
            a.Append(" --log-level INFO");
            a.Append(" --no-console");
            return a.ToString();
        }

        /// <summary>Linea de comando completa, para mostrar al usuario (sin secretos).</summary>
        public static string PreviewCommand(AppConfig cfg)
        {
            return AppPaths.RcloneExe + " " + BuildMountArgs(cfg, 5572) + " --config " + AppPaths.RcloneConf;
        }

        /// <summary>
        /// Monta el bucket. Devuelve true si la unidad aparecio en el Explorador.
        /// progress recibe mensajes de estado intermedios.
        /// </summary>
        public static bool Mount(AppConfig cfg, Action<string> progress, out string error)
        {
            error = null;
            AppPaths.EnsureDirs();

            string why = cfg.Validate();
            if (why != null) { error = why; return false; }
            if (!Deps.RcloneInstalled()) { error = "Falta instalar rclone."; return false; }
            if (!Deps.WinFspInstalled()) { error = "Falta instalar WinFsp."; return false; }

            MountState prev = MountState.Load();
            if (prev.IsAlive())
            {
                if (prev.Drive.Equals(cfg.MountPoint(), StringComparison.OrdinalIgnoreCase) && prev.DriveVisible())
                    return true;                       // ya montado en la misma letra
                Unmount(prev, null);                   // montaje viejo: bajarlo primero
            }

            if (Directory.Exists(cfg.DriveRoot()))
            {
                error = "La letra " + cfg.MountPoint() + " ya esta en uso por otra unidad. Elegir otra.";
                return false;
            }

            TruncateLogIfBig();

            int rcPort = FreeTcpPort();
            string args = BuildMountArgs(cfg, rcPort);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = AppPaths.RcloneExe;
            psi.Arguments = args + ConfigArg();
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = AppPaths.Root;
            ApplyRemoteEnv(psi, cfg);

            Process proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                error = "No se pudo iniciar rclone: " + ex.Message;
                return false;
            }

            if (progress != null) progress("Montando " + cfg.MountPoint() + " ...");

            // rclone tarda unos segundos en negociar con WinFsp y validar el bucket.
            DateTime deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline)
            {
                if (proc.HasExited)
                {
                    error = "rclone se cerro sin montar la unidad.\r\n\r\n" + LastLogError();
                    MountState.Clear();
                    return false;
                }
                if (Directory.Exists(cfg.DriveRoot()))
                {
                    MountState st = new MountState();
                    st.Pid = proc.Id;
                    st.RcPort = rcPort;
                    st.Drive = cfg.MountPoint();
                    st.Bucket = cfg.Bucket;
                    st.StartedUtc = DateTime.UtcNow.ToString("o");
                    st.Save();
                    if (progress != null) progress("Unidad " + cfg.MountPoint() + " montada.");
                    return true;
                }
                Thread.Sleep(400);
            }

            error = "La unidad no aparecio despues de 45 segundos.\r\n\r\n" + LastLogError();
            try { if (!proc.HasExited) proc.Kill(); }
            catch { }
            MountState.Clear();
            return false;
        }

        /// <summary>Cantidad de archivos pendientes de subir a OBS, o -1 si no se pudo consultar.</summary>
        public static int PendingUploads(MountState st)
        {
            if (st == null || st.RcPort <= 0) return -1;
            string json = RcPost(st.RcPort, "vfs/stats", "{}", 4000);
            if (json == null) return -1;
            int total = 0;
            bool found = false;
            foreach (string key in new string[] { "uploadsInProgress", "uploadsQueued" })
            {
                Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)");
                if (m.Success) { total += int.Parse(m.Groups[1].Value); found = true; }
            }
            return found ? total : -1;
        }

        /// <summary>Desmonta: primero pide salida limpia por el control remoto, luego fuerza.</summary>
        public static void Unmount(MountState st, Action<string> progress)
        {
            if (st == null) return;

            if (st.RcPort > 0 && st.IsAlive())
            {
                if (progress != null) progress("Cerrando rclone y vaciando la cola de subida...");
                RcPost(st.RcPort, "core/quit", "{}", 5000);
            }

            for (int i = 0; i < 60 && st.IsAlive(); i++) Thread.Sleep(250);   // hasta 15 s

            if (st.IsAlive())
            {
                if (progress != null) progress("Forzando el cierre de rclone...");
                try { Process.GetProcessById(st.Pid).Kill(); }
                catch (Exception ex) { AppPaths.Log("Unmount kill: " + ex.Message); }
                for (int i = 0; i < 20 && st.IsAlive(); i++) Thread.Sleep(250);
            }

            MountState.Clear();
            if (progress != null) progress("Unidad desmontada.");
        }

        /// <summary>Mata cualquier rclone hijo huerfano de esta app (usado como reparacion).</summary>
        public static int KillOrphanRclone()
        {
            int n = 0;
            try
            {
                foreach (Process p in Process.GetProcessesByName("rclone"))
                {
                    try
                    {
                        if (p.MainModule != null &&
                            string.Equals(p.MainModule.FileName, AppPaths.RcloneExe, StringComparison.OrdinalIgnoreCase))
                        {
                            p.Kill();
                            n++;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return n;
        }

        // ------------------------------------------------------------ utilitarios

        private static string RcPost(int port, string endpoint, string body, int timeoutMs)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                    "http://127.0.0.1:" + port + "/" + endpoint);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.Proxy = null;
                byte[] data = Encoding.UTF8.GetBytes(body ?? "{}");
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                    return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                AppPaths.Log("RcPost " + endpoint + ": " + ex.Message);
                return null;
            }
        }

        private static void TruncateLogIfBig()
        {
            try
            {
                FileInfo fi = new FileInfo(AppPaths.MountLog);
                if (fi.Exists && fi.Length > 5 * 1024 * 1024) fi.Delete();
            }
            catch { }
        }

        /// <summary>Ultimas lineas relevantes del log de montaje, para explicar una falla.</summary>
        public static string LastLogError()
        {
            try
            {
                if (!File.Exists(AppPaths.MountLog)) return "(sin log disponible)";
                string[] all = ReadAllLinesShared(AppPaths.MountLog);
                List<string> keep = new List<string>();
                for (int i = all.Length - 1; i >= 0 && keep.Count < 6; i--)
                {
                    string t = all[i].Trim();
                    if (t.Length == 0) continue;
                    keep.Insert(0, Humanize(t));
                }
                return keep.Count == 0 ? "(log vacio)" : string.Join("\r\n", keep.ToArray());
            }
            catch (Exception ex) { return "(no se pudo leer el log: " + ex.Message + ")"; }
        }

        public static string[] ReadAllLinesShared(string path)
        {
            List<string> lines = new List<string>();
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs))
            {
                string l;
                while ((l = sr.ReadLine()) != null) lines.Add(l);
            }
            return lines.ToArray();
        }

        /// <summary>Traduce los errores mas frecuentes de rclone/OBS a algo accionable.</summary>
        public static string Humanize(string raw)
        {
            string s = raw;
            if (s.IndexOf("InvalidAccessKeyId", StringComparison.OrdinalIgnoreCase) >= 0)
                return "La Access Key (AK) no es valida para este endpoint. " + s;
            if (s.IndexOf("SignatureDoesNotMatch", StringComparison.OrdinalIgnoreCase) >= 0)
                return "La Secret Key (SK) no coincide con la AK. " + s;
            if (s.IndexOf("NoSuchBucket", StringComparison.OrdinalIgnoreCase) >= 0)
                return "El bucket no existe en esta region. Verificar nombre y endpoint. " + s;
            if (s.IndexOf("AccessDenied", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Acceso denegado: la clave no tiene permisos sobre ese bucket. " + s;
            if (s.IndexOf("no such host", StringComparison.OrdinalIgnoreCase) >= 0)
                return "No se pudo resolver el endpoint. Revisar que este bien escrito y que haya Internet. " + s;
            if (s.IndexOf("cannot find winfsp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("failed to mount FUSE fs", StringComparison.OrdinalIgnoreCase) >= 0)
                return "WinFsp no esta disponible. Instalarlo desde el panel de requisitos. " + s;
            return s;
        }

        /// <summary>Letras de unidad libres, de la D a la Z.</summary>
        public static List<string> FreeDriveLetters()
        {
            HashSet<char> used = new HashSet<char>();
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                    used.Add(char.ToUpperInvariant(d.Name[0]));
            }
            catch { }

            List<string> free = new List<string>();
            for (char c = 'D'; c <= 'Z'; c++)
                if (!used.Contains(c)) free.Add(c.ToString());
            return free;
        }
    }
}
