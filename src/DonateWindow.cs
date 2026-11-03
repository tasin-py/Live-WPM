using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LiveWpm
{
    // Shows the bKash Bangla QR so people can send a donation from bKash or any bank app.
    sealed class DonateWindow : BufferedForm
    {
        const int W = 340, H = 520;
        const string Number = "01788393887";
        static readonly Color Pink = Color.FromArgb(226, 19, 110);   // bKash
        static readonly Color LabelRed = Color.FromArgb(226, 35, 26); // "BANGLA QR"
        const int LabelRows = 100;                                     // top rows of the image hold the label
        static readonly RectangleF CloseRect = new RectangleF(W - 38, 10, 28, 28);
        static readonly RectangleF QrCard = new RectangleF(30, 92, W - 60, 320);

        readonly Bitmap qr;
        bool hoverClose, hoverCopy, copied;
        RectangleF copyRect;

        DonateWindow()
        {
            Text = "Donate";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            SetLogicalSize(W, H);
            using (System.IO.Stream st = typeof(DonateWindow).Assembly.GetManifestResourceStream("bkash_qr.png"))
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
            g.Clear(Theme.Bg);

            Theme.DrawText(g, "Support Live WPM", Theme.F(17, true), Theme.Text, 30, 24);
            Theme.DrawText(g, "Scan with bKash or any bank app to send money", Theme.F(11, false), Theme.Muted, 31, 52);
            DrawClose(g);

            Theme.FillRound(g, Color.White, QrCard, 14);
            if (qr != null)
            {
                float iw = QrCard.Width - 40, ih = iw * qr.Height / qr.Width;
                RectangleF img = new RectangleF(QrCard.X + 20, QrCard.Y + 16, iw, ih);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                float split = ih * LabelRows / qr.Height;
                DrawTinted(g, new RectangleF(img.X, img.Y, iw, split), new Rectangle(0, 0, qr.Width, LabelRows), LabelRed);
                DrawTinted(g, new RectangleF(img.X, img.Y + split, iw, ih - split), new Rectangle(0, LabelRows, qr.Width, qr.Height - LabelRows), Pink);
                g.PixelOffsetMode = PixelOffsetMode.None;
            }

            // number + copy
            Theme.DrawTextCenter(g, Number, Theme.F(22, true), Theme.Text, W / 2f, QrCard.Bottom + 18);
            Font bf = Theme.F(12, true);
            string label = copied ? "Copied" : "Copy number";
            float bw = Theme.Measure(g, label, bf) + 34;
            copyRect = new RectangleF((W - bw) / 2f, QrCard.Bottom + 56, bw, 30);
            Theme.FillRound(g, hoverCopy ? Theme.Lerp(Theme.CardHover, Theme.Accent, 0.25) : Theme.CardHover, copyRect, 8);
            Theme.DrawTextCenter(g, label, bf, copied ? Theme.Accent : Theme.Text, copyRect.X + bw / 2, copyRect.Y + 7);
        }

        // The QR is stored as black and white; paint the black parts in the given colour.
        void DrawTinted(Graphics g, RectangleF dst, Rectangle src, Color c)
        {
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetRemapTable(new ColorMap[] { new ColorMap { OldColor = Color.Black, NewColor = c } });
                ia.SetWrapMode(WrapMode.TileFlipXY); // no grey fringe at the edges
                g.DrawImage(qr, new PointF[] { dst.Location, new PointF(dst.Right, dst.Top), new PointF(dst.Left, dst.Bottom) },
                            src, GraphicsUnit.Pixel, ia);
            }
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float x = e.X / S, y = e.Y / S;
            bool hc = CloseRect.Contains(x, y), hp = copyRect.Contains(x, y);
            if (hc != hoverClose || hp != hoverCopy)
            {
                hoverClose = hc; hoverCopy = hp;
                Cursor = hc || hp ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            float x = e.X / S, y = e.Y / S;
            if (CloseRect.Contains(x, y)) Close();
            else if (copyRect.Contains(x, y))
            {
                try { Clipboard.SetText(Number); } catch { }
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
