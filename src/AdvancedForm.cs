using System;
using System.Drawing;
using System.Windows.Forms;

namespace ObsfsAutoMount
{
    /// <summary>
    /// Ajustes de red que solo hacen falta en maquinas de empresa: proxy de salida y certificado
    /// raiz propio. Sin esto, un proxy que inspecciona TLS hace fallar todas las llamadas a OBS
    /// aunque la unidad se monte igual.
    /// </summary>
    internal class AdvancedForm : Form
    {
        private readonly AppConfig cfg;
        private TextBox txtProxy, txtCa;
        private CheckBox chkNoVerify;

        public AdvancedForm(AppConfig config)
        {
            cfg = config;
            BuildUi();
            txtProxy.Text = cfg.HttpsProxy ?? "";
            txtCa.Text = cfg.CaCertPath ?? "";
            chkNoVerify.Checked = cfg.NoCheckCert;
        }

        private void BuildUi()
        {
            Text = "Red corporativa";
            ClientSize = new Size(538, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            Font = Theme.Base;
            ShowInTaskbar = false;

            Label intro = Theme.Hint(
                "Solo hace falta en redes de empresa. Si la conexion ya funciona, dejar todo vacio.",
                14, 14, 510);

            Label l1 = Theme.Lbl("Proxy HTTPS", 14, 52, 112);
            txtProxy = Theme.Txt(132, 49, 392);
            Label h1 = Theme.Hint("Ej.: http://proxy.empresa.local:8080", 132, 74, 392);

            Label l2 = Theme.Lbl("Certificado raiz", 14, 108, 112);
            txtCa = Theme.Txt(132, 105, 298);
            Button btnBrowse = Theme.Btn("Examinar...", 436, 104, 88, 24, false);
            btnBrowse.Font = Theme.Small;
            btnBrowse.Click += BtnBrowse_Click;
            Label h2 = Theme.Hint(
                "Archivo .pem o .crt de la CA de la empresa. Necesario cuando el proxy inspecciona " +
                "el trafico TLS y reemplaza el certificado del servidor.", 132, 130, 392);
            h2.Height = 32;

            chkNoVerify = Theme.Chk("No verificar el certificado del servidor", 14, 180, 400);
            Label h3 = Theme.Hint(
                "Ultimo recurso. Deja la conexion expuesta a intercepcion: usar solo para confirmar " +
                "que el problema es el certificado, y despues cargar la CA.", 32, 202, 492);
            h3.Height = 32;
            h3.ForeColor = Theme.Bad;

            Button ok = Theme.Btn("Aceptar", 306, 252, 108, 32, true);
            ok.Click += BtnOk_Click;
            Button cancel = Theme.Btn("Cancelar", 422, 252, 102, 32, false);
            cancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] {
                intro, l1, txtProxy, h1, l2, txtCa, btnBrowse, h2, chkNoVerify, h3, ok, cancel });

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Certificado raiz de la empresa";
                d.Filter = "Certificados (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|Todos los archivos (*.*)|*.*";
                d.CheckFileExists = true;
                if (d.ShowDialog(this) == DialogResult.OK) txtCa.Text = d.FileName;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string ca = txtCa.Text.Trim();
            if (ca.Length > 0 && !System.IO.File.Exists(ca))
            {
                MessageBox.Show(this, "No se encuentra el archivo de certificado indicado.",
                    "Red corporativa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            cfg.HttpsProxy = txtProxy.Text.Trim();
            cfg.CaCertPath = ca;
            cfg.NoCheckCert = chkNoVerify.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
