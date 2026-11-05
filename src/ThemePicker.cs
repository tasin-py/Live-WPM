using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LiveWpm
{
    // Pop-up from Settings: every theme as a small preview. Clicking one applies it at once
    // (this window restyles too), so the Typer can try them before closing. The Typer's own
    // themes are added here from a file, and can be removed again.
    sealed class ThemePicker : BufferedForm
    {
        const int Cols = 4, TileW = 128, TileH = 88, Gap = 14, NameH = 28, ButtonH = 32, RemoveSize = 22;
        const int W = 24 * 2 + Cols * TileW + (Cols - 1) * Gap;
        static readonly RectangleF CloseRect = new RectangleF(W - 40, 14, 28, 28);

        readonly Action<string> choose;
        string current;
        int hover = -1, hoverButton = -1;  // hoverButton: 0 upload, 1 starter file
        bool hoverClose, hoverRemove;

        ThemePicker(string current, Action<string> choose)
        {
            this.current = current;
            this.choose = choose;
            Text = "Theme";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            SetLogicalSize(W, H);
        }

        public static void ShowFor(IWin32Window owner, string current, Action<string> choose)
        {
            using (ThemePicker p = new ThemePicker(current, choose)) p.ShowDialog(owner);
            Theme.TrimMemory();
        }

        static int Rows { get { return (Theme.ThemeCount + Cols - 1) / Cols; } }
        static int H { get { return 82 + Rows * (TileH + NameH) + (Rows - 1) * 8 + 16 + ButtonH + 20; } }
        static RectangleF ButtonRect(int i) { return new RectangleF(24 + i * 150, H - 20 - ButtonH, 140, ButtonH); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.RoundCorners(Handle);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close();
        }

        RectangleF Tile(int i)
        {
            int c = i % Cols, r = i / Cols;
            return new RectangleF(24 + c * (TileW + Gap), 82 + r * (TileH + NameH + 8), TileW, TileH);
        }

        RectangleF RemoveRect(int i)
        {
            RectangleF t = Tile(i);
            return new RectangleF(t.Right - RemoveSize - 4, t.Y + 4, RemoveSize, RemoveSize);
        }

        static bool IsCustom(int i) { return i >= Theme.Palettes.Length; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            g.ScaleTransform(S, S);
            Theme.PaintBackground(g, W, H);

            Theme.DrawText(g, "Themes", Theme.F(18, true), Theme.Text, 26, 20);
            Theme.DrawText(g, "Pick one to apply it. Live WPM remembers your choice.", Theme.F(11.5f, false), Theme.Muted, 27, 48);
            DrawClose(g);

            for (int i = 0; i < Theme.ThemeCount; i++)
            {
                Palette p = Theme.ThemeAt(i);
                RectangleF t = Tile(i);
                bool on = p.Id == current;
                DrawPreview(g, p, t);
                if (on) Theme.StrokeRound(g, Theme.Accent, new RectangleF(t.X - 3, t.Y - 3, t.Width + 6, t.Height + 6), 13, 2);
                else if (i == hover) Theme.StrokeRound(g, Theme.Muted, new RectangleF(t.X - 3, t.Y - 3, t.Width + 6, t.Height + 6), 13, 1.2f);
                Font nf = Theme.F(12, on);
                Theme.DrawTextCenter(g, p.Name, nf, on ? Theme.Text : (i == hover ? Theme.Text : Theme.Muted), t.X + t.Width / 2, t.Bottom + 8);
                if (i == hover && IsCustom(i)) DrawRemove(g, RemoveRect(i));
            }

            DrawButton(g, 0, "Upload theme");
            DrawButton(g, 1, "Get starter file");
        }

        void DrawButton(Graphics g, int i, string label)
        {
            RectangleF r = ButtonRect(i);
            Theme.Surface(g, hoverButton == i ? Theme.CardHover : Theme.Card, r, 9);
            Font f = Theme.F(11.5f, true);
            Theme.DrawTextCenter(g, label, f, Theme.Text, r.X + r.Width / 2, r.Y + 8);
        }

        void DrawRemove(Graphics g, RectangleF r)
        {
            Theme.FillRound(g, hoverRemove ? Theme.Danger : Color.FromArgb(190, 0, 0, 0), r, 7);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using (Pen p = new Pen(Color.White, 1.5f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 3.5f, cy - 3.5f, cx + 3.5f, cy + 3.5f);
                g.DrawLine(p, cx + 3.5f, cy - 3.5f, cx - 3.5f, cy + 3.5f);
            }
        }

        // A tiny version of the floating speedometer in that theme's own colours.
        static void DrawPreview(Graphics g, Palette p, RectangleF t)
        {
            GraphicsState st = g.Save();
            using (GraphicsPath clip = Theme.RoundExact(t, 10))
            {
                g.SetClip(clip);
                Theme.PaintBackground(g, p, t);
                RectangleF card = new RectangleF(t.X + 12, t.Y + 12, t.Width - 24, t.Height - 24);
                using (GraphicsPath cp = Theme.RoundExact(card, 8 * p.Radius))
                {
                    using (SolidBrush cb = new SolidBrush(p.Card)) g.FillPath(cb, cp);
                    Color edge = p.Glass ? Color.FromArgb(200, 255, 255, 255) : p.Outline;
                    if (edge.A > 0) using (Pen ep = new Pen(edge, 1)) g.DrawPath(ep, cp);
                }

                // dial: track, accent arc, needle tip
                float cx = card.X + card.Width / 2, cy = card.Y + card.Height / 2 + 6, r = 20;
                RectangleF arc = new RectangleF(cx - r, cy - r, r * 2, r * 2);
                using (Pen track = new Pen(p.Track, 4)) { track.StartCap = track.EndCap = LineCap.Round; g.DrawArc(track, arc, 135, 270); }
                using (Pen val = new Pen(p.DialColor, 4)) { val.StartCap = val.EndCap = LineCap.Round; g.DrawArc(val, arc, 135, 170); }
                double a = (135 + 170) * Math.PI / 180;
                using (SolidBrush tip = new SolidBrush(p.Text))
                    g.FillEllipse(tip, cx + (float)Math.Cos(a) * r - 3, cy + (float)Math.Sin(a) * r - 3, 6, 6);
                // two "text" bars in the theme's text colours
                using (SolidBrush tb = new SolidBrush(p.Text)) g.FillRectangle(tb, card.X + 8, card.Y + 7, 16, 4);
                using (SolidBrush mb = new SolidBrush(p.Accent)) g.FillRectangle(mb, card.X + 27, card.Y + 7, 12, 4);
                if (p.Cute) Theme.Heart(g, p.Accent, card.Right - 12, card.Y + 9, 7);
            }
            g.Restore(st);
            if (p.Light) Theme.StrokeRound(g, Color.FromArgb(28, 0, 0, 0), new RectangleF(t.X + 0.5f, t.Y + 0.5f, t.Width - 1, t.Height - 1), 10, 1);
        }

        void DrawClose(Graphics g)
        {
            if (hoverClose) Theme.FillRound(g, Theme.Danger, CloseRect, 7);
            float cx = CloseRect.X + CloseRect.Width / 2, cy = CloseRect.Y + CloseRect.Height / 2;
            using (Pen p = new Pen(hoverClose ? Color.White : Theme.Muted, 1.6f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                g.DrawLine(p, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
            }
        }

        int HitTile(float x, float y)
        {
            for (int i = 0; i < Theme.ThemeCount; i++)
            {
                RectangleF t = Tile(i);
                if (new RectangleF(t.X, t.Y, t.Width, t.Height + NameH).Contains(x, y)) return i;
            }
            return -1;
        }

        static int HitButton(float x, float y)
        {
            for (int i = 0; i < 2; i++) if (ButtonRect(i).Contains(x, y)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float x = e.X / S, y = e.Y / S;
            bool hc = CloseRect.Contains(x, y);
            int ht = HitTile(x, y), hb = HitButton(x, y);
            bool hr = ht >= 0 && IsCustom(ht) && RemoveRect(ht).Contains(x, y);
            if (hc != hoverClose || ht != hover || hb != hoverButton || hr != hoverRemove)
            {
                hoverClose = hc; hover = ht; hoverButton = hb; hoverRemove = hr;
                Cursor = hc || ht >= 0 || hb >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverClose || hover >= 0 || hoverButton >= 0)
            {
                hoverClose = hoverRemove = false; hover = hoverButton = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            float x = e.X / S, y = e.Y / S;
            if (CloseRect.Contains(x, y)) { Close(); return; }
            int b = HitButton(x, y);
            if (b == 0) { Upload(); return; }
            if (b == 1) { SaveStarter(); return; }
            int t = HitTile(x, y);
            if (t < 0) { Theme.BeginDrag(Handle); return; }
            if (IsCustom(t) && RemoveRect(t).Contains(x, y)) { RemoveTheme(Theme.ThemeAt(t)); return; }
            current = Theme.ThemeAt(t).Id;
            choose(current);
            Invalidate();
        }

        void Upload()
        {
            string path;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Upload a Live WPM theme";
                d.Filter = "Live WPM theme (*" + ThemeFiles.Extension + ")|*" + ThemeFiles.Extension + "|Text file (*.txt)|*.txt";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                path = d.FileName;
            }
            string error;
            Palette p = ThemeFiles.Import(path, delegate(string name)
            {
                return Ask("You already have a theme called \"" + name + "\". Replace it?");
            }, out error);
            if (error != null)
            {
                MessageBox.Show(this, error, Theme.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (p == null) return;
            ThemesChanged(p.Id);
        }

        void SaveStarter()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "Save a starter theme";
                d.FileName = "My Theme" + ThemeFiles.Extension;
                d.Filter = "Live WPM theme (*" + ThemeFiles.Extension + ")|*" + ThemeFiles.Extension;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(d.FileName, ThemeFiles.Starter);
                    MessageBox.Show(this, "Saved. Open it in Notepad, change the colours and name, save it, then choose Upload theme.",
                                    Theme.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "The file couldn't be saved:\n" + ex.Message, Theme.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        void RemoveTheme(Palette p)
        {
            if (!Ask("Remove \"" + p.Name + "\" from Live WPM? A copy you saved elsewhere isn't affected.")) return;
            try { ThemeFiles.Remove(p); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The theme couldn't be removed:\n" + ex.Message, Theme.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ThemesChanged(current == p.Id ? Theme.DefaultTheme : current);
        }

        // The list of themes changed: fit the window to it and apply the theme now in use.
        void ThemesChanged(string id)
        {
            hover = hoverButton = -1;
            hoverRemove = false;
            current = id;
            SetLogicalSize(W, H);
            choose(id);
            Invalidate();
        }

        bool Ask(string question)
        {
            return MessageBox.Show(this, question, Theme.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                                   MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
    }
}
