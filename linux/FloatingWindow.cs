using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // The small draggable speedometer. Every number on it is animated toward its target, so a
    // jump from 35 to 75 wpm sweeps up like a car's dial. Ported from src/FloatingWindow.cs: the
    // animation and drawing are the same; the see-through window and blur come from Avalonia.
    sealed class FloatingWindow : PaintedWindow
    {
        const int W = 240, H = 184;
        const float CX = W / 2f, CY = 98, R = 58, Corner = 8;
        static readonly RectangleF CloseRect = new RectangleF(W - 34, 8, 26, 26);
        static readonly RectangleF PinRect = new RectangleF(W - 62, 8, 26, 26);

        readonly DataStore store;
        readonly TypingEngine engine;
        readonly DispatcherTimer anim = new DispatcherTimer(DispatcherPriority.Render);
        readonly Stopwatch clock = Stopwatch.StartNew();
        long lastFrame;

        double needle, needleV, avg, peak, words, dialMax;
        double live, arcShow;
        double cr = LiveWpm.Theme.Dial.R, cg = LiveWpm.Theme.Dial.G, cb = LiveWpm.Theme.Dial.B;
        double opacity, opacityTarget;
        bool hideWhenFaded, hoverPin, hoverClose, shown;
        long previewUntil;
        DispatcherTimer saveLater;

        public event Action PinChanged, CloseClicked;

        public FloatingWindow(DataStore store, TypingEngine engine)
        {
            this.store = store;
            this.engine = engine;
            Title = LiveWpm.Theme.AppName;
            SystemDecorations = SystemDecorations.None;
            ShowInTaskbar = false;
            ShowActivated = false;          // never takes focus away from your typing
            Topmost = store.Pinned;
            SetLogicalSize(W, H);
            RoundCorners = Corner;
            dialMax = MaxFor(store.PeakWpm);
            anim.Interval = TimeSpan.FromMilliseconds(15);
            anim.Tick += delegate { Frame(); };
            ApplyLook();
            PlaceOnScreen();
            PositionChanged += delegate { RememberPosition(); };
            // Some window managers place a new window themselves, ignoring the position set before it
            // appeared; once it is up, put it where it belongs.
            Opened += delegate { DispatcherTimer.RunOnce(PlaceOnScreen, TimeSpan.FromMilliseconds(120)); };
        }

        bool dragged;   // only a move the Typer made by dragging is remembered

        // The saved spot if it is still on a screen, else the bottom-right corner of the main screen.
        // The screen list can be empty until the window is first shown, so Appear tries again.
        bool placed, placing;

        void PlaceOnScreen()
        {
            Screen primary = Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
            if (primary == null) return;
            placing = true;
            try
            {
                double sc = primary.Scaling;
                PixelPoint wanted = new PixelPoint(store.FloatX, store.FloatY);
                placed = true;
                if (store.FloatX != int.MinValue)
                    foreach (Screen s in Screens.All)
                        if (s.WorkingArea.Intersects(new PixelRect(wanted, new PixelSize((int)(W * sc), (int)(H * sc)))))
                        {
                            Position = wanted;
                            return;
                        }
                PixelRect wa = primary.WorkingArea;
                int m = (int)(16 * sc);
                Position = new PixelPoint(wa.Right - (int)(W * sc) - m, wa.Bottom - (int)(H * sc) - m);
            }
            finally { placing = false; }
        }

        // Saved a moment after the Typer stops dragging it.
        void RememberPosition()
        {
            if (!shown || placing || !dragged) return;
            if (saveLater == null)
            {
                saveLater = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                saveLater.Tick += delegate
                {
                    saveLater.Stop();
                    store.FloatX = Position.X;
                    store.FloatY = Position.Y;
                    store.Save();
                };
            }
            saveLater.Stop();
            saveLater.Start();
        }

        public void ApplyPin()
        {
            Topmost = store.Pinned;
            Invalidate();
        }

        public void Preview()
        {
            previewUntil = clock.ElapsedMilliseconds + 2500;
            ApplyLook();
            Appear();
        }

        static Color Faint { get { return LiveWpm.Theme.Light ? LiveWpm.Theme.Text : LiveWpm.Theme.Muted; } }

        void ApplyLook()
        {
            cr = LiveWpm.Theme.Dial.R; cg = LiveWpm.Theme.Dial.G; cb = LiveWpm.Theme.Dial.B;
            UseTransparency(LiveWpm.Theme.Glass);
            Invalidate();
        }

        // ---- visibility -----------------------------------------------------

        public void Appear()
        {
            hideWhenFaded = false;
            opacityTarget = 1;
            if (!IsVisible)
            {
                opacity = 0;
                Show();
                if (!placed) PlaceOnScreen();
                shown = true;
            }
            Kick();
        }

        public void Disappear()
        {
            if (!IsVisible) return;
            opacityTarget = 0;
            hideWhenFaded = true;
            Kick();
        }

        public void Kick()
        {
            if (anim.IsEnabled) return;
            lastFrame = clock.ElapsedMilliseconds;
            anim.Start();
        }

        static double MaxFor(double peak) { return peak > 0 ? peak : 60; }

        static double Step(double v, double target, double amount)
        {
            return target > v ? Math.Min(target, v + amount) : Math.Max(target, v - amount);
        }

        static double Approach(double v, double target, double rate, double dt)
        {
            return v + (target - v) * (1 - Math.Exp(-rate * dt));
        }

        // ---- animation (the same as Windows) ---------------------------------------

        void Frame()
        {
            long now = clock.ElapsedMilliseconds;
            double dt = Math.Min(0.05, Math.Max(0.001, (now - lastFrame) / 1000.0));
            lastFrame = now;

            double tNeedle = 0, tAvg = 0, tPeak = 0, tWords = 0, tArc = 0, tLive = 0;
            TypingSession s = engine.Session;
            if (engine.Phase == Phase.Typing && engine.Shown)
            {
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

            const double k = 38, c = 10.5;
            needleV += (k * (tNeedle - needle) - c * needleV) * dt;
            needle += needleV * dt;
            if (needle < 0) { needle = 0; needleV = 0; }

            live = tLive <= 0 ? 0 : Approach(live, tLive, 12, dt);
            avg = Approach(avg, tAvg, 4.5, dt);
            peak = Approach(peak, tPeak, 4.5, dt);
            words = Approach(words, tWords, 9, dt);
            dialMax = Approach(dialMax, tMax, 3, dt);
            arcShow = Step(arcShow, tArc, dt * 2.2);

            Color target;
            if (engine.RecordPace) target = LiveWpm.Theme.Orange;
            else
            {
                double f = needle / dialMax;
                target = f >= 0.85 ? LiveWpm.Theme.Lerp(LiveWpm.Theme.Dial, LiveWpm.Theme.Hot, (f - 0.85) / 0.15) : LiveWpm.Theme.Dial;
            }
            double ct = 1 - Math.Exp(-4 * dt);
            cr += (target.R - cr) * ct; cg += (target.G - cg) * ct; cb += (target.B - cb) * ct;

            opacity = Step(opacity, opacityTarget, dt * 4.5);
            bool previewing = now < previewUntil;

            bool calm = !engine.Shown && needle < 0.4 && Math.Abs(needleV) < 0.4 &&
                        avg < 0.5 && peak < 0.5 && words < 0.5 && arcShow <= 0 && Math.Abs(dialMax - tMax) < 0.2;

            bool dropping = !engine.Shown && needle < dialMax * 0.15;
            if ((calm || dropping) && store.OnlyWhenTyping && !hideWhenFaded && IsVisible && !previewing) Disappear();

            if (hideWhenFaded && opacity <= 0)
            {
                Hide();
                hideWhenFaded = false;
            }

            if (calm && opacity == opacityTarget && !previewing || !IsVisible)
            {
                needle = needleV = avg = peak = words = live = 0;
                dialMax = tMax;
                cr = LiveWpm.Theme.Dial.R; cg = LiveWpm.Theme.Dial.G; cb = LiveWpm.Theme.Dial.B;
                anim.Stop();
            }
            Invalidate();
        }

        // ---- painting -----------------------------------------------------------

        protected override void OnPaint(Graphics g)
        {
            if (opacity <= 0.001) return;
            GraphicsState st = g.Save();
            if (opacity < 0.999) g.PushOpacity(opacity);
            float a = store.FloatBgOpacity / 100f;
            LiveWpm.Theme.AlphaText = true;
            LiveWpm.Theme.TextShadow = a < 0.6f ? (int)(150 * (0.6f - a) / 0.6f) : 0;
            if (LiveWpm.Theme.Glass) LiveWpm.Theme.TextShadow = Math.Max(LiveWpm.Theme.TextShadow, LiveWpm.Theme.GlassTextShadow);
            try { DrawFrame(g); }
            finally { LiveWpm.Theme.AlphaText = false; LiveWpm.Theme.TextShadow = 0; g.Restore(st); }
        }

        void DrawFrame(Graphics g)
        {
            bool record = engine.RecordPace;
            DrawBackground(g);

            string cur = LiveWpm.Theme.Fmt(live);
            Font big = LiveWpm.Theme.F(22, true);
            LiveWpm.Theme.DrawText(g, cur, big, LiveWpm.Theme.Text, 14, 9);
            LiveWpm.Theme.DrawText(g, "wpm now", LiveWpm.Theme.F(10, LiveWpm.Theme.Light), Faint, 14 + LiveWpm.Theme.Measure(g, cur, big) + 5, 20);

            DrawPin(g);
            LiveWpm.Theme.DrawCloseX(g, CloseRect, hoverClose, Faint);
            DrawDial(g);

            string av = LiveWpm.Theme.Fmt(avg);
            LiveWpm.Theme.DrawTextCenter(g, av, LiveWpm.Theme.F(32, record), record ? LiveWpm.Theme.Orange : LiveWpm.Theme.Text, CX, CY - 24);
            LiveWpm.Theme.DrawTextCenter(g, "avg wpm", LiveWpm.Theme.F(10, false), Faint, CX, CY + 15);

            LiveWpm.Theme.DrawText(g, "PEAK", LiveWpm.Theme.F(LiveWpm.Theme.Light ? 10 : 9.5f, true), Faint, 14, H - 40);
            LiveWpm.Theme.DrawText(g, LiveWpm.Theme.Fmt(peak), LiveWpm.Theme.F(17, true), LiveWpm.Theme.Text, 13, H - 28);
            LiveWpm.Theme.DrawTextRight(g, "WORDS", LiveWpm.Theme.F(LiveWpm.Theme.Light ? 10 : 9.5f, true), Faint, W - 14, H - 40);
            LiveWpm.Theme.DrawTextRight(g, LiveWpm.Theme.Num((long)Math.Round(words)), LiveWpm.Theme.F(17, true), LiveWpm.Theme.Text, W - 13, H - 28);
        }

        // The Typer's background opacity. Never fully clear, so the window still catches the mouse at 0 %.
        // Without the desktop's blur (or without transparency at all) glass is tinted more strongly,
        // so the numbers stay readable over a busy desktop.
        void DrawBackground(Graphics g)
        {
            float a = store.FloatBgOpacity / 100f;
            RectangleF r = new RectangleF(0, 0, W, H);
            Color tint = LiveWpm.Theme.FloatTint;
            int alpha = tint.A;
            if (LiveWpm.Theme.Glass && !Blurred) alpha = Math.Max(alpha, 190);
            if (!SeeThrough) { alpha = 255; a = 1; }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Max(1, (int)Math.Round(alpha * a)), tint)))
                g.FillRectangle(b, r);
            if (!LiveWpm.Theme.Glass) return;

            double s = 0.5 + 0.5 * a;
            LiveWpm.Theme.Bevel(g, LiveWpm.Theme.RoundExact(new RectangleF(0.5f, 0.5f, W - 1, H - 1), Corner), r,
                        Color.FromArgb((int)(210 * s), 255, 255, 255), Color.FromArgb((int)(36 * s), 255, 255, 255));
            LiveWpm.Theme.Bevel(g, LiveWpm.Theme.RoundExact(new RectangleF(1.5f, 1.5f, W - 3, H - 3), Corner - 1), r,
                        Color.FromArgb(0, 0, 0, 0), Color.FromArgb((int)(70 * s), 0, 0, 0));
        }

        void DrawDial(Graphics g)
        {
            RectangleF arc = new RectangleF(CX - R, CY - R, R * 2, R * 2);
            using (Pen track = new Pen(LiveWpm.Theme.Track, 7))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                g.DrawArc(track, arc, 135, 270);
            }

            for (int i = 0; i <= 10; i++)
            {
                double ang = (135 + 27 * i) * Math.PI / 180;
                float r1 = R - (i % 5 == 0 ? 16 : 13), r2 = R - 9;
                using (Pen tick = new Pen(Color.FromArgb(i % 5 == 0 ? 150 : 80, Faint), i % 5 == 0 ? 1.6f : 1.1f))
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

            double an = (135 + sweep) * Math.PI / 180;
            float cos = (float)Math.Cos(an), sin = (float)Math.Sin(an);
            using (Pen n = new Pen(LiveWpm.Theme.Text, 2.6f))
            {
                n.StartCap = n.EndCap = LineCap.Round;
                g.DrawLine(n, CX + cos * (R - 24), CY + sin * (R - 24), CX + cos * (R - 3), CY + sin * (R - 3));
            }
            using (SolidBrush tip = new SolidBrush(LiveWpm.Theme.Text))
                g.FillEllipse(tip, CX + cos * R - 3.5f, CY + sin * R - 3.5f, 7, 7);
        }

        void DrawPin(Graphics g)
        {
            bool on = store.Pinned;
            if (on) LiveWpm.Theme.FillRound(g, Color.FromArgb(40, LiveWpm.Theme.Dial), PinRect, 7);
            else if (hoverPin) LiveWpm.Theme.FillRound(g, LiveWpm.Theme.CardHover, PinRect, 7);
            Color c = on ? LiveWpm.Theme.Dial : (hoverPin ? LiveWpm.Theme.Text : Faint);

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

        protected override void OnMouseMove(float x, float y)
        {
            bool hp = PinRect.Contains(x, y), hc = CloseRect.Contains(x, y);
            if (hp != hoverPin || hc != hoverClose)
            {
                hoverPin = hp; hoverClose = hc;
                SetHandCursor(hp || hc);
                Invalidate();
            }
        }

        protected override void OnMouseLeave()
        {
            if (hoverPin || hoverClose) { hoverPin = hoverClose = false; SetHandCursor(false); Invalidate(); }
        }

        protected override void OnMouseDown(float x, float y, MouseButton b)
        {
            if (b != MouseButton.Left) return;
            if (PinRect.Contains(x, y))
            {
                store.Pinned = !store.Pinned;
                ApplyPin();
                store.Save();
                if (PinChanged != null) PinChanged();
            }
            else if (CloseRect.Contains(x, y)) { if (CloseClicked != null) CloseClicked(); }
            else { dragged = true; BeginDrag(); }
        }

        public void Dispose()
        {
            anim.Stop();
            if (saveLater != null) saveLater.Stop();
            Close();
        }
    }
}
