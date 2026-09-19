#!/bin/sh
# TERMINAL HELL - one-line installer for Linux
#
#   curl -fsSL https://raw.githubusercontent.com/GorkemSarac/TerminalHell/main/linux/install.sh | sh
#
# What it does:
#   1. downloads the game from the latest GitHub release: one self-contained program, nothing else to install
#      (if there is no download for this computer yet and the .NET 10 SDK is installed, it builds it from source)
#   2. installs it as ~/.local/bin/terminalhell
#   3. adds "TERMINAL HELL" to the applications menu
# Running it again updates an existing installation. TERMINALHELL_NO_LAUNCH=1 skips the "Play now?" question.
set -eu

REPO="GorkemSarac/TerminalHell"   # GitHub "owner/repository" (change with tools/set-repo.ps1)
BRANCH="main"
BIN_DIR="$HOME/.local/bin"
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
EXE="$BIN_DIR/terminalhell"
HERE=""   # the linux folder of a checkout, when run as ./linux/install.sh (empty when piped from curl)
case "$0" in *install.sh) HERE=$(cd "$(dirname "$0")" 2>/dev/null && pwd || echo "") ;; esac

say() { printf '%s\n' "$*"; }
note() { printf '\033[33m%s\033[0m\n' "$*"; }
die() { printf '\033[31merror: %s\033[0m\n' "$*" >&2; exit 1; }

printf '\033[31m'
cat <<'EOF'

  _____ ___ ___ __  __ ___ _  _   _   _      _  _ ___ _    _
 |_   _| __| _ \  \/  |_ _| \| | /_\ | |    | || | __| |  | |
   | | | _||   / |\/| || || .` |/ _ \| |__  | __ | _|| |__| |__
   |_| |___|_|_\_|  |_|___|_|\_/_/ \_\____| |_||_|___|____|____|
EOF
printf '\033[33m   a first person shooter for your terminal\033[0m\n\n'

[ "$(uname -s)" = "Linux" ] || die "this installer is for Linux (on Windows, use install.ps1)"
case "$(uname -m)" in
    x86_64 | amd64) ARCH=x64 ;;
    aarch64 | arm64) ARCH=arm64 ;;
    *) die "there is no build for this processor ($(uname -m)); see linux/README.md to build it yourself" ;;
esac
RID="linux-$ARCH"
if [ -e "/lib/ld-musl-x86_64.so.1" ] || [ -e "/lib/ld-musl-aarch64.so.1" ]; then RID="linux-musl-$ARCH"; fi   # Alpine and friends
if [ "$(id -u)" = "0" ]; then note "Note: installing for the root user. Run it without sudo to install it for yourself."; fi

DL=""
if command -v curl >/dev/null 2>&1; then DL=curl; elif command -v wget >/dev/null 2>&1; then DL=wget; fi

fetch() {   # fetch URL FILE
    case "$DL" in
        curl) curl -fsSL --retry 2 -o "$2" "$1" ;;
        wget) wget -q -O "$2" "$1" ;;
        *) return 1 ;;
    esac
}

have_sdk() {
    command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -Eq '^(1[0-9]|[2-9][0-9])\.'
}

build() {   # build PROJECT_DIR: publishes the game into $TMP/terminalhell
    say "Building from source with the .NET SDK (a minute or two)..."
    if ! dotnet publish "$1/TerminalHell.Linux.csproj" -c Release -r "$RID" -o "$TMP/out" -nologo >"$TMP/build.log" 2>&1; then
        cat "$TMP/build.log" >&2
        die "the build failed"
    fi
    mv "$TMP/out/terminalhell" "$TMP/terminalhell"
}

TMP=$(mktemp -d 2>/dev/null || mktemp -d -t terminalhell)
trap 'rm -rf "$TMP"' EXIT
trap 'exit 1' INT TERM

# ---- the game: a local build (running ./install.sh from a checkout), a release download, or a build from source
if [ -n "$HERE" ] && [ -f "$HERE/dist/terminalhell-$RID" ]; then
    say "Using the local build linux/dist/terminalhell-$RID"
    cp "$HERE/dist/terminalhell-$RID" "$TMP/terminalhell"
elif [ -n "$HERE" ] && [ -f "$HERE/TerminalHell.Linux.csproj" ] && have_sdk; then
    build "$HERE"
else
    [ -n "$DL" ] || die "curl or wget is needed to download the game"
    say "Downloading TERMINAL HELL ($RID) from github.com/$REPO ..."
    if fetch "https://github.com/$REPO/releases/latest/download/terminalhell-$RID" "$TMP/terminalhell" 2>/dev/null; then
        :
    elif have_sdk; then
        say "There is no download for $RID yet."
        fetch "https://github.com/$REPO/archive/refs/heads/$BRANCH.tar.gz" "$TMP/source.tar.gz"
        mkdir "$TMP/source"
        tar -xzf "$TMP/source.tar.gz" -C "$TMP/source" --strip-components=1
        build "$TMP/source/linux"
    else
        die "there is no download for $RID yet. Install the .NET 10 SDK (https://dot.net) and run this again to build it from source."
    fi
fi

chmod +x "$TMP/terminalhell"
VERSION=$("$TMP/terminalhell" --version 2>/dev/null) || die "the game doesn't run on this system (it needs 64-bit Linux with glibc 2.27 or newer)"

# ---- install (copy next to the old one, then rename: safe even while the old version is running)
mkdir -p "$BIN_DIR"
cp "$TMP/terminalhell" "$EXE.new"
chmod +x "$EXE.new"
mv -f "$EXE.new" "$EXE"

# ---- menu entry, icon and the uninstaller (all optional)
mkdir -p "$DATA_HOME/applications" "$DATA_HOME/icons/hicolor/256x256/apps" "$DATA_HOME/TerminalHell"
if [ -n "$HERE" ] && [ -f "$HERE/../assets/terminalhell.png" ]; then
    cp "$HERE/../assets/terminalhell.png" "$DATA_HOME/icons/hicolor/256x256/apps/terminalhell.png"
    cp "$HERE/uninstall.sh" "$DATA_HOME/TerminalHell/uninstall.sh" 2>/dev/null || true
else
    fetch "https://raw.githubusercontent.com/$REPO/$BRANCH/assets/terminalhell.png" "$DATA_HOME/icons/hicolor/256x256/apps/terminalhell.png" 2>/dev/null || true
    fetch "https://raw.githubusercontent.com/$REPO/$BRANCH/linux/uninstall.sh" "$DATA_HOME/TerminalHell/uninstall.sh" 2>/dev/null || true
fi
cat >"$DATA_HOME/applications/terminalhell.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=TERMINAL HELL
Comment=A first person shooter for your terminal
Exec="$EXE"
Icon=terminalhell
Terminal=true
Categories=Game;ActionGame;
EOF
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$DATA_HOME/applications" >/dev/null 2>&1 || true

rm -rf "$TMP"
say ""
printf '\033[32mTERMINAL HELL %s is installed!\033[0m\n' "$VERSION"
case ":$PATH:" in
    *":$BIN_DIR:"*) say "  Play:       type  terminalhell  in a terminal, or start TERMINAL HELL from your applications menu" ;;
    *) say "  Play:       $EXE   (or start TERMINAL HELL from your applications menu)"
       say "              To type just  terminalhell  add ~/.local/bin to your PATH, for example:"
       say "                echo 'export PATH=\"\$HOME/.local/bin:\$PATH\"' >> ~/.bashrc" ;;
esac
say "  Tips:       maximize the terminal window and zoom out (Ctrl -) for a sharper picture;"
say "              terminalhell --input  shows how well your terminal handles the keyboard and mouse"
if ! id -nG 2>/dev/null | tr ' ' '\n' | grep -qx input; then
    say "              for the best controls in any terminal (real key releases, proper mouse look):"
    say "                sudo usermod -aG input \$USER   then log out and back in"
    say "              (this lets programs you run read your keyboard and mouse directly)"
fi
say "  Uninstall:  sh \"$DATA_HOME/TerminalHell/uninstall.sh\""
say ""

if [ -z "${TERMINALHELL_NO_LAUNCH:-}" ] && [ -t 1 ] && (: </dev/tty) 2>/dev/null; then
    printf 'Play now? [Y/n] '
    answer=""
    read -r answer </dev/tty || answer=n
    case "$answer" in
        [nN]*) ;;
        *) exec "$EXE" </dev/tty ;;
    esac
fi
