// TERMINAL HELL - Win32 interop declarations.
// NOTE: all source files must stay C# 5 compatible (built with the csc.exe that ships with Windows).
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TerminalHell
{
    [StructLayout(LayoutKind.Sequential)]
    struct COORD
    {
        public short X, Y;
        public COORD(int x, int y) { X = (short)x; Y = (short)y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SMALL_RECT
    {
        public short Left, Top, Right, Bottom;
        public SMALL_RECT(int l, int t, int r, int b) { Left = (short)l; Top = (short)t; Right = (short)r; Bottom = (short)b; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public short wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CONSOLE_SCREEN_BUFFER_INFO_EX
    {
        public int cbSize;
        public COORD dwSize;
        public COORD dwCursorPosition;
        public short wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
        public short wPopupAttributes;
        public int bFullscreenSupported;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] ColorTable;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CONSOLE_FONT_INFOEX
    {
        public int cbSize;
        public int nFont;
        public COORD dwFontSize;
        public int FontFamily;
        public int FontWeight;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CONSOLE_CURSOR_INFO
    {
        public int dwSize;
        public int bVisible;
    }

    // INPUT_RECORD is a tagged union: 2 byte type, 2 byte padding, 16 byte event payload.
    [StructLayout(LayoutKind.Explicit)]
    struct INPUT_RECORD
    {
        [FieldOffset(0)] public ushort EventType;
        // KEY_EVENT_RECORD
        [FieldOffset(4)] public int KeyDown;
        [FieldOffset(8)] public ushort RepeatCount;
        [FieldOffset(10)] public ushort VirtualKeyCode;
        [FieldOffset(12)] public ushort VirtualScanCode;
        [FieldOffset(14)] public ushort UnicodeChar;
        [FieldOffset(16)] public uint ControlKeyState;
        // MOUSE_EVENT_RECORD
        [FieldOffset(4)] public short MouseX;
        [FieldOffset(6)] public short MouseY;
        [FieldOffset(8)] public uint ButtonState;
        [FieldOffset(12)] public uint MouseControlKeyState;
        [FieldOffset(16)] public uint MouseEventFlags;
        // FOCUS_EVENT_RECORD
        [FieldOffset(4)] public int SetFocus;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct CHAR_INFO
    {
        [FieldOffset(0)] public char Char;
        [FieldOffset(2)] public short Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    delegate bool ConsoleCtrlDelegate(int ctrlType);

    static class Native
    {
        public const int STD_INPUT_HANDLE = -10;
        public const int STD_OUTPUT_HANDLE = -11;

        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
        public const uint CONSOLE_TEXTMODE_BUFFER = 1;

        // input modes
        public const uint ENABLE_PROCESSED_INPUT = 0x0001;
        public const uint ENABLE_LINE_INPUT = 0x0002;
        public const uint ENABLE_ECHO_INPUT = 0x0004;
        public const uint ENABLE_WINDOW_INPUT = 0x0008;
        public const uint ENABLE_MOUSE_INPUT = 0x0010;
        public const uint ENABLE_INSERT_MODE = 0x0020;
        public const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        public const uint ENABLE_EXTENDED_FLAGS = 0x0080;
        // output modes
        public const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
        public const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;
        public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        public const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        public const ushort KEY_EVENT = 1, MOUSE_EVENT = 2, WINDOW_BUFFER_SIZE_EVENT = 4, MENU_EVENT = 8, FOCUS_EVENT = 16;
        public const uint MOUSE_MOVED = 1, DOUBLE_CLICK = 2, MOUSE_WHEELED = 4, MOUSE_HWHEELED = 8;

        public const uint WM_INPUT = 0x00FF;
        public const uint PM_REMOVE = 1;
        public const uint RIDEV_INPUTSINK = 0x00000100;
        public const uint RID_INPUT = 0x10000003;
        public const uint RIM_TYPEMOUSE = 0;

        public const int SW_MAXIMIZE = 3, SW_RESTORE = 9;
        public const uint GA_ROOTOWNER = 3;
        public const uint GW_OWNER = 4;

        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetConsoleMode(IntPtr h, out uint mode);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleMode(IntPtr h, uint mode);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateConsoleScreenBuffer(uint access, uint share, IntPtr security, uint flags, IntPtr reserved);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleActiveScreenBuffer(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetConsoleScreenBufferInfo(IntPtr h, out CONSOLE_SCREEN_BUFFER_INFO info);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetConsoleScreenBufferInfoEx(IntPtr h, ref CONSOLE_SCREEN_BUFFER_INFO_EX info);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleScreenBufferSize(IntPtr h, COORD size);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleWindowInfo(IntPtr h, bool absolute, ref SMALL_RECT rect);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern int GetLargestConsoleWindowSize(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetCurrentConsoleFontEx(IntPtr h, bool max, ref CONSOLE_FONT_INFOEX info);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetCurrentConsoleFontEx(IntPtr h, bool max, ref CONSOLE_FONT_INFOEX info);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetConsoleCursorInfo(IntPtr h, out CONSOLE_CURSOR_INFO info);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleCursorInfo(IntPtr h, ref CONSOLE_CURSOR_INFO info);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool SetConsoleTitleW(string title);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern int GetConsoleTitleW(StringBuilder sb, int size);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool WriteConsoleW(IntPtr h, char[] buf, int count, out int written, IntPtr reserved);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WriteConsoleW")] public static extern unsafe bool WriteConsolePtr(IntPtr h, char* buf, int count, out int written, IntPtr reserved);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool WriteConsoleOutputW(IntPtr h, CHAR_INFO[] buf, COORD size, COORD pos, ref SMALL_RECT region);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool ReadConsoleInputW(IntPtr h, [Out] INPUT_RECORD[] buf, int len, out int read);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetNumberOfConsoleInputEvents(IntPtr h, out int n);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool FlushConsoleInputBuffer(IntPtr h);
        [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT pt);
        [DllImport("user32.dll")] public static extern bool ClipCursor(ref RECT rect);
        [DllImport("user32.dll", EntryPoint = "ClipCursor")] public static extern bool ClipCursorNull(IntPtr rect);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
        [DllImport("user32.dll")] public static extern uint GetRawInputData(IntPtr hRawInput, uint command, byte[] data, ref uint size, uint headerSize);
        [DllImport("user32.dll")] public static extern bool PeekMessageW(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax, uint remove);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessageW(ref MSG msg);

        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern int waveOutOpen(out IntPtr hwo, int deviceId, ref WAVEFORMATEX fmt, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] public static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveOutWrite(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] public static extern int waveOutClose(IntPtr hwo);
    }
}
