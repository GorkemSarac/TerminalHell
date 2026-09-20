// TERMINAL HELL - pixel utilities, images, drawing canvas and bitmap fonts.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    /// <summary>Colors are packed ints: 0x00RRGGBB plus two flag bits used by images.</summary>
    static class Col
    {
        public const int OPAQUE = 0x2000000;    // sprite pixel is visible
        public const int EMISSIVE = 0x1000000;  // pixel ignores lighting (lights, fire, screens)
        public const int RGB = 0xFFFFFF;

        public static int Rgb(int r, int g, int b)
        {
            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (b < 0) b = 0; else if (b > 255) b = 255;
            return (r << 16) | (g << 8) | b;
        }

        public static int Rgbf(float r, float g, float b) { return Rgb((int)(r + 0.5f), (int)(g + 0.5f), (int)(b + 0.5f)); }
        public static int R(int c) { return (c >> 16) & 255; }
        public static int G(int c) { return (c >> 8) & 255; }
        public static int B(int c) { return c & 255; }

        public static int Lerp(int a, int b, float t)
        {
            if (t <= 0) return a & RGB;
            if (t >= 1) return b & RGB;
            int ar = (a >> 16) & 255, ag = (a >> 8) & 255, ab = a & 255;
            int br = (b >> 16) & 255, bg = (b >> 8) & 255, bb = b & 255;
            return Rgb(ar + (int)((br - ar) * t), ag + (int)((bg - ag) * t), ab + (int)((bb - ab) * t));
        }

        public static int Scale(int c, float s)
        {
            return Rgb((int)(((c >> 16) & 255) * s), (int)(((c >> 8) & 255) * s), (int)((c & 255) * s));
        }

        public static int Add(int a, int b)
        {
            return Rgb(((a >> 16) & 255) + ((b >> 16) & 255), ((a >> 8) & 255) + ((b >> 8) & 255), (a & 255) + (b & 255));
        }

        public static int Mul(int a, int b)
        {
            return Rgb(((a >> 16) & 255) * ((b >> 16) & 255) / 255, ((a >> 8) & 255) * ((b >> 8) & 255) / 255, (a & 255) * (b & 255) / 255);
        }

        public static int Luma(int c) { return (((c >> 16) & 255) * 299 + ((c >> 8) & 255) * 587 + (c & 255) * 114) / 1000; }

        public static int Hex(string s)
        {
            return Convert.ToInt32(s.TrimStart('#'), 16);
        }

        /// <summary>Returns a color with brightness scaled and a hue shift toward another color.</summary>
        public static int Tint(int c, int tint, float amount) { return Lerp(c, Mul(c, tint), amount); }
    }

    /// <summary>Deterministic hash-based noise helpers used by the procedural art.</summary>
    static class Noise
    {
        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public static float Hashf(int x, int y, int seed) { return (Hash(x, y, seed) & 0xFFFFFF) / 16777216f; }

        static float Smooth(float t) { return t * t * (3 - 2 * t); }

        /// <summary>Value noise that tiles with the given period.</summary>
        public static float Value(float x, float y, int period, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            int ax = Mod(x0, period), ay = Mod(y0, period), bx = Mod(x0 + 1, period), by = Mod(y0 + 1, period);
            float a = Hashf(ax, ay, seed), b = Hashf(bx, ay, seed), c = Hashf(ax, by, seed), d = Hashf(bx, by, seed);
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }

        /// <summary>Tileable fractal noise on a size x size texture, returns roughly 0..1.</summary>
        public static float Fbm(float x, float y, int size, int baseFreq, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int freq = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(x * freq / size, y * freq / size, freq, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2;
            }
            return sum / norm;
        }

        /// <summary>Tileable cellular (Worley) noise, returns distance to nearest feature point (0..~1).</summary>
        public static float Cell(float x, float y, int size, int cells, int seed)
        {
            float cx = x * cells / size, cy = y * cells / size;
            int ix = (int)Math.Floor(cx), iy = (int)Math.Floor(cy);
            float best = 9;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = ix + ox, gy = iy + oy;
                    int hx = Mod(gx, cells), hy = Mod(gy, cells);
                    float px = gx + Hashf(hx, hy, seed), py = gy + Hashf(hx, hy, seed + 7);
                    float d = (px - cx) * (px - cx) + (py - cy) * (py - cy);
                    if (d < best) best = d;
                }
            return (float)Math.Sqrt(best);
        }

        public static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
    }

    /// <summary>A bitmap (texture or sprite). Pixels are packed colors with optional flag bits.</summary>
    sealed class Image
    {
        public int W, H;
        public int[] Px;
        public int[] ColTop, ColBot;   // per-column opaque extent, for fast sprite rendering

        public Image(int w, int h) { W = w; H = h; Px = new int[w * h]; }

        public int Get(int x, int y) { return Px[y * W + x]; }
        public void Set(int x, int y, int c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[y * W + x] = c; }

        public void ComputeExtents()
        {
            ColTop = new int[W]; ColBot = new int[W];
            for (int x = 0; x < W; x++)
            {
                int t = H, b = -1;
                for (int y = 0; y < H; y++)
                    if ((Px[y * W + x] & Col.OPAQUE) != 0) { if (y < t) t = y; b = y; }
                ColTop[x] = t; ColBot[x] = b;
            }
        }

        public Image Clone()
        {
            var im = new Image(W, H);
            Array.Copy(Px, im.Px, Px.Length);
            return im;
        }

        /// <summary>Horizontal mirror.</summary>
        public Image Mirror()
        {
            var im = new Image(W, H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    im.Px[y * W + x] = Px[y * W + (W - 1 - x)];
            im.ComputeExtents();
            return im;
        }
    }

    /// <summary>
    /// Floating point drawing canvas used to build sprites and weapons procedurally.
    /// Shapes are shaded as if lit from the upper-left-front to give a 3D "clay" look.
    /// </summary>
    sealed class Canvas
    {
        public readonly Image Img;
        public readonly int W, H;
        public float LightX = -0.45f, LightY = -0.6f, LightZ = 0.66f;
        public float Ambient = 0.35f, Diffuse = 0.75f, Spec = 0.25f;
        public int NoiseSeed = 1;
        public float NoiseAmt = 0.06f;

        public Canvas(int w, int h) { W = w; H = h; Img = new Image(w, h); }

        public void Plot(int x, int y, int c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Img.Px[y * W + x] = (c & (Col.RGB | Col.EMISSIVE)) | Col.OPAQUE;
        }

        public int Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return 0;
            return Img.Px[y * W + x];
        }

        public void Blend(int x, int y, int c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int old = Img.Px[y * W + x];
            if ((old & Col.OPAQUE) == 0) { if (a >= 0.5f) Plot(x, y, c); return; }
            Img.Px[y * W + x] = (Col.Lerp(old, c, a) | Col.OPAQUE) | (old & Col.EMISSIVE);
        }

        float Grain(int x, int y)
        {
            return 1 + (Noise.Hashf(x, y, NoiseSeed) - 0.5f) * 2 * NoiseAmt;
        }

        int ShadeNormal(int baseCol, float nx, float ny, float nz, int x, int y)
        {
            float d = nx * LightX + ny * LightY + nz * LightZ;
            if (d < 0) d = 0;
            float s = (Ambient + Diffuse * d) * Grain(x, y);
            // specular (half vector approx towards viewer)
            float hx = LightX, hy = LightY, hz = LightZ + 1;
            float hl = (float)Math.Sqrt(hx * hx + hy * hy + hz * hz);
            float sp = (nx * hx + ny * hy + nz * hz) / hl;
            float spec = sp > 0 ? (float)Math.Pow(sp, 12) * Spec : 0;
            int r = (int)(Col.R(baseCol) * s + 255 * spec);
            int g = (int)(Col.G(baseCol) * s + 255 * spec);
            int b = (int)(Col.B(baseCol) * s + 255 * spec);
            return Col.Rgb(r, g, b);
        }

        /// <summary>Filled ellipse with spherical shading.</summary>
        public void Ball(float cx, float cy, float rx, float ry, int col)
        {
            Ball(cx, cy, rx, ry, col, true);
        }

        public void Ball(float cx, float cy, float rx, float ry, int col, bool shaded)
        {
            int x0 = (int)Math.Floor(cx - rx), x1 = (int)Math.Ceiling(cx + rx);
            int y0 = (int)Math.Floor(cy - ry), y1 = (int)Math.Ceiling(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    float d2 = nx * nx + ny * ny;
                    if (d2 > 1) continue;
                    float nz = (float)Math.Sqrt(1 - d2);
                    Plot(x, y, shaded ? ShadeNormal(col, nx, ny, nz, x, y) : col);
                }
        }

        /// <summary>Tapered capsule (limb) between two points with cylindrical shading.</summary>
        public void Limb(float x0, float y0, float r0, float x1, float y1, float r1, int col)
        {
            float minX = Math.Min(x0 - r0, x1 - r1), maxX = Math.Max(x0 + r0, x1 + r1);
            float minY = Math.Min(y0 - r0, y1 - r1), maxY = Math.Max(y0 + r0, y1 + r1);
            float dx = x1 - x0, dy = y1 - y0;
            float len2 = dx * dx + dy * dy;
            if (len2 < 1e-6f) { Ball(x0, y0, r0, r0, col); return; }
            float len = (float)Math.Sqrt(len2);
            float px = -dy / len, py = dx / len;   // perpendicular
            for (int y = (int)Math.Floor(minY); y <= (int)Math.Ceiling(maxY); y++)
                for (int x = (int)Math.Floor(minX); x <= (int)Math.Ceiling(maxX); x++)
                {
                    float qx = x + 0.5f - x0, qy = y + 0.5f - y0;
                    float t = (qx * dx + qy * dy) / len2;
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    float cx = x0 + dx * t, cy = y0 + dy * t;
                    float r = r0 + (r1 - r0) * t;
                    float ox = x + 0.5f - cx, oy = y + 0.5f - cy;
                    float d = (float)Math.Sqrt(ox * ox + oy * oy);
                    if (d > r) continue;
                    // normal: across the limb plus a bit along it at the rounded ends
                    float across = (ox * px + oy * py) / r;
                    float nx = ox / r, ny = oy / r;
                    float nz = (float)Math.Sqrt(Math.Max(0, 1 - (nx * nx + ny * ny)));
                    Plot(x, y, ShadeNormal(col, nx * 0.9f + px * across * 0.1f, ny * 0.9f + py * across * 0.1f, nz, x, y));
                }
        }

        /// <summary>Flat-shaded (optionally gradient) convex or concave polygon via scanline even-odd fill.</summary>
        public void Poly(float[] pts, int col) { Poly(pts, col, col); }

        public void Poly(float[] pts, int colTop, int colBottom)
        {
            int n = pts.Length / 2;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++) { minY = Math.Min(minY, pts[i * 2 + 1]); maxY = Math.Max(maxY, pts[i * 2 + 1]); }
            var xs = new List<float>();
            for (int y = (int)Math.Floor(minY); y <= (int)Math.Ceiling(maxY); y++)
            {
                float sy = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    float ax = pts[i * 2], ay = pts[i * 2 + 1];
                    float bx = pts[((i + 1) % n) * 2], by = pts[((i + 1) % n) * 2 + 1];
                    if ((ay <= sy && by > sy) || (by <= sy && ay > sy))
                        xs.Add(ax + (sy - ay) / (by - ay) * (bx - ax));
                }
                xs.Sort();
                float t = maxY > minY ? (sy - minY) / (maxY - minY) : 0;
                int c = Col.Lerp(colTop, colBottom, t) | (colTop & Col.EMISSIVE);
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = (int)Math.Round(xs[k]), xb = (int)Math.Round(xs[k + 1]);
                    for (int x = xa; x < xb; x++) Plot(x, y, Col.Scale(c, Grain(x, y)) | (c & Col.EMISSIVE));
                }
            }
        }

        public void Rect(float x, float y, float w, float h, int col)
        {
            Poly(new float[] { x, y, x + w, y, x + w, y + h, x, y + h }, col);
        }

        public void Rect(float x, float y, float w, float h, int colTop, int colBottom)
        {
            Poly(new float[] { x, y, x + w, y, x + w, y + h, x, y + h }, colTop, colBottom);
        }

        /// <summary>Horizontal cylinder shading for a box (used for barrels, guns): brighter in the middle row band.</summary>
        public void Tube(float x, float y, float w, float h, int col, bool vertical)
        {
            int x0 = (int)Math.Floor(x), x1 = (int)Math.Ceiling(x + w), y0 = (int)Math.Floor(y), y1 = (int)Math.Ceiling(y + h);
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    float u = vertical ? (px + 0.5f - x) / w : (py + 0.5f - y) / h;
                    if (u < 0 || u > 1) continue;
                    float n = u * 2 - 1;   // -1..1 across the tube
                    float nz = (float)Math.Sqrt(Math.Max(0, 1 - n * n));
                    Plot(px, py, vertical ? ShadeNormal(col, n, 0, nz, px, py) : ShadeNormal(col, 0, n, nz, px, py));
                }
        }

        /// <summary>Anti-aliased-ish thick line.</summary>
        public void Line(float x0, float y0, float x1, float y1, float thick, int col)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = (int)(Math.Max(Math.Abs(dx), Math.Abs(dy)) * 2) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float cx = x0 + dx * t, cy = y0 + dy * t;
                int r = (int)Math.Ceiling(thick / 2);
                for (int oy = -r; oy <= r; oy++)
                    for (int ox = -r; ox <= r; ox++)
                    {
                        float px = (float)Math.Floor(cx) + ox + 0.5f, py = (float)Math.Floor(cy) + oy + 0.5f;
                        if ((px - cx) * (px - cx) + (py - cy) * (py - cy) <= thick * thick / 4 + 0.25f)
                            Plot((int)Math.Floor(px), (int)Math.Floor(py), col);
                    }
            }
        }

        /// <summary>Radial glow (emissive) that fades out; used for fire, plasma, muzzle flashes.</summary>
        public void Glow(float cx, float cy, float r, int inner, int outer)
        {
            for (int y = (int)(cy - r) - 1; y <= (int)(cy + r) + 1; y++)
                for (int x = (int)(cx - r) - 1; x <= (int)(cx + r) + 1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) / r;
                    if (d > 1) continue;
                    int c = Col.Lerp(inner, outer, d * d);
                    Plot(x, y, c | Col.EMISSIVE);
                }
        }

        /// <summary>Darkens edge pixels next to transparency to give a readable silhouette.</summary>
        public void Outline(int col)
        {
            var src = (int[])Img.Px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if ((src[y * W + x] & Col.OPAQUE) != 0) continue;
                    bool edge = false;
                    for (int k = 0; k < 4 && !edge; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int s = src[ny * W + nx];
                        if ((s & Col.OPAQUE) != 0 && (s & Col.EMISSIVE) == 0) edge = true;
                    }
                    if (edge) Img.Px[y * W + x] = col | Col.OPAQUE;
                }
        }

        /// <summary>Shades existing pixels: darker toward the bottom, used to ground figures.</summary>
        public void GroundShade(float strength)
        {
            for (int y = 0; y < H; y++)
            {
                float s = 1 - strength * y / H;
                for (int x = 0; x < W; x++)
                {
                    int p = Img.Px[y * W + x];
                    if ((p & Col.OPAQUE) == 0 || (p & Col.EMISSIVE) != 0) continue;
                    Img.Px[y * W + x] = Col.Scale(p, s) | Col.OPAQUE;
                }
            }
        }

        public Image Done()
        {
            Img.ComputeExtents();
            return Img;
        }
    }

    /// <summary>5x7 pixel font used for big titles drawn into the pixel buffer.</summary>
    static class PixFont
    {
        static readonly Dictionary<char, byte[]> glyphs = new Dictionary<char, byte[]>();

        static PixFont()
        {
            string[] defs =
            {
                "A 01110 10001 10001 11111 10001 10001 10001",
                "B 11110 10001 10001 11110 10001 10001 11110",
                "C 01111 10000 10000 10000 10000 10000 01111",
                "D 11110 10001 10001 10001 10001 10001 11110",
                "E 11111 10000 10000 11110 10000 10000 11111",
                "F 11111 10000 10000 11110 10000 10000 10000",
                "G 01111 10000 10000 10011 10001 10001 01111",
                "H 10001 10001 10001 11111 10001 10001 10001",
                "I 11111 00100 00100 00100 00100 00100 11111",
                "J 00111 00010 00010 00010 00010 10010 01100",
                "K 10001 10010 10100 11000 10100 10010 10001",
                "L 10000 10000 10000 10000 10000 10000 11111",
                "M 10001 11011 10101 10101 10001 10001 10001",
                "N 10001 11001 10101 10011 10001 10001 10001",
                "O 01110 10001 10001 10001 10001 10001 01110",
                "P 11110 10001 10001 11110 10000 10000 10000",
                "Q 01110 10001 10001 10001 10101 10010 01101",
                "R 11110 10001 10001 11110 10100 10010 10001",
                "S 01111 10000 10000 01110 00001 00001 11110",
                "T 11111 00100 00100 00100 00100 00100 00100",
                "U 10001 10001 10001 10001 10001 10001 01110",
                "V 10001 10001 10001 10001 10001 01010 00100",
                "W 10001 10001 10001 10101 10101 10101 01010",
                "X 10001 10001 01010 00100 01010 10001 10001",
                "Y 10001 10001 01010 00100 00100 00100 00100",
                "Z 11111 00001 00010 00100 01000 10000 11111",
                "0 01110 10001 10011 10101 11001 10001 01110",
                "1 00100 01100 00100 00100 00100 00100 01110",
                "2 01110 10001 00001 00010 00100 01000 11111",
                "3 11110 00001 00001 01110 00001 00001 11110",
                "4 00010 00110 01010 10010 11111 00010 00010",
                "5 11111 10000 11110 00001 00001 10001 01110",
                "6 00110 01000 10000 11110 10001 10001 01110",
                "7 11111 00001 00010 00100 01000 01000 01000",
                "8 01110 10001 10001 01110 10001 10001 01110",
                "9 01110 10001 10001 01111 00001 00010 01100",
                "! 00100 00100 00100 00100 00100 00000 00100",
                "? 01110 10001 00001 00010 00100 00000 00100",
                ". 00000 00000 00000 00000 00000 00000 00100",
                ", 00000 00000 00000 00000 00000 00100 01000",
                ": 00000 00100 00000 00000 00000 00100 00000",
                "- 00000 00000 00000 11111 00000 00000 00000",
                "' 00100 00100 01000 00000 00000 00000 00000",
                "% 11001 11010 00010 00100 01000 01011 10011",
                "/ 00001 00010 00010 00100 01000 01000 10000",
                "_ 00000 00000 00000 00000 00000 00000 11111",
            };
            foreach (var d in defs)
            {
                var parts = d.Split(' ');
                var rows = new byte[7];
                for (int i = 0; i < 7; i++) rows[i] = Convert.ToByte(parts[i + 1], 2);
                glyphs[parts[0][0]] = rows;
            }
            glyphs[' '] = new byte[7];
        }

        public static int TextWidth(string s, int scale) { return s.Length == 0 ? 0 : (s.Length * 6 - 1) * scale; }

        public static bool Pixel(char c, int x, int y)
        {
            byte[] g;
            if (!glyphs.TryGetValue(char.ToUpperInvariant(c), out g)) return false;
            if (x < 0 || x > 4 || y < 0 || y > 6) return false;
            return (g[y] & (16 >> x)) != 0;
        }

        /// <summary>Draws text into a pixel buffer with a vertical gradient and a drop shadow/outline.</summary>
        public static void Draw(int[] pix, int pw, int ph, string s, int x0, int y0, int scale, int colTop, int colBot, int shadow)
        {
            Draw(pix, pw, ph, s, x0, y0, scale, colTop, colBot, shadow, 1f);
        }

        public static void Draw(int[] pix, int pw, int ph, string s, int x0, int y0, int scale, int colTop, int colBot, int shadow, float alpha)
        {
            int th = 7 * scale;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    for (int gy = 0; gy < 7; gy++)
                        for (int gx = 0; gx < 5; gx++)
                        {
                            if (!Pixel(c, gx, gy)) continue;
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    int px = x0 + (i * 6 + gx) * scale + sx, py = y0 + gy * scale + sy;
                                    if (pass == 0)
                                    {
                                        if (shadow < 0) continue;
                                        for (int o = 1; o <= Math.Max(1, scale / 2); o++)
                                            PutPix(pix, pw, ph, px + o, py + o, shadow, alpha * 0.8f);
                                        PutPix(pix, pw, ph, px - 1, py, shadow, alpha * 0.8f);
                                        PutPix(pix, pw, ph, px, py - 1, shadow, alpha * 0.8f);
                                    }
                                    else
                                    {
                                        float t = th > 1 ? (float)(py - y0) / (th - 1) : 0;
                                        PutPix(pix, pw, ph, px, py, Col.Lerp(colTop, colBot, t), alpha);
                                    }
                                }
                        }
                }
            }
        }

        internal static void PutPix(int[] pix, int pw, int ph, int x, int y, int c, float alpha)
        {
            if (x < 0 || y < 0 || x >= pw || y >= ph) return;
            pix[y * pw + x] = alpha >= 1 ? c : Col.Lerp(pix[y * pw + x], c, alpha);
        }
    }

    /// <summary>A smaller 4x5 pixel font, used for pickup messages: clearly bigger than a line of terminal
    /// text, without the titles' 7 pixel letters taking over the top of the screen.</summary>
    static class MiniFont
    {
        static readonly Dictionary<char, byte[]> glyphs = new Dictionary<char, byte[]>();

        static MiniFont()
        {
            string[] defs =
            {
                "A 0110 1001 1111 1001 1001",
                "B 1110 1001 1110 1001 1110",
                "C 0111 1000 1000 1000 0111",
                "D 1110 1001 1001 1001 1110",
                "E 1111 1000 1110 1000 1111",
                "F 1111 1000 1110 1000 1000",
                "G 0111 1000 1011 1001 0111",
                "H 1001 1001 1111 1001 1001",
                "I 1110 0100 0100 0100 1110",
                "J 0011 0001 0001 1001 0110",
                "K 1001 1010 1100 1010 1001",
                "L 1000 1000 1000 1000 1111",
                "M 1001 1111 1111 1001 1001",
                "N 1001 1101 1011 1001 1001",
                "O 0110 1001 1001 1001 0110",
                "P 1110 1001 1110 1000 1000",
                "Q 0110 1001 1001 1011 0111",
                "R 1110 1001 1110 1010 1001",
                "S 0111 1000 0110 0001 1110",
                "T 1111 0100 0100 0100 0100",
                "U 1001 1001 1001 1001 0110",
                "V 1001 1001 1001 1010 0100",
                "W 1001 1001 1111 1111 0110",
                "X 1001 1001 0110 1001 1001",
                "Y 1001 1001 0110 0100 0100",
                "Z 1111 0010 0100 1000 1111",
                "0 0110 1011 1101 1001 0110",
                "1 0100 1100 0100 0100 1110",
                "2 1110 0001 0110 1000 1111",
                "3 1110 0001 0110 0001 1110",
                "4 1001 1001 1111 0001 0001",
                "5 1111 1000 1110 0001 1110",
                "6 0110 1000 1110 1001 0110",
                "7 1111 0001 0010 0100 0100",
                "8 0110 1001 0110 1001 0110",
                "9 0110 1001 0111 0001 0110",
                "! 0100 0100 0100 0000 0100",
                "? 1110 0001 0110 0000 0100",
                ". 0000 0000 0000 0000 0100",
                ", 0000 0000 0000 0100 1000",
                ": 0000 0100 0000 0100 0000",
                "- 0000 0000 1111 0000 0000",
                "' 0100 0100 0000 0000 0000",
                "+ 0000 0100 1110 0100 0000",
                "% 1001 0010 0100 1000 1001",
                "/ 0001 0010 0100 1000 1000",
                "_ 0000 0000 0000 0000 1111",
            };
            foreach (var d in defs)
            {
                var parts = d.Split(' ');
                var rows = new byte[5];
                for (int i = 0; i < 5; i++) rows[i] = Convert.ToByte(parts[i + 1], 2);
                glyphs[parts[0][0]] = rows;
            }
            glyphs[' '] = new byte[5];
        }

        public const int Height = 5;

        public static int TextWidth(string s, int scale) { return s.Length == 0 ? 0 : (s.Length * 5 - 1) * scale; }

        static bool Pixel(char c, int x, int y)
        {
            byte[] g;
            if (!glyphs.TryGetValue(char.ToUpperInvariant(c), out g)) return false;
            if (x < 0 || x > 3 || y < 0 || y > 4) return false;
            return (g[y] & (8 >> x)) != 0;
        }

        /// <summary>Same look as the big font: a vertical gradient with a dark outline behind it.</summary>
        public static void Draw(int[] pix, int pw, int ph, string s, int x0, int y0, int scale, int colTop, int colBot, int shadow, float alpha)
        {
            int th = Height * scale;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < s.Length; i++)
                    for (int gy = 0; gy < Height; gy++)
                        for (int gx = 0; gx < 4; gx++)
                        {
                            if (!Pixel(s[i], gx, gy)) continue;
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    int px = x0 + (i * 5 + gx) * scale + sx, py = y0 + gy * scale + sy;
                                    if (pass == 0)
                                    {
                                        if (shadow < 0) continue;
                                        PixFont.PutPix(pix, pw, ph, px + 1, py + 1, shadow, alpha * 0.8f);
                                        PixFont.PutPix(pix, pw, ph, px - 1, py, shadow, alpha * 0.8f);
                                        PixFont.PutPix(pix, pw, ph, px, py - 1, shadow, alpha * 0.8f);
                                    }
                                    else
                                    {
                                        float t = th > 1 ? (float)(py - y0) / (th - 1) : 0;
                                        PixFont.PutPix(pix, pw, ph, px, py, Col.Lerp(colTop, colBot, t), alpha);
                                    }
                                }
                        }
        }
    }

    /// <summary>3x5 digit font rendered with half-block characters for the status bar (3 cells wide, 3 rows tall).</summary>
    static class BigDigits
    {
        static readonly string[] font =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001010010010", "111101111101111", "111101111001111",
        };
        // '%' glyph
        const string percent = "101001010100101";

        public static bool Pix(char c, int x, int y)
        {
            if (y >= 5 || y < 0 || x < 0 || x > 2) return false;
            string g = c == '%' ? percent : (c >= '0' && c <= '9') ? font[c - '0'] : null;
            if (g == null) return false;
            return g[y * 3 + x] == '1';
        }

        /// <summary>Returns the half-block char for glyph column x at text row r (0..2).</summary>
        public static char Cell(char c, int x, int r)
        {
            bool top = Pix(c, x, r * 2), bot = Pix(c, x, r * 2 + 1);
            if (top && bot) return '█';
            if (top) return '▀';
            if (bot) return '▄';
            return ' ';
        }
    }
}
