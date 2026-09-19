// TERMINAL HELL - developer tools: PNG export of frames, texture sheets and terminal emulation.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TerminalHell
{
    static class DebugTools
    {
        public static void SavePng(int[] pix, int w, int h, string path, int scale)
        {
            using (var bmp = new Bitmap(w * scale, h * scale, PixelFormat.Format32bppArgb))
            {
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                var row = new int[bmp.Width];
                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int p = pix[(y / scale) * w + (x / scale)];
                        row[x] = unchecked((int)0xFF000000) | (p & 0xFFFFFF);
                    }
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, bmp.Width);
                }
                bmp.UnlockBits(data);
                bmp.Save(path, ImageFormat.Png);
            }
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

        /// <summary>Renders the composed screen the way a terminal would show it (8x16 cells, half blocks, text).</summary>
        public static void SaveTerminalShot(Screen s, string path)
        {
            const int cw = 8, ch = 16;
            using (var bmp = new Bitmap(s.Cols * cw, s.Rows * ch, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Consolas", 10.5f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                var fmt = StringFormat.GenericTypographic;
                for (int r = 0; r < s.Rows; r++)
                    for (int c = 0; c < s.Cols; c++)
                    {
                        int i = r * s.Cols + c;
                        int top = s.Pix[(r * 2) * s.W + c], bot = s.Pix[(r * 2 + 1) * s.W + c];
                        char t = s.TCh[i];
                        if (t == '\0')
                        {
                            using (var b1 = new SolidBrush(Color.FromArgb(255, Color.FromArgb(top & 0xFFFFFF))))
                                g.FillRectangle(b1, c * cw, r * ch, cw, ch / 2);
                            using (var b2 = new SolidBrush(Color.FromArgb(255, Color.FromArgb(bot & 0xFFFFFF))))
                                g.FillRectangle(b2, c * cw, r * ch + ch / 2, cw, ch / 2);
                        }
                        else
                        {
                            int bg = s.TBg[i];
                            if (bg < 0) bg = Screen.DimBehind(top, bot);
                            int fg = s.TFg[i];
                            using (var b1 = new SolidBrush(Color.FromArgb(255, Color.FromArgb(bg & 0xFFFFFF))))
                                g.FillRectangle(b1, c * cw, r * ch, cw, ch);
                            using (var b2 = new SolidBrush(Color.FromArgb(255, Color.FromArgb(fg & 0xFFFFFF))))
                            {
                                if (t == '▀') g.FillRectangle(b2, c * cw, r * ch, cw, ch / 2);
                                else if (t == '▄') g.FillRectangle(b2, c * cw, r * ch + ch / 2, cw, ch / 2);
                                else if (t == '█') g.FillRectangle(b2, c * cw, r * ch, cw, ch);
                                else if (t != ' ') g.DrawString(t.ToString(), font, b2, c * cw - 1, r * ch + 1, fmt);
                            }
                        }
                    }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
