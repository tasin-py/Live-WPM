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
        public static readonly Color Red = Color.FromArgb(255, 59, 59);
        public static readonly Color Off = Color.FromArgb(132, 136, 144);
        public static readonly Color Brand = Color.FromArgb(56, 214, 255);   // tray icon, the same in every theme

        // Set by Apply from the chosen palette: every window reads these while painting,
        // so a theme change only needs a repaint.
        public static Color Bg, Card, CardHover, Border, Track, Text, Muted, Accent, OnAccent, Dial,
                            Hot, Orange, Danger, DangerText, Outline, FloatTint, LogoAccent;
        public static bool Glass, Light, Cute;
        public static Palette Current;

        public const string DefaultTheme = "tasin", GlassTheme = "glass";
        public const string AppName = "Live WPM";

        // In the order the theme picker shows them.
        public static readonly Palette[] Palettes = {
            // The original look: flat 2D on #323437, no gradients, glows or shadows.
            new Palette {
                Id = "tasin", Name = "Average Tasin",
                Bg = C(50, 52, 55), Card = C(61, 64, 68), CardHover = C(72, 75, 80), Border = C(78, 81, 87), Track = C(78, 81, 87),
                Text = C(240, 241, 243), Muted = C(165, 169, 177), Accent = C(56, 214, 255), OnAccent = C(8, 18, 26) },

            // Soft blush, white cards, deep plum text, extra-round corners and little hearts. Kept minimal.
            new Palette {
                Id = "tashhid", Name = "Beverage Tashhid", Light = true, Cute = true, Radius = 1.4f,
                Bg = C(255, 243, 247), Card = C(255, 255, 255), CardHover = C(255, 230, 240), Border = C(246, 205, 222), Track = C(250, 216, 230),
                Text = C(58, 18, 40), Muted = C(122, 66, 96), Accent = C(232, 82, 146), OnAccent = C(255, 255, 255),
                Hot = C(200, 30, 110), Orange = C(240, 110, 70), DangerText = C(196, 30, 84), Outline = A(90, 240, 150, 188) },

            // Premium black and gold: near-black surfaces, a metallic #D5A149 accent and a gold-lit edge on every card.
            // The speedometer keeps its own sky-blue dial.
            new Palette {
                Id = "ahnaf", Name = "Ahnaf Dark",
                Bg = C(11, 11, 12), Card = C(21, 21, 23), CardHover = C(31, 30, 32), Border = C(40, 38, 40), Track = C(44, 42, 44),
                Text = C(240, 238, 235), Muted = C(150, 145, 138), Accent = C(213, 161, 73), OnAccent = C(26, 17, 4),
                Hot = C(240, 203, 128), Orange = C(240, 203, 128), Dial = C(77, 196, 255), Shiny = true },

            // Midnight blue with a soft periwinkle accent and warm gold for records.
            new Palette {
                Id = "makam", Name = "Makam Blue",
                Bg = C(12, 19, 38), Card = C(20, 31, 58), CardHover = C(28, 42, 75), Border = C(37, 52, 88), Track = C(38, 54, 92),
                Text = C(232, 238, 252), Muted = C(139, 156, 196), Accent = C(116, 166, 255), OnAccent = C(8, 14, 32),
                Orange = C(255, 196, 87), Outline = A(18, 150, 180, 255) },

            // Minimal and dark: black surfaces, and everything on them white or a grey between. No other colour.
            new Palette {
                Id = "ziraf", Name = "Ziraf Minimal",
                Bg = C(8, 8, 8), Card = C(20, 20, 20), CardHover = C(36, 36, 36), Border = C(52, 52, 52), Track = C(58, 58, 58),
                Text = C(255, 255, 255), Muted = C(176, 176, 176), Accent = C(255, 255, 255), OnAccent = C(0, 0, 0),
                Hot = C(255, 255, 255), Orange = C(255, 255, 255), Danger = C(92, 92, 92), DangerText = C(255, 255, 255),
                Outline = A(26, 255, 255, 255), LogoAccent = C(255, 255, 255) },

            // Deep wine-black with a crimson accent; near the top of the dial the arc turns gold.
            new Palette {
                Id = "adib", Name = "Adib Red",
                Bg = C(23, 15, 17), Card = C(36, 23, 27), CardHover = C(48, 31, 36), Border = C(62, 40, 46), Track = C(64, 42, 48),
                Text = C(248, 238, 239), Muted = C(181, 151, 157), Accent = C(255, 76, 92), OnAccent = C(255, 255, 255),
                Hot = C(255, 196, 77), Orange = C(255, 160, 60), DangerText = C(255, 140, 150), Outline = A(16, 255, 120, 130) },

            // Deep sky-blue glass: raised see-through panes with a lit edge and a soft shadow,
            // no colour washes. The floating window is mostly the blurred desktop with a light sky tint.
            new Palette {
                Id = "glass", Name = "Safir Glass", Glass = true,
                Bg = C(0, 72, 112), Card = A(8, 255, 255, 255), CardHover = A(24, 255, 255, 255), Border = A(60, 255, 255, 255), Track = A(64, 255, 255, 255),
                Text = C(255, 255, 255), Muted = C(226, 241, 250), Accent = C(170, 230, 255), OnAccent = C(0, 50, 80),
                Dial = C(255, 255, 255), FloatTint = A(110, 0, 92, 146) },
        };

        // The Typer's own themes, loaded from files by ThemeFiles. The picker shows them after the built-in ones.
        public static readonly List<Palette> Custom = new List<Palette>();
        public static int ThemeCount { get { return Palettes.Length + Custom.Count; } }
        public static Palette ThemeAt(int i) { return i < Palettes.Length ? Palettes[i] : Custom[i - Palettes.Length]; }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        static Color A(int a, int r, int g, int b) { return Color.FromArgb(a, r, g, b); }

        static Theme() { Apply(DefaultTheme); }

        public static Palette Find(string id)
        {
            if (id == "flat") id = DefaultTheme; // name used by 1.1.0
            foreach (Palette p in Palettes) if (p.Id == id) return p;
            foreach (Palette p in Custom) if (p.Id == id) return p;
            return null;
        }

        public static void Apply(string id)
        {
            Palette p = Find(id) ?? Palettes[0];
            Current = p;
            Bg = p.Bg; Card = p.Card; CardHover = p.CardHover; Border = p.Border; Track = p.Track;
            Text = p.Text; Muted = p.Muted; Accent = p.Accent; OnAccent = p.OnAccent;
            Dial = p.DialColor;
            Hot = p.Hot.IsEmpty ? Red : p.Hot;
            Orange = p.Orange.IsEmpty ? Color.FromArgb(255, 149, 0) : p.Orange;
            Danger = p.Danger.IsEmpty ? Red : p.Danger;
            DangerText = p.DangerText.IsEmpty ? Color.FromArgb(255, 110, 110) : p.DangerText;
            Outline = p.Outline;
            FloatTint = p.FloatTint.IsEmpty ? p.Bg : p.FloatTint;
            LogoAccent = p.LogoAccent.IsEmpty ? p.Accent : p.LogoAccent;
            Glass = p.Glass; Light = p.Light; Cute = p.Cute;
            radiusScale = p.Radius;
        }

        // Card colour without transparency, for Windows controls that can't blend (text boxes).
        public static Color CardSolid { get { return Solid(Card); } }

        // Window background. Glass lays soft colour behind its see-through cards so they read as glass.
        public static void PaintBackground(Graphics g, float w, float h)
        {
            PaintBackground(g, Current, new RectangleF(0, 0, w, h));
        }

        public static void PaintBackground(Graphics g, Palette p, RectangleF r)
        {
            using (SolidBrush b = new SolidBrush(p.Bg)) g.FillRectangle(b, r);
            if (p.Blobs == null) return;
            float w = r.Width, h = r.Height;
            Blob(g, r.X + w * 0.10f, r.Y + h * 0.05f, w * 0.62f, p.Blobs[0]);
            Blob(g, r.X + w * 0.98f, r.Y + h * 0.95f, w * 0.62f, p.Blobs[1]);
            Blob(g, r.X + w * 0.78f, r.Y + h * 0.20f, w * 0.38f, p.Blobs[2]);
        }

        static void Blob(Graphics g, float cx, float cy, float r, Color c)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (PathGradientBrush b = new PathGradientBrush(p))
                {
                    b.CenterColor = c;
                    b.SurroundColors = new[] { Color.FromArgb(0, c) };
                    g.FillPath(b, p);
                }
            }
        }

        // A card or panel: fill, plus the theme's hairline edge, or a raised glass pane.
        public static void Surface(Graphics g, Color c, RectangleF r, float rad)
        {
            if (Glass) { GlassPane(g, c, r, rad); return; }
            FillRound(g, c, r, rad);
            if (Current.Shiny)
                Bevel(g, Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad), r,
                      Color.FromArgb(120, 236, 202, 132), Color.FromArgb(16, 213, 161, 73));
            else if (Outline.A > 0) StrokeRound(g, Outline, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad, 1);
        }

        // A pane of glass that sits above the page: a soft shadow beneath it (outside the pane only,
        // so it never muddies the see-through fill), the tinted fill, and a bevel - lit along the
        // top edge, shaded along the bottom.
        static void GlassPane(Graphics g, Color c, RectangleF r, float rad)
        {
            using (GraphicsPath pane = Round(r, rad))
            {
                GraphicsState st = g.Save();
                g.SetClip(pane, CombineMode.Exclude);
                for (int i = 1; i <= 4; i++)
                    FillRound(g, Color.FromArgb(Math.Max(1, 10 - i * 2), 0, 0, 0),
                              new RectangleF(r.X - i * 0.5f, r.Y + i * 1.1f, r.Width + i, r.Height + i * 0.7f), rad + i * 0.5f);
                g.Restore(st);

                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, pane);

            }
            Rim(g, r, rad, 1);
        }

        // Light across the upper half of a pane, fading out by the middle.
        public static void Sheen(Graphics g, RectangleF r, int alpha)
        {
            float h = Math.Max(1, r.Height * 0.5f);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, h + 2),
                       Color.FromArgb(alpha, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
            {
                b.WrapMode = WrapMode.TileFlipXY; // a plain Tile repeats the bright start as a line
                g.FillRectangle(b, r.X, r.Y, r.Width, h);
            }
        }

        // Glass bevel: the outer edge is lit at the top and fades down; just inside it, a shade
        // grows toward the bottom. Together they read as a raised, rounded pane.
        public static void Rim(Graphics g, RectangleF r, float rad, double strength)
        {
            Bevel(g, Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad), r,
                  Color.FromArgb((int)(190 * strength), 255, 255, 255), Color.FromArgb((int)(30 * strength), 255, 255, 255));
            Bevel(g, Round(new RectangleF(r.X + 1.5f, r.Y + 1.5f, r.Width - 3, r.Height - 3), Math.Max(0, rad - 1)), r,
                  Color.FromArgb(0, 0, 0, 0), Color.FromArgb((int)(60 * strength), 0, 0, 0));
        }

        public static void Bevel(Graphics g, GraphicsPath p, RectangleF r, Color top, Color bottom)
        {
            using (p)
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 2, r.Width, r.Height + 4), top, bottom, 90f))
            using (Pen pen = new Pen(b, 1))
            {
                b.WrapMode = WrapMode.TileFlipXY;
                g.DrawPath(pen, p);
            }
        }

        // A small heart (the pink theme's separators).
        public static void Heart(Graphics g, Color c, float cx, float cy, float size)
        {
            float s = size / 2;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(cx, cy + s, cx - s * 1.6f, cy - s * 0.1f, cx - s * 0.9f, cy - s * 1.4f, cx, cy - s * 0.45f);
                p.AddBezier(cx, cy - s * 0.45f, cx + s * 0.9f, cy - s * 1.4f, cx + s * 1.6f, cy - s * 0.1f, cx, cy + s);
                p.CloseFigure();
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
            }
        }

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

        static float radiusScale = 1;

        // Rounded rectangle; corners follow the theme (the pink theme is extra round).
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            return RoundExact(r, Math.Min(rad * radiusScale, Math.Min(r.Width, r.Height) / 2));
        }

        // Rounded rectangle with exactly this radius (to match a shape Windows draws).
        public static GraphicsPath RoundExact(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Max(0.01f, rad * 2);
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
            // HighQuality blends in linear light, which makes the glass theme's faint white layers
            // far brighter than their alpha says; glass blends plainly so they stay see-through.
            g.CompositingQuality = Glass ? CompositingQuality.AssumeLinear : CompositingQuality.HighQuality;
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

        // Set while painting a see-through surface (the floating window): GDI text would punch holes
        // in it, so text is drawn with GDI+ instead, with an optional faint shadow for busy backdrops.
        public static bool AlphaText;
        public const int GlassTextShadow = 55; // strength of the dark edge that lifts glass text off a bright desktop
        public static int TextShadow;
        static readonly StringFormat Typo = StringFormat.GenericTypographic;

        // ---- sharp text on glass ---------------------------------------------------
        // GDI+ only smooths text in greyscale, which looks soft next to the ClearType in every other
        // theme. So glass text is drawn by GDI on a private black bitmap, white on black, which leaves
        // ClearType's coverage in each colour channel, and that is then blended into the see-through
        // frame one channel at a time. Finished runs are kept: most text repeats frame after frame.
        sealed class TextRun { public int Width, W, H; public byte[] Cover, Edge; }
        const int RunPad = 2;
        static readonly Dictionary<string, TextRun> runs = new Dictionary<string, TextRun>();
        static IntPtr glassBits;
        static int glassW, glassH, rowCap;
        static int[] rowBuf = new int[0];

        // The premultiplied frame being painted in the glass theme (IntPtr.Zero when done).
        public static void SetGlassFrame(IntPtr bits, int w, int h) { glassBits = bits; glassW = w; glassH = h; }

        static TextRun GetRun(string s, Font df)
        {
            string key = s + "|" + df.Name + "|" + df.Size + "|" + (int)df.Style + "|" + (int)df.Unit;
            TextRun run;
            if (runs.TryGetValue(key, out run)) return run;
            if (runs.Count > 800) runs.Clear();

            Size sz = TextRenderer.MeasureText(s, df, Size.Empty, Tf);
            run = new TextRun { W = sz.Width + RunPad * 2, H = sz.Height + RunPad * 2 };
            int w = run.W, h = run.H;
            byte[] raw = new byte[w * h * 4];
            using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb))
            {
                using (Graphics bg = Graphics.FromImage(bmp))
                {
                    bg.Clear(Color.Black);
                    TextRenderer.DrawText(bg, s, df, new Point(RunPad, RunPad), Color.White, Color.Black, Tf);
                }
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                for (int y = 0; y < h; y++) Marshal.Copy(data.Scan0 + y * data.Stride, raw, y * w * 4, w * 4);
                bmp.UnlockBits(data);
            }

            run.Cover = new byte[w * h * 3];
            byte[] most = new byte[w * h];
            for (int i = 0; i < w * h; i++)
            {
                byte b = raw[i * 4], gn = raw[i * 4 + 1], r = raw[i * 4 + 2];
                run.Cover[i * 3] = b; run.Cover[i * 3 + 1] = gn; run.Cover[i * 3 + 2] = r;
                most[i] = Math.Max(b, Math.Max(gn, r));
                // width to the last inked pixel: GDI's own measure runs long on big digits, which would push their neighbours aside
                if (most[i] > 0) run.Width = Math.Max(run.Width, i % w + 1 - RunPad);
            }
            // The dark edge: each letter's outline grown by a pixel, fading out, so there is no offset copy to read as a double.
            run.Edge = new byte[w * h];
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    int near = Math.Max(Math.Max(most[i - 1], most[i + 1]), Math.Max(most[i - w], most[i + w]));
                    int far = Math.Max(Math.Max(most[i - w - 1], most[i - w + 1]), Math.Max(most[i + w - 1], most[i + w + 1]));
                    run.Edge[i] = (byte)Math.Max(most[i], Math.Max(near * 3 / 5, far * 3 / 10));
                }
            runs[key] = run;
            return run;
        }

        static void DrawGlassText(Graphics g, string s, Font df, Color c, int px, int py)
        {
            g.Flush(FlushIntention.Sync);
            TextRun run = GetRun(s, df);
            int ox = px - RunPad, oy = py - RunPad;
            RectangleF clip = g.ClipBounds;
            int left = Math.Max(Math.Max(ox, 0), (int)Math.Max(0, Math.Ceiling(clip.Left)));
            int top = Math.Max(Math.Max(oy, 0), (int)Math.Max(0, Math.Ceiling(clip.Top)));
            int right = Math.Min(Math.Min(ox + run.W, glassW), (int)Math.Min(glassW, Math.Floor(clip.Right)));
            int bottom = Math.Min(Math.Min(oy + run.H, glassH), (int)Math.Min(glassH, Math.Floor(clip.Bottom)));
            int n = right - left;
            if (n <= 0 || bottom <= top) return;
            if (n > rowCap) { rowCap = n; rowBuf = new int[n]; }
            int ca = c.A, edge = TextShadow * ca / 255;

            for (int y = top; y < bottom; y++)
            {
                IntPtr row = glassBits + (y * glassW + left) * 4;
                Marshal.Copy(row, rowBuf, 0, n);
                int at = (y - oy) * run.W + (left - ox);
                for (int k = 0; k < n; k++, at++)
                {
                    int e = run.Edge[at] * edge / 255;
                    int cb = run.Cover[at * 3], cg = run.Cover[at * 3 + 1], cr = run.Cover[at * 3 + 2];
                    if (e == 0 && (cb | cg | cr) == 0) continue;
                    int d = rowBuf[k];
                    int da = (d >> 24) & 255, dr = (d >> 16) & 255, dg = (d >> 8) & 255, db = d & 255;
                    // the edge darkens what is behind...
                    da += (255 - da) * e / 255; dr -= dr * e / 255; dg -= dg * e / 255; db -= db * e / 255;
                    // ...and the letter goes on top, each channel by its own coverage
                    cb = cb * ca / 255; cg = cg * ca / 255; cr = cr * ca / 255;
                    int most = Math.Max(cb, Math.Max(cg, cr));
                    da += (255 - da) * most / 255;
                    dr = Math.Min(da, dr + (c.R - dr) * cr / 255);
                    dg = Math.Min(da, dg + (c.G - dg) * cg / 255);
                    db = Math.Min(da, db + (c.B - db) * cb / 255);
                    rowBuf[k] = (da << 24) | (dr << 16) | (dg << 8) | db;
                }
                Marshal.Copy(rowBuf, 0, row, n);
            }
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, float x, float y)
        {
            PointF[] p = { new PointF(x, y) };
            float scale;
            using (Matrix m = g.Transform) { scale = m.Elements[0]; m.TransformPoints(p); }
            GraphicsState st = g.Save();
            g.ResetTransform();
            bool gold = Current.Shiny && (c.ToArgb() == Accent.ToArgb() || c.ToArgb() == Orange.ToArgb());
            if (glassBits != IntPtr.Zero)
            {
                if (s.Length > 0) DrawGlassText(g, s, DeviceFont(f, scale), c, (int)Math.Round(p[0].X), (int)Math.Round(p[0].Y));
                g.Restore(st);
                return;
            }
            if (AlphaText || gold)
            {
                // Whole device pixels and a font at device size: glyphs stay on the pixel grid, so they stay sharp.
                Font df = DeviceFont(f, scale);
                float px = (float)Math.Round(p[0].X), py = (float)Math.Round(p[0].Y);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.TextContrast = 1;
                if (TextShadow > 0)
                    using (SolidBrush sh = new SolidBrush(Light ? Color.FromArgb(TextShadow * c.A / 255, 255, 255, 255)   // a soft glow behind dark text
                                                                : Color.FromArgb(TextShadow * c.A / 255, 0, 0, 0)))
                        g.DrawString(s, df, sh, px, py + 1, Typo);
                using (Brush b = gold ? GoldBrush(new RectangleF(px, py, Math.Max(1, df.Height), df.Height)) : (Brush)new SolidBrush(c))
                    g.DrawString(s, df, b, px, py, Typo);
                g.Restore(st);
                return;
            }
            TextRenderer.DrawText(g, s, DeviceFont(f, scale),
                new Point((int)Math.Round(p[0].X), (int)Math.Round(p[0].Y)), Solid(c), Tf);
            g.Restore(st);
        }

        // Polished metal: a bright highlight at the top, the theme's gold in the middle, a deeper shade below.
        static Brush GoldBrush(RectangleF r)
        {
            LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Accent, Accent, 90f);
            ColorBlend blend = new ColorBlend(3);
            blend.Colors = new[] { Color.FromArgb(252, 233, 182), Accent, Color.FromArgb(160, 113, 40) };
            blend.Positions = new[] { 0f, 0.5f, 1f };
            b.InterpolationColors = blend;
            b.WrapMode = WrapMode.TileFlipXY;
            return b;
        }

        // A filled accent shape: the theme's accent, as polished metal in a shiny theme.
        public static void FillAccent(Graphics g, RectangleF r, float rad)
        {
            if (!Current.Shiny) { FillRound(g, Accent, r, rad); return; }
            using (GraphicsPath path = Round(r, rad))
            using (Brush b = GoldBrush(r))
                g.FillPath(b, path);
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
            if (glassBits != IntPtr.Zero) return GetRun(s, DeviceFont(f, scale)).Width / scale;
            if (AlphaText) return g.MeasureString(s, f, PointF.Empty, Typo).Width;
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
                using (GraphicsPath bgp = RoundExact(new RectangleF(0, 0, s, s), s * 0.22f)) using (SolidBrush bgb = new SolidBrush(Color.FromArgb(50, 52, 55))) g.FillPath(bgb, bgp); // brand tile, same in every theme
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
                using (Bitmap b = Logo(s, Brand))
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
                int on = Light ? 0 : 1; // dark or light title bar to match the theme
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

    // One colour theme. Empty optional colours fall back to the defaults in Theme.Apply.
    sealed class Palette
    {
        public string Id, Name;
        public string SourceFile;  // where one of the Typer's own themes is saved; null for a built-in theme
        public Color Bg, Card, CardHover, Border, Track, Text, Muted, Accent, OnAccent;
        public Color Dial, Hot, Orange, Danger, DangerText, Outline, FloatTint, LogoAccent;

        public Color DialColor { get { return Dial.IsEmpty ? Accent : Dial; } }   // the speedometer's arc
        public Color[] Blobs;      // soft colour behind see-through surfaces (glass)
        public bool Light, Glass, Cute, Shiny;
        public float Radius = 1;   // corner roundness
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

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Theme.PaintBackground(e.Graphics, ClientSize.Width, ClientSize.Height);
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
