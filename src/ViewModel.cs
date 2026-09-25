// TERMINAL HELL - first-person weapons as small 3D models, rasterized in software each frame.
// The gun is held at the lower right like in modern shooters and points at the crosshair.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    struct V3
    {
        public float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 operator *(V3 a, float s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        public float Dot(V3 b) { return X * b.X + Y * b.Y + Z * b.Z; }
        public V3 Cross(V3 b) { return new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X); }
        public float Len() { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); }
        public V3 Norm() { float l = Len(); return l > 1e-9f ? this * (1 / l) : new V3(0, 1, 0); }
    }

    /// <summary>Affine transform: 3x3 rotation (rows) + translation.</summary>
    struct Mat
    {
        public float A, B, C, D, E, F, G, H, I, TX, TY, TZ;

        public static Mat Identity { get { return new Mat { A = 1, E = 1, I = 1 }; } }

        public static Mat Translate(float x, float y, float z) { var m = Identity; m.TX = x; m.TY = y; m.TZ = z; return m; }

        public static Mat Scale(float s) { return new Mat { A = s, E = s, I = s }; }

        /// <summary>A different scale on each axis: squashes a round tube into an oval one.</summary>
        public static Mat Scale(float sx, float sy, float sz) { return new Mat { A = sx, E = sy, I = sz }; }

        /// <summary>Rotation around X (pitch: positive tips the front up).</summary>
        public static Mat RotX(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Mat { A = 1, E = c, F = s, H = -s, I = c };
        }

        /// <summary>Rotation around Y (yaw: positive turns the front to the right).</summary>
        public static Mat RotY(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Mat { A = c, C = s, E = 1, G = -s, I = c };
        }

        /// <summary>Rotation around Z (roll).</summary>
        public static Mat RotZ(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Mat { A = c, B = -s, D = s, E = c, I = 1 };
        }

        /// <summary>this * m (apply m first).</summary>
        public Mat Mul(Mat m)
        {
            var r = new Mat();
            r.A = A * m.A + B * m.D + C * m.G; r.B = A * m.B + B * m.E + C * m.H; r.C = A * m.C + B * m.F + C * m.I;
            r.D = D * m.A + E * m.D + F * m.G; r.E = D * m.B + E * m.E + F * m.H; r.F = D * m.C + E * m.F + F * m.I;
            r.G = G * m.A + H * m.D + I * m.G; r.H = G * m.B + H * m.E + I * m.H; r.I = G * m.C + H * m.F + I * m.I;
            r.TX = A * m.TX + B * m.TY + C * m.TZ + TX;
            r.TY = D * m.TX + E * m.TY + F * m.TZ + TY;
            r.TZ = G * m.TX + H * m.TY + I * m.TZ + TZ;
            return r;
        }

        public V3 Point(V3 p) { return new V3(A * p.X + B * p.Y + C * p.Z + TX, D * p.X + E * p.Y + F * p.Z + TY, G * p.X + H * p.Y + I * p.Z + TZ); }
        public V3 Dir(V3 p) { return new V3(A * p.X + B * p.Y + C * p.Z, D * p.X + E * p.Y + F * p.Z, G * p.X + H * p.Y + I * p.Z); }
    }

    sealed class Tri
    {
        public V3 P0, P1, P2, N0, N1, N2;
        public int Col;
        public bool Emissive, Flat;
    }

    /// <summary>Builds triangles in camera space (x right, y up, z forward) from simple primitives.</summary>
    sealed class MeshBuilder
    {
        public readonly List<Tri> Tris = new List<Tri>();
        readonly List<Tri> pool = new List<Tri>();
        public Mat M = Mat.Identity;

        public void Clear()
        {
            pool.AddRange(Tris);
            Tris.Clear();
            M = Mat.Identity;
        }

        Tri New()
        {
            Tri t;
            if (pool.Count > 0) { t = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1); }
            else t = new Tri();
            Tris.Add(t);
            return t;
        }

        void Face(V3 a, V3 b, V3 c, V3 d, int col, bool emissive)
        {
            var t = New();
            t.P0 = M.Point(a); t.P1 = M.Point(b); t.P2 = M.Point(c); t.Col = col; t.Emissive = emissive; t.Flat = true;
            t = New();
            t.P0 = M.Point(a); t.P1 = M.Point(c); t.P2 = M.Point(d); t.Col = col; t.Emissive = emissive; t.Flat = true;
        }

        /// <summary>Axis-aligned box in the current local frame (centre, half sizes).</summary>
        public void Box(float cx, float cy, float cz, float hx, float hy, float hz, int col) { Box(cx, cy, cz, hx, hy, hz, col, false); }

        public void Box(float cx, float cy, float cz, float hx, float hy, float hz, int col, bool emissive)
        {
            var p = new V3[8];
            for (int i = 0; i < 8; i++)
                p[i] = new V3(cx + ((i & 1) != 0 ? hx : -hx), cy + ((i & 2) != 0 ? hy : -hy), cz + ((i & 4) != 0 ? hz : -hz));
            Face(p[0], p[1], p[3], p[2], col, emissive);   // back (-z)
            Face(p[4], p[5], p[7], p[6], col, emissive);   // front (+z)
            Face(p[0], p[1], p[5], p[4], col, emissive);   // bottom
            Face(p[2], p[3], p[7], p[6], col, emissive);   // top
            Face(p[0], p[2], p[6], p[4], col, emissive);   // left
            Face(p[1], p[3], p[7], p[5], col, emissive);   // right
        }

        /// <summary>Smooth-shaded (optionally tapered) cylinder between two local points.</summary>
        public void Cyl(V3 a, V3 b, float r0, float r1, int seg, int col, bool caps) { Cyl(a, b, r0, r1, seg, col, caps, 0, false); }

        public void Cyl(V3 a, V3 b, float r0, float r1, int seg, int col, bool caps, float phase, bool emissive)
        {
            V3 axis = (b - a).Norm();
            V3 up = Math.Abs(axis.Y) < 0.9f ? new V3(0, 1, 0) : new V3(1, 0, 0);
            V3 u = axis.Cross(up).Norm(), v = axis.Cross(u).Norm();
            for (int i = 0; i < seg; i++)
            {
                double t0 = phase + i * 2 * Math.PI / seg, t1 = phase + (i + 1) * 2 * Math.PI / seg;
                V3 d0 = u * (float)Math.Cos(t0) + v * (float)Math.Sin(t0);
                V3 d1 = u * (float)Math.Cos(t1) + v * (float)Math.Sin(t1);
                V3 a0 = a + d0 * r0, a1 = a + d1 * r0, b0 = b + d0 * r1, b1 = b + d1 * r1;
                var t = New();
                t.P0 = M.Point(a0); t.P1 = M.Point(b0); t.P2 = M.Point(b1);
                t.N0 = M.Dir(d0); t.N1 = M.Dir(d0); t.N2 = M.Dir(d1);
                t.Col = col; t.Emissive = emissive; t.Flat = false;
                t = New();
                t.P0 = M.Point(a0); t.P1 = M.Point(b1); t.P2 = M.Point(a1);
                t.N0 = M.Dir(d0); t.N1 = M.Dir(d1); t.N2 = M.Dir(d1);
                t.Col = col; t.Emissive = emissive; t.Flat = false;
                if (caps)
                {
                    var c1 = New();
                    c1.P0 = M.Point(b); c1.P1 = M.Point(b0); c1.P2 = M.Point(b1); c1.Col = col; c1.Emissive = emissive; c1.Flat = true;
                    var c2 = New();
                    c2.P0 = M.Point(a); c2.P1 = M.Point(a1); c2.P2 = M.Point(a0); c2.Col = col; c2.Emissive = emissive; c2.Flat = true;
                }
            }
        }

        /// <summary>One flat-shaded quad (a, b, c, d in order) in the current local frame.</summary>
        public void Quad(V3 a, V3 b, V3 c, V3 d, int col, bool emissive) { Face(a, b, c, d, col, emissive); }

        /// <summary>
        /// A side profile extruded sideways: a convex polygon given as (y, z) pairs in the local YZ plane, made solid by
        /// giving it a thickness of 2*hx along X around cx. Stocks, grips, guards and angular housings are all built this way.
        /// </summary>
        public void Slab(float cx, float hx, float[] yz, int col) { Slab(cx, hx, yz, col, false); }

        public void Slab(float cx, float hx, float[] yz, int col, bool emissive)
        {
            int n = yz.Length / 2;
            for (int i = 1; i + 1 < n; i++)
            {
                Tri3(new V3(cx + hx, yz[0], yz[1]), new V3(cx + hx, yz[i * 2], yz[i * 2 + 1]), new V3(cx + hx, yz[i * 2 + 2], yz[i * 2 + 3]), col, emissive);
                Tri3(new V3(cx - hx, yz[0], yz[1]), new V3(cx - hx, yz[i * 2 + 2], yz[i * 2 + 3]), new V3(cx - hx, yz[i * 2], yz[i * 2 + 1]), col, emissive);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Face(new V3(cx - hx, yz[i * 2], yz[i * 2 + 1]), new V3(cx + hx, yz[i * 2], yz[i * 2 + 1]),
                     new V3(cx + hx, yz[j * 2], yz[j * 2 + 1]), new V3(cx - hx, yz[j * 2], yz[j * 2 + 1]), col, emissive);
            }
        }

        /// <summary>A box with its four long edges (the ones running along X) cut off at 45 degrees by c - so a housing reads
        /// as machined metal rather than a plain block.</summary>
        public void Bevel(float cx, float cy, float cz, float hx, float hy, float hz, float c, int col)
        {
            Slab(cx, hx, new[] { cy + hy, cz - hz + c, cy + hy, cz + hz - c, cy + hy - c, cz + hz, cy - hy + c, cz + hz,
                                 cy - hy, cz + hz - c, cy - hy, cz - hz + c, cy - hy + c, cz - hz, cy + hy - c, cz - hz }, col);
        }

        /// <summary>
        /// A curved band: the part of a flat ring (in the local YZ plane, centred on (cy, cz)) between angles a0 and a1,
        /// from radius r0 to r1, given a thickness of 2*hx along X. Angle 0 points forward (+z), a quarter turn points up.
        /// </summary>
        public void Band(float cx, float cy, float cz, float r0, float r1, float a0, float a1, float hx, int seg, int col)
        {
            for (int i = 0; i < seg; i++)
            {
                float t0 = a0 + (a1 - a0) * i / seg, t1 = a0 + (a1 - a0) * (i + 1) / seg;
                float s0 = (float)Math.Sin(t0), c0 = (float)Math.Cos(t0), s1 = (float)Math.Sin(t1), c1 = (float)Math.Cos(t1);
                var yz = new[] { cy + s0 * r0, cz + c0 * r0, cy + s0 * r1, cz + c0 * r1, cy + s1 * r1, cz + c1 * r1, cy + s1 * r0, cz + c1 * r0 };
                Slab(cx, hx, yz, col);
            }
        }

        public void Tri3(V3 a, V3 b, V3 c, int col, bool emissive)
        {
            var t = New();
            t.P0 = M.Point(a); t.P1 = M.Point(b); t.P2 = M.Point(c); t.Col = col; t.Emissive = emissive; t.Flat = true;
        }

        /// <summary>Low-poly smooth ellipsoid (knuckles, hands).</summary>
        public void Ball(float cx, float cy, float cz, float rx, float ry, float rz, int col) { Ball(cx, cy, cz, rx, ry, rz, col, false); }

        public void Ball(float cx, float cy, float cz, float rx, float ry, float rz, int col, bool emissive)
        {
            const int lat = 5, lon = 8;
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    V3 n00 = Sph(i, j, lat, lon), n01 = Sph(i, j + 1, lat, lon), n10 = Sph(i + 1, j, lat, lon), n11 = Sph(i + 1, j + 1, lat, lon);
                    Func<V3, V3> P = n => new V3(cx + n.X * rx, cy + n.Y * ry, cz + n.Z * rz);
                    var t = New();
                    t.P0 = M.Point(P(n00)); t.P1 = M.Point(P(n10)); t.P2 = M.Point(P(n11));
                    t.N0 = M.Dir(n00); t.N1 = M.Dir(n10); t.N2 = M.Dir(n11); t.Col = col; t.Flat = false; t.Emissive = emissive;
                    t = New();
                    t.P0 = M.Point(P(n00)); t.P1 = M.Point(P(n11)); t.P2 = M.Point(P(n01));
                    t.N0 = M.Dir(n00); t.N1 = M.Dir(n11); t.N2 = M.Dir(n01); t.Col = col; t.Flat = false; t.Emissive = emissive;
                }
        }

        static V3 Sph(int i, int j, int lat, int lon)
        {
            double th = Math.PI * i / lat, ph = 2 * Math.PI * j / lon;
            return new V3((float)(Math.Sin(th) * Math.Cos(ph)), (float)Math.Cos(th), (float)(Math.Sin(th) * Math.Sin(ph)));
        }
    }

    /// <summary>Weapon models, their animation, and the rasterizer that draws them over the 3D view.</summary>
    static class ViewModel
    {
        static readonly MeshBuilder mb = new MeshBuilder();
        static float[] depth = new float[0];
        static float[] shadeBuf = new float[0];
        static float spin, spinSpeed;

        static readonly int Metal = Col.Rgb(104, 108, 118), Dark = Col.Rgb(62, 62, 70), Black = Col.Rgb(18, 18, 20);
        static readonly int Wood = Col.Rgb(132, 82, 44), Olive = Col.Rgb(90, 108, 70), Skin = Col.Rgb(208, 152, 118);
        static readonly int Sleeve = Col.Rgb(66, 104, 56), Glove = Col.Rgb(92, 78, 60), Steel = Col.Rgb(120, 124, 132);

        static readonly V3 Light = new V3(-0.35f, 0.8f, -0.48f).Norm();

        /// <summary>Draws the current weapon into the view (rows 0..viewH) of the screen.</summary>
        public static void Draw(Screen s, int viewH, Player p, float dt, float time, float aspect, float light)
        {
            int W = s.W;
            if (depth.Length < W * viewH) { depth = new float[W * viewH]; shadeBuf = new float[W * viewH]; }
            Array.Clear(depth, 0, W * viewH);

            int weapon = p.Weapon;
            var def = WeaponDef.All[weapon];
            float t = p.FireAnim;
            bool firing = t < def.Anim + 0.05f;
            float charge = def.ChargeTime > 0 ? Math.Min(1, p.Charge / def.ChargeTime) : 0;

            // minigun barrels spin up while firing and spin down afterwards
            if (weapon == 3 && t < 0.15f) spinSpeed = Math.Min(40, spinSpeed + dt * 160);
            else spinSpeed = Math.Max(0, spinSpeed - dt * 30);
            spin += spinSpeed * dt;
            // the saw's blade follows its motor; the laser warms up while it burns and cools down after
            sawSpin += (weapon == 1 ? p.SawRev * p.SawRev * 75 : 0) * dt;
            laserHeat = weapon == 6 && p.BeamOn ? Math.Min(1, laserHeat + dt * 0.35f) : Math.Max(0, laserHeat - dt * 0.5f);

            // ---- where the gun is held (camera space) and how it moves
            float bob = p.BobAmt;
            float bx = (float)Math.Cos(p.BobPhase) * 0.011f * bob;
            float by = -(float)Math.Abs(Math.Sin(p.BobPhase)) * 0.009f * bob + (float)Math.Sin(time * 1.7f) * 0.0018f;
            float recoil = 0, kickUp = 0;
            switch (weapon)
            {
                case 1:
                    // the motor makes the whole tool buzz in the hand, and it bucks when the blade catches
                    bx += (float)Math.Sin(time * 91) * 0.0018f * p.SawRev + (float)Math.Sin(time * 57) * 0.004f * (p.SawBite > 0 ? 1 : 0);
                    by += (float)Math.Sin(time * 77) * 0.0015f * p.SawRev;
                    recoil = p.SawBite > 0 ? 0.35f + 0.25f * (float)Math.Sin(time * 60) : 0;
                    break;
                case 2: recoil = Kick(t, 0.02f, 16) * 1.0f; break;
                case 3: recoil = t < 0.1f ? 0.45f + 0.25f * (float)Math.Sin(time * 90) : 0; break;
                case 4: recoil = Kick(t, 0.02f, 9) * 2.0f; break;
                case 5: recoil = Kick(t, 0.025f, 6) * 3.2f; break;
                case 6:
                    recoil = p.BeamOn ? 0.1f + 0.06f * (float)Math.Sin(time * 83) : 0;
                    bx += p.BeamOn ? (float)Math.Sin(time * 61) * 0.0012f : 0;
                    break;
                case 7:
                    // winding up it trembles harder and harder; letting go it kicks
                    recoil = Kick(t, 0.03f, 6) * 2.8f;
                    bx += (float)Math.Sin(time * 70) * 0.003f * charge * charge;
                    by += (float)Math.Sin(time * 53) * 0.002f * charge * charge;
                    break;
                case 8: recoil = Kick(t, 0.03f, 7) * 2.6f; break;
                case 9: recoil = Kick(t, 0.025f, 7) * 2.6f; break;
                case 10: recoil = Kick(t, 0.04f, 8) * 2.2f; break;
            }
            kickUp = recoil;
            float sw = p.SwitchPos;

            // per weapon: how far right / up / forward it is held
            float ox = 0.13f, oy = -0.105f, oz = 0.42f;
            switch (weapon)
            {
                case 0: ox = 0.12f; oy = -0.1f; oz = 0.36f; break;
                case 1: ox = 0.118f; oy = -0.088f; oz = 0.36f; break;
                case 2: ox = 0.118f; oy = -0.075f; oz = 0.45f; break;
                case 3: oy = -0.11f; oz = 0.46f; break;
                case 5: ox = 0.128f; oy = -0.1f; oz = 0.38f; break;
                case 6: ox = 0.13f; oy = -0.106f; oz = 0.4f; break;
                case 7: ox = 0.098f; oy = -0.1f; oz = 0.4f; break;
                case 8: ox = 0.14f; oy = -0.115f; oz = 0.5f; break;
                case 9: ox = 0.132f; oy = -0.112f; oz = 0.42f; break;
                case 10: ox = 0.128f; oy = -0.1f; oz = 0.44f; break;
            }
            // the parry: a backhand bash that winds the weapon in, then sweeps it out to the right
            float arc = SwingArc(p.PunchAnim);
            var root = Mat.Translate(ox + bx - p.SwayX * 0.04f + arc * 0.04f, oy + by - sw * 0.2f + recoil * 0.008f + Math.Abs(arc) * 0.03f, oz - recoil * 0.028f + arc * 0.07f);
            // aim so the barrel line meets the crosshair a few metres ahead, then add recoil tip and lowering
            root = root.Mul(Mat.RotY(-0.2f + p.SwayX * 0.08f));
            root = root.Mul(Mat.RotX(0.05f + kickUp * 0.12f - sw * 0.7f));
            root = root.Mul(Mat.RotZ(-0.1f - p.SwayX * 0.05f));
            root = root.Mul(Mat.Scale(1.0f));
            // the swing turns about the butt of the weapon, so the whole thing sweeps across
            // in one arc instead of spinning about its middle
            if (arc != 0)
            {
                const float px = 0.015f, py = -0.075f, pz = -0.13f;
                var sweep = Mat.Translate(px, py, pz);
                sweep = sweep.Mul(Mat.RotY(arc * 0.5f));
                sweep = sweep.Mul(Mat.RotZ(arc * -0.22f));
                sweep = sweep.Mul(Mat.RotX(arc * -0.1f));
                root = root.Mul(sweep.Mul(Mat.Translate(-px, -py, -pz)));
            }

            mb.Clear();
            V3 muzzle;
            switch (weapon)
            {
                case 0: muzzle = BuildFists(root, t); break;
                case 1: muzzle = BuildSaw(root, p, time); break;
                case 2: muzzle = BuildPistol(root); break;
                case 3: muzzle = BuildMinigun(root, spin); break;
                case 4: muzzle = BuildShotgun(root, t); break;
                case 5: muzzle = BuildDoubleShotgun(root, t, p.LastShots); break;
                case 6: muzzle = BuildLaser(root, p.BeamOn, time); break;
                case 7: muzzle = BuildLaserRay(root, t, charge, time); break;
                case 8: muzzle = BuildLauncher(root, t); break;
                case 9: muzzle = BuildGrenadeLauncher(root, t, p.Ammo[2]); break;
                default: muzzle = BuildRayGun(root, t, time, charge); break;
            }

            // ---- rasterize
            // size the weapon by the view height so it looks the same in wide or tall windows
            float fy = (viewH * 0.5f) / (float)Math.Tan(21 * Math.PI / 180), fx = fy / aspect;
            float cx = W * 0.5f, cy = viewH * 0.5f;
            bool bigFlash = weapon == 4 || weapon == 5 || weapon == 8 || weapon == 9;
            bool flash = weapon != 0 && weapon != 1 && weapon != 6 && weapon != 7 && t < (bigFlash ? 0.08f : 0.055f);
            float lit = light + (flash ? 0.6f : 0);
            foreach (var tri in mb.Tris) Raster(s.Pix, W, viewH, tri, fx, fy, cx, cy, lit);

            // ---- dark outline around the silhouette so the gun reads against any background
            for (int y = 0; y < viewH; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (depth[i] == 0) continue;
                    bool edge = (x > 0 && depth[i - 1] == 0) || (x < W - 1 && depth[i + 1] == 0) || (y > 0 && depth[i - W] == 0);
                    if (edge) s.Pix[i] = Col.Scale(s.Pix[i], 0.55f);
                }

            // ---- muzzle flash (both barrels of the double barrel get their own)
            if (flash && muzzle.Z > 0.05f)
            {
                float r = bigFlash ? 0.034f : 0.022f;
                if (weapon == 5 && p.LastShots >= 2)
                {
                    Flash(s, W, viewH, dsgMuzzleL, r * 0.8f, fx, fy, cx, cy, aspect, time, FlashHot, FlashFire);
                    Flash(s, W, viewH, dsgMuzzleR, r * 0.8f, fx, fy, cx, cy, aspect, time + 0.3f, FlashHot, FlashFire);
                }
                else Flash(s, W, viewH, muzzle, r, fx, fy, cx, cy, aspect, time, FlashHot, FlashFire);
            }
            // ---- the laser's beam: one line of red light from the lens to whatever the crosshair is on
            if (weapon == 6 && p.BeamOn && muzzle.Z > 0.05f) DrawBeam(s, W, viewH, muzzle, fx, fy, cx, cy, p, time);
            // ---- the rocket launcher's back blast, a glow at the open end of the tube
            if (weapon == 8 && t < 0.25f && launcherRear.Z > 0.05f)
                Flash(s, W, viewH, launcherRear, 0.06f * (1 - t / 0.25f) + 0.02f, fx, fy, cx, cy, aspect, time, FlashHot, FlashFire);
            // ---- the laser ray letting go: a blue flash across its emitter
            if (weapon == 7 && t < 0.12f && muzzle.Z > 0.05f)
                Flash(s, W, viewH, muzzle, 0.05f * (1 - t / 0.12f) + 0.02f, fx, fy, cx, cy, aspect, time, Col.Rgb(230, 255, 255), Col.Rgb(30, 130, 255));
        }

        static float sawSpin, laserHeat;
        static V3 dsgMuzzleL, dsgMuzzleR;
        static readonly int FlashHot = Col.Rgb(255, 250, 215), FlashFire = Col.Rgb(255, 110, 20);

        static void Flash(Screen s, int W, int H, V3 at, float size, float fx, float fy, float cx, float cy, float aspect, float time, int hot, int fire)
        {
            if (at.Z <= 0.05f) return;
            float mx = cx + at.X / at.Z * fx, my = cy - at.Y / at.Z * fy;
            FlashGlow(s, W, H, mx, my, Math.Max(2, size / at.Z * fx), aspect, time, hot, fire);
        }

        /// <summary>
        /// The laser's beam, drawn over the view: a line from the lens (lower right) to the crosshair, which is exactly where
        /// the beam meets whatever it is burning - every point along the aim line lands on the crosshair. It is thick and
        /// hot at the gun, thins with distance, flickers, and ends in a flare that is brighter when it is cutting into a body.
        /// </summary>
        static void DrawBeam(Screen s, int W, int H, V3 muzzle, float fx, float fy, float cx, float cy, Player p, float time)
        {
            float mx = cx + muzzle.X / muzzle.Z * fx, my = cy - muzzle.Y / muzzle.Z * fy;
            float ex = cx, ey = cy;
            float len = (float)Math.Sqrt((ex - mx) * (ex - mx) + (ey - my) * (ey - my));
            if (len < 1) return;
            float flicker = 0.85f + 0.15f * (float)Math.Sin(time * 71) + 0.08f * (float)Math.Sin(time * 29);
            float w0 = Math.Max(1.2f, 0.011f / muzzle.Z * fx) * flicker, w1 = Math.Max(0.6f, w0 * 0.22f);
            int core = Col.Rgb(255, 236, 228), glow = Col.Rgb(255, 34, 22);
            int x0 = (int)Math.Max(0, Math.Min(mx, ex) - w0 * 3 - 2), x1 = (int)Math.Min(W - 1, Math.Max(mx, ex) + w0 * 3 + 2);
            int y0 = (int)Math.Max(0, Math.Min(my, ey) - w0 * 3 - 2), y1 = (int)Math.Min(H - 1, Math.Max(my, ey) + w0 * 3 + 2);
            float ux = (ex - mx) / len, uy = (ey - my) / len;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f - mx, py = y + 0.5f - my;
                    float along = px * ux + py * uy;
                    if (along < 0 || along > len) continue;
                    float k = along / len;
                    float half = w0 + (w1 - w0) * k;
                    float d = Math.Abs(px * uy - py * ux);
                    float halo = half * 2.8f;
                    if (d > halo) continue;
                    int i = y * W + x;
                    if (d <= half * 0.55f) s.Pix[i] = Col.Lerp(s.Pix[i], core, 0.95f);
                    else if (d <= half) s.Pix[i] = Col.Lerp(s.Pix[i], Col.Lerp(core, glow, 0.5f), 0.9f);
                    else s.Pix[i] = Col.Lerp(s.Pix[i], glow, 0.55f * (1 - (d - half) / (halo - half)));
                }
            // the flare where it lands, and a smaller one at the lens
            float fr = (p.BeamOnBody ? 5.5f : 3.5f) * Math.Max(0.5f, Math.Min(1.6f, 4 / Math.Max(0.5f, p.BeamDist))) * flicker * H / 90f;
            FlashGlow(s, W, H, ex, ey, Math.Max(1.5f, fr), 1, time, core, glow);
            FlashGlow(s, W, H, mx, my, Math.Max(1.5f, w0 * 1.8f), 1, time, core, glow);
        }

        static float Kick(float t, float rise, float decay)
        {
            if (t < rise) return t / rise;
            return (float)Math.Exp(-(t - rise) * decay);
        }

        static float Smooth(float k) { return k <= 0 ? 0 : k >= 1 ? 1 : k * k * (3 - 2 * k); }

        /// <summary>The parry swing over its 0.36s: a short wind-up inwards (negative), a sweep out to the right
        /// (positive), then back to where the weapon is held.</summary>
        static float SwingArc(float t)
        {
            if (t < 0 || t >= 0.36f) return 0;
            float u = t / 0.36f;
            if (u < 0.22f) return -0.35f * Smooth(u / 0.22f);
            if (u < 0.5f) return -0.35f + 1.35f * Smooth((u - 0.22f) / 0.28f);
            return 1 - Smooth((u - 0.5f) / 0.5f);
        }

        /// <summary>A direction given in camera space, written in the local frame of m (whose rotation is orthonormal).</summary>
        static V3 ToLocal(Mat m, V3 d)
        {
            return new V3(m.A * d.X + m.D * d.Y + m.G * d.Z,
                          m.B * d.X + m.E * d.Y + m.H * d.Z,
                          m.C * d.X + m.F * d.Y + m.I * d.Z);
        }

        // ------------------------------------------------------------------ models (local: x right, y up, z forward)

        static void Hand(Mat m, float x, float y, float z, bool rightHand)
        {
            mb.M = m;
            // gloved palm + four curled fingers wrapping forward around a grip
            mb.Box(x, y, z, 0.021f, 0.03f, 0.024f, Glove);
            for (int k = 0; k < 4; k++)
                mb.Ball(x + (rightHand ? -0.004f : 0.004f), y + 0.022f - k * 0.015f, z + 0.024f, 0.021f, 0.0085f, 0.011f, Col.Scale(Glove, 1.15f - k * 0.04f));
        }

        static void Arm(Mat m, V3 wrist, V3 elbow)
        {
            // armoured sleeve from the glove cuff back past the elbow and out of view
            mb.M = m;
            V3 dir = (elbow - wrist).Norm();
            mb.Cyl(wrist - dir * 0.01f, wrist + dir * 0.03f, 0.026f, 0.027f, 8, Col.Scale(Glove, 0.8f), false);
            mb.Cyl(wrist + dir * 0.03f, elbow, 0.029f, 0.036f, 8, Sleeve, false);
            // whatever the weapon is doing, the rest of the arm leaves through the bottom right corner and
            // ends behind the camera: a swing never leaves a cut-off stump hanging in the middle of the view
            V3 away = ToLocal(m, new V3(0.34f, -0.62f, -0.71f)).Norm();
            mb.Cyl(elbow, elbow + away * 0.9f, 0.036f, 0.052f, 8, Col.Scale(Sleeve, 0.85f), false);
        }

        static V3 BuildPistol(Mat m)
        {
            mb.M = m;
            mb.Box(0, 0.036f, 0.07f, 0.0145f, 0.017f, 0.098f, Col.Scale(Metal, 0.9f));       // slide
            for (int k = 0; k < 5; k++) mb.Box(-0.0146f, 0.036f, -0.015f + k * 0.006f, 0.0008f, 0.012f, 0.0015f, Dark);   // serrations
            mb.Box(0, 0.011f, 0.06f, 0.0135f, 0.01f, 0.085f, Dark);                          // frame
            mb.Box(0, 0.038f, 0.1685f, 0.0065f, 0.0065f, 0.0012f, Black);                    // bore
            mb.Box(0, 0.056f, -0.02f, 0.012f, 0.004f, 0.004f, Dark);                          // rear sight
            mb.Box(0, 0.056f, 0.158f, 0.0025f, 0.004f, 0.004f, Dark);                         // front sight
            mb.Box(0, -0.004f, 0.035f, 0.003f, 0.011f, 0.013f, Dark);                         // trigger guard
            var grip = m.Mul(Mat.Translate(0, -0.005f, -0.005f)).Mul(Mat.RotX(-0.28f));
            mb.M = grip;
            mb.Box(0, -0.045f, 0, 0.0135f, 0.047f, 0.021f, Col.Rgb(44, 42, 44));               // grip
            Hand(grip, 0.002f, -0.05f, -0.004f, true);
            mb.M = m;
            mb.Box(-0.019f, 0.014f, 0.022f, 0.006f, 0.0065f, 0.028f, Glove);                   // thumb along the frame
            Arm(grip, new V3(0.004f, -0.075f, -0.03f), new V3(0.05f, -0.16f, -0.16f));
            return m.Point(new V3(0, 0.038f, 0.18f));
        }

        static V3 BuildShotgun(Mat m, float t)
        {
            float pump = 0;
            if (t > 0.3f && t < 0.72f) pump = -0.1f * (float)Math.Sin(Math.PI * (t - 0.3f) / 0.42f);
            mb.M = m;
            mb.Box(0, 0.02f, 0.02f, 0.019f, 0.029f, 0.08f, Dark);                              // receiver
            mb.Box(0.0192f, 0.024f, 0.03f, 0.0004f, 0.012f, 0.03f, Black);                     // ejection port
            mb.Cyl(new V3(0, 0.037f, 0.09f), new V3(0, 0.037f, 0.58f), 0.011f, 0.011f, 10, Metal, true);    // barrel
            mb.Cyl(new V3(0, 0.009f, 0.09f), new V3(0, 0.009f, 0.5f), 0.0095f, 0.0095f, 10, Col.Scale(Metal, 0.8f), true);   // magazine
            mb.Box(0, 0.037f, 0.5805f, 0.007f, 0.007f, 0.0008f, Black);
            mb.Box(0, 0.05f, 0.565f, 0.002f, 0.0025f, 0.002f, Col.Rgb(230, 220, 170));       // bead sight
            mb.Cyl(new V3(0, 0.012f, 0.25f + pump), new V3(0, 0.012f, 0.39f + pump), 0.02f, 0.02f, 8, Wood, true);   // pump
            for (int k = 0; k < 4; k++)
                mb.Cyl(new V3(0, 0.012f, 0.27f + k * 0.03f + pump), new V3(0, 0.012f, 0.28f + k * 0.03f + pump), 0.0205f, 0.0205f, 8, Col.Scale(Wood, 0.6f), false);
            var stock = m.Mul(Mat.Translate(0, 0.005f, -0.06f)).Mul(Mat.RotX(-0.2f));
            mb.M = stock;
            mb.Box(0, -0.02f, -0.02f, 0.014f, 0.03f, 0.03f, Wood);                             // pistol grip / wrist
            Hand(stock, 0.002f, -0.03f, -0.01f, true);
            Arm(stock, new V3(0.006f, -0.055f, -0.04f), new V3(0.05f, -0.14f, -0.17f));
            // support hand under the pump
            var left = m.Mul(Mat.Translate(-0.004f, -0.006f, 0.32f + pump)).Mul(Mat.RotZ(0.6f));
            mb.M = left;
            mb.Box(0, -0.012f, 0, 0.02f, 0.016f, 0.036f, Glove);
            for (int k = 0; k < 4; k++) mb.Ball(0.022f, -0.004f, -0.024f + k * 0.016f, 0.008f, 0.012f, 0.0075f, Col.Scale(Glove, 1.04f));
            Arm(m, new V3(-0.02f, -0.02f, 0.3f + pump), new V3(-0.1f, -0.1f, 0.1f));
            return m.Point(new V3(0, 0.037f, 0.6f));
        }

        static V3 BuildMinigun(Mat m, float spin)
        {
            mb.M = m;
            mb.Box(0, 0.0f, -0.01f, 0.042f, 0.038f, 0.085f, Dark);                            // housing
            mb.Box(0, 0.052f, 0.0f, 0.008f, 0.014f, 0.05f, Metal);                             // carry handle
            mb.Box(0, 0.042f, -0.045f, 0.008f, 0.004f, 0.006f, Metal);
            mb.Box(0, 0.042f, 0.045f, 0.008f, 0.004f, 0.006f, Metal);
            mb.Box(0.056f, -0.02f, -0.01f, 0.016f, 0.028f, 0.045f, Olive);                    // ammo box
            for (int k = 0; k < 6; k++) mb.Box(0.03f, 0.012f - k * 0.004f, -0.01f + k * 0.012f, 0.012f, 0.003f, 0.004f, Col.Rgb(200, 160, 60));   // belt
            // rotating barrel cluster
            for (int k = 0; k < 6; k++)
            {
                double a = spin + k * Math.PI / 3;
                float ox = (float)Math.Cos(a) * 0.022f, oy = (float)Math.Sin(a) * 0.022f;
                mb.Cyl(new V3(ox, oy, 0.07f), new V3(ox, oy, 0.46f), 0.0058f, 0.0058f, 6, k == 0 ? Steel : Metal, true);
                mb.Box(ox, oy, 0.4605f, 0.003f, 0.003f, 0.0006f, Black);
            }
            mb.Cyl(new V3(0, 0, 0.18f), new V3(0, 0, 0.2f), 0.034f, 0.034f, 12, Dark, true, spin, false);    // clamps
            mb.Cyl(new V3(0, 0, 0.4f), new V3(0, 0, 0.415f), 0.032f, 0.032f, 12, Dark, true, spin, false);
            mb.Cyl(new V3(0, 0, 0.075f), new V3(0, 0, 0.44f), 0.008f, 0.008f, 6, Black, false);
            // rear grip on the right and a front handle on the left
            mb.Box(0.012f, -0.06f, -0.06f, 0.012f, 0.03f, 0.016f, Col.Rgb(40, 38, 36));
            Hand(m, 0.014f, -0.068f, -0.066f, true);
            Arm(m, new V3(0.02f, -0.095f, -0.09f), new V3(0.07f, -0.17f, -0.2f));
            mb.M = m;
            mb.Box(-0.045f, -0.035f, 0.12f, 0.009f, 0.03f, 0.011f, Col.Rgb(40, 38, 36));
            var left = m.Mul(Mat.Translate(-0.047f, -0.04f, 0.115f)).Mul(Mat.RotY(0.4f));
            Hand(left, 0, 0, 0, false);
            Arm(m, new V3(-0.052f, -0.065f, 0.1f), new V3(-0.12f, -0.14f, -0.05f));
            return m.Point(new V3(0, 0, 0.48f));
        }

        static V3 launcherRear;

        /// <summary>
        /// The rocket launcher: an olive tube open at both ends. Looking down the back you see straight into it - a dark liner,
        /// and the tail of the next rocket sitting deep inside with its fins and a dull nozzle. Firing throws a plume of flame and
        /// sparks out of the back of the tube, and the rear of the tube glows as the blast dies away.
        /// </summary>
        static V3 BuildLauncher(Mat m, float t)
        {
            mb.M = m;
            const float Y = 0.035f;
            V3 a = new V3(0, Y, -0.03f), b = new V3(0, Y, 0.44f);
            float blast = t < 0.3f ? 1 - t / 0.3f : 0;
            mb.Cyl(a, b, 0.047f, 0.047f, 14, Olive, false);                                    // tube
            mb.Cyl(new V3(0, Y, 0.1f), new V3(0, Y, 0.135f), 0.0475f, 0.0475f, 14, Col.Rgb(210, 170, 40), false);   // hazard band
            mb.Cyl(new V3(0, Y, 0.43f), new V3(0, Y, 0.48f), 0.047f, 0.058f, 14, Col.Scale(Metal, 0.9f), false);    // flared muzzle
            mb.Cyl(new V3(0, Y, 0.475f), new V3(0, Y, 0.476f), 0.052f, 0.052f, 14, Black, true);                    // bore
            if (t > 0.45f) mb.Cyl(new V3(0, Y, 0.44f), new V3(0, Y, 0.476f), 0.024f, 0.004f, 8, Col.Rgb(170, 40, 30), false);   // next rocket
            // ---- the open back: a metal lip round the mouth, a black liner inside the tube, and the rocket loaded deep in it
            int lip = Col.Lerp(Col.Scale(Metal, 0.9f), Col.Rgb(255, 150, 60), blast * 0.6f);
            mb.Cyl(new V3(0, Y, -0.052f), new V3(0, Y, -0.026f), 0.058f, 0.052f, 14, lip, false);                        // flared rear lip
            mb.Cyl(new V3(0, Y, -0.026f), new V3(0, Y, 0.3f), 0.0425f, 0.0425f, 14, Col.Rgb(16, 16, 18), false);         // the liner
            mb.Cyl(new V3(0, Y, 0.12f), new V3(0, Y, 0.3f), 0.029f, 0.029f, 10, Col.Rgb(120, 40, 34), false);             // rocket body
            mb.Cyl(new V3(0, Y, 0.112f), new V3(0, Y, 0.12f), 0.02f, 0.029f, 10, Col.Rgb(60, 60, 66), false);            // its nozzle
            mb.Cyl(new V3(0, Y, 0.111f), new V3(0, Y, 0.113f), 0.014f, 0.014f, 8, Col.Lerp(Col.Rgb(90, 40, 20), Col.Rgb(255, 170, 60), blast), false, 0, blast > 0);
            for (int k = 0; k < 4; k++)
            {
                double fa = k * Math.PI / 2 + Math.PI / 4;
                float fxx = (float)Math.Cos(fa), fyy = (float)Math.Sin(fa);
                mb.Box(fxx * 0.036f, Y + fyy * 0.036f, 0.135f, 0.004f, 0.004f, 0.022f, Col.Rgb(70, 70, 76));             // fins
            }
            launcherRear = m.Point(new V3(0, Y, -0.045f));
            // ---- the back blast: a plume of flame thrown out of the open end towards the shooter, with sparks
            if (blast > 0)
            {
                int frame = (int)(t * 60);
                float len = 0.05f + 0.19f * blast;
                mb.Cyl(new V3(0, Y, -0.03f), new V3(0, Y, -0.03f - len), 0.04f + 0.012f * blast, 0.006f, 10, Col.Rgb(255, 110, 20), false, 0, true);
                mb.Cyl(new V3(0, Y, -0.03f), new V3(0, Y, -0.03f - len * 0.7f), 0.026f, 0.004f, 10, Col.Rgb(255, 210, 90), false, 0, true);
                mb.Cyl(new V3(0, Y, -0.03f), new V3(0, Y, -0.03f - len * 0.4f), 0.014f, 0.003f, 8, Col.Rgb(255, 250, 220), false, 0, true);
                for (int k = 0; k < 7; k++)
                {
                    float sa = (float)(Noise.Hashf(k, frame, 61) * Math.PI * 2), sr = 0.04f + 0.09f * Noise.Hashf(k, frame, 62);
                    float sz = -0.05f - len * (0.3f + 0.9f * Noise.Hashf(k, frame, 63));
                    mb.Box((float)Math.Cos(sa) * sr, Y + (float)Math.Sin(sa) * sr, sz, 0.004f, 0.004f, 0.007f, Col.Lerp(Col.Rgb(255, 150, 40), Col.Rgb(255, 240, 180), Noise.Hashf(k, frame, 64)), true);
                }
            }
            // sight on the left with a glowing lens
            mb.M = m;
            mb.Box(-0.06f, 0.07f, 0.12f, 0.011f, 0.018f, 0.04f, Dark);
            mb.Box(-0.06f, 0.072f, 0.079f, 0.006f, 0.008f, 0.0015f, Col.Rgb(90, 200, 255), true);
            // grip, trigger hand and support hand
            var grip = m.Mul(Mat.Translate(0, -0.012f, 0.03f)).Mul(Mat.RotX(-0.22f));
            mb.M = grip;
            mb.Box(0, -0.03f, 0, 0.013f, 0.032f, 0.018f, Dark);
            Hand(grip, 0.002f, -0.036f, -0.004f, true);
            Arm(grip, new V3(0.005f, -0.062f, -0.03f), new V3(0.05f, -0.15f, -0.16f));
            var left = m.Mul(Mat.Translate(-0.01f, -0.02f, 0.26f)).Mul(Mat.RotZ(0.7f));
            mb.M = left;
            mb.Box(0, -0.012f, 0, 0.02f, 0.016f, 0.036f, Glove);
            Arm(m, new V3(-0.025f, -0.035f, 0.24f), new V3(-0.1f, -0.11f, 0.05f));
            return m.Point(new V3(0, Y, 0.5f));
        }

        /// <summary>How far through a stretch of an animation: 0 before a, 1 after b, eased in between.</summary>
        static float Seg(float t, float a, float b) { return Smooth((t - a) / (b - a)); }

        static V3 Lerp3(V3 a, V3 b, float k) { return a + (b - a) * k; }

        // ------------------------------------------------------------------ the saw

        static readonly int ToolYellow = Col.Rgb(226, 176, 34), ToolYellowDark = Col.Rgb(150, 110, 18), Rubber = Col.Rgb(30, 30, 32);
        static readonly int GearGrey = Col.Rgb(96, 100, 108), GuardGrey = Col.Rgb(64, 66, 72), BladeSteel = Col.Rgb(176, 182, 192);

        /// <summary>
        /// The saw: a cordless angle saw. A yellow motor body with a black rubber pistol grip and a battery under it, a
        /// grey gear head, and a toothed blade standing on edge at the front-left, spinning in a half guard. The left hand
        /// holds a stub handle under the head. The blade runs up with the motor, and throws sparks when it bites.
        /// </summary>
        static V3 BuildSaw(Mat m, Player p, float time)
        {
            m = m.Mul(Mat.Scale(0.74f));
            const float R = 0.078f;
            var hub = new V3(-0.046f, -0.004f, 0.108f);
            mb.M = m;
            // motor body, banded, with vents along the top and a status light
            mb.Cyl(new V3(0, 0, -0.1f), new V3(0, 0, 0.055f), 0.025f, 0.03f, 14, ToolYellow, false);
            mb.Ball(0, 0, -0.1f, 0.025f, 0.025f, 0.014f, Rubber);
            mb.Cyl(new V3(0, 0, -0.05f), new V3(0, 0, -0.034f), 0.0275f, 0.0285f, 14, Rubber, false);
            for (int k = 0; k < 4; k++) mb.Box(0, 0.027f + k * 0.0006f, -0.02f + k * 0.016f, 0.012f, 0.002f, 0.003f, ToolYellowDark);
            mb.Box(0.012f, 0.022f, -0.075f, 0.003f, 0.003f, 0.004f, p.SawRev > 0.05f ? Col.Rgb(90, 255, 110) : Col.Rgb(20, 90, 30), true);
            // the gear head and the spindle housing reaching over to the blade
            mb.Box(0, -0.002f, 0.085f, 0.026f, 0.027f, 0.032f, GearGrey);
            mb.Cyl(new V3(0.015f, hub.Y, hub.Z), new V3(hub.X + 0.011f, hub.Y, hub.Z), 0.021f, 0.019f, 12, Col.Scale(GearGrey, 0.85f), true);
            // the pistol grip under the back of the motor, with the trigger and the battery
            var grip = m.Mul(Mat.Translate(0, -0.024f, -0.072f)).Mul(Mat.RotX(-0.34f));
            mb.M = grip;
            mb.Box(0, -0.036f, 0, 0.013f, 0.04f, 0.018f, Rubber);
            mb.Box(0, -0.004f, 0.022f, 0.005f, 0.009f, 0.006f, Col.Rgb(200, 40, 30));
            mb.Box(0, -0.084f, 0.012f, 0.021f, 0.014f, 0.036f, ToolYellow);
            mb.Box(0, -0.1f, 0.012f, 0.0215f, 0.003f, 0.0365f, Rubber);
            Hand(grip, 0.002f, -0.042f, -0.004f, true);
            Arm(grip, new V3(0.005f, -0.07f, -0.03f), new V3(0.05f, -0.15f, -0.16f));
            // the stub handle under the gear head, and the left hand on it
            mb.M = m;
            mb.Cyl(new V3(-0.006f, -0.024f, 0.086f), new V3(-0.016f, -0.088f, 0.078f), 0.011f, 0.012f, 10, Rubber, true);
            var left = m.Mul(Mat.Translate(-0.014f, -0.06f, 0.07f)).Mul(Mat.RotY(0.5f));
            Hand(left, 0, 0, 0, false);
            Arm(m, new V3(-0.02f, -0.09f, 0.05f), new V3(-0.1f, -0.16f, -0.06f));

            // the guard: a curved band over the top and back of the blade, a yellow lip along its edge, a plate on the body side
            var head = m.Mul(Mat.Translate(hub.X, hub.Y, hub.Z));
            mb.M = head;
            const float g0 = 0.72f, g1 = 3.75f;
            mb.Band(0.003f, 0, 0, R + 0.004f, R + 0.013f, g0, g1, 0.013f, 14, GuardGrey);
            mb.Band(-0.011f, 0, 0, R + 0.01f, R + 0.0145f, g0, g1, 0.0016f, 14, ToolYellow);
            mb.Band(0.0145f, 0, 0, 0.018f, R + 0.013f, g0, g1, 0.0015f, 10, Col.Scale(GuardGrey, 0.8f));
            // the blade itself, turning with the motor: a steel disc, a darker hub, cooling slots, a hex nut, and the teeth
            var blade = head.Mul(Mat.RotX(-sawSpin));
            mb.M = blade;
            bool blur = p.SawRev > 0.7f;
            mb.Cyl(new V3(-0.0025f, 0, 0), new V3(0.0025f, 0, 0), R, R, 28, blur ? Col.Scale(BladeSteel, 1.1f) : BladeSteel, true);
            mb.Cyl(new V3(-0.003f, 0, 0), new V3(0.003f, 0, 0), 0.028f, 0.028f, 16, Col.Scale(BladeSteel, 0.72f), true);
            mb.Cyl(new V3(-0.003f, 0, 0), new V3(-0.011f, 0, 0), 0.011f, 0.011f, 6, Col.Rgb(70, 72, 78), true);
            for (int k = 0; k < 6; k++)
            {
                mb.M = blade.Mul(Mat.RotX(k * (float)Math.PI / 3));
                mb.Box(-0.0027f, 0, 0.05f, 0.0006f, 0.0022f, 0.013f, Col.Scale(BladeSteel, 0.55f));
            }
            // teeth: hooked, and smeared into a pale ring once the blade is really moving
            for (int k = 0; k < 24; k++)
            {
                mb.M = blade.Mul(Mat.RotX(k * (float)Math.PI / 12));
                mb.Slab(0, 0.0034f, new[] { 0f, R - 0.004f, 0.012f, R - 0.002f, 0.003f, R + 0.011f }, blur ? Col.Rgb(220, 222, 226) : Col.Rgb(238, 238, 232));
            }
            if (blur)
            {
                mb.M = head;
                mb.Band(0, 0, 0, R - 0.003f, R + 0.009f, -2.6f, 0.72f, 0.0022f, 12, Col.Rgb(206, 210, 216));
            }
            // sparks off the front edge while it is biting into something
            if (p.SawBite > 0)
            {
                int frame = (int)(time * 30);
                for (int k = 0; k < 14; k++)
                {
                    float r1 = Noise.Hashf(k, frame, 71), r2 = Noise.Hashf(k, frame, 72), r3 = Noise.Hashf(k, frame, 73);
                    float a = -0.55f + (r1 - 0.5f) * 0.5f;
                    float along = r2 * 0.14f;
                    var at = new V3(0.004f * (r3 - 0.5f), (float)Math.Sin(a) * R, (float)Math.Cos(a) * R);
                    var dir = new V3((r3 - 0.5f) * 0.6f, -0.55f - r1 * 0.4f, -0.9f + r2 * 0.3f).Norm();
                    var sp = at + dir * along;
                    mb.M = head;
                    int col = Col.Lerp(Col.Rgb(255, 250, 210), Col.Rgb(255, 130, 30), r2);
                    mb.Box(sp.X, sp.Y, sp.Z, 0.0016f, 0.0016f, 0.0016f + along * 0.04f, col, true);
                }
            }
            return head.Point(new V3(0, 0, R));
        }

        // ------------------------------------------------------------------ the double barrel

        static readonly int Blued = Col.Rgb(58, 60, 72), BluedHi = Col.Rgb(96, 100, 116), WoodDark = Col.Rgb(88, 50, 24);
        static readonly int ShellRed = Col.Rgb(176, 34, 26), Brass = Col.Rgb(206, 170, 84);

        /// <summary>A shotgun shell (red hull, brass head) along +z from the local origin.</summary>
        static void Shell(Mat m)
        {
            mb.M = m;
            mb.Cyl(new V3(0, 0, 0), new V3(0, 0, 0.052f), 0.0105f, 0.0105f, 8, ShellRed, true);
            mb.Cyl(new V3(0, 0, -0.009f), new V3(0, 0, 0), 0.0112f, 0.0112f, 8, Brass, true);
        }

        /// <summary>
        /// The double barrel: short, sawn-off side-by-side barrels over a chunky wooden fore-end, a blued action with twin
        /// hammers, and a wooden stock. After each shot it is broken open - the barrels swing down on the hinge, the spent
        /// shells flick out past your face, the left hand fetches two more and thumbs them in, and the action snaps shut.
        /// </summary>
        static V3 BuildDoubleShotgun(Mat m, float t, int shots)
        {
            m = m.Mul(Mat.RotZ(0.3f));   // canted a little, so both barrels show side by side
            // the reload, as a few overlapping phases
            float open = 0.6f * Seg(t, 0.22f, 0.38f) * (1 - Seg(t, 0.9f, 0.98f));
            if (t > 0.98f && t < 1.1f) open = -0.04f * (float)Math.Sin((t - 0.98f) / 0.12f * Math.PI);   // the snap shut rattles
            float tip = Seg(t, 0.2f, 0.4f) * (1 - Seg(t, 0.95f, 1.12f));                                 // the whole gun tipped to show the breech
            var g = m.Mul(Mat.Translate(0, 0, 0.1f)).Mul(Mat.RotX(-0.22f * tip)).Mul(Mat.RotZ(0.2f * tip)).Mul(Mat.Translate(-0.01f * tip, 0.02f * tip, -0.1f));
            var hinge = new V3(0, 0.012f, 0.088f);
            var bar = g.Mul(Mat.Translate(hinge.X, hinge.Y, hinge.Z)).Mul(Mat.RotX(-open)).Mul(Mat.Translate(-hinge.X, -hinge.Y, -hinge.Z));

            // the action: blued steel, twin hammer spurs, the top lever, two triggers in their guard
            mb.M = g;
            mb.Box(0, 0.026f, 0.04f, 0.028f, 0.022f, 0.049f, Blued);
            mb.Box(0, 0.049f, 0.02f, 0.006f, 0.0022f, 0.03f, BluedHi);                                            // top strap
            mb.Box(0.002f + 0.012f * tip, 0.05f, -0.006f, 0.004f, 0.003f, 0.014f, BluedHi);                        // top lever, pushed aside to open
            for (int k = -1; k <= 1; k += 2)
            {
                var hm = g.Mul(Mat.Translate(k * 0.013f, 0.046f, -0.004f)).Mul(Mat.RotX(0.6f));
                mb.M = hm;
                mb.Box(0, 0.006f, 0, 0.004f, 0.009f, 0.004f, Col.Scale(Blued, 0.8f));
                mb.Box(0, 0.014f, -0.004f, 0.0045f, 0.002f, 0.006f, BluedHi);
            }
            mb.M = g;
            mb.Box(0, -0.004f, 0.03f, 0.004f, 0.002f, 0.028f, Blued);                                             // trigger guard
            mb.Box(0, 0.002f, 0.004f, 0.004f, 0.009f, 0.002f, Blued);
            mb.Box(0.004f, 0.004f, 0.03f, 0.0015f, 0.008f, 0.002f, BluedHi);
            mb.Box(-0.004f, 0.004f, 0.018f, 0.0015f, 0.008f, 0.002f, BluedHi);
            // the stock: a wooden wrist curving down into a pistol grip (the butt is behind the hand, out of view)
            mb.Slab(0, 0.016f, new[] { 0.034f, -0.008f, 0.03f, -0.05f, -0.012f, -0.078f, -0.036f, -0.062f, -0.026f, -0.03f, 0.004f, -0.008f }, Wood);
            var grip = g.Mul(Mat.Translate(0, -0.01f, -0.048f)).Mul(Mat.RotX(-0.55f));
            Hand(grip, 0.002f, -0.024f, -0.004f, true);
            Arm(grip, new V3(0.006f, -0.052f, -0.04f), new V3(0.05f, -0.15f, -0.18f));

            // the barrels, on the hinge: two tubes, a rib above and below, bores, the bead, the wooden fore-end
            mb.M = bar;
            for (int k = -1; k <= 1; k += 2)
            {
                float bx = k * 0.0132f;
                mb.Cyl(new V3(bx, 0.034f, 0.088f), new V3(bx, 0.034f, 0.39f), 0.0128f, 0.0125f, 14, Col.Rgb(84, 86, 98), false);
                mb.Cyl(new V3(bx, 0.034f, 0.385f), new V3(bx, 0.034f, 0.402f), 0.0136f, 0.0136f, 14, BluedHi, false);
                mb.Cyl(new V3(bx, 0.034f, 0.4015f), new V3(bx, 0.034f, 0.4025f), 0.0085f, 0.0085f, 10, Black, true);
                mb.Cyl(new V3(bx, 0.034f, 0.0875f), new V3(bx, 0.034f, 0.0885f), 0.0128f, 0.0128f, 12, Blued, true);   // breech face
                mb.Cyl(new V3(bx, 0.034f, 0.086f), new V3(bx, 0.034f, 0.0892f), 0.0086f, 0.0086f, 10, Black, true);   // chamber
            }
            mb.Box(0, 0.048f, 0.24f, 0.0055f, 0.0035f, 0.15f, Blued);                                              // top rib
            mb.Box(0, 0.02f, 0.3f, 0.0055f, 0.004f, 0.09f, Blued);                                                // bottom rib
            mb.Box(0, 0.053f, 0.392f, 0.0018f, 0.002f, 0.002f, Col.Rgb(236, 226, 180));                          // bead
            mb.Box(0, 0.011f, 0.19f, 0.024f, 0.013f, 0.085f, Wood);                                               // fore-end
            mb.Cyl(new V3(0, 0.004f, 0.11f), new V3(0, 0.004f, 0.27f), 0.019f, 0.017f, 10, WoodDark, true);
            mb.Box(0, 0.002f, 0.275f, 0.012f, 0.006f, 0.006f, Blued);                                             // fore-end latch
            // brass in the chambers: the spent pair until they're flicked out, the fresh pair once they're pushed home
            bool spent = t < 0.4f, fresh = t > 0.86f;
            if (spent || fresh)
                for (int k = -1; k <= 1; k += 2)
                {
                    if (spent && shots < 2 && k > 0) continue;
                    mb.Cyl(new V3(k * 0.0132f, 0.034f, 0.0835f), new V3(k * 0.0132f, 0.034f, 0.0875f), 0.0112f, 0.0112f, 10, Brass, true);
                }
            dsgMuzzleL = bar.Point(new V3(-0.0132f, 0.034f, 0.41f));
            dsgMuzzleR = bar.Point(new V3(0.0132f, 0.034f, 0.41f));

            // the spent shells flying out past your face
            if (t > 0.4f && t < 0.8f)
                for (int k = 0; k < Math.Max(1, shots); k++)
                {
                    float tau = t - 0.4f - k * 0.025f;
                    if (tau < 0) continue;
                    float side = k == 0 ? -1 : 1;
                    var at = new V3(side * 0.02f + side * 0.16f * tau, 0.05f + 0.45f * tau - 1.9f * tau * tau, 0.07f - 0.22f * tau);
                    Shell(g.Mul(Mat.Translate(at.X, at.Y, at.Z)).Mul(Mat.RotX(0.8f + tau * 16)).Mul(Mat.RotY(side * tau * 6)));
                }

            // the left hand: on the fore-end, then down out of sight, back with two shells, thumbing them in, and back again
            var onFore = bar.Mul(Mat.Translate(-0.004f, -0.006f, 0.21f)).Mul(Mat.RotZ(0.6f));
            var breech = bar.Mul(Mat.Translate(0.0f, 0.034f, 0.088f));
            Mat lh;
            bool holding = false;
            float push = 0;
            if (t < 0.46f || t > 1.08f) lh = onFore;
            else if (t < 0.6f) lh = Blend(onFore, g.Mul(Mat.Translate(0.03f, -0.24f, 0.12f)), Seg(t, 0.46f, 0.6f));
            else if (t < 0.76f) { lh = Blend(g.Mul(Mat.Translate(0.03f, -0.24f, 0.12f)), breech.Mul(Mat.Translate(0, -0.018f, -0.07f)), Seg(t, 0.6f, 0.76f)); holding = true; }
            else if (t < 0.9f) { push = Seg(t, 0.78f, 0.86f); lh = breech.Mul(Mat.Translate(0, -0.018f, -0.07f + 0.05f * push)); holding = push < 1; }
            else lh = Blend(breech.Mul(Mat.Translate(0, -0.018f, -0.02f)), onFore, Seg(t, 0.9f, 1.08f));
            mb.M = lh;
            mb.Box(0, -0.012f, 0, 0.02f, 0.016f, 0.034f, Glove);
            for (int k = 0; k < 4; k++) mb.Ball(0.021f, -0.004f, -0.022f + k * 0.015f, 0.008f, 0.012f, 0.0075f, Col.Scale(Glove, 1.04f));
            if (holding)
                for (int k = -1; k <= 1; k += 2) Shell(lh.Mul(Mat.Translate(k * 0.0132f, 0.018f, 0.03f)));
            // the forearm hangs from the wrist toward the lower left of the view, wherever the hand has got to
            var wrist = lh.Point(new V3(-0.01f, -0.024f, -0.02f));
            LeftArm(wrist, wrist + new V3(-0.03f, -0.1f, -0.03f));
            return bar.Point(new V3(0, 0.034f, 0.41f));
        }

        /// <summary>A left forearm given in camera space: sleeve from the wrist to the elbow, then almost straight down out of
        /// the view. Used when the hand is moving about and a frame-relative elbow would swing through the camera.</summary>
        static void LeftArm(V3 wrist, V3 elbow)
        {
            mb.M = Mat.Identity;
            V3 dir = (elbow - wrist).Norm();
            mb.Cyl(wrist - dir * 0.01f, wrist + dir * 0.03f, 0.026f, 0.027f, 8, Col.Scale(Glove, 0.8f), false);
            mb.Cyl(wrist + dir * 0.03f, elbow, 0.029f, 0.034f, 8, Sleeve, false);
            V3 away = new V3(-0.12f, -0.97f, -0.2f).Norm();
            mb.Cyl(elbow, elbow + away * 0.5f, 0.034f, 0.045f, 8, Col.Scale(Sleeve, 0.85f), false);
        }

        /// <summary>Somewhere between two frames: positions blend straight, the rotations blend closely enough for a hand.</summary>
        static Mat Blend(Mat a, Mat b, float k)
        {
            var r = new Mat();
            r.A = a.A + (b.A - a.A) * k; r.B = a.B + (b.B - a.B) * k; r.C = a.C + (b.C - a.C) * k;
            r.D = a.D + (b.D - a.D) * k; r.E = a.E + (b.E - a.E) * k; r.F = a.F + (b.F - a.F) * k;
            r.G = a.G + (b.G - a.G) * k; r.H = a.H + (b.H - a.H) * k; r.I = a.I + (b.I - a.I) * k;
            r.TX = a.TX + (b.TX - a.TX) * k; r.TY = a.TY + (b.TY - a.TY) * k; r.TZ = a.TZ + (b.TZ - a.TZ) * k;
            return r;
        }

        // ------------------------------------------------------------------ the laser

        static readonly int LaserWhite = Col.Rgb(218, 220, 226), LaserGrey = Col.Rgb(140, 144, 156), LaserDark = Col.Rgb(38, 40, 48);

        /// <summary>
        /// The laser: a slim white rifle. An oval body tapering into a nose, grey bands, a red light strip down each flank,
        /// a dark spine with a red-dot sight, a finned emitter barrel ending in a red lens, a glowing cell slung underneath
        /// in a cage, and heat vents on top at the back that go from dark to orange as it is held on. The beam itself is drawn
        /// by DrawBeam.
        /// </summary>
        static V3 BuildLaser(Mat m, bool on, float time)
        {
            float pulse = on ? 0.75f + 0.25f * (float)Math.Sin(time * 40) : 0;
            int strip = Col.Lerp(Col.Rgb(90, 20, 18), Col.Rgb(255, 60, 40), on ? 0.6f + 0.4f * pulse : 0.15f);
            int lens = Col.Lerp(Col.Rgb(110, 20, 16), Col.Rgb(255, 230, 220), on ? pulse : 0.1f);
            // the body: an oval white tube running into a tapered nose, with a rounded tail and two grey bands
            mb.M = m.Mul(Mat.Scale(0.78f, 1f, 1f));
            mb.Cyl(new V3(0, 0, -0.07f), new V3(0, 0, 0.11f), 0.03f, 0.03f, 16, LaserWhite, false);
            mb.Cyl(new V3(0, 0, 0.11f), new V3(0, 0, 0.158f), 0.03f, 0.017f, 16, LaserWhite, false);
            mb.Ball(0, 0, -0.07f, 0.03f, 0.03f, 0.022f, LaserWhite);
            mb.Cyl(new V3(0, 0, -0.046f), new V3(0, 0, -0.036f), 0.0306f, 0.0306f, 16, LaserGrey, false);
            mb.Cyl(new V3(0, 0, 0.074f), new V3(0, 0, 0.084f), 0.0306f, 0.0306f, 16, LaserGrey, false);
            mb.M = m;
            for (int k = -1; k <= 1; k += 2) mb.Box(k * 0.0237f, 0.004f, 0.018f, 0.0009f, 0.0024f, 0.05f, strip, true);
            // the spine along the top, the red-dot sight on it, and the heat vents behind it
            mb.Bevel(0, 0.033f, 0.02f, 0.0065f, 0.0045f, 0.075f, 0.002f, LaserDark);
            mb.Bevel(0, 0.046f, 0.0f, 0.009f, 0.008f, 0.013f, 0.003f, LaserDark);
            mb.Box(0, 0.047f, 0.0133f, 0.0038f, 0.0038f, 0.0005f, Col.Rgb(255, 40, 30), true);
            int vent = Col.Lerp(Col.Rgb(34, 30, 30), Col.Rgb(255, 120, 30), laserHeat);
            for (int k = 0; k < 3; k++) mb.Box(0.012f, 0.026f, -0.058f + k * 0.011f, 0.007f, 0.0015f, 0.0025f, vent, laserHeat > 0.05f);
            // the emitter: a dark barrel through a grey shroud, cooling fins, the lens ring and the lens
            mb.Cyl(new V3(0, 0, 0.15f), new V3(0, 0, 0.3f), 0.011f, 0.01f, 12, LaserDark, false);
            mb.Cyl(new V3(0, 0, 0.148f), new V3(0, 0, 0.196f), 0.018f, 0.016f, 12, LaserGrey, false);
            for (int k = 0; k < 5; k++)
                mb.Cyl(new V3(0, 0, 0.206f + k * 0.018f), new V3(0, 0, 0.212f + k * 0.018f), 0.0152f, 0.0152f, 12, Col.Scale(LaserGrey, 0.85f), false);
            mb.Cyl(new V3(0, 0, 0.295f), new V3(0, 0, 0.312f), 0.0145f, 0.013f, 12, LaserDark, false);
            mb.Cyl(new V3(0, 0, 0.311f), new V3(0, 0, 0.313f), 0.009f, 0.009f, 12, lens, true, 0, true);
            // the power cell slung under the body, glowing through its cage
            int cell = Col.Lerp(Col.Rgb(120, 30, 20), Col.Rgb(255, 150, 90), on ? pulse : 0.25f);
            mb.Box(0, -0.024f, 0.02f, 0.007f, 0.006f, 0.045f, LaserGrey);
            mb.Cyl(new V3(0, -0.036f, -0.03f), new V3(0, -0.036f, 0.07f), 0.0105f, 0.0105f, 10, cell, false, 0, true);
            for (int k = 0; k < 3; k++) mb.Cyl(new V3(0, -0.036f, -0.032f + k * 0.049f), new V3(0, -0.036f, -0.026f + k * 0.049f), 0.0128f, 0.0128f, 10, LaserGrey, true);
            // the grip and the trigger hand
            var grip = m.Mul(Mat.Translate(0, -0.03f, -0.045f)).Mul(Mat.RotX(-0.3f));
            mb.M = grip;
            mb.Bevel(0, -0.034f, 0, 0.012f, 0.036f, 0.018f, 0.005f, LaserDark);
            Hand(grip, 0.002f, -0.04f, -0.004f, true);
            Arm(grip, new V3(0.005f, -0.066f, -0.03f), new V3(0.05f, -0.15f, -0.16f));
            // the support hand under the nose
            var left = m.Mul(Mat.Translate(-0.004f, -0.03f, 0.13f)).Mul(Mat.RotZ(0.6f));
            mb.M = left;
            mb.Box(0, -0.012f, 0, 0.019f, 0.015f, 0.032f, Glove);
            for (int k = 0; k < 4; k++) mb.Ball(0.021f, -0.004f, -0.021f + k * 0.014f, 0.008f, 0.011f, 0.007f, Col.Scale(Glove, 1.04f));
            Arm(m, new V3(-0.02f, -0.046f, 0.12f), new V3(-0.1f, -0.12f, -0.02f));
            return m.Point(new V3(0, 0, 0.314f));
        }

        // ------------------------------------------------------------------ the laser ray

        static readonly int RayMetal = Col.Rgb(78, 86, 102), RayDark = Col.Rgb(36, 40, 50);

        /// <summary>
        /// The laser slicer: an energy crossbow. A gunmetal tiller with a groove for the bolt, a wide prod of two curved limbs
        /// spreading out from a riser with glowing cams at the tips, and a taut string. While the trigger is held the string is
        /// drawn back, the limbs flex, the bolt of light lying in the groove brightens and electricity crackles along the prod;
        /// when it lets go the string snaps forward, the bolt is gone, and a new one slides into the groove.
        /// </summary>
        static V3 BuildLaserRay(Mat m, float t, float charge, float time)
        {
            float after = t < 0.6f ? 1 - t / 0.6f : 0;                 // the heat left in it after a shot
            int dim = Col.Rgb(22, 52, 92), lit = Col.Rgb(70, 200, 255), hot = Col.Rgb(235, 250, 255);
            int edge = Col.Lerp(Col.Lerp(dim, lit, 0.55f + 0.45f * charge), hot, Math.Max(after, charge * charge * 0.7f));
            int glow = Col.Lerp(lit, hot, 0.35f + 0.65f * Math.Max(charge, after));
            // the crossbow is held nearly square to the view (the other weapons are turned in towards the crosshair), so that
            // both limbs of the prod show and it reads as a bow instead of half of one
            m = m.Mul(Mat.RotY(0.17f)).Mul(Mat.Scale(1.12f));
            mb.M = m;
            // ---- the tiller: a bevelled gunmetal stock with a dark groove for the bolt along its top and light strips down the flanks
            mb.Bevel(0, 0.0f, 0.02f, 0.021f, 0.024f, 0.145f, 0.008f, RayMetal);
            mb.Bevel(0, 0.028f, 0.05f, 0.008f, 0.004f, 0.11f, 0.002f, RayDark);
            for (int side = -1; side <= 1; side += 2)
            {
                mb.Box(side * 0.0216f, 0.001f, 0.035f, 0.0012f, 0.007f, 0.085f, edge, true);
                mb.Box(side * 0.0216f, 0.001f, -0.085f, 0.0012f, 0.007f, 0.012f, edge, true);
            }
            // the riser the prod is bolted to, and a small sight post at the back
            mb.Bevel(0, 0.002f, 0.185f, 0.017f, 0.02f, 0.03f, 0.007f, RayDark);
            mb.Box(0, 0.034f, -0.06f, 0.003f, 0.007f, 0.004f, RayDark);

            // ---- the prod: two limbs spreading out sideways from the riser, bowed so their tips sweep back towards the shooter
            const float Span = 0.125f, Zc = 0.2f, Bow = 0.075f;
            const int N = 6;
            float draw = 0.07f * charge;                                 // the string is drawn back as it winds up
            float snap = t < 0.3f ? (float)Math.Sin(t * 95) * 0.022f * (1 - t / 0.3f) : 0;
            float tipZ = Zc - Bow;
            float stringZ = tipZ - draw + snap;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < N; i++)
                {
                    float s0 = (float)i / N, s1 = (float)(i + 1) / N, sm = (s0 + s1) * 0.5f;
                    float x0 = side * s0 * Span, x1 = side * s1 * Span;
                    float z0 = Zc - Bow * s0 * s0, z1 = Zc - Bow * s1 * s1;
                    float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
                    float ang = (float)Math.Atan2(z1 - z0, x1 - x0);      // the limb's direction in the XZ plane
                    // a limb flexes back while the string is drawn: tips move back, the middle stays put
                    float flex = draw * 0.35f * sm * sm;
                    mb.M = m.Mul(Mat.Translate((x0 + x1) * 0.5f, 0.0f, (z0 + z1) * 0.5f - flex)).Mul(Mat.RotY(-ang));
                    float thick = 0.014f - 0.006f * sm;
                    mb.Bevel(0, 0, 0, len * 0.5f + 0.0012f, thick, 0.008f, 0.003f, i % 2 == 0 ? RayMetal : Col.Scale(RayMetal, 1.12f));
                    // glowing inlay along the front edge of the limb
                    mb.Box(0, 0.0f, 0.0083f, len * 0.5f, thick * 0.55f, 0.0012f, edge, true);
                }
                // the tip: a small glowing cam the string runs round
                mb.M = m;
                mb.Ball(side * Span, 0.0f, tipZ - draw * 0.35f, 0.0085f, 0.0085f, 0.0085f, glow, true);
            }
            // ---- the string: two taut lines from the tips to where the bolt is nocked
            V3 tipL = new V3(-Span, 0.0f, tipZ - draw * 0.35f), tipR = new V3(Span, 0.0f, tipZ - draw * 0.35f);
            V3 nock = new V3(0, 0.022f, stringZ);
            mb.M = m;
            mb.Cyl(tipL, nock, 0.0026f, 0.0026f, 4, Col.Lerp(glow, hot, 0.4f), false, 0, true);
            mb.Cyl(tipR, nock, 0.0026f, 0.0026f, 4, Col.Lerp(glow, hot, 0.4f), false, 0, true);
            // ---- the bolt: a needle of energy lying in the groove, its tail on the string. It is gone the instant the shot leaves
            // and comes back a moment later
            bool loaded = t > 0.3f;
            if (loaded)
            {
                float grow = Math.Min(1, (t - 0.3f) / 0.2f);
                V3 tail = new V3(0, 0.03f, stringZ), tip = new V3(0, 0.03f, stringZ + 0.2f * grow);
                mb.Cyl(tail, tip, 0.0045f, 0.0045f, 6, Col.Lerp(lit, hot, 0.5f + 0.5f * charge), false, 0, true);
                mb.Cyl(tip, tip + new V3(0, 0, 0.032f * grow), 0.0045f, 0.0008f, 6, hot, false, 0, true);
                for (int f = -1; f <= 1; f += 2) mb.Box(f * 0.006f, 0.03f, stringZ + 0.012f, 0.0035f, 0.0012f, 0.011f, Col.Lerp(lit, hot, 0.3f), true);
            }
            // ---- charging: electricity crackles along the limbs and through the string
            if (charge > 0.05f)
            {
                int frame = (int)(time * 24);
                int bolts = 2 + (int)(charge * 6);
                for (int b = 0; b < bolts; b++)
                {
                    float sd = (Noise.Hashf(b, frame, 91) < 0.5f ? -1 : 1);
                    float sm = 0.1f + 0.9f * Noise.Hashf(b, frame, 92);
                    float bx0 = sd * sm * Span, bz0 = Zc - Bow * sm * sm - draw * 0.35f * sm * sm;
                    float jy = (Noise.Hashf(b, frame, 93) - 0.5f) * 0.03f, jz = (Noise.Hashf(b, frame, 94) - 0.5f) * 0.04f;
                    mb.M = m;
                    mb.Box(bx0 * 0.85f, jy * 0.5f, bz0 + jz * 0.5f, Math.Abs(bx0) * 0.15f + 0.002f, Math.Abs(jy) * 0.5f + 0.002f, Math.Abs(jz) * 0.5f + 0.002f, Col.Lerp(lit, hot, 0.7f), true);
                }
            }
            // ---- grip, trigger hand, and the support hand under the tiller
            var grip = m.Mul(Mat.Translate(0, -0.024f, -0.045f)).Mul(Mat.RotX(-0.3f));
            mb.M = grip;
            mb.Bevel(0, -0.034f, 0, 0.013f, 0.037f, 0.019f, 0.005f, RayDark);
            Hand(grip, 0.002f, -0.04f, -0.004f, true);
            Arm(grip, new V3(0.005f, -0.068f, -0.03f), new V3(0.05f, -0.15f, -0.16f));
            var left = m.Mul(Mat.Translate(-0.012f, -0.034f, 0.06f)).Mul(Mat.RotZ(0.5f));
            mb.M = left;
            mb.Box(0, -0.012f, 0, 0.02f, 0.016f, 0.034f, Glove);
            for (int k = 0; k < 4; k++) mb.Ball(0.022f, -0.004f, -0.022f + k * 0.015f, 0.008f, 0.012f, 0.0075f, Col.Scale(Glove, 1.04f));
            Arm(m, new V3(-0.028f, -0.05f, 0.05f), new V3(-0.1f, -0.12f, -0.06f));
            return m.Point(new V3(0, 0.03f, 0.27f));
        }

        // ------------------------------------------------------------------ the grenade launcher

        static readonly int GlBlack = Col.Rgb(36, 38, 36), GlOlive = Col.Rgb(90, 102, 64), GlOliveDark = Col.Rgb(54, 62, 40);

        /// <summary>
        /// The grenade launcher: a six-shot revolver launcher. A short, fat ribbed barrel over a big fluted olive drum with a
        /// grenade's brass nose in each loaded chamber, a top strap, a pistol grip, a vertical fore-grip and a folding stock.
        /// After each shot the drum clicks round a sixth of a turn to bring the next grenade under the barrel.
        /// </summary>
        static V3 BuildGrenadeLauncher(Mat m, float t, int ammo)
        {
            m = m.Mul(Mat.RotX(-0.07f));
            const float BarrelY = 0.038f, ChamberR = 0.036f;
            float turn = Seg(t, 0.22f, 0.4f);
            float drum = ((ammo % 6) + (1 - turn)) * (float)Math.PI / 3;
            mb.M = m;
            // the barrel: fat and short, ribbed, with a muzzle ring and a flip-up ladder sight
            mb.Cyl(new V3(0, BarrelY, 0.1f), new V3(0, BarrelY, 0.31f), 0.026f, 0.026f, 16, GlBlack, false);
            for (int k = 0; k < 3; k++)
                mb.Cyl(new V3(0, BarrelY, 0.135f + k * 0.045f), new V3(0, BarrelY, 0.146f + k * 0.045f), 0.0278f, 0.0278f, 16, Col.Scale(GlBlack, 1.6f), false);
            mb.Cyl(new V3(0, BarrelY, 0.304f), new V3(0, BarrelY, 0.325f), 0.03f, 0.03f, 16, Col.Scale(GlBlack, 1.35f), false);
            mb.Cyl(new V3(0, BarrelY, 0.3245f), new V3(0, BarrelY, 0.3255f), 0.02f, 0.02f, 14, Black, true);
            mb.Box(0, BarrelY + 0.03f, 0.19f, 0.009f, 0.004f, 0.008f, GlBlack);
            mb.Box(0, BarrelY + 0.041f, 0.19f, 0.007f, 0.008f, 0.0015f, GlBlack);
            // the frame: a strap over the drum, the drum's axle block, and a bevelled rear frame
            mb.Bevel(0, 0.068f, 0.035f, 0.01f, 0.005f, 0.08f, 0.003f, GlBlack);
            mb.Bevel(0, 0.012f, -0.052f, 0.019f, 0.04f, 0.022f, 0.01f, GlBlack);
            mb.Box(0, 0.0f, 0.1f, 0.011f, 0.011f, 0.012f, GlBlack);
            // the drum, turning: olive steel, fluted between the chambers, a grenade nose in each loaded chamber
            var dm = m.Mul(Mat.RotZ(drum));
            mb.M = dm;
            mb.Cyl(new V3(0, 0, -0.03f), new V3(0, 0, 0.094f), 0.054f, 0.054f, 18, GlOlive, true);
            mb.Cyl(new V3(0, 0, -0.024f), new V3(0, 0, -0.018f), 0.0555f, 0.0555f, 18, GlOliveDark, false);
            mb.Cyl(new V3(0, 0, 0.082f), new V3(0, 0, 0.088f), 0.0555f, 0.0555f, 18, GlOliveDark, false);
            int loaded = Math.Min(6, ammo + (turn < 1 ? 1 : 0));
            for (int k = 0; k < 6; k++)
            {
                mb.M = dm.Mul(Mat.RotZ(k * (float)Math.PI / 3));
                mb.Box(0, 0.054f, 0.032f, 0.006f, 0.003f, 0.05f, GlOliveDark);                                        // a flute
                mb.M = dm.Mul(Mat.RotZ(k * (float)Math.PI / 3 + (float)Math.PI / 6));
                mb.Cyl(new V3(0, ChamberR, 0.0935f), new V3(0, ChamberR, 0.0955f), 0.014f, 0.014f, 10, Black, true);
                if (k < loaded) mb.Ball(0, ChamberR, 0.0915f, 0.011f, 0.011f, 0.007f, Col.Rgb(190, 156, 72));
            }
            // pistol grip and trigger hand
            var grip = m.Mul(Mat.Translate(0, -0.03f, -0.06f)).Mul(Mat.RotX(-0.3f));
            mb.M = grip;
            mb.Bevel(0, -0.034f, 0, 0.012f, 0.036f, 0.019f, 0.005f, GlBlack);
            Hand(grip, 0.002f, -0.04f, -0.004f, true);
            Arm(grip, new V3(0.005f, -0.066f, -0.03f), new V3(0.05f, -0.15f, -0.16f));
            // the folding stock: two struts running back out of view
            mb.M = m;
            mb.Cyl(new V3(0.012f, 0.034f, -0.07f), new V3(0.014f, 0.036f, -0.28f), 0.0045f, 0.0045f, 6, GlBlack, false);
            mb.Cyl(new V3(0.012f, -0.012f, -0.07f), new V3(0.014f, -0.02f, -0.28f), 0.0045f, 0.0045f, 6, GlBlack, false);
            // the vertical fore-grip under the barrel, and the left hand round it
            mb.Bevel(0, 0.006f, 0.22f, 0.009f, 0.006f, 0.05f, 0.002f, GlBlack);
            mb.Cyl(new V3(0, 0.0f, 0.23f), new V3(0, -0.07f, 0.215f), 0.012f, 0.013f, 10, GlBlack, true);
            var left = m.Mul(Mat.Translate(-0.004f, -0.036f, 0.21f)).Mul(Mat.RotY(0.35f));
            Hand(left, 0, 0, 0, false);
            Arm(m, new V3(-0.012f, -0.064f, 0.19f), new V3(-0.1f, -0.15f, 0.02f));
            return m.Point(new V3(0, BarrelY, 0.33f));
        }


        /// <summary>
        /// The ray gun: a weapon that belongs in Hell but was built by something with machine tools. A black-glass receiver
        /// trimmed in brass, veined with glowing rune slits, a pair of brass horns rising over the action, and a forked barrel -
        /// two curved prongs like a demon's claws with the burning core hanging between them. While the trigger is held the
        /// core swells and brightens, arcs of fire jump across the fork, and the runes light up one after another.
        /// </summary>
        static V3 BuildRayGun(Mat m, float t, float time, float charge)
        {
            int obs = Col.Rgb(40, 32, 50), obsLight = Col.Rgb(78, 62, 92), brass = Col.Rgb(190, 142, 54), brassDark = Col.Rgb(122, 88, 32);
            float heat = t < 0.3f ? 1 - t / 0.3f : 0;
            // while the trigger is held the core winds up: brighter, bigger, and shaking faster
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * (5 + charge * 60));
            int ember = Col.Lerp(Col.Rgb(214, 36, 120), Col.Rgb(255, 235, 255), Math.Min(1, heat + pulse * (0.25f + charge * 0.5f) + charge * 0.55f));
            int rune = Col.Lerp(Col.Rgb(120, 16, 56), ember, 0.25f + 0.75f * Math.Max(charge, heat));
            float grow = 1 + charge * 0.55f;
            mb.M = m;
            // ---- the receiver: a bevelled black body with a brass band at each end and a glowing channel along the spine
            mb.Bevel(0, 0.018f, 0.06f, 0.025f, 0.03f, 0.135f, 0.011f, obs);
            mb.Bevel(0, 0.052f, 0.06f, 0.013f, 0.006f, 0.12f, 0.003f, obsLight);
            mb.Box(0, 0.0585f, 0.06f, 0.0045f, 0.0015f, 0.11f, rune, true);
            for (int side = -1; side <= 1; side += 2) mb.Box(side * 0.0255f, 0.018f, -0.072f, 0.0022f, 0.0315f, 0.005f, brass);   // trim strips at the back
            mb.Box(0, 0.0492f, -0.072f, 0.0265f, 0.0022f, 0.005f, brass);
            mb.Box(0, 0.018f, 0.195f, 0.0265f, 0.0315f, 0.007f, brass);
            mb.Box(0, 0.018f, 0.06f, 0.0262f, 0.0312f, 0.0025f, brassDark);
            // rune slits down both flanks; one more lights for every fifth of a charge
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 5; k++)
                {
                    bool on = charge * 5 > k + 0.1f || heat > 0.2f;
                    mb.Box(side * 0.0257f, 0.02f, -0.04f + k * 0.05f, 0.0013f, 0.012f - (k & 1) * 0.004f, 0.009f, on ? ember : Col.Rgb(90, 18, 52), on);
                }
            // ---- the eye: a lens on top of the receiver at the back, watching the shooter
            mb.Ball(0, 0.066f, -0.03f, 0.0135f, 0.0095f, 0.013f, rune, true);
            mb.Cyl(new V3(0, 0.056f, -0.03f), new V3(0, 0.062f, -0.03f), 0.017f, 0.015f, 10, brass, false);
            // ---- horns: two small brass spikes rising forward over the action
            for (int side = -1; side <= 1; side += 2)
            {
                mb.Cyl(new V3(side * 0.017f, 0.052f, 0.11f), new V3(side * 0.027f, 0.092f, 0.15f), 0.0085f, 0.002f, 6, brass, false);
                mb.Cyl(new V3(side * 0.017f, 0.052f, 0.11f), new V3(side * 0.02f, 0.06f, 0.12f), 0.0105f, 0.0085f, 6, brassDark, false);
            }
            // ---- the fork: a rod out of the receiver, then two clawed prongs sweeping out and back in round the burning core
            mb.Cyl(new V3(0, 0.018f, 0.2f), new V3(0, 0.018f, 0.46f), 0.0095f, 0.0075f, 8, obsLight, false);
            mb.Cyl(new V3(0, 0.018f, 0.2f), new V3(0, 0.018f, 0.44f), 0.0035f, 0.0035f, 6, rune, false, 0, true);
            for (int side = -1; side <= 1; side += 2)
            {
                V3 root = new V3(side * 0.016f, 0.018f, 0.2f), bend = new V3(side * 0.046f, 0.02f, 0.36f), tip = new V3(side * 0.014f, 0.022f, 0.56f);
                mb.Cyl(root, bend, 0.013f, 0.0105f, 8, obs, true);
                mb.Cyl(bend, tip, 0.0105f, 0.0025f, 8, obs, false);
                mb.Cyl(root + (bend - root) * 0.16f, root + (bend - root) * 0.28f, 0.0145f, 0.0145f, 8, brass, false);
                mb.Cyl(bend - (bend - root) * 0.12f, bend + (tip - bend) * 0.1f, 0.0125f, 0.0125f, 8, brassDark, false);
                // a glowing vein on the inner face of each claw
                mb.Cyl(bend + new V3(-side * 0.008f, 0, 0), tip + new V3(-side * 0.0035f, 0, 0), 0.0028f, 0.0012f, 5, rune, false, 0, true);
            }
            // ---- the core: a ball of hellfire held in the fork, and sparks leaping between the claws while it charges
            mb.Ball(0, 0.02f, 0.4f, 0.022f * grow, 0.022f * grow, 0.026f * grow, ember, true);
            if (charge > 0.05f)
            {
                int frame = (int)(time * 24);
                int arcs = 1 + (int)(charge * 5);
                for (int b = 0; b < arcs; b++)
                {
                    float zz = 0.34f + 0.2f * Noise.Hashf(b, frame, 71);
                    float yy = 0.02f + (Noise.Hashf(b, frame, 72) - 0.5f) * 0.03f;
                    mb.Box(0, yy, zz, 0.028f, 0.0018f, 0.0022f, Col.Lerp(ember, Col.Rgb(255, 245, 255), 0.6f), true);
                }
            }
            // ---- the grip: wrapped black leather with brass studs, and the trigger hand
            var grip = m.Mul(Mat.Translate(0, -0.02f, 0.005f)).Mul(Mat.RotX(-0.25f));
            mb.M = grip;
            mb.Bevel(0, -0.038f, 0, 0.015f, 0.04f, 0.021f, 0.006f, obs);
            for (int k = 0; k < 3; k++) mb.Box(0.0155f, -0.024f - k * 0.018f, 0, 0.0012f, 0.004f, 0.009f, brass);
            Hand(grip, 0.002f, -0.046f, -0.004f, true);
            Arm(grip, new V3(0.005f, -0.072f, -0.03f), new V3(0.05f, -0.155f, -0.16f));
            return m.Point(new V3(0, 0.02f, 0.58f));
        }

        static V3 BuildFists(Mat m, float t)
        {
            // right fist punches toward the crosshair
            float e = t < 0.32f ? (float)Math.Sin(Math.PI * Math.Min(1, t / 0.32f)) : 0;
            var fist = m.Mul(Mat.Translate(-0.05f * e, 0.03f * e + 0.01f, 0.03f + 0.17f * e)).Mul(Mat.RotY(-0.3f * e));
            mb.M = fist;
            mb.Box(0, 0, 0, 0.03f, 0.027f, 0.03f, Glove);
            for (int k = 0; k < 4; k++) mb.Ball(-0.021f + k * 0.014f, 0.004f, 0.03f, 0.0085f, 0.013f, 0.009f, Col.Scale(Glove, 1.06f));
            mb.Box(-0.03f, -0.01f, 0.012f, 0.008f, 0.009f, 0.02f, Glove);
            Arm(fist, new V3(0.005f, -0.01f, -0.03f), new V3(0.04f, -0.08f, -0.17f));
            return fist.Point(new V3(0, 0, 0.05f));
        }

        // ------------------------------------------------------------------ rasterizer

        static float Shade(V3 n, V3 pos)
        {
            // n faces the camera; light from the upper left, a touch of specular
            float d = n.Dot(Light);
            float diff = d > 0 ? d : 0;
            V3 view = (pos * -1).Norm();
            V3 h = (Light + view).Norm();
            float sp = n.Dot(h);
            float spec = sp > 0 ? (float)Math.Pow(sp, 16) * 0.35f : 0;
            return 0.32f + 0.78f * diff + spec;
        }

        static void Raster(int[] pix, int W, int H, Tri t, float fx, float fy, float cx, float cy, float light)
        {
            V3 a = t.P0, b = t.P1, c = t.P2;
            // geometry reaching behind the camera (the arms) is pulled onto the near plane
            if (a.Z < 0.03f && b.Z < 0.03f && c.Z < 0.03f) return;
            if (a.Z < 0.03f) a.Z = 0.03f;
            if (b.Z < 0.03f) b.Z = 0.03f;
            if (c.Z < 0.03f) c.Z = 0.03f;
            float ax = cx + a.X / a.Z * fx, ay = cy - a.Y / a.Z * fy;
            float bx = cx + b.X / b.Z * fx, by = cy - b.Y / b.Z * fy;
            float cxx = cx + c.X / c.Z * fx, cyy = cy - c.Y / c.Z * fy;
            float area = (bx - ax) * (cyy - ay) - (by - ay) * (cxx - ax);
            if (Math.Abs(area) < 1e-6f) return;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cxx)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cxx))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cyy)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cyy))));
            if (x0 > x1 || y0 > y1) return;

            // per-vertex light (flat faces use the face normal)
            float s0, s1, s2;
            if (t.Emissive) s0 = s1 = s2 = 1.2f;
            else
            {
                V3 fn = (b - a).Cross(c - a).Norm();
                if (fn.Dot(a) > 0) fn = fn * -1;
                if (t.Flat) { s0 = s1 = s2 = Shade(fn, a); }
                else
                {
                    V3 n0 = t.N0.Norm(), n1 = t.N1.Norm(), n2 = t.N2.Norm();
                    if (n0.Dot(fn) < 0) n0 = n0 * -1;
                    if (n1.Dot(fn) < 0) n1 = n1 * -1;
                    if (n2.Dot(fn) < 0) n2 = n2 * -1;
                    s0 = Shade(n0, a); s1 = Shade(n1, b); s2 = Shade(n2, c);
                }
                s0 *= light; s1 *= light; s2 *= light;
            }
            float iz0 = 1 / a.Z, iz1 = 1 / b.Z, iz2 = 1 / c.Z;
            float inv = 1 / area;
            int cr = Col.R(t.Col), cg = Col.G(t.Col), cb = Col.B(t.Col);
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f;
                    float w0 = ((bx - px) * (cyy - py) - (by - py) * (cxx - px)) * inv;
                    float w1 = ((cxx - px) * (ay - py) - (cyy - py) * (ax - px)) * inv;
                    float w2 = 1 - w0 - w1;
                    if (w0 < -0.001f || w1 < -0.001f || w2 < -0.001f) continue;
                    float iz = w0 * iz0 + w1 * iz1 + w2 * iz2;
                    int i = y * W + x;
                    if (iz <= depth[i]) continue;
                    depth[i] = iz;
                    float sh = w0 * s0 + w1 * s1 + w2 * s2;
                    // a little surface grain so large faces are not perfectly flat
                    sh *= 0.96f + 0.08f * Noise.Hashf(x, y, 17);
                    int r = (int)(cr * sh), g = (int)(cg * sh), bl = (int)(cb * sh);
                    pix[i] = ((r > 255 ? 255 : r) << 16) | ((g > 255 ? 255 : g) << 8) | (bl > 255 ? 255 : bl);
                }
            }
        }

        static void FlashGlow(Screen s, int W, int H, float mx, float my, float r, float aspect, float time, int hot, int fire)
        {
            float ry = r * aspect;
            int x0 = (int)(mx - r * 1.8f), x1 = (int)(mx + r * 1.8f), y0 = (int)(my - ry * 1.8f), y1 = (int)(my + ry * 1.8f);
            float rot = time * 7;
            for (int y = Math.Max(0, y0); y <= Math.Min(H - 1, y1); y++)
                for (int x = Math.Max(0, x0); x <= Math.Min(W - 1, x1); x++)
                {
                    float dx = (x + 0.5f - mx) / r, dy = (y + 0.5f - my) / ry;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    double ang = Math.Atan2(dy, dx) + rot;
                    float star = 0.75f + 0.45f * (float)Math.Abs(Math.Cos(ang * 2.5));
                    float k = 1 - d / (1.6f * star);
                    if (k <= 0) continue;
                    int c = Col.Lerp(fire, hot, Math.Min(1, k * 1.6f));
                    int i = y * W + x;
                    s.Pix[i] = Col.Lerp(s.Pix[i], c, Math.Min(1, k * 2.2f));
                }
        }
    }
}
