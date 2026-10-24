#!/bin/sh
# Installs Rewordly for the current user (no root needed).
set -e
DIR="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$HOME/.local/bin" "$HOME/.local/share/applications" "$HOME/.local/share/icons/hicolor/256x256/apps"
install -m 755 "$DIR/Rewordly" "$HOME/.local/bin/Rewordly"
install -m 644 "$DIR/rewordly.png" "$HOME/.local/share/icons/hicolor/256x256/apps/rewordly.png"
sed "s|^Exec=.*|Exec=$HOME/.local/bin/Rewordly|" "$DIR/rewordly.desktop" > "$HOME/.local/share/applications/rewordly.desktop"
echo "Installed to $HOME/.local/bin/Rewordly. Start it from your app menu or run: Rewordly &"
echo "Recommended packages: xdotool xclip espeak-ng libsecret-tools  (e.g. sudo apt install xdotool xclip espeak-ng libsecret-tools)"
