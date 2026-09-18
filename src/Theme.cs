using System;
using System.Drawing;
using System.Windows.Forms;

namespace ObsfsAutoMount
{
    /// <summary>Paleta y fabricas de controles, para mantener la UI consistente.</summary>
    internal static class Theme
    {
        public static readonly Color Red      = Color.FromArgb(0xCF, 0x0A, 0x2C);
        public static readonly Color RedDark  = Color.FromArgb(0x9E, 0x08, 0x21);
        public static readonly Color Ink      = Color.FromArgb(0x22, 0x22, 0x22);
        public static readonly Color InkSoft  = Color.FromArgb(0x6B, 0x6B, 0x6B);
        public static readonly Color Surface  = Color.White;
        public static readonly Color Bar      = Color.FromArgb(0xF4, 0xF4, 0xF6);
        public static readonly Color Line     = Color.FromArgb(0xD5, 0xD5, 0xD8);
        public static readonly Color Ok       = Color.FromArgb(0x1B, 0x8A, 0x3E);
        public static readonly Color Warn     = Color.FromArgb(0xC8, 0x77, 0x00);
        public static readonly Color Bad      = Color.FromArgb(0xC0, 0x1C, 0x28);

        // Fuentes compartidas: se asignan explicitamente a cada control para que no hereden
        // la negrita del GroupBox contenedor.
        public static readonly Font Base     = new Font("Segoe UI", 9f);
        public static readonly Font Bold     = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Small    = new Font("Segoe UI", 8.25f);
        public static readonly Font Title    = new Font("Segoe UI Semibold", 15f);
        public static readonly Font SubTitle = new Font("Segoe UI", 8.75f);
        public static readonly Font Mono     = new Font("Consolas", 8.5f);

        public static Label Lbl(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, w, 18);
            l.Font = Base;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Ink;
            return l;
        }

        public static Label Hint(string text, int x, int y, int w)
        {
            Label l = Lbl(text, x, y, w);
            l.Font = Small;
            l.ForeColor = InkSoft;
            return l;
        }

        public static GroupBox Group(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.SetBounds(x, y, w, h);
            g.ForeColor = RedDark;
            g.Font = Bold;
            return g;
        }

        public static Button Btn(string text, int x, int y, int w, int h, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            if (primary)
            {
                b.BackColor = Red;
                b.ForeColor = Color.White;
                b.Font = Bold;
                b.FlatAppearance.BorderColor = RedDark;
                b.FlatAppearance.MouseOverBackColor = RedDark;
                b.FlatAppearance.MouseDownBackColor = RedDark;
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Ink;
                b.Font = Base;
                b.FlatAppearance.BorderColor = Line;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF0, 0xF0, 0xF2);
            }
            return b;
        }

        public static TextBox Txt(int x, int y, int w)
        {
            TextBox t = new TextBox();
            t.SetBounds(x, y, w, 22);
            t.Font = Base;
            t.BorderStyle = BorderStyle.FixedSingle;
            return t;
        }

        public static ComboBox Cmb(int x, int y, int w, bool editable)
        {
            ComboBox c = new ComboBox();
            c.SetBounds(x, y, w, 22);
            c.Font = Base;
            c.DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Standard;
            return c;
        }

        public static CheckBox Chk(string text, int x, int y, int w)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.SetBounds(x, y, w, 20);
            c.Font = Base;
            c.ForeColor = Ink;
            c.Cursor = Cursors.Hand;
            return c;
        }
    }
}
