using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace LiveWpm
{
    // The Live WPM keyboard service: `live-wpm --keyboard-service`, started by systemd
    // (packaging/live-wpm-keys.socket and .service) when Live WPM connects to /run/live-wpm/keys.sock.
    //
    // Why it exists: reading the keyboard on Linux needs the "input" group. Putting your own account in
    // that group would let EVERY program you run read every key you press. Instead, only this service
    // gets that access, and systemd locks it down: a temporary user that exists only while it runs,
    // no network at all, a read-only system, no home folders, and no access to anything but input devices.
    //
    // What it sends: only the kind of each key (C = character, W = word break, B = backspace,
    // L = a long hold), the same four kinds the Windows app uses, plus how many keyboards it reads.
    // Never which key. It reads the keyboards only while Live WPM is connected, and it answers only
    // the user who is logged in at the screen right now: not remote (SSH) logins, and not a user
    // who has switched away.
    //
    // Protocol, one byte each:  'C' 'W' 'B' 'L'  key kinds
    //                           'S' access count  status (access: KeyboardAccess as a byte)
    //                           'D'               refused: not the active local user (then closed)
    static class KeyboardService
    {
        const string DefaultSocket = "/run/live-wpm/keys.sock";
        const int IdleExitSeconds = 30;   // no client for this long: exit (systemd starts it again on demand)

        sealed class Client
        {
            public Socket Socket;
            public uint Uid;
            public volatile bool Active;    // its user is at the screen right now
        }

        static readonly object gate = new object();
        static readonly List<Client> clients = new List<Client>();
        static EvdevKeyboard keyboard;
        static string sessionsDir = "/run/systemd/sessions";
        static DateTime idleSince = DateTime.UtcNow;

        public static int Run(string[] args)
        {
            keyboard = new EvdevKeyboard();
            string devices = Arg(args, "--devices"), sys = Arg(args, "--sys"), sessions = Arg(args, "--sessions");
            if (devices != null) keyboard.DevicePath = devices;
            if (sys != null) keyboard.SysPath = sys;
            if (sessions != null) sessionsDir = sessions;

            Socket listener = FromSystemd() ?? Listen(Arg(args, "--socket") ?? DefaultSocket);
            if (listener == null) { Console.Error.WriteLine("live-wpm keyboard service: no socket to listen on"); return 1; }

            keyboard.Key += delegate(KeyKind k) { Broadcast((byte)"CWBL"[(int)k]); };
            keyboard.AccessChanged += SendStatus;
            new Thread(Watch) { IsBackground = true, Name = "watch" }.Start();

            while (true)
            {
                Socket s;
                try { s = listener.Accept(); }
                catch { return 0; }
                Admit(s);
            }
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // Socket activation: systemd hands over the listening socket as fd 3.
        static Socket FromSystemd()
        {
            if (Environment.GetEnvironmentVariable("LISTEN_PID") != Environment.ProcessId.ToString()) return null;
            if (Environment.GetEnvironmentVariable("LISTEN_FDS") != "1") return null;
            return new Socket(new SafeSocketHandle((IntPtr)3, true));
        }

        static Socket Listen(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                Socket s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                s.Bind(new UnixDomainSocketEndPoint(path));
                s.Listen(8);
                return s;
            }
            catch { return null; }
        }

        // ---- who may connect ----------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)] struct UCred { public int Pid; public uint Uid; public uint Gid; }
        [DllImport("libc", SetLastError = true)] static extern int getsockopt(int fd, int level, int name, out UCred cred, ref int len);

        static bool PeerUid(Socket s, out uint uid)
        {
            UCred c;
            int len = Marshal.SizeOf(typeof(UCred));
            bool ok = getsockopt((int)s.Handle, 1 /* SOL_SOCKET */, 17 /* SO_PEERCRED */, out c, ref len) == 0;
            uid = c.Uid;
            return ok;
        }

        // True when this user has an active, local, graphical login (systemd-logind's own records).
        internal static bool AtTheScreen(uint uid, string dir)
        {
            try
            {
                foreach (string file in Directory.GetFiles(dir))
                {
                    if (file.EndsWith(".ref")) continue;
                    Dictionary<string, string> v = new Dictionary<string, string>();
                    foreach (string line in File.ReadAllLines(file))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) v[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
                    string u, active, remote, type, cls;
                    v.TryGetValue("UID", out u); v.TryGetValue("ACTIVE", out active); v.TryGetValue("REMOTE", out remote);
                    v.TryGetValue("TYPE", out type); v.TryGetValue("CLASS", out cls);
                    if (u == uid.ToString() && active == "1" && remote != "1" &&
                        (type == "x11" || type == "wayland" || type == "mir") && (cls == null || cls.StartsWith("user")))
                        return true;
                }
            }
            catch { }
            return false;
        }

        static void Admit(Socket s)
        {
            uint uid;
            if (!PeerUid(s, out uid) || !AtTheScreen(uid, sessionsDir))
            {
                try { s.Send(new[] { (byte)'D' }); } catch { }
                s.Dispose();
                return;
            }
            Client c = new Client { Socket = s, Uid = uid, Active = true };
            s.SendTimeout = 1000;
            bool first;
            lock (gate) { clients.Add(c); first = clients.Count == 1; }
            if (first) keyboard.Start();    // the keyboards are opened only while someone is listening
            SendStatus(c);
            new Thread(delegate() { WaitForClose(c); }) { IsBackground = true, Name = "client" }.Start();
        }

        // A client never sends anything; a read returning means it went away.
        static void WaitForClose(Client c)
        {
            byte[] b = new byte[16];
            try { while (c.Socket.Receive(b) > 0) { } } catch { }
            Drop(c);
        }

        static void Drop(Client c)
        {
            bool last;
            lock (gate)
            {
                if (!clients.Remove(c)) return;
                last = clients.Count == 0;
                if (last) idleSince = DateTime.UtcNow;
            }
            try { c.Socket.Dispose(); } catch { }
            if (last) keyboard.Stop();      // nobody listening: the keyboards are closed again
        }

        // ---- sending ------------------------------------------------------------------

        static void Broadcast(byte kind)
        {
            Client[] now;
            lock (gate) now = clients.ToArray();
            foreach (Client c in now)
                if (c.Active) Send(c, new[] { kind });
        }

        static void SendStatus()
        {
            Client[] now;
            lock (gate) now = clients.ToArray();
            foreach (Client c in now) SendStatus(c);
        }

        static void SendStatus(Client c)
        {
            Send(c, new[] { (byte)'S', (byte)keyboard.Access, (byte)Math.Min(255, keyboard.Count) });
        }

        static void Send(Client c, byte[] data)
        {
            try { c.Socket.Send(data); }
            catch { Drop(c); }
        }

        // Once a second: follow who is at the screen, and exit after a quiet spell.
        static void Watch()
        {
            while (true)
            {
                Thread.Sleep(1000);
                Client[] now;
                lock (gate) now = clients.ToArray();
                foreach (Client c in now) c.Active = AtTheScreen(c.Uid, sessionsDir);
                lock (gate)
                    if (clients.Count == 0 && (DateTime.UtcNow - idleSince).TotalSeconds > IdleExitSeconds)
                        Environment.Exit(0);
            }
        }
    }
}
