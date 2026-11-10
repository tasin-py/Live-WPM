using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // Pop-up from History: one day's speed graph with that day's totals. Ported from src/DayGraphWindow.cs.
    sealed class DayGraphWindow : PaintedWindow
    {
        const int W = 600, H = 340;
        static readonly RectangleF CloseRect = new RectangleF(W - 40, 14, 28, 28);
        static readonly RectangleF GraphRect = new RectangleF(24, 96, W - 48, H - 120);

        readonly DayStat day;
        int hoverPoint = -1;
        bool hoverClose;

        DayGraphWindow(DayStat day)
        {
            this.day = day;
            Title = day.Date.ToString("D", CultureInfo.InvariantCulture);
            SystemDecorations = SystemDecorations.None;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            UseTransparency(false);
            RoundCorners = 12;
            SetLogicalSize(W, H);
        }

        public static Task ShowFor(Window owner, DayStat day) { return new DayGraphWindow(day).ShowDialog(owner); }

        protected override void OnKey(Key k) { if (k == Key.Escape) Close(); }

        protected override void OnPaint(Graphics g)
        {
            LiveWpm.Theme.PaintBackground(g, W, H);
            if (SeeThrough) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Border, new RectangleF(0.5f, 0.5f, W - 1, H - 1), 12, 1);

            DateTime today = DateTime.Today;
            string title = day.Date == today ? "Today" : day.Date == today.AddDays(-1) ? "Yesterday" : day.Date.ToString("dddd, MMM d", CultureInfo.InvariantCulture);
            Font tf = LiveWpm.Theme.F(18, true);
            LiveWpm.Theme.DrawText(g, title, tf, LiveWpm.Theme.Text, 26, 22);
            LiveWpm.Theme.DrawText(g, day.Date.ToString("yyyy", CultureInfo.InvariantCulture), LiveWpm.Theme.F(12, false), LiveWpm.Theme.Muted, 32 + LiveWpm.Theme.Measure(g, title, tf), 28);

            List<string> list = new List<string> {
                day.PeakWpm > 0 ? "Peak " + LiveWpm.Theme.Fmt(day.PeakWpm) + " wpm" : "Peak —",
                day.AvgWpm > 0 ? "Average " + LiveWpm.Theme.Fmt(day.AvgWpm) + " wpm" : "Average —",
                LiveWpm.Theme.Num(day.Words) + " words" };
            if (day.Points.Count > 0) list.Add(LiveWpm.Theme.Num(day.Points.Count) + (day.Points.Count == 1 ? " run" : " runs"));
            Font sf = LiveWpm.Theme.F(11.5f, false);
            float x = 27;
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    using (SolidBrush b = new SolidBrush(LiveWpm.Theme.Border)) g.FillEllipse(b, x + 9, 64, 3, 3);
                    x += 21;
                }
                LiveWpm.Theme.DrawText(g, list[i], sf, i == 0 ? LiveWpm.Theme.Accent : LiveWpm.Theme.Muted, x, 57);
                x += LiveWpm.Theme.Measure(g, list[i], sf);
            }
            LiveWpm.Theme.DrawCloseX(g, CloseRect, hoverClose, LiveWpm.Theme.Muted);

            LiveWpm.Theme.Surface(g, LiveWpm.Theme.Card, GraphRect, 12);
            SpeedGraph.Draw(g, GraphRect, day, hoverPoint,
                day.Words > 0 ? "No graph for this day. Speed graphs are recorded from today onward." : "No runs this day.",
                LiveWpm.Theme.Card);
        }

        protected override void OnMouseMove(float x, float y)
        {
            bool hc = CloseRect.Contains(x, y);
            int hp = SpeedGraph.HitTest(GraphRect, day, x, y);
            if (hc != hoverClose || hp != hoverPoint)
            {
                hoverClose = hc; hoverPoint = hp;
                SetHandCursor(hc);
                Invalidate();
            }
        }

        protected override void OnMouseLeave()
        {
            if (hoverClose || hoverPoint >= 0) { hoverClose = false; hoverPoint = -1; Invalidate(); }
        }

        protected override void OnMouseDown(float x, float y, MouseButton b)
        {
            if (b != MouseButton.Left) return;
            if (CloseRect.Contains(x, y)) Close();
            else BeginDrag();
        }
    }
}
