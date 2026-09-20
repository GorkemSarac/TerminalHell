// TERMINAL HELL - procedurally generated wall, floor, ceiling and sky textures (no asset files).
using System;

namespace TerminalHell
{
    static class Tex
    {
        public const int S = 64;         // texture size
        public const int Mask = S - 1;

        // wall texture ids (index into Walls)
        public const int STONE = 1, BRICK = 2, TECH = 3, WOOD = 4, MARBLE = 5, FLESH = 6, COMPUTER = 7, RUST = 8, SKULLS = 9, ROCK = 10;
        public const int EXIT_OFF = 11, EXIT_ON = 12, DOOR = 13, DOOR_RED = 14, DOOR_BLUE = 15, DOOR_YELLOW = 16, JAMB = 17;
        // one variant of every plain wall (STONE..ROCK): secret walls that can be pushed, and the lit panels beside doors
        public const int SECRET_BASE = 18, DOORLIT_BASE = 28;
        public const int WALL_COUNT = 38;

        /// <summary>The "this one slides away" version of a wall texture (secret push walls).</summary>
        public static int SecretOf(int tex) { return tex >= STONE && tex <= ROCK ? SECRET_BASE + tex - STONE : tex; }

        /// <summary>The version with a light strip, used on the walls either side of a door.</summary>
        public static int DoorLitOf(int tex) { return tex >= STONE && tex <= ROCK ? DOORLIT_BASE + tex - STONE : tex; }

        // flat texture ids (index into Flats)
        public const int F_TILE = 0, F_METAL = 1, F_DIRT = 2, F_WOOD = 3, F_HELL = 4, F_LAVA = 5, F_NUKAGE = 6;
        public const int C_PANEL = 7, C_STONE = 8, C_WOOD = 9, F_GRATE = 10, F_MARBLE = 11, C_FLESH = 12;
        public const int FLAT_COUNT = 13;

        public static Image[] Walls = new Image[WALL_COUNT];
        public static Image[] Flats = new Image[FLAT_COUNT];
        public static Image Sky;          // red hell sky
        public static Image SkyNight;     // darker sky variant

        public static void Build()
        {
            Walls[STONE] = BlockWall(11, new[] { 16, 16, 16, 16 }, new[] { new[] { 24, 20, 20 }, new[] { 32, 32 }, new[] { 20, 24, 20 }, new[] { 30, 34 } },
                new[] { 0, 16, 40, 5 }, 2, Col.Rgb(112, 110, 104), 18, Col.Rgb(38, 36, 34), 0.28f);
            Walls[BRICK] = BlockWall(23, new[] { 8, 8, 8, 8, 8, 8, 8, 8 }, new[] { new[] { 16, 16, 16, 16 }, new[] { 16, 16, 16, 16 } },
                new[] { 0, 8, 0, 8, 0, 8, 0, 8 }, 1, Col.Rgb(142, 62, 42), 22, Col.Rgb(86, 78, 70), 0.22f);
            Walls[TECH] = TechWall(Col.Rgb(92, 102, 118), 3);
            Walls[WOOD] = WoodWall();
            Walls[MARBLE] = MarbleWall();
            Walls[FLESH] = FleshWall();
            Walls[COMPUTER] = ComputerWall();
            Walls[RUST] = RustWall();
            Walls[SKULLS] = SkullWall();
            Walls[ROCK] = RockWall();
            Walls[EXIT_OFF] = ExitWall(false);
            Walls[EXIT_ON] = ExitWall(true);
            Walls[DOOR] = DoorTex(-1);
            Walls[DOOR_RED] = DoorTex(Col.Rgb(255, 40, 30));
            Walls[DOOR_BLUE] = DoorTex(Col.Rgb(40, 110, 255));
            Walls[DOOR_YELLOW] = DoorTex(Col.Rgb(255, 210, 40));
            Walls[JAMB] = JambTex();
            Walls[0] = Walls[STONE];
            for (int t = STONE; t <= ROCK; t++)
            {
                Walls[SecretOf(t)] = SecretWall(Walls[t]);
                Walls[DoorLitOf(t)] = DoorLitWall(Walls[t]);
            }

            Flats[F_TILE] = TileFloor();
            Flats[F_METAL] = MetalFloor();
            Flats[F_DIRT] = DirtFloor();
            Flats[F_WOOD] = WoodFloor();
            Flats[F_HELL] = HellFloor();
            Flats[F_LAVA] = Liquid(Col.Rgb(255, 120, 20), Col.Rgb(255, 230, 120), Col.Rgb(90, 20, 5), 51);
            Flats[F_NUKAGE] = Liquid(Col.Rgb(40, 200, 40), Col.Rgb(190, 255, 120), Col.Rgb(10, 60, 10), 77);
            Flats[C_PANEL] = CeilPanel();
            Flats[C_STONE] = CeilStone();
            Flats[C_WOOD] = CeilWood();
            Flats[F_GRATE] = GrateFloor();
            Flats[F_MARBLE] = MarbleFloor();
            Flats[C_FLESH] = FleshCeil();

            Sky = MakeSky(Col.Rgb(40, 4, 8), Col.Rgb(210, 70, 20), Col.Rgb(255, 170, 60), 5);
            SkyNight = MakeSky(Col.Rgb(4, 4, 18), Col.Rgb(60, 30, 70), Col.Rgb(160, 70, 60), 9);
        }

        static int Px(Image im, int x, int y) { return im.Px[(y & Mask) * S + (x & Mask)]; }

        static void Set(Image im, int x, int y, int c) { im.Px[(y & Mask) * S + (x & Mask)] = c | Col.OPAQUE; }

        static int Vary(int c, float f) { return Col.Scale(c, f); }

        // ---------------------------------------------------------------- walls

        /// <summary>Generic block/brick wall with bevels, per-block tint and grime.</summary>
        static Image BlockWall(int seed, int[] rowH, int[][] widths, int[] rowOffset, int mortar, int baseCol, int varAmt, int mortarCol, float grime)
        {
            var im = new Image(S, S);
            int y0 = 0;
            for (int r = 0; r < rowH.Length; r++)
            {
                int[] ws = widths[r % widths.Length];
                int x0 = rowOffset[r];
                for (int b = 0; b < ws.Length; b++)
                {
                    int w = ws[b];
                    float tint = 1 + (Noise.Hashf(r, b, seed) - 0.5f) * varAmt / 100f * 2.2f;
                    int hueShift = (int)((Noise.Hashf(b, r, seed + 3) - 0.5f) * 14);
                    int bc = Col.Rgb((int)(Col.R(baseCol) * tint) + hueShift, (int)(Col.G(baseCol) * tint), (int)(Col.B(baseCol) * tint) - hueShift);
                    for (int yy = 0; yy < rowH[r]; yy++)
                        for (int xx = 0; xx < w; xx++)
                        {
                            int x = x0 + xx, y = y0 + yy;
                            int c;
                            if (yy >= rowH[r] - mortar || xx >= w - mortar)
                            {
                                c = Vary(mortarCol, 0.8f + Noise.Hashf(x, y, seed + 9) * 0.4f);
                            }
                            else
                            {
                                float n = Noise.Fbm(x, y, S, 4, 4, seed);
                                float f = 0.78f + n * 0.44f;
                                // bevel: light on top/left edges, dark on bottom/right
                                if (yy == 0 || xx == 0) f += 0.22f;
                                else if (yy == 1 || xx == 1) f += 0.08f;
                                if (yy == rowH[r] - mortar - 1 || xx == w - mortar - 1) f -= 0.25f;
                                // speckles
                                if (Noise.Hashf(x, y, seed + 5) > 0.93f) f -= 0.18f;
                                c = Vary(bc, f);
                            }
                            // vertical grime streaks
                            float g = Noise.Fbm(x * 3, y * 0.5f, S, 4, 3, seed + 11);
                            float gy = (float)(y) / S;
                            c = Col.Scale(c, 1 - grime * Math.Max(0, g - 0.45f) * 2 * (0.4f + gy));
                            Set(im, x, y, c);
                        }
                    x0 += w;
                }
                y0 += rowH[r];
            }
            return im;
        }

        static Image TechWall(int baseCol, int seed)
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 3, seed) * 0.25f + 0.85f;
                    int c = Vary(baseCol, n);
                    int px = x % 32, py = y % 32;
                    // horizontal band at the middle
                    if (y >= 26 && y < 38)
                    {
                        c = Vary(baseCol, 0.62f + (x % 4 == 0 ? -0.2f : 0.05f));
                        if (y == 26) c = Vary(baseCol, 1.25f);
                        if (y == 37) c = Vary(baseCol, 0.35f);
                    }
                    else
                    {
                        // panel bevels
                        if (px == 0 || py == 0) c = Vary(baseCol, 1.35f);
                        else if (px == 31 || py == 31) c = Vary(baseCol, 0.4f);
                        else if (px == 1 || py == 1) c = Vary(baseCol, 1.1f);
                        // rivets
                        int rx = px < 16 ? px - 4 : px - 27, ry = py < 16 ? py - 4 : py - 27;
                        if (rx * rx + ry * ry <= 2 && !(y >= 22 && y < 42))
                            c = rx + ry < 0 ? Col.Rgb(210, 215, 225) : Vary(baseCol, 0.5f);
                    }
                    // scratches
                    if (Noise.Hashf(x / 3, y, seed + 2) > 0.985f) c = Vary(c, 1.3f);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image WoodWall()
        {
            var im = new Image(S, S);
            int[] edges = { 0, 14, 30, 45, 64 };
            for (int p = 0; p < 4; p++)
            {
                float tone = 0.85f + Noise.Hashf(p, 1, 41) * 0.3f;
                for (int x = edges[p]; x < edges[p + 1]; x++)
                    for (int y = 0; y < S; y++)
                    {
                        float n = Noise.Fbm(x * 4, y * 0.6f, S, 4, 3, 40 + p);
                        float grain = (float)Math.Sin((x + n * 10) * 1.3f) * 0.5f + 0.5f;
                        float f = tone * (0.75f + grain * 0.3f);
                        int c = Col.Rgb((int)(126 * f), (int)(78 * f), (int)(42 * f));
                        if (x == edges[p]) c = Col.Rgb(40, 24, 14);
                        else if (x == edges[p] + 1) c = Vary(c, 1.2f);
                        else if (x == edges[p + 1] - 1) c = Vary(c, 0.6f);
                        // nails
                        if ((y == 6 || y == 58) && (x == edges[p] + 3 || x == edges[p + 1] - 4)) c = Col.Rgb(160, 150, 130);
                        Set(im, x, y, c);
                    }
            }
            return im;
        }

        static Image MarbleWall()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 4, 5, 55);
                    float v = (float)Math.Abs(Math.Sin((x * 0.098 + y * 0.049) * Math.PI * 2 / 1.0 + n * 9));
                    float vein = (float)Math.Pow(1 - v, 8);
                    int c = Col.Lerp(Col.Rgb(34, 72, 48), Col.Rgb(150, 200, 150), vein * 0.9f);
                    c = Vary(c, 0.8f + n * 0.4f);
                    // carved frame
                    int fx = Math.Min(x, 63 - x), fy = Math.Min(y, 63 - y);
                    int f = Math.Min(fx, fy);
                    if (f < 4)
                    {
                        c = Col.Rgb(60, 58, 50);
                        if (f == 0) c = Col.Rgb(30, 28, 24);
                        else if (fx == f && x < 32 || fy == f && y < 32) c = Col.Rgb(98, 94, 84);
                        c = Vary(c, 0.85f + Noise.Hashf(x, y, 56) * 0.3f);
                    }
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image FleshWall()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float cell = Noise.Cell(x, y, S, 6, 61);
                    float n = Noise.Fbm(x, y, S, 4, 4, 62);
                    float bump = 1 - Math.Min(1, cell * 1.4f);
                    int c = Col.Lerp(Col.Rgb(50, 6, 10), Col.Rgb(190, 50, 45), bump * 0.9f + n * 0.2f);
                    // veins
                    float ridge = 1 - Math.Abs(Noise.Fbm(x, y, S, 2, 4, 63) - 0.5f) * 2;
                    if (ridge > 0.9f) c = Col.Lerp(c, Col.Rgb(90, 20, 70), (ridge - 0.9f) * 8);
                    // wet highlight
                    if (bump > 0.75f && Noise.Hashf(x, y, 64) > 0.6f) c = Vary(c, 1.25f);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image ComputerWall()
        {
            var im = TechWall(Col.Rgb(78, 84, 96), 71);
            // screen
            for (int y = 8; y < 36; y++)
                for (int x = 6; x < 58; x++)
                {
                    int c;
                    if (y == 8 || x == 6) c = Col.Rgb(30, 32, 38);
                    else if (y == 35 || x == 57) c = Col.Rgb(150, 156, 170);
                    else
                    {
                        c = Col.Rgb(6, 22, 14) | Col.EMISSIVE;
                        int line = (y - 11) / 3;
                        if ((y - 11) % 3 == 0 && y > 10 && y < 33)
                        {
                            int len = 12 + (int)(Noise.Hashf(line, 0, 72) * 34);
                            if (x - 9 < len && Noise.Hashf(x, line, 73) > 0.22f && x > 8)
                                c = Col.Rgb(80, 255, 120) | Col.EMISSIVE;
                        }
                    }
                    im.Px[y * S + x] = c | Col.OPAQUE;
                }
            // indicator lights
            int[] lights = { Col.Rgb(255, 40, 30), Col.Rgb(60, 255, 60), Col.Rgb(255, 220, 40), Col.Rgb(60, 160, 255) };
            for (int i = 0; i < 8; i++)
            {
                int lx = 9 + i * 6, ly = 44;
                int c = lights[(i * 7 + 3) % 4];
                for (int yy = 0; yy < 3; yy++)
                    for (int xx = 0; xx < 3; xx++)
                        im.Px[(ly + yy) * S + lx + xx] = (yy == 0 && xx == 0 ? Col.Lerp(c, 0xFFFFFF, 0.6f) : c) | Col.EMISSIVE | Col.OPAQUE;
            }
            // vents
            for (int y = 52; y < 60; y += 2)
                for (int x = 10; x < 54; x++)
                    im.Px[y * S + x] = Col.Rgb(26, 28, 32) | Col.OPAQUE;
            return im;
        }

        static Image RustWall()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 4, 5, 81);
                    float r = Noise.Fbm(x, y, S, 8, 3, 82);
                    int c = Col.Lerp(Col.Rgb(96, 88, 80), Col.Rgb(150, 72, 30), Math.Min(1, Math.Max(0, (r - 0.4f) * 3)));
                    c = Vary(c, 0.75f + n * 0.45f);
                    // streaks
                    float s = Noise.Fbm(x * 4, y * 0.3f, S, 4, 2, 83);
                    if (s > 0.6f) c = Col.Lerp(c, Col.Rgb(90, 40, 16), (s - 0.6f) * 2);
                    int px = x % 32, py = y % 32;
                    if (px == 0 || py == 0) c = Col.Rgb(40, 30, 24);
                    else if (px == 1 || py == 1) c = Vary(c, 1.25f);
                    if ((px == 4 || px == 28) && py % 6 == 3) c = Col.Rgb(180, 150, 120);
                    if ((px == 5 || px == 29) && py % 6 == 4) c = Col.Rgb(40, 28, 20);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image SkullWall()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 3, 91);
                    Set(im, x, y, Vary(Col.Rgb(46, 32, 26), 0.7f + n * 0.6f));
                }
            for (int k = 0; k < 4; k++)
            {
                int ox = (k % 2) * 32 + ((k / 2) % 2) * 16, oy = (k / 2) * 32;
                var cv = new Canvas(32, 32);
                cv.NoiseSeed = 92 + k;
                cv.NoiseAmt = 0.08f;
                int bone = Col.Rgb(206, 192, 160);
                cv.Ball(16, 13, 11, 10, bone);
                cv.Ball(16, 21, 7, 6, Col.Scale(bone, 0.9f));
                cv.Ball(11.5f, 14, 3.2f, 3.4f, Col.Rgb(20, 10, 8), false);
                cv.Ball(20.5f, 14, 3.2f, 3.4f, Col.Rgb(20, 10, 8), false);
                cv.Poly(new float[] { 16, 17, 14.5f, 21, 17.5f, 21 }, Col.Rgb(30, 16, 12));
                for (int t = 0; t < 5; t++) cv.Rect(11 + t * 2.2f, 23, 1.2f, 3, Col.Rgb(236, 226, 200));
                cv.Outline(Col.Rgb(16, 10, 8));
                var sk = cv.Done();
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        int p = sk.Px[y * 32 + x];
                        if ((p & Col.OPAQUE) != 0) Set(im, ox + x, oy + y, p & Col.RGB);
                    }
            }
            return im;
        }

        static Image RockWall()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 4, 5, 101);
                    float cell = Noise.Cell(x, y, S, 5, 102);
                    float crack = cell < 0.08f ? 0.45f : 1;
                    float ledge = (float)Math.Sin((y + n * 14) * 0.35f) * 0.12f;
                    int c = Col.Lerp(Col.Rgb(98, 82, 66), Col.Rgb(178, 152, 124), n);
                    c = Vary(c, (0.85f + ledge) * crack);
                    Set(im, x, y, c);
                }
            return im;
        }

        static void TinyText(Image im, string s, int x0, int y0, int col)
        {
            // 3x5 font for the few letters we need
            string[] g = { "E:111100110100111", "X:101101010101101", "I:111010010010111", "T:111010010010010" };
            int x = x0;
            foreach (char ch in s)
            {
                foreach (var def in g)
                    if (def[0] == ch)
                        for (int yy = 0; yy < 5; yy++)
                            for (int xx = 0; xx < 3; xx++)
                                if (def[2 + yy * 3 + xx] == '1') im.Px[(y0 + yy) * S + x + xx] = col | Col.OPAQUE | Col.EMISSIVE;
                x += 4;
            }
        }

        static Image ExitWall(bool on)
        {
            var im = TechWall(Col.Rgb(92, 96, 104), 111);
            // sign
            for (int y = 6; y < 17; y++)
                for (int x = 20; x < 44; x++)
                    im.Px[y * S + x] = (y == 6 || y == 16 || x == 20 || x == 43 ? Col.Rgb(40, 40, 44) : Col.Rgb(40, 6, 6) | Col.EMISSIVE) | Col.OPAQUE;
            TinyText(im, "EXIT", 24, 9, Col.Rgb(255, 50, 30));
            // switch plate
            for (int y = 22; y < 50; y++)
                for (int x = 22; x < 42; x++)
                {
                    int c = Col.Rgb(130, 130, 136);
                    if (x == 22 || y == 22) c = Col.Rgb(190, 190, 200);
                    if (x == 41 || y == 49) c = Col.Rgb(50, 50, 56);
                    im.Px[y * S + x] = c | Col.OPAQUE;
                }
            // slot
            for (int y = 26; y < 46; y++)
                for (int x = 30; x < 34; x++)
                    im.Px[y * S + x] = Col.Rgb(20, 20, 22) | Col.OPAQUE;
            // lever
            int ly = on ? 40 : 28;
            for (int y = ly; y < ly + 5; y++)
                for (int x = 27; x < 37; x++)
                    im.Px[y * S + x] = (y == ly ? Col.Rgb(240, 240, 240) : Col.Rgb(170, 170, 176)) | Col.OPAQUE;
            // status light
            int lc = on ? Col.Rgb(60, 255, 60) : Col.Rgb(255, 40, 30);
            for (int y = 52; y < 57; y++)
                for (int x = 29; x < 35; x++)
                    im.Px[y * S + x] = lc | Col.OPAQUE | Col.EMISSIVE;
            return im;
        }

        static Image DoorTex(int keyCol)
        {
            var im = new Image(S, S);
            int baseCol = Col.Rgb(104, 100, 96);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 3, 121);
                    int c = Vary(baseCol, 0.8f + n * 0.35f);
                    // panels: 2 columns x 3 rows
                    int px = x < 32 ? x - 4 : x - 36, py = (y - 4) % 20;
                    bool inPanel = px >= 0 && px < 24 && y >= 4 && y < 60 && py < 16;
                    if (inPanel)
                    {
                        c = Vary(baseCol, 0.72f + n * 0.3f);
                        if (px == 0 || py == 0) c = Vary(baseCol, 0.45f);
                        if (px == 23 || py == 15) c = Vary(baseCol, 1.3f);
                    }
                    // center seam
                    if (x == 31) c = Col.Rgb(28, 26, 24);
                    if (x == 32) c = Col.Rgb(150, 146, 140);
                    // bolts
                    if ((x == 2 || x == 61) && y % 8 == 4) c = Col.Rgb(200, 196, 180);
                    Set(im, x, y, c);
                }
            if (keyCol >= 0)
            {
                // colored light strip + key symbol plate
                for (int y = 26; y < 38; y++)
                    for (int x = 0; x < S; x++)
                    {
                        int c = Col.Rgb(30, 30, 34);
                        if (y > 27 && y < 36) c = Col.Scale(keyCol, 0.55f + 0.45f * (float)Math.Abs(Math.Sin(x * 0.2))) | Col.EMISSIVE;
                        im.Px[y * S + x] = c | Col.OPAQUE;
                    }
                for (int y = 27; y < 37; y++)
                    for (int x = 25; x < 39; x++)
                        im.Px[y * S + x] = (x == 25 || x == 38 || y == 27 || y == 36 ? Col.Rgb(20, 20, 22) : Col.Rgb(230, 230, 230) | Col.EMISSIVE) | Col.OPAQUE;
                // skull-ish key glyph
                for (int y = 29; y < 35; y++)
                    for (int x = 28; x < 36; x++)
                        if ((x - 31.5) * (x - 31.5) / 9 + (y - 31.5) * (y - 31.5) / 6 < 1)
                            im.Px[y * S + x] = keyCol | Col.OPAQUE | Col.EMISSIVE;
            }
            else
            {
                // hazard stripes at the bottom
                for (int y = 56; y < 64; y++)
                    for (int x = 0; x < S; x++)
                        im.Px[y * S + x] = (((x + y) / 4) % 2 == 0 ? Col.Rgb(210, 170, 30) : Col.Rgb(30, 28, 24)) | Col.OPAQUE;
            }
            return im;
        }

        /// <summary>A secret wall: the same wall, with three small claw scratches gouged into the middle of it.
        /// Only the scratches tell it apart, so you still have to be looking.</summary>
        static Image SecretWall(Image src)
        {
            var im = src.Clone();
            for (int i = 0; i < 3; i++)
                for (int y = 22; y < 42; y++)
                {
                    int x = 27 + i * 5 + (y - 22) / 4;
                    bool tip = y < 24 || y > 39;                                            // the marks taper off at the ends
                    Set(im, x, y, Col.Scale(Px(im, x, y), tip ? 0.6f : 0.32f));             // the groove
                    if (!tip) Set(im, x + 1, y, Col.Lerp(Px(im, x + 1, y), Col.Rgb(255, 246, 232), 0.45f));   // its lit edge
                }
            return im;
        }

        /// <summary>A wall panel with a light strip across the top, put either side of every door so doorways
        /// are easy to find in a dark room.</summary>
        static Image DoorLitWall(Image src)
        {
            var im = src.Clone();
            int housing = Col.Rgb(46, 46, 52), lamp = Col.Rgb(255, 238, 190), lampDim = Col.Rgb(150, 130, 90);
            for (int x = 6; x < S - 6; x++)
            {
                bool cap = x < 9 || x >= S - 9;
                for (int y = 4; y < 14; y++)
                {
                    int c;
                    if (y < 6 || y > 11 || cap) c = housing;                       // the fitting around the tube
                    else if (y == 6 || y == 11) c = lampDim | Col.EMISSIVE;
                    else c = lamp | Col.EMISSIVE;
                    Set(im, x, y, c);
                }
                // light spilling down the wall below the strip
                if (!cap)
                    for (int y = 14; y < 30; y++)
                    {
                        float f = 1 - (y - 14) / 16f;
                        int c = Px(im, x, y);
                        Set(im, x, y, Col.Lerp(c, lamp, f * 0.45f));
                    }
            }
            return im;
        }

        static Image JambTex()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int c = ((x + y) / 6) % 2 == 0 ? Col.Rgb(200, 160, 30) : Col.Rgb(34, 32, 30);
                    c = Vary(c, 0.8f + Noise.Fbm(x, y, S, 8, 2, 131) * 0.35f);
                    if (x < 6 || x > 57) c = Vary(Col.Rgb(80, 80, 86), x == 5 || x == 58 ? 0.5f : 1f);
                    Set(im, x, y, c);
                }
            return im;
        }

        // ---------------------------------------------------------------- flats

        static Image TileFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int tx = x / 16, ty = y / 16;
                    bool check = ((tx + ty) & 1) == 0;
                    int c = check ? Col.Rgb(96, 92, 84) : Col.Rgb(76, 72, 66);
                    c = Vary(c, 0.9f + Noise.Hashf(tx, ty, 141) * 0.2f);
                    float n = Noise.Fbm(x, y, S, 4, 4, 142);
                    c = Vary(c, 0.8f + n * 0.4f);
                    if (x % 16 == 0 || y % 16 == 0) c = Col.Rgb(42, 40, 36);
                    else if (x % 16 == 1 || y % 16 == 1) c = Vary(c, 1.15f);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image MetalFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 4, 3, 151);
                    int c = Vary(Col.Rgb(98, 100, 104), 0.8f + n * 0.3f);
                    // diamond plate bumps
                    int cx = x % 8, cy = y % 8;
                    bool alt = ((x / 8 + y / 8) & 1) == 0;
                    int dx = alt ? cx - cy : cx + cy - 7;
                    if (Math.Abs(dx) <= 1 && cx > 1 && cx < 7 && cy > 1 && cy < 7) c = Vary(c, dx < 0 ? 1.35f : 0.65f);
                    if (x % 32 == 0 || y % 32 == 0) c = Col.Rgb(40, 40, 44);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image GrateFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int c;
                    int gx = x % 8, gy = y % 8;
                    if (gx < 2 || gy < 2) c = Vary(Col.Rgb(110, 106, 96), gx == 0 || gy == 0 ? 1.2f : 0.9f);
                    else c = Col.Rgb(18, 16, 14);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image DirtFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 4, 5, 161);
                    float m = Noise.Fbm(x, y, S, 2, 3, 162);
                    int c = Col.Lerp(Col.Rgb(78, 60, 40), Col.Rgb(70, 78, 40), Math.Max(0, (m - 0.5f) * 3));
                    c = Vary(c, 0.7f + n * 0.6f);
                    if (Noise.Hashf(x, y, 163) > 0.96f) c = Col.Rgb(130, 120, 104);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image WoodFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int plank = y / 8;
                    int off = (plank * 23) % 64;
                    float n = Noise.Fbm(x * 0.5f, y * 4, S, 4, 3, 171 + plank);
                    float grain = (float)Math.Sin((y + n * 8) * 1.7f) * 0.5f + 0.5f;
                    float tone = 0.8f + Noise.Hashf(plank, (x + off) / 32, 172) * 0.35f;
                    int c = Col.Rgb((int)(110 * tone * (0.8f + grain * 0.25f)), (int)(70 * tone * (0.8f + grain * 0.25f)), (int)(40 * tone));
                    if (y % 8 == 0 || (x + off) % 32 == 0) c = Col.Rgb(34, 22, 14);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image HellFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float cell = Noise.Cell(x, y, S, 5, 181);
                    float n = Noise.Fbm(x, y, S, 4, 4, 182);
                    int c = Vary(Col.Rgb(70, 40, 34), 0.6f + n * 0.6f);
                    if (cell < 0.07f) c = Col.Lerp(Col.Rgb(255, 200, 60), Col.Rgb(200, 50, 10), cell / 0.07f) | Col.EMISSIVE;
                    else if (cell < 0.12f) c = Col.Rgb(30, 10, 8);
                    im.Px[y * S + x] = c | Col.OPAQUE;
                }
            return im;
        }

        static Image MarbleFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 4, 5, 191);
                    float v = (float)Math.Abs(Math.Sin((x * 0.05 + y * 0.1) * Math.PI * 2 + n * 8));
                    float vein = (float)Math.Pow(1 - v, 10);
                    int c = Col.Lerp(Col.Rgb(60, 56, 54), Col.Rgb(170, 160, 150), vein);
                    c = Vary(c, 0.85f + n * 0.3f);
                    if (x % 32 == 0 || y % 32 == 0) c = Col.Rgb(26, 24, 22);
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image Liquid(int mid, int hot, int crust, int seed)
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 4, 4, seed);
                    float cell = Noise.Cell(x, y, S, 4, seed + 1);
                    int c = Col.Lerp(mid, hot, Math.Max(0, (n - 0.45f) * 2.5f));
                    if (cell > 0.55f) c = Col.Lerp(c, crust, Math.Min(1, (cell - 0.55f) * 3));
                    im.Px[y * S + x] = c | Col.OPAQUE | Col.EMISSIVE;
                }
            return im;
        }

        static Image CeilPanel()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 3, 201);
                    int c = Vary(Col.Rgb(82, 84, 88), 0.8f + n * 0.3f);
                    int px = x % 32, py = y % 32;
                    if (px == 0 || py == 0) c = Col.Rgb(34, 34, 38);
                    else if (px == 1 || py == 1) c = Vary(c, 1.2f);
                    // one light panel per 64x64 block
                    if (x >= 38 && x < 58 && y >= 38 && y < 58)
                    {
                        if (x == 38 || y == 38 || x == 57 || y == 57) c = Col.Rgb(50, 50, 54);
                        else c = Col.Lerp(Col.Rgb(250, 250, 235), Col.Rgb(200, 205, 210), (float)Math.Abs(x - 47.5) / 10) | Col.EMISSIVE;
                    }
                    im.Px[y * S + x] = c | Col.OPAQUE;
                }
            return im;
        }

        static Image CeilStone()
        {
            var im = BlockWall(211, new[] { 32, 32 }, new[] { new[] { 32, 32 } }, new[] { 0, 16 }, 2, Col.Rgb(70, 66, 62), 20, Col.Rgb(26, 24, 22), 0.1f);
            return im;
        }

        static Image CeilWood()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int c;
                    int bx = x % 32;
                    if (bx < 8)
                    {
                        float n = Noise.Fbm(x * 4, y * 0.5f, S, 4, 2, 221);
                        c = Col.Rgb((int)(96 * (0.8f + n * 0.3f)), (int)(60 * (0.8f + n * 0.3f)), 32);
                        if (bx == 0) c = Col.Rgb(30, 18, 10);
                        if (bx == 7) c = Col.Rgb(50, 30, 16);
                    }
                    else
                    {
                        float n = Noise.Fbm(x, y, S, 8, 2, 222);
                        c = Vary(Col.Rgb(54, 40, 30), 0.8f + n * 0.3f);
                    }
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image FleshCeil()
        {
            var im = FleshWall();
            for (int i = 0; i < im.Px.Length; i++) im.Px[i] = Col.Scale(im.Px[i], 0.7f) | Col.OPAQUE;
            return im;
        }

        // ---------------------------------------------------------------- sky

        public const int SKY_W = 256, SKY_H = 100;

        static Image MakeSky(int top, int mid, int horizon, int seed)
        {
            var im = new Image(SKY_W, SKY_H);
            // mountain silhouette heights
            var hgt = new float[SKY_W];
            for (int x = 0; x < SKY_W; x++)
            {
                float n = Noise.Fbm(x, 0, SKY_W, 4, 5, seed) * 1.2f;
                float n2 = Noise.Fbm(x, 7, SKY_W, 16, 3, seed + 1);
                hgt[x] = SKY_H - 6 - n * 24 - n2 * 6;
            }
            var hgt2 = new float[SKY_W];
            for (int x = 0; x < SKY_W; x++)
                hgt2[x] = SKY_H - 4 - Noise.Fbm(x, 3, SKY_W, 8, 4, seed + 5) * 16;
            for (int y = 0; y < SKY_H; y++)
                for (int x = 0; x < SKY_W; x++)
                {
                    float t = (float)y / SKY_H;
                    int c = t < 0.6f ? Col.Lerp(top, mid, t / 0.6f) : Col.Lerp(mid, horizon, (t - 0.6f) / 0.4f);
                    // clouds
                    float cl = Noise.Fbm(x, y * 2.2f, SKY_W, 8, 5, seed + 2);
                    if (cl > 0.5f) c = Col.Lerp(c, Col.Scale(c, 0.45f), Math.Min(1, (cl - 0.5f) * 3));
                    float glow = Noise.Fbm(x, y * 2.0f, SKY_W, 4, 4, seed + 3);
                    if (glow > 0.62f && t > 0.3f) c = Col.Lerp(c, horizon, (glow - 0.62f) * 2);
                    // far mountains
                    if (y > hgt2[x]) c = Col.Lerp(c, Col.Rgb(40, 16, 18), 0.8f);
                    // near mountains with rim light
                    if (y > hgt[x])
                    {
                        float d = y - hgt[x];
                        c = d < 1.5f ? Col.Lerp(horizon, Col.Rgb(30, 10, 10), 0.4f) : Col.Rgb(22, 8, 10);
                        c = Col.Scale(c, 0.9f + Noise.Hashf(x, y, seed + 4) * 0.2f);
                    }
                    im.Px[y * SKY_W + x] = c | Col.OPAQUE | Col.EMISSIVE;
                }
            return im;
        }
    }
}
