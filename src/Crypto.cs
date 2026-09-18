using System;
using System.Security.Cryptography;
using System.Text;

namespace ObsfsAutoMount
{
    /// <summary>
    /// Cifrado de la Secret Key usando DPAPI con alcance de usuario: solo la cuenta de Windows
    /// que la guardo puede descifrarla, y solo en esta maquina.
    /// </summary>
    internal static class Crypto
    {
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("OBSFS-AutoMount/v1/huawei-obs-secret-key");

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                byte[] enc = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch (Exception ex)
            {
                AppPaths.Log("Crypto.Protect: " + ex.Message);
                return "";
            }
        }

        public static string Unprotect(string cipherB64)
        {
            if (string.IsNullOrEmpty(cipherB64)) return "";
            try
            {
                byte[] dec = ProtectedData.Unprotect(
                    Convert.FromBase64String(cipherB64), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch (Exception ex)
            {
                AppPaths.Log("Crypto.Unprotect: " + ex.Message);
                return "";
            }
        }
    }
}
