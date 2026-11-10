<p align="center">
  <img src="docs/images/logo.png" width="96" alt="Live WPM logo">
</p>

<h1 align="center">Live WPM</h1>

<p align="center">
  <b>A live typing speedometer for Windows and Linux.</b><br>
  See how fast you type while you type: in any app, with no setup, and without recording a single key.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-38D6FF?style=flat-square&logo=windows&logoColor=white" alt="Windows 10 | 11">
  <img src="https://img.shields.io/badge/Linux-Arch%20%7C%20X11%20%7C%20Wayland-38D6FF?style=flat-square&logo=linux&logoColor=white" alt="Linux: Arch, X11 and Wayland">
  <img src="https://img.shields.io/badge/Windows%20exe-~125%20KB-38D6FF?style=flat-square" alt="Windows exe about 125 KB">
  <img src="https://img.shields.io/badge/install-not%20needed-38D6FF?style=flat-square" alt="No install needed">
  <img src="https://img.shields.io/badge/keys%20recorded-none-38D6FF?style=flat-square" alt="No keys recorded">
  <img src="https://img.shields.io/github/v/release/tasin-py/Live-WPM?style=flat-square&color=38D6FF" alt="Latest release">
</p>

<p align="center">
  <a href="https://github.com/tasin-py/Live-WPM/releases/latest"><b>Download for Windows</b></a> ·
  <a href="#linux"><b>Download for Linux</b></a> ·
  <a href="#whats-new">What's new</a> ·
  <a href="#features">Features</a> ·
  <a href="#privacy">Privacy</a> ·
  <a href="#linux">Linux</a> ·
  <a href="#using-live-wpm">Guide</a> ·
  <a href="#faq">FAQ</a> ·
  <a href="#building-from-source">Build</a>
</p>

<p align="center">
  <img src="docs/images/home.png" width="720" alt="Live WPM home screen with today's speed graph, best run and lifetime stats">
</p>

---

## Contents

- [What's new](#whats-new)
- [Features](#features)
- [Privacy](#privacy)
- [Requirements](#requirements)
- [Installation](#installation)
- [Linux](#linux)
  - [Install on Arch Linux](#install-on-arch-linux)
  - [Install on any distro](#install-on-any-distro)
  - [The keyboard service](#the-keyboard-service)
  - [Troubleshooting on Linux](#troubleshooting-on-linux)
- [Using Live WPM](#using-live-wpm)
  - [The tray icon](#the-tray-icon)
  - [The floating speedometer](#the-floating-speedometer)
  - [Home](#home)
  - [History](#history)
  - [Settings](#settings)
  - [Themes](#themes)
- [How speed is measured](#how-speed-is-measured)
- [Updates](#updates)
- [Your data](#your-data)
- [Uninstalling](#uninstalling)
- [FAQ](#faq)
- [Building from source](#building-from-source)
- [Publishing a release](#publishing-a-release)
- [Project structure](#project-structure)
- [Support](#support)

---

## What's new

**1.7** (current: 1.7.0)

- **Live WPM for Linux**: the same app, themes, stats and speedometer, now on Linux. Tested first on Arch Linux, and works under both X11 and Wayland. See [Linux](#linux).
- **A locked-down keyboard service on Linux**: a small service reads the keyboard for Live WPM and passes on only the kind of each key, never which key. systemd runs it with a temporary user of its own, no network and no access to your files. Your own account needs no extra rights, and you don't join the `input` group. See [The keyboard service](#the-keyboard-service).
- **Fixed on Windows: missing text on scaled displays.** At 125%, 150% or 200% display scaling, button labels, values, the lower stat cards and the About group could lose their text. Everything now shows at any scale.

**1.6**

- **Your own themes**: upload a small theme file from the theme picker and it joins the built-in themes. See [Make your own theme](#make-your-own-theme).
- **New way to support the project** (1.6.1): **Buy Me A Coffee** now shows a SupportKori QR code and link, with **Open link** and **Copy link** buttons.

**1.5**

- **Seven themes**: Average Tasin, Beverage Tashhid, Ahnaf Dark, Makam Blue, Ziraf Minimal, Adib Red and Safir Glass. Each one restyles the main window and the floating speedometer.
- **Safir Glass**: the speedometer shows your blurred desktop through a deep sky-blue tint, and the main window matches it in deep navy, with raised 3D panes.
- **Floating window background slider**: fade the speedometer's background from solid to fully clear, with the numbers and dial staying sharp.
- **Remove last session**: take back your most recent run, peak speed included.
- **Cleaner Settings**: three simple groups (General, Data, About), one line per setting.
- **More reliable typing detection**: the keyboard hook re-attaches itself, and full-screen detection no longer mistakes a maximised browser or editor for a game.
- **Smoother speedometer**: true rounded corners, no line through the middle, and bolder corner labels in the light themes.
- **Sharper Safir Glass text**: glass now uses the same ClearType text as the other themes, so it stays crisp over any desktop.
- **Your theme is remembered**: Live WPM opens in the theme you last picked.
- **Updates now on**: the app checks this repository's releases once a day.

---

## Features

| | |
|---|---|
| **Live speedometer** | A small dial floats over your screen and shows your speed as you type, in any app. |
| **Shows up only when you type** | The dial fades in when you start typing and fades out about 4 seconds after you stop. |
| **Best-run records** | Keeps your peak speed, along with the accuracy and word count of that run. |
| **Daily graphs** | Every typing run is plotted across the day, so you can see when you type fastest. |
| **Themes** | Seven built-in looks, from flat dark grey to pink, black-and-gold, all-white-on-black and see-through glass, plus up to nine of your own. |
| **History** | One row per day with peak speed, average speed and word count. You can delete any day. |
| **Ignores games** | Key presses are skipped while a full-screen app is in front, and runs full of held-down keys are thrown out. |
| **Ignores shortcuts** | Ctrl, Alt and Win (Super) shortcuts and key auto-repeat are never counted. |
| **One switch to stop** | Turn tracking off from the tray and the app ignores your keyboard completely. |
| **Light** | On Windows, one ~125 KB exe. No installer, no runtime to download, and next to no CPU while idle. |
| **Linux too** | The same app for Linux: a tray icon, the floating speedometer and every theme, on X11 and Wayland. |
| **Sharp on any display** | Per-monitor DPI support keeps text crisp at 100%, 150%, 200% and on mixed-scale setups. |
| **Undo a run** | Remove the last session, and the peak it set goes back to what it was before. |
| **Self-updating** | Checks once a day for a new version and updates when you say so. |

<p align="center">
  <img src="docs/images/speedometer.png" width="360" alt="The floating speedometer">
  &nbsp;&nbsp;
  <img src="docs/images/tray-menu.png" width="268" alt="The tray menu">
</p>

---

## Privacy

Live WPM is built so that it **cannot** tell anyone what you typed, including you.

**What it keeps:** only numbers. These are speeds, accuracy, word and character counts, backspace counts, durations, and the time of day each run started.

**What it never keeps:** the keys themselves. When a key is pressed, the app sorts it into one of four kinds (a character, a space or Enter, a backspace, or a held key) and discards it right away. Which letter you pressed is never stored, in memory or on disk. Passwords, messages and documents are never seen as text.

**Network:** the app connects to the internet for one thing only: a once-a-day check of this project's GitHub Releases for a new version, plus the download when you choose to update. No statistics, analytics or telemetry are ever sent.

**Off means off:** when tracking is switched off, the app stops reacting to the keyboard entirely.

**On Linux:** the same rules hold. A small, locked-down [keyboard service](#the-keyboard-service) hands Live WPM only the kind of each key, and it has no network access at all. When tracking is off, Live WPM disconnects and the service closes the keyboards.

> [!NOTE]
> Everything is stored in a single plain-text file on your own PC (see [Your data](#your-data)). You can open it in Notepad and check that it contains only numbers.

---

## Requirements

**Windows**

- Windows 10 or Windows 11 (64-bit or 32-bit)
- .NET Framework 4.x, which is already part of every Windows 10 and 11 install

**Linux**

- A 64-bit Linux desktop (x86-64). Tested first on **Arch Linux**, and it works on X11 and Wayland sessions alike.
- Nothing else to install for the download: it carries everything it needs. The Arch package uses Arch's own `dotnet-runtime`.
- systemd, for Live WPM's keyboard service (see [The keyboard service](#the-keyboard-service)). Arch, Fedora, Ubuntu, Debian, openSUSE and most other distros use it.

---

## Installation

### Windows

1. Download **`Live WPM.exe`** from the [latest release](https://github.com/tasin-py/Live-WPM/releases/latest).
2. Move it to a folder you'll keep, for example `C:\Users\<you>\Apps\Live WPM\`.
   Start with Windows remembers this location, so don't run it from your Downloads folder if you plan to clean that out.
3. Double-click it.
4. If Windows shows **"Windows protected your PC"**, click **More info → Run anyway**.
   This appears for apps from smaller publishers that aren't code-signed. See the [FAQ](#faq).
5. Enter a name when asked. It's only shown in the app, as your *Typer* name.

Live WPM now runs in the system tray, next to the clock. Start typing anywhere and the speedometer appears.

> [!TIP]
> If you don't see the icon, click the **^** arrow in the taskbar's notification area. You can drag the icon out of there to keep it always visible.

### On Linux

See **[Linux](#linux)** below for the Arch package, the download for any distro and the one-time keyboard step.

---

## Linux

Live WPM for Linux is the same app as on Windows: the tray icon, the floating speedometer, Home, History, Settings, all seven themes, your own `.lwtheme` files and the same stats file. It lives in this repository, next to the Windows app, and every release has a download for each.

**Arch Linux is the first supported distro.** The Linux edition reads the keyboard straight from the kernel, so it works the same in X11 and Wayland sessions.

- [What you need](#what-you-need)
- [Install on Arch Linux](#install-on-arch-linux)
- [Install on any distro](#install-on-any-distro)
- [The keyboard service](#the-keyboard-service)
- [First start](#first-start)
- [The tray icon on your desktop](#the-tray-icon-on-your-desktop)
- [Start on login](#start-on-login)
- [Updating on Linux](#updating-on-linux)
- [Moving your stats from Windows](#moving-your-stats-from-windows)
- [Uninstalling on Linux](#uninstalling-on-linux)
- [Troubleshooting on Linux](#troubleshooting-on-linux)
- [How it differs from Windows](#how-it-differs-from-windows)

### What you need

- A 64-bit (x86-64) Linux PC with a desktop: KDE Plasma, GNOME, Xfce, Cinnamon, MATE, Hyprland, Sway or similar.
- An X11 or a Wayland session. Both work.
- systemd, which runs Live WPM's [keyboard service](#the-keyboard-service). Switching that on needs `sudo` once. Your own account needs no extra rights.
- **For the download:** nothing else. It carries everything it needs, .NET included.
- **For the Arch package:** `git` and `base-devel` to build it. The package then uses Arch's own .NET runtime, which pacman installs for you.

### Install on Arch Linux

The Arch way: build a real package with `makepkg` and let pacman install it. It goes to `/usr/lib/live-wpm`, with `live-wpm` on your PATH, an app-menu entry and an icon, and pacman can remove it cleanly later.

1. Install the build tools, if you don't have them:
   ```bash
   sudo pacman -S --needed git base-devel
   ```
2. Get the source:
   ```bash
   git clone https://github.com/tasin-py/Live-WPM.git
   ```
3. Build and install the package. pacman asks for your password and pulls in `dotnet-sdk` (to build) and `dotnet-runtime` (to run):
   ```bash
   cd Live-WPM/linux/packaging/arch && makepkg -si
   ```
4. Switch on the keyboard service (once):
   ```bash
   sudo systemctl enable --now live-wpm-keys.socket
   ```
5. Open **Live WPM** from your app menu, or run `live-wpm`.

> [!TIP]
> The build checks out the release tagged in the PKGBUILD (`pkgver`), so you always get a released version, not work in progress.

### Install on any distro

The download is one self-contained program plus a small installer. It works on Arch too, if you'd rather not build a package.

1. Download **`live-wpm-linux-x64.tar.gz`** from the [latest release](https://github.com/tasin-py/Live-WPM/releases/latest), or from a terminal:
   ```bash
   curl -LO https://github.com/tasin-py/Live-WPM/releases/latest/download/live-wpm-linux-x64.tar.gz
   ```
2. Unpack it:
   ```bash
   tar -xzf live-wpm-linux-x64.tar.gz
   ```
3. Run the installer:
   ```bash
   ./live-wpm/install.sh
   ```
   It installs the app for your user only, in `~/.local/bin/live-wpm`, with an app-menu entry and an icon. Then it explains the [keyboard service](#the-keyboard-service) and asks to install it. That step uses `sudo` once.
4. Open **Live WPM** from your app menu.

You can also run it without installing anything: `./live-wpm/live-wpm`.

### The keyboard service

**Why it's needed.** Linux only lets programs in the `input` group read the keyboard, and Wayland doesn't let apps watch each other's typing at all. Putting your own account in the `input` group would work, but then *every* program you run could read every key you press, passwords included. Live WPM doesn't ask you to do that.

**What it does instead.** A small service reads the keyboard for Live WPM and passes on only the kind of each key: a character, a space or Enter, a backspace, or a long hold. It never passes on which key. Live WPM, running as you, gets nothing more than that, and your own account needs no extra rights.

**How it's locked down.** systemd runs the service ([`live-wpm-keys.service`](linux/packaging/live-wpm-keys.service)) like this:

- **A temporary user of its own,** created when it starts and gone when it stops. No account is added to your system.
- **No network at all.** It can't send anything anywhere.
- **Your files are out of reach.** Home folders are hidden from it, and the rest of the system is read-only.
- **Only input devices.** It has no privileges, and nothing it could use to get any.
- **Only for you, at your screen.** It answers only the user logged in at the screen right now. It won't answer a remote (SSH) login, or you once another user has switched in.
- **Only while Live WPM listens.** It starts when Live WPM connects, closes the keyboards the moment Live WPM disconnects (for example when you turn tracking off), and exits after 30 quiet seconds.

`systemd-analyze security live-wpm-keys.service` rates it **0.8 SAFE**. Its code is one short file, [`linux/KeyboardService.cs`](linux/KeyboardService.cs), and the key reading is [`linux/EvdevKeyboard.cs`](linux/EvdevKeyboard.cs), so you can read exactly what it does.

**Switching it on.** `install.sh` does this for you. For the Arch package, run once:

```bash
sudo systemctl enable --now live-wpm-keys.socket
```

To check it's on:

```bash
systemctl status live-wpm-keys.socket
```

In the app, **Settings → Keyboard access** shows the state:

| It says | Meaning |
|---|---|
| **1 keyboard, through the keyboard service** | All set. |
| **Keyboard service is off** | It's installed but not switched on. **Set up** copies the command above. |
| **Keyboard service not installed** | Run `install.sh` again and answer yes, or install the Arch package. |
| **Not available in this session** | Someone else is logged in at the screen, or this is a remote login. |
| **No keyboard found** | No keyboard is connected. Plug one in and Live WPM picks it up within a few seconds. |
| **Off while paused** | Tracking is off, so nothing is reading the keyboard. |

> [!NOTE]
> **Privacy is the same as on Windows.** Every key event is sorted into its kind and forgotten at once. Which key you pressed is never stored, in memory or on disk, and nothing leaves your PC.

#### Without systemd

On a distro without systemd (Artix, Void, Gentoo with OpenRC and others) the service can't run. Live WPM can still read the keyboard itself if you add your account to the `input` group (`sudo usermod -aG input $USER`, then log out and back in). **Know what that means first:** it lets every program you run read your keyboard, not just Live WPM. If you later get the service, leave the group again with `sudo gpasswd -d $USER input`. Live WPM always prefers the service when it's there.

### First start

1. Live WPM asks for a name. It's only shown in the app, as your *Typer* name.
2. The main window opens, and the Live WPM icon appears in your tray.
3. Start typing anywhere. After a few words the speedometer fades in at the bottom-right corner. Drag it wherever you like and it remembers the spot.
4. Close the main window when you're done. Live WPM keeps running in the tray.

The **Guide** below ([Using Live WPM](#using-live-wpm)) describes every screen. It's the same on Linux.

### The tray icon on your desktop

Live WPM puts its icon in the system tray, using the standard Linux tray protocol (StatusNotifierItem). Clicking it opens your desktop's own menu with the same switches as on Windows: **Enabled**, **Floating window**, **Only when typing**, **Open Live WPM** and **Exit**.

| Desktop | Tray |
|---|---|
| **KDE Plasma** | Built in. |
| **Xfce, Cinnamon, MATE, Budgie** | Built in. |
| **GNOME** | Needs the *AppIndicator and KStatusNotifierItem Support* extension (on Arch: `sudo pacman -S gnome-shell-extension-appindicator`, then turn it on in Extensions). |
| **Hyprland, Sway and other tiling setups** | Add a tray to your bar, for example the `tray` module in Waybar. |

With no tray at all, Live WPM still runs. Starting it again from the app menu opens its window, because only one copy ever runs.

### Start on login

**Settings → Start on login** (called *Start with Windows* on Windows) adds Live WPM to `~/.config/autostart`, which every desktop reads at login. It starts quietly in the tray. Turn the switch off to remove it again.

### Updating on Linux

- **The download (install.sh):** the same as Windows. Once a day Live WPM checks this repository's releases, shows a notification when a new version is out, and **Settings → Updates → Update** downloads it, swaps it in and restarts. Your stats stay as they are.
- **The Arch package:** pacman owns the files, so the app doesn't replace them itself. Its **Updates** row says a new version is out and **Open** takes you to the release. To update, pull the new release and build again:
  ```bash
  cd Live-WPM && git pull && cd linux/packaging/arch && makepkg -si
  ```

### Moving your stats from Windows

Both editions use the same stats format, so your records, history and graphs can come with you.

1. On Windows, close Live WPM (tray icon → **Exit**).
2. Copy `%APPDATA%\Live WPM\stats.dat`, and the `themes` folder beside it if you made your own themes.
3. On Linux, exit Live WPM, then put them in `~/.config/Live WPM/`.
4. Start Live WPM again.

### Uninstalling on Linux

- **The download:** run the installer with `--uninstall`. It removes the app, its menu entry, its icon, the login entry and the keyboard service (that part asks for `sudo`):
  ```bash
  ./live-wpm/install.sh --uninstall
  ```
  If you no longer have the download, delete `~/.local/bin/live-wpm`, `~/.local/share/applications/live-wpm.desktop` and `~/.config/autostart/live-wpm.desktop`.
- **The Arch package:**
  ```bash
  sudo pacman -R live-wpm
  ```

Both remove the keyboard service too. Your stats stay in `~/.config/Live WPM` either way. Delete that folder to remove them too.

### Troubleshooting on Linux

<details>
<summary><b>The speedometer never appears when I type.</b></summary>
<br>

Check these in order:

- **Settings → Keyboard access** doesn't say *through the keyboard service*: see [The keyboard service](#the-keyboard-service).
- **Enabled** is off in the tray menu.
- **Floating window** is off in the tray menu.
- The run was very short. The speedometer shows once you've typed a few words.
</details>

<details>
<summary><b>It says the keyboard service is off, but I switched it on.</b></summary>
<br>

Check it with `systemctl status live-wpm-keys.socket`. It should say *active (listening)*. Live WPM looks for the service every few seconds, so there's no need to restart it. If the socket is active but Live WPM still can't connect, run `journalctl -u live-wpm-keys` and [open an issue](https://github.com/tasin-py/Live-WPM/issues) with what it shows.
</details>

<details>
<summary><b>The speedometer has square, black corners or isn't see-through.</b></summary>
<br>

Your desktop isn't compositing, so windows can't be see-through. KDE, GNOME and most modern desktops composite by default. On a plain window manager, run a compositor such as `picom`. Live WPM still works without one, with a solid background.
</details>

<details>
<summary><b>The speedometer doesn't stay on top, or opens in the wrong place.</b></summary>
<br>

On Wayland the desktop decides where windows go. KDE honours Live WPM's position and "always on top"; GNOME may not. Drag it where you want it and the pin keeps it above other windows where the desktop allows. On a tiling window manager, add a rule that floats windows titled "Live WPM".
</details>

<details>
<summary><b>It won't start, or I want to see what's wrong.</b></summary>
<br>

Run it from a terminal to see any error: `live-wpm` for the Arch package, or `~/.local/bin/live-wpm` for the download. Then [open an issue](https://github.com/tasin-py/Live-WPM/issues) with what it printed, your distro and your desktop.
</details>

### How it differs from Windows

- **Keyboard:** on Linux a small, locked-down [keyboard service](#the-keyboard-service) reads the keyboard, switched on once with `sudo`. Windows needs nothing.
- **Tray menu:** your desktop's own menu, instead of Live WPM's drawn pop-up.
- **Safir Glass:** blurs what's behind the speedometer where the desktop offers blur (KDE Plasma). Elsewhere the glass is tinted more strongly so the numbers stay readable.
- **Full-screen game detection:** Windows only. On Linux, runs full of held-down keys (game controls) are still thrown out.
- **Size:** the Linux download is about 15 MB, because it carries .NET with it; the Windows exe uses the .NET that's built into Windows. The Arch package uses Arch's own .NET instead.
- **Font:** Inter, built into the app, so it looks the same on every distro.

### Building it yourself

You need the .NET 10 SDK (`sudo pacman -S dotnet-sdk` on Arch). From the repository:

```bash
./linux/build.sh
```

This makes `versions/v1.7.0/live-wpm-linux-x64.tar.gz`, the same download as in the releases. To just run it while working on it:

```bash
dotnet run --project linux
```

See [Building from source](#building-from-source) for how the Linux edition shares code with Windows.

---

## Using Live WPM

### The tray icon

| Action | Result |
|---|---|
| **Click** (left or right) | Opens the quick menu |
| **Double-click** | Opens the main Live WPM window |

The quick menu has three switches:

- **Enabled:** turns tracking on or off. When it's off, the tray icon turns grey and no keys are seen.
- **Floating window:** shows or hides the speedometer.
- **Only when typing:** shows the speedometer only while you type, instead of all the time.

Below them are **Open Live WPM** and **Exit**.

### The floating speedometer

| Part | Meaning |
|---|---|
| **wpm now** (top left) | Your speed right now, from your last 3 seconds of typing. It drops to 0 as soon as you pause. |
| **Dial and centre number** | Your average speed for the current run. |
| **PEAK** | The highest live speed reached in this run. |
| **WORDS** | Words typed in this run. |

The dial's range is set by your all-time peak, so a full dial means you're typing at your record speed.

- The arc turns **red** as you approach your record.
- The centre number turns **orange** when you're on pace to **beat** your record.

Controls:

- **Drag** anywhere on the dial to move it. Its position is remembered.
- **Pin** (top right) keeps it above other windows. Unpin it to let other windows cover it.
- **✕** hides the speedometer. Turn it back on from the tray menu's **Floating window** switch.

### Home

- **Today's speed:** a graph of every typing run today. Each point is one run's average speed. Hover a point to see its time, peak and word count.
- **Best run:** your peak speed, with that run's accuracy and word count.
- **Lifetime:** your average speed, average accuracy and total words.

### History

One row per day, newest first, with that day's **peak**, **average** and **words**. Hover a row to see two buttons:

- the **graph** icon opens that day's speed graph
- the **trash** icon deletes that day

<p align="center">
  <img src="docs/images/history.png" width="640" alt="History page">
</p>

### Settings

Settings is split into three groups.

**General**

| Setting | What it does |
|---|---|
| **Typer Name** | Change the name shown in the app. |
| **Start with Windows** | Starts Live WPM quietly in the tray when you sign in. It appears in Task Manager → Startup apps. |
| **Theme** | Click **Change** to open the theme picker. Clicking a theme applies it at once, to the app and the floating speedometer. Live WPM remembers your choice and opens in it next time. You can also upload your own. See [Themes](#themes). |
| **Floating window background** | A 0–100 % slider for the speedometer's background only. The numbers, dial and buttons stay solid at any setting. The speedometer appears for a moment whenever you change this or the theme, so you can see the result. |

**Data**

| Setting | What it does |
|---|---|
| **Remove last session** | Takes your most recent run out of every total. If that run set your peak speed, your previous peak comes back. Press again to remove the run before it, up to 50 runs back. |
| **Reset peak speed** | Clears only your best-run record. |
| **Reset word count** | Sets lifetime words back to zero. |
| **Reset all** | Erases every stat and all history. Your Typer Name is kept. |

Every one of these asks for confirmation first and can't be undone.

**About**

| Setting | What it does |
|---|---|
| **Updates** | Shows your version and checks for, or installs, a new one. |
| **Support Live WPM** | **Buy Me A Coffee** shows a QR code and link to support the project on SupportKori, with buttons to open or copy the link. |

### Themes

<p align="center">
  <img src="docs/images/theme-picker.png" width="600" alt="The theme picker with the seven built-in themes, a custom theme and the Upload theme and Get starter file buttons">
</p>

| Theme | Look |
|---|---|
| **Average Tasin** | The original: flat dark grey (#323437) with a cyan accent. The default. |
| **Beverage Tashhid** | Soft blush pink with deep plum text, extra-round corners and little hearts. |
| **Ahnaf Dark** | Premium black and polished gold (#D5A149), with a gold-lit edge on every card. The speedometer keeps its sky-blue dial. |
| **Makam Blue** | Midnight blue with a periwinkle accent and gold for record pace. |
| **Ziraf Minimal** | Minimal and dark: black surfaces with only white and grey on them, including the floating speedometer. |
| **Adib Red** | Deep wine-black with a crimson accent. Near the top of the dial the arc turns gold. |
| **Safir Glass** | Deep sky-blue glass with a 3D look: raised panes with a lit top edge and a soft shadow, and no colour washes. The floating speedometer shows your blurred desktop with a light deep-blue tint, and the main window uses the same look in deep navy. The opacity slider changes only the speedometer. Needs Windows **Transparency effects** turned on. |

<p align="center">
  <img src="docs/images/speedometer-glass.png" width="300" alt="The Safir Glass floating speedometer showing a blurred, colourful background through its glass">
  <br>
  <sub>The Safir Glass speedometer over a colourful background</sub>
</p>

#### Make your own theme

A theme is a small text file of colours, ending in `.lwtheme`.

1. Open **Settings → Theme → Change** and click **Get starter file**. Save it anywhere.
2. Right-click the file, choose **Open with**, then pick **Notepad**. Change the name and colours, and save it.
3. Back in the theme picker, click **Upload theme** and choose your file. It applies straight away.

```
name = My Theme
background = #14202B
card = #1E2F3D
text = #F2F6F8
accent = #4FD1B5
```

| Line | What it sets | Needed? |
|---|---|---|
| `name` | The name in the picker, up to 20 characters | Yes |
| `background` | The window behind everything | Yes |
| `card` | The panels and the floating speedometer's details | Yes |
| `text` | Main text | Yes |
| `accent` | Highlights, switches and buttons | Yes |
| `muted` | Quieter text | No, worked out for you |
| `border` | Lines and the dial's track | No, worked out for you |
| `dial` | The speedometer's arc | No, uses `accent` |

- Colours are written as `#RRGGBB`.
- A light `background` switches the theme to light-mode behaviour automatically.
- Live WPM refuses a file if the text would be hard to read, and tells you what to change.
- Uploading a theme with the same name as one of yours offers to replace it.
- To remove one of your themes, hover over it in the picker and click the **×**.
- **Uploading stays on your PC.** Nothing goes online. Live WPM keeps its own copy and reads only the name and colours, so a theme file can't run anything. To share a theme, send someone the file.

---

## How speed is measured

Live WPM uses the standard typing-test definition: **one word = 5 characters**. So 300 characters in one minute is 60 WPM, whatever the actual word lengths.

- **Runs.** A run starts with your first key press and ends after 3 seconds without typing.
- **Live speed** (*wpm now*) looks at the last 3 seconds of typing, so it reacts quickly.
- **Peak** is the highest live speed that held for at least 2 seconds, so one quick burst can't set a record.
- **Accuracy** is the share of characters you didn't delete with Backspace.

A run counts toward your records, averages and graphs only if:

- it has at least **25 characters** and lasts at least **3 seconds**
- it doesn't look like gaming, meaning no more than 20% of key presses were held down (WASD movement, for example)

Shorter runs still add to your word count.

---

## Updates

Live WPM never updates without asking.

1. About 30 seconds after it starts, and at most **once a day**, it checks GitHub for a newer release.
2. If one exists, a notification appears: *"Version X is available. Open Settings to update."*
3. Open **Settings → Updates** and click **Update**, then confirm.
4. The new version downloads, replaces the old one and restarts by itself. Your stats and settings stay as they are.

You can also click **Check** in Settings at any time.

On Linux, see [Updating on Linux](#updating-on-linux).

> [!IMPORTANT]
> Automatic updates start from version 1.0.0. Earlier test builds have to be replaced by hand once.

---

## Your data

All your stats and settings are in one file:

```
%APPDATA%\Live WPM\stats.dat
```

Paste `%APPDATA%\Live WPM` into File Explorer's address bar to open the folder.

On Linux it's `~/.config/Live WPM/stats.dat`, in the same format, with your own themes in `~/.config/Live WPM/themes`. See [Moving your stats from Windows](#moving-your-stats-from-windows).

- **Back up or move to a new PC:** copy `stats.dat` into the same folder on the other PC while Live WPM is closed.
- **Start fresh:** use **Settings → Reset all**, or delete the file while the app is closed.
- **Your own themes** are in the `themes` folder beside it. Copy that folder too when moving to a new PC. **Reset all** doesn't remove them.

The file is written safely: a new copy is saved first and then swapped in, so a crash or power cut can't leave it half-written.

---

## Uninstalling

Live WPM doesn't install anything, so removing it takes three steps:

1. Open **Settings** and turn off **Start with Windows**. This removes its startup entry.
2. Right-click the tray icon and choose **Exit**.
3. Delete `Live WPM.exe`. To remove your stats too, delete the `%APPDATA%\Live WPM` folder.

On Linux, see [Uninstalling on Linux](#uninstalling-on-linux).

---

## FAQ

<details>
<summary><b>Windows says "Windows protected your PC". Is it safe?</b></summary>
<br>

That's Microsoft SmartScreen. It appears for any app that isn't code-signed and hasn't been downloaded by many people yet. It isn't a virus warning. Click **More info → Run anyway**. The warning fades as more people download a release.

The full source code is in this repository, so you can read it or [build the exe yourself](#building-from-source).
</details>

<details>
<summary><b>My antivirus flagged it. Why?</b></summary>
<br>

To measure typing in every app, Live WPM uses a standard Windows keyboard hook (`WH_KEYBOARD_LL`). Keyloggers use the same mechanism, so some antivirus tools flag new, unsigned apps that use it, even when the app does nothing harmful. This is a false positive.

Live WPM keeps only key *types*, never which key was pressed, and sends nothing anywhere except the update check. You can confirm this in [`src/KeyboardHook.cs`](src/KeyboardHook.cs) and [`src/TypingEngine.cs`](src/TypingEngine.cs). If your antivirus quarantines it, restore it and add it as an exception, or build it from source.
</details>

<details>
<summary><b>Does it work on Linux?</b></summary>
<br>

Yes. See [Linux](#linux) for installing it, the one-time keyboard step and [Troubleshooting on Linux](#troubleshooting-on-linux).
</details>

<details>
<summary><b>Does it record my passwords?</b></summary>
<br>

No. No text is recorded at all, passwords included. See [Privacy](#privacy).
</details>

<details>
<summary><b>Why didn't my typing count?</b></summary>
<br>

Check these in order:

- **Enabled** is off in the tray menu (the icon is grey).
- A full-screen app or game was in front.
- The run was under 25 characters or 3 seconds. Its words are counted, but it doesn't affect your records or averages.
- You were using shortcuts or holding keys down. Those are ignored on purpose.
</details>

<details>
<summary><b>The speedometer disappeared.</b></summary>
<br>

With **Only when typing** on, it hides about 4 seconds after you stop typing. If it doesn't come back when you type, turn **Floating window** back on in the tray menu. Clicking the ✕ turns that switch off.
</details>

<details>
<summary><b>The speedometer is off-screen after I changed monitors.</b></summary>
<br>

Live WPM moves the dial back onto a visible screen when it starts. Exit and reopen the app to reset its position.
</details>

<details>
<summary><b>Does it slow my PC down?</b></summary>
<br>

No. It does a few counter updates per key press and redraws only while the dial is visible. Memory use is kept low and trimmed while idle.
</details>

<details>
<summary><b>Can I use it on more than one PC?</b></summary>
<br>

Yes. Each PC keeps its own stats. To carry stats over, copy `stats.dat` as described in [Your data](#your-data).
</details>

---

## Building from source

No Visual Studio or .NET SDK needed. The build uses the C# compiler that's already part of Windows.

```powershell
git clone https://github.com/tasin-py/Live-WPM.git
cd Live-WPM
powershell -ExecutionPolicy Bypass -File build.ps1
```

The result goes in its own folder per version, named from `AssemblyVersion` in `src\Program.cs`. For example, version 1.7.0 builds to `versions\v1.7.0\Live WPM.exe`. Earlier builds stay in their own folders, so building a new version never overwrites an old one.

**What the build does**

- Compiles every `src\*.cs` file with `csc.exe` from .NET Framework 4.x.
- Embeds everything in `src\assets\`, and any `.ttf` or `.otf` fonts in `src\fonts\`, into the exe.
- Applies the icon (`src\app.ico`) and the DPI manifest (`src\app.manifest`). If the icon is missing, the build draws it first using the app's own code.

**Optional: app font.** Live WPM is designed for **TBJ Neuetra Sans**. Its licence doesn't allow sharing the files, so they aren't in this repository. If you own the font, put its `.ttf` or `.otf` files in `src\fonts\` before building. Without them, the app uses Segoe UI.

**Code style.** The code is limited to C# 5, the version the built-in compiler supports. That means no string interpolation (`$"..."`), no `?.` and no `=>` members.

**Linux.** The Linux edition is in `linux/`: C# on .NET 10 with [Avalonia](https://avaloniaui.net/) for the windows. It compiles the shared files from `src/` (typing engine, stats, themes and speed graph) unchanged, so both editions measure, store and look the same. With the .NET 10 SDK installed (`sudo pacman -S dotnet-sdk` on Arch):

```bash
./linux/build.sh
```

This makes `versions/v1.7.0/live-wpm-linux-x64.tar.gz`: one self-contained program (no .NET needed on the PC) plus `install.sh`. The version comes from `<Version>` in `linux/LiveWpm.Linux.csproj`. Keep it the same as the Windows one.

**Command-line options**

| Option | Purpose |
|---|---|
| `--tray` | Start hidden in the tray. Used by Start with Windows. |
| `--make-icon <path>` | Write the app icon to a `.ico` file. Used by the build. |
| `--updated` | Internal. Used when restarting after an update. |
| `--version` | Linux only. Prints the version. |

---

## Publishing a release

Releases drive the in-app updater, so the steps must be followed exactly.

1. Raise the version in `src/Program.cs`:
   ```csharp
   [assembly: AssemblyVersion("1.7.0.0")]
   [assembly: AssemblyFileVersion("1.7.0.0")]
   ```
   Use the same version in `linux/LiveWpm.Linux.csproj` (`<Version>1.7.0</Version>`) and `linux/packaging/arch/PKGBUILD` (`pkgver=1.7.0`): both editions share one version and one release.
2. Build Windows with `build.ps1`, and Linux with `linux/build.sh` (on Linux, or in WSL).
3. On GitHub, go to **Releases → Draft a new release**.
4. Create a tag that matches the version: **`v1.7.0`**.
5. Attach the two files from `versions\v1.7.0\`: **`Live WPM.exe`** and **`live-wpm-linux-x64.tar.gz`**. Not the whole folder.
6. Write what changed, then click **Publish release**.

Or do it all from a terminal with the GitHub CLI:

```powershell
gh release create v1.7.0 "versions/v1.7.0/Live WPM.exe" "versions/v1.7.0/live-wpm-linux-x64.tar.gz" --title "Live WPM 1.7.0" --notes "What changed"
```

Within a day, every running copy shows the update notification.

> [!WARNING]
> The tag must **match** the version inside the new exe (`v1.7.0` for `1.7.0.0`) and be **higher** than the previous release. A copy only updates when the tag is higher than its own version. A lower tag means nobody gets the update. A tag higher than the new exe's version means people who update are offered the same update again every day. If you fork this project, set `Repo` in [`src/Updater.cs`](src/Updater.cs) to your own `owner/repository`, or the updater will look at the wrong place.

---

## Project structure

```
Live WPM/
├── build.ps1                 Windows build script (no SDK needed)
├── versions/                 Built apps, one folder per version (v1.0.0, v1.1.0, ...)
├── README.md
├── docs/images/              Screenshots for this page
└── src/
    ├── Program.cs            Entry point, tray icon, single-instance, Start with Windows
    ├── KeyboardHook.cs       Low-level keyboard hook: sorts keys into kinds, skips full-screen games
    ├── TypingEngine.cs       Runs, live speed, peak, accuracy, gaming filter
    ├── DataStore.cs          Settings and stats, saved to %APPDATA%\Live WPM\stats.dat
    ├── FloatingWindow.cs     The floating speedometer
    ├── MainWindow.cs         Home, History and Settings pages
    ├── SpeedGraph.cs         Daily speed graph drawing
    ├── DayGraphWindow.cs     Pop-up graph for one day in History
    ├── TrayMenu.cs           The tray quick menu
    ├── NamePrompt.cs         Typer Name dialog
    ├── DonateWindow.cs       SupportKori QR and link
    ├── Updater.cs            GitHub release check and self-update
    ├── Theme.cs              Themes, colours, fonts, text drawing, DPI-aware window base
    ├── ThemePicker.cs        The theme picker window
    ├── ThemeFiles.cs         Loads, checks and saves the Typer's own theme files (shared with Linux)
    ├── Palettes.cs           The theme list and colour maths (shared with Linux)
    ├── GlassCanvas.cs        Glass drawing surface for the Safir Glass main window
    ├── app.ico / app.manifest
    ├── assets/               Images built into the exe
    └── fonts/                Optional app font (not in the repository)

linux/                        Live WPM for Linux (.NET 10 + Avalonia)
├── LiveWpm.Linux.csproj      Also compiles TypingEngine, DataStore, ThemeFiles, Palettes and SpeedGraph from src/
├── build.sh / install.sh     Builds the release download / installs it for one user
├── EvdevKeyboard.cs          Reads the keyboard devices: only the kind of key, never which key
├── KeyboardService.cs        The keyboard service (live-wpm --keyboard-service), run locked down by systemd
├── KeyboardInput.cs          The app's side: takes key kinds from the service
├── Gfx.cs                    The drawing calls the Windows paint code uses, drawn with Avalonia
├── Theme.cs                  Colours in use, Inter font, drawing helpers
├── Program.cs                Startup, tray icon, one copy at a time
├── Platform.cs               Links, notifications, Start on login
├── Updater.cs                GitHub release check and self-update
├── *Window.cs, NamePrompt.cs, ThemePicker.cs   The same windows as on Windows
└── packaging/                Arch PKGBUILD, the menu entry and the keyboard service's systemd units
```

---

## Support

Live WPM is free. If it's useful to you, you can support it on [SupportKori](https://www.supportkori.com/tasinpy), or open the app and go to **Settings → Buy Me A Coffee** to scan the QR code.

Found a bug or have an idea? [Open an issue](https://github.com/tasin-py/Live-WPM/issues).

<p align="center">
  <b>Tasin's Problem</b><br>
  <a href="https://github.com/tasin-py/">github.com/tasin-py</a> · <a href="https://fb.com/tasin.py">fb.com/tasin.py</a> · Discord: <code>tasin_py</code>
</p>
