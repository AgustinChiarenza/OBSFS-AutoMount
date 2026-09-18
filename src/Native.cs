using System;
using System.Runtime.InteropServices;

namespace ObsfsAutoMount
{
    /// <summary>Interop minimo: solo lo necesario para traer al frente la instancia ya abierta.</summary>
    internal static class Native
    {
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xFFFF);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Mensaje privado usado para pedirle a la instancia viva que se muestre.</summary>
        public static readonly int WmShowMe = RegisterWindowMessage("OBSFS_AutoMount_ShowWindow_v1");

        public static void BroadcastShowMe()
        {
            if (WmShowMe != 0) PostMessage(HwndBroadcast, WmShowMe, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
