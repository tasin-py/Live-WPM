using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Live WPM")]
[assembly: AssemblyProduct("Live WPM")]
[assembly: AssemblyDescription("Live WPM - live typing speedometer")]
[assembly: AssemblyCompany("Live WPM")]
[assembly: AssemblyVersion("1.6.0.0")]
[assembly: AssemblyFileVersion("1.6.0.0")]

namespace LiveWpm
{
    static class Program
    {
        const string InstanceName = "LIVE_WPM_single_instance";

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--make-icon") { Theme.SaveIco(args[1]); return; }

            bool created;
            using (Mutex mutex = new Mutex(true, InstanceName, out created))
            using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + "_show"))
            {
                bool updated = Array.IndexOf(args, "--updated") >= 0;
                if (!created && updated)
                {
                    // just updated: the old copy is still closing, wait for it to let go
                    try { created = mutex.WaitOne(15000); }
                    catch (AbandonedMutexException) { created = true; }
                }
                if (!created)
                {
                    show.Set(); // already running: ask the first copy to open its window
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool tray = Array.IndexOf(args, "--tray") >= 0 || updated;
                Application.Run(new TrayApp(show, !tray));
                GC.KeepAlive(mutex);
            }
        }
    }

    // Lives in the notification area and owns everything else.
    sealed class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        readonly DataStore store;
        readonly KeyboardHook hook = new KeyboardHook();
        readonly TypingEngine engine;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Icon iconOn, iconOff;
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer clickDelay = new System.Windows.Forms.Timer();
        readonly Control ui = new Control();
        readonly TrayMenu menu;
        FloatingWindow floating;
        MainWindow main;

        public TrayApp(EventWaitHandle showSignal, bool openWindow)
        {
            ui.CreateControl(); // gives us a UI-thread handle for cross-thread calls

            ThemeFiles.LoadAll();   // before the stats, which name the theme last used
            store = DataStore.Load();
            // Opens in the theme the Typer last picked (a first run gets the stock theme, Average Tasin).
            Theme.Apply(store.ThemeName);
            if (string.IsNullOrEmpty(store.TyperName))
            {
                store.TyperName = NamePrompt.Ask("", true, null) ?? "Typer";
                store.Save();
            }
            ApplyStartup();

            engine = new TypingEngine(store);
            hook.Key += engine.OnKey;
            hook.Key += delegate { if (!tick.Enabled) tick.Start(); };
            engine.Started += UpdateFloating;
            engine.Ended += delegate { if (main != null) main.RefreshData(); };
            engine.HoldDone += delegate { tick.Stop(); if (floating != null) floating.Kick(); };
            tick.Interval = 100;
            tick.Tick += delegate { engine.Tick(); if (engine.Phase == Phase.Idle) tick.Stop(); };

            int small = (int)(16 * Theme.Scale);
            iconOn = Theme.MakeIcon(small, Theme.Brand);
            iconOff = Theme.MakeIcon(small, Theme.Off);
            tray.Visible = true;
            tray.MouseClick += OnTrayClick;
            tray.MouseDoubleClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                clickDelay.Stop();
                ShowMain();
            };
            // A single click waits for the double-click time so a double-click doesn't flash the menu.
            clickDelay.Interval = SystemInformation.DoubleClickTime;
            clickDelay.Tick += delegate { clickDelay.Stop(); menu.ShowNear(Cursor.Position); };

            menu = new TrayMenu(store, ToggleFloating, ToggleTracking, ToggleOnlyTyping, ShowMain, Quit);

            ApplyTracking();
            ThreadPool.RegisterWaitForSingleObject(showSignal, delegate
            {
                try { ui.BeginInvoke((MethodInvoker)ShowMain); } catch { }
            }, null, -1, false);

            Updater.Changed += delegate { if (main != null) main.RefreshData(); };
            Updater.Found += delegate(string v)
            {
                tray.ShowBalloonTip(5000, Theme.AppName, "Version " + v + " is available. Open Settings to update.", ToolTipIcon.Info);
            };
            tray.BalloonTipClicked += delegate { ShowMain(); };
            Updater.Restart += Quit;
            Updater.Init(ui, store);

            if (openWindow) ShowMain();
            Theme.TrimMemory();
        }

        void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { menu.ShowNear(Cursor.Position); return; }
            if (e.Button == MouseButtons.Left) { clickDelay.Stop(); clickDelay.Start(); }
        }

        // ---- toggles ---------------------------------------------------------------

        void ToggleFloating() { store.Floating = !store.Floating; store.Save(); UpdateFloating(); }

        void ToggleTracking() { store.Tracking = !store.Tracking; store.Save(); ApplyTracking(); }

        void ToggleOnlyTyping() { store.OnlyWhenTyping = !store.OnlyWhenTyping; store.Save(); UpdateFloating(); }

        // When tracking is off the hook is removed and timers stop: the app goes completely silent.
        void ApplyTracking()
        {
            if (store.Tracking) hook.Start();
            else
            {
                hook.Stop();
                engine.Stop();
                tick.Stop();
            }
            tray.Icon = store.Tracking ? iconOn : iconOff;
            tray.Text = Theme.AppName + (store.Tracking ? " - tracking" : " - paused");
            UpdateFloating();
            if (main != null) main.RefreshData();
        }

        void UpdateFloating()
        {
            bool wanted = store.Tracking && store.Floating;
            if (!wanted)
            {
                if (floating != null) { floating.Dispose(); floating = null; Theme.TrimMemory(); }
                return;
            }
            if (floating == null)
            {
                floating = new FloatingWindow(store, engine);
                floating.CloseClicked += ToggleFloating; // the X turns the floating window option off
            }
            if (!store.OnlyWhenTyping || engine.Shown) floating.Appear();
            else floating.Disappear();
        }

        // Theme or floating background changed in Settings: restyle everything and flash the speedometer as a preview.
        void AppearanceChanged()
        {
            menu.ThemeChanged();
            if (floating != null) floating.Preview();
        }

        // ---- windows -------------------------------------------------------------------

        void ShowMain()
        {
            if (main == null || main.IsDisposed)
            {
                main = new MainWindow(store, ApplyStartup, delegate { if (floating != null) floating.Kick(); }, AppearanceChanged);
                main.FormClosed += delegate { main = null; Theme.TrimMemory(); };
                main.Show();
            }
            if (main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;
            main.Activate();
            main.RefreshData();
        }

        // Registers the app under HKCU\...\Run, which is what Windows lists in Settings > Apps > Startup.
        void ApplyStartup()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (store.StartWithWindows) k.SetValue(Theme.AppName, "\"" + Application.ExecutablePath + "\" --tray");
                    else if (k.GetValue(Theme.AppName) != null) k.DeleteValue(Theme.AppName);
                }
            }
            catch { }
            if (main != null) main.RefreshData();
        }

        void Quit()
        {
            engine.Stop();
            hook.Dispose();
            tray.Visible = false;
            tray.Dispose();
            if (floating != null) floating.Dispose();
            if (main != null) main.Close();
            ExitThread();
        }
    }
}
