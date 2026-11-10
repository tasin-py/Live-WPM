using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // The Linux half of Theme: colours in use, fonts and drawing helpers. The theme list itself
    // is shared with Windows (src/Palettes.cs). Ported from src/Theme.cs.
    static partial class Theme
    {
        public static readonly Color Red = Color.FromArgb(255, 59, 59);
        public static readonly Color Off = Color.FromArgb(132, 136, 144);
        public static readonly Color Brand = Color.FromArgb(56, 214, 255);

        public static Color Bg, Card, CardHover, Border, Track, Text, Muted, Accent, OnAccent, Dial,
                            Hot, Orange, Danger, DangerText, Outline, FloatTint, LogoAccent;
        public static bool Glass, Light, Cute;
        public static Palette Current;
        static float radiusScale = 1;

        // Avalonia works in logical pixels and scales for the display itself.
        public const float Scale = 1;

        static Theme() { Apply(DefaultTheme); }

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

        public static Color CardSolid { get { return Solid(Card); } }

        static Color Solid(Color c) { return c.A == 255 ? c : Lerp(Bg, Color.FromArgb(255, c), c.A / 255.0); }

        // On Windows the glass main window is its tint over black; Linux paints that same colour.
        public static Color GlassBase { get { return Lerp(Color.Black, Color.FromArgb(255, FloatTint), FloatTint.A / 255.0); } }

        public static void Prepare(Graphics g) { }
        public static void TrimMemory() { }

        public static void PaintBackground(Graphics g, float w, float h) { PaintBackground(g, Current, new RectangleF(0, 0, w, h)); }

        public static void PaintBackground(Graphics g, Palette p, RectangleF r)
        {
            using (SolidBrush b = new SolidBrush(p.Bg)) g.FillRectangle(b, r);
        }

        public static void Surface(Graphics g, Color c, RectangleF r, float rad)
        {
            if (Glass) { GlassPane(g, c, r, rad); return; }
            FillRound(g, c, r, rad);
            if (Current.Shiny)
                Bevel(g, Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad), r,
                      Color.FromArgb(120, 236, 202, 132), Color.FromArgb(16, 213, 161, 73));
            else if (Outline.A > 0) StrokeRound(g, Outline, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad, 1);
        }

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
                g.DrawPath(pen, p);
        }

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

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            return RoundExact(r, Math.Min(rad * radiusScale, Math.Min(r.Width, r.Height) / 2));
        }

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

        // ---- text: Inter, built into the app, so it looks the same on every distro ----

        static readonly FontFamily family = new FontFamily("fonts:Inter#Inter");
        static readonly Typeface regular = new Typeface(family, FontStyle.Normal, FontWeight.Normal);
        static readonly Typeface bold = new Typeface(family, FontStyle.Normal, FontWeight.Bold);
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        public const string FontName = "Inter";

        public static Font F(float size, bool isBold)
        {
            string key = size.ToString(CultureInfo.InvariantCulture) + (isBold ? "b" : "");
            Font f;
            if (!fonts.TryGetValue(key, out f)) { f = new Font(size, isBold); fonts[key] = f; }
            return f;
        }

        // Segoe UI's line spacing, so layouts made for Windows space out the same.
        public static float LineHeight(Font f) { return f.Size * 1.33f; }

        public static bool AlphaText;
        public const int GlassTextShadow = 55;
        public static int TextShadow;

        static FormattedText Make(string s, Font f, IBrush b)
        {
            return new FormattedText(s ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, f.Bold ? bold : regular, f.Size, b);
        }

        // Windows draws a line of text with its baseline about 1.08 em below the given top (Segoe UI);
        // Inter's baseline sits a little higher, so text is nudged down to land on the same line.
        static double Top(FormattedText ft, Font f, float y) { return y + f.Size * 1.077 - ft.Baseline; }

        public static void DrawText(Graphics g, string s, Font f, Color c, float x, float y)
        {
            if (string.IsNullOrEmpty(s)) return;
            bool gold = Current.Shiny && (c.ToArgb() == Accent.ToArgb() || c.ToArgb() == Orange.ToArgb());
            if (TextShadow > 0)
            {
                Color sh = Light ? Color.FromArgb(TextShadow * c.A / 255, 255, 255, 255) : Color.FromArgb(TextShadow * c.A / 255, 0, 0, 0);
                FormattedText shadow = Make(s, f, new ImmutableSolidColorBrush(Conv.C(sh)));
                g.Dc.DrawText(shadow, new Point(x, Top(shadow, f, y) + 1));
            }
            IBrush brush = gold ? GoldBrush() : new ImmutableSolidColorBrush(Conv.C(c));
            FormattedText ft = Make(s, f, brush);
            g.Dc.DrawText(ft, new Point(x, Top(ft, f, y)));
        }

        // Several lines, wrapped to a width. Returns the height used.
        public static float DrawTextWrapped(Graphics g, string s, Font f, Color c, float x, float y, float width)
        {
            FormattedText ft = Make(s, f, new ImmutableSolidColorBrush(Conv.C(c)));
            ft.MaxTextWidth = width;
            ft.LineHeight = f.Size * 1.45;
            if (g != null) g.Dc.DrawText(ft, new Point(x, y));
            return (float)ft.Height;
        }

        static IBrush GoldBrush()
        {
            return new ImmutableLinearGradientBrush(new[] {
                    new ImmutableGradientStop(0, Avalonia.Media.Color.FromRgb(252, 233, 182)),
                    new ImmutableGradientStop(0.5, Conv.C(Accent)),
                    new ImmutableGradientStop(1, Avalonia.Media.Color.FromRgb(160, 113, 40)) },
                1, null, null, GradientSpreadMethod.Pad,
                new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative));
        }

        public static void FillAccent(Graphics g, RectangleF r, float rad)
        {
            if (!Current.Shiny) { FillRound(g, Accent, r, rad); return; }
            using (GraphicsPath path = Round(r, rad))
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Accent, Accent, 90f))
            {
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new[] { Color.FromArgb(252, 233, 182), Accent, Color.FromArgb(160, 113, 40) };
                blend.Positions = new[] { 0f, 0.5f, 1f };
                b.InterpolationColors = blend;
                g.FillPath(b, path);
            }
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
            if (string.IsNullOrEmpty(s)) return 0;
            return (float)Make(s, f, Brushes.White).WidthIncludingTrailingWhitespace;
        }

        // ---- the logo, drawn as shapes (the same drawing as Theme.Logo on Windows) ----

        public static void DrawLogo(Graphics g, RectangleF box, Color accent)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(box.X, box.Y);
            float s = box.Width;
            using (GraphicsPath bgp = RoundExact(new RectangleF(0, 0, s, s), s * 0.22f))
            using (SolidBrush bgb = new SolidBrush(Color.FromArgb(50, 52, 55))) g.FillPath(bgb, bgp);
            float w = Math.Max(1.6f, s * 0.13f);
            RectangleF arc = new RectangleF(s * 0.17f, s * 0.2f, s * 0.66f, s * 0.66f);
            using (Pen track = new Pen(Color.FromArgb(95, 99, 106), w)) { track.StartCap = track.EndCap = LineCap.Round; g.DrawArc(track, arc, 135, 270); }
            using (Pen p = new Pen(accent, w)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawArc(p, arc, 135, 190); }
            float cx = arc.X + arc.Width / 2, cy = arc.Y + arc.Height / 2;
            double a = (135 + 190) * Math.PI / 180;
            using (Pen n = new Pen(Color.White, Math.Max(1.4f, s * 0.08f)))
            {
                n.StartCap = n.EndCap = LineCap.Round;
                g.DrawLine(n, cx, cy, cx + (float)Math.Cos(a) * arc.Width * 0.36f, cy + (float)Math.Sin(a) * arc.Width * 0.36f);
            }
            using (SolidBrush hub = new SolidBrush(Color.White))
                g.FillEllipse(hub, cx - s * 0.06f, cy - s * 0.06f, s * 0.12f, s * 0.12f);
            g.Restore(st);
        }

        public static void DrawSwitch(Graphics g, RectangleF r, bool on)
        {
            if (on) FillAccent(g, r, r.Height / 2); else FillRound(g, Track, r, r.Height / 2);
            float d = r.Height - 6;
            float x = on ? r.Right - d - 3 : r.X + 3;
            using (SolidBrush knob = new SolidBrush(on ? OnAccent : Muted))
                g.FillEllipse(knob, x, r.Y + 3, d, d);
        }

        public static void DrawCloseX(Graphics g, RectangleF r, bool hover, Color normal)
        {
            if (hover) FillRound(g, Danger, r, 7);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using (Pen p = new Pen(hover ? Color.White : normal, 1.6f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                g.DrawLine(p, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
            }
        }

        public static string Fmt(double v)
        {
            return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
        }

        public static string Num(long v)
        {
            return v.ToString("#,0", CultureInfo.InvariantCulture);
        }
    }
}
