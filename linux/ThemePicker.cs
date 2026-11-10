using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // Pop-up from Settings: every theme as a small preview. Clicking one applies it at once.
    // The Typer's own themes are added from a file here, and can be removed. Ported from src/ThemePicker.cs.
    sealed class ThemePicker : PaintedWindow
    {
        const int Cols = 4, TileW = 128, TileH = 88, Gap = 14, NameH = 28, ButtonH = 32, RemoveSize = 22;
        const int W = 24 * 2 + Cols * TileW + (Cols - 1) * Gap;
        static readonly RectangleF CloseRect = new RectangleF(W - 40, 14, 28, 28);

        readonly Action<string> choose;
        string current;
        int hover = -1, hoverButton = -1;
        bool hoverClose, hoverRemove, busy;

        ThemePicker(string current, Action<string> choose)
        {
            this.current = current;
            this.choose = choose;
            Title = "Theme";
            SystemDecorations = SystemDecorations.None;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            UseTransparency(false);
            RoundCorners = 12;
            SetLogicalSize(W, H);
        }

        public static Task ShowFor(Window owner, string current, Action<string> choose)
        {
            return new ThemePicker(current, choose).ShowDialog(owner);
        }

        static int Rows { get { return (LiveWpm.Theme.ThemeCount + Cols - 1) / Cols; } }
        static int H { get { return 82 + Rows * (TileH + NameH) + (Rows - 1) * 8 + 16 + ButtonH + 20; } }
        static RectangleF ButtonRect(int i) { return new RectangleF(24 + i * 150, H - 20 - ButtonH, 140, ButtonH); }

        protected override void OnKey(Key k)
        {
            if (k == Key.Escape || k == Key.Enter) Close();
        }

        RectangleF Tile(int i)
        {
            int c = i % Cols, r = i / Cols;
            return new RectangleF(24 + c * (TileW + Gap), 82 + r * (TileH + NameH + 8), TileW, TileH);
        }

        RectangleF RemoveRect(int i)
        {
            RectangleF t = Tile(i);
            return new RectangleF(t.Right - RemoveSize - 4, t.Y + 4, RemoveSize, RemoveSize);
        }

        static bool IsCustom(int i) { return i >= LiveWpm.Theme.Palettes.Length; }

        protected override void OnPaint(Graphics g)
        {
            LiveWpm.Theme.PaintBackground(g, W, H);
            if (SeeThrough) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Border, new RectangleF(0.5f, 0.5f, W - 1, H - 1), 12, 1);

            LiveWpm.Theme.DrawText(g, "Themes", LiveWpm.Theme.F(18, true), LiveWpm.Theme.Text, 26, 20);
            LiveWpm.Theme.DrawText(g, "Pick one to apply it. Live WPM remembers your choice.", LiveWpm.Theme.F(11.5f, false), LiveWpm.Theme.Muted, 27, 48);
            LiveWpm.Theme.DrawCloseX(g, CloseRect, hoverClose, LiveWpm.Theme.Muted);

            for (int i = 0; i < LiveWpm.Theme.ThemeCount; i++)
            {
                Palette p = LiveWpm.Theme.ThemeAt(i);
                RectangleF t = Tile(i);
                bool on = p.Id == current;
                DrawPreview(g, p, t);
                if (on) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Accent, new RectangleF(t.X - 3, t.Y - 3, t.Width + 6, t.Height + 6), 13, 2);
                else if (i == hover) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Muted, new RectangleF(t.X - 3, t.Y - 3, t.Width + 6, t.Height + 6), 13, 1.2f);
                Font nf = LiveWpm.Theme.F(12, on);
                LiveWpm.Theme.DrawTextCenter(g, p.Name, nf, on ? LiveWpm.Theme.Text : (i == hover ? LiveWpm.Theme.Text : LiveWpm.Theme.Muted), t.X + t.Width / 2, t.Bottom + 8);
                if (i == hover && IsCustom(i)) DrawRemove(g, RemoveRect(i));
            }

            DrawButton(g, 0, "Upload theme");
            DrawButton(g, 1, "Get starter file");
        }

        void DrawButton(Graphics g, int i, string label)
        {
            RectangleF r = ButtonRect(i);
            LiveWpm.Theme.Surface(g, hoverButton == i ? LiveWpm.Theme.CardHover : LiveWpm.Theme.Card, r, 9);
            LiveWpm.Theme.DrawTextCenter(g, label, LiveWpm.Theme.F(11.5f, true), LiveWpm.Theme.Text, r.X + r.Width / 2, r.Y + 8);
        }

        void DrawRemove(Graphics g, RectangleF r)
        {
            LiveWpm.Theme.FillRound(g, hoverRemove ? LiveWpm.Theme.Danger : Color.FromArgb(190, 0, 0, 0), r, 7);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using (Pen p = new Pen(Color.White, 1.5f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 3.5f, cy - 3.5f, cx + 3.5f, cy + 3.5f);
                g.DrawLine(p, cx + 3.5f, cy - 3.5f, cx - 3.5f, cy + 3.5f);
            }
        }

        // A tiny version of the floating speedometer in that theme's own colours.
        static void DrawPreview(Graphics g, Palette p, RectangleF t)
        {
            GraphicsState st = g.Save();
            using (GraphicsPath clip = LiveWpm.Theme.RoundExact(t, 10))
            {
                g.SetClip(clip);
                LiveWpm.Theme.PaintBackground(g, p, t);
                RectangleF card = new RectangleF(t.X + 12, t.Y + 12, t.Width - 24, t.Height - 24);
                using (GraphicsPath cp = LiveWpm.Theme.RoundExact(card, 8 * p.Radius))
                {
                    using (SolidBrush cb = new SolidBrush(p.Card)) g.FillPath(cb, cp);
                    Color edge = p.Glass ? Color.FromArgb(200, 255, 255, 255) : p.Outline;
                    if (edge.A > 0) using (Pen ep = new Pen(edge, 1)) g.DrawPath(ep, cp);
                }

                float cx = card.X + card.Width / 2, cy = card.Y + card.Height / 2 + 6, r = 20;
                RectangleF arc = new RectangleF(cx - r, cy - r, r * 2, r * 2);
                using (Pen track = new Pen(p.Track, 4)) { track.StartCap = track.EndCap = LineCap.Round; g.DrawArc(track, arc, 135, 270); }
                using (Pen val = new Pen(p.DialColor, 4)) { val.StartCap = val.EndCap = LineCap.Round; g.DrawArc(val, arc, 135, 170); }
                double a = (135 + 170) * Math.PI / 180;
                using (SolidBrush tip = new SolidBrush(p.Text))
                    g.FillEllipse(tip, cx + (float)Math.Cos(a) * r - 3, cy + (float)Math.Sin(a) * r - 3, 6, 6);
                using (SolidBrush tb = new SolidBrush(p.Text)) g.FillRectangle(tb, card.X + 8, card.Y + 7, 16, 4);
                using (SolidBrush mb = new SolidBrush(p.Accent)) g.FillRectangle(mb, card.X + 27, card.Y + 7, 12, 4);
                if (p.Cute) LiveWpm.Theme.Heart(g, p.Accent, card.Right - 12, card.Y + 9, 7);
            }
            g.Restore(st);
            if (p.Light) LiveWpm.Theme.StrokeRound(g, Color.FromArgb(28, 0, 0, 0), new RectangleF(t.X + 0.5f, t.Y + 0.5f, t.Width - 1, t.Height - 1), 10, 1);
        }

        int HitTile(float x, float y)
        {
            for (int i = 0; i < LiveWpm.Theme.ThemeCount; i++)
            {
                RectangleF t = Tile(i);
                if (new RectangleF(t.X, t.Y, t.Width, t.Height + NameH).Contains(x, y)) return i;
            }
            return -1;
        }

        static int HitButton(float x, float y)
        {
            for (int i = 0; i < 2; i++) if (ButtonRect(i).Contains(x, y)) return i;
            return -1;
        }

        protected override void OnMouseMove(float x, float y)
        {
            bool hc = CloseRect.Contains(x, y);
            int ht = HitTile(x, y), hb = HitButton(x, y);
            bool hr = ht >= 0 && IsCustom(ht) && RemoveRect(ht).Contains(x, y);
            if (hc != hoverClose || ht != hover || hb != hoverButton || hr != hoverRemove)
            {
                hoverClose = hc; hover = ht; hoverButton = hb; hoverRemove = hr;
                SetHandCursor(hc || ht >= 0 || hb >= 0);
                Invalidate();
            }
        }

        protected override void OnMouseLeave()
        {
            if (hoverClose || hover >= 0 || hoverButton >= 0)
            {
                hoverClose = hoverRemove = false; hover = hoverButton = -1;
                Invalidate();
            }
        }

        protected override async void OnMouseDown(float x, float y, MouseButton b)
        {
            if (b != MouseButton.Left || busy) return;
            if (CloseRect.Contains(x, y)) { Close(); return; }
            busy = true;
            try
            {
                int bt = HitButton(x, y);
                if (bt == 0) { await Upload(); return; }
                if (bt == 1) { await SaveStarter(); return; }
                int t = HitTile(x, y);
                if (t < 0) { BeginDrag(); return; }
                if (IsCustom(t) && RemoveRect(t).Contains(x, y)) { await RemoveTheme(LiveWpm.Theme.ThemeAt(t)); return; }
                current = LiveWpm.Theme.ThemeAt(t).Id;
                choose(current);
                Invalidate();
            }
            finally { busy = false; }
        }

        static readonly FilePickerFileType ThemeType = new FilePickerFileType("Live WPM theme")
        {
            Patterns = new[] { "*" + ThemeFiles.Extension }, MimeTypes = new[] { "text/plain" }
        };
        static readonly FilePickerFileType TextType = new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } };

        async Task Upload()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Upload a Live WPM theme",
                AllowMultiple = false,
                FileTypeFilter = new[] { ThemeType, TextType }
            });
            if (files == null || files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) { await MessageWindow.Info(this, "That file couldn't be opened. Choose a theme file saved on this PC."); return; }

            // Import asks about replacing a theme of the same name; answer that first, then import.
            string error;
            Palette same = null;
            Palette probe = ThemeFiles.Parse(SafeRead(path), out error);
            if (probe != null) foreach (Palette p in LiveWpm.Theme.Custom) if (string.Equals(p.Name, probe.Name, StringComparison.OrdinalIgnoreCase)) same = p;
            bool replace = same == null || await MessageWindow.Confirm(this, "You already have a theme called \"" + same.Name + "\". Replace it?");
            if (!replace) return;
            Palette added = ThemeFiles.Import(path, delegate(string name) { return true; }, out error);
            if (error != null) { await MessageWindow.Info(this, error); return; }
            if (added == null) return;
            ThemesChanged(added.Id);
        }

        static string SafeRead(string path)
        {
            try { return new FileInfo(path).Length > 16 * 1024 ? "" : File.ReadAllText(path); } catch { return ""; }
        }

        async Task SaveStarter()
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save a starter theme",
                SuggestedFileName = "My Theme" + ThemeFiles.Extension,
                DefaultExtension = ThemeFiles.Extension.TrimStart('.'),
                FileTypeChoices = new[] { ThemeType }
            });
            if (file == null) return;
            string path = file.TryGetLocalPath();
            try
            {
                if (path == null) throw new IOException("Choose a folder on this PC.");
                File.WriteAllText(path, ThemeFiles.Starter.Replace(" or in Paint", "").Replace("\r\n", "\n"));
                await MessageWindow.Info(this, "Saved. Open it in a text editor, change the colours and name, save it, then choose Upload theme.");
            }
            catch (Exception ex)
            {
                await MessageWindow.Info(this, "The file couldn't be saved:\n" + ex.Message);
            }
        }

        async Task RemoveTheme(Palette p)
        {
            if (!await MessageWindow.Confirm(this, "Remove \"" + p.Name + "\" from Live WPM? A copy you saved elsewhere isn't affected.")) return;
            try { ThemeFiles.Remove(p); }
            catch (Exception ex)
            {
                await MessageWindow.Info(this, "The theme couldn't be removed:\n" + ex.Message);
                return;
            }
            ThemesChanged(current == p.Id ? LiveWpm.Theme.DefaultTheme : current);
        }

        void ThemesChanged(string id)
        {
            hover = hoverButton = -1;
            hoverRemove = false;
            current = id;
            SetLogicalSize(W, H);
            choose(id);
            Invalidate();
        }
    }
}
