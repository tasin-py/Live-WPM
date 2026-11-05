using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiveWpm
{
    // Pop-up from History: one day's speed graph with that day's totals.
    sealed class DayGraphWindow : BufferedForm
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
            Text = day.Date.ToString("D");
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            SetLogicalSize(W, H);
        }

        public static void ShowFor(IWin32Window owner, DayStat day)
        {
            using (DayGraphWindow w = new DayGraphWindow(day)) w.ShowDialog(owner);
            Theme.TrimMemory();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.RoundCorners(Handle);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            g.ScaleTransform(S, S);
            Theme.PaintBackground(g, W, H);

            DateTime today = DateTime.Today;
            string title = day.Date == today ? "Today" : day.Date == today.AddDays(-1) ? "Yesterday" : day.Date.ToString("dddd, MMM d");
            Font tf = Theme.F(18, true);
            Theme.DrawText(g, title, tf, Theme.Text, 26, 22);
            Theme.DrawText(g, day.Date.ToString("yyyy"), Theme.F(12, false), Theme.Muted, 32 + Theme.Measure(g, title, tf), 28);

            // the day's totals, as a quiet stat line
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string> {
                day.PeakWpm > 0 ? "Peak " + Theme.Fmt(day.PeakWpm) + " wpm" : "Peak —",
                day.AvgWpm > 0 ? "Average " + Theme.Fmt(day.AvgWpm) + " wpm" : "Average —",
                Theme.Num(day.Words) + " words" };
            if (day.Points.Count > 0) list.Add(Theme.Num(day.Points.Count) + (day.Points.Count == 1 ? " run" : " runs"));
            string[] stats = list.ToArray();
            Font sf = Theme.F(11.5f, false);
            float x = 27;
            for (int i = 0; i < stats.Length; i++)
            {
                if (i > 0)
                {
                    using (SolidBrush b = new SolidBrush(Theme.Border)) g.FillEllipse(b, x + 9, 64, 3, 3);
                    x += 21;
                }
                Theme.DrawText(g, stats[i], sf, i == 0 ? Theme.Accent : Theme.Muted, x, 57);
                x += Theme.Measure(g, stats[i], sf);
            }
            DrawClose(g);

            Theme.Surface(g, Theme.Card, GraphRect, 12);
            SpeedGraph.Draw(g, GraphRect, day, hoverPoint,
                day.Words > 0 ? "No graph for this day. Speed graphs are recorded from today onward." : "No runs this day.",
                Theme.Card);
        }

        void DrawClose(Graphics g)
        {
            if (hoverClose) Theme.FillRound(g, Theme.Danger, CloseRect, 7);
            float cx = CloseRect.X + CloseRect.Width / 2, cy = CloseRect.Y + CloseRect.Height / 2;
            using (Pen p = new Pen(hoverClose ? Color.White : Theme.Muted, 1.6f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                g.DrawLine(p, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float x = e.X / S, y = e.Y / S;
            bool hc = CloseRect.Contains(x, y);
            int hp = SpeedGraph.HitTest(GraphRect, day, x, y);
            if (hc != hoverClose || hp != hoverPoint)
            {
                hoverClose = hc; hoverPoint = hp;
                Cursor = hc ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverClose || hoverPoint >= 0) { hoverClose = false; hoverPoint = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (CloseRect.Contains(e.X / S, e.Y / S)) Close();
            else Theme.BeginDrag(Handle);
        }
    }
}
