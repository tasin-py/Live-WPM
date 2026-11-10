using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using SD = System.Drawing;

// The Windows paint code says "using System.Drawing.Drawing2D;". That namespace only exists on
// Windows, so it is declared here; the drawing types themselves live in LiveWpm, below.
namespace System.Drawing.Drawing2D { static class LinuxShim { } }

namespace LiveWpm
{
    // A small stand-in for the parts of GDI+ the Windows app paints with (Graphics, Pen, brushes,
    // paths), drawn with Avalonia. It lets the Windows windows' paint code, and the shared
    // SpeedGraph.cs, run on Linux nearly unchanged, so both editions look the same.

    enum LineCap { Flat, Square, Round }
    enum LineJoin { Miter, Bevel, Round }
    enum FillMode { Alternate, Winding }
    enum CombineMode { Replace, Intersect, Exclude }
    enum WrapMode { Tile, TileFlipX, TileFlipY, TileFlipXY, Clamp }

    static class Conv
    {
        public static Color C(SD.Color c) { return Color.FromArgb(c.A, c.R, c.G, c.B); }
        public static Rect R(SD.RectangleF r) { return new Rect(r.X, r.Y, Math.Max(0, r.Width), Math.Max(0, r.Height)); }
        public static Point P(SD.PointF p) { return new Point(p.X, p.Y); }
    }

    abstract class Brush : IDisposable
    {
        internal abstract IBrush Make();
        public void Dispose() { }
    }

    sealed class SolidBrush : Brush
    {
        public SD.Color Color;
        public SolidBrush(SD.Color c) { Color = c; }
        internal override IBrush Make() { return new ImmutableSolidColorBrush(Conv.C(Color)); }
    }

    sealed class ColorBlend
    {
        public SD.Color[] Colors;
        public float[] Positions;
        public ColorBlend(int n) { Colors = new SD.Color[n]; Positions = new float[n]; }
    }

    // A gradient across a rectangle in drawing coordinates: angle 90 runs top to bottom, 0 left to right.
    sealed class LinearGradientBrush : Brush
    {
        readonly SD.RectangleF rect;
        readonly SD.Color a, b;
        readonly float angle;
        public WrapMode WrapMode;
        public ColorBlend InterpolationColors;

        public LinearGradientBrush(SD.RectangleF r, SD.Color a, SD.Color b, float angle)
        {
            rect = r; this.a = a; this.b = b; this.angle = angle;
        }

        internal override IBrush Make()
        {
            bool vertical = Math.Abs(angle - 90) < 45;
            Point start = new Point(rect.X, rect.Y);
            Point end = vertical ? new Point(rect.X, rect.Bottom) : new Point(rect.Right, rect.Y);
            List<ImmutableGradientStop> stops = new List<ImmutableGradientStop>();
            if (InterpolationColors != null)
                for (int i = 0; i < InterpolationColors.Colors.Length; i++)
                    stops.Add(new ImmutableGradientStop(InterpolationColors.Positions[i], Conv.C(InterpolationColors.Colors[i])));
            else
            {
                stops.Add(new ImmutableGradientStop(0, Conv.C(a)));
                stops.Add(new ImmutableGradientStop(1, Conv.C(b)));
            }
            return new ImmutableLinearGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad,
                new RelativePoint(start, RelativeUnit.Absolute), new RelativePoint(end, RelativeUnit.Absolute));
        }
    }

    sealed class Pen : IDisposable
    {
        readonly Brush brush;
        public float Width;
        public LineCap StartCap, EndCap;
        public LineJoin LineJoin;

        public Pen(SD.Color c, float width) { brush = new SolidBrush(c); Width = width; }
        public Pen(SD.Color c) : this(c, 1) { }
        public Pen(Brush b, float width) { brush = b; Width = width; }

        internal IPen Make()
        {
            PenLineCap cap = StartCap == LineCap.Round || EndCap == LineCap.Round ? PenLineCap.Round
                           : StartCap == LineCap.Square ? PenLineCap.Square : PenLineCap.Flat;
            PenLineJoin join = LineJoin == LineJoin.Round ? PenLineJoin.Round : LineJoin == LineJoin.Bevel ? PenLineJoin.Bevel : PenLineJoin.Miter;
            return new ImmutablePen((IImmutableBrush)brush.Make(), Width, null, cap, join);
        }

        public void Dispose() { }
    }

    // GDI+-style path: arcs, lines and curves join up into figures until CloseFigure.
    sealed class GraphicsPath : IDisposable
    {
        sealed class Figure
        {
            public Point Start;
            public readonly List<Point[]> Segs = new List<Point[]>(); // 1 point = line to, 3 points = cubic curve to
            public bool Closed, Started;
            public Point Last;
        }

        readonly List<Figure> figures = new List<Figure>();
        public FillMode FillMode;

        public GraphicsPath() { }
        public GraphicsPath(FillMode mode) { FillMode = mode; }

        Figure Open()
        {
            if (figures.Count == 0 || figures[figures.Count - 1].Closed) figures.Add(new Figure());
            return figures[figures.Count - 1];
        }

        void MoveOrLine(Point p)
        {
            Figure f = Open();
            if (!f.Started) { f.Start = p; f.Started = true; }
            else f.Segs.Add(new[] { p });
            f.Last = p;
        }

        void Curve(Point c1, Point c2, Point p)
        {
            Figure f = Open();
            f.Segs.Add(new[] { c1, c2, p });
            f.Last = p;
        }

        public void AddLine(float x1, float y1, float x2, float y2)
        {
            MoveOrLine(new Point(x1, y1));
            MoveOrLine(new Point(x2, y2));
        }

        public void AddLines(SD.PointF[] pts) { foreach (SD.PointF p in pts) MoveOrLine(Conv.P(p)); }

        public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        {
            MoveOrLine(new Point(x1, y1));
            Curve(new Point(x2, y2), new Point(x3, y3), new Point(x4, y4));
        }

        // An elliptical arc in a box, GDI angles: degrees, clockwise from three o'clock.
        public void AddArc(float x, float y, float w, float h, float start, float sweep)
        {
            double cx = x + w / 2.0, cy = y + h / 2.0, rx = w / 2.0, ry = h / 2.0;
            double a0 = start * Math.PI / 180, total = sweep * Math.PI / 180;
            int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(total) / (Math.PI / 2)));
            double step = total / n;
            MoveOrLine(new Point(cx + rx * Math.Cos(a0), cy + ry * Math.Sin(a0)));
            for (int i = 0; i < n; i++)
            {
                double s = a0 + step * i, e = s + step;
                double k = 4.0 / 3.0 * Math.Tan(step / 4);
                Point p1 = new Point(cx + rx * (Math.Cos(s) - k * Math.Sin(s)), cy + ry * (Math.Sin(s) + k * Math.Cos(s)));
                Point p2 = new Point(cx + rx * (Math.Cos(e) + k * Math.Sin(e)), cy + ry * (Math.Sin(e) - k * Math.Cos(e)));
                Curve(p1, p2, new Point(cx + rx * Math.Cos(e), cy + ry * Math.Sin(e)));
            }
        }

        public void AddEllipse(float x, float y, float w, float h)
        {
            CloseFigure();
            figures.Add(new Figure());
            AddArc(x, y, w, h, 0, 360);
            CloseFigure();
        }

        public void CloseFigure()
        {
            if (figures.Count > 0) figures[figures.Count - 1].Closed = true;
        }

        internal Geometry Build()
        {
            StreamGeometry geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                ctx.SetFillRule(FillMode == FillMode.Winding ? FillRule.NonZero : FillRule.EvenOdd);
                foreach (Figure f in figures)
                {
                    if (!f.Started) continue;
                    ctx.BeginFigure(f.Start, true);
                    foreach (Point[] s in f.Segs)
                        if (s.Length == 1) ctx.LineTo(s[0]);
                        else ctx.CubicBezierTo(s[0], s[1], s[2]);
                    ctx.EndFigure(f.Closed);
                }
            }
            return geo;
        }

        public void Dispose() { }
    }

    sealed class GraphicsState
    {
        internal readonly int Depth;
        internal GraphicsState(int d) { Depth = d; }
    }

    // App font sizes are in pixels, as on Windows. Height is the line spacing.
    sealed class Font
    {
        public readonly float Size;
        public readonly bool Bold;
        public Font(float size, bool bold) { Size = size; Bold = bold; }
        public float Height { get { return Theme.LineHeight(this); } }
    }

    sealed class Graphics
    {
        public readonly DrawingContext Dc;
        readonly Stack<IDisposable> pushed = new Stack<IDisposable>();
        readonly Rect canvas;

        public Graphics(DrawingContext dc, Rect canvas) { Dc = dc; this.canvas = canvas; }

        // GDI quality switches: Avalonia always draws anti-aliased.
        public object SmoothingMode, PixelOffsetMode, CompositingQuality, InterpolationMode, TextRenderingHint;

        public GraphicsState Save() { return new GraphicsState(pushed.Count); }

        public void Restore(GraphicsState s)
        {
            while (pushed.Count > s.Depth) pushed.Pop().Dispose();
        }

        // Undo everything still pushed (end of a paint).
        public void Done() { while (pushed.Count > 0) pushed.Pop().Dispose(); }

        public void ScaleTransform(float x, float y)
        {
            if (Math.Abs(x - 1) < 0.0001 && Math.Abs(y - 1) < 0.0001) return;
            pushed.Push(Dc.PushTransform(Matrix.CreateScale(x, y)));
        }

        public void TranslateTransform(float x, float y) { pushed.Push(Dc.PushTransform(Matrix.CreateTranslation(x, y))); }
        public void RotateTransform(float deg) { pushed.Push(Dc.PushTransform(Matrix.CreateRotation(deg * Math.PI / 180))); }
        public void PushOpacity(double o) { pushed.Push(Dc.PushOpacity(o)); }

        public void SetClip(SD.RectangleF r) { pushed.Push(Dc.PushClip(Conv.R(r))); }
        public void SetClip(GraphicsPath p) { pushed.Push(Dc.PushGeometryClip(p.Build())); }

        public void SetClip(GraphicsPath p, CombineMode mode)
        {
            if (mode != CombineMode.Exclude) { SetClip(p); return; }
            Geometry outside = new CombinedGeometry(GeometryCombineMode.Exclude,
                new RectangleGeometry(canvas.Inflate(4000)), p.Build());
            pushed.Push(Dc.PushGeometryClip(outside));
        }

        public void Clear(SD.Color c) { Dc.FillRectangle(new ImmutableSolidColorBrush(Conv.C(c)), canvas); }

        public void FillRectangle(Brush b, SD.RectangleF r) { Dc.DrawRectangle(b.Make(), null, Conv.R(r)); }
        public void FillRectangle(Brush b, float x, float y, float w, float h) { FillRectangle(b, new SD.RectangleF(x, y, w, h)); }
        public void FillRectangle(Brush b, int x, int y, int w, int h) { FillRectangle(b, new SD.RectangleF(x, y, w, h)); }
        public void DrawRectangle(Pen p, float x, float y, float w, float h) { Dc.DrawRectangle(null, p.Make(), new Rect(x, y, w, h)); }

        public void FillEllipse(Brush b, float x, float y, float w, float h)
        {
            Dc.DrawEllipse(b.Make(), null, new Point(x + w / 2, y + h / 2), w / 2, h / 2);
        }

        public void DrawEllipse(Pen p, float x, float y, float w, float h)
        {
            Dc.DrawEllipse(null, p.Make(), new Point(x + w / 2, y + h / 2), w / 2, h / 2);
        }

        public void DrawLine(Pen p, float x1, float y1, float x2, float y2) { Dc.DrawLine(p.Make(), new Point(x1, y1), new Point(x2, y2)); }

        public void DrawLines(Pen p, SD.PointF[] pts)
        {
            if (pts.Length < 2) return;
            using (GraphicsPath path = new GraphicsPath()) { path.AddLines(pts); DrawPath(p, path); }
        }

        public void DrawArc(Pen p, SD.RectangleF r, float start, float sweep)
        {
            using (GraphicsPath path = new GraphicsPath()) { path.AddArc(r.X, r.Y, r.Width, r.Height, start, sweep); DrawPath(p, path); }
        }

        public void DrawPath(Pen p, GraphicsPath path) { Dc.DrawGeometry(null, p.Make(), path.Build()); }
        public void FillPath(Brush b, GraphicsPath path) { Dc.DrawGeometry(b.Make(), null, path.Build()); }
    }
}
