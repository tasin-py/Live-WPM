using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // Shows the SupportKori QR and link so people can support Live WPM. Ported from src/DonateWindow.cs.
    sealed class DonateWindow : PaintedWindow
    {
        const int W = 340, H = 480;
        const string Url = "https://www.supportkori.com/tasinpy";
        const string ShownUrl = "supportkori.com/tasinpy";
        static readonly Color QrColor = Color.FromArgb(229, 137, 10);  // SupportKori orange
        static readonly RectangleF CloseRect = new RectangleF(W - 38, 10, 28, 28);
        static readonly RectangleF QrCard = new RectangleF(30, 88, W - 60, W - 60);

        static bool[,] modules;   // the QR, one entry per module (true = dark)
        bool hoverClose, copied;
        int hoverButton = -1;
        readonly RectangleF[] buttons = new RectangleF[2];

        DonateWindow()
        {
            Title = "Buy Me A Coffee";
            SystemDecorations = SystemDecorations.None;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            UseTransparency(false);
            RoundCorners = 12;
            SetLogicalSize(W, H);
            LoadQr();
        }

        public static Task ShowFor(Window owner) { return new DonateWindow().ShowDialog(owner); }

        // The QR image is stored one pixel per module (black = dark).
        static void LoadQr()
        {
            if (modules != null) return;
            try
            {
                using (System.IO.Stream st = typeof(DonateWindow).Assembly.GetManifestResourceStream("supportkori_qr.png"))
                using (WriteableBitmap bmp = WriteableBitmap.Decode(st))
                using (ILockedFramebuffer fb = bmp.Lock())
                {
                    int w = fb.Size.Width, h = fb.Size.Height;
                    byte[] row = new byte[fb.RowBytes];
                    bool[,] m = new bool[h, w];
                    // The file is 1-bit greyscale, which decodes to one byte per pixel; colour decodes to four.
                    int bpp = fb.RowBytes >= w * 4 ? 4 : 1;
                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy(fb.Address + y * fb.RowBytes, row, 0, row.Length);
                        for (int x = 0; x < w; x++)
                            m[y, x] = bpp == 1 ? row[x] < 128 : row[x * 4] + row[x * 4 + 1] + row[x * 4 + 2] < 384;
                    }
                    modules = m;
                }
            }
            catch { modules = null; }
        }

        protected override void OnKey(Key k) { if (k == Key.Escape) Close(); }

        protected override void OnPaint(Graphics g)
        {
            LiveWpm.Theme.PaintBackground(g, W, H);
            if (SeeThrough) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Border, new RectangleF(0.5f, 0.5f, W - 1, H - 1), 12, 1);

            LiveWpm.Theme.DrawText(g, "Buy Me A Coffee", LiveWpm.Theme.F(17, true), LiveWpm.Theme.Text, 30, 24);
            LiveWpm.Theme.DrawText(g, "Scan to support Live WPM on SupportKori", LiveWpm.Theme.F(11, false), LiveWpm.Theme.Muted, 31, 52);
            LiveWpm.Theme.DrawCloseX(g, CloseRect, hoverClose, LiveWpm.Theme.Muted);

            LiveWpm.Theme.FillRound(g, Color.White, QrCard, 14);
            if (LiveWpm.Theme.Light) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Border, QrCard, 14, 1);
            if (modules != null) DrawQr(g);

            LiveWpm.Theme.DrawTextCenter(g, ShownUrl, LiveWpm.Theme.F(14, true), LiveWpm.Theme.Text, W / 2f, QrCard.Bottom + 16);

            Font bf = LiveWpm.Theme.F(12, true);
            string[] labels = { "Open link", copied ? "Copied" : "Copy link" };
            float gap = 10, bw = 120, x = (W - bw * 2 - gap) / 2f, y = QrCard.Bottom + 50;
            for (int i = 0; i < 2; i++)
            {
                buttons[i] = new RectangleF(x + i * (bw + gap), y, bw, 32);
                bool hot = hoverButton == i;
                Color fill = i == 0 ? (hot ? LiveWpm.Theme.Lerp(LiveWpm.Theme.Accent, Color.White, 0.15) : LiveWpm.Theme.Accent)
                                    : (hot ? LiveWpm.Theme.Lerp(LiveWpm.Theme.CardHover, LiveWpm.Theme.Accent, 0.25) : LiveWpm.Theme.CardHover);
                Color text = i == 0 ? LiveWpm.Theme.OnAccent : (copied ? LiveWpm.Theme.Accent : LiveWpm.Theme.Text);
                LiveWpm.Theme.FillRound(g, fill, buttons[i], 8);
                LiveWpm.Theme.DrawTextCenter(g, labels[i], bf, text, buttons[i].X + bw / 2, buttons[i].Y + 8);
            }
        }

        // Whole logical pixels per module, centred in the card, so every module has crisp, even edges.
        void DrawQr(Graphics g)
        {
            int n = modules.GetLength(0);
            int cell = Math.Max(1, (int)((QrCard.Width - 40) / n));
            int size = cell * n;
            float left = (float)Math.Round(QrCard.X + QrCard.Width / 2 - size / 2f);
            float top = (float)Math.Round(QrCard.Y + QrCard.Height / 2 - size / 2f);
            using (SolidBrush b = new SolidBrush(QrColor))
                for (int r = 0; r < n; r++)
                    for (int c = 0; c < n; c++)
                        if (modules[r, c]) g.FillRectangle(b, left + c * cell, top + r * cell, cell, cell);
        }

        int ButtonAt(float x, float y)
        {
            for (int i = 0; i < buttons.Length; i++) if (buttons[i].Contains(x, y)) return i;
            return -1;
        }

        protected override void OnMouseMove(float x, float y)
        {
            bool hc = CloseRect.Contains(x, y);
            int hb = ButtonAt(x, y);
            if (hc != hoverClose || hb != hoverButton)
            {
                hoverClose = hc; hoverButton = hb;
                SetHandCursor(hc || hb >= 0);
                Invalidate();
            }
        }

        protected override void OnMouseLeave()
        {
            if (hoverClose || hoverButton >= 0) { hoverClose = false; hoverButton = -1; Invalidate(); }
        }

        protected override void OnMouseDown(float x, float y, MouseButton b)
        {
            if (b != MouseButton.Left) return;
            int bt = ButtonAt(x, y);
            if (CloseRect.Contains(x, y)) Close();
            else if (bt == 0) Platform.Open(Url);
            else if (bt == 1) { CopyText(Url); copied = true; Invalidate(); }
            else BeginDrag();
        }
    }
}
