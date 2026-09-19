// TERMINAL HELL - Windows only: 16-color WriteConsoleOutput with ordered dithering (fast on old consoles).
using System;

namespace TerminalHell
{
    sealed class LegacyPresenter
    {
        CHAR_INFO[] cells = new CHAR_INFO[0];
        byte[] lut;      // 32x32x32 rgb -> palette index
        int[] palette = new int[16];
        static readonly int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public LegacyPresenter()
        {
            // default Windows 10 "Campbell" palette, overridden with the real console palette when available
            int[] def = { 0x0C0C0C, 0x0037DA, 0x13A10E, 0x3A96DD, 0xC50F1F, 0x881798, 0xC19C00, 0xCCCCCC,
                          0x767676, 0x3B78FF, 0x16C60C, 0x61D6D6, 0xE74856, 0xB4009E, 0xF9F1A5, 0xF2F2F2 };
            Array.Copy(def, palette, 16);
            try
            {
                var info = new CONSOLE_SCREEN_BUFFER_INFO_EX();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CONSOLE_SCREEN_BUFFER_INFO_EX));
                if (Native.GetConsoleScreenBufferInfoEx(Term.Out, ref info) && info.ColorTable != null)
                {
                    // COLORREF is 0x00BBGGRR
                    for (int i = 0; i < 16; i++)
                    {
                        uint c = info.ColorTable[i];
                        palette[i] = (int)(((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF));
                    }
                }
            }
            catch { }
            lut = new byte[32 * 32 * 32];
            for (int r = 0; r < 32; r++)
                for (int g = 0; g < 32; g++)
                    for (int b = 0; b < 32; b++)
                    {
                        int rr = r * 8 + 4, gg = g * 8 + 4, bb = b * 8 + 4;
                        int best = 0, bestD = int.MaxValue;
                        for (int i = 0; i < 16; i++)
                        {
                            int dr = rr - Col.R(palette[i]), dg = gg - Col.G(palette[i]), db = bb - Col.B(palette[i]);
                            int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                            if (d < bestD) { bestD = d; best = i; }
                        }
                        lut[(r << 10) | (g << 5) | b] = (byte)best;
                    }
        }

        int Nearest(int c, int x, int y)
        {
            int t = bayer[(y & 3) * 4 + (x & 3)] * 3 - 22;
            int r = ((c >> 16) & 255) + t, g = ((c >> 8) & 255) + t, b = (c & 255) + t;
            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (b < 0) b = 0; else if (b > 255) b = 255;
            return lut[((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3)];
        }

        public void Present(Screen s)
        {
            int count = s.Cols * s.Rows;
            if (cells.Length != count) cells = new CHAR_INFO[count];
            for (int y = 0; y < s.Rows; y++)
                for (int x = 0; x < s.Cols; x++)
                {
                    int i = y * s.Cols + x;
                    int top = s.Pix[(y * 2) * s.W + x], bot = s.Pix[(y * 2 + 1) * s.W + x];
                    char ch = s.TCh[i];
                    if (ch == '\0')
                    {
                        int f = Nearest(top, x, y * 2), b = Nearest(bot, x, y * 2 + 1);
                        cells[i].Char = '▀';
                        cells[i].Attributes = (short)(f | (b << 4));
                    }
                    else
                    {
                        int bgc = s.TBg[i] >= 0 ? s.TBg[i] : Screen.DimBehind(top, bot);
                        int f = Nearest(s.TFg[i], 1, 1), b = Nearest(bgc, 0, 0);
                        if (f == b && ch != ' ') f = b == 0 ? 7 : 0;
                        cells[i].Char = ch;
                        cells[i].Attributes = (short)(f | (b << 4));
                    }
                }
            var region = new SMALL_RECT(0, 0, s.Cols - 1, s.Rows - 1);
            Native.WriteConsoleOutputW(Term.Out, cells, new COORD(s.Cols, s.Rows), new COORD(0, 0), ref region);
        }
    }
}
