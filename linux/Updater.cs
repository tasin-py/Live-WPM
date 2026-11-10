using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace LiveWpm
{
    // Checks GitHub Releases for a newer Linux build (at most once a day, at startup, or when the
    // Typer presses Check) and swaps it in. Releases carry "live-wpm-linux-x64.tar.gz"; the Windows
    // updater only ever looks at ".exe" files, so the two never pick up each other's download.
    // The only thing sent is a plain request for the latest release; no stats or typing data.
    static class Updater
    {
        const string Repo = "tasin-py/Live-WPM";
        public const string ReleasePage = "https://github.com/" + Repo + "/releases/latest";

        static DataStore store;
        static string downloadUrl, error;
        static bool checkedOk;

        public static string Available;
        public static bool Busy;
        public static event Action Changed;
        public static event Action<string> Found;
        public static event Action Restart;

        public static Version Current { get { return typeof(Updater).Assembly.GetName().Version; } }
        static string Short(Version v) { return v.Major + "." + v.Minor + "." + Math.Max(0, v.Build); }

        static string Arch { get { return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64"; } }
        static string AssetName { get { return "live-wpm-linux-" + Arch + ".tar.gz"; } }

        // A copy installed by a package manager (in /usr) can't replace itself: that is the package manager's job.
        public static bool CanInstall
        {
            get
            {
                try
                {
                    string exe = Platform.ExecutablePath;
                    if (exe == null || exe.StartsWith("/usr/")) return false;
                    return access(Path.GetDirectoryName(exe), 2) == 0;   // W_OK
                }
                catch { return false; }
            }
        }

        [DllImport("libc", SetLastError = true)]
        static extern int access([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);

        public static string Status
        {
            get
            {
                string v = "v" + Short(Current);
                if (Available != null) return CanInstall ? "Version " + Available + " is ready" : "Version " + Available + ": update it where you installed it";
                if (Busy) return v + " · checking...";
                if (error != null) return v + " · couldn't check";
                if (checkedOk) return v + " · up to date";
                return v;
            }
        }

        public static void Init(DataStore data)
        {
            store = data;
            try { File.Delete(Platform.ExecutablePath + ".old"); } catch { }
            if (store.LastUpdateCheck == DateTime.Today.ToString("yyyy-MM-dd")) return;
            DispatcherTimer.RunOnce(delegate { Check(false); }, TimeSpan.FromSeconds(30));
        }

        static void Notify() { if (Changed != null) Changed(); }

        static HttpClient Client()
        {
            HttpClient c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("LiveWPM-Linux/" + Short(Current));
            return c;
        }

        public static void Check(bool manual)
        {
            if (Busy) return;
            Busy = true;
            error = null;
            Notify();
            Task.Run(async delegate
            {
                string found = null, url = null, err = null;
                try
                {
                    using (HttpClient c = Client())
                    using (JsonDocument doc = JsonDocument.Parse(await c.GetStringAsync("https://api.github.com/repos/" + Repo + "/releases/latest")))
                    {
                        string tag = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                        foreach (JsonElement a in doc.RootElement.GetProperty("assets").EnumerateArray())
                            if (string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                                url = a.GetProperty("browser_download_url").GetString();
                        Version v;
                        if (url != null && Version.TryParse(tag, out v) && v > Current) found = Short(v);
                    }
                }
                catch (Exception ex) { err = ex.Message; }

                Dispatcher.UIThread.Post(delegate
                {
                    Busy = false;
                    error = err;
                    checkedOk = err == null;
                    Available = found;
                    downloadUrl = url;
                    if (err == null)
                    {
                        store.LastUpdateCheck = DateTime.Today.ToString("yyyy-MM-dd");
                        store.Save();
                    }
                    Notify();
                    if (found != null && !manual && Found != null) Found(found);
                });
            });
        }

        // Downloads the release, takes the app out of it, and swaps it in. Linux lets a running
        // program be renamed, so the old one becomes ".old" and is deleted at the next start.
        public static void Install(Window owner)
        {
            if (Busy || downloadUrl == null || !CanInstall) return;
            Busy = true;
            Notify();
            string exe = Platform.ExecutablePath, fresh = exe + ".new", url = downloadUrl;
            Task.Run(async delegate
            {
                string err = null;
                try
                {
                    using (HttpClient c = Client())
                    using (Stream net = await c.GetStreamAsync(url))
                    using (GZipStream gz = new GZipStream(net, CompressionMode.Decompress))
                    using (TarReader tar = new TarReader(gz))
                    {
                        TarEntry e;
                        bool done = false;
                        while ((e = tar.GetNextEntry()) != null)
                            if (e.EntryType == TarEntryType.RegularFile && Path.GetFileName(e.Name) == "live-wpm")
                            {
                                e.ExtractToFile(fresh, true);
                                done = true;
                                break;
                            }
                        if (!done) throw new Exception("The download doesn't contain the app.");
                    }
                    byte[] head = new byte[4];
                    using (FileStream fs = File.OpenRead(fresh)) fs.ReadExactly(head, 0, 4);
                    if (head[0] != 0x7F || head[1] != 'E' || head[2] != 'L' || head[3] != 'F' || new FileInfo(fresh).Length < 1000000)
                        throw new Exception("The download isn't a valid app file.");
                    File.SetUnixFileMode(fresh, File.GetUnixFileMode(exe));
                }
                catch (Exception ex) { err = ex.Message; }

                Dispatcher.UIThread.Post(async delegate
                {
                    Busy = false;
                    if (err == null)
                    {
                        try
                        {
                            if (File.Exists(exe + ".old")) File.Delete(exe + ".old");
                            File.Move(exe, exe + ".old");
                            try { File.Move(fresh, exe); }
                            catch { File.Move(exe + ".old", exe); throw; }
                            Process.Start(new ProcessStartInfo(exe, "--updated") { UseShellExecute = false });
                            if (Restart != null) Restart();
                            return;
                        }
                        catch (Exception ex) { err = ex.Message; }
                    }
                    try { File.Delete(fresh); } catch { }
                    Notify();
                    await MessageWindow.Info(owner, "The update didn't work:\n" + err);
                });
            });
        }
    }
}
