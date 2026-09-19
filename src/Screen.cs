// TERMINAL HELL - the composed frame: a pixel buffer (2 pixels per character cell) plus a text layer.
using System;

namespace TerminalHell
{
    sealed class Screen
    {
        public int Cols, Rows;     // character cells
        public int W, H;           // pixels (W = Cols, H = Rows * 2)
        public int[] Pix = new int[0];
        public char[] TCh = new char[0];    // '\0' = show pixels
        public int[] TFg = new int[0];
        public int[] TBg = new int[0];      // -1 = darkened pixels behind the text

        public const int Transparent = -1;

        public void Resize(int cols, int rows)
        {
            if (cols < 20) cols = 20;
            if (rows < 10) rows = 10;
            if (cols == Cols && rows == Rows) return;
            Cols = cols; Rows = rows;
            W = cols; H = rows * 2;
            Pix = new int[W * H];
            TCh = new char[cols * rows];
            TFg = new int[cols * rows];
            TBg = new int[cols * rows];
        }

        public void Clear(int color)
        {
            for (int i = 0; i < Pix.Length; i++) Pix[i] = color;
            Array.Clear(TCh, 0, TCh.Length);
        }

        public void ClearText() { Array.Clear(TCh, 0, TCh.Length); }

        public void Put(int x, int y, char c, int fg, int bg)
        {
            if (x < 0 || y < 0 || x >= Cols || y >= Rows) return;
            int i = y * Cols + x;
            TCh[i] = c == '\0' ? ' ' : c;
            TFg[i] = fg;
            TBg[i] = bg;
        }

        public void Print(int x, int y, string s, int fg, int bg)
        {
            for (int i = 0; i < s.Length; i++) Put(x + i, y, s[i], fg, bg);
        }

        public void PrintCenter(int y, string s, int fg, int bg)
        {
            Print((Cols - s.Length) / 2, y, s, fg, bg);
        }

        /// <summary>Fills a rectangle of cells with a solid text background.</summary>
        public void FillCells(int x, int y, int w, int h, int bg)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Put(xx, yy, ' ', bg, bg);
        }

        public void SetPix(int x, int y, int c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Pix[y * W + x] = c;
        }

        public void BlendPix(int x, int y, int c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            Pix[i] = Col.Lerp(Pix[i], c, a);
        }

        public void FillPix(int x, int y, int w, int h, int c)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++)
                    Pix[yy * W + xx] = c;
        }

        public void DarkenPix(int x, int y, int w, int h, float f)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++)
                    Pix[yy * W + xx] = Col.Scale(Pix[yy * W + xx], f);
        }

        /// <summary>Background used behind transparent text: the cell's pixels, darkened for contrast.</summary>
        public static int DimBehind(int top, int bot)
        {
            // about 28% of the average brightness keeps overlay text readable on any background
            int r = ((((top >> 16) & 255) + ((bot >> 16) & 255)) * 36) >> 8;
            int g = ((((top >> 8) & 255) + ((bot >> 8) & 255)) * 36) >> 8;
            int b = (((top & 255) + (bot & 255)) * 36) >> 8;
            return (r << 16) | (g << 8) | b;
        }
    }
}
