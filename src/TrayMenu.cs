using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveWpm
{
    // The little window that opens when the tray icon is clicked once.
    sealed class TrayMenu : BufferedForm
    {
        sealed class Item
        {
            public string Label, Sub;
            public Func<bool> State;   // null for plain buttons
            public Action Run;
            public RectangleF Rect;
            public bool Danger;
        }

        const int W = 268;
        readonly DataStore store;
        readonly List<Item> items = new List<Item>();
        Bitmap logo;
        int hover = -1;
        float height;

        public TrayMenu(DataStore store, Action toggleFloating, Action toggleTracking, Action toggleOnlyTyping,
                        Action open, Action exit)
        {
            this.store = store;
            Text = Theme.AppName;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            logo = Theme.Logo((int)(26 * S), Theme.Accent);

            Add("Enabled", "Track typing speed", delegate { return store.Tracking; }, toggleTracking);
            Add("Floating window", "Speedometer in the corner", delegate { return store.Floating; }, toggleFloating);
            Add("Only when typing", "Show the dial only while typing", delegate { return store.OnlyWhenTyping; }, toggleOnlyTyping);
            Add("Open " + Theme.AppName, null, null, open);
            Add("Exit", null, null, exit).Danger = true;

            float y = 62;
            foreach (Item it in items)
            {
                if (it.State == null && items.IndexOf(it) == 3) y += 9; // gap + divider before buttons
                float h = it.State != null ? 46 : 36;
                it.Rect = new RectangleF(8, y, W - 16, h);
                y += h;
            }
            height = y + 8;
            SetLogicalSize(W, height);
        }

        Item Add(string label, string sub, Func<bool> state, Action run)
        {
            Item it = new Item();
            it.Label = label; it.Sub = sub; it.State = state; it.Run = run;
            items.Add(it);
            return it;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // tool window: not in Alt+Tab
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.RoundCorners(Handle);
        }

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr h);

        public void ShowNear(Point cursor)
        {
            Rectangle wa = Screen.FromPoint(cursor).WorkingArea;
            int gap = (int)(10 * S);
            int x = Math.Max(wa.Left + gap, Math.Min(cursor.X - Width / 2, wa.Right - Width - gap));
            int y = cursor.Y - Height - gap;
            if (y < wa.Top) y = cursor.Y + gap;
            y = Math.Max(wa.Top + gap, Math.Min(y, wa.Bottom - Height - gap));
            Location = new Point(x, y);
            hover = -1;
            Show();
            Activate();
            SetForegroundWindow(Handle);
            Invalidate();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            g.ScaleTransform(S, S);
            Theme.FillRound(g, Theme.Bg, new RectangleF(0, 0, W, height), 12);

            g.DrawImage(logo, new RectangleF(16, 16, 26, 26));
            Theme.DrawText(g, Theme.AppName, Theme.F(14, true), Theme.Text, 52, 14);
            Theme.DrawText(g, "Typer: " + store.TyperName, Theme.F(11, false), Theme.Muted, 52, 33);

            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                RectangleF r = it.Rect;
                if (i == 3)
                    using (Pen div = new Pen(Theme.Border, 1)) g.DrawLine(div, 16, r.Y - 5, W - 16, r.Y - 5);
                if (i == hover) Theme.FillRound(g, Theme.CardHover, r, 8);

                if (it.State != null)
                {
                    Theme.DrawText(g, it.Label, Theme.F(13, true), Theme.Text, r.X + 10, r.Y + 7);
                    Theme.DrawText(g, it.Sub, Theme.F(10.5f, false), Theme.Muted, r.X + 10, r.Y + 25);
                    DrawSwitch(g, new RectangleF(r.Right - 48, r.Y + (r.Height - 20) / 2, 38, 20), it.State());
                }
                else
                {
                    Color c = it.Danger ? Color.FromArgb(255, 110, 110) : Theme.Text;
                    Theme.DrawText(g, it.Label, Theme.F(13, false), c, r.X + 10, r.Y + 9);
                }
            }
        }

        public static void DrawSwitch(Graphics g, RectangleF r, bool on)
        {
            Theme.FillRound(g, on ? Theme.Accent : Theme.Track, r, r.Height / 2);
            float d = r.Height - 6;
            float x = on ? r.Right - d - 3 : r.X + 3;
            using (SolidBrush knob = new SolidBrush(on ? Color.FromArgb(10, 20, 28) : Theme.Muted))
                g.FillEllipse(knob, x, r.Y + 3, d, d);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].Rect.Contains(p.X / S, p.Y / S)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != -1) { hover = -1; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int h = HitTest(e.Location);
            if (h < 0) return;
            Item it = items[h];
            if (it.State == null) Hide();
            it.Run();
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Hide();
        }

        protected override void OnScaleChanged()
        {
            logo.Dispose();
            logo = Theme.Logo((int)(26 * S), Theme.Accent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) logo.Dispose();
            base.Dispose(disposing);
        }
    }
}
