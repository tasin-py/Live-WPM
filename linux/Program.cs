using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

// The screenshot and test tools drive the windows directly.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("lwshots")]

namespace LiveWpm
{
    static class Program
    {
        public static string[] Args = new string[0];

        [STAThread]
        static int Main(string[] args)
        {
            // The sandboxed keyboard service (run by systemd): no windows, nothing else of the app.
            if (args.Length > 0 && args[0] == "--keyboard-service") return KeyboardService.Run(args);
            Args = args;
            if (Array.IndexOf(args, "--version") >= 0) { Console.WriteLine(Theme.AppName + " " + Updater.Current); return 0; }
            if (Array.IndexOf(args, "--updated") >= 0) Platform.WaitForOldCopy();
            else if (Platform.SignalRunningCopy()) return 0;   // already running: it opens its window instead
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();
        }
    }

    sealed class App : Application
    {
        public static WindowIcon WindowIcon, IconOff;
        TrayApp tray;

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = ThemeVariant.Dark;
            WindowIcon = LoadIcon("live-wpm.png");
            IconOff = LoadIcon("live-wpm-off.png");
        }

        static WindowIcon LoadIcon(string name)
        {
            try
            {
                using (Stream s = typeof(App).Assembly.GetManifestResourceStream(name))
                {
                    MemoryStream copy = new MemoryStream();
                    s.CopyTo(copy);
                    copy.Position = 0;
                    return new WindowIcon(copy);
                }
            }
            catch { return null; }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            IClassicDesktopStyleApplicationLifetime desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                bool openWindow = Array.IndexOf(Program.Args, "--tray") < 0 && Array.IndexOf(Program.Args, "--updated") < 0;
                tray = new TrayApp(this, desktop, openWindow);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }

    // Lives in the tray and owns everything else. Ported from TrayApp in src/Program.cs.
    sealed class TrayApp
    {
        readonly Application app;
        readonly IClassicDesktopStyleApplicationLifetime desktop;
        DataStore store;
        readonly KeyboardInput keyboard = new KeyboardInput();
        TypingEngine engine;
        readonly DispatcherTimer tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        TrayIcon icon;
        NativeMenuItem itemTracking, itemFloating, itemOnlyTyping;
        FloatingWindow floating;
        MainWindow main;

        public TrayApp(Application app, IClassicDesktopStyleApplicationLifetime desktop, bool openWindow)
        {
            this.app = app;
            this.desktop = desktop;
            Dispatcher.UIThread.Post(async delegate
            {
                ThemeFiles.LoadAll();     // before the stats, which name the theme last used
                store = DataStore.Load();
                Theme.Apply(store.ThemeName);
                if (string.IsNullOrEmpty(store.TyperName))
                {
                    store.TyperName = await NamePrompt.Ask("", true, null) ?? "Typer";
                    store.Save();
                    openWindow = true;
                }
                Start(openWindow);
            });
        }

        void Start(bool openWindow)
        {
            ApplyStartup();
            engine = new TypingEngine(store);
            // The keyboard reader runs on its own threads; only the kind of key crosses over.
            keyboard.Key += delegate(KeyKind k)
            {
                Dispatcher.UIThread.Post(delegate
                {
                    if (!store.Tracking) return;
                    engine.OnKey(k);
                    if (!tick.IsEnabled) tick.Start();
                }, DispatcherPriority.Input);
            };
            keyboard.AccessChanged += delegate { Dispatcher.UIThread.Post(delegate { if (main != null) main.RefreshData(); }); };
            engine.Started += UpdateFloating;
            engine.Ended += delegate { if (main != null) main.RefreshData(); };
            engine.HoldDone += delegate { tick.Stop(); if (floating != null) floating.Kick(); };
            tick.Tick += delegate { engine.Tick(); if (engine.Phase == Phase.Idle) tick.Stop(); };

            BuildTray();
            ApplyTracking();
            Platform.ListenForShow(delegate { Dispatcher.UIThread.Post(ShowMain); });

            Updater.Changed += delegate { if (main != null) main.RefreshData(); };
            Updater.Found += delegate(string v)
            {
                Platform.Notify(Theme.AppName, "Version " + v + " is available. Open Settings to update.");
            };
            Updater.Restart += Quit;
            Updater.Init(store);

            if (openWindow) ShowMain();
        }

        // ---- tray --------------------------------------------------------------------

        void BuildTray()
        {
            itemTracking = Toggle("Enabled: track typing speed", ToggleTracking);
            itemFloating = Toggle("Floating window", ToggleFloating);
            itemOnlyTyping = Toggle("Only when typing", ToggleOnlyTyping);
            NativeMenuItem open = new NativeMenuItem("Open " + Theme.AppName);
            open.Click += delegate { ShowMain(); };
            NativeMenuItem exit = new NativeMenuItem("Exit");
            exit.Click += delegate { Quit(); };

            NativeMenu menu = new NativeMenu();
            menu.Items.Add(itemTracking);
            menu.Items.Add(itemFloating);
            menu.Items.Add(itemOnlyTyping);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(open);
            menu.Items.Add(exit);

            icon = new TrayIcon { Icon = App.WindowIcon, ToolTipText = Theme.AppName, Menu = menu, IsVisible = true };
            icon.Clicked += delegate { ShowMain(); };
            TrayIcon.SetIcons(app, new TrayIcons { icon });
            RefreshTray();
        }

        static NativeMenuItem Toggle(string label, Action run)
        {
            NativeMenuItem it = new NativeMenuItem(label) { ToggleType = NativeMenuItemToggleType.CheckBox };
            it.Click += delegate { run(); };
            return it;
        }

        void RefreshTray()
        {
            if (icon == null) return;
            itemTracking.IsChecked = store.Tracking;
            itemFloating.IsChecked = store.Floating;
            itemOnlyTyping.IsChecked = store.OnlyWhenTyping;
            icon.Icon = store.Tracking ? App.WindowIcon : App.IconOff;
            icon.ToolTipText = Theme.AppName + (store.Tracking ? " - tracking" : " - paused");
        }

        // ---- toggles ---------------------------------------------------------------

        void ToggleFloating() { store.Floating = !store.Floating; store.Save(); UpdateFloating(); RefreshTray(); }

        void ToggleTracking() { store.Tracking = !store.Tracking; store.Save(); ApplyTracking(); }

        void ToggleOnlyTyping() { store.OnlyWhenTyping = !store.OnlyWhenTyping; store.Save(); UpdateFloating(); RefreshTray(); }

        // When tracking is off the keyboard is closed and timers stop: the app goes completely silent.
        void ApplyTracking()
        {
            if (store.Tracking) keyboard.Start();
            else
            {
                keyboard.Stop();
                engine.Stop();
                tick.Stop();
            }
            RefreshTray();
            UpdateFloating();
            if (main != null) main.RefreshData();
        }

        void UpdateFloating()
        {
            bool wanted = store.Tracking && store.Floating;
            if (!wanted)
            {
                if (floating != null) { floating.Dispose(); floating = null; }
                return;
            }
            if (floating == null)
            {
                floating = new FloatingWindow(store, engine);
                floating.CloseClicked += ToggleFloating;
            }
            if (!store.OnlyWhenTyping || engine.Shown) floating.Appear();
            else floating.Disappear();
        }

        void AppearanceChanged()
        {
            if (floating != null) floating.Preview();
        }

        // ---- windows -------------------------------------------------------------------

        void ShowMain()
        {
            if (store == null) return;
            if (main == null)
            {
                main = new MainWindow(store, keyboard, ApplyStartup, delegate { if (floating != null) floating.Kick(); }, AppearanceChanged);
                main.Closed += delegate { main = null; };
                main.Show();
            }
            if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
            main.Activate();
            main.RefreshData();
        }

        // "Start on login": an entry in ~/.config/autostart, which every Linux desktop reads.
        void ApplyStartup()
        {
            Platform.SetAutostart(store.StartWithWindows);
            if (main != null) main.RefreshData();
        }

        void Quit()
        {
            if (engine != null) engine.Stop();
            keyboard.Dispose();
            if (icon != null) { icon.IsVisible = false; icon.Dispose(); }
            if (floating != null) floating.Dispose();
            if (main != null) main.Close();
            Platform.StopListening();
            desktop.Shutdown();
        }
    }
}
