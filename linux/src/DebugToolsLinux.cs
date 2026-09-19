// TERMINAL HELL - Linux developer tools and F12 screenshots: PNG export without System.Drawing (which is Windows only).
// Text in terminal screenshots is drawn with the game's own 5x7 pixel font.
// (the Windows version is src/DebugTools.cs)
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace TerminalHell
{
    static class DebugTools
    {
        public static void SavePng(int[] pix, int w, int h, string path, int scale)
        {
            int W = w * scale, H = h * scale;
            var rgb = new int[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    rgb[y * W + x] = pix[(y / scale) * w + (x / scale)] & 0xFFFFFF;
            Png.Write(path, rgb, W, H);
        }

        /// <summary>Lays out a list of images on a checkerboard sheet (transparent pixels show the checker).</summary>
        public static void SaveSheet(List<Image> imgs, string path, int scale, int perRow)
        {
            int cell = 0;
            foreach (var im in imgs) cell = Math.Max(cell, Math.Max(im.W, im.H));
            cell += 4;
            int rows = (imgs.Count + perRow - 1) / perRow;
            int w = cell * perRow, h = cell * rows;
            var pix = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pix[y * w + x] = ((x / 4 + y / 4) & 1) == 0 ? 0x303038 : 0x404048;
            for (int i = 0; i < imgs.Count; i++)
            {
                var im = imgs[i];
                int ox = (i % perRow) * cell + 2, oy = (i / perRow) * cell + 2;
                for (int y = 0; y < im.H; y++)
                    for (int x = 0; x < im.W; x++)
                    {
                        int p = im.Px[y * im.W + x];
                        if ((p & Col.OPAQUE) != 0) pix[(oy + y) * w + ox + x] = p & 0xFFFFFF;
                    }
            }
            SavePng(pix, w, h, path, scale);
        }

        const int CW = 12, CH = 24;   // one character cell in the screenshot

        /// <summary>Renders the composed screen the way a terminal shows it (half-block pixels, text, box lines).</summary>
        public static void SaveTerminalShot(Screen s, string path)
        {
            int W = s.Cols * CW, H = s.Rows * CH;
            var img = new int[W * H];
            for (int r = 0; r < s.Rows; r++)
                for (int c = 0; c < s.Cols; c++)
                {
                    int i = r * s.Cols + c;
                    int top = s.Pix[(r * 2) * s.W + c] & 0xFFFFFF, bot = s.Pix[(r * 2 + 1) * s.W + c] & 0xFFFFFF;
                    int x0 = c * CW, y0 = r * CH;
                    char t = s.TCh[i];
                    if (t == '\0')
                    {
                        Fill(img, W, x0, y0, CW, CH / 2, top);
                        Fill(img, W, x0, y0 + CH / 2, CW, CH / 2, bot);
                        continue;
                    }
                    int bg = s.TBg[i] >= 0 ? s.TBg[i] & 0xFFFFFF : Screen.DimBehind(top, bot);
                    int fg = s.TFg[i] & 0xFFFFFF;
                    Fill(img, W, x0, y0, CW, CH, bg);
                    DrawChar(img, W, x0, y0, t, fg, bg);
                }
            Png.Write(path, img, W, H);
        }

        static void Fill(int[] img, int W, int x0, int y0, int w, int h, int c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    img[y * W + x] = c;
        }

        static void DrawChar(int[] img, int W, int x0, int y0, char t, int fg, int bg)
        {
            switch (t)
            {
                case ' ': return;
                case '▀': Fill(img, W, x0, y0, CW, CH / 2, fg); return;
                case '▄': Fill(img, W, x0, y0 + CH / 2, CW, CH / 2, fg); return;
                case '█': Fill(img, W, x0, y0, CW, CH, fg); return;
                case '░': Shade(img, W, x0, y0, fg, bg, 0.25f); return;
                case '▒': Shade(img, W, x0, y0, fg, bg, 0.5f); return;
                case '▓': Shade(img, W, x0, y0, fg, bg, 0.75f); return;
                case '►':
                    for (int y = 0; y < 14; y++)
                        for (int x = 0; x < 1 + (y < 7 ? y : 13 - y); x++) img[(y0 + 5 + y) * W + x0 + 3 + x] = fg;
                    return;
            }
            int lines = BoxLines(t);
            if (lines != 0)
            {
                int mx = x0 + CW / 2 - 1, my = y0 + CH / 2 - 1;
                if ((lines & 1) != 0) Fill(img, W, x0, my, CW / 2 + 1, 2, fg);           // left
                if ((lines & 2) != 0) Fill(img, W, mx, my, CW - CW / 2 + 1, 2, fg);      // right
                if ((lines & 4) != 0) Fill(img, W, mx, y0, 2, CH / 2 + 1, fg);           // up
                if ((lines & 8) != 0) Fill(img, W, mx, my, 2, CH - CH / 2 + 1, fg);      // down
                return;
            }
            // the game's 5x7 font at double size, with a few extra symbols it doesn't have
            for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                    if (Glyph(t, gx, gy)) Fill(img, W, x0 + 1 + gx * 2, y0 + 5 + gy * 2, 2, 2, fg);
        }

        static void Shade(int[] img, int W, int x0, int y0, int fg, int bg, float a)
        {
            int c = Col.Lerp(bg, fg, a);
            Fill(img, W, x0, y0, CW, CH, c);
        }

        /// <summary>Box drawing characters as the directions they connect: 1 left, 2 right, 4 up, 8 down.</summary>
        static int BoxLines(char t)
        {
            switch (t)
            {
                case '─': case '═': return 3;
                case '│': case '║': return 12;
                case '┌': case '╔': return 10;
                case '┐': case '╗': return 9;
                case '└': case '╚': return 6;
                case '┘': case '╝': return 5;
                case '├': case '╠': return 14;
                case '┤': case '╣': return 13;
                case '┬': case '╦': return 11;
                case '┴': case '╩': return 7;
                case '┼': case '╬': return 15;
            }
            return 0;
        }

        static readonly Dictionary<char, byte[]> extra = new Dictionary<char, byte[]>
        {
            { '<', Rows("00010 00100 01000 10000 01000 00100 00010") },
            { '>', Rows("01000 00100 00010 00001 00010 00100 01000") },
            { '(', Rows("00010 00100 01000 01000 01000 00100 00010") },
            { ')', Rows("01000 00100 00010 00010 00010 00100 01000") },
            { '[', Rows("01110 01000 01000 01000 01000 01000 01110") },
            { ']', Rows("01110 00010 00010 00010 00010 00010 01110") },
            { '+', Rows("00000 00100 00100 11111 00100 00100 00000") },
            { '=', Rows("00000 00000 11111 00000 11111 00000 00000") },
            { '*', Rows("00000 10101 01110 11111 01110 10101 00000") },
            { '#', Rows("01010 01010 11111 01010 11111 01010 01010") },
            { '"', Rows("01010 01010 00000 00000 00000 00000 00000") },
            { ';', Rows("00000 00100 00000 00000 00100 00100 01000") },
            { '|', Rows("00100 00100 00100 00100 00100 00100 00100") },
            { '@', Rows("01110 10001 10111 10101 10111 10000 01110") },
            { '&', Rows("01100 10010 10100 01000 10101 10010 01101") },
        };

        static byte[] Rows(string bits)
        {
            var parts = bits.Split(' ');
            var r = new byte[7];
            for (int i = 0; i < 7; i++) r[i] = Convert.ToByte(parts[i], 2);
            return r;
        }

        static bool Glyph(char c, int x, int y)
        {
            byte[] g;
            if (extra.TryGetValue(c, out g)) return (g[y] & (16 >> x)) != 0;
            return PixFont.Pixel(c, x, y);
        }
    }

    /// <summary>Minimal PNG writer: 8-bit RGB, one zlib-compressed IDAT chunk.</summary>
    static class Png
    {
        static readonly uint[] crcTable = MakeCrcTable();

        public static void Write(string path, int[] rgb, int w, int h)
        {
            var raw = new byte[(w * 3 + 1) * h];
            int o = 0;
            for (int y = 0; y < h; y++)
            {
                raw[o++] = 0;   // filter: none
                for (int x = 0; x < w; x++)
                {
                    int c = rgb[y * w + x];
                    raw[o++] = (byte)(c >> 16);
                    raw[o++] = (byte)(c >> 8);
                    raw[o++] = (byte)c;
                }
            }
            byte[] z;
            using (var ms = new MemoryStream())
            {
                using (var zs = new ZLibStream(ms, CompressionLevel.Optimal, true)) zs.Write(raw, 0, raw.Length);
                z = ms.ToArray();
            }
            var ihdr = new byte[13];
            Be(ihdr, 0, (uint)w);
            Be(ihdr, 4, (uint)h);
            ihdr[8] = 8;   // bits per channel
            ihdr[9] = 2;   // RGB
            using (var fs = File.Create(path))
            {
                fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                Chunk(fs, "IHDR", ihdr);
                Chunk(fs, "IDAT", z);
                Chunk(fs, "IEND", new byte[0]);
            }
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var head = new byte[8];
            Be(head, 0, (uint)data.Length);
            for (int i = 0; i < 4; i++) head[4 + i] = (byte)type[i];
            s.Write(head, 0, 8);
            s.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFF;
            for (int i = 4; i < 8; i++) crc = crcTable[(crc ^ head[i]) & 0xFF] ^ (crc >> 8);
            foreach (byte b in data) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            var tail = new byte[4];
            Be(tail, 0, crc ^ 0xFFFFFFFF);
            s.Write(tail, 0, 4);
        }

        static void Be(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
        }

        static uint[] MakeCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }
    }
}
