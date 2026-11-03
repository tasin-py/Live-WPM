using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LiveWpm
{
    // The app window: Home (six stat cards), History (one row per day) and Settings.
    // Everything is painted by hand to keep the app tiny; clickable areas are collected
    // as "hits" while painting.
    sealed class MainWindow : BufferedForm
    {
        const int W = 720, H = 600;
        static readonly RectangleF TodayGraph = new RectangleF(28, 120, W - 56, 160);
        const float ListTop = 136, RowH = 46, ListBottom = H - 18;

        readonly DataStore store;
        readonly Action startupChanged, dataChanged;
        Bitmap logo;
        readonly List<KeyValuePair<RectangleF, string>> hits = new List<KeyValuePair<RectangleF, string>>();
        int page;          // 0 home, 1 history, 2 settings
        string hover;
        float scroll;

        public MainWindow(DataStore store, Action startupChanged, Action dataChanged)
        {
            this.store = store;
            this.startupChanged = startupChanged;
            this.dataChanged = dataChanged;
            Text = Theme.AppName;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            SetLogicalSize(W, H);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            logo = Theme.Logo((int)(38 * S), Theme.Accent);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(Handle);
        }

        public void RefreshData() { Invalidate(); }

        // ---- hit testing ---------------------------------------------------------

        void Hit(RectangleF r, string id) { hits.Add(new KeyValuePair<RectangleF, string>(r, id)); }

        string HitAt(Point p)
        {
            float x = p.X / S, y = p.Y / S;
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Key.Contains(x, y)) return hits[i].Value;
            return null;
        }

        bool Hov(string id) { return hover == id; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (page == 0)
            {
                DayStat today;
                store.Days.TryGetValue(DateTime.Today, out today);
                int gh = SpeedGraph.HitTest(TodayGraph, today, e.X / S, e.Y / S);
                if (gh != graphHover) { graphHover = gh; Invalidate(); }
            }
            string h = HitAt(e.Location);
            if (h != hover)
            {
                hover = h;
                Cursor = h != null && !h.StartsWith("row:") ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null || graphHover >= 0) { hover = null; graphHover = -1; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (page != 1) return;
            float max = Math.Max(0, store.Days.Count * RowH - (ListBottom - ListTop));
            scroll = Math.Max(0, Math.Min(max, scroll - e.Delta / 120f * RowH));
            hover = null;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            string id = HitAt(e.Location);
            if (id == null) return;
            string name = store.TyperName;

            if (id == "tab:home") { page = 0; }
            else if (id == "tab:history") { page = 1; scroll = 0; }
            else if (id == "tab:settings") { page = 2; }
            else if (id.StartsWith("del:"))
            {
                DateTime day = DateTime.ParseExact(id.Substring(4), "yyyyMMdd", CultureInfo.InvariantCulture);
                if (Confirm("Delete the history for " + day.ToString("D") + "?"))
                {
                    store.Days.Remove(day);
                    store.Save();
                    dataChanged();
                }
            }
            else if (id.StartsWith("graph:"))
            {
                DateTime day = DateTime.ParseExact(id.Substring(6), "yyyyMMdd", CultureInfo.InvariantCulture);
                DayStat d;
                if (store.Days.TryGetValue(day, out d)) DayGraphWindow.ShowFor(this, d);
            }
            else if (id == "set:name")
            {
                string n = NamePrompt.Ask(store.TyperName, false, this);
                if (n != null) { store.TyperName = n; store.Save(); dataChanged(); }
            }
            else if (id == "set:donate") DonateWindow.ShowFor(this);
            else if (id == "set:update")
            {
                if (Updater.Available != null)
                {
                    if (Confirm("Update Live WPM to version " + Updater.Available + " now? It will restart by itself."))
                        Updater.Install(this);
                }
                else Updater.Check(true);
            }
            else if (id == "link:fb")
            {
                try { System.Diagnostics.Process.Start("https://fb.com/tasin.py"); } catch { }
            }
            else if (id == "link:discord")
            {
                // Discord has no profile links by username, so copy it for pasting into "Add Friend".
                try { Clipboard.SetText("tasin_py"); } catch { }
                discordCopied = true;
                Timer t = new Timer();
                t.Interval = 1600;
                t.Tick += delegate { t.Dispose(); discordCopied = false; Invalidate(); };
                t.Start();
            }
            else if (id == "set:startup")
            {
                store.StartWithWindows = !store.StartWithWindows;
                store.Save();
                startupChanged();
            }
            else if (id == "set:peak" && Confirm(name + ", reset your peak speed? Everything else stays."))
            {
                store.ResetPeak(); store.Save(); dataChanged();
            }
            else if (id == "set:words" && Confirm(name + ", reset your lifetime word count to zero?"))
            {
                store.ResetWords(); store.Save(); dataChanged();
            }
            else if (id == "set:all" && Confirm(name + ", erase ALL stats and history? This can't be undone."))
            {
                store.ResetAll(); store.Save(); dataChanged();
            }
            hover = null;
            Invalidate();
        }

        bool Confirm(string q)
        {
            return MessageBox.Show(this, q, Theme.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                                   MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // ---- painting ------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            g.ScaleTransform(S, S);
            hits.Clear();

            DrawHeader(g);
            if (page == 0) DrawHome(g);
            else if (page == 1) DrawHistory(g);
            else DrawSettings(g);
        }

        void DrawHeader(Graphics g)
        {
            g.DrawImage(logo, new RectangleF(28, 26, 38, 38));
            Theme.DrawText(g, "TYPER", Theme.F(10, true), Theme.Accent, 78, 25);
            Theme.DrawText(g, store.TyperName, Theme.F(21, true), Theme.Text, 77, 38);

            float right = W - 28;
            RectangleF gear = new RectangleF(right - 36, 30, 36, 34);
            Pill(g, gear, page == 2, Hov("tab:settings"));
            DrawGear(g, gear.X + gear.Width / 2, gear.Y + gear.Height / 2,
                     page == 2 || Hov("tab:settings") ? Theme.Text : Theme.Muted,
                     page == 2 ? Theme.Card : (Hov("tab:settings") ? Theme.CardHover : Theme.Bg));
            Hit(gear, "tab:settings");

            RectangleF hist = new RectangleF(gear.X - 8 - 90, 30, 90, 34);
            Pill(g, hist, page == 1, Hov("tab:history"));
            Theme.DrawTextCenter(g, "History", Theme.F(13, true), page == 1 ? Theme.Text : Theme.Muted, hist.X + hist.Width / 2, hist.Y + 9);
            Hit(hist, "tab:history");

            RectangleF home = new RectangleF(hist.X - 4 - 78, 30, 78, 34);
            Pill(g, home, page == 0, Hov("tab:home"));
            Theme.DrawTextCenter(g, "Home", Theme.F(13, true), page == 0 ? Theme.Text : Theme.Muted, home.X + home.Width / 2, home.Y + 9);
            Hit(home, "tab:home");

            using (Pen div = new Pen(Theme.Border, 1)) g.DrawLine(div, 28, 84, W - 28, 84);
        }

        static void Pill(Graphics g, RectangleF r, bool active, bool hover)
        {
            if (active) Theme.FillRound(g, Theme.Card, r, 9);
            else if (hover) Theme.FillRound(g, Theme.CardHover, r, 9);
        }

        // -- home --

        void DrawHome(Graphics g)
        {
            // today's speed graph (a fresh one every day)
            DayStat today;
            store.Days.TryGetValue(DateTime.Today, out today);
            Theme.DrawText(g, "TODAY'S SPEED", Theme.F(10.5f, true), Theme.Muted, 30, 100);
            if (today != null && today.Points.Count > 0)
                Theme.DrawTextRight(g, Theme.Num(today.Points.Count) + (today.Points.Count == 1 ? " run" : " runs") + "  ·  avg wpm per run",
                                    Theme.F(10.5f, false), Theme.Muted, W - 30, 100);
            Theme.FillRound(g, Theme.Card, TodayGraph, 12);
            SpeedGraph.Draw(g, TodayGraph, today, graphHover, "Your runs today will show up here as you type.", Theme.Card);

            float cw = (W - 56 - 32) / 3f;
            Theme.DrawText(g, "BEST RUN", Theme.F(10.5f, true), Theme.Muted, 30, 298);
            Theme.DrawText(g, "LIFETIME", Theme.F(10.5f, true), Theme.Muted, 30, 434);

            bool hasPeak = store.PeakWpm > 0;
            string when = hasPeak ? store.PeakTime.ToString("MMM d, yyyy · h:mm tt") : "type a bit to set it";
            Card(g, 28, 316, cw, "PEAK SPEED", hasPeak ? Theme.Fmt(store.PeakWpm) : "—", "wpm", when, Theme.Accent);
            Card(g, 28 + cw + 16, 316, cw, "ACCURACY", hasPeak ? Theme.Fmt(store.PeakAcc) : "—", "%", "on your peak run", Theme.Text);
            Card(g, 28 + 2 * (cw + 16), 316, cw, "WORDS AT ONCE", hasPeak ? Theme.Num(store.PeakWords) : "—", "words", "typed in that run", Theme.Text);

            bool hasAvg = store.AvgSeconds > 0;
            Card(g, 28, 452, cw, "AVERAGE SPEED", hasAvg ? Theme.Fmt(store.AvgWpm) : "—", "wpm", "across all your runs", Theme.Accent);
            Card(g, 28 + cw + 16, 452, cw, "ACCURACY", hasAvg ? Theme.Fmt(store.AvgAccuracy) : "—", "%", "lifetime average", Theme.Text);
            Card(g, 28 + 2 * (cw + 16), 452, cw, "TOTAL WORDS", Theme.Num(store.TotalWords), "words", "typed in your lifetime", Theme.Text);

            string status = store.Tracking ? "Tracking on" : "Paused";
            Color sc = store.Tracking ? Theme.Accent : Theme.Off;
            float tw = Theme.Measure(g, status, Theme.F(11.5f, true));
            using (SolidBrush b = new SolidBrush(sc)) g.FillEllipse(b, W - 30 - tw - 14, H - 35, 8, 8);
            Theme.DrawTextRight(g, status, Theme.F(11.5f, true), sc, W - 30, H - 40);
        }

        static void Card(Graphics g, float x, float y, float w, string caption, string value, string unit, string foot, Color valueColor)
        {
            RectangleF r = new RectangleF(x, y, w, 104);
            Theme.FillRound(g, Theme.Card, r, 12);
            Theme.DrawText(g, caption, Theme.F(10, true), Theme.Muted, x + 18, y + 14);
            Font big = Theme.F(32, true);
            Theme.DrawText(g, value, big, valueColor, x + 16, y + 30);
            Theme.DrawText(g, unit, Theme.F(12.5f, false), Theme.Muted, x + 20 + Theme.Measure(g, value, big), y + 50);
            Theme.DrawText(g, foot, Theme.F(10.5f, false), Theme.Muted, x + 18, y + 78);
        }

        int graphHover = -1;

        // -- history --

        void DrawHistory(Graphics g)
        {
            const float cPeak = 352, cAvg = 482, cWords = W - 48;
            Font hf = Theme.F(10, true);
            Theme.DrawText(g, "DATE", hf, Theme.Muted, 46, 106);
            Theme.DrawTextRight(g, "PEAK", hf, Theme.Muted, cPeak, 106);
            Theme.DrawTextRight(g, "AVERAGE", hf, Theme.Muted, cAvg, 106);
            Theme.DrawTextRight(g, "WORDS", hf, Theme.Muted, cWords, 106);

            List<DayStat> days = store.Days.Values.OrderByDescending(d => d.Date).ToList();
            if (days.Count == 0)
            {
                Theme.DrawTextCenter(g, "No history yet, " + store.TyperName + ".", Theme.F(15, true), Theme.Text, W / 2f, 230);
                Theme.DrawTextCenter(g, "Turn tracking on from the tray icon and start typing anywhere.", Theme.F(12, false), Theme.Muted, W / 2f, 256);
                return;
            }

            RectangleF clip = new RectangleF(0, ListTop - 4, W, ListBottom - ListTop + 4);
            GraphicsState st = g.Save();
            g.SetClip(clip);
            DateTime today = DateTime.Today;
            for (int i = 0; i < days.Count; i++)
            {
                DayStat d = days[i];
                float y = ListTop + i * RowH - scroll;
                if (y + RowH < clip.Top || y > clip.Bottom) continue;
                string key = d.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                RectangleF row = new RectangleF(28, y, W - 56, RowH - 6);
                bool hot = Hov("row:" + key) || Hov("del:" + key) || Hov("graph:" + key);
                Theme.FillRound(g, hot ? Theme.CardHover : Theme.Card, row, 9);

                string label = d.Date == today ? "Today" : d.Date == today.AddDays(-1) ? "Yesterday" : d.Date.ToString("ddd, MMM d");
                Font df = Theme.F(13, true);
                Theme.DrawText(g, label, df, Theme.Text, 46, y + 11);
                float lw = Theme.Measure(g, label, df);
                Theme.DrawText(g, d.Date.ToString("yyyy"), Theme.F(11, false), Theme.Muted, 52 + lw, y + 13);

                Font vf = Theme.F(13, true);
                Theme.DrawTextRight(g, d.PeakWpm > 0 ? Theme.Fmt(d.PeakWpm) + " wpm" : "—", vf, Theme.Accent, cPeak, y + 11);
                Theme.DrawTextRight(g, d.AvgWpm > 0 ? Theme.Fmt(d.AvgWpm) + " wpm" : "—", vf, Theme.Text, cAvg, y + 11);
                Theme.DrawTextRight(g, Theme.Num(d.Words), vf, Theme.Text, cWords, y + 11);

                RectangleF rowHit = RectangleF.Intersect(row, clip);
                Hit(rowHit, "row:" + key);
                if (hot)
                {
                    RectangleF del = new RectangleF(52 + lw + Theme.Measure(g, "0000", Theme.F(11, false)) + 12, y + 7, 28, 26);
                    bool dh = Hov("del:" + key);
                    if (dh) Theme.FillRound(g, Color.FromArgb(60, Theme.Red), del, 7);
                    DrawTrash(g, del.X + del.Width / 2, del.Y + del.Height / 2, dh ? Color.FromArgb(255, 110, 110) : Theme.Muted);
                    Hit(RectangleF.Intersect(del, clip), "del:" + key);

                    RectangleF gr = new RectangleF(del.Right + 4, del.Y, 28, 26);
                    bool gh = Hov("graph:" + key);
                    if (gh) Theme.FillRound(g, Color.FromArgb(50, Theme.Accent), gr, 7);
                    DrawGraphIcon(g, gr.X + gr.Width / 2, gr.Y + gr.Height / 2, gh ? Theme.Accent : Theme.Muted);
                    Hit(RectangleF.Intersect(gr, clip), "graph:" + key);
                }
            }
            g.Restore(st);
        }

        // -- settings --

        void DrawSettings(Graphics g)
        {
            Theme.DrawText(g, "SETTINGS", Theme.F(10.5f, true), Theme.Muted, 30, 102);
            float y = 124;
            SettingRow(g, ref y, "Typer Name", store.TyperName, "set:name", "Change", false, null);
            SettingRow(g, ref y, "Start with Windows", "Listed in Startup apps; launches quietly in the tray", "set:startup", null, false, store.StartWithWindows);
            SettingPair(g, ref y,
                "Donate", "Support Live WPM via bKash", "set:donate", "Donate",
                "Updates", Updater.Status, "set:update", Updater.Available != null ? "Update" : (Updater.Busy ? "Checking" : "Check"));
            SettingPair(g, ref y,
                "Reset peak speed", "Clears only your peak record", "set:peak", "Reset",
                "Reset word count", "Lifetime words back to zero", "set:words", "Reset");
            SettingRow(g, ref y, "Reset all", "Erases every stat and all history (keeps your Typer Name)", "set:all", "Reset all", true, null);

            // maker's mark and socials, one quiet centred line
            Font f = Theme.F(11, false);
            Color dim = Color.FromArgb(170, Theme.Muted);
            string[] parts = { "made by ~ Tasin", "fb.com/tasin.py", discordCopied ? "copied tasin_py" : "discord: tasin_py" };
            string[] ids = { null, "link:fb", "link:discord" };
            const float gap = 12; // space on each side of the separator dots
            float[] widths = new float[parts.Length];
            float total = gap * 2 * (parts.Length - 1);
            for (int i = 0; i < parts.Length; i++) total += widths[i] = Theme.Measure(g, parts[i], f);
            float x = (W - total) / 2f, ty = H - 30;
            float dotY = ty + f.Height * 0.5f; // the dots sit on the middle of the text
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    x += gap;
                    using (SolidBrush b = new SolidBrush(dim)) g.FillEllipse(b, x - 1.5f, dotY - 1.5f, 3, 3);
                    x += gap;
                }
                Color c = ids[i] == null ? dim : (Hov(ids[i]) || (i == 2 && discordCopied) ? Theme.Accent : Theme.Muted);
                Theme.DrawText(g, parts[i], f, c, x, ty);
                if (ids[i] != null) Hit(new RectangleF(x - 4, ty - 4, widths[i] + 8, 22), ids[i]);
                x += widths[i];
            }
        }

        bool discordCopied;

        void SettingRow(Graphics g, ref float y, string title, string sub, string id, string button, bool danger, bool? toggle)
        {
            SettingCard(g, new RectangleF(28, y, W - 56, 52), title, sub, id, button, danger, toggle);
            y += 60;
        }

        // Two half-width cards side by side.
        void SettingPair(Graphics g, ref float y, string t1, string s1, string id1, string b1, string t2, string s2, string id2, string b2)
        {
            float w = (W - 56 - 8) / 2f;
            SettingCard(g, new RectangleF(28, y, w, 52), t1, s1, id1, b1, false, null);
            SettingCard(g, new RectangleF(28 + w + 8, y, w, 52), t2, s2, id2, b2, false, null);
            y += 60;
        }

        void SettingCard(Graphics g, RectangleF r, string title, string sub, string id, string button, bool danger, bool? toggle)
        {
            Theme.FillRound(g, Theme.Card, r, 10);
            Theme.DrawText(g, title, Theme.F(13, true), danger ? Color.FromArgb(255, 110, 110) : Theme.Text, r.X + 18, r.Y + 9);
            Theme.DrawText(g, sub, Theme.F(11, false), Theme.Muted, r.X + 18, r.Y + 28);

            if (toggle.HasValue)
            {
                RectangleF sw = new RectangleF(r.Right - 62, r.Y + 15, 44, 22);
                TrayMenu.DrawSwitch(g, sw, toggle.Value);
                Hit(r, id);
            }
            else
            {
                Font bf = Theme.F(12, true);
                float bw = Theme.Measure(g, button, bf) + 34;
                RectangleF b = new RectangleF(r.Right - 16 - bw, r.Y + 11, bw, 30);
                bool h = Hov(id);
                Color fill = danger ? Color.FromArgb(h ? 90 : 55, Theme.Red) : (h ? Theme.Lerp(Theme.CardHover, Theme.Accent, 0.25) : Theme.CardHover);
                Theme.FillRound(g, fill, b, 8);
                Theme.DrawTextCenter(g, button, bf, danger ? Color.FromArgb(255, 140, 140) : Theme.Text, b.X + bw / 2, b.Y + 7);
                Hit(b, id);
            }
        }

        // -- icons --

        static void DrawGear(Graphics g, float cx, float cy, Color c, Color bg)
        {
            using (Pen teeth = new Pen(c, 3.4f))
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(teeth, cx + (float)Math.Cos(a) * 5, cy + (float)Math.Sin(a) * 5,
                                      cx + (float)Math.Cos(a) * 9.5f, cy + (float)Math.Sin(a) * 9.5f);
                }
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, cx - 7, cy - 7, 14, 14);
            using (SolidBrush h = new SolidBrush(bg)) g.FillEllipse(h, cx - 3, cy - 3, 6, 6);
        }

        static void DrawTrash(Graphics g, float cx, float cy, Color c)
        {
            using (Pen p = new Pen(c, 1.5f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 6, cy - 4.5f, cx + 6, cy - 4.5f);
                g.DrawLine(p, cx - 2, cy - 7, cx + 2, cy - 7);
                using (GraphicsPath body = Theme.Round(new RectangleF(cx - 4.5f, cy - 3, 9, 10), 1.5f)) g.DrawPath(p, body);
                g.DrawLine(p, cx - 1.5f, cy - 0.5f, cx - 1.5f, cy + 4.5f);
                g.DrawLine(p, cx + 1.5f, cy - 0.5f, cx + 1.5f, cy + 4.5f);
            }
        }

        // a small rising line chart
        static void DrawGraphIcon(Graphics g, float cx, float cy, Color c)
        {
            using (Pen p = new Pen(c, 1.5f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                g.DrawLine(p, cx - 7, cy + 6.5f, cx + 7, cy + 6.5f); // baseline
                g.DrawLines(p, new PointF[] {
                    new PointF(cx - 6, cy + 2.5f), new PointF(cx - 2, cy - 1.5f),
                    new PointF(cx + 1.5f, cy + 1), new PointF(cx + 6, cy - 5) });
            }
        }

        protected override void OnScaleChanged()
        {
            logo.Dispose();
            logo = Theme.Logo((int)(38 * S), Theme.Accent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) logo.Dispose();
            base.Dispose(disposing);
        }
    }
}
