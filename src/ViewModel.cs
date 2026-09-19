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

        /// <summary>Low-poly smooth ellipsoid (knuckles, hands).</summary>
        public void Ball(float cx, float cy, float cz, float rx, float ry, float rz, int col)
        {
            const int lat = 5, lon = 8;
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    V3 n00 = Sph(i, j, lat, lon), n01 = Sph(i, j + 1, lat, lon), n10 = Sph(i + 1, j, lat, lon), n11 = Sph(i + 1, j + 1, lat, lon);
                    Func<V3, V3> P = n => new V3(cx + n.X * rx, cy + n.Y * ry, cz + n.Z * rz);
                    var t = New();
                    t.P0 = M.Point(P(n00)); t.P1 = M.Point(P(n10)); t.P2 = M.Point(P(n11));
                    t.N0 = M.Dir(n00); t.N1 = M.Dir(n10); t.N2 = M.Dir(n11); t.Col = col; t.Flat = false; t.Emissive = false;
                    t = New();
                    t.P0 = M.Point(P(n00)); t.P1 = M.Point(P(n11)); t.P2 = M.Point(P(n01));
                    t.N0 = M.Dir(n00); t.N1 = M.Dir(n11); t.N2 = M.Dir(n01); t.Col = col; t.Flat = false; t.Emissive = false;
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
            float t = p.FireAnim;
            bool firing = t < WeaponDef.All[weapon].Anim + 0.05f;

            // minigun barrels spin up while firing and spin down afterwards
            if (weapon == 3 && t < 0.15f) spinSpeed = Math.Min(40, spinSpeed + dt * 160);
            else spinSpeed = Math.Max(0, spinSpeed - dt * 30);
            spin += spinSpeed * dt;

            // ---- where the gun is held (camera space) and how it moves
            float bob = p.BobAmt;
            float bx = (float)Math.Cos(p.BobPhase) * 0.011f * bob;
            float by = -(float)Math.Abs(Math.Sin(p.BobPhase)) * 0.009f * bob + (float)Math.Sin(time * 1.7f) * 0.0018f;
            float recoil = 0, kickUp = 0;
            switch (weapon)
            {
                case 1: recoil = Kick(t, 0.02f, 16) * 1.0f; break;
                case 2: recoil = Kick(t, 0.02f, 9) * 2.0f; break;
                case 3: recoil = t < 0.1f ? 0.45f + 0.25f * (float)Math.Sin(time * 90) : 0; break;
                case 4: recoil = Kick(t, 0.03f, 7) * 2.6f; break;
            }
            kickUp = recoil;
            float sw = p.SwitchPos;

            // per weapon: how far right / up / forward it is held
            float ox = 0.13f, oy = -0.105f, oz = 0.42f;
            switch (weapon)
            {
                case 0: ox = 0.12f; oy = -0.1f; oz = 0.36f; break;
                case 1: ox = 0.118f; oy = -0.075f; oz = 0.45f; break;
                case 3: oy = -0.11f; oz = 0.46f; break;
                case 4: ox = 0.14f; oy = -0.115f; oz = 0.5f; break;
            }
            var root = Mat.Translate(ox + bx - p.SwayX * 0.04f, oy + by - sw * 0.2f + recoil * 0.008f, oz - recoil * 0.028f);
            // aim so the barrel line meets the crosshair a few metres ahead, then add recoil tip and lowering
            root = root.Mul(Mat.RotY(-0.2f + p.SwayX * 0.08f));
            root = root.Mul(Mat.RotX(0.05f + kickUp * 0.12f - sw * 0.7f));
            root = root.Mul(Mat.RotZ(-0.1f - p.SwayX * 0.05f));
            root = root.Mul(Mat.Scale(1.0f));

            mb.Clear();
            V3 muzzle;
            switch (weapon)
            {
                case 0: muzzle = BuildFists(root, t); break;
                case 1: muzzle = BuildPistol(root); break;
                case 2: muzzle = BuildShotgun(root, t); break;
                case 3: muzzle = BuildMinigun(root, spin); break;
                default: muzzle = BuildLauncher(root, t); break;
            }

            // ---- rasterize
            // size the weapon by the view height so it looks the same in wide or tall windows
            float fy = (viewH * 0.5f) / (float)Math.Tan(21 * Math.PI / 180), fx = fy / aspect;
            float cx = W * 0.5f, cy = viewH * 0.5f;
            bool flash = weapon != 0 && t < (weapon == 2 || weapon == 4 ? 0.08f : 0.055f);
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

            // ---- muzzle flash
            if (flash && muzzle.Z > 0.05f)
            {
                float mx = cx + muzzle.X / muzzle.Z * fx, my = cy - muzzle.Y / muzzle.Z * fy;
                float r = (weapon == 2 || weapon == 4 ? 0.034f : 0.022f) / muzzle.Z * fx;
                FlashGlow(s, W, viewH, mx, my, Math.Max(2, r), aspect, time);
            }
        }

        static float Kick(float t, float rise, float decay)
        {
            if (t < rise) return t / rise;
            return (float)Math.Exp(-(t - rise) * decay);
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
            mb.Cyl(elbow, elbow + dir * 0.25f, 0.036f, 0.044f, 8, Col.Scale(Sleeve, 0.85f), false);
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

        static V3 BuildLauncher(Mat m, float t)
        {
            mb.M = m;
            V3 a = new V3(0, 0.035f, -0.03f), b = new V3(0, 0.035f, 0.44f);
            mb.Cyl(a, b, 0.047f, 0.047f, 14, Olive, false);                                    // tube
            mb.Cyl(new V3(0, 0.035f, 0.1f), new V3(0, 0.035f, 0.135f), 0.0475f, 0.0475f, 14, Col.Rgb(210, 170, 40), false);   // hazard band
            mb.Cyl(new V3(0, 0.035f, 0.43f), new V3(0, 0.035f, 0.48f), 0.047f, 0.058f, 14, Col.Scale(Metal, 0.9f), false);    // flared muzzle
            mb.Cyl(new V3(0, 0.035f, 0.475f), new V3(0, 0.035f, 0.476f), 0.052f, 0.052f, 14, Black, true);                    // bore
            if (t > 0.45f) mb.Cyl(new V3(0, 0.035f, 0.44f), new V3(0, 0.035f, 0.476f), 0.024f, 0.004f, 8, Col.Rgb(170, 40, 30), false);   // next rocket
            mb.Cyl(new V3(0, 0.035f, -0.04f), new V3(0, 0.035f, -0.03f), 0.05f, 0.05f, 14, Dark, true);                     // rear cap
            // sight on the left with a glowing lens
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
            return m.Point(new V3(0, 0.035f, 0.5f));
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

        static void FlashGlow(Screen s, int W, int H, float mx, float my, float r, float aspect, float time)
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
                    int c = Col.Lerp(Col.Rgb(255, 110, 20), Col.Rgb(255, 250, 215), Math.Min(1, k * 1.6f));
                    int i = y * W + x;
                    s.Pix[i] = Col.Lerp(s.Pix[i], c, Math.Min(1, k * 2.2f));
                }
        }
    }
}
