#!/bin/sh
# Builds the Linux version of TERMINAL HELL with the .NET 10 SDK (https://dot.net).
#
#   ./linux/build.sh               -> linux/dist/terminalhell-<this computer>, e.g. terminalhell-linux-x64
#   ./linux/build.sh linux-arm64   -> for another runtime (linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64)
#   ./linux/build.sh --run         -> build, then start the game
#   ./linux/install.sh             -> install what you built (or build and install)
set -eu
cd "$(dirname "$0")"

RUN=""
RID=""
for arg in "$@"; do
    case "$arg" in
        --run) RUN=1 ;;
        *) RID="$arg" ;;
    esac
done
if [ -z "$RID" ]; then
    case "$(uname -m)" in
        x86_64 | amd64) RID=linux-x64 ;;
        aarch64 | arm64) RID=linux-arm64 ;;
        *) echo "error: unknown processor $(uname -m): pass a runtime identifier, e.g. linux-x64" >&2; exit 1 ;;
    esac
    if [ -e "/lib/ld-musl-x86_64.so.1" ] || [ -e "/lib/ld-musl-aarch64.so.1" ]; then RID=$(echo "$RID" | sed 's/^linux-/linux-musl-/'); fi
fi
command -v dotnet >/dev/null 2>&1 || { echo "error: the .NET 10 SDK is needed: https://dot.net" >&2; exit 1; }

dotnet publish TerminalHell.Linux.csproj -c Release -r "$RID" -o "dist/$RID" -nologo
cp "dist/$RID/terminalhell" "dist/terminalhell-$RID"
echo "Built linux/dist/terminalhell-$RID"
if [ -n "$RUN" ]; then exec "./dist/terminalhell-$RID"; fi
