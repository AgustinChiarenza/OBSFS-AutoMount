using System;
using System.Collections.Generic;
using System.IO;

namespace ObsfsAutoMount
{
    internal class AppConfig
    {
        /// <summary>Nombre interno del remoto rclone. Fijo: se inyecta por variables de entorno.</summary>
        public const string RemoteName = "obs";

        public string Endpoint     = "";
        public string AccessKey    = "";
        public string SecretKey    = "";   // en claro solo en memoria
        public string Bucket       = "";
        public string Prefix       = "";   // subcarpeta dentro del bucket, opcional
        public string DriveLetter  = "O";
        public string VolumeLabel  = "OBS";
        public string CacheMode    = "writes";
        public string CacheMaxSize = "50G";
        public string DirCacheTime = "24h";
        public bool   NetworkMode  = false;
        public bool   ReadOnly     = false;
        public bool   AutoStart    = false;
        public int    StartupDelay = 8;    // segundos de espera antes de montar al iniciar sesion

        // --- red corporativa
        public string HttpsProxy   = "";   // ej: http://proxy.empresa.local:8080
        public string CaCertPath   = "";   // CA raiz de la empresa, para proxies que inspeccionan TLS
        public bool   NoCheckCert  = false;

        public static AppConfig Load()
        {
            AppConfig c = new AppConfig();
            Dictionary<string, string> d = IniStore.Read(AppPaths.SettingsFile);
            if (d.Count == 0) return c;

            c.Endpoint     = IniStore.Get(d, "endpoint", c.Endpoint);
            c.AccessKey    = IniStore.Get(d, "access_key", c.AccessKey);
            c.SecretKey    = Crypto.Unprotect(IniStore.Get(d, "secret_key_enc", ""));
            c.Bucket       = IniStore.Get(d, "bucket", c.Bucket);
            c.Prefix       = IniStore.Get(d, "prefix", c.Prefix);
            c.DriveLetter  = IniStore.Get(d, "drive_letter", c.DriveLetter);
            c.VolumeLabel  = IniStore.Get(d, "volume_label", c.VolumeLabel);
            c.CacheMode    = IniStore.Get(d, "cache_mode", c.CacheMode);
            c.CacheMaxSize = IniStore.Get(d, "cache_max_size", c.CacheMaxSize);
            c.DirCacheTime = IniStore.Get(d, "dir_cache_time", c.DirCacheTime);
            c.NetworkMode  = IniStore.GetBool(d, "network_mode", c.NetworkMode);
            c.ReadOnly     = IniStore.GetBool(d, "read_only", c.ReadOnly);
            c.AutoStart    = IniStore.GetBool(d, "auto_start", c.AutoStart);
            c.StartupDelay = IniStore.GetInt(d, "startup_delay", c.StartupDelay);
            c.HttpsProxy   = IniStore.Get(d, "https_proxy", c.HttpsProxy);
            c.CaCertPath   = IniStore.Get(d, "ca_cert", c.CaCertPath);
            c.NoCheckCert  = IniStore.GetBool(d, "no_check_cert", c.NoCheckCert);
            return c;
        }

        public void Save()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            d["endpoint"]       = Endpoint;
            d["access_key"]     = AccessKey;
            d["secret_key_enc"] = Crypto.Protect(SecretKey);
            d["bucket"]         = Bucket;
            d["prefix"]         = Prefix;
            d["drive_letter"]   = DriveLetter;
            d["volume_label"]   = VolumeLabel;
            d["cache_mode"]     = CacheMode;
            d["cache_max_size"] = CacheMaxSize;
            d["dir_cache_time"] = DirCacheTime;
            d["network_mode"]   = NetworkMode ? "1" : "0";
            d["read_only"]      = ReadOnly ? "1" : "0";
            d["auto_start"]     = AutoStart ? "1" : "0";
            d["startup_delay"]  = StartupDelay.ToString();
            d["https_proxy"]    = HttpsProxy;
            d["ca_cert"]        = CaCertPath;
            d["no_check_cert"]  = NoCheckCert ? "1" : "0";
            IniStore.Write(AppPaths.SettingsFile, d);

            try
            {
                // Permisos restrictivos: solo el usuario actual puede leer el archivo.
                FileInfo fi = new FileInfo(AppPaths.SettingsFile);
                System.Security.AccessControl.FileSecurity fs = fi.GetAccessControl();
                fs.SetAccessRuleProtection(true, false);
                fs.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    System.Security.Principal.WindowsIdentity.GetCurrent().User,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                fi.SetAccessControl(fs);
            }
            catch (Exception ex) { AppPaths.Log("AppConfig.Save ACL: " + ex.Message); }
        }

        /// <summary>Normaliza el endpoint: sin esquema, sin barra final, sin espacios.</summary>
        public string NormalizedEndpoint()
        {
            string e = (Endpoint ?? "").Trim();
            if (e.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) e = e.Substring(8);
            else if (e.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) e = e.Substring(7);
            return e.TrimEnd('/');
        }

        /// <summary>Prefijo sin barras sobrantes y siempre con separador de URL.</summary>
        public string NormalizedPrefix()
        {
            const char Bs = (char)92;   // barra invertida
            string p = (Prefix ?? "").Trim().Replace(Bs, '/');
            while (p.StartsWith("/")) p = p.Substring(1);
            while (p.EndsWith("/")) p = p.Substring(0, p.Length - 1);
            return p;
        }

        /// <summary>Ruta remota a montar: obs:bucket, o obs:bucket/subcarpeta si hay prefijo.</summary>
        public string RemotePath()
        {
            string p = NormalizedPrefix();
            string b = (Bucket ?? "").Trim();
            if (p.Length == 0) return RemoteName + ":" + b;
            return RemoteName + ":" + b + "/" + p;
        }

        public string MountPoint()
        {
            string d = (DriveLetter ?? "O").Trim().TrimEnd(':').TrimEnd(Path.DirectorySeparatorChar);
            return d.ToUpperInvariant() + ":";
        }

        public string DriveRoot()
        {
            return MountPoint() + "\\";
        }

        /// <summary>Devuelve null si la configuracion alcanza para montar; si no, el motivo.</summary>
        public string Validate()
        {
            if (string.IsNullOrEmpty(NormalizedEndpoint())) return "Falta el endpoint de OBS.";
            if (string.IsNullOrEmpty((AccessKey ?? "").Trim())) return "Falta la Access Key (AK).";
            if (string.IsNullOrEmpty((SecretKey ?? "").Trim())) return "Falta la Secret Key (SK).";
            if (string.IsNullOrEmpty((Bucket ?? "").Trim())) return "Falta el nombre del bucket.";
            if (string.IsNullOrEmpty((DriveLetter ?? "").Trim())) return "Falta elegir una letra de unidad.";
            return null;
        }
    }
}
