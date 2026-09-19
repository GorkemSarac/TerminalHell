// TERMINAL HELL - the raycasting renderer (walls, doors, push walls, floors, sky, sprites).
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    sealed class Camera
    {
        public float X, Y, Angle;
        public float Pitch;         // horizon shift as a fraction of the view height (-0.5..0.5)
        public float EyeZ = 0.5f;   // eye height (0 = floor, 1 = ceiling)
        public float Fov = 72;      // horizontal field of view in degrees
    }

    sealed class DynLight
    {
        public float X, Y, R, Cr, Cg, Cb;
    }

    sealed class SpriteInst
    {
        public float X, Y, Z;
        public Image Img;
        public float Scale = 1f / 64;   // world units per image pixel
        public float Flash;             // 0..1 white-hot damage flash
        public bool FullBright;
        public float Depth;
        public int Tag;                 // identifies the owner (for crosshair targeting)
    }

    sealed class Renderer
    {
        public int W, H;               // view size in pixels
        public float[] ZBuf = new float[0];
        float[] rowDist = new float[0];
        float[] rowFog = new float[0];
        public float Proj, ProjY, Horizon;
        public float Aspect = 1f;       // pixel width / pixel height
        public float DirX, DirY, PlaneX, PlaneY, PlaneLen;
        public int CenterTag;          // sprite tag under the crosshair
        public float Time;

        // per-frame state
        Map map;
        Camera cam;
        float eye;
        int fogR, fogG, fogB;
        float fogDensity;
        float extra;
        DynLight[] dl = new DynLight[16];
        int dlCount;

        public void Render(int[] pix, int pixW, int viewH, Map m, Camera c, List<SpriteInst> sprites, List<DynLight> lights, float extraLight)
        {
            W = pixW; H = viewH;
            map = m; cam = c;
            if (ZBuf.Length != W) ZBuf = new float[W];
            if (rowDist.Length != H) { rowDist = new float[H]; rowFog = new float[H]; }
            extra = extraLight;
            dlCount = 0;
            if (lights != null)
                foreach (var l in lights) { if (dlCount < dl.Length) dl[dlCount++] = l; }

            float a = c.Angle;
            DirX = (float)Math.Cos(a); DirY = (float)Math.Sin(a);
            PlaneLen = (float)Math.Tan(c.Fov * Math.PI / 360);
            PlaneX = -DirY * PlaneLen; PlaneY = DirX * PlaneLen;
            Proj = (W * 0.5f) / PlaneLen;
            ProjY = Proj * Aspect;
            Horizon = H * 0.5f + c.Pitch * H;
            eye = c.EyeZ;
            fogR = Col.R(m.Def.FogColor); fogG = Col.G(m.Def.FogColor); fogB = Col.B(m.Def.FogColor);
            fogDensity = m.Def.FogDensity;

            for (int y = 0; y < H; y++)
            {
                float dy = y + 0.5f - Horizon;
                float d;
                if (dy > 0.01f) d = eye * ProjY / dy;
                else if (dy < -0.01f) d = (1 - eye) * ProjY / -dy;
                else d = 1000;
                rowDist[y] = d;
                rowFog[y] = Fog(d);
            }

            for (int x = 0; x < W; x++) RenderColumn(pix, x);
            CenterTag = 0;
            RenderSprites(pix, sprites);
        }

        float Fog(float d)
        {
            return (float)Math.Exp(-d * fogDensity);
        }

        void Lighting(float wx, float wy, out float lr, out float lg, out float lb)
        {
            map.SampleLight(wx, wy, out lr, out lg, out lb);
            lr += extra; lg += extra * 0.85f; lb += extra * 0.6f;
            for (int i = 0; i < dlCount; i++)
            {
                var L = dl[i];
                float dx = wx - L.X, dy = wy - L.Y;
                float d2 = dx * dx + dy * dy, r2 = L.R * L.R;
                if (d2 >= r2) continue;
                float f = 1 - d2 / r2;
                f *= f;
                lr += L.Cr * f; lg += L.Cg * f; lb += L.Cb * f;
            }
        }

        static int ShadePix(int t, float lr, float lg, float lb, float fog, int fr, int fg, int fb)
        {
            if ((t & Col.EMISSIVE) != 0) { lr = lr < 1 ? 1 : lr; lg = lg < 1 ? 1 : lg; lb = lb < 1 ? 1 : lb; fog = fog * 0.5f + 0.5f; }
            float inv = 1 - fog;
            int r = (int)(((t >> 16) & 255) * lr * fog + fr * inv);
            int g = (int)(((t >> 8) & 255) * lg * fog + fg * inv);
            int b = (int)((t & 255) * lb * fog + fb * inv);
            if (r > 255) r = 255;
            if (g > 255) g = 255;
            if (b > 255) b = 255;
            return (r << 16) | (g << 8) | b;
        }

        void RenderColumn(int[] pix, int x)
        {
            float camX = 2 * (x + 0.5f) / W - 1;
            float rdx = DirX + PlaneX * camX, rdy = DirY + PlaneY * camX;
            float px = cam.X, py = cam.Y;
            int mx = (int)Math.Floor(px), my = (int)Math.Floor(py);
            float ddx = rdx == 0 ? 1e30f : Math.Abs(1 / rdx);
            float ddy = rdy == 0 ? 1e30f : Math.Abs(1 / rdy);
            int stepX, stepY;
            float sdx, sdy;
            if (rdx < 0) { stepX = -1; sdx = (px - mx) * ddx; } else { stepX = 1; sdx = (mx + 1 - px) * ddx; }
            if (rdy < 0) { stepY = -1; sdy = (py - my) * ddy; } else { stepY = 1; sdy = (my + 1 - py) * ddy; }

            int W_ = map.W;
            bool hit = false;
            float dist = 1000, u = 0;
            int tex = Tex.STONE, side = 0;
            float wallH = 1;
            int prevCell = my * W_ + mx;
            float tEnter = 0;
            bool firstFromOutdoor = false;
            int steps = 0;

            // a door in the starting cell (player standing in a doorway)
            if (map.In(mx, my) && map.Kind[prevCell] == CellKind.Door)
            {
                float t, uu;
                if (DoorHit(map.Doors[map.DoorIdx[prevCell]], mx, my, px, py, rdx, rdy, out t, out uu))
                {
                    hit = true; dist = t; u = uu; tex = map.Doors[map.DoorIdx[prevCell]].Texture; side = map.Doors[map.DoorIdx[prevCell]].Horizontal ? 1 : 0;
                }
            }

            while (!hit && steps++ < 96)
            {
                if (sdx < sdy) { tEnter = sdx; sdx += ddx; mx += stepX; side = 0; }
                else { tEnter = sdy; sdy += ddy; my += stepY; side = 1; }
                if (mx < 0 || my < 0 || mx >= W_ || my >= map.H) { dist = tEnter; break; }
                int ci = my * W_ + mx;
                map.Seen[ci] = true;
                var k = map.Kind[ci];
                if (k == CellKind.Empty) { prevCell = ci; continue; }
                if (k == CellKind.Wall)
                {
                    hit = true;
                    dist = tEnter;
                    tex = map.WallTex[ci];
                    wallH = map.WallHeight[ci];
                    if (map.Kind[prevCell] == CellKind.Door) { tex = Tex.JAMB; wallH = 1; }
                    float wx = side == 0 ? py + dist * rdy : px + dist * rdx;
                    u = wx - (float)Math.Floor(wx);
                    if (side == 0 && rdx < 0) u = 1 - u;
                    if (side == 1 && rdy > 0) u = 1 - u;
                    firstFromOutdoor = map.IsOutdoor(prevCell % W_, prevCell / W_);
                    break;
                }
                if (k == CellKind.Door)
                {
                    float t, uu;
                    var d = map.Doors[map.DoorIdx[ci]];
                    if (DoorHit(d, mx, my, px, py, rdx, rdy, out t, out uu))
                    {
                        hit = true; dist = t; u = uu; tex = d.Texture; side = d.Horizontal ? 1 : 0;
                        break;
                    }
                    prevCell = ci;
                    continue;
                }
                if (k == CellKind.Push)
                {
                    var p = map.PushWalls[map.DoorIdx[ci]];
                    if (!p.Active)
                    {
                        hit = true;
                        dist = tEnter;
                        tex = p.Texture;
                        float wx = side == 0 ? py + dist * rdy : px + dist * rdx;
                        u = wx - (float)Math.Floor(wx);
                        if (side == 0 && rdx < 0) u = 1 - u;
                        if (side == 1 && rdy > 0) u = 1 - u;
                        break;
                    }
                    float t, uu; int sd;
                    if (PushHit(p, mx, my, px, py, rdx, rdy, out t, out uu, out sd))
                    {
                        hit = true; dist = t; u = uu; tex = p.Texture; side = sd;
                        break;
                    }
                    prevCell = ci;
                    continue;
                }
            }

            if (dist < 0.02f) dist = 0.02f;
            ZBuf[x] = dist;

            // wall slice
            float lineH = ProjY / dist;
            float yTop = Horizon + (eye - wallH) * lineH;
            float yBot = Horizon + eye * lineH;
            int iTop = (int)Math.Ceiling(yTop - 0.5f), iBot = (int)Math.Ceiling(yBot - 0.5f);
            int drawTop = Math.Max(0, iTop), drawBot = Math.Min(H, iBot);

            if (hit)
            {
                // light the wall from the open side of the hit
                float hx = px + rdx * (dist - 0.04f), hy = py + rdy * (dist - 0.04f);
                float lr, lg, lb;
                Lighting(hx, hy, out lr, out lg, out lb);
                if (side == 1) { lr *= 0.8f; lg *= 0.8f; lb *= 0.8f; }
                float fog = Fog(dist);
                var img = Tex.Walls[tex];
                int tx = (int)(u * Tex.S) & Tex.Mask;
                float vStep = Tex.S / lineH;
                float v = (drawTop + 0.5f - yTop) * vStep;
                for (int y = drawTop; y < drawBot; y++)
                {
                    int ty = (int)v & Tex.Mask;
                    pix[y * W + x] = ShadePix(img.Px[ty * Tex.S + tx], lr, lg, lb, fog, fogR, fogG, fogB);
                    v += vStep;
                }
            }
            else
            {
                drawTop = drawBot = (int)Horizon;
                if (drawTop < 0) drawTop = drawBot = 0;
                if (drawTop > H) drawTop = drawBot = H;
            }

            // floor
            for (int y = Math.Max(drawBot, 0); y < H; y++)
            {
                float d = rowDist[y];
                if (y + 0.5f <= Horizon) continue;
                float wx = px + rdx * d, wy = py + rdy * d;
                int cx = (int)wx, cy = (int)wy;
                int t;
                FloorKind fk = map.In(cx, cy) ? map.Floor[cy * W_ + cx] : FloorKind.A;
                int tu = (int)(wx * Tex.S), tv = (int)(wy * Tex.S);
                switch (fk)
                {
                    case FloorKind.A: t = Tex.Flats[map.Def.FloorA].Px[(tv & Tex.Mask) * Tex.S + (tu & Tex.Mask)]; break;
                    case FloorKind.B: t = Tex.Flats[map.Def.FloorB].Px[(tv & Tex.Mask) * Tex.S + (tu & Tex.Mask)]; break;
                    case FloorKind.Outdoor: t = Tex.Flats[map.Def.FloorOut].Px[(tv & Tex.Mask) * Tex.S + (tu & Tex.Mask)]; break;
                    case FloorKind.Lava:
                    case FloorKind.LavaOut:
                        {
                            int o = (int)(Time * 9);
                            int wv = (int)(Math.Sin(wy * 3 + Time * 1.3) * 3);
                            t = Tex.Flats[Tex.F_LAVA].Px[((tv + o) & Tex.Mask) * Tex.S + ((tu + wv) & Tex.Mask)];
                            break;
                        }
                    default:
                        {
                            int o = (int)(Time * 12);
                            int wv = (int)(Math.Sin(wy * 4 + Time * 2) * 2);
                            t = Tex.Flats[Tex.F_NUKAGE].Px[((tv + wv) & Tex.Mask) * Tex.S + ((tu + o) & Tex.Mask)];
                            break;
                        }
                }
                float lr, lg, lb;
                Lighting(wx, wy, out lr, out lg, out lb);
                pix[y * W + x] = ShadePix(t, lr, lg, lb, rowFog[y], fogR, fogG, fogB);
            }

            // ceiling / sky
            int skyU = -1;
            int ceilEnd = Math.Min(drawTop, H);
            for (int y = 0; y < ceilEnd; y++)
            {
                float d = rowDist[y];
                if (y + 0.5f >= Horizon) break;
                float wx = px + rdx * d, wy = py + rdy * d;
                int cx = (int)wx, cy = (int)wy;
                FloorKind fk = map.In(cx, cy) ? map.Floor[cy * W_ + cx] : FloorKind.Outdoor;
                if (fk == FloorKind.Outdoor || fk == FloorKind.LavaOut || fk == FloorKind.NukageOut)
                {
                    if (skyU < 0) skyU = SkyU(rdx, rdy);
                    pix[y * W + x] = SkyPix(skyU, y);
                    continue;
                }
                int flat = fk == FloorKind.B ? map.Def.CeilB : map.Def.CeilA;
                int tu = (int)(wx * Tex.S), tv = (int)(wy * Tex.S);
                int t = Tex.Flats[flat].Px[(tv & Tex.Mask) * Tex.S + (tu & Tex.Mask)];
                float lr, lg, lb;
                Lighting(wx, wy, out lr, out lg, out lb);
                pix[y * W + x] = ShadePix(t, lr * 0.9f, lg * 0.9f, lb * 0.9f, rowFog[y], fogR, fogG, fogB);
            }

            // tall walls seen from outdoors: keep marching and draw anything that rises above what we drew
            if (hit && firstFromOutdoor && drawTop > 0)
            {
                float maxH = wallH;
                int clip = drawTop;
                int guard = 0;
                while (guard++ < 64 && clip > 0 && maxH < 2)
                {
                    float tE;
                    if (sdx < sdy) { tE = sdx; sdx += ddx; mx += stepX; side = 0; }
                    else { tE = sdy; sdy += ddy; my += stepY; side = 1; }
                    if (mx < 0 || my < 0 || mx >= W_ || my >= map.H) break;
                    int ci = my * W_ + mx;
                    if (map.Kind[ci] != CellKind.Wall) continue;
                    float h2 = map.WallHeight[ci];
                    if (h2 <= maxH) continue;
                    maxH = h2;
                    float lh = ProjY / tE;
                    float t2 = Horizon + (eye - h2) * lh;
                    int i2 = (int)Math.Ceiling(t2 - 0.5f);
                    int from = Math.Max(0, i2), to = Math.Min(clip, H);
                    if (from >= to) continue;
                    int t3 = map.WallTex[ci];
                    float wx = side == 0 ? py + tE * rdy : px + tE * rdx;
                    float uu = wx - (float)Math.Floor(wx);
                    if (side == 0 && rdx < 0) uu = 1 - uu;
                    if (side == 1 && rdy > 0) uu = 1 - uu;
                    float hx = px + rdx * (tE - 0.04f), hy = py + rdy * (tE - 0.04f);
                    float lr, lg, lb;
                    Lighting(hx, hy, out lr, out lg, out lb);
                    if (side == 1) { lr *= 0.8f; lg *= 0.8f; lb *= 0.8f; }
                    float fog = Fog(tE);
                    var img = Tex.Walls[t3];
                    int tx = (int)(uu * Tex.S) & Tex.Mask;
                    float vStep = Tex.S / lh;
                    float v = (from + 0.5f - t2) * vStep;
                    for (int y = from; y < to; y++)
                    {
                        pix[y * W + x] = ShadePix(img.Px[((int)v & Tex.Mask) * Tex.S + tx], lr, lg, lb, fog, fogR, fogG, fogB);
                        v += vStep;
                    }
                    clip = from;
                }
            }
        }

        int SkyU(float rdx, float rdy)
        {
            double ang = Math.Atan2(rdy, rdx);
            int u = (int)(ang / (2 * Math.PI) * Tex.SKY_W * 2);
            return Noise.Mod(u, Tex.SKY_W);
        }

        int SkyPix(int u, int y)
        {
            var sky = map.Def.NightSky ? Tex.SkyNight : Tex.Sky;
            float rowsAbove = Horizon - (y + 0.5f);
            int v = Tex.SKY_H - 1 - (int)(rowsAbove * Tex.SKY_H / (H * 0.85f)) + 6;
            if (v < 0) v = 0;
            if (v >= Tex.SKY_H) v = Tex.SKY_H - 1;
            return sky.Px[v * Tex.SKY_W + u] & Col.RGB;
        }

        public static bool DoorHit(Door d, int cx, int cy, float px, float py, float rdx, float rdy, out float t, out float u)
        {
            t = 0; u = 0;
            if (d.Horizontal)
            {
                if (Math.Abs(rdy) < 1e-6f) return false;
                t = (cy + 0.5f - py) / rdy;
                if (t <= 0) return false;
                float hx = px + t * rdx;
                if (hx < cx || hx >= cx + 1) return false;
                float uu = hx - cx;
                if (uu < d.Open) return false;
                u = uu - d.Open;
                return true;
            }
            else
            {
                if (Math.Abs(rdx) < 1e-6f) return false;
                t = (cx + 0.5f - px) / rdx;
                if (t <= 0) return false;
                float hy = py + t * rdy;
                if (hy < cy || hy >= cy + 1) return false;
                float uu = hy - cy;
                if (uu < d.Open) return false;
                u = uu - d.Open;
                return true;
            }
        }

        static bool PushHit(PushWall p, int cx, int cy, float px, float py, float rdx, float rdy, out float t, out float u, out int side)
        {
            t = 0; u = 0; side = 0;
            float bx = p.X + p.DX * p.Offset, by = p.Y + p.DY * p.Offset;
            float txa, txb, tya, tyb;
            if (Math.Abs(rdx) < 1e-6f) { if (px < bx || px > bx + 1) return false; txa = -1e30f; txb = 1e30f; }
            else { txa = (bx - px) / rdx; txb = (bx + 1 - px) / rdx; if (txa > txb) { float s = txa; txa = txb; txb = s; } }
            if (Math.Abs(rdy) < 1e-6f) { if (py < by || py > by + 1) return false; tya = -1e30f; tyb = 1e30f; }
            else { tya = (by - py) / rdy; tyb = (by + 1 - py) / rdy; if (tya > tyb) { float s = tya; tya = tyb; tyb = s; } }
            float tn = Math.Max(txa, tya), tf = Math.Min(txb, tyb);
            if (tn > tf || tn <= 0) return false;
            float hx = px + rdx * tn, hy = py + rdy * tn;
            const float eps = 0.001f;
            if (hx < cx - eps || hx > cx + 1 + eps || hy < cy - eps || hy > cy + 1 + eps) return false;
            t = tn;
            if (txa > tya) { side = 0; u = hy - by; if (rdx < 0) u = 1 - u; }
            else { side = 1; u = hx - bx; if (rdy > 0) u = 1 - u; }
            if (u < 0) u = 0; if (u > 0.999f) u = 0.999f;
            return true;
        }

        // ------------------------------------------------------------ sprites

        readonly List<SpriteInst> sorted = new List<SpriteInst>();
        static readonly Comparison<SpriteInst> byDepth = (a, b) => b.Depth.CompareTo(a.Depth);

        void RenderSprites(int[] pix, List<SpriteInst> sprites)
        {
            if (sprites == null) return;
            sorted.Clear();
            float rightX = -DirY, rightY = DirX;
            foreach (var s in sprites)
            {
                float rx = s.X - cam.X, ry = s.Y - cam.Y;
                s.Depth = rx * DirX + ry * DirY;
                if (s.Depth < 0.08f) continue;
                sorted.Add(s);
            }
            sorted.Sort(byDepth);
            int cxs = W / 2, cys = H / 2;   // the crosshair sits at the centre of the view
            foreach (var s in sorted)
            {
                float rx = s.X - cam.X, ry = s.Y - cam.Y;
                float lateral = rx * rightX + ry * rightY;
                float depth = s.Depth;
                float scr = Proj / depth;
                float sx = W * 0.5f + lateral * scr;
                float scrY = ProjY / depth;
                float ww = s.Img.W * s.Scale * scr, hh = s.Img.H * s.Scale * scrY;
                float left = sx - ww * 0.5f;
                float top = Horizon + (eye - (s.Z + s.Img.H * s.Scale)) * scrY;
                int x0 = Math.Max(0, (int)Math.Ceiling(left - 0.5f)), x1 = Math.Min(W, (int)Math.Ceiling(left + ww - 0.5f));
                if (x0 >= x1) continue;
                int y0 = Math.Max(0, (int)Math.Ceiling(top - 0.5f)), y1 = Math.Min(H, (int)Math.Ceiling(top + hh - 0.5f));
                if (y0 >= y1) continue;
                float lr = 1, lg = 1, lb = 1;
                if (!s.FullBright)
                {
                    Lighting(s.X, s.Y, out lr, out lg, out lb);
                }
                float fog = Fog(depth);
                var img = s.Img;
                float du = img.W / ww, dv = img.H / hh;
                int flashCol = 0xFFFFFF;
                for (int x = x0; x < x1; x++)
                {
                    if (depth >= ZBuf[x]) continue;
                    int tx = (int)((x + 0.5f - left) * du);
                    if (tx < 0) tx = 0; else if (tx >= img.W) tx = img.W - 1;
                    int ct = img.ColTop[tx], cb = img.ColBot[tx];
                    if (ct > cb) continue;
                    float v = (y0 + 0.5f - top) * dv;
                    for (int y = y0; y < y1; y++, v += dv)
                    {
                        int ty = (int)v;
                        if (ty < ct) continue;
                        if (ty > cb) break;
                        int p = img.Px[ty * img.W + tx];
                        if ((p & Col.OPAQUE) == 0) continue;
                        int c = s.FullBright ? ShadePix(p | Col.EMISSIVE, 1, 1, 1, 1, 0, 0, 0) : ShadePix(p, lr, lg, lb, fog, fogR, fogG, fogB);
                        if (s.Flash > 0) c = Col.Lerp(c, flashCol, s.Flash);
                        pix[y * W + x] = c;
                        if (x == cxs && y == cys && s.Tag != 0) CenterTag = s.Tag;
                    }
                }
            }
        }

        /// <summary>Projects a world point to screen; returns false if behind the camera.</summary>
        public bool Project(float wx, float wy, float wz, out float sx, out float sy, out float depth)
        {
            float rx = wx - cam.X, ry = wy - cam.Y;
            depth = rx * DirX + ry * DirY;
            sx = sy = 0;
            if (depth < 0.05f) return false;
            float lateral = rx * -DirY + ry * DirX;
            sx = W * 0.5f + lateral * Proj / depth;
            sy = Horizon + (eye - wz) * ProjY / depth;
            return true;
        }
    }
}
