#!/bin/bash
# Builds Live WPM for Linux as one self-contained, trimmed program (no .NET needed on the PC) and packs it
# with install.sh into versions/v<version>/live-wpm-linux-<arch>.tar.gz.
#   ./build.sh          x64 (most PCs)
#   ./build.sh arm64    ARM PCs
# Needs the .NET 10 SDK (Arch: sudo pacman -S dotnet-sdk).
set -euo pipefail
cd "$(dirname "$0")"
ARCH="${1:-x64}"
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' LiveWpm.Linux.csproj)
OUT="$(cd .. && pwd)/versions/v$VERSION"
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

dotnet publish LiveWpm.Linux.csproj -c Release -r "linux-$ARCH" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
    -p:PublishTrimmed=true \
    -nologo -o "$WORK/live-wpm"
cp install.sh assets/live-wpm.png "$WORK/live-wpm/"
chmod +x "$WORK/live-wpm/live-wpm" "$WORK/live-wpm/install.sh"

mkdir -p "$OUT"
tar -C "$WORK" -czf "$OUT/live-wpm-linux-$ARCH.tar.gz" live-wpm
echo "Built $OUT/live-wpm-linux-$ARCH.tar.gz ($(du -h "$OUT/live-wpm-linux-$ARCH.tar.gz" | cut -f1))"
