// TERMINAL HELL - Linux terminal management: raw mode, alternate screen, size, output and a clean exit.
// (the Windows version of this class is src/TermWin.cs)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TerminalHell
{
    static class Term
    {
        public static bool IsClassicConsole;           // always false: the game can't resize or re-font a Linux terminal
        public static bool VtOk = true;
        public static float CellAspect = 1f;           // pixel width / pixel height for half-block pixels
        public const string FullscreenKey = "F11";
        public static int CellW = 9, CellH = 18;       // one character cell in pixels (the usual shape until the terminal tells us)
        public static bool CellSizeKnown;
        public static int Cols = 80, Rows = 24;
        public static bool KittyPushed;                // InputLinux pushed kitty keyboard flags that must be popped on exit

        static readonly object sync = new object();
        static byte[] origTermios;
        static bool active;
        static int savedStderr = -1;
        static byte[] obuf = new byte[1 << 18];
        static bool pixelsFromIoctl;
        static PosixSignalRegistration[] signals;

        const string SyncBegin = "\x1b[?2026h", SyncEnd = "\x1b[?2026l";   // synchronized output: whole frames, no tearing

        public static void Init(Settings s)
        {
            LibC.Setup();
            if (LibC.isatty(0) == 0 || LibC.isatty(1) == 0)
                throw new InvalidOperationException("TERMINAL HELL has to run in a terminal (its input and output can't be redirected).");
            var t = new byte[256];
            if (LibC.tcgetattr(0, t) != 0) throw new InvalidOperationException("can't read the terminal settings (errno " + LibC.Errno + ")");
            origTermios = t;
            if (LibC.tcsetattr(0, LibC.TCSANOW, LibC.MakeRaw(t)) != 0) throw new InvalidOperationException("can't switch the terminal to raw mode (errno " + LibC.Errno + ")");
            active = true;

            // however the game ends, give the shell its terminal back
            AppDomain.CurrentDomain.ProcessExit += delegate { Restore(); };
            AppDomain.CurrentDomain.UnhandledException += delegate { Restore(); };
            try
            {
                signals = new[]
                {
                    PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal),
                    PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnSignal),
                    PosixSignalRegistration.Create(PosixSignal.SIGINT, OnSignal),
                    PosixSignalRegistration.Create(PosixSignal.SIGQUIT, OnSignal),
                };
            }
            catch { }
            RedirectStderr();

            if (s.Display == DisplayMode.Legacy) s.Display = DisplayMode.HD;   // 16 colors is a Windows console mode
            // alternate screen, save the window title and set ours, hide the cursor, no auto wrap
            Write("\x1b[?1049h\x1b[22;0t\x1b]0;TERMINAL HELL\x07\x1b[0m\x1b[2J\x1b[?25l\x1b[?7l");
            int c, r;
            GetSize(out c, out r);
        }

        static void OnSignal(PosixSignalContext ctx)
        {
            Restore();   // the default action (quitting) follows
        }

        /// <summary>Anything written to stderr (sound libraries love to complain there) would land on top of the picture:
        /// send it to a log file while the game runs.</summary>
        static void RedirectStderr()
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                int fd = LibC.open(Path.Combine(Settings.Dir, "stderr.log"), LibC.O_WRONLY | LibC.O_CREAT | LibC.O_TRUNC | LibC.O_CLOEXEC, 420 /* 0644 */);
                if (fd < 0) return;
                savedStderr = LibC.dup(2);
                if (savedStderr >= 0) LibC.dup2(fd, 2);
                LibC.close(fd);
            }
            catch { }
        }

        public static void HideCursor()
        {
            Write("\x1b[?25l\x1b[?7l");
        }

        /// <summary>Nothing to apply: Linux terminals choose their own font and window size (the player zooms with Ctrl +/-).</summary>
        public static void ApplyVideo(Settings s) { }

        /// <summary>Current size in cells. Also tracks the cell size in pixels, which sets the shape of the half-block pixels.</summary>
        public static void GetSize(out int cols, out int rows)
        {
            int xp, yp;
            if (!LibC.GetWinSize(1, out cols, out rows, out xp, out yp) || cols <= 0 || rows <= 0) { cols = 80; rows = 24; xp = yp = 0; }
            if (cols != Cols || rows != Rows || !CellSizeKnown)
            {
                bool changed = cols != Cols || rows != Rows;
                Cols = cols; Rows = rows;
                if (xp > 0 && yp > 0)
                {
                    pixelsFromIoctl = true;
                    SetCellSize(xp / cols, yp / rows);
                }
                else if (changed && !pixelsFromIoctl)
                {
                    Write("\x1b[16t");   // ask for the cell size in pixels (the answer arrives as input: CSI 6 ; h ; w t)
                }
            }
        }

        /// <summary>Cell size in pixels, from the window size ioctl or the terminal's answer to CSI 16 t.</summary>
        public static void SetCellSize(int w, int h)
        {
            if (w < 2 || h < 4 || w > 200 || h > 400) return;
            CellW = w; CellH = h;
            CellSizeKnown = true;
            float a = w / (h * 0.5f);
            CellAspect = a < 0.6f || a > 1.8f ? 1f : a;
        }

        public static void Write(string s)
        {
            lock (sync)
            {
                if (!active) return;
                int n = Encoding.UTF8.GetBytes(s, 0, s.Length, Buf(s.Length * 3), 0);
                LibC.WriteAll(1, obuf, n);
            }
        }

        /// <summary>One frame from the presenter, wrapped in synchronized-output markers so the terminal shows it all at once.</summary>
        public static void Write(char[] buf, int count)
        {
            lock (sync)
            {
                if (!active) return;
                var b = Buf(count * 3 + 32);
                int n = Ascii(b, 0, SyncBegin);
                n += Encoding.UTF8.GetBytes(buf, 0, count, b, n);
                n += Ascii(b, n, SyncEnd);
                LibC.WriteAll(1, b, n);
            }
        }

        static byte[] Buf(int size)
        {
            if (obuf.Length < size) obuf = new byte[size + size / 2];
            return obuf;
        }

        static int Ascii(byte[] b, int at, string s)
        {
            for (int i = 0; i < s.Length; i++) b[at + i] = (byte)s[i];
            return s.Length;
        }

        public static string Diag()
        {
            int c, r, xp, yp;
            LibC.GetWinSize(1, out c, out r, out xp, out yp);
            return "term=" + Env("TERM") + " program=" + Env("TERM_PROGRAM") + " size=" + c + "x" + r + " pixels=" + xp + "x" + yp +
                " cell=" + CellW + "x" + CellH + (CellSizeKnown ? "" : "(guess)") + " " + Input.Describe();
        }

        public static string Env(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(v) ? "-" : v;
        }

        /// <summary>Ctrl+C: leave straight away, like any other terminal program.</summary>
        public static void QuitNow()
        {
            try { Input.Shutdown(); } catch { }
            try { Audio.Shutdown(); } catch { }
            Restore();
            Environment.Exit(0);
        }

        public static void Restore()
        {
            lock (sync)
            {
                if (!active) return;
                active = false;
                try
                {
                    var sb = new StringBuilder();
                    if (KittyPushed) { sb.Append("\x1b[<u"); KittyPushed = false; }   // pop our keyboard flags
                    // stop mouse and focus reports, colors / cursor / wrapping back, leave the alternate screen, restore the title
                    sb.Append("\x1b[?1003l\x1b[?1002l\x1b[?1000l\x1b[?1006l\x1b[?1016l\x1b[?1004l\x1b[?2026l\x1b[0m\x1b[?25h\x1b[?7h\x1b[?1049l\x1b[23;0t");
                    var b = Encoding.ASCII.GetBytes(sb.ToString());
                    LibC.WriteAll(1, b, b.Length);
                    // TCSADRAIN waits until the terminal has read all of that; then drop any key/mouse reports still queued
                    LibC.tcsetattr(0, LibC.TCSADRAIN, origTermios);
                    Thread.Sleep(20);
                    LibC.tcflush(0, LibC.TCIFLUSH);
                }
                catch { }
                if (savedStderr >= 0) { LibC.dup2(savedStderr, 2); LibC.close(savedStderr); savedStderr = -1; }
            }
        }
    }

    /// <summary>The 16 color mode only exists in the Windows console; on Linux it shows HD pixels instead.</summary>
    sealed class LegacyPresenter
    {
        readonly VtPresenter vt = new VtPresenter();
        public void Present(Screen s) { vt.Present(s, false); }
    }
}
