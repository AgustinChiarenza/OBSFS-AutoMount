using System;
using Microsoft.Win32;

namespace ObsfsAutoMount
{
    /// <summary>
    /// Inicio automatico por la clave Run del usuario actual (HKCU). No requiere privilegios de
    /// administrador y se limpia solo desde el Administrador de tareas > Inicio.
    /// </summary>
    internal static class Startup
    {
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ValueName = "OBSFS-AutoMount";
        public const string AutoStartArg = "--autostart";

        private static string CommandLine()
        {
            return "\"" + AppPaths.SelfExe + "\" " + AutoStartArg;
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(ValueName);
                    return v != null && !string.IsNullOrEmpty(v.ToString());
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log("Startup.IsEnabled: " + ex.Message);
                return false;
            }
        }

        /// <summary>true si la entrada existe pero apunta a otra ruta (el exe fue movido).</summary>
        public static bool NeedsRefresh()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(ValueName);
                    if (v == null) return false;
                    return !string.Equals(v.ToString(), CommandLine(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static void Enable()
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                k.SetValue(ValueName, CommandLine(), RegistryValueKind.String);
            }
            AppPaths.Log("Inicio automatico habilitado: " + CommandLine());
        }

        public static void Disable()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k != null && k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                }
                AppPaths.Log("Inicio automatico deshabilitado.");
            }
            catch (Exception ex) { AppPaths.Log("Startup.Disable: " + ex.Message); }
        }

        public static void Apply(bool enabled)
        {
            if (enabled) Enable();
            else Disable();
        }
    }
}
