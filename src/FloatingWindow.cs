using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiveWpm
{
    // The small draggable speedometer. Every number on it is animated toward its target,
    // so a jump from 35 to 75 wpm sweeps up like a car's dial instead of snapping.
    sealed class FloatingWindow : BufferedForm
    {
        const int W = 240, H = 184;
        const float CX = W / 2f, CY = 98, R = 58;
        static readonly RectangleF CloseRect = new RectangleF(W - 34, 8, 26, 26);
        static readonly RectangleF PinRect = new RectangleF(W - 62, 8, 26, 26);

        readonly DataStore store;
        readonly TypingEngine engine;
        readonly Timer anim = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        long lastFrame;

        double needle, needleV, avg, peak, words, dialMax;
        double live;                       // top-left "wpm now": follows the live speed closely, not the needle
        double arcShow;                    // how visible the coloured speed arc is (0..1)
        double cr = Theme.Accent.R, cg = Theme.Accent.G, cb = Theme.Accent.B; // arc colour, kept as doubles so fades finish
        double opacity, opacityTarget;    // tracked here: Form.Opacity rounds to 1/255 and would never "arrive"
        bool hideWhenFaded, hoverPin, hoverClose;

        public event Action PinChanged, CloseClicked;

        public FloatingWindow(DataStore store, TypingEngine engine)
        {
            this.store = store;
            this.engine = engine;
            Text = Theme.AppName;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            SetLogicalSize(W, H);
            TopMost = store.Pinned;
            dialMax = MaxFor(store.PeakWpm);
            anim.Interval = 15;
            anim.Tick += delegate { Frame(); };
            PlaceOnScreen();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE: never steals focus from your typing
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.RoundCorners(Handle);
        }

        void PlaceOnScreen()
        {
            Rectangle wanted = new Rectangle(store.FloatX, store.FloatY, Width, Height);
            bool onScreen = false;
            foreach (Screen sc in Screen.AllScreens)
                if (store.FloatX != int.MinValue && sc.WorkingArea.IntersectsWith(wanted)) onScreen = true;
            if (onScreen) { Location = wanted.Location; return; }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int m = (int)(16 * S);
            Location = new Point(wa.Right - Width - m, wa.Bottom - Height - m);
        }

        public void ApplyPin()
        {
            TopMost = store.Pinned;
            Invalidate();
        }

        // ---- visibility -----------------------------------------------------

        public void Appear()
        {
            hideWhenFaded = false;
            opacityTarget = 1;
            if (!Visible)
            {
                opacity = 0;
                Opacity = 0;
                Show();
            }
            Kick();
        }

        public void Disappear()
        {
            if (!Visible) return;
            opacityTarget = 0;
            hideWhenFaded = true;
            Kick();
        }

        public void Kick()
        {
            if (anim.Enabled) return;
            lastFrame = clock.ElapsedMilliseconds;
            anim.Start();
        }

        static double MaxFor(double peak) { return peak > 0 ? peak : 60; }

        // Moves linearly toward the target and lands on it exactly.
        static double Step(double v, double target, double amount)
        {
            return target > v ? Math.Min(target, v + amount) : Math.Max(target, v - amount);
        }

        static double Approach(double v, double target, double rate, double dt)
        {
            return v + (target - v) * (1 - Math.Exp(-rate * dt));
        }

        // ---- animation --------------------------------------------------------

        void Frame()
        {
            long now = clock.ElapsedMilliseconds;
            double dt = Math.Min(0.05, Math.Max(0.001, (now - lastFrame) / 1000.0));
            lastFrame = now;

            double tNeedle = 0, tAvg = 0, tPeak = 0, tWords = 0, tArc = 0, tLive = 0;
            TypingSession s = engine.Session;
            if (engine.Phase == Phase.Typing && engine.Shown)
            {
                // Once you pause, the dial settles on the session's average and the coloured
                // arc fades away; typing again brings it back.
                bool paused = engine.Paused;
                tLive = engine.CurrentWpm;
                tNeedle = paused ? s.AvgWpm : tLive;
                tArc = paused ? 0 : 1;
                tAvg = s.AvgWpm; tPeak = s.PeakWpm; tWords = s.LiveWords;
            }
            else if (engine.Phase == Phase.Holding && s != null)
            {
                tNeedle = s.AvgWpm; tAvg = s.AvgWpm; tPeak = s.PeakWpm; tWords = s.Words;
            }
            double tMax = MaxFor(!engine.Shown ? store.PeakWpm : engine.StartPeak);

            // Slightly under-damped spring: the needle sweeps and settles with a tiny overshoot.
            const double k = 38, c = 10.5;
            needleV += (k * (tNeedle - needle) - c * needleV) * dt;
            needle += needleV * dt;
            if (needle < 0) { needle = 0; needleV = 0; }

            // "wpm now" drops to 0 the instant you stop, and tracks the live speed quickly otherwise.
            live = tLive <= 0 ? 0 : Approach(live, tLive, 12, dt);
            avg = Approach(avg, tAvg, 4.5, dt);
            peak = Approach(peak, tPeak, 4.5, dt);
            words = Approach(words, tWords, 9, dt);
            dialMax = Approach(dialMax, tMax, 3, dt);
            arcShow = Step(arcShow, tArc, dt * 2.2); // ~0.45 s fade

            Color target;
            if (engine.RecordPace) target = Theme.Orange;
            else
            {
                double f = needle / dialMax;
                target = f >= 0.85 ? Theme.Lerp(Theme.Accent, Theme.Red, (f - 0.85) / 0.15) : Theme.Accent;
            }
            double ct = 1 - Math.Exp(-4 * dt);
            cr += (target.R - cr) * ct; cg += (target.G - cg) * ct; cb += (target.B - cb) * ct;

            opacity = Step(opacity, opacityTarget, dt * 4.5);
            Opacity = opacity;

            bool calm = !engine.Shown && needle < 0.4 && Math.Abs(needleV) < 0.4 &&
                        avg < 0.5 && peak < 0.5 && words < 0.5 && arcShow <= 0 && Math.Abs(dialMax - tMax) < 0.2;

            // "Only when typing": pop out as the dial drops back toward zero.
            bool dropping = !engine.Shown && needle < dialMax * 0.15;
            if ((calm || dropping) && store.OnlyWhenTyping && !hideWhenFaded && Visible) Disappear();

            if (hideWhenFaded && opacity <= 0)
            {
                Hide();
                hideWhenFaded = false;
            }

            if (calm && opacity == opacityTarget || !Visible)
            {
                needle = needleV = avg = peak = words = live = 0;
                dialMax = tMax;
                cr = Theme.Accent.R; cg = Theme.Accent.G; cb = Theme.Accent.B;
                anim.Stop();
            }
            Invalidate();
        }

        // ---- painting -----------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            g.ScaleTransform(S, S);
            bool record = engine.RecordPace;

            g.Clear(Theme.Bg);

            // top-left: exact current speed
            string cur = Theme.Fmt(live);
            Font big = Theme.F(22, true);
            Theme.DrawText(g, cur, big, Theme.Text, 14, 9);
            Theme.DrawText(g, "wpm now", Theme.F(10, false), Theme.Muted, 14 + Theme.Measure(g, cur, big) + 5, 20);

            DrawPin(g);
            DrawClose(g);
            DrawDial(g);

            // centre: session average
            string a = Theme.Fmt(avg);
            Theme.DrawTextCenter(g, a, Theme.F(32, record), record ? Theme.Orange : Theme.Text, CX, CY - 24);
            Theme.DrawTextCenter(g, "avg wpm", Theme.F(10, false), Theme.Muted, CX, CY + 15);

            // bottom-left: session peak, bottom-right: words
            Theme.DrawText(g, "PEAK", Theme.F(9.5f, true), Theme.Muted, 14, H - 40);
            Theme.DrawText(g, Theme.Fmt(peak), Theme.F(17, true), Theme.Text, 13, H - 28);
            Theme.DrawTextRight(g, "WORDS", Theme.F(9.5f, true), Theme.Muted, W - 14, H - 40);
            Theme.DrawTextRight(g, Theme.Num((long)Math.Round(words)), Theme.F(17, true), Theme.Text, W - 13, H - 28);
        }

        void DrawDial(Graphics g)
        {
            RectangleF arc = new RectangleF(CX - R, CY - R, R * 2, R * 2);
            using (Pen track = new Pen(Theme.Track, 7))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                g.DrawArc(track, arc, 135, 270);
            }

            for (int i = 0; i <= 10; i++)
            {
                double ang = (135 + 27 * i) * Math.PI / 180;
                float r1 = R - (i % 5 == 0 ? 16 : 13), r2 = R - 9;
                using (Pen tick = new Pen(Color.FromArgb(i % 5 == 0 ? 150 : 80, Theme.Muted), i % 5 == 0 ? 1.6f : 1.1f))
                    g.DrawLine(tick, CX + (float)Math.Cos(ang) * r1, CY + (float)Math.Sin(ang) * r1,
                                     CX + (float)Math.Cos(ang) * r2, CY + (float)Math.Sin(ang) * r2);
            }

            double frac = Math.Max(0, Math.Min(1, needle / dialMax));
            float sweep = (float)(270 * frac);
            if (sweep > 0.5f && arcShow > 0)
            {
                using (Pen val = new Pen(Color.FromArgb((int)(255 * arcShow), (int)cr, (int)cg, (int)cb), 7))
                {
                    val.StartCap = val.EndCap = LineCap.Round;
                    g.DrawArc(val, arc, 135, sweep);
                }
            }

            // pointer: an outer needle segment, leaving the centre free for the average
            double a = (135 + sweep) * Math.PI / 180;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            using (Pen n = new Pen(Theme.Text, 2.6f))
            {
                n.StartCap = n.EndCap = LineCap.Round;
                g.DrawLine(n, CX + cos * (R - 24), CY + sin * (R - 24), CX + cos * (R - 3), CY + sin * (R - 3));
            }
            using (SolidBrush tip = new SolidBrush(Theme.Text))
                g.FillEllipse(tip, CX + cos * R - 3.5f, CY + sin * R - 3.5f, 7, 7);
        }

        void DrawClose(Graphics g)
        {
            if (hoverClose) Theme.FillRound(g, Theme.Red, CloseRect, 7);
            float cx = CloseRect.X + CloseRect.Width / 2, cy = CloseRect.Y + CloseRect.Height / 2;
            using (Pen p = new Pen(hoverClose ? Color.White : Theme.Muted, 1.6f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                g.DrawLine(p, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
            }
        }

        void DrawPin(Graphics g)
        {
            bool on = store.Pinned;
            if (on) Theme.FillRound(g, Color.FromArgb(40, Theme.Accent), PinRect, 7);
            else if (hoverPin) Theme.FillRound(g, Theme.CardHover, PinRect, 7);
            Color c = on ? Theme.Accent : (hoverPin ? Theme.Text : Theme.Muted);

            GraphicsState st = g.Save();
            g.TranslateTransform(PinRect.X + PinRect.Width / 2, PinRect.Y + PinRect.Height / 2);
            g.RotateTransform(45);
            using (Pen p = new Pen(c, 1.6f))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                RectangleF head = new RectangleF(-3.5f, -8, 7, 6);
                if (on) g.FillRectangle(b, head); else g.DrawRectangle(p, head.X, head.Y, head.Width, head.Height);
                g.DrawLine(p, -6, -1.5f, 6, -1.5f);
                g.DrawLine(p, 0, -1.5f, 0, 7.5f);
            }
            g.Restore(st);
        }

        // ---- mouse --------------------------------------------------------------

        bool OverPin(Point p) { return PinRect.Contains(p.X / S, p.Y / S); }
        bool OverClose(Point p) { return CloseRect.Contains(p.X / S, p.Y / S); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hp = OverPin(e.Location), hc = OverClose(e.Location);
            if (hp != hoverPin || hc != hoverClose)
            {
                hoverPin = hp; hoverClose = hc;
                Cursor = hp || hc ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverPin || hoverClose) { hoverPin = hoverClose = false; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (OverPin(e.Location))
            {
                store.Pinned = !store.Pinned;
                ApplyPin();
                store.Save();
                if (PinChanged != null) PinChanged();
            }
            else if (OverClose(e.Location)) { if (CloseClicked != null) CloseClicked(); }
            else Theme.BeginDrag(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x232) // WM_EXITSIZEMOVE: remember where the Typer parked it
            {
                store.FloatX = Left;
                store.FloatY = Top;
                store.Save();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) anim.Dispose();
            base.Dispose(disposing);
        }
    }
}
