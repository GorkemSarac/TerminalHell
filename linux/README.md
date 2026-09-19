# TERMINAL HELL on Linux

The same game as the Windows version, running in any Linux terminal.

## Install

```bash
curl -fsSL https://raw.githubusercontent.com/GorkemSarac/TerminalHell/main/linux/install.sh | sh
```

This installs one self-contained program as `~/.local/bin/terminalhell` and adds TERMINAL HELL to your applications menu. You don't need .NET or anything else. Run the command again to update.

Uninstall with `sh ~/.local/share/TerminalHell/uninstall.sh`.

Supported systems: 64-bit Linux (x86-64 or ARM64) with glibc 2.27 or newer, which covers Ubuntu 18.04+, Debian 10+, Fedora and Arch. Alpine and other musl systems need their own build (see [Building](#building)).

## Getting the most out of it

**Picture.** A Linux program can't resize its terminal or change its font, so the game draws at whatever size the terminal is. For more detail, maximize the window and zoom out a few steps with Ctrl + minus (Ctrl + Shift + minus in kitty).

**Keyboard.** Most terminals only tell a program that a key was pressed, never that it was released. The game handles this in one of three ways, best first:

| Where | What you get |
|---|---|
| kitty, foot, Ghostty, Alacritty, WezTerm (set `enable_kitty_keyboard = true`) | Real key releases through the kitty keyboard protocol. Hold several keys at once, like on Windows. |
| Any terminal, with the `input` group (below) | Real key releases, read straight from the keyboard. |
| Any other terminal (GNOME Terminal, Konsole, xterm, tmux…) | Releases are guessed from key repeat. Playable, but holding two movement keys at once doesn't work well. |

**Mouse look.** Terminals only report which character cell the pointer is over, and they can't hide or lock the pointer. Without the `input` group, turning follows the pointer inside the window. Push it to the left or right edge to keep turning. With the `input` group, you get proper mouse look: the game grabs the mouse while you play, like the Windows version, and gives it back in menus or when you switch windows.

**The `input` group.** This gives the game real key releases and proper mouse look in any terminal:

```bash
sudo usermod -aG input $USER
```

Then log out and back in. This also lets every other program you run read your keyboard and mouse directly, so only do it if you're comfortable with that.

**Check your setup.** `terminalhell --input` shows what your terminal supports (key releases, focus reports, mouse) and every key and mouse event as it arrives.

**Sound** plays through PulseAudio (which PipeWire also provides) or ALSA. Without either, the game runs silently.

**Files.** Settings are in `~/.local/share/TerminalHell/settings.cfg`. F12 screenshots go to `~/Pictures/TerminalHell`.

**If the terminal looks broken after a crash**, type `reset` and press Enter.

## Options

```
terminalhell --ascii        start in ASCII display mode
terminalhell --level N      jump straight to level N (1-3)
terminalhell --nomouse      keyboard only (arrow keys turn)
terminalhell --nosound      no audio
terminalhell --no-evdev     never read keyboards and mice from /dev/input
terminalhell --no-kitty     don't use the kitty keyboard protocol
terminalhell --input        check what this terminal gives the game
```

## Building

You need the free [.NET 10 SDK](https://dot.net). Visual Studio isn't needed.

- **On Linux:** `sh linux/build.sh` creates `linux/dist/terminalhell-linux-x64` (or `-arm64`). Add `--run` to start it straight away, or run `sh linux/install.sh` to install your build. For Alpine, use `sh linux/build.sh linux-musl-x64`.
- **On Windows:** `.\linux\build.ps1` creates both `terminalhell-linux-x64` and `terminalhell-linux-arm64` in `linux\dist`.

### Publishing a release

The installer downloads `terminalhell-linux-x64` or `terminalhell-linux-arm64` from the **latest GitHub release**:

1. Run `.\linux\build.ps1` on Windows.
2. Create a release on GitHub.
3. Attach both files from `linux\dist`. The names must stay exactly as they are.

Until a release exists, the installer builds the game from source if the .NET 10 SDK is installed. Otherwise it explains what's missing.

## How the port is organised

The game itself (rendering, levels, monsters, weapons, sound synthesis, music, menus) lives in `../src`. Windows and Linux build from the same files.

Only the platform layer differs:

| | Windows (`../src`) | Linux (`src`) |
|---|---|---|
| Terminal | `TermWin.cs`: console buffer, font and window size | `TermLinux.cs`: raw mode, alternate screen, size, synchronized output, clean exit on signals |
| Keyboard and mouse | `InputWin.cs`: console events, raw input, cursor lock | `InputLinux.cs` + `VtInput.cs` (escape sequences, kitty protocol, SGR mouse) + `LegacyKeys.cs` (guessed releases) + `Evdev.cs` (/dev/input) |
| Audio output | `AudioWin.cs`: waveOut | `AudioLinux.cs`: PulseAudio / ALSA |
| Screenshots | `DebugTools.cs`: System.Drawing | `DebugToolsLinux.cs`: its own PNG writer |
| System calls | `Native.cs` | `LibC.cs` |

`TerminalHell.Linux.csproj` compiles the shared files plus `src`, and defines `LINUX` for the few `#if LINUX` spots in shared code.

There are two sets of tests:

- `terminalhell --dev-linux-selftest` checks the input decoding. It runs anywhere, even on Windows with `dotnet run`.
- `tests/pty_smoke.py` runs the real game in a pseudo terminal. It checks drawing, keys, Ctrl+C, SIGTERM, and that the terminal is always restored.

GitHub Actions runs both on every push.
