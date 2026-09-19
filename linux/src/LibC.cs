// TERMINAL HELL - Linux: libc interop (terminal modes, raw reads and writes, /dev/input devices).
// Constants and struct layouts are the ones shared by x86-64 and arm64 Linux (the two builds we ship).
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace TerminalHell
{
    static class LibC
    {
        const string Lib = "libc";

        public const int O_RDONLY = 0, O_WRONLY = 1, O_CREAT = 0x40, O_TRUNC = 0x200, O_NONBLOCK = 0x800, O_CLOEXEC = 0x80000;
        public const int EPERM = 1, EINTR = 4, EAGAIN = 11, EACCES = 13, ENODEV = 19;
        public const int TCSANOW = 0, TCSADRAIN = 1, TCIFLUSH = 0;
        public const uint TIOCGWINSZ = 0x5413;
        public const uint EVIOCGRAB = 0x40044590;       // _IOW('E', 0x90, int)

        // struct termios: c_iflag, c_oflag, c_cflag, c_lflag (4 bytes each), c_line, c_cc[32], c_ispeed, c_ospeed
        const int IFLAG = 0, LFLAG = 12, CC = 17, VTIME = 5, VMIN = 6;
        const uint IGNBRK = 0x1, BRKINT = 0x2, PARMRK = 0x8, ISTRIP = 0x20, INLCR = 0x40, IGNCR = 0x80, ICRNL = 0x100, IXON = 0x400;
        const uint ISIG = 0x1, ICANON = 0x2, ECHO = 0x8, ECHONL = 0x40, IEXTEN = 0x8000;

        [DllImport(Lib, SetLastError = true)] public static extern int isatty(int fd);
        [DllImport(Lib, SetLastError = true)] public static extern int tcgetattr(int fd, byte[] termios);
        [DllImport(Lib, SetLastError = true)] public static extern int tcsetattr(int fd, int actions, byte[] termios);
        [DllImport(Lib, SetLastError = true)] public static extern int tcflush(int fd, int queue);
        [DllImport(Lib, SetLastError = true)] static extern unsafe IntPtr read(int fd, byte* buf, UIntPtr count);
        [DllImport(Lib, SetLastError = true)] static extern unsafe IntPtr write(int fd, byte* buf, UIntPtr count);
        [DllImport(Lib, SetLastError = true)] public static extern int open(string path, int flags, int mode);
        [DllImport(Lib, SetLastError = true)] public static extern int close(int fd);
        [DllImport(Lib, SetLastError = true)] public static extern int dup(int fd);
        [DllImport(Lib, SetLastError = true)] public static extern int dup2(int oldFd, int newFd);
        [DllImport(Lib, SetLastError = true)] static extern int ioctl(int fd, UIntPtr request, byte[] arg);
        [DllImport(Lib, SetLastError = true, EntryPoint = "ioctl")] static extern int ioctlValue(int fd, UIntPtr request, IntPtr arg);

        static bool setup;

        static LibC() { Setup(); }

        /// <summary>Finds libc under its real file name (glibc: libc.so.6, musl: libc.musl-*.so.1). Call before any P/Invoke.</summary>
        public static void Setup()
        {
            if (setup) return;
            setup = true;
            try { NativeLibrary.SetDllImportResolver(typeof(LibC).Assembly, Resolve); }
            catch (InvalidOperationException) { }   // already set
        }

        static IntPtr Resolve(string name, Assembly asm, DllImportSearchPath? path)
        {
            if (name != Lib) return IntPtr.Zero;   // everything else: default search
            foreach (var n in new[] { "libc.so.6", "libc.musl-x86_64.so.1", "libc.musl-aarch64.so.1", "libc.so" })
            {
                IntPtr h;
                if (NativeLibrary.TryLoad(n, out h)) return h;
            }
            return IntPtr.Zero;
        }

        public static int Errno { get { return Marshal.GetLastWin32Error(); } }

        /// <summary>read(2) into buf[offset..]: bytes read, 0 when nothing is waiting, -1 on error (see Errno).</summary>
        public static unsafe int Read(int fd, byte[] buf, int offset, int count)
        {
            fixed (byte* p = buf) return (int)(long)read(fd, p + offset, new UIntPtr((uint)count));
        }

        /// <summary>Writes everything, retrying partial writes and interrupted calls.</summary>
        public static unsafe bool WriteAll(int fd, byte[] buf, int count)
        {
            fixed (byte* p = buf)
            {
                int off = 0, waits = 0;
                while (off < count)
                {
                    long n = (long)write(fd, p + off, new UIntPtr((uint)(count - off)));
                    if (n > 0) { off += (int)n; continue; }
                    int e = Errno;
                    if (n < 0 && e == EINTR) continue;
                    if (n < 0 && e == EAGAIN && ++waits < 2000) { Thread.Sleep(1); continue; }
                    return false;
                }
            }
            return true;
        }

        /// <summary>Terminal size in cells and (when the terminal reports it) in pixels.</summary>
        public static bool GetWinSize(int fd, out int cols, out int rows, out int xpix, out int ypix)
        {
            var ws = new byte[8];   // struct winsize { ushort row, col, xpixel, ypixel }
            cols = rows = xpix = ypix = 0;
            if (ioctl(fd, new UIntPtr(TIOCGWINSZ), ws) != 0) return false;
            rows = BitConverter.ToUInt16(ws, 0);
            cols = BitConverter.ToUInt16(ws, 2);
            xpix = BitConverter.ToUInt16(ws, 4);
            ypix = BitConverter.ToUInt16(ws, 6);
            return true;
        }

        /// <summary>Exclusive access to an input device: its events stop reaching the desktop while grabbed.</summary>
        public static bool Grab(int fd, bool on)
        {
            return ioctlValue(fd, new UIntPtr(EVIOCGRAB), new IntPtr(on ? 1 : 0)) == 0;
        }

        /// <summary>A copy of the terminal settings switched to raw input: no echo, no line editing, no signals
        /// from Ctrl+C/Ctrl+Z, no flow control, and reads that return at once when nothing is waiting.</summary>
        public static byte[] MakeRaw(byte[] termios)
        {
            var t = (byte[])termios.Clone();
            uint iflag = BitConverter.ToUInt32(t, IFLAG) & ~(IGNBRK | BRKINT | PARMRK | ISTRIP | INLCR | IGNCR | ICRNL | IXON);
            uint lflag = BitConverter.ToUInt32(t, LFLAG) & ~(ECHO | ECHONL | ICANON | ISIG | IEXTEN);
            BitConverter.GetBytes(iflag).CopyTo(t, IFLAG);
            BitConverter.GetBytes(lflag).CopyTo(t, LFLAG);
            t[CC + VMIN] = 0;
            t[CC + VTIME] = 0;
            return t;
        }
    }
}
