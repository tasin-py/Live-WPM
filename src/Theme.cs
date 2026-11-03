using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveWpm
{
    // Colors, fonts and small drawing helpers shared by every window.
    static class Theme
    {
        // Flat 2D palette on the Typer's chosen background (#323437). No gradients, glows or shadows.
        public static readonly Color Bg = Color.FromArgb(50, 52, 55);
        public static readonly Color Card = Color.FromArgb(61, 64, 68);
        public static readonly Color CardHover = Color.FromArgb(72, 75, 80);
        public static readonly Color Border = Color.FromArgb(78, 81, 87);
        public static readonly Color Track = Color.FromArgb(78, 81, 87);
        public static readonly Color Text = Color.FromArgb(240, 241, 243);
        public static readonly Color Muted = Color.FromArgb(165, 169, 177);
        public static readonly Color Accent = Color.FromArgb(56, 214, 255);
        public static readonly Color Red = Color.FromArgb(255, 59, 59);
        public static readonly Color Orange = Color.FromArgb(255, 149, 0);
        public static readonly Color Off = Color.FromArgb(132, 136, 144);

        public const string AppName = "Live WPM";

        static float scale;
        public static float Scale
        {
            get
            {
                if (scale == 0)
                    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
                return scale;
            }
        }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        // ---- app font: TBJ Neuetra Sans ---------------------------------------
        // The .ttf files in src\fonts are built into the exe, so the font works on any PC.
        // An installed copy is used if there is one; Segoe UI is the fallback.
        const string FontName = "TBJ Neuetra Sans";
        static readonly System.Drawing.Text.PrivateFontCollection privateFonts = new System.Drawing.Text.PrivateFontCollection();
        static FontFamily regular, bold;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr data, uint len, IntPtr reserved, ref uint fonts);

        static void LoadFonts()
        {
            if (regular != null) return;
            try
            {
                System.Reflection.Assembly asm = typeof(Theme).Assembly;
                foreach (string res in asm.GetManifestResourceNames())
                {
                    string lower = res.ToLowerInvariant();
                    if (!lower.EndsWith(".ttf") && !lower.EndsWith(".otf")) continue;
                    byte[] data;
                    using (System.IO.Stream st = asm.GetManifestResourceStream(res))
                    {
                        data = new byte[st.Length];
                        st.Read(data, 0, data.Length);
                    }
                    IntPtr mem = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(data.Length); // kept for the app's lifetime
                    System.Runtime.InteropServices.Marshal.Copy(data, 0, mem, data.Length);
                    try { privateFonts.AddMemoryFont(mem, data.Length); } catch { } // GDI+ can't read every .otf
                    uint n = 0;
                    AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref n); // so text boxes can use it too
                }
            }
            catch { }

            // The family may be one "TBJ Neuetra Sans" or split per weight ("... Bold").
            List<FontFamily> all = new List<FontFamily>(privateFonts.Families);
            using (System.Drawing.Text.InstalledFontCollection installed = new System.Drawing.Text.InstalledFontCollection())
                all.AddRange(installed.Families);
            foreach (FontFamily fam in all)
            {
                string n = fam.Name;
                if (n.IndexOf("Neuetra", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool isBold = n.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isBold && regular == null && fam.IsStyleAvailable(FontStyle.Regular)) regular = fam;
                if (isBold && bold == null) bold = fam;
            }
            if (regular == null) regular = new FontFamily("Segoe UI");
            if (bold == null) bold = regular;
        }

        public static bool HasAppFont { get { LoadFonts(); return regular.Name.IndexOf("Neuetra", StringComparison.OrdinalIgnoreCase) >= 0; } }

        public static Font F(float size, bool isBold)
        {
            string key = size.ToString(System.Globalization.CultureInfo.InvariantCulture) + (isBold ? "b" : "");
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                LoadFonts();
                FontFamily fam = isBold ? bold : regular;
                FontStyle style = isBold && fam == regular ? FontStyle.Bold : FontStyle.Regular;
                if (!fam.IsStyleAvailable(style)) style = fam.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
                f = new Font(fam, size, style, GraphicsUnit.Pixel);
                fonts[key] = f;
            }
            return f;
        }

        public static Color Lerp(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float rad)
        {
            using (GraphicsPath p = Round(r, rad))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color c, RectangleF r, float rad, float w)
        {
            using (GraphicsPath p = Round(r, rad))
            using (Pen pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        public static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;   // true pixel-coverage edges on shapes
            g.CompositingQuality = CompositingQuality.HighQuality;
        }

        // ---- text ---------------------------------------------------------------
        // Text goes through Windows' own renderer (GDI ClearType, the same one Explorer and
        // Settings use): letters are hinted to the pixel grid as whole words, so they stay sharp
        // and evenly spaced. GDI ignores scaling, so each call maps the logical position and
        // font size to real device pixels first.

        const TextFormatFlags Tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                                   TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping;
        static readonly Dictionary<string, Font> deviceFonts = new Dictionary<string, Font>();

        static Font DeviceFont(Font f, float scale)
        {
            if (Math.Abs(scale - 1) < 0.001f) return f;
            string key = f.Name + "|" + f.Size + "|" + (int)f.Style + "|" + scale;
            Font d;
            if (!deviceFonts.TryGetValue(key, out d))
            {
                d = new Font(f.FontFamily, f.Size * scale, f.Style, GraphicsUnit.Pixel);
                deviceFonts[key] = d;
            }
            return d;
        }

        static float ScaleOf(Graphics g)
        {
            using (Matrix m = g.Transform) return m.Elements[0];
        }

        // GDI can't blend text colour with transparency, so a faded colour is mixed with the background.
        static Color Solid(Color c)
        {
            return c.A == 255 ? c : Lerp(Bg, Color.FromArgb(255, c), c.A / 255.0);
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, float x, float y)
        {
            PointF[] p = { new PointF(x, y) };
            float scale;
            using (Matrix m = g.Transform) { scale = m.Elements[0]; m.TransformPoints(p); }
            GraphicsState st = g.Save();
            g.ResetTransform();
            TextRenderer.DrawText(g, s, DeviceFont(f, scale),
                new Point((int)Math.Round(p[0].X), (int)Math.Round(p[0].Y)), Solid(c), Tf);
            g.Restore(st);
        }

        public static void DrawTextRight(Graphics g, string s, Font f, Color c, float right, float y)
        {
            DrawText(g, s, f, c, right - Measure(g, s, f), y);
        }

        public static void DrawTextCenter(Graphics g, string s, Font f, Color c, float cx, float y)
        {
            DrawText(g, s, f, c, cx - Measure(g, s, f) / 2, y);
        }

        public static float Measure(Graphics g, string s, Font f)
        {
            float scale = ScaleOf(g);
            return TextRenderer.MeasureText(g, s, DeviceFont(f, scale), Size.Empty, Tf).Width / scale;
        }

        // ---- logo / icons -------------------------------------------------

        public static Bitmap Logo(int size, Color accent)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float s = size;
                FillRound(g, Bg, new RectangleF(0, 0, s, s), s * 0.22f);
                float w = Math.Max(1.6f, s * 0.13f);
                RectangleF arc = new RectangleF(s * 0.17f, s * 0.2f, s * 0.66f, s * 0.66f);
                using (Pen track = new Pen(Color.FromArgb(95, 99, 106), w))
                {
                    track.StartCap = LineCap.Round; track.EndCap = LineCap.Round;
                    g.DrawArc(track, arc, 135, 270);
                }
                using (Pen p = new Pen(accent, w))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawArc(p, arc, 135, 190);
                }
                float cx = arc.X + arc.Width / 2, cy = arc.Y + arc.Height / 2;
                double a = (135 + 190) * Math.PI / 180;
                using (Pen n = new Pen(Color.White, Math.Max(1.4f, s * 0.08f)))
                {
                    n.StartCap = LineCap.Round; n.EndCap = LineCap.Round;
                    g.DrawLine(n, cx, cy, cx + (float)Math.Cos(a) * arc.Width * 0.36f, cy + (float)Math.Sin(a) * arc.Width * 0.36f);
                }
                using (SolidBrush hub = new SolidBrush(Color.White))
                    g.FillEllipse(hub, cx - s * 0.06f, cy - s * 0.06f, s * 0.12f, s * 0.12f);
            }
            return bmp;
        }

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr h);

        public static Icon MakeIcon(int size, Color accent)
        {
            using (Bitmap b = Logo(size, accent))
            {
                IntPtr h = b.GetHicon();
                Icon tmp = Icon.FromHandle(h);
                Icon copy = (Icon)tmp.Clone();
                tmp.Dispose();
                DestroyIcon(h);
                return copy;
            }
        }

        // Writes a multi-size PNG-compressed .ico (used by build.ps1 for the exe icon).
        public static void SaveIco(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 256 };
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in sizes)
            {
                using (Bitmap b = Logo(s, Accent))
                using (MemoryStream ms = new MemoryStream())
                {
                    b.Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }
            }
            using (FileStream fs = File.Create(path))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) w.Write(p);
            }
        }

        // ---- window chrome --------------------------------------------------

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void DarkTitleBar(IntPtr hwnd)
        {
            try
            {
                int on = 1;
                DwmSetWindowAttribute(hwnd, 20, ref on, 4);
                int caption = Bg.R | (Bg.G << 8) | (Bg.B << 16); // title bar matches the background (Win 11)
                DwmSetWindowAttribute(hwnd, 35, ref caption, 4);
            }
            catch { }
        }

        public static void RoundCorners(IntPtr hwnd)
        {
            try { int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, 4); } catch { }
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        public static void BeginDrag(IntPtr hwnd)
        {
            ReleaseCapture();
            SendMessage(hwnd, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
        }

        [DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        // Hands unused memory back to Windows so the tray app stays tiny while idle.
        public static void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }

        public static string Fmt(double v)
        {
            return Math.Round(v).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Num(long v)
        {
            return v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // A form that never flickers when we paint it ourselves.
    class BufferedForm : Form
    {
        public BufferedForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            AutoScaleMode = AutoScaleMode.None;
        }

        // Logical-to-device pixel factor. The app is per-monitor DPI aware (see app.manifest),
        // so each window draws itself at its monitor's real resolution instead of being stretched.
        protected float S = Theme.Scale;
        float logicalW, logicalH;

        [DllImport("user32.dll")]
        static extern uint GetDpiForWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        protected void SetLogicalSize(float w, float h)
        {
            logicalW = w; logicalH = h;
            ClientSize = new Size((int)Math.Round(w * S), (int)Math.Round(h * S));
        }

        // Called after S changes (window created on, or moved to, a monitor with another scale).
        protected virtual void OnScaleChanged() { }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                uint dpi = GetDpiForWindow(Handle);
                if (dpi > 0 && Math.Abs(dpi / 96f - S) > 0.001f) Rescale(dpi / 96f, null);
            }
            catch { }
        }

        void Rescale(float scale, Rectangle? suggested)
        {
            S = scale;
            if (suggested.HasValue) Bounds = suggested.Value;
            if (logicalW > 0) ClientSize = new Size((int)Math.Round(logicalW * S), (int)Math.Round(logicalH * S));
            OnScaleChanged();
            Invalidate(true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x02E0) // WM_DPICHANGED
            {
                RECT r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                Rescale(((int)m.WParam & 0xFFFF) / 96f, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
