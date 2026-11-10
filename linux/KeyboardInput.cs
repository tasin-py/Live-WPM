using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace LiveWpm
{
    enum KeyboardMode { None, Service, Direct }

    // Where Live WPM gets its key kinds from on Linux:
    //  1. the keyboard service (live-wpm-keys), when it's installed: the safe way, nothing extra for your account;
    //  2. otherwise, reading the keyboard devices itself, which only works if the Typer chose to put their
    //     account in the "input" group (an advanced fallback: it lets every program they run read keys).
    // Either way only the kind of each key ever arrives here.
    sealed class KeyboardInput : IDisposable
    {
        public event Action<KeyKind> Key;          // raised on a background thread
        public event Action AccessChanged;         // raised on a background thread

        public string SocketPath = "/run/live-wpm/keys.sock";
        const int RetryMs = 3000;

        readonly EvdevKeyboard direct = new EvdevKeyboard();
        readonly object gate = new object();
        volatile bool running;
        Socket socket;
        Timer retry;

        // The fallback reader (test tools point it at fake devices).
        internal EvdevKeyboard Direct { get { return direct; } }

        public KeyboardAccess Access { get; private set; }
        public KeyboardMode Mode { get; private set; }
        public int Count { get; private set; }

        public KeyboardInput()
        {
            direct.Key += delegate(KeyKind k) { if (Key != null) Key(k); };
            direct.AccessChanged += delegate { if (Mode == KeyboardMode.Direct) Set(direct.Access, direct.Count); };
        }

        // True when the keyboard service is installed on this PC (even if not switched on yet).
        public static bool ServiceInstalled
        {
            get
            {
                return File.Exists("/usr/lib/systemd/system/live-wpm-keys.socket") ||
                       File.Exists("/etc/systemd/system/live-wpm-keys.socket");
            }
        }

        public void Start()
        {
            if (running) return;
            running = true;
            Connect();
            retry = new Timer(delegate { if (running && Mode != KeyboardMode.Service) Connect(); }, null, RetryMs, RetryMs);
        }

        public void Stop()
        {
            running = false;
            if (retry != null) { retry.Dispose(); retry = null; }
            lock (gate) { if (socket != null) { try { socket.Dispose(); } catch { } socket = null; } }
            direct.Stop();
            Mode = KeyboardMode.None;
            Set(KeyboardAccess.Off, 0);
        }

        public void Dispose() { Stop(); }

        void Set(KeyboardAccess a, int count)
        {
            if (Access == a && Count == count) return;
            Access = a; Count = count;
            if (AccessChanged != null) AccessChanged();
        }

        void Connect()
        {
            lock (gate)
            {
                if (!running || socket != null) return;
                if (File.Exists(SocketPath))
                {
                    Socket s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    try
                    {
                        s.Connect(new UnixDomainSocketEndPoint(SocketPath));
                        socket = s;
                        if (Mode == KeyboardMode.Direct) direct.Stop();   // the service is here now: prefer it
                        Mode = KeyboardMode.Service;
                        new Thread(delegate() { Listen(s); }) { IsBackground = true, Name = "keyboard service" }.Start();
                        return;
                    }
                    catch { s.Dispose(); }
                }
            }
            if (Mode == KeyboardMode.Direct) return;
            // No service: read the devices directly, if this account is allowed to.
            direct.Start();
            if (direct.Access == KeyboardAccess.NoPermission)
            {
                direct.Stop();
                Mode = KeyboardMode.None;
                Set(KeyboardAccess.NoPermission, 0);
            }
            else
            {
                Mode = KeyboardMode.Direct;
                Set(direct.Access, direct.Count);
            }
        }

        void Listen(Socket s)
        {
            byte[] buf = new byte[64];
            int pending = 0;            // bytes still to read of an 'S' status message
            byte access = 0;
            try
            {
                while (running)
                {
                    int n = s.Receive(buf);
                    if (n <= 0) break;
                    for (int i = 0; i < n; i++)
                    {
                        byte b = buf[i];
                        if (pending == 2) { access = b; pending = 1; continue; }
                        if (pending == 1) { pending = 0; Set((KeyboardAccess)access, b); continue; }
                        switch ((char)b)
                        {
                            case 'S': pending = 2; break;
                            case 'D': Set(KeyboardAccess.NotAllowed, 0); break;
                            case 'C': Raise(KeyKind.Char); break;
                            case 'W': Raise(KeyKind.WordBreak); break;
                            case 'B': Raise(KeyKind.Backspace); break;
                            case 'L': Raise(KeyKind.LongPress); break;
                        }
                    }
                }
            }
            catch { }
            lock (gate)
            {
                if (socket == s) socket = null;
                try { s.Dispose(); } catch { }
            }
            // The service went away (systemd starts it again on demand) or refused this session
            // (another user is at the screen): try again shortly.
            if (running) Mode = KeyboardMode.None;
        }

        void Raise(KeyKind k) { if (Key != null) Key(k); }
    }
}
