using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace LiveWpm
{
    // The app's own message and yes/no boxes, in the current theme. "No" is the default
    // (Enter and Esc), as on Windows, so a reset can't happen by accident.
    sealed class MessageWindow : PaintedWindow
    {
        const float W = 400, Pad = 24, ButtonH = 34, ButtonW = 96;
        readonly string message;
        readonly string[] buttons;    // right to left: [0] is the default
        readonly float textH, h;
        int hover = -1;
        int result = 0;

        MessageWindow(string message, string[] buttons)
        {
            this.message = message;
            this.buttons = buttons;
            SystemDecorations = SystemDecorations.None;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            UseTransparency(false);
            RoundCorners = 12;
            textH = LiveWpm.Theme.DrawTextWrapped(null, message, LiveWpm.Theme.F(13, false), LiveWpm.Theme.Text, 0, 0, W - Pad * 2);
            h = Pad + 30 + textH + 22 + ButtonH + Pad;
            SetLogicalSize(W, h);
        }

        RectangleF ButtonRect(int i) { return new RectangleF(W - Pad - ButtonW - i * (ButtonW + 10), h - Pad - ButtonH, ButtonW, ButtonH); }

        protected override void OnPaint(Graphics g)
        {
            LiveWpm.Theme.PaintBackground(g, W, h);
            if (SeeThrough) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Border, new RectangleF(0.5f, 0.5f, W - 1, h - 1), 12, 1);
            LiveWpm.Theme.DrawText(g, LiveWpm.Theme.AppName, LiveWpm.Theme.F(15, true), LiveWpm.Theme.Text, Pad, Pad - 2);
            LiveWpm.Theme.DrawTextWrapped(g, message, LiveWpm.Theme.F(13, false), LiveWpm.Theme.Muted, Pad, Pad + 30, W - Pad * 2);
            for (int i = 0; i < buttons.Length; i++)
            {
                RectangleF r = ButtonRect(i);
                bool main = buttons.Length > 1 ? i == 1 : true;   // "Yes" / "OK" stands out
                Color fill = main ? (hover == i ? LiveWpm.Theme.Lerp(LiveWpm.Theme.Accent, Color.White, 0.15) : LiveWpm.Theme.Accent)
                                  : (hover == i ? LiveWpm.Theme.Lerp(LiveWpm.Theme.CardHover, LiveWpm.Theme.Accent, 0.25) : LiveWpm.Theme.CardHover);
                if (main) LiveWpm.Theme.FillRound(g, fill, r, 8); else LiveWpm.Theme.FillRound(g, fill, r, 8);
                if (i == 0 && buttons.Length > 1) LiveWpm.Theme.StrokeRound(g, LiveWpm.Theme.Muted, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), 8, 1);
                LiveWpm.Theme.DrawTextCenter(g, buttons[i], LiveWpm.Theme.F(12.5f, true), main ? LiveWpm.Theme.OnAccent : LiveWpm.Theme.Text, r.X + r.Width / 2, r.Y + 9);
            }
        }

        int HitButton(float x, float y)
        {
            for (int i = 0; i < buttons.Length; i++) if (ButtonRect(i).Contains(x, y)) return i;
            return -1;
        }

        protected override void OnMouseMove(float x, float y)
        {
            int b = HitButton(x, y);
            if (b != hover) { hover = b; SetHandCursor(b >= 0); Invalidate(); }
        }

        protected override void OnMouseLeave() { if (hover != -1) { hover = -1; Invalidate(); } }

        protected override void OnMouseDown(float x, float y, MouseButton b)
        {
            if (b != MouseButton.Left) return;
            int hit = HitButton(x, y);
            if (hit >= 0) { result = hit; Close(); }
            else BeginDrag();
        }

        protected override void OnKey(Key k)
        {
            if (k == Key.Escape || k == Key.Enter) { result = 0; Close(); }
        }

        // Yes/No. Returns true only for Yes.
        public static async Task<bool> Confirm(Window owner, string question)
        {
            MessageWindow m = new MessageWindow(question, new[] { "No", "Yes" });
            await Show(m, owner);
            return m.result == 1;
        }

        public static async Task Info(Window owner, string text)
        {
            MessageWindow m = new MessageWindow(text, new[] { "OK" });
            await Show(m, owner);
        }

        static Task Show(Window w, Window owner)
        {
            if (owner != null && owner.IsVisible) return w.ShowDialog(owner);
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.Closed += delegate { done.TrySetResult(true); };
            w.Show();
            return done.Task;
        }
    }
}
