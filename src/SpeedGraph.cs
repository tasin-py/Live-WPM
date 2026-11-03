using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LiveWpm
{
    // A day's speed as a line: one point per typing run (its average wpm) across the hours
    // of that day. Used on Home (today) and in the History pop-up (any day).
    static class SpeedGraph
    {
        const float PadL = 38, PadR = 14, PadT = 14, PadB = 26;

        static RectangleF Plot(RectangleF r)
        {
            return new RectangleF(r.X + PadL, r.Y + PadT, r.Width - PadL - PadR, r.Height - PadT - PadB);
        }

        // Time range shown: whole hours around the runs, at least two hours wide.
        static void Range(List<SpeedPoint> pts, out int from, out int to)
        {
            int min = int.MaxValue, max = 0;
            foreach (SpeedPoint p in pts) { min = Math.Min(min, p.Second); max = Math.Max(max, p.Second); }
            from = min / 3600 * 3600;
            to = (max / 3600 + 1) * 3600;
            if (to - from < 7200) to = Math.Min(86400, from + 7200);
            if (to - from < 7200) from = to - 7200;
        }

        static float NiceMax(List<SpeedPoint> pts, out float step)
        {
            float top = 0;
            foreach (SpeedPoint p in pts) top = Math.Max(top, p.Wpm);
            top = Math.Max(40, top * 1.12f);
            float[] steps = { 10, 20, 25, 50, 100 };
            step = 100;
            foreach (float s in steps) if (top / s <= 4) { step = s; break; }
            return (float)Math.Ceiling(top / step) * step;
        }

        static PointF Pos(RectangleF plot, SpeedPoint p, int from, int to, float max)
        {
            return new PointF(plot.X + plot.Width * (p.Second - from) / (float)(to - from),
                              plot.Bottom - plot.Height * Math.Min(1, p.Wpm / max));
        }

        static string Clock(int second)
        {
            return DateTime.Today.AddSeconds(second).ToString("h:mm tt");
        }

        static string HourLabel(int second)
        {
            return DateTime.Today.AddSeconds(second).ToString("h tt").ToLowerInvariant().Replace(" ", "");
        }

        // Which run is under the mouse (nearest in time), or -1.
        public static int HitTest(RectangleF r, DayStat day, float x, float y)
        {
            if (day == null || day.Points.Count == 0 || !r.Contains(x, y)) return -1;
            RectangleF plot = Plot(r);
            int from, to;
            List<SpeedPoint> pts = Sorted(day);
            Range(pts, out from, out to);
            int best = -1;
            float bestD = 24; // px: snaps to a run within this distance
            for (int i = 0; i < pts.Count; i++)
            {
                float px = plot.X + plot.Width * (pts[i].Second - from) / (float)(to - from);
                float d = Math.Abs(px - x);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        static List<SpeedPoint> Sorted(DayStat day)
        {
            List<SpeedPoint> pts = new List<SpeedPoint>(day.Points);
            pts.Sort((a, b) => a.Second.CompareTo(b.Second));
            return pts;
        }

        public static void Draw(Graphics g, RectangleF r, DayStat day, int hover, string emptyText, Color surface)
        {
            if (day == null || day.Points.Count == 0)
            {
                Theme.DrawTextCenter(g, emptyText, Theme.F(12, false), Theme.Muted, r.X + r.Width / 2, r.Y + r.Height / 2 - 8);
                return;
            }

            List<SpeedPoint> pts = Sorted(day);
            RectangleF plot = Plot(r);
            int from, to;
            Range(pts, out from, out to);
            float step, max = NiceMax(pts, out step);
            Font small = Theme.F(10, false);

            // recessive grid + y labels
            using (Pen grid = new Pen(Color.FromArgb(70, Theme.Border), 1))
                for (float v = 0; v <= max + 0.1f; v += step)
                {
                    float y = plot.Bottom - plot.Height * v / max;
                    g.DrawLine(grid, plot.X, y, plot.Right, y);
                    Theme.DrawTextRight(g, Theme.Fmt(v), small, Theme.Muted, plot.X - 8, y - 7);
                }

            // x labels: whole hours, at most ~7 of them
            int hours = (to - from) / 3600;
            int every = hours <= 6 ? 1 : hours <= 12 ? 2 : hours <= 18 ? 3 : 4;
            for (int s = from; s <= to; s += 3600 * every)
            {
                float x = plot.X + plot.Width * (s - from) / (float)(to - from);
                Theme.DrawTextCenter(g, HourLabel(s), small, Theme.Muted, x, plot.Bottom + 8);
            }

            PointF[] at = new PointF[pts.Count];
            for (int i = 0; i < pts.Count; i++) at[i] = Pos(plot, pts[i], from, to, max);

            // flat area under the line, then the 2px line
            if (at.Length > 1)
            {
                using (GraphicsPath area = new GraphicsPath())
                {
                    area.AddLine(at[0].X, plot.Bottom, at[0].X, at[0].Y);
                    area.AddLines(at);
                    area.AddLine(at[at.Length - 1].X, at[at.Length - 1].Y, at[at.Length - 1].X, plot.Bottom);
                    area.CloseFigure();
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(16, Theme.Accent))) g.FillPath(fill, area);
                }
                using (Pen line = new Pen(Theme.Accent, 2))
                {
                    line.LineJoin = LineJoin.Round;
                    g.DrawLines(line, at);
                }
            }

            // run markers with a ring in the surface colour so they read on the line
            float mr = pts.Count > 60 ? 2.5f : 4;
            using (SolidBrush dot = new SolidBrush(Theme.Accent))
            using (Pen ring = new Pen(surface, 2))
                foreach (PointF p in at)
                {
                    g.DrawEllipse(ring, p.X - mr, p.Y - mr, mr * 2, mr * 2);
                    g.FillEllipse(dot, p.X - mr, p.Y - mr, mr * 2, mr * 2);
                }

            if (hover >= 0 && hover < pts.Count) DrawTip(g, r, plot, at[hover], pts[hover], surface);
        }

        static void DrawTip(Graphics g, RectangleF r, RectangleF plot, PointF p, SpeedPoint sp, Color surface)
        {
            using (Pen cross = new Pen(Color.FromArgb(110, Theme.Muted), 1))
                g.DrawLine(cross, p.X, plot.Y, p.X, plot.Bottom);
            using (SolidBrush dot = new SolidBrush(Theme.Accent))
            using (Pen ring = new Pen(surface, 2.5f))
            {
                g.DrawEllipse(ring, p.X - 6, p.Y - 6, 12, 12);
                g.FillEllipse(dot, p.X - 6, p.Y - 6, 12, 12);
            }

            string head = Theme.Fmt(sp.Wpm) + " wpm";
            string line1 = Clock(sp.Second) + "  ·  peak " + Theme.Fmt(sp.Peak);
            string line2 = Theme.Num(sp.Words) + " words";
            Font hf = Theme.F(13, true), sf = Theme.F(10.5f, false);
            float w = Math.Max(Theme.Measure(g, head, hf), Math.Max(Theme.Measure(g, line1, sf), Theme.Measure(g, line2, sf))) + 24;
            float h = 62;
            float x = p.X + 12, y = p.Y - h / 2;
            if (x + w > r.Right - 4) x = p.X - 12 - w;           // flip to the left near the right edge
            y = Math.Max(r.Y + 4, Math.Min(r.Bottom - h - 4, y));
            RectangleF box = new RectangleF(x, y, w, h);
            Theme.FillRound(g, Theme.CardHover, box, 9);
            Theme.DrawText(g, head, hf, Theme.Text, x + 12, y + 8);
            Theme.DrawText(g, line1, sf, Theme.Muted, x + 12, y + 28);
            Theme.DrawText(g, line2, sf, Theme.Muted, x + 12, y + 43);
        }
    }
}
