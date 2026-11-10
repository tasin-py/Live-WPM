using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // Asks for the "Typer Name" on first start (and from Settings later). Ported from src/NamePrompt.cs.
    sealed class NamePrompt : PaintedWindow
    {
        const int W = 380, H = 214;
        readonly TextBox box = new TextBox();
        readonly bool firstRun;
        readonly RectangleF btn = new RectangleF(24, 156, W - 48, 38);
        bool hoverBtn;
        public string Result;

        NamePrompt(string current, bool firstRun)
        {
            this.firstRun = firstRun;
            Title = firstRun ? "Welcome to " + LiveWpm.Theme.AppName : "Typer Name";
            WindowStartupLocation = firstRun ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            ShowInTaskbar = firstRun;
            Topmost = firstRun;
            SetLogicalSize(W, H);

            // A plain text box sitting on the painted field: no background or border of its own.
            IBrush clear = Brushes.Transparent;
            foreach (string key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused",
                                           "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
                box.Resources[key] = clear;
            IBrush text = new ImmutableSolidColorBrush(Conv.C(LiveWpm.Theme.Text));
            foreach (string key in new[] { "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused" })
                box.Resources[key] = text;
            box.Resources["TextControlSelectionHighlightColor"] = new ImmutableSolidColorBrush(Conv.C(Color.FromArgb(120, LiveWpm.Theme.Accent)));
            box.Background = clear;
            box.BorderThickness = new Thickness(0);
            box.Foreground = text;
            box.CaretBrush = text;
            box.FontFamily = new FontFamily("fonts:Inter#Inter");
            box.FontSize = 15;
            box.MaxLength = 32;
            box.Padding = new Thickness(0);
            box.MinHeight = 0;
            box.Width = W - 76;
            box.Height = 24;
            box.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
            box.Text = current;
            Canvas.SetLeft(box, 38);
            Canvas.SetTop(box, 108);
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; Submit(); }
                else if (e.Key == Key.Escape && !firstRun) { e.Handled = true; Close(); }
            };
            box.GotFocus += delegate { Invalidate(); };
            box.LostFocus += delegate { Invalidate(); };
            Overlay.Children.Add(box);
            Opened += delegate { Activate(); box.Focus(); box.SelectAll(); };
            Closing += delegate
            {
                // On first start a name is required; closing the window just uses "Typer".
                if (firstRun && Result == null) Result = (box.Text ?? "").Trim().Length > 0 ? box.Text.Trim() : "Typer";
            };
        }

        void Submit()
        {
            string n = (box.Text ?? "").Trim();
            if (n.Length == 0) { box.Focus(); return; }
            Result = n;
            Close();
        }

        protected override void OnPaint(Graphics g)
        {
            LiveWpm.Theme.PaintBackground(g, W, H);
            LiveWpm.Theme.DrawLogo(g, new RectangleF(24, 22, 30, 30), LiveWpm.Theme.LogoAccent);
            LiveWpm.Theme.DrawText(g, firstRun ? "Hey there, Typer!" : "Change your name", LiveWpm.Theme.F(19, true), LiveWpm.Theme.Text, 64, 18);
            LiveWpm.Theme.DrawText(g, firstRun ? "What should " + LiveWpm.Theme.AppName + " call you?" : "This is how " + LiveWpm.Theme.AppName + " greets you.",
                           LiveWpm.Theme.F(12, false), LiveWpm.Theme.Muted, 65, 43);
            LiveWpm.Theme.DrawText(g, "TYPER NAME", LiveWpm.Theme.F(10, true), LiveWpm.Theme.Muted, 26, 80);
            LiveWpm.Theme.FillRound(g, LiveWpm.Theme.CardSolid, new RectangleF(24, 98, W - 48, 44), 9);
            LiveWpm.Theme.StrokeRound(g, box.IsFocused ? LiveWpm.Theme.Accent : LiveWpm.Theme.Border, new RectangleF(24, 98, W - 48, 44), 9, 1.2f);
            if (hoverBtn) LiveWpm.Theme.FillRound(g, LiveWpm.Theme.Lerp(LiveWpm.Theme.Accent, Color.White, 0.2), btn, 9); else LiveWpm.Theme.FillAccent(g, btn, 9);
            LiveWpm.Theme.DrawTextCenter(g, firstRun ? "Let's type" : "Save", LiveWpm.Theme.F(13.5f, true), LiveWpm.Theme.OnAccent, btn.X + btn.Width / 2, btn.Y + 10);
        }

        protected override void OnMouseMove(float x, float y)
        {
            bool h = btn.Contains(x, y);
            if (h != hoverBtn) { hoverBtn = h; SetHandCursor(h); Invalidate(); }
        }

        protected override void OnMouseUp(float x, float y, MouseButton b)
        {
            if (btn.Contains(x, y)) Submit();
            else box.Focus();
        }

        // Returns the new name, or null if the Typer cancelled.
        public static async Task<string> Ask(string current, bool firstRun, Window owner)
        {
            NamePrompt p = new NamePrompt(current, firstRun);
            if (owner != null) await p.ShowDialog(owner);
            else
            {
                TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
                p.Closed += delegate { done.TrySetResult(true); };
                p.Show();
                await done.Task;
            }
            return p.Result;
        }
    }
}
