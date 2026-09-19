// TERMINAL HELL - console host management: private screen buffer, modes, font/size, window handle.
using System;
using System.Text;

namespace TerminalHell
{
    static class Term
    {
        public static IntPtr In, OrigOut, Out;
        public static IntPtr ConsoleHwnd, GameHwnd;
        public static bool IsClassicConsole;   // conhost window we can resize and re-font
        public static bool VtOk;
        public static float CellAspect = 1f;   // pixel width / pixel height for half-block pixels

        static uint origInMode, origOutMode;
        static CONSOLE_FONT_INFOEX origFont;
        static bool haveOrigFont, fontChanged, active;
        static string origTitle = "";
        static ConsoleCtrlDelegate ctrlHandler;

        public static void Init(Settings s)
        {
            // real pixel coordinates for window sizing and the mouse lock (no DPI virtualisation)
            try { Native.SetProcessDPIAware(); } catch { }
            In = Native.GetStdHandle(Native.STD_INPUT_HANDLE);
            OrigOut = Native.GetStdHandle(Native.STD_OUTPUT_HANDLE);
            Native.GetConsoleMode(In, out origInMode);
            Native.GetConsoleMode(OrigOut, out origOutMode);

            var sb = new StringBuilder(512);
            if (Native.GetConsoleTitleW(sb, sb.Capacity) > 0) origTitle = sb.ToString();
            Native.SetConsoleTitleW("TERMINAL HELL");

            uint inMode = Native.ENABLE_EXTENDED_FLAGS | Native.ENABLE_WINDOW_INPUT | Native.ENABLE_MOUSE_INPUT;
            Native.SetConsoleMode(In, inMode);
            Native.FlushConsoleInputBuffer(In);

            ConsoleHwnd = Native.GetConsoleWindow();
            IsClassicConsole = ConsoleHwnd != IntPtr.Zero && Native.IsWindowVisible(ConsoleHwnd);
            if (IsClassicConsole) GameHwnd = ConsoleHwnd;
            else
            {
                IntPtr owner = ConsoleHwnd != IntPtr.Zero ? Native.GetAncestor(ConsoleHwnd, Native.GA_ROOTOWNER) : IntPtr.Zero;
                if (owner != IntPtr.Zero && owner != ConsoleHwnd && Native.IsWindowVisible(owner)) GameHwnd = owner;
                else GameHwnd = Native.GetForegroundWindow();
            }

            origFont = new CONSOLE_FONT_INFOEX();
            origFont.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CONSOLE_FONT_INFOEX));
            haveOrigFont = Native.GetCurrentConsoleFontEx(OrigOut, false, ref origFont);

            Out = Native.CreateConsoleScreenBuffer(Native.GENERIC_READ | Native.GENERIC_WRITE, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero, Native.CONSOLE_TEXTMODE_BUFFER, IntPtr.Zero);
            if (Out == IntPtr.Zero || Out == new IntPtr(-1)) Out = OrigOut;
            else Native.SetConsoleActiveScreenBuffer(Out);
            active = true;

            VtOk = Native.SetConsoleMode(Out, Native.ENABLE_PROCESSED_OUTPUT | Native.ENABLE_VIRTUAL_TERMINAL_PROCESSING | Native.DISABLE_NEWLINE_AUTO_RETURN);
            if (!VtOk) Native.SetConsoleMode(Out, Native.ENABLE_PROCESSED_OUTPUT);

            HideCursor();

            ctrlHandler = OnCtrl;
            Native.SetConsoleCtrlHandler(ctrlHandler, true);

            if (IsClassicConsole)
            {
                Native.GetWindowRect(ConsoleHwnd, out origRect);
                wasZoomed = Native.IsZoomed(ConsoleHwnd);
                ApplyVideo(s);
            }
            else
            {
                FitBufferToWindow();
            }
        }

        public static void HideCursor()
        {
            var ci = new CONSOLE_CURSOR_INFO { dwSize = 1, bVisible = 0 };
            Native.SetConsoleCursorInfo(Out, ref ci);
            if (VtOk) Write("\x1b[?25l\x1b[?7l");
        }

        static bool OnCtrl(int type)
        {
            // Close / logoff / shutdown: put the console back the way we found it.
            try { Restore(); } catch { }
            return type == 0 || type == 1;   // swallow Ctrl+C / Ctrl+Break
        }

        /// <summary>Target number of character cells for the windowed presets (LOW, MEDIUM, HIGH).</summary>
        static readonly int[] targetCells = { 4000, 6500, 9500 };
        public static string FontName = "";
        static RECT origRect;
        static bool wasZoomed;

        static bool SetFont(string face, int w, int h, int family)
        {
            var f = new CONSOLE_FONT_INFOEX();
            f.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CONSOLE_FONT_INFOEX));
            f.FaceName = face;
            f.dwFontSize = new COORD(w, h);
            f.FontFamily = family;
            f.FontWeight = 400;
            if (!Native.SetCurrentConsoleFontEx(Out, false, ref f)) return false;
            fontChanged = true;
            int fw, fh;
            string got = CurrentFont(out fw, out fh);
            return got.Equals(face, StringComparison.OrdinalIgnoreCase);
        }

        static string CurrentFont(out int fw, out int fh)
        {
            var g = new CONSOLE_FONT_INFOEX();
            g.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CONSOLE_FONT_INFOEX));
            fw = 8; fh = 16;
            if (!Native.GetCurrentConsoleFontEx(Out, false, ref g)) return "";
            if (g.dwFontSize.X > 0 && g.dwFontSize.Y > 0) { fw = g.dwFontSize.X; fh = g.dwFontSize.Y; }
            return g.FaceName ?? "";
        }

        /// <summary>
        /// Classic console only: picks the font and window size.
        ///   Crisp  : the console's bitmap "Terminal" font (10x18) - block characters are pixel exact.
        ///   Smooth : Consolas, scaled freely (TrueType glyphs can show thin seams between cells).
        ///   LOW / MEDIUM / HIGH set the window size in cells, FULL SCREEN maximizes the window.
        /// </summary>
        public static void ApplyVideo(Settings s)
        {
            if (!IsClassicConsole) { FitBufferToWindow(); return; }
            bool full = s.Resolution >= 3;
            bool crisp = s.CrispFont && VtOk && s.Display != DisplayMode.Legacy && SetFont("Terminal", 10, 18, 0x30);

            if (full)
            {
                if (!Native.IsZoomed(ConsoleHwnd))
                {
                    GrowBufferToLargest();
                    Native.ShowWindow(ConsoleHwnd, Native.SW_MAXIMIZE);
                }
                if (!crisp)
                {
                    // choose a Consolas size that fills the maximized window with ~11000 cells
                    RECT rc;
                    Native.GetClientRect(ConsoleHwnd, out rc);
                    double cellW = Math.Sqrt((double)Math.Max(200, rc.Right) * Math.Max(150, rc.Bottom) / (2.0 * 11000));
                    SetFont("Consolas", 0, Math.Max(8, Math.Min(40, (int)Math.Round(cellW * 2))), 54);
                }
                FitToClient();
            }
            else
            {
                if (Native.IsZoomed(ConsoleHwnd)) Native.ShowWindow(ConsoleHwnd, Native.SW_RESTORE);
                if (!crisp) SetFont("Consolas", 0, 16, 54);
                int fw, fh;
                FontName = CurrentFont(out fw, out fh);
                int target = targetCells[Math.Max(0, Math.Min(2, s.Resolution))];
                // aim for a 16:10 window
                int cols = (int)Math.Sqrt(target * 1.6 * fh / fw);
                int rows = target / Math.Max(1, cols);
                int largest = Native.GetLargestConsoleWindowSize(Out);
                int lx = largest & 0xFFFF, ly = (largest >> 16) & 0xFFFF;
                if (lx > 0) cols = Math.Min(cols, lx);
                if (ly > 0) rows = Math.Min(rows, ly);
                ResizeBuffer(Math.Max(40, cols), Math.Max(15, rows));
                CenterWindow();
            }
            int w2, h2;
            FontName = CurrentFont(out w2, out h2);
            CellAspect = w2 / (h2 * 0.5f);
            if (CellAspect < 0.6f || CellAspect > 1.8f) CellAspect = 1;
        }

        static void FitToClient()
        {
            RECT rc;
            if (!Native.GetClientRect(ConsoleHwnd, out rc)) return;
            int fw, fh;
            CurrentFont(out fw, out fh);
            // a couple of pixels of slack: a buffer that fills the client exactly makes the console add scrollbars
            int cols = (rc.Right - rc.Left - 2) / fw, rows = (rc.Bottom - rc.Top - 2) / fh;
            int largest = Native.GetLargestConsoleWindowSize(Out);
            int lx = largest & 0xFFFF, ly = (largest >> 16) & 0xFFFF;
            if (lx > 0) cols = Math.Min(cols, lx);
            if (ly > 0) rows = Math.Min(rows, ly);
            ResizeBuffer(Math.Max(40, cols), Math.Max(15, rows));
        }

        static void CenterWindow()
        {
            RECT wr;
            if (!Native.GetWindowRect(ConsoleHwnd, out wr)) return;
            IntPtr mon = Native.MonitorFromWindow(ConsoleHwnd, 2);
            var mi = new MONITORINFO();
            mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
            if (!Native.GetMonitorInfo(mon, ref mi)) return;
            int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;
            int x = mi.rcWork.Left + Math.Max(0, (mi.rcWork.Right - mi.rcWork.Left - w) / 2);
            int y = mi.rcWork.Top + Math.Max(0, (mi.rcWork.Bottom - mi.rcWork.Top - h) / 2);
            Native.SetWindowPos(ConsoleHwnd, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);   // NOSIZE | NOZORDER | NOACTIVATE
        }

        public static string Diag()
        {
            var g = new CONSOLE_FONT_INFOEX();
            g.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CONSOLE_FONT_INFOEX));
            Native.GetCurrentConsoleFontEx(Out, false, ref g);
            RECT rc, wr;
            Native.GetClientRect(ConsoleHwnd, out rc);
            Native.GetWindowRect(ConsoleHwnd, out wr);
            CONSOLE_SCREEN_BUFFER_INFO info;
            Native.GetConsoleScreenBufferInfo(Out, out info);
            return "font=" + g.FaceName + " " + g.dwFontSize.X + "x" + g.dwFontSize.Y + " family=" + g.FontFamily +
                " client=" + (rc.Right - rc.Left) + "x" + (rc.Bottom - rc.Top) + " window=" + wr.Left + "," + wr.Top + "," + wr.Right + "," + wr.Bottom +
                " buffer=" + info.dwSize.X + "x" + info.dwSize.Y + " view=" + info.srWindow.Left + "," + info.srWindow.Top + "," + info.srWindow.Right + "," + info.srWindow.Bottom +
                " zoomed=" + Native.IsZoomed(ConsoleHwnd);
        }

        static void GrowBufferToLargest()
        {
            int largest = Native.GetLargestConsoleWindowSize(Out);
            int lx = largest & 0xFFFF, ly = (largest >> 16) & 0xFFFF;
            if (lx <= 0 || ly <= 0) return;
            CONSOLE_SCREEN_BUFFER_INFO info;
            if (!Native.GetConsoleScreenBufferInfo(Out, out info)) return;
            int bx = Math.Max(lx, info.dwSize.X), by = Math.Max(ly, info.dwSize.Y);
            Native.SetConsoleScreenBufferSize(Out, new COORD(bx, by));
        }

        public static void ResizeBuffer(int cols, int rows)
        {
            CONSOLE_SCREEN_BUFFER_INFO info;
            if (!Native.GetConsoleScreenBufferInfo(Out, out info)) return;
            int ww = info.srWindow.Right - info.srWindow.Left + 1, wh = info.srWindow.Bottom - info.srWindow.Top + 1;
            var r = new SMALL_RECT(0, 0, Math.Min(ww, cols) - 1, Math.Min(wh, rows) - 1);
            Native.SetConsoleWindowInfo(Out, true, ref r);
            Native.SetConsoleScreenBufferSize(Out, new COORD(cols, rows));
            r = new SMALL_RECT(0, 0, cols - 1, rows - 1);
            Native.SetConsoleWindowInfo(Out, true, ref r);
        }

        /// <summary>Makes the buffer exactly the visible window size (no scrollbars, no scrollback).</summary>
        public static void FitBufferToWindow()
        {
            CONSOLE_SCREEN_BUFFER_INFO info;
            if (!Native.GetConsoleScreenBufferInfo(Out, out info)) return;
            int ww = info.srWindow.Right - info.srWindow.Left + 1, wh = info.srWindow.Bottom - info.srWindow.Top + 1;
            if (info.dwSize.X != ww || info.dwSize.Y != wh || info.srWindow.Left != 0 || info.srWindow.Top != 0)
            {
                var r = new SMALL_RECT(0, 0, ww - 1, wh - 1);
                Native.SetConsoleWindowInfo(Out, true, ref r);
                Native.SetConsoleScreenBufferSize(Out, new COORD(ww, wh));
            }
        }

        /// <summary>Current visible size in cells. Also keeps the buffer in sync with the window.</summary>
        public static void GetSize(out int cols, out int rows)
        {
            CONSOLE_SCREEN_BUFFER_INFO info;
            if (!Native.GetConsoleScreenBufferInfo(Out, out info)) { cols = 80; rows = 25; return; }
            cols = info.srWindow.Right - info.srWindow.Left + 1;
            rows = info.srWindow.Bottom - info.srWindow.Top + 1;
            if (info.dwSize.X != cols || info.dwSize.Y != rows || info.srWindow.Top != 0 || info.srWindow.Left != 0)
            {
                FitBufferToWindow();
                HideCursor();
            }
            if (IsClassicConsole)
            {
                // the window may have been resized larger than our buffer allows: grow into the client area
                RECT rc;
                var g = new CONSOLE_FONT_INFOEX();
                g.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CONSOLE_FONT_INFOEX));
                if (Native.GetClientRect(ConsoleHwnd, out rc) && Native.GetCurrentConsoleFontEx(Out, false, ref g) && g.dwFontSize.X > 0)
                {
                    int cw = rc.Right - rc.Left, ch = rc.Bottom - rc.Top;
                    int fitC = cw / g.dwFontSize.X, fitR = ch / g.dwFontSize.Y;                 // what is visible
                    int growC = (cw - 2) / g.dwFontSize.X, growR = (ch - 2) / g.dwFontSize.Y;   // what fits with slack
                    int largest = Native.GetLargestConsoleWindowSize(Out);
                    int lx = largest & 0xFFFF, ly = (largest >> 16) & 0xFFFF;
                    if (lx > 0) { fitC = Math.Min(fitC, lx); growC = Math.Min(growC, lx); }
                    if (ly > 0) { fitR = Math.Min(fitR, ly); growR = Math.Min(growR, ly); }
                    if ((fitC < cols || fitR < rows) && fitC >= 40 && fitR >= 15)
                    {
                        // scrollbars appeared (part of the buffer is hidden): shrink to what is visible
                        cols = Math.Min(cols, fitC); rows = Math.Min(rows, fitR);
                        ResizeBuffer(cols, rows);
                    }
                    else if ((growC > cols || growR > rows) && growC > 0 && growR > 0)
                    {
                        // the window was made larger: use the extra space
                        cols = Math.Max(growC, cols); rows = Math.Max(growR, rows);
                        ResizeBuffer(cols, rows);
                    }
                }
            }
        }

        public static void Write(string s)
        {
            int w;
            Native.WriteConsoleW(Out, s.ToCharArray(), s.Length, out w, IntPtr.Zero);
        }

        public static unsafe void Write(char[] buf, int count)
        {
            fixed (char* p = buf)
            {
                int off = 0;
                while (off < count)
                {
                    int n = Math.Min(32768, count - off);
                    int w;
                    Native.WriteConsolePtr(Out, p + off, n, out w, IntPtr.Zero);
                    off += n;
                }
            }
        }

        public static bool IsForeground()
        {
            return Native.GetForegroundWindow() == GameHwnd;
        }

        public static void Restore()
        {
            if (!active) return;
            active = false;
            Native.ClipCursorNull(IntPtr.Zero);
            if (VtOk) Write("\x1b[0m\x1b[?25h\x1b[?7h");
            if (Out != OrigOut)
            {
                Native.SetConsoleActiveScreenBuffer(OrigOut);
            }
            if (fontChanged && haveOrigFont)
            {
                var f = origFont;
                Native.SetCurrentConsoleFontEx(OrigOut, false, ref f);
            }
            if (IsClassicConsole)
            {
                if (Native.IsZoomed(ConsoleHwnd) && !wasZoomed) Native.ShowWindow(ConsoleHwnd, Native.SW_RESTORE);
                if (wasZoomed && !Native.IsZoomed(ConsoleHwnd)) Native.ShowWindow(ConsoleHwnd, Native.SW_MAXIMIZE);
                if (!wasZoomed) Native.SetWindowPos(ConsoleHwnd, IntPtr.Zero, origRect.Left, origRect.Top, 0, 0, 0x0001 | 0x0004 | 0x0010);
            }
            if (Out != OrigOut) Native.CloseHandle(Out);
            Out = OrigOut;
            Native.SetConsoleMode(In, origInMode);
            Native.FlushConsoleInputBuffer(In);
            Native.SetConsoleTitleW(origTitle);
        }
    }
}
