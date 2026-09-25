using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ObsfsAutoMount
{
    internal class MainForm : Form
    {
        // Endpoints publicos de OBS mas usados. El combo es editable: se acepta cualquier otro.
        private static readonly string[] Endpoints = new string[]
        {
            "obs.la-south-2.myhuaweicloud.com",      // Santiago (LA-Santiago)
            "obs.sa-brazil-1.myhuaweicloud.com",     // Sao Paulo (LA-Sao Paulo1)
            "obs.la-north-2.myhuaweicloud.com",      // Mexico City2
            "obs.na-mexico-1.myhuaweicloud.com",     // Mexico City1
            "obs.ap-southeast-1.myhuaweicloud.com",  // Hong Kong
            "obs.ap-southeast-2.myhuaweicloud.com",  // Bangkok
            "obs.ap-southeast-3.myhuaweicloud.com",  // Singapore
            "obs.af-south-1.myhuaweicloud.com",      // Johannesburg
            "obs.me-east-1.myhuaweicloud.com",       // Riyadh
            "obs.tr-west-1.myhuaweicloud.com",       // Istanbul
            "obs.cn-north-4.myhuaweicloud.com",      // Beijing4
            "obs.cn-east-3.myhuaweicloud.com",       // Shanghai1
            "obs.cn-south-1.myhuaweicloud.com",      // Guangzhou
            "obs.eu-west-101.myhuaweicloud.eu"       // Dublin (Huawei Cloud EU)
        };

        private const string DepsHint =
            "rclone es el cliente de OBS; WinFsp lo muestra como unidad. Se instalan una sola vez.";

        private AppConfig cfg;
        private readonly bool startedForAutoMount;
        private bool busy;
        private bool reallyExit;
        private bool suppressAutoStartEvent;

        // --- controles
        private Label lblRcloneDot, lblRclone, lblWinFspDot, lblWinFsp, lblProgress;
        private Button btnDeps;
        private ProgressBar pb;

        private ComboBox cmbEndpoint, cmbBucket, cmbLetter, cmbCache;
        private TextBox txtAk, txtSk, txtPrefix, txtLabel, txtCacheSize, txtDirCache;
        private Button btnEye, btnTest, btnAdvanced;
        private Label lblTest;

        private CheckBox chkNetwork, chkReadOnly, chkAutoStart;
        private Button btnMount, btnUnmount, btnOpen, btnLog;
        private Label lblStatusDot, lblStatus;

        private NotifyIcon tray;
        private ToolStripMenuItem miMount, miUnmount, miOpen;
        private System.Windows.Forms.Timer statusTimer;

        public MainForm(bool autoMount)
        {
            startedForAutoMount = autoMount;
            AppPaths.EnsureDirs();
            cfg = AppConfig.Load();
            BuildUi();
            BuildTray();

            // Arranque silencioso: se abre minimizada para que no destelle antes de ocultarse.
            if (autoMount)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }
        }

        // =============================================================== construccion de la UI

        private void BuildUi()
        {
            Text = "OBSFS AutoMount";
            ClientSize = new Size(620, 756);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Surface;
            Font = Theme.Base;
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6f, 13f);
            try { Icon = Icon.ExtractAssociatedIcon(AppPaths.SelfExe); }
            catch { }

            // ---------- encabezado
            Panel header = new Panel();
            header.SetBounds(0, 0, 620, 62);
            header.BackColor = Theme.Red;
            Label t1 = new Label();
            t1.Text = "OBSFS AutoMount";
            t1.Font = Theme.Title;
            t1.ForeColor = Color.White;
            t1.SetBounds(18, 8, 400, 28);
            t1.BackColor = Color.Transparent;
            Label t2 = new Label();
            t2.Text = "Monta un bucket de Huawei Cloud OBS como unidad de Windows";
            t2.Font = Theme.SubTitle;
            t2.ForeColor = Color.FromArgb(0xFF, 0xD8, 0xDE);
            t2.SetBounds(20, 37, 560, 18);
            t2.BackColor = Color.Transparent;
            header.Controls.Add(t1);
            header.Controls.Add(t2);
            Controls.Add(header);

            // ---------- 1. requisitos
            GroupBox g1 = Theme.Group("  1 · Requisitos del sistema  ", 14, 74, 592, 116);
            lblRcloneDot = Theme.Lbl("●", 14, 25, 14);
            lblRclone = Theme.Lbl("rclone: verificando...", 32, 25, 390);
            lblWinFspDot = Theme.Lbl("●", 14, 47, 14);
            lblWinFsp = Theme.Lbl("WinFsp: verificando...", 32, 47, 390);
            btnDeps = Theme.Btn("Instalar / Reparar", 436, 26, 140, 30, false);
            btnDeps.Click += BtnDeps_Click;
            pb = new ProgressBar();
            pb.SetBounds(14, 74, 562, 10);
            pb.Style = ProgressBarStyle.Continuous;
            pb.Visible = false;
            lblProgress = Theme.Hint(DepsHint, 14, 88, 562);
            AddTo(g1, lblRcloneDot, lblRclone, lblWinFspDot, lblWinFsp, btnDeps, pb, lblProgress);
            Controls.Add(g1);

            // ---------- 2. credenciales
            GroupBox g2 = Theme.Group("  2 · Credenciales de Huawei Cloud OBS  ", 14, 198, 592, 250);
            Label l1 = Theme.Lbl("Endpoint", 14, 28, 110);
            cmbEndpoint = Theme.Cmb(128, 25, 448, true);
            cmbEndpoint.Items.AddRange(Endpoints);
            Label l2 = Theme.Lbl("Access Key (AK)", 14, 58, 110);
            txtAk = Theme.Txt(128, 55, 448);
            Label l3 = Theme.Lbl("Secret Key (SK)", 14, 88, 110);
            txtSk = Theme.Txt(128, 85, 382);
            txtSk.UseSystemPasswordChar = true;
            btnEye = Theme.Btn("Ver", 516, 84, 60, 24, false);
            btnEye.Font = Theme.Small;
            btnEye.Click += delegate
            {
                txtSk.UseSystemPasswordChar = !txtSk.UseSystemPasswordChar;
                btnEye.Text = txtSk.UseSystemPasswordChar ? "Ver" : "Ocultar";
            };
            btnTest = Theme.Btn("Probar conexión", 128, 116, 160, 30, false);
            btnTest.Click += BtnTest_Click;
            btnAdvanced = Theme.Btn("Red corporativa...", 300, 116, 170, 30, false);
            btnAdvanced.Click += BtnAdvanced_Click;
            lblTest = Theme.Hint("Probá la conexión para validar las claves y listar los buckets.",
                                 14, 152, 562);
            lblTest.Height = 30;
            Label l4 = Theme.Lbl("Bucket", 14, 188, 110);
            cmbBucket = Theme.Cmb(128, 185, 448, true);
            Label l5 = Theme.Lbl("Carpeta", 14, 218, 110);
            txtPrefix = Theme.Txt(128, 215, 200);
            Label l6 = Theme.Hint("Opcional: monta solo esa subcarpeta.", 336, 218, 240);
            AddTo(g2, l1, cmbEndpoint, l2, txtAk, l3, txtSk, btnEye, btnTest, btnAdvanced,
                      lblTest, l4, cmbBucket, l5, txtPrefix, l6);
            Controls.Add(g2);

            // ---------- 3. unidad
            GroupBox g3 = Theme.Group("  3 · Unidad en el Explorador  ", 14, 456, 592, 148);
            Label m1 = Theme.Lbl("Letra", 14, 28, 60);
            cmbLetter = Theme.Cmb(128, 25, 64, false);
            Label m2 = Theme.Lbl("Etiqueta", 212, 28, 60);
            txtLabel = Theme.Txt(278, 25, 298);
            Label m3 = Theme.Lbl("Caché escritura", 14, 58, 110);
            cmbCache = Theme.Cmb(128, 55, 100, false);
            cmbCache.Items.AddRange(new object[] { "writes", "full", "minimal", "off" });
            Label m4 = Theme.Lbl("Tamaño máx.", 240, 58, 84);
            txtCacheSize = Theme.Txt(328, 55, 70);
            Label m5 = Theme.Lbl("Dir-cache", 412, 58, 62);
            txtDirCache = Theme.Txt(478, 55, 98);
            chkNetwork = Theme.Chk("Montar como unidad de red", 128, 86, 220);
            chkReadOnly = Theme.Chk("Solo lectura", 360, 86, 150);
            Label m6 = Theme.Hint("La caché local se guarda en " + AppPaths.CacheDir, 14, 114, 562);
            AddTo(g3, m1, cmbLetter, m2, txtLabel, m3, cmbCache, m4, txtCacheSize, m5, txtDirCache,
                      chkNetwork, chkReadOnly, m6);
            Controls.Add(g3);

            // ---------- inicio automatico
            chkAutoStart = Theme.Chk("Montar al iniciar el equipo", 18, 614, 300);
            chkAutoStart.Font = Theme.Bold;
            chkAutoStart.CheckedChanged += ChkAutoStart_Changed;
            Label a1 = Theme.Hint(
                "Se monta sola al iniciar sesión en Windows, sin ventanas ni permisos de administrador.",
                36, 634, 570);
            Controls.Add(chkAutoStart);
            Controls.Add(a1);

            // ---------- acciones
            btnMount = Theme.Btn("Montar ahora", 18, 660, 158, 38, true);
            btnMount.Click += BtnMount_Click;
            btnUnmount = Theme.Btn("Desmontar", 184, 660, 126, 38, false);
            btnUnmount.Click += BtnUnmount_Click;
            btnOpen = Theme.Btn("Abrir unidad", 318, 660, 126, 38, false);
            btnOpen.Click += delegate { OpenDrive(); };
            btnLog = Theme.Btn("Ver registro", 452, 660, 152, 38, false);
            btnLog.Click += delegate { new LogForm().ShowDialog(this); };
            Controls.Add(btnMount);
            Controls.Add(btnUnmount);
            Controls.Add(btnOpen);
            Controls.Add(btnLog);

            // ---------- barra de estado
            Panel bar = new Panel();
            bar.SetBounds(0, 712, 620, 44);
            bar.BackColor = Theme.Bar;
            lblStatusDot = Theme.Lbl("●", 18, 13, 14);
            lblStatusDot.BackColor = Color.Transparent;
            lblStatus = Theme.Lbl("Iniciando...", 36, 13, 560);
            lblStatus.BackColor = Color.Transparent;
            bar.Controls.Add(lblStatusDot);
            bar.Controls.Add(lblStatus);
            Controls.Add(bar);

            Load += MainForm_Load;
            FormClosing += MainForm_Closing;

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 3000;
            statusTimer.Tick += delegate { RefreshMountStatus(); };
        }

        private void AddTo(GroupBox g, params Control[] controls)
        {
            foreach (Control c in controls)
            {
                if (c.Font == g.Font) c.Font = Theme.Base;
                g.Controls.Add(c);
            }
        }

        private void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem miOpenWin = new ToolStripMenuItem("Abrir OBSFS AutoMount");
            miOpenWin.Font = new Font(menu.Font, FontStyle.Bold);
            miOpenWin.Click += delegate { ShowFromTray(); };
            miMount = new ToolStripMenuItem("Montar");
            miMount.Click += BtnMount_Click;
            miUnmount = new ToolStripMenuItem("Desmontar");
            miUnmount.Click += BtnUnmount_Click;
            miOpen = new ToolStripMenuItem("Abrir unidad en el Explorador");
            miOpen.Click += delegate { OpenDrive(); };
            ToolStripMenuItem miExit = new ToolStripMenuItem("Salir (deja la unidad montada)");
            miExit.Click += delegate { reallyExit = true; Application.Exit(); };

            menu.Items.Add(miOpenWin);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miMount);
            menu.Items.Add(miUnmount);
            menu.Items.Add(miOpen);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            tray = new NotifyIcon();
            tray.Text = "OBSFS AutoMount";
            try { tray.Icon = Icon.ExtractAssociatedIcon(AppPaths.SelfExe); }
            catch { tray.Icon = SystemIcons.Application; }
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowFromTray(); };
            tray.Visible = true;
        }

        // =============================================================== ciclo de vida

        private async void MainForm_Load(object sender, EventArgs e)
        {
            LoadConfigIntoUi();
            statusTimer.Start();
            RefreshMountStatus();

            if (startedForAutoMount) Hide();

            await RefreshDepsAsync();

            if (startedForAutoMount) await AutoMountAsync();
        }

        private void MainForm_Closing(object sender, FormClosingEventArgs e)
        {
            if (busy && e.CloseReason == CloseReason.UserClosing)
            {
                MessageBox.Show(this, "Hay una operación en curso. Esperá a que termine.",
                    "OBSFS AutoMount", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }

            // Si el inicio automatico esta activo, la app se queda en la bandeja del sistema.
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing && chkAutoStart.Checked)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                tray.BalloonTipTitle = "OBSFS AutoMount sigue activo";
                tray.BalloonTipText = "La unidad queda montada. Hacé clic derecho en este ícono para desmontarla.";
                tray.ShowBalloonTip(3000);
                return;
            }

            // Sin inicio automatico: al cerrar, la unidad quedaria montada sin icono en la bandeja.
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                MountState st = MountState.Load();
                if (st.IsAlive())
                {
                    DialogResult dr = MessageBox.Show(this,
                        "La unidad " + st.Drive + " sigue montada.\r\n\r\n" +
                        "Sí  ->  cerrar y dejarla montada (sigue disponible en el Explorador)\r\n" +
                        "No  ->  desmontarla y cerrar\r\n" +
                        "Cancelar  ->  volver a la aplicación",
                        "OBSFS AutoMount", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                    if (dr == DialogResult.Cancel) { e.Cancel = true; return; }
                    if (dr == DialogResult.No)
                    {
                        Cursor = Cursors.WaitCursor;
                        Mounter.Unmount(st, null);
                        Cursor = Cursors.Default;
                    }
                }
            }

            if (tray != null) tray.Visible = false;
        }

        private void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WmShowMe && Native.WmShowMe != 0)
            {
                ShowFromTray();
                return;
            }
            base.WndProc(ref m);
        }

        // =============================================================== configuracion <-> UI

        private void LoadConfigIntoUi()
        {
            cmbEndpoint.Text = cfg.Endpoint;
            txtAk.Text = cfg.AccessKey;
            txtSk.Text = cfg.SecretKey;
            if (!string.IsNullOrEmpty(cfg.Bucket))
            {
                cmbBucket.Items.Add(cfg.Bucket);
                cmbBucket.Text = cfg.Bucket;
            }
            txtPrefix.Text = cfg.Prefix;
            RefreshAdvancedButton();

            RefreshDriveLetters(cfg.DriveLetter);
            txtLabel.Text = cfg.VolumeLabel;
            cmbCache.SelectedItem = cfg.CacheMode;
            if (cmbCache.SelectedIndex < 0) cmbCache.SelectedItem = "writes";
            txtCacheSize.Text = cfg.CacheMaxSize;
            txtDirCache.Text = cfg.DirCacheTime;
            chkNetwork.Checked = cfg.NetworkMode;
            chkReadOnly.Checked = cfg.ReadOnly;

            suppressAutoStartEvent = true;
            chkAutoStart.Checked = Startup.IsEnabled();
            suppressAutoStartEvent = false;

            // Si el ejecutable fue movido, se corrige la entrada de inicio automatico.
            if (chkAutoStart.Checked && Startup.NeedsRefresh()) Startup.Enable();
        }

        private void RefreshDriveLetters(string preferred)
        {
            MountState st = MountState.Load();
            string mounted = st.IsAlive() ? st.Drive.TrimEnd(':') : null;

            List<string> free = Mounter.FreeDriveLetters();
            if (!string.IsNullOrEmpty(mounted) && !free.Contains(mounted)) free.Insert(0, mounted);
            if (!string.IsNullOrEmpty(preferred) && !free.Contains(preferred)) free.Insert(0, preferred);

            string current = cmbLetter.SelectedItem as string;
            cmbLetter.Items.Clear();
            cmbLetter.Items.AddRange(free.ToArray());

            string pick = preferred;
            if (string.IsNullOrEmpty(pick) || !free.Contains(pick)) pick = current;
            if (string.IsNullOrEmpty(pick) || !free.Contains(pick)) pick = free.Count > 0 ? free[0] : "O";
            cmbLetter.SelectedItem = pick;
            if (cmbLetter.SelectedIndex < 0 && cmbLetter.Items.Count > 0) cmbLetter.SelectedIndex = 0;
        }

        private void ReadUiIntoConfig()
        {
            cfg.Endpoint = cmbEndpoint.Text.Trim();
            cfg.AccessKey = txtAk.Text.Trim();
            cfg.SecretKey = txtSk.Text.Trim();
            cfg.Bucket = cmbBucket.Text.Trim();
            cfg.Prefix = txtPrefix.Text.Trim();
            cfg.DriveLetter = (cmbLetter.SelectedItem as string) ?? cfg.DriveLetter;
            cfg.VolumeLabel = txtLabel.Text.Trim();
            cfg.CacheMode = (cmbCache.SelectedItem as string) ?? "writes";
            cfg.CacheMaxSize = txtCacheSize.Text.Trim();
            cfg.DirCacheTime = txtDirCache.Text.Trim();
            cfg.NetworkMode = chkNetwork.Checked;
            cfg.ReadOnly = chkReadOnly.Checked;
            cfg.AutoStart = chkAutoStart.Checked;
        }

        // =============================================================== requisitos

        private async Task RefreshDepsAsync()
        {
            SetDot(lblRcloneDot, Theme.InkSoft);
            lblRclone.Text = "rclone: verificando...";
            SetDot(lblWinFspDot, Theme.InkSoft);
            lblWinFsp.Text = "WinFsp: verificando...";

            bool hasRclone = false, hasWinFsp = false;
            string ver = null;

            await Task.Run(delegate
            {
                hasRclone = Deps.RcloneInstalled();
                if (hasRclone) ver = Deps.RcloneVersion();
                hasWinFsp = Deps.WinFspInstalled();
            });

            SetDot(lblRcloneDot, hasRclone ? Theme.Ok : Theme.Bad);
            lblRclone.Text = hasRclone
                ? "rclone " + (ver ?? "instalado") + "  —  listo"
                : "rclone: no instalado (se descarga automáticamente, ~30 MB)";

            SetDot(lblWinFspDot, hasWinFsp ? Theme.Ok : Theme.Bad);
            lblWinFsp.Text = hasWinFsp
                ? "WinFsp instalado  —  listo"
                : "WinFsp: no instalado (necesario, pide permiso de administrador una vez)";

            btnDeps.Text = (hasRclone && hasWinFsp) ? "Reinstalar" : "Instalar faltantes";
            RefreshMountStatus();
        }

        private async void BtnDeps_Click(object sender, EventArgs e)
        {
            if (busy) return;
            bool force = Deps.RcloneInstalled() && Deps.WinFspInstalled();
            if (force)
            {
                DialogResult dr = MessageBox.Show(this,
                    "rclone y WinFsp ya están instalados.\r\n\r\n¿Reinstalar de todas formas?",
                    "OBSFS AutoMount", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
            }

            SetBusy(true, "Instalando requisitos...");
            pb.Visible = true;
            pb.Value = 0;

            string failure = null;
            bool cancelled = false;
            Action<string, int> prog = UiProgress();

            await Task.Run(delegate
            {
                try
                {
                    if (force || !Deps.RcloneInstalled()) Deps.InstallRclone(prog);
                    if (force || !Deps.WinFspInstalled())
                    {
                        if (!Deps.InstallWinFsp(prog)) cancelled = true;
                    }
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                    AppPaths.Log("InstallDeps: " + ex);
                }
            });

            pb.Visible = false;
            lblProgress.Text = DepsHint;
            SetBusy(false, null);
            await RefreshDepsAsync();

            if (failure != null)
                Alert("No se pudo completar la instalación.\r\n\r\n" + failure, MessageBoxIcon.Error);
            else if (cancelled)
                Alert("WinFsp no se instaló porque se rechazó el aviso de Windows.\r\n\r\n" +
                      "Sin WinFsp no es posible montar la unidad.", MessageBoxIcon.Warning);
        }

        // =============================================================== conexion

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            if (busy) return;
            ReadUiIntoConfig();

            if (string.IsNullOrEmpty(cfg.NormalizedEndpoint()) ||
                string.IsNullOrEmpty(cfg.AccessKey) || string.IsNullOrEmpty(cfg.SecretKey))
            {
                SetTestResult("Completá endpoint, AK y SK.", Theme.Warn);
                return;
            }
            if (!Deps.RcloneInstalled())
            {
                SetTestResult("Falta instalar rclone (paso 1).", Theme.Bad);
                return;
            }

            SetBusy(true, "Probando conexión con OBS...");
            SetTestResult("Conectando...", Theme.InkSoft);

            List<string> buckets = null;
            string error = null;
            bool ok = false;
            AppConfig snapshot = cfg;

            await Task.Run(delegate { ok = Mounter.TestConnection(snapshot, out buckets, out error); });

            SetBusy(false, null);

            if (ok)
            {
                string keep = cmbBucket.Text.Trim();
                cmbBucket.Items.Clear();
                if (buckets != null) cmbBucket.Items.AddRange(buckets.ToArray());
                if (!string.IsNullOrEmpty(keep) && cmbBucket.Items.Contains(keep)) cmbBucket.Text = keep;
                else if (cmbBucket.Items.Count == 1) cmbBucket.SelectedIndex = 0;
                else cmbBucket.Text = keep;

                int n = buckets == null ? 0 : buckets.Count;
                SetTestResult("Conexión correcta. " + n + " bucket(s) accesible(s).", Theme.Ok);
                cfg.Save();
            }
            else
            {
                SetTestResult("Falló la conexión.", Theme.Bad);
                Alert("No se pudo conectar a OBS.\r\n\r\n" + error +
                      "\r\n\r\n" + Mounter.Diagnose(error), MessageBoxIcon.Error);
            }
        }

        private void BtnAdvanced_Click(object sender, EventArgs e)
        {
            if (busy) return;
            ReadUiIntoConfig();
            using (AdvancedForm f = new AdvancedForm(cfg))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    cfg.Save();
                    RefreshAdvancedButton();
                }
            }
        }

        /// <summary>Marca el boton cuando hay proxy o certificado propio configurado.</summary>
        private void RefreshAdvancedButton()
        {
            bool on = !string.IsNullOrEmpty((cfg.HttpsProxy ?? "").Trim())
                   || !string.IsNullOrEmpty((cfg.CaCertPath ?? "").Trim())
                   || cfg.NoCheckCert;
            btnAdvanced.Text = on ? "Red corporativa ✓" : "Red corporativa...";
        }

        private void SetTestResult(string text, Color color)
        {
            lblTest.Text = text;
            lblTest.ForeColor = color;
        }

        // =============================================================== montar / desmontar

        private async void BtnMount_Click(object sender, EventArgs e)
        {
            if (busy) return;
            ReadUiIntoConfig();

            string why = cfg.Validate();
            if (why != null) { Alert(why, MessageBoxIcon.Warning); return; }
            if (!Deps.RcloneInstalled() || !Deps.WinFspInstalled())
            {
                Alert("Faltan requisitos. Usá el botón \"Instalar faltantes\" del paso 1.", MessageBoxIcon.Warning);
                return;
            }

            cfg.Save();
            SetBusy(true, "Montando " + cfg.MountPoint() + " ...");

            string error = null;
            bool ok = false;
            AppConfig snapshot = cfg;
            Action<string> prog = UiStatus();

            await Task.Run(delegate { ok = Mounter.Mount(snapshot, prog, out error); });

            SetBusy(false, null);
            RefreshDriveLetters(cfg.DriveLetter);
            RefreshMountStatus();

            if (ok)
            {
                if (!startedForAutoMount || Visible) OpenDrive();
                tray.BalloonTipTitle = "Unidad montada";
                tray.BalloonTipText = cfg.MountPoint() + "  ->  " + cfg.Bucket;
                tray.ShowBalloonTip(2500);
            }
            else
            {
                Alert("No se pudo montar la unidad.\r\n\r\n" + error, MessageBoxIcon.Error);
            }
        }

        private async void BtnUnmount_Click(object sender, EventArgs e)
        {
            if (busy) return;
            MountState st = MountState.Load();
            if (!st.IsAlive())
            {
                int killed = Mounter.KillOrphanRclone();
                MountState.Clear();
                RefreshMountStatus();
                if (killed == 0) Alert("No hay ninguna unidad montada por esta aplicación.", MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Consultando subidas pendientes...");
            int pending = -1;
            await Task.Run(delegate { pending = Mounter.PendingUploads(st); });

            if (pending > 0)
            {
                SetBusy(false, null);
                DialogResult dr = MessageBox.Show(this,
                    "Todavía hay " + pending + " archivo(s) subiendo a OBS.\r\n\r\n" +
                    "Si desmontás ahora la subida se interrumpe (se reanuda en el próximo montaje).\r\n\r\n" +
                    "¿Desmontar igual?",
                    "OBSFS AutoMount", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) { RefreshMountStatus(); return; }
                SetBusy(true, "Desmontando...");
            }

            SetStatus("Desmontando...", Theme.Warn);
            Action<string> prog = UiStatus();
            await Task.Run(delegate { Mounter.Unmount(st, prog); });

            SetBusy(false, null);
            RefreshDriveLetters(cfg.DriveLetter);
            RefreshMountStatus();
        }

        private async Task AutoMountAsync()
        {
            if (!Deps.RcloneInstalled() || !Deps.WinFspInstalled())
            {
                tray.BalloonTipTitle = "OBSFS AutoMount";
                tray.BalloonTipText = "Faltan requisitos (rclone / WinFsp). Abrí la aplicación para instalarlos.";
                tray.ShowBalloonTip(6000);
                ShowFromTray();
                return;
            }
            if (cfg.Validate() != null)
            {
                ShowFromTray();
                return;
            }

            MountState existing = MountState.Load();
            if (existing.IsAlive() && existing.DriveVisible()) { RefreshMountStatus(); return; }

            // Al iniciar sesion la red puede no estar lista todavia: se reintenta.
            int delay = cfg.StartupDelay > 0 ? cfg.StartupDelay : 8;
            SetStatus("Esperando la red antes de montar...", Theme.Warn);
            await Task.Delay(delay * 1000);

            string error = null;
            bool ok = false;
            AppConfig snapshot = cfg;

            for (int attempt = 1; attempt <= 3 && !ok; attempt++)
            {
                SetStatus("Montando (intento " + attempt + " de 3)...", Theme.Warn);
                await Task.Run(delegate { ok = Mounter.Mount(snapshot, null, out error); });
                if (!ok && attempt < 3) await Task.Delay(10000);
            }

            RefreshMountStatus();

            if (ok)
            {
                tray.BalloonTipTitle = "Unidad montada";
                tray.BalloonTipText = cfg.MountPoint() + "  ->  " + cfg.Bucket;
                tray.ShowBalloonTip(2500);
            }
            else
            {
                AppPaths.Log("AutoMount fallido: " + error);
                tray.BalloonTipTitle = "No se pudo montar la unidad";
                tray.BalloonTipText = "Abrí OBSFS AutoMount para ver el detalle.";
                tray.ShowBalloonTip(6000);
            }
        }

        private void OpenDrive()
        {
            MountState st = MountState.Load();
            string root = st.IsAlive() && !string.IsNullOrEmpty(st.Drive)
                ? st.Drive + "\\"
                : cfg.DriveRoot();
            if (!Directory.Exists(root))
            {
                Alert("La unidad " + root + " no está montada.", MessageBoxIcon.Information);
                return;
            }
            try { Process.Start("explorer.exe", root); }
            catch (Exception ex) { Alert("No se pudo abrir el Explorador: " + ex.Message, MessageBoxIcon.Error); }
        }

        // =============================================================== inicio automatico

        private void ChkAutoStart_Changed(object sender, EventArgs e)
        {
            if (suppressAutoStartEvent) return;
            try
            {
                if (chkAutoStart.Checked)
                {
                    ReadUiIntoConfig();
                    string why = cfg.Validate();
                    if (why != null)
                    {
                        suppressAutoStartEvent = true;
                        chkAutoStart.Checked = false;
                        suppressAutoStartEvent = false;
                        Alert("Para activar el inicio automático hay que completar la configuración primero.\r\n\r\n" + why,
                            MessageBoxIcon.Warning);
                        return;
                    }
                    cfg.Save();
                    Startup.Enable();
                    SetStatus("Inicio automático activado.", Theme.Ok);
                }
                else
                {
                    Startup.Disable();
                    ReadUiIntoConfig();
                    cfg.Save();
                }
            }
            catch (Exception ex)
            {
                Alert("No se pudo cambiar el inicio automático: " + ex.Message, MessageBoxIcon.Error);
            }
            RefreshMountStatus();
        }

        // =============================================================== estado

        private void RefreshMountStatus()
        {
            MountState st = MountState.Load();
            bool alive = st.IsAlive();
            bool visible = alive && st.DriveVisible();

            if (visible)
            {
                SetStatus("Montado: " + st.Drive + "  ->  " + st.Bucket, Theme.Ok);
                Text = "OBSFS AutoMount  —  " + st.Drive + " montada";
                if (tray != null) tray.Text = "OBSFS AutoMount — " + st.Drive + " montada";
            }
            else if (alive)
            {
                SetStatus("rclone activo, la unidad todavía no responde...", Theme.Warn);
            }
            else
            {
                if (!string.IsNullOrEmpty(st.Drive)) MountState.Clear();
                SetStatus("Sin montar.", Theme.InkSoft);
                Text = "OBSFS AutoMount";
                if (tray != null) tray.Text = "OBSFS AutoMount";
            }

            btnUnmount.Enabled = !busy && alive;
            btnOpen.Enabled = !busy && visible;
            btnMount.Enabled = !busy && !visible;
            if (miMount != null) miMount.Enabled = btnMount.Enabled;
            if (miUnmount != null) miUnmount.Enabled = btnUnmount.Enabled;
            if (miOpen != null) miOpen.Enabled = btnOpen.Enabled;
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text = text;
            lblStatusDot.ForeColor = color;
        }

        private void SetDot(Label dot, Color c) { dot.ForeColor = c; }

        private void SetBusy(bool on, string status)
        {
            busy = on;
            Cursor = on ? Cursors.WaitCursor : Cursors.Default;
            btnDeps.Enabled = !on;
            btnTest.Enabled = !on;
            btnMount.Enabled = !on;
            btnUnmount.Enabled = !on;
            btnOpen.Enabled = !on;
            chkAutoStart.Enabled = !on;
            if (!string.IsNullOrEmpty(status)) SetStatus(status, Theme.Warn);
            if (!on) RefreshMountStatus();
        }

        private void SetProgress(string text, int pct)
        {
            lblProgress.Text = text;
            if (pct >= 0 && pct <= 100) pb.Value = pct;
        }

        /// <summary>Callback de progreso seguro para invocar desde un hilo de fondo.</summary>
        private Action<string, int> UiProgress()
        {
            return delegate(string text, int pct)
            {
                try
                {
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke((MethodInvoker)delegate { SetProgress(text, pct); });
                }
                catch { }
            };
        }

        private Action<string> UiStatus()
        {
            return delegate(string text)
            {
                try
                {
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke((MethodInvoker)delegate { SetStatus(text, Theme.Warn); });
                }
                catch { }
            };
        }

        private void Alert(string text, MessageBoxIcon icon)
        {
            if (!Visible) ShowFromTray();
            MessageBox.Show(this, text, "OBSFS AutoMount", MessageBoxButtons.OK, icon);
        }
    }
}
