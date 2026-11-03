using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace LiveWpm
{
    // Checks GitHub Releases for a newer "Live WPM.exe" (at most once a day, at startup, or when
    // the Typer presses Check) and swaps it in. The only thing sent is a plain request for the
    // latest release; no stats or typing data ever leave the PC.
    static class Updater
    {
        // GitHub "owner/repo" whose Releases hold new versions. Each release needs a tag like
        // "v1.1.0" and the new "Live WPM.exe" attached. Empty = updates are switched off.
        const string Repo = "";

        static Control ui;
        static DataStore store;
        static string downloadUrl, error;
        static bool checkedOk;

        public static string Available;   // newer version waiting to be installed, or null
        public static bool Busy;
        public static event Action Changed;
        public static event Action<string> Found;  // a daily check found a new version
        public static event Action Restart;        // the new exe is in place: quit so it can take over

        public static Version Current { get { return typeof(Updater).Assembly.GetName().Version; } }
        static string Short(Version v) { return v.Major + "." + v.Minor + "." + Math.Max(0, v.Build); }

        public static string Status
        {
            get
            {
                string v = "v" + Short(Current);
                if (Available != null) return "Version " + Available + " is ready";
                if (Repo.Length == 0) return v + " · updates not set up";
                if (Busy) return v + " · checking...";
                if (error != null) return v + " · couldn't check";
                if (checkedOk) return v + " · up to date";
                return v;
            }
        }

        public static void Init(Control uiThread, DataStore data)
        {
            ui = uiThread;
            store = data;
            try { File.Delete(Application.ExecutablePath + ".old"); } catch { } // left over from the last update
            if (store.LastUpdateCheck == DateTime.Today.ToString("yyyy-MM-dd")) return;
            // wait a little after startup so Windows isn't busy signing in
            System.Windows.Forms.Timer later = new System.Windows.Forms.Timer();
            later.Interval = 30000;
            later.Tick += delegate { later.Dispose(); Check(false); };
            later.Start();
        }

        static void Notify() { if (Changed != null) Changed(); }

        static void OnUi(MethodInvoker m)
        {
            try { ui.BeginInvoke(m); } catch { }
        }

        public static void Check(bool manual)
        {
            if (Busy || Repo.Length == 0) return;
            Busy = true;
            error = null;
            Notify();
            new Thread(delegate()
            {
                string found = null, url = null, err = null;
                try
                {
                    Dictionary<string, object> rel = GetJson("https://api.github.com/repos/" + Repo + "/releases/latest");
                    string tag = (rel["tag_name"] as string ?? "").TrimStart('v', 'V');
                    foreach (object o in (IEnumerable)rel["assets"])
                    {
                        Dictionary<string, object> a = (Dictionary<string, object>)o;
                        if (((string)a["name"]).EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            url = (string)a["browser_download_url"];
                            break;
                        }
                    }
                    Version v;
                    if (url != null && Version.TryParse(tag, out v) && v > Current) found = Short(v);
                }
                catch (Exception ex) { err = ex.Message; }

                OnUi(delegate
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
            }) { IsBackground = true }.Start();
        }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "LiveWPM/" + Short(Current);
            return wc;
        }

        static Dictionary<string, object> GetJson(string url)
        {
            using (WebClient wc = Client())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                string json = wc.DownloadString(url);
                return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            }
        }

        // Downloads the new exe next to the current one, then swaps them: Windows lets a running
        // exe be renamed, so the old one becomes "Live WPM.exe.old" and is deleted next start.
        public static void Install(IWin32Window owner)
        {
            if (Busy || downloadUrl == null) return;
            Busy = true;
            Notify();
            string exe = Application.ExecutablePath, fresh = exe + ".new", url = downloadUrl;
            new Thread(delegate()
            {
                string err = null;
                try
                {
                    using (WebClient wc = Client()) wc.DownloadFile(url, fresh);
                    byte[] head = new byte[2];
                    using (FileStream fs = File.OpenRead(fresh)) fs.Read(head, 0, 2);
                    if (head[0] != 'M' || head[1] != 'Z' || new FileInfo(fresh).Length < 20000)
                        throw new Exception("The download isn't a valid app file.");
                }
                catch (Exception ex) { err = ex.Message; }

                OnUi(delegate
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
                            Process.Start(exe, "--updated");
                            if (Restart != null) Restart();
                            return;
                        }
                        catch (Exception ex) { err = ex.Message; }
                    }
                    try { File.Delete(fresh); } catch { }
                    Notify();
                    MessageBox.Show(owner, "The update didn't work:\n" + err, Theme.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            }) { IsBackground = true }.Start();
        }
    }
}
