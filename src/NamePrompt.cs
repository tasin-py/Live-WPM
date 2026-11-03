using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiveWpm
{
    // Asks for the "Typer Name" on first start (and from Settings later).
    sealed class NamePrompt : BufferedForm
    {
        const int W = 380, H = 214;
        readonly TextBox box = new TextBox();
        Bitmap logo;
        readonly bool firstRun;
        RectangleF btn = new RectangleF(24, 156, W - 48, 38);
        bool hoverBtn;

        NamePrompt(string current, bool firstRun)
        {
            this.firstRun = firstRun;
            Text = firstRun ? "Welcome to " + Theme.AppName : "Typer Name";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = firstRun;
            TopMost = firstRun;
            SetLogicalSize(W, H);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            logo = Theme.Logo((int)(30 * S), Theme.Accent);

            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.Card;
            box.ForeColor = Theme.Text;
            box.Font = Theme.F(15 * S, false);
            box.MaxLength = 32;
            box.Text = current;
            box.SetBounds((int)(38 * S), (int)(110 * S), (int)((W - 76) * S), (int)(24 * S));
            box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Submit(); }
                else if (e.KeyCode == Keys.Escape && !firstRun) { e.SuppressKeyPress = true; Close(); }
            };
            box.GotFocus += delegate { Invalidate(); };
            box.LostFocus += delegate { Invalidate(); };
            Controls.Add(box);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(Handle);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            box.Focus();
            box.SelectAll();
        }

        public string Result;

        void Submit()
        {
            string n = box.Text.Trim();
            if (n.Length == 0) { box.Focus(); return; }
            Result = n;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            g.ScaleTransform(S, S);
            g.DrawImage(logo, new RectangleF(24, 22, 30, 30));
            Theme.DrawText(g, firstRun ? "Hey there, Typer!" : "Change your name", Theme.F(19, true), Theme.Text, 64, 18);
            Theme.DrawText(g, firstRun ? "What should " + Theme.AppName + " call you?" : "This is how " + Theme.AppName + " greets you.",
                           Theme.F(12, false), Theme.Muted, 65, 43);
            Theme.DrawText(g, "TYPER NAME", Theme.F(10, true), Theme.Muted, 26, 80);
            Theme.FillRound(g, Theme.Card, new RectangleF(24, 98, W - 48, 44), 9);
            Theme.StrokeRound(g, box.Focused ? Theme.Accent : Theme.Border, new RectangleF(24, 98, W - 48, 44), 9, 1.2f);
            Theme.FillRound(g, hoverBtn ? Theme.Lerp(Theme.Accent, Color.White, 0.2) : Theme.Accent, btn, 9);
            Theme.DrawTextCenter(g, firstRun ? "Let's type" : "Save", Theme.F(13.5f, true), Color.FromArgb(8, 18, 26), btn.X + btn.Width / 2, btn.Y + 10);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = btn.Contains(e.X / S, e.Y / S);
            if (h != hoverBtn) { hoverBtn = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (btn.Contains(e.X / S, e.Y / S)) Submit();
            else box.Focus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // On first start a name is required; closing the window just uses "Typer".
            if (firstRun && Result == null) Result = box.Text.Trim().Length > 0 ? box.Text.Trim() : "Typer";
            base.OnFormClosing(e);
        }

        void LayoutBox()
        {
            box.Font = Theme.F(15 * S, false);
            box.SetBounds((int)(38 * S), (int)(110 * S), (int)((W - 76) * S), (int)(24 * S));
        }

        protected override void OnScaleChanged()
        {
            LayoutBox();
            logo.Dispose();
            logo = Theme.Logo((int)(30 * S), Theme.Accent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) logo.Dispose();
            base.Dispose(disposing);
        }

        // Returns the new name, or null if the Typer cancelled.
        public static string Ask(string current, bool firstRun, IWin32Window owner)
        {
            using (NamePrompt p = new NamePrompt(current, firstRun))
            {
                if (owner != null) p.ShowDialog(owner); else p.ShowDialog();
                return p.Result;
            }
        }
    }
}
