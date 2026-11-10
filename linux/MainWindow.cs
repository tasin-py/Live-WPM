using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // The app window: Home (today's graph and six stat cards), History (one row per day) and
    // Settings. Ported from src/MainWindow.cs; the drawing is the same, line for line where it can be.
    sealed class MainWindow : PaintedWindow
    {
        const int W = 720, H = 636;   // one more Settings row than Windows: Keyboard access
        static readonly RectangleF TodayGraph = new RectangleF(28, 120, W - 56, 160);
        const float ListTop = 136, RowH = 46, ListBottom = H - 18;

        readonly DataStore store;
        readonly KeyboardInput keyboard;
        readonly Action startupChanged, dataChanged, appearanceChanged;
        readonly List<KeyValuePair<RectangleF, string>> hits = new List<KeyValuePair<RectangleF, string>>();
        int page;          // 0 home, 1 history, 2 settings
        string hover;
        float scroll;
        bool busy;         // a dialog is open

        public MainWindow(DataStore store, KeyboardInput keyboard, Action startupChanged, Action dataChanged, Action appearanceChanged)
        {
            this.store = store;
            this.keyboard = keyboard;
            this.startupChanged = startupChanged;
            this.dataChanged = dataChanged;
            this.appearanceChanged = appearanceChanged;
            Title = LiveWpm.Theme.AppName;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SetLogicalSize(W, H);
        }

        public int Page { get { return page; } set { page = value; Invalidate(); } }

        public void RefreshData() { Invalidate(); }

        // ---- hit testing ---------------------------------------------------------

        void Hit(RectangleF r, string id) { hits.Add(new KeyValuePair<RectangleF, string>(r, id)); }

        string HitAt(float x, float y)
        {
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Key.Contains(x, y)) return hits[i].Value;
            return null;
        }

        bool Hov(string id) { return hover == id; }

        protected override void OnMouseMove(float x, float y)
        {
            if (sliding) { SlideTo(x); return; }
            if (page == 0)
            {
                DayStat today;
                store.Days.TryGetValue(DateTime.Today, out today);
                int gh = SpeedGraph.HitTest(TodayGraph, today, x, y);
                if (gh != graphHover) { graphHover = gh; Invalidate(); }
            }
            string h = HitAt(x, y);
            if (h != hover)
            {
                hover = h;
                SetHandCursor(h != null && !h.StartsWith("row:"));
                Invalidate();
            }
        }

        protected override void OnMouseLeave()
        {
            if (sliding) return;
            if (hover != null || graphHover >= 0) { hover = null; graphHover = -1; Invalidate(); }
        }

        protected override void OnMouseWheel(float delta)
        {
            if (page != 1) return;
            float max = Math.Max(0, store.Days.Count * RowH - (ListBottom - ListTop));
            scroll = Math.Max(0, Math.Min(max, scroll - delta * RowH));
            hover = null;
            Invalidate();
        }

        protected override async void OnMouseUp(float x, float y, MouseButton b)
        {
            if (b != MouseButton.Left) return;
            if (sliding)
            {
                sliding = false;
                store.Save();
                Invalidate();
                return;
            }
            // A click counts only if it also started here.
            string id = HitAt(x, y);
            bool started = id == pressed;
            pressed = null;
            if (id == null || !started || busy) return;
            string name = store.TyperName;
            busy = true;
            try
            {
                if (id == "tab:home") { page = 0; }
                else if (id == "tab:history") { page = 1; scroll = 0; }
                else if (id == "tab:settings") { page = 2; }
                else if (id.StartsWith("del:"))
                {
                    DateTime day = DateTime.ParseExact(id.Substring(4), "yyyyMMdd", CultureInfo.InvariantCulture);
                    if (await Confirm("Delete the history for " + day.ToString("D") + "?"))
                    {
                        store.Days.Remove(day);
                        store.ForgetDay(day);
                        store.Save();
                        dataChanged();
                    }
                }
                else if (id.StartsWith("graph:"))
                {
                    DateTime day = DateTime.ParseExact(id.Substring(6), "yyyyMMdd", CultureInfo.InvariantCulture);
                    DayStat d;
                    if (store.Days.TryGetValue(day, out d)) await DayGraphWindow.ShowFor(this, d);
                }
                else if (id == "set:name")
                {
                    string n = await NamePrompt.Ask(store.TyperName, false, this);
                    if (n != null) { store.TyperName = n; store.Save(); dataChanged(); }
                }
                else if (id == "set:theme") await ThemePicker.ShowFor(this, store.ThemeName, SetTheme);
                else if (id == "set:donate") await DonateWindow.ShowFor(this);
                else if (id == "set:keyboard") await ExplainKeyboard();
                else if (id == "set:update")
                {
                    if (Updater.Available != null)
                    {
                        if (!Updater.CanInstall) Platform.Open(Updater.ReleasePage);
                        else if (await Confirm("Update Live WPM to version " + Updater.Available + " now? It will restart by itself."))
                            Updater.Install(this);
                    }
                    else Updater.Check(true);
                }
                else if (id == "link:github") Platform.Open("https://github.com/tasin-py/");
                else if (id == "link:fb") Platform.Open("https://fb.com/tasin.py");
                else if (id == "link:discord")
                {
                    // Discord has no profile links by username, so copy it for pasting into "Add Friend".
                    CopyText("tasin_py");
                    discordCopied = true;
                    DispatcherTimer.RunOnce(delegate { discordCopied = false; Invalidate(); }, TimeSpan.FromMilliseconds(1600));
                }
                else if (id == "set:startup")
                {
                    store.StartWithWindows = !store.StartWithWindows;
                    store.Save();
                    startupChanged();
                }
                else if (id == "set:peak")
                {
                    if (await Confirm(name + ", reset your peak speed? Everything else stays.")) { store.ResetPeak(); store.Save(); dataChanged(); }
                }
                else if (id == "set:words")
                {
                    if (await Confirm(name + ", reset your lifetime word count to zero?")) { store.ResetWords(); store.Save(); dataChanged(); }
                }
                else if (id == "set:undo")
                {
                    SessionUndo last = store.LastSession;
                    if (last == null) await MessageWindow.Info(this, "There's no recent session to remove.");
                    else if (await Confirm(name + ", remove your last session (" + LastLabel(last) + ")?" +
                                           (last.Counted && store.PeakWpm > last.PrevPeakWpm ? " It set your peak speed, so your previous peak comes back." : "")))
                    {
                        store.RemoveLastSession(); store.Save(); dataChanged();
                    }
                }
                else if (id == "set:all")
                {
                    if (await Confirm(name + ", erase ALL stats and history? This can't be undone.")) { store.ResetAll(); store.Save(); dataChanged(); }
                }
            }
            finally { busy = false; }
            hover = null;
            Invalidate();
        }

        System.Threading.Tasks.Task<bool> Confirm(string q) { return MessageWindow.Confirm(this, q); }

        async System.Threading.Tasks.Task ExplainKeyboard()
        {
            const string about =
                "Live WPM's keyboard service reads your keyboard for it. The service runs locked down by systemd: " +
                "no network, no access to your files, and it sends Live WPM only the kind of each key " +
                "(a letter, a space or a backspace), never which key. Your own account needs no extra rights.";
            if (keyboard.Access == KeyboardAccess.NoPermission)
            {
                if (KeyboardInput.ServiceInstalled)
                {
                    const string cmd = "sudo systemctl enable --now live-wpm-keys.socket";
                    CopyText(cmd);
                    await MessageWindow.Info(this, about + "\n\nIt's installed but switched off. Run this once in a terminal:\n\n" +
                        cmd + "\n\nThe command is already copied for you. Live WPM connects by itself a few seconds later.");
                }
                else
                    await MessageWindow.Info(this, about + "\n\nIt isn't installed yet. Run install.sh from the Live WPM download again " +
                        "and answer yes to the keyboard service, or install the Arch package. Both are explained under Linux in the README.");
            }
            else if (keyboard.Access == KeyboardAccess.NotAllowed)
                await MessageWindow.Info(this, "The keyboard service only answers the person logged in at this screen. " +
                    "It doesn't for remote logins, or while another user has switched in.");
            else if (keyboard.Access == KeyboardAccess.NoKeyboard)
                await MessageWindow.Info(this, "No keyboard was found. Plug one in and Live WPM picks it up within a few seconds.");
            else if (keyboard.Mode == KeyboardMode.Direct)
                await MessageWindow.Info(this,
                    "Live WPM is reading your keyboard itself, because your account is in the \"input\" group. " +
                    "That works, but it also lets every other program you run read your keys. " +
                    "The keyboard service is the safer way: install it, and you can leave the input group with\n\n" +
                    "sudo gpasswd -d $USER input");
            else
                await MessageWindow.Info(this, about + " Turn tracking off from the tray menu and it stops reading the keyboard completely.");
        }

        // ---- painting ------------------------------------------------------------

        protected override void OnPaint(Graphics g)
        {
            if (LiveWpm.Theme.Glass) using (SolidBrush b = new SolidBrush(LiveWpm.Theme.GlassBase)) g.FillRectangle(b, 0, 0, W, H);
            else LiveWpm.Theme.PaintBackground(g, W, H);
            hits.Clear();
            DrawHeader(g);
            if (page == 0) DrawHome(g);
            else if (page == 1) DrawHistory(g);
            else DrawSettings(g);
        }

        void DrawHeader(Graphics g)
        {
            LiveWpm.Theme.DrawLogo(g, new RectangleF(28, 26, 38, 38), LiveWpm.Theme.LogoAccent);
            LiveWpm.Theme.DrawText(g, "TYPER", LiveWpm.Theme.F(10, true), LiveWpm.Theme.Accent, 78, 25);
            LiveWpm.Theme.DrawText(g, store.TyperName, LiveWpm.Theme.F(21, true), LiveWpm.Theme.Text, 77, 38);

            float right = W - 28;
            RectangleF gear = new RectangleF(right - 36, 30, 36, 34);
            Pill(g, gear, page == 2, Hov("tab:settings"));
            DrawGear(g, gear.X + gear.Width / 2, gear.Y + gear.Height / 2,
                     page == 2 || Hov("tab:settings") ? LiveWpm.Theme.Text : LiveWpm.Theme.Muted);
            Hit(gear, "tab:settings");

            RectangleF hist = new RectangleF(gear.X - 8 - 90, 30, 90, 34);
            Pill(g, hist, page == 1, Hov("tab:history"));
            LiveWpm.Theme.DrawTextCenter(g, "History", LiveWpm.Theme.F(13, true), page == 1 ? LiveWpm.Theme.Text : LiveWpm.Theme.Muted, hist.X + hist.Width / 2, hist.Y + 9);
            Hit(hist, "tab:history");

            RectangleF home = new RectangleF(hist.X - 4 - 78, 30, 78, 34);
            Pill(g, home, page == 0, Hov("tab:home"));
            LiveWpm.Theme.DrawTextCenter(g, "Home", LiveWpm.Theme.F(13, true), page == 0 ? LiveWpm.Theme.Text : LiveWpm.Theme.Muted, home.X + home.Width / 2, home.Y + 9);
            Hit(home, "tab:home");

            using (Pen div = new Pen(LiveWpm.Theme.Border, 1)) g.DrawLine(div, 28, 84, W - 28, 84);
        }

        static void Pill(Graphics g, RectangleF r, bool active, bool hover)
        {
            if (active) LiveWpm.Theme.Surface(g, LiveWpm.Theme.Card, r, 9);
            else if (hover) LiveWpm.Theme.FillRound(g, LiveWpm.Theme.CardHover, r, 9);
        }

        // -- home --

        void DrawHome(Graphics g)
        {
            DayStat today;
            store.Days.TryGetValue(DateTime.Today, out today);
            LiveWpm.Theme.DrawText(g, "TODAY'S SPEED", LiveWpm.Theme.F(10.5f, true), LiveWpm.Theme.Muted, 30, 100);
            if (today != null && today.Points.Count > 0)
                LiveWpm.Theme.DrawTextRight(g, LiveWpm.Theme.Num(today.Points.Count) + (today.Points.Count == 1 ? " run" : " runs") + "  ·  avg wpm per run",
                                    LiveWpm.Theme.F(10.5f, false), LiveWpm.Theme.Muted, W - 30, 100);
            LiveWpm.Theme.Surface(g, LiveWpm.Theme.Card, TodayGraph, 12);
            SpeedGraph.Draw(g, TodayGraph, today, graphHover, "Your runs today will show up here as you type.", LiveWpm.Theme.Card);

            float cw = (W - 56 - 32) / 3f;
            LiveWpm.Theme.DrawText(g, "BEST RUN", LiveWpm.Theme.F(10.5f, true), LiveWpm.Theme.Muted, 30, 298);
            LiveWpm.Theme.DrawText(g, "LIFETIME", LiveWpm.Theme.F(10.5f, true), LiveWpm.Theme.Muted, 30, 434);

            bool hasPeak = store.PeakWpm > 0;
            string when = hasPeak ? store.PeakTime.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture) : "type a bit to set it";
            Card(g, 28, 316, cw, "PEAK SPEED", hasPeak ? LiveWpm.Theme.Fmt(store.PeakWpm) : "—", "wpm", when, LiveWpm.Theme.Accent);
            Card(g, 28 + cw + 16, 316, cw, "ACCURACY", hasPeak ? LiveWpm.Theme.Fmt(store.PeakAcc) : "—", "%", "on your peak run", LiveWpm.Theme.Text);
            Card(g, 28 + 2 * (cw + 16), 316, cw, "WORDS AT ONCE", hasPeak ? LiveWpm.Theme.Num(store.PeakWords) : "—", "words", "typed in that run", LiveWpm.Theme.Text);

            bool hasAvg = store.AvgSeconds > 0;
            Card(g, 28, 452, cw, "AVERAGE SPEED", hasAvg ? LiveWpm.Theme.Fmt(store.AvgWpm) : "—", "wpm", "across all your runs", LiveWpm.Theme.Accent);
            Card(g, 28 + cw + 16, 452, cw, "ACCURACY", hasAvg ? LiveWpm.Theme.Fmt(store.AvgAccuracy) : "—", "%", "lifetime average", LiveWpm.Theme.Text);
            Card(g, 28 + 2 * (cw + 16), 452, cw, "TOTAL WORDS", LiveWpm.Theme.Num(store.TotalWords), "words", "typed in your lifetime", LiveWpm.Theme.Text);

            string status; Color sc;
            if (!store.Tracking) { status = "Paused"; sc = LiveWpm.Theme.Off; }
            else if (keyboard.Access == KeyboardAccess.NoPermission) { status = "Keyboard service needed: see Settings"; sc = LiveWpm.Theme.DangerText; }
            else if (keyboard.Access == KeyboardAccess.NotAllowed) { status = "Not this session"; sc = LiveWpm.Theme.Muted; }
            else if (keyboard.Access == KeyboardAccess.NoKeyboard) { status = "No keyboard found"; sc = LiveWpm.Theme.Muted; }
            else { status = "Tracking on"; sc = LiveWpm.Theme.Accent; }
            float tw = LiveWpm.Theme.Measure(g, status, LiveWpm.Theme.F(11.5f, true));
            using (SolidBrush b = new SolidBrush(sc)) g.FillEllipse(b, W - 30 - tw - 14, H - 35, 8, 8);
            LiveWpm.Theme.DrawTextRight(g, status, LiveWpm.Theme.F(11.5f, true), sc, W - 30, H - 40);
        }

        static void Card(Graphics g, float x, float y, float w, string caption, string value, string unit, string foot, Color valueColor)
        {
            RectangleF r = new RectangleF(x, y, w, 104);
            LiveWpm.Theme.Surface(g, LiveWpm.Theme.Card, r, 12);
            LiveWpm.Theme.DrawText(g, caption, LiveWpm.Theme.F(10, true), LiveWpm.Theme.Muted, x + 18, y + 14);
            Font big = LiveWpm.Theme.F(32, true);
            LiveWpm.Theme.DrawText(g, value, big, valueColor, x + 16, y + 30);
            LiveWpm.Theme.DrawText(g, unit, LiveWpm.Theme.F(12.5f, false), LiveWpm.Theme.Muted, x + 20 + LiveWpm.Theme.Measure(g, value, big), y + 50);
            LiveWpm.Theme.DrawText(g, foot, LiveWpm.Theme.F(10.5f, false), LiveWpm.Theme.Muted, x + 18, y + 78);
        }

        int graphHover = -1;

        // -- history --

        void DrawHistory(Graphics g)
        {
            const float cPeak = 352, cAvg = 482, cWords = W - 48;
            Font hf = LiveWpm.Theme.F(10, true);
            LiveWpm.Theme.DrawText(g, "DATE", hf, LiveWpm.Theme.Muted, 46, 106);
            LiveWpm.Theme.DrawTextRight(g, "PEAK", hf, LiveWpm.Theme.Muted, cPeak, 106);
            LiveWpm.Theme.DrawTextRight(g, "AVERAGE", hf, LiveWpm.Theme.Muted, cAvg, 106);
            LiveWpm.Theme.DrawTextRight(g, "WORDS", hf, LiveWpm.Theme.Muted, cWords, 106);

            List<DayStat> days = store.Days.Values.OrderByDescending(d => d.Date).ToList();
            if (days.Count == 0)
            {
                LiveWpm.Theme.DrawTextCenter(g, "No history yet, " + store.TyperName + ".", LiveWpm.Theme.F(15, true), LiveWpm.Theme.Text, W / 2f, 230);
                LiveWpm.Theme.DrawTextCenter(g, "Turn tracking on from the tray icon and start typing anywhere.", LiveWpm.Theme.F(12, false), LiveWpm.Theme.Muted, W / 2f, 256);
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
                LiveWpm.Theme.Surface(g, hot ? LiveWpm.Theme.CardHover : LiveWpm.Theme.Card, row, 9);

                string label = d.Date == today ? "Today" : d.Date == today.AddDays(-1) ? "Yesterday" : d.Date.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
                Font df = LiveWpm.Theme.F(13, true);
                LiveWpm.Theme.DrawText(g, label, df, LiveWpm.Theme.Text, 46, y + 11);
                float lw = LiveWpm.Theme.Measure(g, label, df);
                LiveWpm.Theme.DrawText(g, d.Date.ToString("yyyy", CultureInfo.InvariantCulture), LiveWpm.Theme.F(11, false), LiveWpm.Theme.Muted, 52 + lw, y + 13);

                Font vf = LiveWpm.Theme.F(13, true);
                LiveWpm.Theme.DrawTextRight(g, d.PeakWpm > 0 ? LiveWpm.Theme.Fmt(d.PeakWpm) + " wpm" : "—", vf, LiveWpm.Theme.Accent, cPeak, y + 11);
                LiveWpm.Theme.DrawTextRight(g, d.AvgWpm > 0 ? LiveWpm.Theme.Fmt(d.AvgWpm) + " wpm" : "—", vf, LiveWpm.Theme.Text, cAvg, y + 11);
                LiveWpm.Theme.DrawTextRight(g, LiveWpm.Theme.Num(d.Words), vf, LiveWpm.Theme.Text, cWords, y + 11);

                RectangleF rowHit = RectangleF.Intersect(row, clip);
                Hit(rowHit, "row:" + key);
                if (hot)
                {
                    RectangleF del = new RectangleF(52 + lw + LiveWpm.Theme.Measure(g, "0000", LiveWpm.Theme.F(11, false)) + 12, y + 7, 28, 26);
                    bool dh = Hov("del:" + key);
                    if (dh) LiveWpm.Theme.FillRound(g, Color.FromArgb(60, LiveWpm.Theme.Danger), del, 7);
                    DrawTrash(g, del.X + del.Width / 2, del.Y + del.Height / 2, dh ? LiveWpm.Theme.DangerText : LiveWpm.Theme.Muted);
                    Hit(RectangleF.Intersect(del, clip), "del:" + key);

                    RectangleF gr = new RectangleF(del.Right + 4, del.Y, 28, 26);
                    bool gh = Hov("graph:" + key);
                    if (gh) LiveWpm.Theme.FillRound(g, Color.FromArgb(50, LiveWpm.Theme.Accent), gr, 7);
                    DrawGraphIcon(g, gr.X + gr.Width / 2, gr.Y + gr.Height / 2, gh ? LiveWpm.Theme.Accent : LiveWpm.Theme.Muted);
                    Hit(RectangleF.Intersect(gr, clip), "graph:" + key);
                }
            }
            g.Restore(st);
        }

        // -- settings --

        const float SetRowH = 36;

        void DrawSettings(Graphics g)
        {
            float y = 100;
            RectangleF grp = Group(g, ref y, "GENERAL", 5);
            ButtonRow(g, Row(grp, 0), "Typer Name", store.TyperName, "set:name", "Change", false);
            SwitchRow(g, Row(grp, 1), "Start on login", "Launches quietly in the tray", "set:startup", store.StartWithWindows);
            ButtonRow(g, Row(grp, 2), "Theme", LiveWpm.Theme.Current.Name, "set:theme", "Change", false);
            SliderRow(g, Row(grp, 3), "Floating window background");
            string kv; string kb;
            switch (keyboard.Access)
            {
                case KeyboardAccess.Ok:
                    kv = (keyboard.Count == 1 ? "1 keyboard" : keyboard.Count + " keyboards") +
                         (keyboard.Mode == KeyboardMode.Direct ? ", through the input group" : ", through the keyboard service");
                    kb = "About"; break;
                case KeyboardAccess.NoPermission: kv = KeyboardInput.ServiceInstalled ? "Keyboard service is off" : "Keyboard service not installed"; kb = "Set up"; break;
                case KeyboardAccess.NotAllowed: kv = "Not available in this session"; kb = "Help"; break;
                case KeyboardAccess.NoKeyboard: kv = "No keyboard found"; kb = "Help"; break;
                default: kv = "Off while paused"; kb = "About"; break;
            }
            ButtonRow(g, Row(grp, 4), "Keyboard access", kv, "set:keyboard", kb, keyboard.Access == KeyboardAccess.NoPermission);

            SessionUndo last = store.LastSession;
            grp = Group(g, ref y, "DATA", 4);
            ButtonRow(g, Row(grp, 0), "Remove last session", last != null ? LastLabel(last) : "Nothing to remove", "set:undo", "Remove", false);
            ButtonRow(g, Row(grp, 1), "Reset peak speed", null, "set:peak", "Reset", false);
            ButtonRow(g, Row(grp, 2), "Reset word count", null, "set:words", "Reset", false);
            ButtonRow(g, Row(grp, 3), "Reset all", "Keeps your name", "set:all", "Reset all", true);

            grp = Group(g, ref y, "ABOUT", 2);
            ButtonRow(g, Row(grp, 0), "Updates", Updater.Status, "set:update",
                      Updater.Available != null ? (Updater.CanInstall ? "Update" : "Open") : (Updater.Busy ? "Checking" : "Check"), false);
            ButtonRow(g, Row(grp, 1), "Support Live WPM", null, "set:donate", "Buy Me A Coffee", false);

            // maker's mark and socials, one quiet centred line
            Font f = LiveWpm.Theme.F(11, false);
            Color dim = Color.FromArgb(170, LiveWpm.Theme.Muted);
            string[] parts = { "Tasin's Problem", "github.com/tasin-py", "fb.com/tasin.py", discordCopied ? "copied tasin_py" : "discord: tasin_py" };
            string[] ids = { null, "link:github", "link:fb", "link:discord" };
            const float gap = 12;
            float[] widths = new float[parts.Length];
            float total = gap * 2 * (parts.Length - 1);
            for (int i = 0; i < parts.Length; i++) total += widths[i] = LiveWpm.Theme.Measure(g, parts[i], f);
            float x = (W - total) / 2f, ty = H - 30;
            float dotY = ty + f.Height * 0.5f;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    x += gap;
                    if (LiveWpm.Theme.Cute) LiveWpm.Theme.Heart(g, Color.FromArgb(200, LiveWpm.Theme.Accent), x, dotY, 7);
                    else using (SolidBrush b = new SolidBrush(dim)) g.FillEllipse(b, x - 1.5f, dotY - 1.5f, 3, 3);
                    x += gap;
                }
                Color c = ids[i] == null ? dim : (Hov(ids[i]) || (ids[i] == "link:discord" && discordCopied) ? LiveWpm.Theme.Accent : LiveWpm.Theme.Muted);
                LiveWpm.Theme.DrawText(g, parts[i], f, c, x, ty);
                if (ids[i] != null) Hit(new RectangleF(x - 4, ty - 4, widths[i] + 8, 22), ids[i]);
                x += widths[i];
            }
        }

        bool discordCopied;

        static string LastLabel(SessionUndo u)
        {
            string when = u.Started.Date == DateTime.Today ? u.Started.ToString("h:mm tt", CultureInfo.InvariantCulture)
                                                          : u.Started.ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture);
            return LiveWpm.Theme.Num(u.Words) + (u.Words == 1 ? " word" : " words") + " at " + when;
        }

        RectangleF Group(Graphics g, ref float y, string label, int rows)
        {
            LiveWpm.Theme.DrawText(g, label, LiveWpm.Theme.F(10, true), LiveWpm.Theme.Muted, 30, y);
            RectangleF r = new RectangleF(28, y + 17, W - 56, rows * SetRowH);
            LiveWpm.Theme.Surface(g, LiveWpm.Theme.Card, r, 10);
            using (Pen div = new Pen(Color.FromArgb(LiveWpm.Theme.Light ? 24 : 16, LiveWpm.Theme.Text), 1))
                for (int i = 1; i < rows; i++)
                    g.DrawLine(div, r.X + 16, r.Y + i * SetRowH, r.Right - 16, r.Y + i * SetRowH);
            y = r.Bottom + 14;
            return r;
        }

        static RectangleF Row(RectangleF group, int i)
        {
            return new RectangleF(group.X, group.Y + i * SetRowH, group.Width, SetRowH);
        }

        void RowTitle(Graphics g, RectangleF r, string title, bool danger)
        {
            LiveWpm.Theme.DrawText(g, title, LiveWpm.Theme.F(12.5f, true), danger ? LiveWpm.Theme.DangerText : LiveWpm.Theme.Text, r.X + 16, r.Y + 10);
        }

        void ButtonRow(Graphics g, RectangleF r, string title, string value, string id, string button, bool danger)
        {
            RowTitle(g, r, title, danger);
            Font bf = LiveWpm.Theme.F(11.5f, true);
            float bw = LiveWpm.Theme.Measure(g, button, bf) + 26;
            RectangleF b = new RectangleF(r.Right - 8 - bw, r.Y + 5, bw, SetRowH - 10);
            bool h = Hov(id);
            Color fill = danger ? Color.FromArgb(h ? 80 : 40, LiveWpm.Theme.Danger) : (h ? LiveWpm.Theme.Lerp(LiveWpm.Theme.CardHover, LiveWpm.Theme.Accent, 0.25) : LiveWpm.Theme.CardHover);
            LiveWpm.Theme.FillRound(g, fill, b, 7);
            LiveWpm.Theme.DrawTextCenter(g, button, bf, danger ? LiveWpm.Theme.DangerText : LiveWpm.Theme.Text, b.X + bw / 2, b.Y + 6);
            Hit(b, id);
            if (!string.IsNullOrEmpty(value))
                LiveWpm.Theme.DrawTextRight(g, value, LiveWpm.Theme.F(11.5f, false), LiveWpm.Theme.Muted, b.X - 12, r.Y + 11);
        }

        void SwitchRow(Graphics g, RectangleF r, string title, string value, string id, bool on)
        {
            RowTitle(g, r, title, false);
            RectangleF sw = new RectangleF(r.Right - 12 - 38, r.Y + 8, 38, 20);
            LiveWpm.Theme.DrawSwitch(g, sw, on);
            LiveWpm.Theme.DrawTextRight(g, value, LiveWpm.Theme.F(11.5f, false), LiveWpm.Theme.Muted, sw.X - 12, r.Y + 11);
            Hit(r, id);
        }

        RectangleF sliderTrack;
        bool sliding;
        string pressed;

        void SliderRow(Graphics g, RectangleF r, string title)
        {
            RowTitle(g, r, title, false);
            sliderTrack = new RectangleF(r.Right - 20 - 140, r.Y + SetRowH / 2 - 2, 140, 4);
            float frac = store.FloatBgOpacity / 100f;
            LiveWpm.Theme.FillRound(g, LiveWpm.Theme.Track, sliderTrack, 2);
            if (frac > 0) LiveWpm.Theme.FillAccent(g, new RectangleF(sliderTrack.X, sliderTrack.Y, sliderTrack.Width * frac, 4), 2);
            float kx = sliderTrack.X + sliderTrack.Width * frac;
            bool hot = sliding || Hov("slider:bg");
            float kr = hot ? 7.5f : 6.5f;
            using (SolidBrush k = new SolidBrush(LiveWpm.Theme.Text)) g.FillEllipse(k, kx - kr, sliderTrack.Y + 2 - kr, kr * 2, kr * 2);
            LiveWpm.Theme.DrawTextRight(g, store.FloatBgOpacity + "%", LiveWpm.Theme.F(11.5f, false), hot ? LiveWpm.Theme.Text : LiveWpm.Theme.Muted, sliderTrack.X - 14, r.Y + 11);
            Hit(new RectangleF(sliderTrack.X - 10, r.Y, sliderTrack.Width + 20, SetRowH), "slider:bg");
        }

        public void SetTheme(string id)
        {
            store.ThemeName = id;
            store.Save();
            LiveWpm.Theme.Apply(id);
            appearanceChanged();
            Invalidate();
        }

        void SlideTo(float x)
        {
            int v = (int)Math.Round((x - sliderTrack.X) / sliderTrack.Width * 20) * 5; // steps of 5 %
            v = Math.Max(0, Math.Min(100, v));
            if (v == store.FloatBgOpacity) return;
            store.FloatBgOpacity = v;
            appearanceChanged();
            Invalidate();
        }

        protected override void OnMouseDown(float x, float y, MouseButton b)
        {
            if (b != MouseButton.Left) return;
            pressed = HitAt(x, y);
            if (pressed != "slider:bg") return;
            sliding = true;
            SlideTo(x);
        }

        // -- icons --

        static void DrawGear(Graphics g, float cx, float cy, Color c)
        {
            using (Pen teeth = new Pen(c, 3.4f))
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(teeth, cx + (float)Math.Cos(a) * 7, cy + (float)Math.Sin(a) * 7,
                                      cx + (float)Math.Cos(a) * 9.5f, cy + (float)Math.Sin(a) * 9.5f);
                }
            using (GraphicsPath ring = new GraphicsPath(FillMode.Alternate))
            using (SolidBrush b = new SolidBrush(c))
            {
                ring.AddEllipse(cx - 7, cy - 7, 14, 14);
                ring.AddEllipse(cx - 3, cy - 3, 6, 6);
                g.FillPath(b, ring);
            }
        }

        static void DrawTrash(Graphics g, float cx, float cy, Color c)
        {
            using (Pen p = new Pen(c, 1.5f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 6, cy - 4.5f, cx + 6, cy - 4.5f);
                g.DrawLine(p, cx - 2, cy - 7, cx + 2, cy - 7);
                using (GraphicsPath body = LiveWpm.Theme.Round(new RectangleF(cx - 4.5f, cy - 3, 9, 10), 1.5f)) g.DrawPath(p, body);
                g.DrawLine(p, cx - 1.5f, cy - 0.5f, cx - 1.5f, cy + 4.5f);
                g.DrawLine(p, cx + 1.5f, cy - 0.5f, cx + 1.5f, cy + 4.5f);
            }
        }

        static void DrawGraphIcon(Graphics g, float cx, float cy, Color c)
        {
            using (Pen p = new Pen(c, 1.5f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                g.DrawLine(p, cx - 7, cy + 6.5f, cx + 7, cy + 6.5f);
                g.DrawLines(p, new PointF[] {
                    new PointF(cx - 6, cy + 2.5f), new PointF(cx - 2, cy - 1.5f),
                    new PointF(cx + 1.5f, cy + 1), new PointF(cx + 6, cy - 5) });
            }
        }
    }
}
