using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    enum MouseButton { Left, Right, Middle, None }

    // A window that paints itself by hand, like the Windows app's BufferedForm. Subclasses override
    // OnPaint and the mouse methods, which get positions in the window's own logical pixels.
    class PaintedWindow : Window
    {
        sealed class Surface : Control
        {
            readonly PaintedWindow owner;
            public Surface(PaintedWindow w) { owner = w; ClipToBounds = true; }

            public override void Render(DrawingContext dc)
            {
                Rect all = new Rect(Bounds.Size);
                Graphics g = new Graphics(dc, all);
                try
                {
                    if (owner.RoundCorners > 0 && owner.SeeThrough)
                        g.SetClip(LiveWpm.Theme.RoundExact(new RectangleF(0, 0, (float)all.Width, (float)all.Height), owner.RoundCorners));
                    owner.OnPaint(g);
                }
                finally { g.Done(); }
            }
        }

        readonly Surface surface;
        protected readonly Canvas Overlay = new Canvas();   // for real controls, such as a text box
        protected const float S = 1;
        PointerPressedEventArgs lastPress;

        // Corner rounding for borderless windows, used when the desktop can show transparency.
        protected float RoundCorners;

        protected PaintedWindow()
        {
            surface = new Surface(this);
            Panel root = new Panel();
            root.Children.Add(surface);
            root.Children.Add(Overlay);
            Content = root;
            Title = LiveWpm.Theme.AppName;
            Icon = App.WindowIcon;
            CanResize = false;
            SizeToContent = SizeToContent.Manual;
            Background = Brushes.Transparent;

            surface.PointerMoved += (s, e) => { Point p = e.GetPosition(surface); OnMouseMove((float)p.X, (float)p.Y); };
            surface.PointerExited += (s, e) => OnMouseLeave();
            surface.PointerPressed += (s, e) =>
            {
                lastPress = e;
                Point p = e.GetPosition(surface);
                OnMouseDown((float)p.X, (float)p.Y, ButtonOf(e.GetCurrentPoint(surface).Properties.PointerUpdateKind));
            };
            surface.PointerReleased += (s, e) =>
            {
                Point p = e.GetPosition(surface);
                OnMouseUp((float)p.X, (float)p.Y, ButtonOf(e.GetCurrentPoint(surface).Properties.PointerUpdateKind));
            };
            surface.PointerWheelChanged += (s, e) => OnMouseWheel((float)e.Delta.Y);
            KeyDown += (s, e) => OnKey(e.Key);
        }

        static MouseButton ButtonOf(PointerUpdateKind k)
        {
            switch (k)
            {
                case PointerUpdateKind.LeftButtonPressed: case PointerUpdateKind.LeftButtonReleased: return MouseButton.Left;
                case PointerUpdateKind.RightButtonPressed: case PointerUpdateKind.RightButtonReleased: return MouseButton.Right;
                case PointerUpdateKind.MiddleButtonPressed: case PointerUpdateKind.MiddleButtonReleased: return MouseButton.Middle;
            }
            return MouseButton.None;
        }

        // Asks for a see-through window; RoundCorners only applies when the desktop agrees.
        protected void UseTransparency(bool blur)
        {
            TransparencyLevelHint = blur
                ? new[] { WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent }
                : new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
        }

        public bool SeeThrough
        {
            get { return ActualTransparencyLevel == WindowTransparencyLevel.Transparent || ActualTransparencyLevel == WindowTransparencyLevel.Blur; }
        }

        public bool Blurred { get { return ActualTransparencyLevel == WindowTransparencyLevel.Blur; } }

        protected void SetLogicalSize(float w, float h)
        {
            Width = w; Height = h;
            MinWidth = MaxWidth = w; MinHeight = MaxHeight = h;
        }

        public void Invalidate() { surface.InvalidateVisual(); }

        protected void SetHandCursor(bool hand)
        {
            surface.Cursor = hand ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        }

        // Lets the window manager move the window, from the press that is happening now.
        protected void BeginDrag()
        {
            if (lastPress != null) try { BeginMoveDrag(lastPress); } catch { }
        }

        protected virtual void OnPaint(Graphics g) { }
        protected virtual void OnMouseMove(float x, float y) { }
        protected virtual void OnMouseLeave() { }
        protected virtual void OnMouseDown(float x, float y, MouseButton b) { }
        protected virtual void OnMouseUp(float x, float y, MouseButton b) { }
        protected virtual void OnMouseWheel(float delta) { }
        protected virtual void OnKey(Key k) { }

        public void CopyText(string s)
        {
            try { if (Clipboard != null) Clipboard.SetTextAsync(s); } catch { }
        }
    }
}
