using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ObsfsAutoMount
{
    /// <summary>Almacen plano clave=valor. Evita dependencias de serializadores JSON.</summary>
    internal static class IniStore
    {
        public static Dictionary<string, string> Read(string path)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return d;
            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string key = line.Substring(0, i).Trim();
                    string val = Unescape(line.Substring(i + 1));
                    d[key] = val;
                }
            }
            catch (Exception ex) { AppPaths.Log("IniStore.Read " + path + ": " + ex.Message); }
            return d;
        }

        public static void Write(string path, Dictionary<string, string> data)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# " + AppPaths.AppName + " - generado automaticamente, no editar a mano");
            foreach (KeyValuePair<string, string> kv in data)
            {
                sb.AppendLine(kv.Key + "=" + Escape(kv.Value));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // Se escribe caracter por caracter para no depender de literales con barra invertida.
        private const char Bs = (char)92;   // '\'

        public static string Escape(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            StringBuilder sb = new StringBuilder(v.Length + 8);
            foreach (char c in v)
            {
                if (c == Bs) { sb.Append(Bs).Append(Bs); }
                else if (c == '\n') { sb.Append(Bs).Append('n'); }
                else if (c == '\r') { /* se descarta */ }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Unescape(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            StringBuilder sb = new StringBuilder(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i] == Bs && i + 1 < v.Length)
                {
                    char n = v[i + 1];
                    if (n == 'n') { sb.Append('\n'); i++; continue; }
                    if (n == Bs) { sb.Append(Bs); i++; continue; }
                }
                sb.Append(v[i]);
            }
            return sb.ToString();
        }

        public static string Get(Dictionary<string, string> d, string key, string fallback)
        {
            string v;
            if (d.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            return fallback;
        }

        public static bool GetBool(Dictionary<string, string> d, string key, bool fallback)
        {
            string v;
            if (d.TryGetValue(key, out v))
            {
                if (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return fallback;
        }

        public static int GetInt(Dictionary<string, string> d, string key, int fallback)
        {
            string v; int n;
            if (d.TryGetValue(key, out v) && int.TryParse(v, out n)) return n;
            return fallback;
        }
    }
}
