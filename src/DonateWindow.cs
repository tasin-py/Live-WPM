using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiveWpm
{
    // Shows the SupportKori QR and link so people can support Live WPM.
    sealed class DonateWindow : BufferedForm
    {
        const int W = 340, H = 480;
        const string Url = "https://www.supportkori.com/tasinpy";
        const string ShownUrl = "supportkori.com/tasinpy";
        static readonly Color QrColor = Color.FromArgb(229, 137, 10);  // SupportKori orange
        static readonly RectangleF CloseRect = new RectangleF(W - 38, 10, 28, 28);
        static readonly RectangleF QrCard = new RectangleF(30, 88, W - 60, W - 60);

        // The QR is stored one pixel per module (black = dark), so it can be drawn sharp at any scale.
        readonly Bitmap qr;
        bool hoverClose, copied;
        int hoverButton = -1;   // 0 open link, 1 copy link
        readonly RectangleF[] buttons = new RectangleF[2];

        DonateWindow()
        {
            Text = "Buy Me A Coffee";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            SetLogicalSize(W, H);
            using (System.IO.Stream st = typeof(DonateWindow).Assembly.GetManifestResourceStream("supportkori_qr.png"))
                if (st != null) qr = new Bitmap(st);
        }

        public static void ShowFor(IWin32Window owner)
        {
            using (DonateWindow d = new DonateWindow()) d.ShowDialog(owner);
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

            Theme.DrawText(g, "Buy Me A Coffee", Theme.F(17, true), Theme.Text, 30, 24);
            Theme.DrawText(g, "Scan to support Live WPM on SupportKori", Theme.F(11, false), Theme.Muted, 31, 52);
            DrawClose(g);

            Theme.FillRound(g, Color.White, QrCard, 14);
            if (Theme.Light) Theme.StrokeRound(g, Theme.Border, QrCard, 14, 1); // keeps the white card visible on light themes
            if (qr != null) DrawQr(g);

            Theme.DrawTextCenter(g, ShownUrl, Theme.F(14, true), Theme.Text, W / 2f, QrCard.Bottom + 16);

            Font bf = Theme.F(12, true);
            string[] labels = { "Open link", copied ? "Copied" : "Copy link" };
            float gap = 10, bw = 120, x = (W - bw * 2 - gap) / 2f, y = QrCard.Bottom + 50;
            for (int i = 0; i < 2; i++)
            {
                buttons[i] = new RectangleF(x + i * (bw + gap), y, bw, 32);
                bool hot = hoverButton == i;
                Color fill = i == 0 ? (hot ? Theme.Lerp(Theme.Accent, Color.White, 0.15) : Theme.Accent)
                                    : (hot ? Theme.Lerp(Theme.CardHover, Theme.Accent, 0.25) : Theme.CardHover);
                Color text = i == 0 ? Theme.OnAccent : (copied ? Theme.Accent : Theme.Text);
                Theme.FillRound(g, fill, buttons[i], 8);
                Theme.DrawTextCenter(g, labels[i], bf, text, buttons[i].X + bw / 2, buttons[i].Y + 8);
            }
        }

        // Whole device pixels per module, centred in the card, so every module has crisp, even edges.
        void DrawQr(Graphics g)
        {
            GraphicsState state = g.Save();
            g.ResetTransform();
            g.SmoothingMode = SmoothingMode.None;
            int n = qr.Width;
            int cell = Math.Max(1, (int)((QrCard.Width - 40) * S / n));
            int size = cell * n;
            int left = (int)Math.Round((QrCard.X + QrCard.Width / 2) * S - size / 2f);
            int top = (int)Math.Round((QrCard.Y + QrCard.Height / 2) * S - size / 2f);
            using (SolidBrush b = new SolidBrush(QrColor))
                for (int r = 0; r < n; r++)
                    for (int c = 0; c < n; c++)
                        if (qr.GetPixel(c, r).GetBrightness() < 0.5f)
                            g.FillRectangle(b, left + c * cell, top + r * cell, cell, cell);
            g.Restore(state);
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

        int ButtonAt(float x, float y)
        {
            for (int i = 0; i < buttons.Length; i++) if (buttons[i].Contains(x, y)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float x = e.X / S, y = e.Y / S;
            bool hc = CloseRect.Contains(x, y);
            int hb = ButtonAt(x, y);
            if (hc != hoverClose || hb != hoverButton)
            {
                hoverClose = hc; hoverButton = hb;
                Cursor = hc || hb >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverClose || hoverButton >= 0) { hoverClose = false; hoverButton = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            float x = e.X / S, y = e.Y / S;
            int b = ButtonAt(x, y);
            if (CloseRect.Contains(x, y)) Close();
            else if (b == 0)
            {
                try { System.Diagnostics.Process.Start(Url); } catch { }   // the Typer's own browser opens it
            }
            else if (b == 1)
            {
                try { Clipboard.SetText(Url); } catch { }
                copied = true;
                Invalidate();
            }
            else Theme.BeginDrag(Handle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && qr != null) qr.Dispose();
            base.Dispose(disposing);
        }
    }
}
