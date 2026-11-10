#!/bin/sh
# Installs Live WPM:
#   - the app, for your user only (no root): ~/.local/bin/live-wpm, an app-menu entry and an icon;
#   - the keyboard service, if you agree (needs sudo once): it reads the keyboard for Live WPM,
#     locked down by systemd, so your own account needs no extra rights.
# Remove everything again with:  ./install.sh --uninstall
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"
BIN="$HOME/.local/bin"
APPS="$DATA/applications"
ICONS="$DATA/icons/hicolor/256x256/apps"
AUTOSTART="${XDG_CONFIG_HOME:-$HOME/.config}/autostart/live-wpm.desktop"
SERVICE_BIN=/usr/local/lib/live-wpm/live-wpm
UNITS=/etc/systemd/system

if [ "${1:-}" = "--uninstall" ]; then
    pkill -x live-wpm 2>/dev/null || true
    rm -f "$BIN/live-wpm" "$APPS/live-wpm.desktop" "$ICONS/live-wpm.png" "$AUTOSTART"
    if [ -e "$UNITS/live-wpm-keys.socket" ] || [ -e "$SERVICE_BIN" ]; then
        echo "Removing the keyboard service (needs sudo):"
        sudo systemctl disable --now live-wpm-keys.socket live-wpm-keys.service 2>/dev/null || true
        sudo rm -f "$UNITS/live-wpm-keys.socket" "$UNITS/live-wpm-keys.service" "$SERVICE_BIN"
        sudo rmdir /usr/local/lib/live-wpm 2>/dev/null || true
        sudo systemctl daemon-reload
    fi
    echo "Live WPM is removed."
    echo "Your stats are still in ~/.config/Live WPM. Delete that folder to remove them too."
    exit 0
fi

mkdir -p "$BIN" "$APPS" "$ICONS"
pkill -x live-wpm 2>/dev/null || true
install -m 755 "$HERE/live-wpm" "$BIN/live-wpm"
install -m 644 "$HERE/live-wpm.png" "$ICONS/live-wpm.png"
cat > "$APPS/live-wpm.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Live WPM
GenericName=Typing speedometer
Comment=See how fast you type, live, in any app
Exec="$BIN/live-wpm"
Icon=live-wpm
Terminal=false
Categories=Utility;
Keywords=typing;wpm;speed;keyboard;
EOF
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS" >/dev/null 2>&1 || true
echo "Live WPM is installed: $BIN/live-wpm (and in your app menu)."

# The keyboard service. Linux only lets programs in the "input" group read keyboards, and putting your
# own account in that group would let every program you run read your keys. Instead, only this small
# service gets that access, with no network, no access to your files, and a temporary user of its own.
if ! command -v systemctl >/dev/null 2>&1; then
    echo
    echo "This system doesn't use systemd, so the keyboard service can't be installed."
    echo "See 'Without systemd' under Linux in the README for the other way."
    exit 0
fi
echo
echo "One more step: the keyboard service."
echo "Live WPM needs to see when you type. A small service reads the keyboard for it and passes on only"
echo "the kind of each key (a letter, a space or a backspace), never which key. systemd runs it locked"
echo "down: no network at all, no access to your files, and your own account gets no extra rights."
echo "Installing it needs sudo once. It puts a copy of Live WPM in $SERVICE_BIN"
echo "and two systemd files in $UNITS."
printf "Install the keyboard service now? [Y/n] "
read -r answer || answer=n
case "$answer" in
    n|N|no) echo "Skipped. Run ./install.sh again any time to add it."; exit 0 ;;
esac
sudo install -D -o root -g root -m 755 "$HERE/live-wpm" "$SERVICE_BIN"
UNIT=$(mktemp)
sed "s|/usr/lib/live-wpm/live-wpm|$SERVICE_BIN|" "$HERE/live-wpm-keys.service" > "$UNIT"
sudo install -o root -g root -m 644 "$UNIT" "$UNITS/live-wpm-keys.service"
rm -f "$UNIT"
sudo install -o root -g root -m 644 "$HERE/live-wpm-keys.socket" "$UNITS/live-wpm-keys.socket"
sudo systemctl daemon-reload
sudo systemctl enable --now live-wpm-keys.socket
echo "The keyboard service is on. Open Live WPM and start typing."
case ":$PATH:" in *":$BIN:"*) ;; *) echo "(Note: $BIN isn't on your PATH, so start Live WPM from the app menu.)" ;; esac
