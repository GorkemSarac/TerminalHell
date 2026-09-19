// TERMINAL HELL - turns a composed Screen into terminal output (escape sequences, used on every platform).
//   HD     : half-block characters with 24-bit color escape sequences (2 pixels per cell)
//   ASCII  : one colored ASCII character per cell, brightness mapped to glyph density
//   (the Windows-only 16-color mode is in LegacyPresenter.cs)
using System;

namespace TerminalHell
{
    sealed class VtPresenter
    {
        char[] buf = new char[1 << 16];
        int n;
        int[] prevA = new int[0], prevB = new int[0];
        char[] prevC = new char[0];
        int cols, rows;
        bool invalid = true;
        int curFg, curBg, cx, cy;
        static readonly char[][] num = new char[256][];
        public int LastChars;

        const string Ramp = " .,:;-=+*oa#%&@";

        static VtPresenter()
        {
            for (int i = 0; i < 256; i++) num[i] = i.ToString().ToCharArray();
        }

        public void Invalidate() { invalid = true; }

        readonly int[] hist = new int[256];
        readonly int[] eqMap = new int[256];

        /// <summary>Histogram equalization of cell brightness so dark scenes still use the whole glyph ramp.</summary>
        void BuildEqualizer(Screen s)
        {
            Array.Clear(hist, 0, 256);
            int total = 0;
            for (int y = 0; y < s.Rows; y++)
                for (int x = 0; x < s.Cols; x++)
                {
                    if (s.TCh[y * s.Cols + x] != '\0') continue;
                    int top = s.Pix[(y * 2) * s.W + x], bot = s.Pix[(y * 2 + 1) * s.W + x];
                    int r = (((top >> 16) & 255) + ((bot >> 16) & 255)) >> 1;
                    int g = (((top >> 8) & 255) + ((bot >> 8) & 255)) >> 1;
                    int b = ((top & 255) + (bot & 255)) >> 1;
                    hist[(r * 3 + g * 6 + b) / 10]++;
                    total++;
                }
            int acc = 0;
            for (int l = 0; l < 256; l++)
            {
                acc += hist[l];
                eqMap[l] = total > 0 ? acc * 255 / total : l;
            }
        }

        /// <summary>Per-channel colour tolerance (0 = exact). Higher = less output, slightly coarser colour.</summary>
        public static int Tolerance = 7;
        int tol;

        static bool Near(int a, int b, int t)
        {
            if (a < 0 || b < 0) return false;
            int d = ((a >> 16) & 255) - ((b >> 16) & 255);
            if (d > t || d < -t) return false;
            d = ((a >> 8) & 255) - ((b >> 8) & 255);
            if (d > t || d < -t) return false;
            d = (a & 255) - (b & 255);
            return d <= t && d >= -t;
        }

        void Ensure(int more)
        {
            if (n + more < buf.Length) return;
            var nb = new char[Math.Max(buf.Length * 2, n + more + 1024)];
            Array.Copy(buf, nb, n);
            buf = nb;
        }

        void Put(string s) { for (int i = 0; i < s.Length; i++) buf[n++] = s[i]; }

        void PutNum(int v)
        {
            if (v < 256) { var d = num[v]; for (int i = 0; i < d.Length; i++) buf[n++] = d[i]; return; }
            Put(v.ToString());
        }

        void PutRgb(int c)
        {
            PutNum((c >> 16) & 255); buf[n++] = ';';
            PutNum((c >> 8) & 255); buf[n++] = ';';
            PutNum(c & 255);
        }

        void Sgr(int fg, int bg)
        {
            bool f = fg != curFg, b = bg != curBg;
            if (!f && !b) return;
            buf[n++] = '\x1b'; buf[n++] = '[';
            if (f) { Put("38;2;"); PutRgb(fg); curFg = fg; }
            if (f && b) buf[n++] = ';';
            if (b) { Put("48;2;"); PutRgb(bg); curBg = bg; }
            buf[n++] = 'm';
        }

        void MoveTo(int x, int y)
        {
            if (y == cy && x == cx) return;
            if (y == cy && x > cx && cx >= 0)
            {
                buf[n++] = '\x1b'; buf[n++] = '['; PutNum(x - cx); buf[n++] = 'C';
            }
            else
            {
                buf[n++] = '\x1b'; buf[n++] = '['; PutNum(y + 1); buf[n++] = ';'; PutNum(x + 1); buf[n++] = 'H';
            }
            cx = x; cy = y;
        }

        public void Present(Screen s, bool ascii)
        {
            if (s.Cols != cols || s.Rows != rows)
            {
                cols = s.Cols; rows = s.Rows;
                prevA = new int[cols * rows]; prevB = new int[cols * rows]; prevC = new char[cols * rows];
                invalid = true;
            }
            n = 0;
            Ensure(cols * rows * 44 + 64);
            if (invalid)
            {
                Put("\x1b[0m\x1b[?25l\x1b[?7l");
                curFg = curBg = -1;
                cx = cy = -1;
            }
            // cursor may have moved since the last frame (e.g. after a resize): force an absolute move first
            cx = -1; cy = -1;
            tol = Tolerance;
            int w = s.W;
            int[] pix = s.Pix;
            if (ascii) BuildEqualizer(s);
            for (int y = 0; y < rows; y++)
            {
                int rowTop = (y * 2) * w, rowBot = rowTop + w;
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    char ch = s.TCh[i];
                    int a, b;
                    char c;
                    if (ch == '\0' && !ascii)
                    {
                        // HD half-block pixel pair, with a small colour tolerance so near-identical
                        // colours reuse what is already on screen or already selected (far less output)
                        int top = pix[rowTop + x] & 0xFFFFFF, bot = pix[rowBot + x] & 0xFFFFFF;
                        if (!invalid && prevC[i] == '\0' && Near(prevA[i], top, tol) && Near(prevB[i], bot, tol)) continue;
                        MoveTo(x, y);
                        int dt, db;
                        if (Near(top, bot, tol))
                        {
                            if (Near(top, curBg, tol)) { buf[n++] = ' '; dt = db = curBg; }
                            else if (Near(top, curFg, tol)) { buf[n++] = '█'; dt = db = curFg; }
                            else { Sgr(curFg, top); buf[n++] = ' '; dt = db = top; }
                        }
                        else
                        {
                            bool fa = Near(curFg, top, tol), ba = Near(curBg, bot, tol);
                            bool fb = Near(curFg, bot, tol), bb = Near(curBg, top, tol);
                            int costUp = (fa ? 0 : 1) + (ba ? 0 : 1);
                            int costDn = (fb ? 0 : 1) + (bb ? 0 : 1);
                            if (costDn < costUp) { Sgr(fb ? curFg : bot, bb ? curBg : top); buf[n++] = '▄'; dt = curBg; db = curFg; }
                            else { Sgr(fa ? curFg : top, ba ? curBg : bot); buf[n++] = '▀'; dt = curFg; db = curBg; }
                        }
                        prevC[i] = '\0'; prevA[i] = dt; prevB[i] = db;
                        cx++;
                        if (cx >= cols) cx = -1;
                        continue;
                    }
                    if (ch == '\0')
                    {
                        // ASCII: brightness (contrast-equalized per frame) picks the glyph, colour carries the hue
                        int top = pix[rowTop + x], bot = pix[rowBot + x];
                        int r = (((top >> 16) & 255) + ((bot >> 16) & 255)) >> 1;
                        int g = (((top >> 8) & 255) + ((bot >> 8) & 255)) >> 1;
                        int bl = ((top & 255) + (bot & 255)) >> 1;
                        int lum = (r * 3 + g * 6 + bl) / 10;
                        int e = lum < 5 ? 0 : (eqMap[lum] * 2 + lum) / 3;
                        c = Ramp[(e * (Ramp.Length - 1) + 127) / 255];
                        int fgc = 0;
                        if (c != ' ')
                        {
                            int mx = Math.Max(r, Math.Max(g, Math.Max(bl, 1)));
                            float k = 255f / mx * (0.4f + 0.6f * e / 255f);
                            fgc = Col.Rgb((int)(r * k), (int)(g * k), (int)(bl * k));
                        }
                        if (!invalid && prevC[i] == c && prevB[i] == 0 && (c == ' ' || Near(prevA[i], fgc, tol + 6))) continue;
                        MoveTo(x, y);
                        if (c == ' ') { if (curBg != 0) Sgr(curFg, 0); }
                        else Sgr(Near(curFg, fgc, tol + 6) ? curFg : fgc, 0);
                        buf[n++] = c;
                        prevC[i] = c; prevA[i] = curFg; prevB[i] = 0;
                        cx++;
                        if (cx >= cols) cx = -1;
                        continue;
                    }
                    c = ch; a = s.TFg[i] & 0xFFFFFF;
                    b = s.TBg[i] >= 0 ? s.TBg[i] & 0xFFFFFF : Screen.DimBehind(pix[rowTop + x], pix[rowBot + x]) & 0xFCFCFC;
                    if (!invalid && prevC[i] == c && prevA[i] == a && prevB[i] == b) continue;
                    prevC[i] = c; prevA[i] = a; prevB[i] = b;
                    MoveTo(x, y);
                    if (c == ' ') { if (b != curBg) Sgr(curFg, b); }
                    else Sgr(a, b);
                    buf[n++] = c;
                    cx++;
                    if (cx >= cols) cx = -1;   // no auto wrap: force an absolute move next
                }
            }
            invalid = false;
            LastChars = n;
            if (n > 0) Term.Write(buf, n);
        }
    }
}
