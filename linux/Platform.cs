using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LiveWpm
{
    // Small Linux helpers: links, notifications, start on login and the single-instance signal.
    static class Platform
    {
        // Where the app itself is: the AppImage when run as one, else this executable.
        public static string ExecutablePath
        {
            get { return Environment.GetEnvironmentVariable("APPIMAGE") ?? Environment.ProcessPath; }
        }

        static void Run(string file, params string[] args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file) { UseShellExecute = false };
                foreach (string a in args) psi.ArgumentList.Add(a);
                using (Process p = Process.Start(psi)) { }
            }
            catch { }
        }

        // Opens a web page in the Typer's own browser.
        public static void Open(string url) { Run("xdg-open", url); }

        // A desktop notification, where the desktop has notify-send (libnotify).
        public static void Notify(string title, string text) { Run("notify-send", "--app-name=" + Theme.AppName, "--icon=live-wpm", title, text); }

        // ---- start on login: an XDG autostart entry ----------------------------

        static string AutostartFile
        {
            get
            {
                string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(config)) config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(config, "autostart", "live-wpm.desktop");
            }
        }

        public static void SetAutostart(bool on)
        {
            try
            {
                if (!on) { if (File.Exists(AutostartFile)) File.Delete(AutostartFile); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(AutostartFile));
                File.WriteAllText(AutostartFile,
                    "[Desktop Entry]\nType=Application\nName=" + Theme.AppName + "\nComment=Live typing speedometer\n" +
                    "Exec=" + Quote(ExecutablePath) + " --tray\nIcon=live-wpm\nTerminal=false\nX-GNOME-Autostart-enabled=true\n");
            }
            catch { }
        }

        // Desktop-entry quoting: the path in double quotes, with " ` $ \ escaped.
        static string Quote(string path)
        {
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in path)
            {
                if (c == '"' || c == '`' || c == '$' || c == '\\') sb.Append('\\');
                sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        // ---- one copy at a time ----------------------------------------------------
        // The first copy listens on a socket that only this user can reach; starting the app
        // again just asks that copy to open its window.

        static Socket server;

        static string SocketPath
        {
            get
            {
                string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Path.GetTempPath();
                return Path.Combine(dir, "live-wpm-" + Environment.UserName + ".sock");
            }
        }

        // True when another copy is running (it has been asked to show itself).
        public static bool SignalRunningCopy()
        {
            try
            {
                using (Socket s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                {
                    s.Connect(new UnixDomainSocketEndPoint(SocketPath));
                    s.Send(Encoding.ASCII.GetBytes("show"));
                    return true;
                }
            }
            catch { return false; }
        }

        // After an update the old copy is still closing: wait up to 15 s for it to go.
        public static void WaitForOldCopy()
        {
            for (int i = 0; i < 60; i++)
            {
                if (!File.Exists(SocketPath)) return;
                try
                {
                    using (Socket s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                        s.Connect(new UnixDomainSocketEndPoint(SocketPath));
                }
                catch { return; }
                Thread.Sleep(250);
            }
        }

        public static void ListenForShow(Action show)
        {
            try
            {
                if (File.Exists(SocketPath)) File.Delete(SocketPath);   // left by a copy that crashed
                server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                server.Bind(new UnixDomainSocketEndPoint(SocketPath));
                server.Listen(4);
                new Thread(delegate()
                {
                    while (true)
                    {
                        try
                        {
                            using (Socket c = server.Accept())
                            {
                                byte[] buf = new byte[16];
                                c.ReceiveTimeout = 1000;
                                int n = c.Receive(buf);
                                if (n > 0 && Encoding.ASCII.GetString(buf, 0, n) == "show") show();
                            }
                        }
                        catch (ObjectDisposedException) { return; }
                        catch { }
                    }
                }) { IsBackground = true, Name = "single-instance" }.Start();
            }
            catch { }
        }

        public static void StopListening()
        {
            try
            {
                if (server != null) server.Dispose();
                if (File.Exists(SocketPath)) File.Delete(SocketPath);
            }
            catch { }
        }
    }
}
