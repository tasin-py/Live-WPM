using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LiveWpm
{
    enum KeyboardAccess { Off, Ok, NoPermission, NoKeyboard }

    // Reads the keyboard on Linux, under X11 and Wayland alike, from the kernel's input devices
    // (/dev/input/event*). Needs the Typer to be in the "input" group, once.
    //
    // PRIVACY: the same rule as the Windows hook. Each event is turned straight into the *kind* of
    // key (a character, a word break or a backspace) and the key itself is forgotten. No key codes,
    // characters or text are ever stored, queued, logged or written anywhere. The only per-key state
    // is "is this key held down right now, and since when", cleared when the key is released.
    // When tracking is off every device is closed (within half a second) and nothing more is read.
    sealed class EvdevKeyboard : IDisposable
    {
        public event Action<KeyKind> Key;          // raised on a reader thread
        public event Action AccessChanged;         // raised on a reader/scan thread

        const int LongPressMs = 400;               // typists tap keys for ~100 ms; games hold them
        const int StuckKeyMs = 10000;
        const int RescanMs = 3000;                 // picks up keyboards plugged in later
        const int PollMs = 500;                    // how soon a reader notices tracking was switched off
        const ushort EV_KEY = 1;

        // KEY_* codes from linux/input-event-codes.h
        const int KEY_BACKSPACE = 14, KEY_TAB = 15, KEY_ENTER = 28, KEY_SPACE = 57, KEY_KPENTER = 96;
        const int KEY_LEFTCTRL = 29, KEY_RIGHTCTRL = 97, KEY_LEFTALT = 56, KEY_LEFTMETA = 125, KEY_RIGHTMETA = 126;

        // ---- libc -------------------------------------------------------------
        const int O_RDONLY = 0, O_NONBLOCK = 0x800, O_CLOEXEC = 0x80000;
        const short POLLIN = 1;
        const int EACCES = 13, EPERM = 1, EAGAIN = 11, EINTR = 4;

        [StructLayout(LayoutKind.Sequential)]
        struct PollFd { public int fd; public short events; public short revents; }

        [DllImport("libc", SetLastError = true)] static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
        [DllImport("libc", SetLastError = true)] static extern int close(int fd);
        [DllImport("libc", SetLastError = true)] static extern IntPtr read(int fd, byte[] buf, IntPtr count);
        [DllImport("libc", SetLastError = true)] static extern int poll(ref PollFd fds, ulong nfds, int timeout);

        sealed class Device { public int Fd; public volatile bool Alive = true; }

        readonly object gate = new object();
        readonly Dictionary<string, Device> open_ = new Dictionary<string, Device>();
        readonly long[] downSince = new long[768];
        readonly Stopwatch clock = Stopwatch.StartNew();
        Timer scan;
        volatile bool running;
        int ctrl, alt, meta;                       // how many of each modifier are held, across keyboards

        public KeyboardAccess Access { get; private set; }
        public int Count { get { lock (gate) return open_.Count; } }

        // Where devices are listed; test tools point these elsewhere.
        public string DevicePath = "/dev/input";
        public string SysPath = "/sys/class/input";

        public void Start()
        {
            if (running) return;
            running = true;
            Scan();
            scan = new Timer(delegate { if (running) Scan(); }, null, RescanMs, RescanMs);
        }

        public void Stop()
        {
            running = false;
            if (scan != null) { scan.Dispose(); scan = null; }
            lock (gate)
            {
                foreach (Device d in open_.Values) d.Alive = false;   // each reader closes its own device
                open_.Clear();
                Array.Clear(downSince, 0, downSince.Length);
                ctrl = alt = meta = 0;
            }
            SetAccess(KeyboardAccess.Off);
        }

        public void Dispose() { Stop(); }

        void SetAccess(KeyboardAccess a)
        {
            if (Access == a) return;
            Access = a;
            if (AccessChanged != null) AccessChanged();
        }

        // Finds keyboards (devices with letter keys) and starts reading any new ones.
        void Scan()
        {
            bool denied = false;
            string[] devices;
            try { devices = Directory.GetFiles(DevicePath, "event*"); }
            catch { devices = new string[0]; }
            foreach (string dev in devices)
            {
                lock (gate) if (open_.ContainsKey(dev)) continue;
                if (!IsKeyboard(Path.GetFileName(dev))) continue;
                int fd = open(dev, O_RDONLY | O_NONBLOCK | O_CLOEXEC);
                if (fd < 0)
                {
                    int err = Marshal.GetLastPInvokeError();
                    if (err == EACCES || err == EPERM) denied = true;
                    continue;
                }
                Device d = new Device { Fd = fd };
                lock (gate)
                {
                    if (!running) { close(fd); return; }
                    open_[dev] = d;
                }
                string path = dev;
                new Thread(delegate() { Read(path, d); }) { IsBackground = true, Name = "keyboard " + Path.GetFileName(dev) }.Start();
            }
            if (!running) return;
            int n = Count;
            SetAccess(n > 0 ? KeyboardAccess.Ok : denied ? KeyboardAccess.NoPermission : KeyboardAccess.NoKeyboard);
        }

        // A keyboard reports the letter keys Q, A and Z and the space bar in its key capabilities
        // (a hex bitmap, most significant word first).
        internal bool IsKeyboard(string eventName)
        {
            try
            {
                string caps = File.ReadAllText(Path.Combine(SysPath, eventName, "device", "capabilities", "key")).Trim();
                if (caps.Length == 0) return false;
                string[] words = caps.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Func<int, bool> has = delegate(int code)
                {
                    int bits = IntPtr.Size * 8, w = code / bits;
                    if (w >= words.Length) return false;
                    ulong word = Convert.ToUInt64(words[words.Length - 1 - w], 16);
                    return (word >> (code % bits) & 1) != 0;
                };
                return has(16) && has(30) && has(44) && has(KEY_SPACE);
            }
            catch { return false; }
        }

        // One thread per keyboard. Events are 24 bytes on 64-bit Linux: time (16), type, code, value.
        void Read(string dev, Device d)
        {
            byte[] ev = new byte[24 * 16];
            try
            {
                while (d.Alive)
                {
                    PollFd p = new PollFd { fd = d.Fd, events = POLLIN };
                    int ready = poll(ref p, 1, PollMs);
                    if (!d.Alive) break;
                    if (ready < 0) { if (Marshal.GetLastPInvokeError() == EINTR) continue; break; }
                    if (ready == 0) continue;
                    long got = (long)read(d.Fd, ev, (IntPtr)ev.Length);
                    if (got < 0)
                    {
                        int err = Marshal.GetLastPInvokeError();
                        if (err == EAGAIN || err == EINTR) continue;
                        break;                     // unplugged
                    }
                    if (got == 0) break;
                    if (!d.Alive) break;           // switched off while reading: these events are dropped unseen
                    for (int i = 0; i + 24 <= got; i += 24)
                    {
                        if (BitConverter.ToUInt16(ev, i + 16) != EV_KEY) continue;
                        Handle(BitConverter.ToUInt16(ev, i + 18), BitConverter.ToInt32(ev, i + 20));
                    }
                    Array.Clear(ev, 0, ev.Length);
                }
            }
            catch { }
            close(d.Fd);
            lock (gate)
            {
                Device mine;
                if (open_.TryGetValue(dev, out mine) && mine == d) open_.Remove(dev);
            }
            if (running) SetAccess(Count > 0 ? KeyboardAccess.Ok : KeyboardAccess.NoKeyboard);
        }

        // value: 1 = pressed, 0 = released, 2 = auto-repeat (ignored, as on Windows).
        internal void Handle(int code, int value)
        {
            if (code < 0 || code >= downSince.Length || value == 2) return;
            int kind = -1;
            lock (gate)
            {
                long now = clock.ElapsedMilliseconds + 1;
                if (value == 1)
                {
                    Modifier(code, 1);
                    long since = downSince[code];
                    if (since != 0 && now - since < StuckKeyMs) return;
                    downSince[code] = now;
                    kind = Classify(code);
                }
                else if (value == 0)
                {
                    Modifier(code, -1);
                    long since = downSince[code];
                    downSince[code] = 0;
                    if (since != 0 && now - since >= LongPressMs && IsChar(code)) kind = (int)KeyKind.LongPress;
                }
            }
            if (kind >= 0 && Key != null) Key((KeyKind)kind);
        }

        void Modifier(int code, int d)
        {
            if (code == KEY_LEFTCTRL || code == KEY_RIGHTCTRL) ctrl = Math.Max(0, ctrl + d);
            else if (code == KEY_LEFTALT) alt = Math.Max(0, alt + d);
            else if (code == KEY_LEFTMETA || code == KEY_RIGHTMETA) meta = Math.Max(0, meta + d);
        }

        // Returns -1 for keys that are not typing (shortcuts, arrows, F-keys, modifiers...).
        // AltGr (right Alt) is typing; Ctrl+C, Alt+Tab and Super shortcuts are not.
        int Classify(int code)
        {
            if (meta > 0) return -1;
            if ((ctrl > 0) != (alt > 0)) return -1;
            if (code == KEY_BACKSPACE) return (int)KeyKind.Backspace;
            if (code == KEY_SPACE || code == KEY_ENTER || code == KEY_KPENTER || code == KEY_TAB) return (int)KeyKind.WordBreak;
            if (IsChar(code)) return (int)KeyKind.Char;
            return -1;
        }

        static bool IsChar(int code)
        {
            return (code >= 2 && code <= 13)      // 1-0 - =
                || (code >= 16 && code <= 27)     // q-p [ ]
                || (code >= 30 && code <= 41)     // a-l ; ' `
                || (code >= 43 && code <= 53)     // \ z-m , . /
                || code == 55                     // keypad *
                || (code >= 71 && code <= 83)     // keypad digits - + .
                || code == 86                     // the extra key on ISO keyboards
                || code == 98;                    // keypad /
        }
    }
}
