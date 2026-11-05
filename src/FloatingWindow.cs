using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveWpm
{
    // The small draggable speedometer. Every number on it is animated toward its target,
    // so a jump from 35 to 75 wpm sweeps up like a car's dial instead of snapping.
    // Each frame is drawn with per-pixel transparency and handed to Windows, which rounds the
    // corners (smoothly, on Windows 11) and composites it: the background can fade (Settings)
    // while the numbers and dial stay solid, and in the glass theme Windows blurs what is behind it.
    sealed class FloatingWindow : BufferedForm
    {
        const int W = 240, H = 184;
        const float CX = W / 2f, CY = 98, R = 58, Corner = 8; // Corner: Windows 11's own window rounding
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
        double cr = Theme.Dial.R, cg = Theme.Dial.G, cb = Theme.Dial.B; // arc colour, kept as doubles so fades finish
        double opacity, opacityTarget;    // whole-window fade in/out (applied when the frame is pushed to the screen)
        bool hideWhenFaded, hoverPin, hoverClose;
        long previewUntil;                 // Settings changed: stay up briefly so the Typer sees the result
        int blurState = -1;

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
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false); // frames go straight to the window, alpha intact
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
            try
            {
                int none = unchecked((int)0xFFFFFFFE);
                DwmSetWindowAttribute(Handle, 34, ref none, 4);   // no system border: the theme draws its own edge
                MARGINS all = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                DwmExtendFrameIntoClientArea(Handle, ref all);      // let the frame's alpha show through
            }
            catch { }
            blurState = -1;
            ApplyLook();
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
            Render();
        }

        // Theme or background opacity changed in Settings: restyle, and show it for a moment.
        public void Preview()
        {
            previewUntil = clock.ElapsedMilliseconds + 2500;
            ApplyLook();
            Appear();
        }

        // Blur on or off for the current theme, then a fresh frame.
        // Labels, ticks and icons. Light themes use their solid text colour
        // so everything on the speedometer stays crisp and dark.
        static Color Faint { get { return Theme.Light ? Theme.Text : Theme.Muted; } }

        void ApplyLook()
        {
            cr = Theme.Dial.R; cg = Theme.Dial.G; cb = Theme.Dial.B; // the arc starts in the new theme's colour
            if (!IsHandleCreated) return;
            UpdateBlur();
            Render();
        }

        // ---- visibility -----------------------------------------------------

        public void Appear()
        {
            hideWhenFaded = false;
            opacityTarget = 1;
            if (!Visible)
            {
                opacity = 0;
                if (!IsHandleCreated) CreateHandle();
                Render();   // a clear first frame, so showing it never flashes
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
                target = f >= 0.85 ? Theme.Lerp(Theme.Dial, Theme.Hot, (f - 0.85) / 0.15) : Theme.Dial;
            }
            double ct = 1 - Math.Exp(-4 * dt);
            cr += (target.R - cr) * ct; cg += (target.G - cg) * ct; cb += (target.B - cb) * ct;

            opacity = Step(opacity, opacityTarget, dt * 4.5);
            bool previewing = now < previewUntil;

            bool calm = !engine.Shown && needle < 0.4 && Math.Abs(needleV) < 0.4 &&
                        avg < 0.5 && peak < 0.5 && words < 0.5 && arcShow <= 0 && Math.Abs(dialMax - tMax) < 0.2;

            // "Only when typing": pop out as the dial drops back toward zero.
            bool dropping = !engine.Shown && needle < dialMax * 0.15;
            if ((calm || dropping) && store.OnlyWhenTyping && !hideWhenFaded && Visible && !previewing) Disappear();

            if (hideWhenFaded && opacity <= 0)
            {
                Hide();
                hideWhenFaded = false;
            }

            if (calm && opacity == opacityTarget && !previewing || !Visible)
            {
                needle = needleV = avg = peak = words = live = 0;
                dialMax = tMax;
                cr = Theme.Dial.R; cg = Theme.Dial.G; cb = Theme.Dial.B;
                anim.Stop();
            }
            UpdateBlur();
            Render();
        }

        // ---- painting -----------------------------------------------------------

        // Frames are pushed by Render; a repaint Windows asks for just copies the last one again.
        protected override void OnPaint(PaintEventArgs e) { Blit(); }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        void DrawFrame(Graphics g)
        {
            bool record = engine.RecordPace;
            DrawBackground(g);

            // top-left: exact current speed
            string cur = Theme.Fmt(live);
            Font big = Theme.F(22, true);
            Theme.DrawText(g, cur, big, Theme.Text, 14, 9);
            Theme.DrawText(g, "wpm now", Theme.F(10, Theme.Light), Faint, 14 + Theme.Measure(g, cur, big) + 5, 20);

            DrawPin(g);
            DrawClose(g);
            DrawDial(g);

            // centre: session average
            string a = Theme.Fmt(avg);
            Theme.DrawTextCenter(g, a, Theme.F(32, record), record ? Theme.Orange : Theme.Text, CX, CY - 24);
            Theme.DrawTextCenter(g, "avg wpm", Theme.F(10, false), Faint, CX, CY + 15);

            // bottom-left: session peak, bottom-right: words
            Theme.DrawText(g, "PEAK", Theme.F(Theme.Light ? 10 : 9.5f, true), Faint, 14, H - 40);
            Theme.DrawText(g, Theme.Fmt(peak), Theme.F(17, true), Theme.Text, 13, H - 28);
            Theme.DrawTextRight(g, "WORDS", Theme.F(Theme.Light ? 10 : 9.5f, true), Faint, W - 14, H - 40);
            Theme.DrawTextRight(g, Theme.Num((long)Math.Round(words)), Theme.F(17, true), Theme.Text, W - 13, H - 28);
        }

        // Background at the Typer's chosen opacity. It never goes fully clear (alpha 1 of 255
        // is invisible) so the window still catches the mouse and can be dragged at 0 %.
        void DrawBackground(Graphics g)
        {
            float a = store.FloatBgOpacity / 100f;
            // The whole rectangle is filled: Windows clips it to its own smoothly rounded corners,
            // so there is no second, jagged edge of ours to show.
            RectangleF r = new RectangleF(0, 0, W, H);
            Color tint = Theme.FloatTint;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Max(1, (int)Math.Round(tint.A * a)), tint)))
                g.FillRectangle(b, r);
            if (!Theme.Glass) return;

            // glass: a bevel (lit top edge, shaded inner bottom)
            // that follows Windows' own corners, so the pane looks raised
            double s = 0.5 + 0.5 * a;
            Theme.Bevel(g, Theme.RoundExact(new RectangleF(0.5f, 0.5f, W - 1, H - 1), Corner), r,
                        Color.FromArgb((int)(210 * s), 255, 255, 255), Color.FromArgb((int)(36 * s), 255, 255, 255));
            Theme.Bevel(g, Theme.RoundExact(new RectangleF(1.5f, 1.5f, W - 3, H - 3), Corner - 1), r,
                        Color.FromArgb(0, 0, 0, 0), Color.FromArgb((int)(70 * s), 0, 0, 0));
        }

        // ---- window plumbing -----------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int Left, Right, Top, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct AccentPolicy { public int State, Flags, Color, Animation; }
        [StructLayout(LayoutKind.Sequential)] struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }

        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionData data);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        // The frame is drawn straight into a premultiplied 32-bit DIB and copied to the window
        // as-is, alpha included; Windows composites it over the desktop (or the blur).
        Bitmap canvas;
        IntPtr canvasDC, canvasDib, canvasBits;
        byte[] fadeBuffer;

        void EnsureCanvas(int w, int h)
        {
            if (canvas != null && canvas.Width == w && canvas.Height == h) return;
            FreeCanvas();
            BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
            bi.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
            canvasDC = CreateCompatibleDC(IntPtr.Zero);
            canvasDib = CreateDIBSection(canvasDC, ref bi, 0, out canvasBits, IntPtr.Zero, 0);
            SelectObject(canvasDC, canvasDib);
            canvas = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, canvasBits);
        }

        void FreeCanvas()
        {
            if (canvas != null) { canvas.Dispose(); canvas = null; }
            if (canvasDC != IntPtr.Zero) { DeleteDC(canvasDC); canvasDC = IntPtr.Zero; }
            if (canvasDib != IntPtr.Zero) { DeleteObject(canvasDib); canvasDib = IntPtr.Zero; }
            fadeBuffer = null;
        }

        void Render()
        {
            if (!IsHandleCreated || IsDisposed || Width <= 0 || Height <= 0) return;
            EnsureCanvas(Width, Height);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.Transparent);
                Theme.Prepare(g);
                g.ScaleTransform(S, S);
                float a = store.FloatBgOpacity / 100f;
                Theme.AlphaText = true;
                Theme.TextShadow = a < 0.6f ? (int)(150 * (0.6f - a) / 0.6f) : 0; // keeps text legible on busy backdrops
                if (Theme.Glass) Theme.TextShadow = Math.Max(Theme.TextShadow, Theme.GlassTextShadow); // glass is mostly the desktop: always lift the text
                if (Theme.Glass) Theme.SetGlassFrame(canvasBits, canvas.Width, canvas.Height);
                try { DrawFrame(g); }
                finally { Theme.AlphaText = false; Theme.TextShadow = 0; Theme.SetGlassFrame(IntPtr.Zero, 0, 0); }
            }
            if (opacity < 0.999) Fade();
            Blit();
        }

        // Whole-window fade: every premultiplied channel scales by the same amount.
        void Fade()
        {
            int n = canvas.Width * canvas.Height * 4;
            if (fadeBuffer == null || fadeBuffer.Length != n) fadeBuffer = new byte[n];
            Marshal.Copy(canvasBits, fadeBuffer, 0, n);
            int k = (int)Math.Round(Math.Max(0, opacity) * 256);
            for (int i = 0; i < n; i++) fadeBuffer[i] = (byte)(fadeBuffer[i] * k >> 8);
            Marshal.Copy(fadeBuffer, 0, canvasBits, n);
        }

        void Blit()
        {
            if (canvas == null) return;
            IntPtr dc = GetDC(Handle);
            BitBlt(dc, 0, 0, canvas.Width, canvas.Height, canvasDC, 0, 0, 0x00CC0020); // SRCCOPY
            ReleaseDC(Handle, dc);
        }

        // Windows' own blur behind the glass; the other themes need nothing (see OnHandleCreated).
        // The blur is held back while the window fades so it doesn't pop in or out.
        void UpdateBlur()
        {
            int want = Theme.Glass && store.FloatBgOpacity > 0 && opacity > 0.6 && Visible
                ? 3   // ACCENT_ENABLE_BLURBEHIND
                : 0;  // off: the extended frame alone shows our pixels over the desktop (a gradient accent adds a grey layer)
            if (want == blurState || !IsHandleCreated) return;
            blurState = want;
            AccentPolicy accent = new AccentPolicy { State = want, Flags = 0, Color = 0 };
            int size = Marshal.SizeOf(typeof(AccentPolicy));
            IntPtr mem = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, mem, false);
                CompositionData data = new CompositionData { Attribute = 19, Data = mem, Size = size }; // WCA_ACCENT_POLICY
                SetWindowCompositionAttribute(Handle, ref data);
            }
            catch { } // older Windows: no blur
            finally { Marshal.FreeHGlobal(mem); }
        }

        protected override void OnScaleChanged() { ApplyLook(); }

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
            if (hoverClose) Theme.FillRound(g, Theme.Danger, CloseRect, 7);
            float cx = CloseRect.X + CloseRect.Width / 2, cy = CloseRect.Y + CloseRect.Height / 2;
            using (Pen p = new Pen(hoverClose ? Color.White : Faint, 1.6f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                g.DrawLine(p, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
            }
        }

        void DrawPin(Graphics g)
        {
            bool on = store.Pinned;
            if (on) Theme.FillRound(g, Color.FromArgb(40, Theme.Dial), PinRect, 7);
            else if (hoverPin) Theme.FillRound(g, Theme.CardHover, PinRect, 7);
            Color c = on ? Theme.Dial : (hoverPin ? Theme.Text : Faint);

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
                Render();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverPin || hoverClose) { hoverPin = hoverClose = false; Cursor = Cursors.Default; Render(); }
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
            if (disposing) { anim.Dispose(); FreeCanvas(); }
            base.Dispose(disposing);
        }
    }
}
