#!/bin/sh
# Installs Live WPM for your user only (no root needed):
#   ~/.local/bin/live-wpm, an app-menu entry and an icon.
# Remove it again with:  ./install.sh --uninstall
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"
BIN="$HOME/.local/bin"
APPS="$DATA/applications"
ICONS="$DATA/icons/hicolor/256x256/apps"
AUTOSTART="${XDG_CONFIG_HOME:-$HOME/.config}/autostart/live-wpm.desktop"

if [ "${1:-}" = "--uninstall" ]; then
    pkill -x live-wpm 2>/dev/null || true
    rm -f "$BIN/live-wpm" "$APPS/live-wpm.desktop" "$ICONS/live-wpm.png" "$AUTOSTART"
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

# Live WPM reads the keyboard device to measure speed, which needs the "input" group, once.
if id -nG | tr ' ' '\n' | grep -qx input; then
    echo "Keyboard access: ready."
else
    echo
    echo "One more step. Live WPM reads your keyboard device to measure your speed."
    echo "It keeps only the kind of key (a letter, a space or a backspace), never which key,"
    echo "and nothing leaves your PC. Your account needs to be in the 'input' group:"
    echo
    echo "    sudo usermod -aG input $USER"
    echo
    printf "Run that now? [y/N] "
    read -r answer || answer=n
    case "$answer" in
        y|Y|yes) sudo usermod -aG input "$USER" && echo "Done. Log out and back in, then open Live WPM." ;;
        *) echo "Skipped. Run the command above later, then log out and back in." ;;
    esac
fi
case ":$PATH:" in *":$BIN:"*) ;; *) echo "(Note: $BIN isn't on your PATH, so start Live WPM from the app menu.)" ;; esac
