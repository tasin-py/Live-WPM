using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LiveWpm
{
    // LongPress = a character key was held down for a long time (typical in games, rare in typing).
    enum KeyKind { Char, WordBreak, Backspace, LongPress }

    // Global low-level keyboard hook.
    //
    // PRIVACY: the callback only decides which *kind* of key was pressed (a character,
    // a word break or a backspace) and immediately forgets the key itself. No key codes,
    // characters or text are ever stored, queued, logged or written to disk. The only
    // per-key state is "is this key held down right now, and since when" - needed to skip
    // auto-repeat and spot game-style holds - and it is cleared the moment the key is released.
    // When tracking is off the hook is removed from Windows entirely, so nothing runs at all.
    sealed class KeyboardHook : IDisposable
    {
        public event Action<KeyKind> Key;

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vk);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);

        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
        const int LongPressMs = 400;     // typists tap keys for ~100 ms; games hold them
        const int StuckKeyMs = 10000;    // forget a "held" key whose release we somehow missed

        const int RearmMs = 5000;        // how often the hook is re-attached (see Watch)

        // Full-screen windows of these apps are still typing (F11 browser, zen-mode editors).
        static readonly string[] TypingApps = {
            "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi", "arc",
            "code", "cursor", "devenv", "notepad", "notepad++", "sublime_text", "winword",
            "obsidian", "notion", "discord", "slack", "telegram", "whatsapp", "windowsterminal" };

        IntPtr hook = IntPtr.Zero;
        readonly HookProc proc;
        readonly long[] downSince = new long[256]; // 0 = key is up
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly System.Windows.Forms.Timer watch = new System.Windows.Forms.Timer();
        long rearmedAt;
        bool fullscreen;                 // kept up to date by Watch, only read by the hook
        IntPtr lastFg;
        bool lastFgTypingApp;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")]
        static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int max);
        [DllImport("user32.dll")]
        static extern bool IsZoomed(IntPtr h);
        [DllImport("shell32.dll")]
        static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

        public KeyboardHook()
        {
            proc = Callback; // keep the delegate alive while Windows holds it
            // Compile the key path now: compiling it on the first keypress can be slow enough
            // for Windows to drop the brand-new hook.
            foreach (string name in new[] { "Callback", "Handle", "Classify", "Down" })
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(
                    typeof(KeyboardHook).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).MethodHandle);
            watch.Interval = 1000;
            watch.Tick += delegate { Watch(); };
        }

        public bool Active { get { return hook != IntPtr.Zero; } }

        public void Start()
        {
            if (hook != IntPtr.Zero) return;
            hook = Install();
            rearmedAt = clock.ElapsedMilliseconds;
            fullscreen = false;
            lastFg = IntPtr.Zero;
            watch.Start();
        }

        public void Stop()
        {
            watch.Stop();
            if (hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
            Array.Clear(downSince, 0, downSince.Length);
        }

        public void Dispose() { Stop(); watch.Dispose(); }

        IntPtr Install()
        {
            using (ProcessModule m = Process.GetCurrentProcess().MainModule)
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(m.ModuleName), 0);
        }

        // Runs once a second on the UI thread while tracking, so the hook itself never waits on anything.
        void Watch()
        {
            fullscreen = GameInFront();

            // Windows silently drops a low-level hook that answers too slowly (a busy PC, waking
            // from sleep) and never says so. Re-attaching it every few seconds - new hook first,
            // then the old one removed, so no key slips through - brings tracking back on its own.
            long now = clock.ElapsedMilliseconds;
            if (now - rearmedAt < RearmMs) return;
            rearmedAt = now;
            IntPtr fresh = Install();
            if (fresh == IntPtr.Zero) return;
            UnhookWindowsHookEx(hook);
            hook = fresh;
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0) Handle((int)wParam, Marshal.ReadInt32(lParam) & 0xFF);
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        void Handle(int msg, int vk)
        {
            long now = clock.ElapsedMilliseconds + 1; // +1 so a real timestamp is never 0
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                long since = downSince[vk];
                if (since != 0 && now - since < StuckKeyMs) return; // Windows auto-repeat of a held key
                downSince[vk] = now;
                if (fullscreen) return;                              // a game fills the screen
                int kind = Classify(vk);
                if (kind >= 0 && Key != null) Key((KeyKind)kind);
            }
            else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
            {
                long since = downSince[vk];
                downSince[vk] = 0;
                if (since != 0 && now - since >= LongPressMs && Classify(vk) == (int)KeyKind.Char && Key != null)
                    Key(KeyKind.LongPress);
            }
        }

        // True when a game is in front: exclusive full-screen DirectX, or a borderless window
        // filling its monitor that isn't a browser or editor (those go full-screen for typing too).
        bool GameInFront()
        {
            try
            {
                int state;
                if (SHQueryUserNotificationState(out state) == 0 && state == 3) return true; // D3D full-screen

                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == GetShellWindow() || fg == GetDesktopWindow()) return false;
                System.Text.StringBuilder cls = new System.Text.StringBuilder(64);
                GetClassName(fg, cls, cls.Capacity);
                string c = cls.ToString();
                if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
                if (IsZoomed(fg)) return false; // a maximized editor/browser is normal typing, not full-screen

                RECT r;
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (!GetWindowRect(fg, out r) || !GetMonitorInfo(MonitorFromWindow(fg, 2), ref mi)) return false;
                bool fills = r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top &&
                             r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
                return fills && !IsTypingApp(fg);
            }
            catch { return false; }
        }

        // Looks up the window's program once per window, not every second.
        bool IsTypingApp(IntPtr fg)
        {
            if (fg == lastFg) return lastFgTypingApp;
            lastFg = fg;
            lastFgTypingApp = false;
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                    lastFgTypingApp = Array.IndexOf(TypingApps, p.ProcessName.ToLowerInvariant()) >= 0;
            }
            catch { }
            return lastFgTypingApp;
        }

        static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        // Returns -1 for keys that are not typing (shortcuts, arrows, F-keys, modifiers...).
        static int Classify(int vk)
        {
            bool ctrl = Down(0x11), alt = Down(0x12);
            bool win = Down(0x5B) || Down(0x5C);
            if (win) return -1;
            if (ctrl != alt) return -1;            // Ctrl+C, Alt+Tab... (Ctrl+Alt = AltGr is still typing)

            if (vk == 0x08) return (int)KeyKind.Backspace;
            if (vk == 0x20 || vk == 0x0D || vk == 0x09) return (int)KeyKind.WordBreak;
            if ((vk >= 0x30 && vk <= 0x39) ||      // 0-9
                (vk >= 0x41 && vk <= 0x5A) ||      // A-Z
                (vk >= 0x60 && vk <= 0x6F) ||      // numpad
                (vk >= 0xBA && vk <= 0xC0) ||      // ; = , - . / `
                (vk >= 0xDB && vk <= 0xDF) ||      // [ \ ] '
                vk == 0xE2)
                return (int)KeyKind.Char;
            return -1;
        }
    }
}
