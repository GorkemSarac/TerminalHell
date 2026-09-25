// TERMINAL HELL - tile map: walls, doors, secret push walls, floors, lighting and path finding.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    enum CellKind : byte { Empty = 0, Wall = 1, Door = 2, Push = 3 }
    enum FloorKind : byte { A = 0, B = 1, Outdoor = 2, Lava = 3, LavaOut = 4, Nukage = 5, NukageOut = 6, Bridge = 7, BridgeOut = 8, C = 9 }
    enum DoorState { Closed, Opening, Open, Closing }

    sealed class Door
    {
        public int X, Y;
        public int Key;          // 0 none, 1 red, 2 blue, 3 yellow, 4 boarding pass
        public bool Horizontal;  // true: the door plane runs along X (walls east & west), at y + 0.5
        public float Open;       // 0 closed .. 1 open
        public DoorState State;
        public float Timer;
        public int Texture;
        public bool IsExit;         // the level's exit: opening it starts the level-end sequence
        public bool SealBehind;     // once opened and closed again with the player past it, it locks forever
        public bool SealPositive;   // which side of the doorway counts as "past it" (see World.PastSeal)
        public bool HasOpened, Sealed;
        public bool Trap;           // a wall panel that slides open by itself when a weapon is picked up (see World.WeaponTraps)
    }

    sealed class PushWall
    {
        public int X, Y;          // current base cell
        public int DX, DY;        // push direction
        public float Offset;      // 0..1 progress into next cell
        public int Moved;         // cells moved so far
        public bool Active, Done;
        public int Texture;
        public bool Counted;
    }

    struct Spawn { public char C; public int X, Y; }

    /// <summary>A run of levels: its name in the menu and the words on the screen after its last level.</summary>
    sealed class EpisodeDef
    {
        public string Name, Blurb, EndTitle;
        public string[] Ending;
    }

    sealed class LevelDef
    {
        public string Id, Name, Intro;
        public string[] Map;
        public int Episode;                 // index into Levels.Episodes (set by Levels.All)
        public int FloorA = Tex.F_TILE, CeilA = Tex.C_PANEL, FloorB = Tex.F_METAL, CeilB = Tex.C_STONE, FloorOut = Tex.F_DIRT;
        public int FloorC = Tex.F_METAL, CeilC = Tex.C_PANEL;   // a third indoor look ('-' on the map): the safety room
        public int Sky;                     // 0 red hell sky, 1 night, 2 blue earth sky
        public int Ambient = Col.Rgb(60, 58, 62);
        public int SkyLight = Col.Rgb(190, 150, 130);
        public int FogColor = 0;
        public float FogDensity = 0.09f;
        public int Music;
        public int MusicAfter = -1;         // music once the level's incident has happened (the airport)
        public bool Unarmed;                // the run starts with bare fists and nothing else
        public bool Chime;                  // calm airport announcements until the incident
        public int ExitTexture = -1;        // -1: the usual hazard-striped exit door; otherwise a level-specific look
        public int[][] WallOverrides;       // {x, y, texture} triples, restyling specific wall cells after the map loads
        public string Par = "";
    }

    sealed class Map
    {
        public int W, H;
        public CellKind[] Kind;
        public byte[] WallTex;
        public FloorKind[] Floor;
        public short[] DoorIdx;
        public bool[] Seen;
        public float[] WallHeight;
        public List<Door> Doors = new List<Door>();
        public List<PushWall> PushWalls = new List<PushWall>();
        public List<Spawn> Spawns = new List<Spawn>();
        public LevelDef Def;
        public int SecretCount;

        // lighting
        public const int LS = 4;           // light samples per cell
        public int LW, LH;
        public float[] LR, LG, LB;

        // path finding (distance from player, in cells)
        public short[] Flow;
        public int FlowFromX = -1, FlowFromY = -1;

        public struct Light { public float X, Y, R, I; public int C; }
        public List<Light> Lights = new List<Light>();

        public static float TileHeight(int tex)
        {
            if (tex == Tex.ROCK) return 2.0f;
            return 1.0f;
        }

        public static int WallTexFor(char c)
        {
            switch (c)
            {
                case '1': return Tex.STONE;
                case '2': return Tex.BRICK;
                case '3': return Tex.TECH;
                case '4': return Tex.WOOD;
                case '5': return Tex.MARBLE;
                case '6': return Tex.FLESH;
                case '7': return Tex.COMPUTER;
                case '8': return Tex.RUST;
                case '9': return Tex.SKULLS;
                case '0': return Tex.ROCK;
                case 'A': return Tex.TERM;
                case 'F': return Tex.GLASS;
                case 'H': return Tex.BOARD;
                case 'I': return Tex.SCORCH;
                case 'J': return Tex.SHATTER;
                case 'M': return Tex.DEADBOARD;
                case 'O': return Tex.SAFE;
                case 'P': return Tex.SAFECROSS;
                case 'V': return Tex.CONCRETE;
                case 'Z': return Tex.HANGAR;
            }
            return 0;
        }

        static int FloorFor(char c)
        {
            switch (c)
            {
                case '.': return (int)FloorKind.A;
                case '_': return (int)FloorKind.B;
                case '-': return (int)FloorKind.C;
                case ',': return (int)FloorKind.Outdoor;
                case '~': return (int)FloorKind.Lava;
                case ':': return (int)FloorKind.LavaOut;
                case '=': return (int)FloorKind.Nukage;
                case ';': return (int)FloorKind.NukageOut;
                case '#': return (int)FloorKind.Bridge;   // metal walkway over lava or slime
            }
            return -1;
        }

        public bool In(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }

        public static Map Load(LevelDef def)
        {
            var m = new Map();
            m.Def = def;
            string[] rows = def.Map;
            m.H = rows.Length;
            m.W = 0;
            foreach (var r in rows) m.W = Math.Max(m.W, r.Length);
            int n = m.W * m.H;
            m.Kind = new CellKind[n];
            m.WallTex = new byte[n];
            m.Floor = new FloorKind[n];
            m.DoorIdx = new short[n];
            m.Seen = new bool[n];
            m.WallHeight = new float[n];
            var floorKnown = new bool[n];
            var pushCells = new List<int>();
            for (int i = 0; i < n; i++) m.DoorIdx[i] = -1;

            for (int y = 0; y < m.H; y++)
            {
                string row = rows[y];
                for (int x = 0; x < m.W; x++)
                {
                    char c = x < row.Length ? row[x] : ' ';
                    int i = y * m.W + x;
                    int wt = WallTexFor(c);
                    int fl = FloorFor(c);
                    if (c == ' ') wt = Tex.STONE;
                    if (wt > 0)
                    {
                        m.Kind[i] = CellKind.Wall;
                        m.WallTex[i] = (byte)wt;
                        m.WallHeight[i] = TileHeight(wt);
                    }
                    else if (c == 'D' || c == 'R' || c == 'B' || c == 'Y' || c == '}' || c == 'X')
                    {
                        var d = new Door();
                        d.X = x; d.Y = y;
                        d.Key = c == 'R' ? 1 : c == 'B' ? 2 : c == 'Y' ? 3 : c == '}' ? 4 : 0;
                        d.IsExit = c == 'X';
                        // the boarding gate seals shut behind you once you've walked through it
                        if (c == '}') { d.SealBehind = true; d.SealPositive = false; }
                        d.Texture = d.Key == 1 ? Tex.DOOR_RED : d.Key == 2 ? Tex.DOOR_BLUE : d.Key == 3 ? Tex.DOOR_YELLOW : d.Key == 4 ? Tex.DOOR_PASS
                            : d.IsExit ? (def.ExitTexture >= 0 ? def.ExitTexture : Tex.DOOR_EXIT) : Tex.DOOR;
                        m.Kind[i] = CellKind.Door;
                        m.DoorIdx[i] = (short)m.Doors.Count;
                        m.WallHeight[i] = 1;
                        m.Doors.Add(d);
                    }
                    else if (c == '$')
                    {
                        m.Kind[i] = CellKind.Push;
                        m.WallHeight[i] = 1;
                        pushCells.Add(i);
                    }
                    else if (fl >= 0)
                    {
                        m.Floor[i] = (FloorKind)fl;
                        floorKnown[i] = true;
                    }
                    else
                    {
                        // a thing standing on the level's default floor (resolved below)
                        m.Spawns.Add(new Spawn { C = c, X = x, Y = y });
                    }
                }
            }

            // things inherit the most common neighbouring floor type
            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                foreach (var s in m.Spawns)
                {
                    int i = s.Y * m.W + s.X;
                    if (floorKnown[i]) continue;
                    var count = new int[12];
                    int best = -1, bestN = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        int nx = s.X + DX8[k], ny = s.Y + DY8[k];
                        if (!m.In(nx, ny)) continue;
                        int j = ny * m.W + nx;
                        if (!floorKnown[j]) continue;
                        int f = (int)m.Floor[j];
                        // liquids don't spread onto things
                        if (f == 3 || f == 5) f = 0; else if (f == 4 || f == 6) f = 2;
                        count[f]++;
                        if (count[f] > bestN) { bestN = count[f]; best = f; }
                    }
                    if (best >= 0) { m.Floor[i] = (FloorKind)best; floorKnown[i] = true; changed = true; }
                }
                if (!changed) break;
            }

            // doors: orientation from neighbouring walls
            foreach (var d in m.Doors)
            {
                bool we = m.IsWallTile(d.X - 1, d.Y) && m.IsWallTile(d.X + 1, d.Y);
                d.Horizontal = we;
                // a door's floor follows its neighbours
                int i = d.Y * m.W + d.X;
                int a = d.Horizontal ? (d.Y - 1) * m.W + d.X : d.Y * m.W + d.X - 1;
                if (a < 0 || a >= m.Floor.Length) a = i;   // the door sits on the map's own edge: nothing on that side
                m.Floor[i] = m.Floor[a] == FloorKind.Outdoor ? FloorKind.Outdoor : FloorKind.A;

                // a lit panel on the wall either side of the door, so doorways are easy to find
                m.LightPanel(d.Horizontal ? d.X - 1 : d.X, d.Horizontal ? d.Y : d.Y - 1);
                m.LightPanel(d.Horizontal ? d.X + 1 : d.X, d.Horizontal ? d.Y : d.Y + 1);
                // and light spilling into the rooms on both sides, in the colour of the key a locked door wants
                int lit = d.Key == 1 ? Col.Rgb(255, 90, 60) : d.Key == 2 ? Col.Rgb(100, 150, 255) : d.Key == 3 ? Col.Rgb(255, 210, 90) : d.Key == 4 ? Col.Rgb(90, 230, 210) : Col.Rgb(255, 226, 180);
                int ax = d.Horizontal ? 0 : 1, ay = d.Horizontal ? 1 : 0;   // the way you walk through it
                for (int s = -1; s <= 1; s += 2)
                {
                    int nx = d.X + ax * s, ny = d.Y + ay * s;
                    if (m.In(nx, ny) && m.Kind[ny * m.W + nx] == CellKind.Empty)
                        m.AddLight(d.X + 0.5f + ax * s * 0.6f, d.Y + 0.5f + ay * s * 0.6f, 3.4f, d.Key == 0 ? 0.5f : 0.6f, lit);
                }
            }

            // push walls take the texture of their neighbours
            foreach (int i in pushCells)
            {
                int x = i % m.W, y = i / m.W;
                var count = new Dictionary<int, int>();
                int best = Tex.STONE, bestN = 0;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + DX8[k * 2], ny = y + DY8[k * 2];
                    if (!m.In(nx, ny)) continue;
                    int j = ny * m.W + nx;
                    if (m.Kind[j] != CellKind.Wall) continue;
                    int t = m.WallTex[j];
                    if (t == Tex.EXIT_OFF) continue;
                    int c;
                    count.TryGetValue(t, out c);
                    count[t] = ++c;
                    if (c > bestN) { bestN = c; best = t; }
                }
                var p = new PushWall();
                p.X = x; p.Y = y; p.Texture = Tex.SecretOf(best);   // marked: warmer, with the seams of a sliding panel
                m.WallTex[i] = (byte)p.Texture;
                m.PushWalls.Add(p);
                m.DoorIdx[i] = (short)(m.PushWalls.Count - 1);
                m.SecretCount++;
                // floor under a push wall follows its open neighbours
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + DX8[k * 2], ny = y + DY8[k * 2];
                    if (m.In(nx, ny) && m.Kind[ny * m.W + nx] == CellKind.Empty) { m.Floor[i] = m.Floor[ny * m.W + nx]; break; }
                }
            }

            // a walkway over open-air lava keeps the sky above it
            for (int i = 0; i < n; i++)
            {
                if (m.Floor[i] != FloorKind.Bridge) continue;
                int x = i % m.W, y = i / m.W;
                for (int k = 0; k < 8; k += 2)
                {
                    int nx = x + DX8[k], ny = y + DY8[k];
                    if (m.In(nx, ny) && IsOutdoorFloor(m.Floor[ny * m.W + nx])) { m.Floor[i] = FloorKind.BridgeOut; break; }
                }
            }

            // restyle specific wall cells (a level-authored illusion, a special exit, whatever else needs one cell changed)
            if (def.WallOverrides != null)
                foreach (var o in def.WallOverrides)
                {
                    if (!m.In(o[0], o[1])) continue;
                    int oi = o[1] * m.W + o[0];
                    if (m.Kind[oi] == CellKind.Wall) m.WallTex[oi] = (byte)o[2];
                }

            m.Flow = new short[n];
            return m;
        }

        public static readonly int[] DX8 = { 1, 1, 0, -1, -1, -1, 0, 1 };
        public static readonly int[] DY8 = { 0, 1, 1, 1, 0, -1, -1, -1 };

        /// <summary>Turns a plain wall into the version with a light strip (used beside doors).</summary>
        void LightPanel(int x, int y)
        {
            if (!In(x, y)) return;
            int i = y * W + x;
            if (Kind[i] != CellKind.Wall) return;
            WallTex[i] = (byte)Tex.DoorLitOf(WallTex[i]);
        }

        /// <summary>Turns plain wall cells into sliding panels that look like the wall around them until something opens them.
        /// Orientation is worked out with the whole set still counting as wall, so a row of panels lines up as one wall.</summary>
        public void AddTrapDoors(List<int[]> cells)
        {
            var set = new HashSet<int>();
            foreach (var c in cells) set.Add(c[1] * W + c[0]);
            Func<int, int, bool> wallish = (x, y) => IsWallTile(x, y) || (In(x, y) && set.Contains(y * W + x));
            var made = new List<Door>();
            foreach (var c in cells)
            {
                int i = c[1] * W + c[0];
                if (Kind[i] != CellKind.Wall) continue;
                var d = new Door();
                d.X = c[0]; d.Y = c[1];
                d.Trap = true;
                d.Horizontal = wallish(c[0] - 1, c[1]) && wallish(c[0] + 1, c[1]);
                d.Texture = WallTex[i];
                made.Add(d);
            }
            foreach (var d in made)
            {
                int i = d.Y * W + d.X;
                Kind[i] = CellKind.Door;
                DoorIdx[i] = (short)Doors.Count;
                WallHeight[i] = 1;
                Doors.Add(d);
                // the floor under a panel follows whichever open neighbour is not a hazard
                for (int k = 0; k < 4; k++)
                {
                    int nx = d.X + DX8[k * 2], ny = d.Y + DY8[k * 2];
                    if (!In(nx, ny)) continue;
                    int j = ny * W + nx;
                    if (Kind[j] == CellKind.Empty && !HurtFloor(j)) { Floor[i] = Floor[j]; break; }
                }
            }
        }

        public bool IsWallTile(int x, int y)
        {
            if (!In(x, y)) return true;
            var k = Kind[y * W + x];
            return k == CellKind.Wall || k == CellKind.Push;
        }

        public CellKind KindAt(int x, int y) { return In(x, y) ? Kind[y * W + x] : CellKind.Wall; }

        public Door DoorAt(int x, int y)
        {
            if (!In(x, y)) return null;
            int i = y * W + x;
            return Kind[i] == CellKind.Door ? Doors[DoorIdx[i]] : null;
        }

        public PushWall PushAt(int x, int y)
        {
            if (!In(x, y)) return null;
            int i = y * W + x;
            return Kind[i] == CellKind.Push && DoorIdx[i] >= 0 ? PushWalls[DoorIdx[i]] : null;
        }

        public bool IsOutdoor(int x, int y)
        {
            return In(x, y) && IsOutdoorFloor(Floor[y * W + x]);
        }

        /// <summary>Floors with open sky above them.</summary>
        public static bool IsOutdoorFloor(FloorKind f)
        {
            return f == FloorKind.Outdoor || f == FloorKind.LavaOut || f == FloorKind.NukageOut || f == FloorKind.BridgeOut;
        }

        /// <summary>Solid for movement of players and monsters (doors block until almost open).</summary>
        public bool BlocksMove(int x, int y)
        {
            if (!In(x, y)) return true;
            int i = y * W + x;
            switch (Kind[i])
            {
                case CellKind.Empty: return false;
                case CellKind.Door: return Doors[DoorIdx[i]].Open < 0.85f;
                default: return true;
            }
        }

        public bool BlocksSight(int x, int y)
        {
            if (!In(x, y)) return true;
            int i = y * W + x;
            switch (Kind[i])
            {
                case CellKind.Empty: return false;
                case CellKind.Door: return Doors[DoorIdx[i]].Open < 0.6f;
                default: return true;
            }
        }

        /// <summary>Line of sight test through the grid.</summary>
        public bool LOS(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return true;
            int steps = (int)(len * 6) + 1;
            float sx = dx / steps, sy = dy / steps;
            float x = x0, y = y0;
            int lastX = -9999, lastY = -9999;
            for (int i = 0; i < steps; i++)
            {
                x += sx; y += sy;
                int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
                if (cx == lastX && cy == lastY) continue;
                lastX = cx; lastY = cy;
                if (cx == (int)Math.Floor(x1) && cy == (int)Math.Floor(y1)) return true;
                if (BlocksSight(cx, cy)) return false;
            }
            return true;
        }

        /// <summary>Distance along a ray to the first wall, closed door part or push wall (for hitscan).</summary>
        public float RayCast(float px, float py, float rdx, float rdy, float maxDist)
        {
            int mx = (int)Math.Floor(px), my = (int)Math.Floor(py);
            float ddx = rdx == 0 ? 1e30f : Math.Abs(1 / rdx), ddy = rdy == 0 ? 1e30f : Math.Abs(1 / rdy);
            int sx, sy;
            float sdx, sdy;
            if (rdx < 0) { sx = -1; sdx = (px - mx) * ddx; } else { sx = 1; sdx = (mx + 1 - px) * ddx; }
            if (rdy < 0) { sy = -1; sdy = (py - my) * ddy; } else { sy = 1; sdy = (my + 1 - py) * ddy; }
            for (int i = 0; i < 128; i++)
            {
                float t;
                if (sdx < sdy) { t = sdx; sdx += ddx; mx += sx; } else { t = sdy; sdy += ddy; my += sy; }
                if (t > maxDist) return maxDist;
                if (!In(mx, my)) return t;
                int ci = my * W + mx;
                var k = Kind[ci];
                if (k == CellKind.Empty) continue;
                if (k == CellKind.Door)
                {
                    float th, u;
                    if (Renderer.DoorHit(Doors[DoorIdx[ci]], mx, my, px, py, rdx, rdy, out th, out u)) return Math.Min(th, maxDist);
                    continue;
                }
                return t;
            }
            return maxDist;
        }

        // ------------------------------------------------------------ lighting

        public void AddLight(float x, float y, float radius, float intensity, int color)
        {
            Lights.Add(new Light { X = x, Y = y, R = radius, I = intensity, C = color });
        }

        bool LightLOS(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            int steps = (int)(len * 5) + 1;
            for (int i = 1; i < steps; i++)
            {
                float t = (float)i / steps;
                int cx = (int)(x0 + dx * t), cy = (int)(y0 + dy * t);
                if (!In(cx, cy)) return false;
                var k = Kind[cy * W + cx];
                if (k == CellKind.Wall || k == CellKind.Push) return false;
            }
            return true;
        }

        public void BuildLightmap()
        {
            // automatic light sources from lava / nukage / computer walls
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    var f = Floor[i];
                    if (Kind[i] == CellKind.Empty && (f == FloorKind.Lava || f == FloorKind.LavaOut))
                        AddLight(x + 0.5f, y + 0.5f, 2.6f, 0.5f, Col.Rgb(255, 110, 30));
                    else if (Kind[i] == CellKind.Empty && (f == FloorKind.Nukage || f == FloorKind.NukageOut))
                        AddLight(x + 0.5f, y + 0.5f, 2.4f, 0.4f, Col.Rgb(80, 255, 60));
                    else if (Kind[i] == CellKind.Wall && (WallTex[i] == Tex.COMPUTER || WallTex[i] == Tex.EXIT_OFF))
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + DX8[k * 2], ny = y + DY8[k * 2];
                            if (In(nx, ny) && Kind[ny * W + nx] == CellKind.Empty)
                                AddLight(x + 0.5f + DX8[k * 2] * 0.62f, y + 0.5f + DY8[k * 2] * 0.62f, 2.2f, 0.28f,
                                    WallTex[i] == Tex.COMPUTER ? Col.Rgb(90, 255, 140) : Col.Rgb(255, 60, 40));
                        }
                    }
                }

            LW = W * LS; LH = H * LS;
            int n = LW * LH;
            LR = new float[n]; LG = new float[n]; LB = new float[n];
            var open = new bool[n];
            float ar = Col.R(Def.Ambient) / 255f, ag = Col.G(Def.Ambient) / 255f, ab = Col.B(Def.Ambient) / 255f;
            float sr = Col.R(Def.SkyLight) / 255f, sg = Col.G(Def.SkyLight) / 255f, sb = Col.B(Def.SkyLight) / 255f;

            // sky exposure: fraction of outdoor cells around each sample (soft edges)
            for (int sy = 0; sy < LH; sy++)
                for (int sx = 0; sx < LW; sx++)
                {
                    float wx = (sx + 0.5f) / LS, wy = (sy + 0.5f) / LS;
                    int cx = (int)wx, cy = (int)wy;
                    int i = sy * LW + sx;
                    var k = Kind[cy * W + cx];
                    open[i] = k != CellKind.Wall && k != CellKind.Push;
                    float sky = 0, tot = 0;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = (int)(wx + ox * 0.5f), ny = (int)(wy + oy * 0.5f);
                            if (!In(nx, ny)) continue;
                            var nk = Kind[ny * W + nx];
                            if (nk == CellKind.Wall || nk == CellKind.Push) continue;
                            tot++;
                            if (IsOutdoor(nx, ny)) sky++;
                        }
                    float s = tot > 0 ? sky / tot : 0;
                    LR[i] = ar + sr * s; LG[i] = ag + sg * s; LB[i] = ab + sb * s;
                }

            foreach (var L in Lights)
            {
                float lr = Col.R(L.C) / 255f * L.I, lg = Col.G(L.C) / 255f * L.I, lb = Col.B(L.C) / 255f * L.I;
                int x0 = Math.Max(0, (int)((L.X - L.R) * LS)), x1 = Math.Min(LW - 1, (int)((L.X + L.R) * LS));
                int y0 = Math.Max(0, (int)((L.Y - L.R) * LS)), y1 = Math.Min(LH - 1, (int)((L.Y + L.R) * LS));
                for (int sy = y0; sy <= y1; sy++)
                    for (int sx = x0; sx <= x1; sx++)
                    {
                        int i = sy * LW + sx;
                        if (!open[i]) continue;
                        float wx = (sx + 0.5f) / LS, wy = (sy + 0.5f) / LS;
                        float d = (float)Math.Sqrt((wx - L.X) * (wx - L.X) + (wy - L.Y) * (wy - L.Y));
                        if (d >= L.R) continue;
                        if (!LightLOS(L.X, L.Y, wx, wy)) continue;
                        float f = 1 - d / L.R;
                        f *= f;
                        LR[i] += lr * f; LG[i] += lg * f; LB[i] += lb * f;
                    }
            }

            // blur open samples a little and fill solid samples from their open neighbours
            for (int pass = 0; pass < 2; pass++)
            {
                var nr = (float[])LR.Clone(); var ng = (float[])LG.Clone(); var nb = (float[])LB.Clone();
                for (int sy = 0; sy < LH; sy++)
                    for (int sx = 0; sx < LW; sx++)
                    {
                        int i = sy * LW + sx;
                        float r = 0, g = 0, b = 0, c = 0;
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                int x = sx + ox, y = sy + oy;
                                if (x < 0 || y < 0 || x >= LW || y >= LH) continue;
                                int j = y * LW + x;
                                if (!open[j]) continue;
                                float w = (ox == 0 && oy == 0) ? 2 : 1;
                                r += LR[j] * w; g += LG[j] * w; b += LB[j] * w; c += w;
                            }
                        if (c > 0) { nr[i] = r / c; ng[i] = g / c; nb[i] = b / c; }
                    }
                LR = nr; LG = ng; LB = nb;
            }
            for (int i = 0; i < n; i++)
            {
                if (LR[i] > 1.7f) LR[i] = 1.7f;
                if (LG[i] > 1.7f) LG[i] = 1.7f;
                if (LB[i] > 1.7f) LB[i] = 1.7f;
            }
        }

        /// <summary>Bilinear lightmap lookup at a world position.</summary>
        public void SampleLight(float x, float y, out float r, out float g, out float b)
        {
            float fx = x * LS - 0.5f, fy = y * LS - 0.5f;
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            float tx = fx - ix, ty = fy - iy;
            if (ix < 0) { ix = 0; tx = 0; } else if (ix >= LW - 1) { ix = LW - 2; tx = 1; }
            if (iy < 0) { iy = 0; ty = 0; } else if (iy >= LH - 1) { iy = LH - 2; ty = 1; }
            int i = iy * LW + ix;
            float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
            r = LR[i] * w00 + LR[i + 1] * w10 + LR[i + LW] * w01 + LR[i + LW + 1] * w11;
            g = LG[i] * w00 + LG[i + 1] * w10 + LG[i + LW] * w01 + LG[i + LW + 1] * w11;
            b = LB[i] * w00 + LB[i + 1] * w10 + LB[i + LW] * w01 + LB[i + LW + 1] * w11;
        }

        // ------------------------------------------------------------ path finding

        readonly Queue<int> bfs = new Queue<int>();

        /// <summary>Breadth-first distance field from the player's cell (monsters walk downhill).</summary>
        public void BuildFlow(int px, int py)
        {
            FlowFromX = px; FlowFromY = py;
            for (int i = 0; i < Flow.Length; i++) Flow[i] = short.MaxValue;
            if (!In(px, py)) return;
            bfs.Clear();
            int s = py * W + px;
            Flow[s] = 0;
            bfs.Enqueue(s);
            while (bfs.Count > 0)
            {
                int c = bfs.Dequeue();
                int cx = c % W, cy = c / W;
                short d = (short)(Flow[c] + 1);
                if (d > 60) continue;
                for (int k = 0; k < 8; k += 2)
                {
                    int nx = cx + DX8[k], ny = cy + DY8[k];
                    if (!In(nx, ny)) continue;
                    int j = ny * W + nx;
                    if (Flow[j] <= d) continue;
                    if (!MonsterPassable(j)) continue;
                    Flow[j] = d;
                    bfs.Enqueue(j);
                }
            }
        }

        /// <summary>Lava and slime: they hurt the player and monsters refuse to walk into them.</summary>
        public bool HurtFloor(int i)
        {
            var f = Floor[i];
            return f == FloorKind.Lava || f == FloorKind.LavaOut || f == FloorKind.Nukage || f == FloorKind.NukageOut;
        }

        public bool HurtFloorAt(int x, int y) { return In(x, y) && Kind[y * W + x] == CellKind.Empty && HurtFloor(y * W + x); }

        public bool MonsterPassable(int i)
        {
            switch (Kind[i])
            {
                case CellKind.Empty: return !HurtFloor(i);
                case CellKind.Door:
                    var d = Doors[DoorIdx[i]];
                    return d.Key == 0 || d.Open > 0.85f;
                default: return false;
            }
        }
    }
}
