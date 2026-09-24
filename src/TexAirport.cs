// TERMINAL HELL - textures for the earth episode: the airport terminal, its wrecked twin, the safety room, the apron and a blue sky.
using System;

namespace TerminalHell
{
    static partial class Tex
    {
        static void BuildAirport()
        {
            Walls[TERM] = TermWall();
            Walls[GLASS] = WindowWall(false);
            Walls[BOARD] = BoardWall(false);
            Walls[SCORCH] = ScorchWall();
            Walls[SHATTER] = WindowWall(true);
            Walls[DEADBOARD] = BoardWall(true);
            Walls[SAFE] = SafeWall(false);
            Walls[SAFECROSS] = SafeWall(true);
            Walls[CONCRETE] = ConcreteWall();
            Walls[HANGAR] = HangarWall();
            Walls[RUNWAY_END] = RunwayEndWall();
            Walls[DOOR_PASS] = DoorTex(Col.Rgb(50, 225, 200));
            for (int t = TERM; t <= HANGAR; t++)
            {
                Walls[SecretOf(t)] = SecretWall(Walls[t]);
                Walls[DoorLitOf(t)] = DoorLitWall(Walls[t]);
            }

            Flats[F_AIR] = TerminalFloor(false);
            Flats[F_WRECK] = TerminalFloor(true);
            Flats[F_SAFE] = SafeFloor();
            Flats[F_APRON] = ApronFloor();
            Flats[F_RUNWAY] = RunwayFloor();
            Flats[C_AIR] = TerminalCeiling(false);
            Flats[C_WRECK] = TerminalCeiling(true);
            Flats[C_SAFE] = SafeCeiling();

            SkyEarth = MakeEarthSky();
            SkyOvercast = MakeOvercastSky();
            SkyTower = MakeTowerSky();
        }

        static float Smooth01(float a, float b, float x)
        {
            float t = (x - a) / (b - a);
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t * t * (3 - 2 * t);
        }

        // ---------------------------------------------------------------- walls

        /// <summary>Painted terminal wall: cream plaster, a blue stripe at eye height and a dark skirting board.</summary>
        static Image TermWall()
        {
            var im = new Image(S, S);
            int plaster = Col.Rgb(204, 198, 182), stripe = Col.Rgb(58, 92, 146), skirt = Col.Rgb(64, 66, 74);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 3, 301);
                    int c = Vary(plaster, 0.92f + n * 0.12f);
                    // panel seams
                    if (x % 32 == 0) c = Vary(c, 0.86f);
                    else if (x % 32 == 1) c = Vary(c, 1.05f);
                    // scuffs near the bottom, drifting streaks from the top
                    float streak = Noise.Fbm(x * 4, y * 0.4f, S, 4, 2, 302);
                    if (streak > 0.62f) c = Vary(c, 1 - (streak - 0.62f) * 1.4f);
                    if (y > 44 && Noise.Hashf(x / 2, y / 2, 303) > 0.9f) c = Vary(c, 0.82f);
                    if (y >= 38 && y < 44)
                    {
                        c = Vary(stripe, 0.9f + n * 0.2f);
                        if (y == 38) c = Vary(stripe, 1.35f);
                        if (y == 43) c = Vary(stripe, 0.6f);
                    }
                    if (y >= 55)
                    {
                        c = Vary(skirt, 0.85f + n * 0.3f);
                        if (y == 55) c = Col.Rgb(150, 150, 156);
                    }
                    Set(im, x, y, c);
                }
            return im;
        }

        /// <summary>A tall window onto the apron: a parked jet and a baggage truck in daylight (or, broken, a burning sky).</summary>
        static Image WindowWall(bool broken)
        {
            var im = new Image(S, S);
            int horizon = 36;
            int frame = Col.Rgb(58, 62, 72);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int c;
                    bool inFrame = x < 3 || x >= S - 3 || y < 3 || y >= S - 3 || x == 31 || x == 32;
                    if (inFrame)
                    {
                        c = Vary(frame, 0.85f + Noise.Hashf(x, y, 311) * 0.3f);
                        if (x == 0 || y == 0 || x == 31) c = Vary(frame, 1.4f);
                        Set(im, x, y, c);
                        continue;
                    }
                    if (!broken)
                    {
                        if (y < horizon)
                        {
                            float t = (float)y / horizon;
                            c = Col.Lerp(Col.Rgb(92, 150, 228), Col.Rgb(205, 228, 248), t);
                            float cl = Noise.Fbm(x * 2, y * 3, S, 8, 4, 312);
                            if (cl > 0.55f) c = Col.Lerp(c, Col.Rgb(250, 252, 255), Math.Min(1, (cl - 0.55f) * 4));
                        }
                        else
                        {
                            float t = (float)(y - horizon) / (S - horizon);
                            c = Col.Lerp(Col.Rgb(128, 130, 132), Col.Rgb(84, 86, 90), t);
                            c = Vary(c, 0.94f + Noise.Hashf(x, y, 313) * 0.12f);
                            if (y == 46 && (x / 5) % 2 == 0) c = Col.Rgb(226, 190, 60);     // taxiway line
                            if (y == horizon) c = Col.Rgb(170, 176, 180);
                        }
                    }
                    else
                    {
                        // the sky over the apron has gone the colour of a furnace
                        if (y < horizon)
                        {
                            float t = (float)y / horizon;
                            c = Col.Lerp(Col.Rgb(34, 10, 8), Col.Rgb(220, 84, 24), t * t);
                            float cl = Noise.Fbm(x * 2, y * 3, S, 8, 4, 314);
                            if (cl > 0.5f) c = Col.Lerp(c, Col.Rgb(20, 8, 8), Math.Min(1, (cl - 0.5f) * 3));
                        }
                        else
                        {
                            float t = (float)(y - horizon) / (S - horizon);
                            c = Col.Lerp(Col.Rgb(90, 34, 16), Col.Rgb(30, 18, 16), t);
                            c = Vary(c, 0.9f + Noise.Hashf(x, y, 315) * 0.25f);
                        }
                    }
                    Set(im, x, y, c | Col.EMISSIVE);
                }

            if (!broken)
            {
                // a parked jet: fuselage, cockpit, tail fin and a wing, in the left pane
                for (int y = 22; y < 36; y++)
                    for (int x = 4; x < 31; x++)
                    {
                        float ex = (x - 17.5f) / 13.5f, ey = (y - 30f) / 4.4f;
                        bool body = ex * ex + ey * ey <= 1 && y >= 26 && y <= 34;
                        bool fin = x >= 4 && x <= 10 && y >= 20 + (10 - x) / 2 && y <= 28 && x + (28 - y) * 0.6f >= 5 && x - (28 - y) * 0.35f <= 10;
                        bool wing = y >= 33 && y <= 35 && x >= 13 && x <= 25 && (x - 13) + (y - 33) * 3 <= 12;
                        if (body || fin || wing)
                        {
                            int col = y > 31 ? Col.Rgb(196, 202, 210) : Col.Rgb(244, 246, 250);
                            if (fin) col = y < 24 ? Col.Rgb(210, 40, 40) : Col.Rgb(236, 238, 244);
                            if (body && y == 29 && x > 9 && x < 27) col = Col.Rgb(46, 98, 172);     // cheatline
                            if (body && y == 28 && x > 12 && x < 27 && x % 3 == 0) col = Col.Rgb(50, 60, 84);   // windows
                            if (body && x >= 26 && y <= 30 && y >= 28) col = Col.Rgb(40, 50, 70);   // cockpit
                            Set(im, x, y, col | Col.EMISSIVE);
                        }
                    }
                // a baggage tug on the right
                for (int y = 32; y < 38; y++)
                    for (int x = 42; x < 56; x++)
                    {
                        int col = y < 35 ? Col.Rgb(236, 190, 40) : Col.Rgb(60, 60, 64);
                        if (y == 32 && x > 50) col = Col.Rgb(40, 60, 80);
                        Set(im, x, y, col | Col.EMISSIVE);
                    }
                // daylight glinting across the glass
                for (int y = 3; y < S - 3; y++)
                    for (int x = 3; x < S - 3; x++)
                    {
                        if (x == 31 || x == 32) continue;
                        int d = (x + y * 2) % 28;
                        if (d < 3) Set(im, x, y, Col.Lerp(im.Px[y * S + x], Col.Rgb(255, 255, 255), 0.16f) | Col.EMISSIVE);
                    }
            }
            else
            {
                // smoke pillars and fires on the apron, a shattered pane and the sharp edges of what is left
                for (int k = 0; k < 3; k++)
                {
                    int bx = 10 + k * 20 + (k == 1 ? 6 : 0);
                    for (int y = 6; y < horizon; y++)
                        for (int x = bx - 3 - (horizon - y) / 8; x <= bx + 3 + (horizon - y) / 8; x++)
                            if (x > 3 && x < S - 3 && x != 31 && x != 32 && Noise.Hashf(x, y, 316 + k) > 0.25f)
                                Set(im, x, y, Col.Scale(Col.Rgb(30, 22, 20), 0.6f + Noise.Hashf(x, y, 320) * 0.6f) | Col.EMISSIVE);
                    for (int y = horizon - 4; y < horizon + 3; y++)
                        for (int x = bx - 4; x <= bx + 4; x++)
                            if (x > 3 && x < S - 3 && x != 31 && x != 32 && Noise.Hashf(x, y, 330 + k) > 0.3f)
                                Set(im, x, y, Col.Lerp(Col.Rgb(255, 210, 80), Col.Rgb(230, 60, 12), (float)(y - (horizon - 4)) / 7f) | Col.EMISSIVE);
                }
                // cracks radiating from an impact
                float cx = 46, cy = 22;
                for (int k = 0; k < 9; k++)
                {
                    double a = k * Math.PI * 2 / 9 + 0.3;
                    for (int r = 0; r < 20 + (k % 3) * 6; r++)
                    {
                        int px = (int)(cx + Math.Cos(a + Math.Sin(r * 0.4) * 0.12) * r), py = (int)(cy + Math.Sin(a + Math.Sin(r * 0.4) * 0.12) * r);
                        if (px > 3 && px < S - 3 && py > 3 && py < S - 3 && px != 31 && px != 32) Set(im, px, py, Col.Rgb(236, 226, 214) | Col.EMISSIVE);
                    }
                }
                // the hole: black and jagged
                for (int y = 16; y < 30; y++)
                    for (int x = 40; x < 53; x++)
                    {
                        float dx = (x - cx) / 6.5f, dy = (y - cy) / 6f;
                        if (dx * dx + dy * dy < 1 - Noise.Hashf(x, y, 340) * 0.5f) Set(im, x, y, Col.Rgb(12, 6, 6));
                    }
            }
            return im;
        }

        /// <summary>The departures board: a header, and rows of flights that are all on time - or not, when it is dead.</summary>
        static Image BoardWall(bool dead)
        {
            var im = TermWall();
            int screen = dead ? Col.Rgb(8, 10, 12) : Col.Rgb(10, 18, 44);
            // bezel and screen
            for (int y = 4; y < 60; y++)
                for (int x = 2; x < 62; x++)
                {
                    bool bezel = x < 4 || x >= 60 || y < 6 || y >= 58;
                    im.Px[y * S + x] = (bezel ? Col.Rgb(34, 36, 42) : screen) | Col.OPAQUE;
                }
            if (!dead)
            {
                for (int y = 6; y < 16; y++)
                    for (int x = 4; x < 60; x++) im.Px[y * S + x] = (Col.Rgb(230, 176, 30) | Col.EMISSIVE) | Col.OPAQUE;
                BoardText(im, "FLIGHTS", 11, 8, Col.Rgb(20, 20, 30));
                for (int r = 0; r < 5; r++)
                {
                    int y = 19 + r * 8;
                    int code = 3 + (int)(Noise.Hashf(r, 1, 350) * 3), dest = 4 + (int)(Noise.Hashf(r, 2, 350) * 4);
                    BoardRun(im, 5, y, code, Col.Rgb(240, 240, 250), r);
                    BoardRun(im, 22, y, dest, Col.Rgb(240, 200, 60), r + 9);
                    BoardRun(im, 45, y, 3, r == 3 ? Col.Rgb(255, 150, 40) : Col.Rgb(90, 240, 120), r + 20);
                }
            }
            else
            {
                // dead: a few stuck lines, static, a crack across the glass
                for (int y = 6; y < 58; y++)
                    for (int x = 4; x < 60; x++)
                        if ((y / 3) % 7 == 0 && Noise.Hashf(x, y, 355) > 0.5f)
                            im.Px[y * S + x] = (Col.Scale(Col.Rgb(160, 30, 24), 0.4f + Noise.Hashf(x, y, 356)) | Col.EMISSIVE) | Col.OPAQUE;
                BoardRun(im, 5, 22, 4, Col.Rgb(90, 96, 110), 31);
                BoardRun(im, 5, 46, 6, Col.Rgb(90, 96, 110), 32);
                BoardText(im, "HELL", 7, 33, Col.Rgb(200, 24, 18));
                for (int t = 0; t < 70; t++)
                {
                    int x = 8 + t * 4 / 3, y = 8 + t * 5 / 6 + (int)(Math.Sin(t * 0.5) * 2);
                    if (x < 60 && y < 58) im.Px[y * S + x] = Col.Rgb(206, 200, 190) | Col.OPAQUE;
                }
            }
            return im;
        }

        static void BoardRun(Image im, int x0, int y, int letters, int col, int seed)
        {
            // rows of tiny pseudo-text: bars of a pixel with gaps, like lines of small type
            int x = x0;
            for (int k = 0; k < letters; k++)
            {
                int w = 2 + (int)(Noise.Hashf(k, seed, 351) * 2);
                for (int xx = 0; xx < w && x + xx < 60; xx++)
                {
                    im.Px[(y + 1) * S + x + xx] = (col | Col.EMISSIVE) | Col.OPAQUE;
                    im.Px[(y + 2) * S + x + xx] = (col | Col.EMISSIVE) | Col.OPAQUE;
                }
                x += w + 1;
            }
        }

        static void BoardText(Image im, string s, int x0, int y0, int col)
        {
            for (int i = 0; i < s.Length; i++)
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                        if (PixFont.Pixel(s[i], gx, gy))
                        {
                            int px = x0 + i * 6 + gx, py = y0 + gy;
                            if (px < S && py < S) im.Px[py * S + px] = (col | Col.EMISSIVE) | Col.OPAQUE;
                        }
        }

        /// <summary>The terminal wall after the incident: soot climbing from the floor, cracks, a patch of bare brick, smears of blood.</summary>
        static Image ScorchWall()
        {
            var im = TermWall();
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 6, 4, 361);
                    float soot = Math.Min(1, Math.Max(0, (float)y / S * 1.1f + (n - 0.5f) * 0.9f));
                    int c = Px(im, x, y);
                    c = Col.Lerp(c, Col.Rgb(18, 14, 12), soot * 0.82f);
                    // a patch of plaster blown off, bare brick underneath
                    if (x >= 6 && x < 30 && y >= 26 && y < 54)
                    {
                        float edge = Noise.Fbm(x * 2, y * 2, S, 8, 3, 362);
                        if (edge > 0.42f)
                        {
                            bool mortar = (y % 6) == 0 || ((x + ((y / 6) & 1) * 6) % 12) == 0;
                            c = mortar ? Col.Rgb(46, 40, 36) : Vary(Col.Rgb(120, 52, 38), 0.6f + Noise.Hashf(x / 4, y / 6, 363) * 0.5f);
                        }
                    }
                    Set(im, x, y, c);
                }
            // cracks
            for (int k = 0; k < 4; k++)
            {
                float fx = 8 + k * 16 + Noise.Hashf(k, 0, 364) * 6, fy = 2 + Noise.Hashf(k, 1, 364) * 8;
                for (int i = 0; i < 46; i++)
                {
                    fx += (Noise.Hashf(i, k, 365) - 0.5f) * 2.4f;
                    fy += 0.9f;
                    Set(im, (int)fx, (int)fy, Col.Rgb(20, 16, 14));
                }
            }
            // blood: drips down from a smear
            for (int k = 0; k < 3; k++)
            {
                int bx = 34 + k * 9 + (int)(Noise.Hashf(k, 5, 366) * 4);
                int len = 12 + (int)(Noise.Hashf(k, 6, 366) * 22);
                for (int y = 20; y < 20 + len && y < S; y++)
                    Set(im, bx, y, Vary(Col.Rgb(118, 12, 12), 0.7f + Noise.Hashf(bx, y, 367) * 0.5f));
                for (int x = bx - 2; x <= bx + 2; x++) Set(im, x, 20, Vary(Col.Rgb(130, 14, 12), 0.8f));
            }
            return im;
        }

        /// <summary>The safety room: white tile, a green band, and (optionally) a first-aid cross.</summary>
        static Image SafeWall(bool cross)
        {
            var im = new Image(S, S);
            int white = Col.Rgb(232, 238, 234), grout = Col.Rgb(160, 184, 168), green = Col.Rgb(30, 150, 84);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 3, 371);
                    int c = Vary(white, 0.94f + n * 0.1f);
                    if (x % 16 == 0 || y % 16 == 0) c = grout;
                    else if (x % 16 == 1 || y % 16 == 1) c = Vary(c, 1.06f);
                    if (y >= 28 && y < 36)
                    {
                        c = Vary(green, 0.9f + n * 0.2f);
                        if (y == 28) c = Vary(green, 1.3f);
                        if (y == 35) c = Vary(green, 0.6f);
                    }
                    if (y >= 60) c = Vary(Col.Rgb(70, 88, 78), 0.9f);
                    Set(im, x, y, c);
                }
            if (cross)
            {
                // a white plate with a big green cross
                for (int y = 6; y < 58; y++)
                    for (int x = 12; x < 52; x++)
                    {
                        bool border = x < 14 || x >= 50 || y < 8 || y >= 56;
                        Set(im, x, y, border ? Col.Rgb(30, 110, 70) : Col.Rgb(246, 250, 246));
                    }
                for (int y = 12; y < 52; y++)
                    for (int x = 26; x < 38; x++) Set(im, x, y, (Col.Rgb(28, 170, 92) | Col.EMISSIVE));
                for (int y = 26; y < 38; y++)
                    for (int x = 16; x < 48; x++) Set(im, x, y, (Col.Rgb(28, 170, 92) | Col.EMISSIVE));
            }
            return im;
        }

        static Image ConcreteWall()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 4, 381);
                    int c = Vary(Col.Rgb(158, 158, 152), 0.82f + n * 0.3f);
                    if (y % 32 == 0) c = Col.Rgb(74, 74, 72);
                    else if (y % 32 == 1) c = Vary(c, 1.15f);
                    if (x % 32 == 0) c = Vary(c, 0.7f);
                    // tie holes
                    int hx = x % 32, hy = y % 32;
                    if ((hx == 8 || hx == 24) && (hy == 8 || hy == 24)) c = Col.Rgb(50, 50, 50);
                    float stain = Noise.Fbm(x * 3, y * 0.5f, S, 4, 3, 382);
                    if (stain > 0.58f) c = Vary(c, 1 - (stain - 0.58f) * 1.6f);
                    Set(im, x, y, c);
                }
            return im;
        }

        /// <summary>Corrugated metal cladding of a hangar.</summary>
        static Image HangarWall()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float ridge = 0.5f + 0.5f * (float)Math.Sin(x * Math.PI * 2 / 8);
                    float n = Noise.Fbm(x, y, S, 8, 3, 391);
                    int c = Vary(Col.Rgb(150, 162, 176), 0.66f + ridge * 0.5f + (n - 0.5f) * 0.2f);
                    float rust = Noise.Fbm(x * 6, y * 0.6f, S, 4, 3, 392);
                    if (rust > 0.6f) c = Col.Lerp(c, Col.Rgb(140, 84, 50), Math.Min(1, (rust - 0.6f) * 3) * 0.7f);
                    if (y >= 30 && y <= 33) c = Vary(c, y == 31 ? 1.25f : 0.7f);        // rail
                    if (y >= 58) c = Vary(c, 0.55f);
                    Set(im, x, y, c);
                }
            return im;
        }

        /// <summary>Not a real opening: a flat painted illusion of the runway carrying on into the haze,
        /// with a rockslide making sure nobody tries to find out.</summary>
        static Image RunwayEndWall()
        {
            var im = new Image(S, S);
            int horizon = 30;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int c;
                    if (y < horizon)
                    {
                        float t = (float)y / horizon;
                        c = Col.Lerp(Col.Rgb(150, 152, 156), Col.Rgb(190, 192, 194), t);
                        float haze = Noise.Fbm(x, y * 2, S, 6, 4, 611);
                        if (haze > 0.55f) c = Col.Lerp(c, Col.Rgb(205, 206, 208), Math.Min(1, (haze - 0.55f) * 3));
                    }
                    else
                    {
                        // the tarmac converging to a vanishing point, fading into fog the further away it gets
                        float fromHorizon = (y - horizon) / (float)(S - horizon);
                        float half = (0.5f - fromHorizon * 0.46f);
                        float u = (x / (float)S - 0.5f);
                        bool onRoad = Math.Abs(u) < half;
                        c = Col.Rgb(58, 58, 62);
                        if (onRoad)
                        {
                            float n = Noise.Fbm(x, y, S, 8, 3, 612);
                            c = Vary(Col.Rgb(56, 56, 60), 0.8f + n * 0.3f);
                            float laneW = half * 0.14f;
                            if (Math.Abs(u) < laneW && ((int)(fromHorizon * 26) % 2) == 0) c = Vary(Col.Rgb(205, 204, 194), 0.8f + n * 0.2f);
                        }
                        float fog = Math.Min(1, fromHorizon < 0.4f ? (0.4f - fromHorizon) * 2.4f : 0);
                        c = Col.Lerp(c, Col.Rgb(150, 152, 156), fog);
                    }
                    Set(im, x, y, c | Col.EMISSIVE);
                }
            // the rockslide: a heap of broken concrete piled across the bottom, closing it off for good
            for (int x = 0; x < S; x++)
            {
                float top = S - 14 - Noise.Fbm(x, 0, S, 4, 4, 613) * 10;
                for (int y = (int)top; y < S; y++)
                {
                    float shade = 0.6f + Noise.Fbm(x, y, S, 6, 3, 614) * 0.5f;
                    int rc = Vary(Col.Rgb(120, 116, 108), shade);
                    if (Noise.Hashf(x, y, 615) > 0.93f) rc = Vary(rc, 1.3f);
                    im.Px[y * S + x] = rc | Col.OPAQUE;
                }
            }
            return im;
        }

        // ---------------------------------------------------------------- floors and ceilings

        /// <summary>Polished terminal floor in big cream tiles. The wrecked version is scorched, cracked and littered.</summary>
        static Image TerminalFloor(bool wreck)
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int tx = x / 32, ty = y / 32;
                    int c = Col.Rgb(166, 162, 152);
                    c = Vary(c, 0.93f + Noise.Hashf(tx, ty, 401) * 0.12f);
                    float n = Noise.Fbm(x, y, S, 4, 4, 402);
                    c = Vary(c, 0.9f + n * 0.2f);
                    // a darker inlay band round each tile, and the grout
                    int lx = x % 32, ly = y % 32;
                    if (lx == 0 || ly == 0) c = Col.Rgb(118, 114, 106);
                    else if (lx == 1 || ly == 1) c = Vary(c, 1.1f);
                    else if (lx == 5 || ly == 5 || lx == 26 || ly == 26) c = Vary(c, 0.86f);
                    // polish: a soft diagonal sheen
                    float sheen = (float)Math.Abs(Math.Sin((x + y) * 0.098));
                    c = Vary(c, 1 + (sheen > 0.94f ? 0.05f : 0));
                    if (wreck)
                    {
                        float soot = Noise.Fbm(x, y, S, 4, 4, 403);
                        c = Col.Lerp(c, Col.Rgb(34, 28, 26), Math.Min(0.5f, Math.Max(0, (soot - 0.3f) * 1.3f)));
                        // blood, in clots
                        float bl = Noise.Fbm(x * 2, y * 2, S, 6, 3, 404);
                        if (bl > 0.7f) c = Col.Lerp(c, Col.Rgb(96, 14, 12), Math.Min(0.8f, (bl - 0.7f) * 4));
                        // litter: paper, glass
                        float lit = Noise.Hashf(x, y, 405);
                        if (lit > 0.997f) c = Col.Rgb(190, 190, 182);
                        else if (lit < 0.002f) c = Col.Rgb(130, 160, 176);
                    }
                    Set(im, x, y, c);
                }
            if (wreck)
            {
                // cracks running across the tiles
                for (int k = 0; k < 3; k++)
                {
                    float fx = Noise.Hashf(k, 0, 406) * S, fy = Noise.Hashf(k, 1, 406) * S;
                    double a = Noise.Hashf(k, 2, 406) * Math.PI * 2;
                    for (int i = 0; i < 40; i++)
                    {
                        a += (Noise.Hashf(i, k, 407) - 0.5f) * 0.7;
                        fx += (float)Math.Cos(a); fy += (float)Math.Sin(a);
                        Set(im, (int)Math.Floor(fx), (int)Math.Floor(fy), Col.Rgb(58, 50, 46));
                    }
                }
            }
            return im;
        }

        static Image SafeFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int tx = x / 16, ty = y / 16;
                    int c = ((tx + ty) & 1) == 0 ? Col.Rgb(226, 234, 228) : Col.Rgb(176, 210, 190);
                    c = Vary(c, 0.94f + Noise.Fbm(x, y, S, 8, 3, 411) * 0.14f);
                    if (x % 16 == 0 || y % 16 == 0) c = Col.Rgb(120, 150, 134);
                    else if (x % 16 == 1 || y % 16 == 1) c = Vary(c, 1.08f);
                    Set(im, x, y, c);
                }
            return im;
        }

        /// <summary>The apron: expansion-jointed concrete, oil stains and a worn yellow guide line.</summary>
        static Image ApronFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 4, 421);
                    int c = Vary(Col.Rgb(132, 132, 128), 0.8f + n * 0.35f);
                    if (x % 32 == 0 || y % 32 == 0) c = Col.Rgb(56, 56, 56);
                    else if (x % 32 == 1 || y % 32 == 1) c = Vary(c, 1.1f);
                    float oil = Noise.Fbm(x * 2, y * 2, S, 4, 3, 422);
                    if (oil > 0.6f) c = Col.Lerp(c, Col.Rgb(44, 44, 48), Math.Min(1, (oil - 0.6f) * 4) * 0.7f);
                    if (Noise.Hashf(x, y, 423) > 0.985f) c = Vary(c, 1.25f);
                    Set(im, x, y, c);
                }
            return im;
        }

        /// <summary>The runway itself: worn dark asphalt, tar patches, tire-skid smudges and a dashed white
        /// centerline running through it - distinct from the plain grey apron near the terminal.</summary>
        static Image RunwayFloor()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise.Fbm(x, y, S, 8, 4, 601);
                    int c = Vary(Col.Rgb(54, 54, 58), 0.78f + n * 0.32f);
                    // tar patch seams, coarser and darker than the apron's
                    float patch = Noise.Fbm(x * 1.4f, y * 1.4f, S, 5, 4, 602);
                    if (patch > 0.58f) c = Col.Lerp(c, Col.Rgb(30, 30, 33), Math.Min(1, (patch - 0.58f) * 2.6f) * 0.75f);
                    // rubber skid streaks, angled
                    float skid = Noise.Fbm(x * 0.6f + y * 1.8f, y * 0.5f, S, 4, 3, 603);
                    if (skid > 0.64f) c = Col.Lerp(c, Col.Rgb(20, 20, 22), Math.Min(1, (skid - 0.64f) * 3f) * 0.6f);
                    if (Noise.Hashf(x, y, 604) > 0.99f) c = Vary(c, 1.3f);
                    // the dashed white centerline
                    if (y >= 29 && y < 35 && (x % 20) < 12)
                    {
                        int line = Col.Rgb(210, 208, 196);
                        if (y == 29 || y == 34) line = Vary(line, 0.55f);
                        c = Vary(line, 0.85f + n * 0.2f);
                    }
                    Set(im, x, y, c);
                }
            return im;
        }

        /// <summary>Acoustic ceiling tiles in a grid, with fluorescent panels. Wrecked: tiles down, pipes and wiring showing, no lights.</summary>
        static Image TerminalCeiling(bool wreck)
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int lx = x % 32, ly = y % 32;
                    float n = Noise.Fbm(x, y, S, 8, 3, 431);
                    int c = Vary(Col.Rgb(178, 178, 170), 0.88f + n * 0.16f);
                    if (Noise.Hashf(x / 2, y / 2, 432) > 0.86f) c = Vary(c, 0.86f);          // perforations
                    if (lx == 0 || ly == 0) c = Col.Rgb(90, 92, 98);
                    else if (lx == 1 || ly == 1) c = Vary(c, 1.1f);
                    // one lamp panel in every other tile
                    bool lampTile = ((x / 32) + (y / 32)) % 2 == 0;
                    if (lampTile && lx >= 6 && lx < 26 && ly >= 9 && ly < 23)
                    {
                        bool edge = lx == 6 || lx == 25 || ly == 9 || ly == 22;
                        c = edge ? Col.Rgb(90, 92, 98) : (Col.Lerp(Col.Rgb(255, 252, 238), Col.Rgb(214, 224, 232), (float)Math.Abs(lx - 15.5) / 10) | Col.EMISSIVE);
                    }
                    if (wreck)
                    {
                        float hole = Noise.Fbm(x, y, S, 3, 3, 433);
                        if (hole > 0.58f && !(lx == 0 || ly == 0))
                        {
                            // a tile has come down: darkness above, a pipe and a hanging cable
                            c = Vary(Col.Rgb(22, 22, 26), 0.7f + Noise.Hashf(x, y, 434) * 0.6f);
                            if (y % 12 >= 5 && y % 12 < 8) c = Vary(Col.Rgb(120, 122, 128), 0.6f + (float)Math.Sin((y % 12 - 5) * 1.0) * 0.5f);
                        }
                        else
                        {
                            float soot = Noise.Fbm(x, y, S, 4, 3, 435);
                            c = Col.Lerp(c & Col.RGB, Col.Rgb(30, 24, 22), Math.Min(0.7f, Math.Max(0, (soot - 0.3f) * 1.8f)));
                        }
                    }
                    Set(im, x, y, c);
                }
            return im;
        }

        static Image SafeCeiling()
        {
            var im = new Image(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int c = Vary(Col.Rgb(236, 240, 238), 0.94f + Noise.Fbm(x, y, S, 8, 3, 441) * 0.1f);
                    if (x % 32 == 0 || y % 32 == 0) c = Col.Rgb(150, 170, 158);
                    if (x >= 12 && x < 52 && y >= 26 && y < 38)
                    {
                        bool edge = x == 12 || x == 51 || y == 26 || y == 37;
                        c = edge ? Col.Rgb(70, 130, 96) : (Col.Rgb(240, 255, 244) | Col.EMISSIVE);
                    }
                    Set(im, x, y, c);
                }
            return im;
        }

        // ---------------------------------------------------------------- sky

        /// <summary>A clear blue day with clouds, low hills, a tree line and the airport on the horizon.</summary>
        static Image MakeEarthSky()
        {
            var im = new Image(SKY_W, SKY_H);
            var hills = new float[SKY_W];
            var trees = new float[SKY_W];
            for (int x = 0; x < SKY_W; x++)
            {
                hills[x] = SKY_H - 14 - Noise.Fbm(x, 0, SKY_W, 3, 4, 451) * 14 - Noise.Fbm(x, 5, SKY_W, 12, 3, 452) * 3;
                trees[x] = SKY_H - 8 - Noise.Fbm(x, 9, SKY_W, 24, 3, 453) * 4;
            }
            for (int y = 0; y < SKY_H; y++)
                for (int x = 0; x < SKY_W; x++)
                {
                    float t = (float)y / SKY_H;
                    int c = t < 0.62f ? Col.Lerp(Col.Rgb(30, 84, 196), Col.Rgb(104, 168, 240), t / 0.62f)
                                      : Col.Lerp(Col.Rgb(104, 168, 240), Col.Rgb(212, 232, 248), (t - 0.62f) / 0.38f);
                    // clouds: bright tops, a little shade underneath
                    float cl = Noise.Fbm(x, y * 2.4f, SKY_W, 6, 5, 454);
                    float cover = Smooth01(0.52f, 0.72f, cl);
                    if (cover > 0)
                    {
                        float below = Noise.Fbm(x, (y + 3) * 2.4f, SKY_W, 6, 5, 454);
                        int cloud = below < cl - 0.02f ? Col.Rgb(214, 224, 238) : Col.Rgb(252, 252, 255);
                        c = Col.Lerp(c, cloud, cover * (1 - t * 0.35f));
                    }
                    // the sun
                    float dx = x - 176f, dy = (y - 30f) * 1.3f;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d < 26) c = Col.Lerp(c, Col.Rgb(255, 250, 220), (1 - d / 26) * (1 - d / 26) * 0.9f);
                    // hills, and behind the tree line a little airport
                    if (y > hills[x]) c = Col.Lerp(Col.Rgb(150, 178, 196), Col.Rgb(126, 156, 176), (y - hills[x]) / 12);
                    im.Px[y * SKY_W + x] = c | Col.OPAQUE | Col.EMISSIVE;
                }
            // control tower, hangars and a departing jet with its trail
            Box(im, 58, SKY_H - 34, 4, 26, Col.Rgb(196, 200, 206));
            Box(im, 55, SKY_H - 40, 10, 7, Col.Rgb(90, 130, 170));
            Box(im, 54, SKY_H - 41, 12, 2, Col.Rgb(150, 154, 160));
            Box(im, 60, SKY_H - 46, 1, 5, Col.Rgb(150, 154, 160));
            for (int k = 0; k < 4; k++) Box(im, 92 + k * 22, SKY_H - 18, 18, 6, Col.Lerp(Col.Rgb(170, 176, 186), Col.Rgb(140, 148, 160), k / 3f));
            Box(im, 120, SKY_H - 22, 40, 4, Col.Rgb(176, 182, 190));
            for (int i = 0; i < 46; i++)
            {
                int x = 196 + i, y = 22 - i / 3;
                if (y < 4) break;
                float fade = 1 - i / 46f;
                int p = im.Px[y * SKY_W + x];
                im.Px[y * SKY_W + x] = (Col.Lerp(p, Col.Rgb(255, 255, 255), fade * 0.8f) | Col.OPAQUE | Col.EMISSIVE);
            }
            Box(im, 240, 8, 4, 1, Col.Rgb(240, 240, 250));
            Box(im, 241, 7, 2, 3, Col.Rgb(240, 240, 250));
            for (int x = 0; x < SKY_W; x++)
                for (int y = (int)trees[x]; y < SKY_H; y++)
                {
                    float g = 0.75f + Noise.Hashf(x, y, 455) * 0.5f;
                    im.Px[y * SKY_W + x] = (Col.Rgb((int)(50 * g), (int)(88 * g), (int)(58 * g)) | Col.OPAQUE | Col.EMISSIVE);
                }
            return im;
        }

        /// <summary>The same airport horizon, under a flat grey overcast: no sun, no blue, a low uniform ceiling of cloud.</summary>
        static Image MakeOvercastSky()
        {
            var im = new Image(SKY_W, SKY_H);
            var hills = new float[SKY_W];
            var trees = new float[SKY_W];
            for (int x = 0; x < SKY_W; x++)
            {
                hills[x] = SKY_H - 14 - Noise.Fbm(x, 0, SKY_W, 3, 4, 461) * 14 - Noise.Fbm(x, 5, SKY_W, 12, 3, 462) * 3;
                trees[x] = SKY_H - 8 - Noise.Fbm(x, 9, SKY_W, 24, 3, 463) * 4;
            }
            for (int y = 0; y < SKY_H; y++)
                for (int x = 0; x < SKY_W; x++)
                {
                    float t = (float)y / SKY_H;
                    // a dull grey ceiling, darker overhead, paler toward the horizon - never blue, never bright
                    int c = t < 0.62f ? Col.Lerp(Col.Rgb(96, 98, 102), Col.Rgb(140, 142, 146), t / 0.62f)
                                      : Col.Lerp(Col.Rgb(140, 142, 146), Col.Rgb(182, 182, 180), (t - 0.62f) / 0.38f);
                    // low, heavy cloud with soft edges instead of distinct puffs - it reads as one unbroken ceiling
                    float cl = Noise.Fbm(x, y * 1.6f, SKY_W, 5, 5, 464);
                    c = Col.Lerp(c, Col.Rgb(118, 120, 124), Math.Max(0, cl - 0.3f) * 1.1f);
                    float dark = Noise.Fbm(x * 2, y * 2.2f, SKY_W, 8, 4, 465);
                    if (dark > 0.6f) c = Col.Lerp(c, Col.Rgb(80, 82, 88), Math.Min(1, (dark - 0.6f) * 2f) * 0.5f);
                    // hills, muted with no daylight to catch
                    if (y > hills[x]) c = Col.Lerp(Col.Rgb(100, 104, 106), Col.Rgb(80, 84, 88), (y - hills[x]) / 12);
                    im.Px[y * SKY_W + x] = c | Col.OPAQUE | Col.EMISSIVE;
                }
            // the same control tower and hangars, flat and shadowless under the cloud
            Box(im, 58, SKY_H - 34, 4, 26, Col.Rgb(150, 152, 156));
            Box(im, 55, SKY_H - 40, 10, 7, Col.Rgb(76, 90, 100));
            Box(im, 54, SKY_H - 41, 12, 2, Col.Rgb(120, 122, 126));
            Box(im, 60, SKY_H - 46, 1, 5, Col.Rgb(120, 122, 126));
            for (int k = 0; k < 4; k++) Box(im, 92 + k * 22, SKY_H - 18, 18, 6, Col.Lerp(Col.Rgb(130, 132, 138), Col.Rgb(108, 112, 118), k / 3f));
            Box(im, 120, SKY_H - 22, 40, 4, Col.Rgb(134, 136, 140));
            for (int x = 0; x < SKY_W; x++)
                for (int y = (int)trees[x]; y < SKY_H; y++)
                {
                    float g = 0.6f + Noise.Hashf(x, y, 466) * 0.35f;
                    im.Px[y * SKY_W + x] = Col.Rgb((int)(46 * g), (int)(60 * g), (int)(48 * g)) | Col.OPAQUE | Col.EMISSIVE;
                }
            return im;
        }

        /// <summary>The control tower up close, through the hole in the ceiling: smoke, fire, broken glass, no sky left to speak of.</summary>
        static Image MakeTowerSky()
        {
            var im = new Image(SKY_W, SKY_H);
            for (int y = 0; y < SKY_H; y++)
                for (int x = 0; x < SKY_W; x++)
                {
                    float t = (float)y / SKY_H;
                    // a smoke-choked dusk, lit orange from below by whatever is burning
                    int c = t < 0.5f ? Col.Lerp(Col.Rgb(40, 32, 36), Col.Rgb(70, 56, 54), t / 0.5f)
                                      : Col.Lerp(Col.Rgb(70, 56, 54), Col.Rgb(120, 74, 48), (t - 0.5f) / 0.5f);
                    float smoke = Noise.Fbm(x, y * 1.8f, SKY_W, 6, 5, 471);
                    c = Col.Lerp(c, Col.Rgb(54, 46, 46), Math.Max(0, smoke - 0.25f) * 0.9f);
                    im.Px[y * SKY_W + x] = c | Col.OPAQUE | Col.EMISSIVE;
                }
            // the tower, filling most of the frame - this is "up close", not a skyline silhouette
            int tx0 = 78, tw = 96;
            Box(im, tx0, SKY_H - 92, tw, 92, Col.Rgb(74, 68, 66));
            Box(im, tx0 - 6, SKY_H - 30, tw + 12, 30, Col.Rgb(58, 54, 54));
            // the observation deck: a wider band near the top, ringed with windows
            Box(im, tx0 - 14, SKY_H - 92, tw + 28, 22, Col.Rgb(88, 80, 76));
            Box(im, tx0 - 16, SKY_H - 96, tw + 32, 5, Col.Rgb(96, 88, 82));
            for (int wx = tx0 - 10; wx < tx0 + tw + 10; wx += 6)
            {
                bool lit = ((wx * 7) & 3) != 0;
                Box(im, wx, SKY_H - 86, 3, 10, lit ? Col.Rgb(255, 140, 40) : Col.Rgb(30, 24, 22));
            }
            // body windows in ragged columns, mostly dark, some still burning
            for (int wy = SKY_H - 66; wy < SKY_H - 6; wy += 9)
                for (int wx = tx0 + 6; wx < tx0 + tw - 6; wx += 10)
                {
                    int h = (int)(Noise.Hashf(wx, wy, 472) * 7);
                    if (h < 3) Box(im, wx, wy, 4, 5, Col.Rgb(255, 120, 30));
                    else Box(im, wx, wy, 4, 5, Col.Rgb(36, 32, 32));
                }
            // smoke plumes rising off the top
            for (int x = 0; x < SKY_W; x++)
                for (int y = 0; y < SKY_H - 90; y++)
                {
                    float plume = Noise.Fbm(x * 0.7f, y * 2.2f + 40, SKY_W, 5, 4, 474);
                    float near = 1 - Math.Min(1, Math.Abs(x - (tx0 + tw / 2)) / 70f);
                    float amt = Math.Max(0, plume - 0.35f) * near * (1 - (float)y / (SKY_H - 90));
                    if (amt > 0) im.Px[y * SKY_W + x] = Col.Lerp(im.Px[y * SKY_W + x], Col.Rgb(60, 54, 52), Math.Min(1, amt * 1.6f)) | Col.OPAQUE | Col.EMISSIVE;
                }
            return im;
        }

        static void Box(Image im, int x0, int y0, int w, int h, int col)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && y >= 0 && x < im.W && y < im.H) im.Px[y * im.W + x] = col | Col.OPAQUE | Col.EMISSIVE;
        }
    }
}
