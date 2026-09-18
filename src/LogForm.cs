using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ObsfsAutoMount
{
    /// <summary>Visor del log de rclone, para diagnosticar montajes fallidos.</summary>
    internal class LogForm : Form
    {
        private const int TailLines = 400;
        private TextBox box;

        public LogForm()
        {
            Text = "Registro de OBSFS AutoMount";
            ClientSize = new Size(880, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            Font = Theme.Base;
            MinimizeBox = false;
            try { Icon = Icon.ExtractAssociatedIcon(AppPaths.SelfExe); }
            catch { }

            box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;
            box.Font = Theme.Mono;
            box.BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
            box.ForeColor = Color.FromArgb(0xE8, 0xE8, 0xE8);
            box.BorderStyle = BorderStyle.None;
            box.SetBounds(12, 12, 856, 452);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(box);

            Button refresh = Theme.Btn("Actualizar", 12, 476, 110, 32, false);
            refresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            refresh.Click += delegate { LoadLog(); };

            Button folder = Theme.Btn("Abrir carpeta de datos", 130, 476, 170, 32, false);
            folder.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            folder.Click += delegate
            {
                try { Process.Start("explorer.exe", AppPaths.Root); }
                catch { }
            };

            Button copy = Theme.Btn("Copiar todo", 308, 476, 110, 32, false);
            copy.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            copy.Click += delegate
            {
                try { if (box.TextLength > 0) Clipboard.SetText(box.Text); }
                catch { }
            };

            Button close = Theme.Btn("Cerrar", 758, 476, 110, 32, false);
            close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            close.Click += delegate { Close(); };

            Controls.Add(refresh);
            Controls.Add(folder);
            Controls.Add(copy);
            Controls.Add(close);

            LoadLog();
        }

        private void LoadLog()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== " + AppPaths.MountLog + " (ultimas " + TailLines + " lineas) ===");
            sb.AppendLine(Tail(AppPaths.MountLog));
            sb.AppendLine();
            sb.AppendLine("=== " + AppPaths.AppLog + " ===");
            sb.AppendLine(Tail(AppPaths.AppLog));

            box.Text = sb.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
        }

        private static string Tail(string path)
        {
            try
            {
                if (!File.Exists(path)) return "(sin registros todavia)";
                string[] all = Mounter.ReadAllLinesShared(path);
                int start = Math.Max(0, all.Length - TailLines);
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < all.Length; i++) sb.AppendLine(all[i]);
                return sb.Length == 0 ? "(vacio)" : sb.ToString();
            }
            catch (Exception ex) { return "(no se pudo leer: " + ex.Message + ")"; }
        }
    }
}
