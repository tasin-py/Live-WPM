using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LiveWpm
{
    // Lets an ordinary window show the blurred desktop through it, like the floating window does
    // in the glass theme: frames are drawn into a premultiplied 32-bit DIB and copied to the
    // window with their alpha, Windows blurs what is behind, and the frame is extended over the
    // whole client area so that alpha is honoured.
    sealed class GlassCanvas : IDisposable
    {
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
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        Bitmap canvas;
        IntPtr dc, dib, bits;

        // The frame's raw pixels, for text blended straight into it.
        public IntPtr Bits { get { return bits; } }

        // Turns the see-through, blurred look on or off for a window.
        public static void Apply(IntPtr hwnd, bool on)
        {
            try
            {
                if (on)
                {
                    // the title bar colour (and any system backdrop) would otherwise fill the extended frame
                    int defaultColour = unchecked((int)0xFFFFFFFF), none = 1;
                    DwmSetWindowAttribute(hwnd, 35, ref defaultColour, 4); // DWMWA_CAPTION_COLOR: default
                    DwmSetWindowAttribute(hwnd, 38, ref none, 4);          // DWMWA_SYSTEMBACKDROP_TYPE: none
                }
                MARGINS m = on ? new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 } : new MARGINS();
                DwmExtendFrameIntoClientArea(hwnd, ref m);
                AccentPolicy accent = new AccentPolicy { State = on ? 3 : 0 }; // ACCENT_ENABLE_BLURBEHIND / off
                int size = Marshal.SizeOf(typeof(AccentPolicy));
                IntPtr mem = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(accent, mem, false);
                    CompositionData data = new CompositionData { Attribute = 19, Data = mem, Size = size }; // WCA_ACCENT_POLICY
                    SetWindowCompositionAttribute(hwnd, ref data);
                }
                finally { Marshal.FreeHGlobal(mem); }
            }
            catch { } // older Windows: no blur
        }

        // A cleared canvas of the given size to draw the next frame on.
        public Bitmap Begin(int w, int h)
        {
            if (canvas == null || canvas.Width != w || canvas.Height != h)
            {
                Free();
                BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
                bi.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
                dc = CreateCompatibleDC(IntPtr.Zero);
                dib = CreateDIBSection(dc, ref bi, 0, out bits, IntPtr.Zero, 0);
                SelectObject(dc, dib);
                canvas = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
            }
            return canvas;
        }

        // Copies the finished frame, alpha included, onto the window.
        public void Present(Graphics target)
        {
            if (canvas == null) return;
            IntPtr hdc = target.GetHdc();
            try { BitBlt(hdc, 0, 0, canvas.Width, canvas.Height, dc, 0, 0, 0x00CC0020); } // SRCCOPY
            finally { target.ReleaseHdc(hdc); }
        }

        void Free()
        {
            if (canvas != null) { canvas.Dispose(); canvas = null; }
            if (dc != IntPtr.Zero) { DeleteDC(dc); dc = IntPtr.Zero; }
            if (dib != IntPtr.Zero) { DeleteObject(dib); dib = IntPtr.Zero; }
            bits = IntPtr.Zero;
        }

        public void Dispose() { Free(); }
    }
}
