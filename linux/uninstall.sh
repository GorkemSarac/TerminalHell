#!/bin/sh
# TERMINAL HELL - uninstaller for Linux
#
#   sh ~/.local/share/TerminalHell/uninstall.sh
# or
#   curl -fsSL https://raw.githubusercontent.com/GorkemSarac/TerminalHell/main/linux/uninstall.sh | sh
#
# Removes the game, its menu entry and icon, and the saved settings (screenshots in Pictures are kept).
set -eu
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"

if command -v pkill >/dev/null 2>&1; then pkill -x terminalhell 2>/dev/null || true; fi
rm -f "$HOME/.local/bin/terminalhell" \
      "$DATA_HOME/applications/terminalhell.desktop" \
      "$DATA_HOME/icons/hicolor/256x256/apps/terminalhell.png"
rm -rf "$DATA_HOME/TerminalHell"
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database "$DATA_HOME/applications" >/dev/null 2>&1 || true; fi
printf '\033[32mTERMINAL HELL has been uninstalled.\033[0m\n'
