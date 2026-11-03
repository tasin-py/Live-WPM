# Live WPM

A tiny Windows tray app that shows your live typing speed on a floating speedometer.

- **Private:** it never records which keys you press, only how fast you type. Nothing is sent anywhere, except a once-a-day check for a new version.
- **Light:** a single ~90 KB exe; nothing to install.
- Peak speed, accuracy, words, and a daily history.
- Ignores games (full-screen apps), shortcuts, and held keys.

## Download

Get `Live WPM.exe` from the [latest release](../../releases/latest) and put it in a folder you'll keep, then run it.
Windows may show "Windows protected your PC" the first time. Click **More info → Run anyway**.

Requires Windows 10 or 11.

## Build it yourself

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

It uses the C# compiler that ships with Windows, so you don't need an SDK. The exe goes to `dist\`.

## Support

Made by ~ Tasin · [fb.com/tasin.py](https://fb.com/tasin.py) · Discord: `tasin_py`
Donations via bKash: open **Settings → Donate** in the app.
