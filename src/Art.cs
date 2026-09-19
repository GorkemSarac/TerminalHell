// TERMINAL HELL - procedural sprites: monsters, items, decorations, projectiles and effects.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    static class Art
    {
        // monster frame indices
        public const int WALK1 = 0, WALK2 = 1, AIM = 2, FIRE = 3, PAIN = 4, DIE1 = 5, DIE2 = 6, DIE3 = 7, DEAD = 8, FRAMES = 9;

        public static Image[] Ghoul, Fiend, Brute, Warden;
        public static Dictionary<char, Image> Items = new Dictionary<char, Image>();
        public static Image Barrel, Pillar, Corpse, BloodPool, Skulls, CeilLamp, TechLamp, BarrelDead;
        public static Image[] Torch = new Image[3];
        public static Image[] Fireball = new Image[2], Rocket = new Image[2], Explosion = new Image[6], Puff = new Image[3], Blood = new Image[3];
        public static Image[] Soulsphere = new Image[2];
        public static Image[] Keys = new Image[4];

        public static void Build()
        {
            Ghoul = new Image[FRAMES];
            for (int f = 0; f <= PAIN; f++) Ghoul[f] = DrawGhoul(f);
            MakeDeath(Ghoul, Col.Rgb(130, 10, 10), 1);
            Fiend = new Image[FRAMES];
            for (int f = 0; f <= PAIN; f++) Fiend[f] = DrawFiend(f);
            MakeDeath(Fiend, Col.Rgb(120, 20, 8), 2);
            Brute = new Image[FRAMES];
            for (int f = 0; f <= PAIN; f++) Brute[f] = DrawBrute(f);
            MakeDeath(Brute, Col.Rgb(140, 14, 20), 3);
            Warden = new Image[FRAMES];
            for (int f = 0; f <= PAIN; f++) Warden[f] = DrawWarden(f);
            MakeDeath(Warden, Col.Rgb(120, 16, 10), 4);

            BuildItems();
            BuildDecor();
            BuildEffects();
        }

        // ================================================================ helpers

        static int Sh(int c, float f) { return Col.Scale(c, f); }

        /// <summary>Resamples an image anchored at its bottom centre (used for death / squash frames).</summary>
        static Image Squash(Image src, int outW, float sx, float sy, float shear)
        {
            var dst = new Image(outW, src.H);
            float cxs = src.W * 0.5f, cxd = outW * 0.5f;
            for (int y = 0; y < src.H; y++)
                for (int x = 0; x < outW; x++)
                {
                    float fromBottom = src.H - (y + 0.5f);
                    float syf = src.H - fromBottom / sy;
                    float shx = shear * (fromBottom / src.H);
                    float sxf = (x + 0.5f - cxd - shx) / sx + cxs;
                    int ix = (int)Math.Floor(sxf), iy = (int)Math.Floor(syf);
                    if (ix < 0 || iy < 0 || ix >= src.W || iy >= src.H) continue;
                    dst.Px[y * outW + x] = src.Px[iy * src.W + ix];
                }
            return dst;
        }

        static void Splatter(Canvas c, int seed, int count, float x0, float y0, float x1, float y1, int col)
        {
            for (int i = 0; i < count; i++)
            {
                float x = x0 + Noise.Hashf(i, 1, seed) * (x1 - x0), y = y0 + Noise.Hashf(i, 2, seed) * (y1 - y0);
                float r = 0.6f + Noise.Hashf(i, 3, seed) * 1.4f;
                c.Ball(x, y, r, r * 0.9f, Col.Scale(col, 0.8f + Noise.Hashf(i, 4, seed) * 0.5f), false);
            }
        }

        static Image Paste(Canvas c, Image src, int ox, int oy)
        {
            for (int y = 0; y < src.H; y++)
                for (int x = 0; x < src.W; x++)
                {
                    int p = src.Px[y * src.W + x];
                    if ((p & Col.OPAQUE) != 0) c.Img.Set(ox + x, oy + y, p);
                }
            return c.Img;
        }

        /// <summary>Builds the death animation and corpse from the standing frame.</summary>
        static void MakeDeath(Image[] frames, int blood, int seed)
        {
            var baseImg = frames[WALK1];
            int W = baseImg.W, H = baseImg.H;
            int outW = (int)(W * 1.7f);
            float[] sy = { 0.86f, 0.55f, 0.32f, 0.2f };
            float[] sx = { 1.0f, 1.15f, 1.4f, 1.6f };
            float[] shear = { 6, 10, 6, 0 };
            float[] pool = { 0, 0.4f, 0.8f, 1.0f };
            for (int k = 0; k < 4; k++)
            {
                var sq = Squash(frames[k == 0 ? PAIN : WALK1], outW, sx[k], sy[k], shear[k]);
                var c = new Canvas(outW, H);
                c.NoiseSeed = seed * 13 + k;
                if (pool[k] > 0)
                {
                    float pw = W * 0.55f * pool[k] + 4, ph = 2.5f + 2.5f * pool[k];
                    c.Ball(outW * 0.5f, H - ph * 0.7f, pw, ph, Col.Scale(blood, 0.8f), false);
                    c.Ball(outW * 0.5f - 2, H - ph * 0.8f, pw * 0.6f, ph * 0.6f, Col.Scale(blood, 1.15f), false);
                }
                Paste(c, sq, 0, 0);
                Splatter(c, seed * 7 + k, 6 + k * 4, outW * 0.3f, H * (1 - sy[k]) + 2, outW * 0.7f, H - 2, blood);
                c.Outline(Col.Rgb(12, 6, 6));
                frames[DIE1 + k] = c.Done();
            }
        }

        // ================================================================ monsters

        /// <summary>Undead soldier with a rifle (hitscan).</summary>
        static Image DrawGhoul(int frame)
        {
            var c = new Canvas(44, 62);
            c.NoiseSeed = 11;
            int skin = Col.Rgb(150, 162, 126), vest = Col.Rgb(90, 102, 54), pants = Col.Rgb(64, 70, 54), boot = Col.Rgb(44, 36, 30);
            int helm = Col.Rgb(70, 78, 46), metal = Col.Rgb(58, 58, 64), wood = Col.Rgb(100, 64, 34), belt = Col.Rgb(60, 44, 28);
            float step = frame == WALK1 ? 1 : frame == WALK2 ? -1 : 0;
            float bob = step != 0 ? -1 : 0;
            bool aim = frame == AIM || frame == FIRE;
            float tilt = frame == PAIN ? 2.5f : 0;

            // legs (front view: the lifted foot rises and its knee turns out)
            float lUp = step > 0 ? 3 : 0, rUp = step < 0 ? 3 : 0;
            c.Limb(18, 38 + bob, 4.2f, 16 - lUp * 0.6f, 48 - lUp * 0.6f, 3.6f, pants);
            c.Limb(16 - lUp * 0.6f, 48 - lUp * 0.6f, 3.6f, 17, 57 - lUp, 3.0f, pants);
            c.Limb(26, 38 + bob, 4.2f, 28 + rUp * 0.6f, 48 - rUp * 0.6f, 3.6f, pants);
            c.Limb(28 + rUp * 0.6f, 48 - rUp * 0.6f, 3.6f, 27, 57 - rUp, 3.0f, pants);
            c.Ball(17, 58.5f - lUp, 4.2f, 2.8f, boot);
            c.Ball(27, 58.5f - rUp, 4.2f, 2.8f, boot);

            // torso
            float ty = bob;
            c.Ball(22, 30 + ty, 9.5f, 10.5f, vest);
            c.Rect(13, 36 + ty, 18, 3.2f, belt);
            c.Rect(20.5f, 36.3f + ty, 3, 2.6f, Col.Rgb(170, 150, 90));
            c.Rect(15, 25 + ty, 3, 6, Sh(vest, 0.7f));   // pouches
            c.Rect(26, 25 + ty, 3, 6, Sh(vest, 0.7f));
            Splatter(c, 17, 7, 16, 24 + ty, 28, 36 + ty, Col.Rgb(110, 12, 10));

            // head
            float hx = 22 + tilt, hy = 13 + ty;
            c.Limb(22, 21 + ty, 3, hx, 17 + ty, 2.6f, Sh(skin, 0.85f));
            c.Ball(hx, hy - 1, 7.2f, 5.5f, helm);
            c.Ball(hx, hy + 1.2f, 5.6f, 5.4f, skin);
            c.Rect(hx - 7, hy - 1.5f, 14, 1.4f, Sh(helm, 0.6f));
            c.Ball(hx - 2.3f, hy + 0.8f, 1.5f, 1.2f, Col.Rgb(255, 40, 20) | Col.EMISSIVE, false);
            c.Ball(hx + 2.3f, hy + 0.8f, 1.5f, 1.2f, Col.Rgb(255, 40, 20) | Col.EMISSIVE, false);
            c.Rect(hx - 2.5f, hy + 4, 5, 1.3f, Col.Rgb(40, 10, 10));
            c.Rect(hx - 1.5f, hy + 5.2f, 2, 1.4f, Col.Rgb(140, 14, 12));

            // arms and rifle
            if (!aim)
            {
                c.Limb(13, 24 + ty, 3.4f, 11, 32 + ty, 3, vest);
                c.Limb(11, 32 + ty, 3, 14, 38 + ty, 2.6f, skin);
                c.Limb(31, 24 + ty, 3.4f, 33, 32 + ty, 3, vest);
                c.Limb(33, 32 + ty, 3, 29, 33 + ty, 2.6f, skin);
                // rifle carried diagonally
                c.Line(10, 42 + ty, 34, 28 + ty, 3.2f, metal);
                c.Line(10, 42 + ty, 16, 38.5f + ty, 3.6f, wood);
                c.Line(28, 31.5f + ty, 36, 26.8f + ty, 1.6f, Sh(metal, 0.7f));
                c.Ball(14, 38 + ty, 2.4f, 2.2f, skin);
                c.Ball(29, 33 + ty, 2.4f, 2.2f, skin);
            }
            else
            {
                // aiming straight at the player: foreshortened rifle in front of the chest
                c.Limb(13, 24 + ty, 3.4f, 13, 31 + ty, 3, vest);
                c.Limb(13, 31 + ty, 3, 20, 32 + ty, 2.6f, skin);
                c.Limb(31, 24 + ty, 3.4f, 31, 31 + ty, 3, vest);
                c.Limb(31, 31 + ty, 3, 24, 30 + ty, 2.6f, skin);
                c.Rect(18, 26 + ty, 8, 9, metal);
                c.Ball(22, 27.5f + ty, 3.2f, 3.2f, Sh(metal, 1.3f));
                c.Ball(22, 27.5f + ty, 1.5f, 1.5f, Col.Rgb(10, 10, 12), false);
                c.Ball(20, 32 + ty, 2.4f, 2.2f, skin);
                c.Ball(24, 30 + ty, 2.4f, 2.2f, skin);
                if (frame == FIRE)
                {
                    c.Glow(22, 27.5f + ty, 8, Col.Rgb(255, 255, 210), Col.Rgb(255, 120, 20));
                    c.Glow(22, 27.5f + ty, 4, Col.Rgb(255, 255, 255), Col.Rgb(255, 230, 120));
                }
            }
            if (frame == PAIN) Splatter(c, 99, 10, 12, 8, 32, 24, Col.Rgb(170, 10, 10));
            c.Outline(Col.Rgb(14, 14, 10));
            return c.Done();
        }

        /// <summary>Spiked fire-throwing demon.</summary>
        static Image DrawFiend(int frame)
        {
            var c = new Canvas(48, 62);
            c.NoiseSeed = 21;
            c.NoiseAmt = 0.09f;
            int skin = Col.Rgb(150, 76, 48), dark = Col.Rgb(96, 44, 30), bone = Col.Rgb(226, 214, 176), claw = Col.Rgb(236, 226, 200);
            float step = frame == WALK1 ? 1 : frame == WALK2 ? -1 : 0;
            float bob = step != 0 ? -1 : 0;
            float tilt = frame == PAIN ? -2.5f : 0;
            float lUp = step > 0 ? 3 : 0, rUp = step < 0 ? 3 : 0;

            // digitigrade legs
            c.Limb(19, 38 + bob, 4.6f, 16 - lUp * 0.5f, 46 - lUp * 0.5f, 3.6f, skin);
            c.Limb(16 - lUp * 0.5f, 46 - lUp * 0.5f, 3.6f, 19, 53 - lUp, 2.6f, dark);
            c.Limb(19, 53 - lUp, 2.6f, 17, 58 - lUp, 2.4f, dark);
            c.Limb(29, 38 + bob, 4.6f, 32 + rUp * 0.5f, 46 - rUp * 0.5f, 3.6f, skin);
            c.Limb(32 + rUp * 0.5f, 46 - rUp * 0.5f, 3.6f, 29, 53 - rUp, 2.6f, dark);
            c.Limb(29, 53 - rUp, 2.6f, 31, 58 - rUp, 2.4f, dark);
            for (int k = -1; k <= 1; k++)
            {
                c.Limb(17, 58.5f - lUp, 1.2f, 17 + k * 2.5f, 60.5f - lUp, 0.8f, claw);
                c.Limb(31, 58.5f - rUp, 1.2f, 31 + k * 2.5f, 60.5f - rUp, 0.8f, claw);
            }

            // torso: muscular chest with ribs
            float ty = bob;
            c.Ball(24, 29 + ty, 10.5f, 11, skin);
            c.Ball(24, 38 + ty, 7.5f, 5, dark);
            for (int r = 0; r < 3; r++)
            {
                c.Line(18, 31 + r * 2.6f + ty, 22, 32 + r * 2.6f + ty, 1, Sh(dark, 0.8f));
                c.Line(30, 31 + r * 2.6f + ty, 26, 32 + r * 2.6f + ty, 1, Sh(dark, 0.8f));
            }
            c.Ball(20, 24.5f + ty, 3.8f, 3, Sh(skin, 1.12f));
            c.Ball(28, 24.5f + ty, 3.8f, 3, Sh(skin, 1.12f));

            // shoulder spikes
            float[] sp = { 13, 21, 11, 13, 16, 16, 35, 21, 37, 13, 32, 16 };
            for (int s = 0; s < sp.Length; s += 6)
            {
                c.Poly(new float[] { sp[s], sp[s + 1], sp[s + 2], sp[s + 3], sp[s + 4], sp[s + 5] }, bone, Sh(bone, 0.7f));
            }

            // head
            float hx = 24 + tilt, hy = 13 + ty;
            c.Ball(hx, hy, 6.5f, 6.8f, skin);
            c.Poly(new float[] { hx - 5, hy - 3, hx - 9, hy - 10, hx - 3, hy - 5 }, bone, Sh(bone, 0.8f));
            c.Poly(new float[] { hx + 5, hy - 3, hx + 9, hy - 10, hx + 3, hy - 5 }, bone, Sh(bone, 0.8f));
            c.Rect(hx - 5, hy - 1.8f, 10, 1.6f, Sh(dark, 0.7f));
            c.Ball(hx - 2.6f, hy + 0.5f, 1.8f, 1.2f, Col.Rgb(255, 200, 30) | Col.EMISSIVE, false);
            c.Ball(hx + 2.6f, hy + 0.5f, 1.8f, 1.2f, Col.Rgb(255, 200, 30) | Col.EMISSIVE, false);
            c.Ball(hx, hy + 4.6f, 3.4f, 1.8f, Col.Rgb(40, 8, 6), false);
            for (int t = 0; t < 4; t++) c.Rect(hx - 2.6f + t * 1.5f, hy + 3.5f, 0.9f, 1.4f, claw);

            // arms
            if (frame == AIM)
            {
                // right arm raised holding a fireball above the head
                c.Limb(34, 23 + ty, 3.6f, 38, 14 + ty, 3, skin);
                c.Limb(38, 14 + ty, 3, 36, 6 + ty, 2.6f, dark);
                c.Glow(36, 4.5f + ty, 5.5f, Col.Rgb(255, 250, 200), Col.Rgb(255, 90, 10));
                c.Limb(14, 23 + ty, 3.6f, 11, 32 + ty, 3, skin);
                c.Limb(11, 32 + ty, 3, 12, 40 + ty, 2.6f, dark);
            }
            else if (frame == FIRE)
            {
                // throwing: arm swings forward
                c.Limb(34, 23 + ty, 3.6f, 33, 29 + ty, 3.2f, skin);
                c.Limb(33, 29 + ty, 3.2f, 28, 30 + ty, 3.2f, dark);
                c.Glow(26, 29 + ty, 4, Col.Rgb(255, 230, 150), Col.Rgb(255, 80, 10));
                c.Limb(14, 23 + ty, 3.6f, 11, 32 + ty, 3, skin);
                c.Limb(11, 32 + ty, 3, 12, 40 + ty, 2.6f, dark);
            }
            else
            {
                float sw = step * 1.5f;
                c.Limb(14, 23 + ty, 3.6f, 10, 32 + ty + sw, 3, skin);
                c.Limb(10, 32 + ty + sw, 3, 11, 40 + ty + sw, 2.6f, dark);
                c.Limb(34, 23 + ty, 3.6f, 38, 32 + ty - sw, 3, skin);
                c.Limb(38, 32 + ty - sw, 3, 37, 40 + ty - sw, 2.6f, dark);
                for (int k = -1; k <= 1; k++)
                {
                    c.Limb(11, 41 + ty + sw, 1, 11 + k * 1.8f, 44 + ty + sw, 0.6f, claw);
                    c.Limb(37, 41 + ty - sw, 1, 37 + k * 1.8f, 44 + ty - sw, 0.6f, claw);
                }
            }
            if (frame == PAIN) Splatter(c, 98, 10, 14, 8, 34, 28, Col.Rgb(150, 20, 8));
            c.Outline(Col.Rgb(20, 8, 4));
            return c.Done();
        }

        /// <summary>Bulky charging demon with a huge jaw (melee).</summary>
        static Image DrawBrute(int frame)
        {
            var c = new Canvas(60, 58);
            c.NoiseSeed = 31;
            c.NoiseAmt = 0.08f;
            int skin = Col.Rgb(206, 110, 118), dark = Col.Rgb(140, 60, 70), teeth = Col.Rgb(240, 236, 214), gum = Col.Rgb(120, 20, 30);
            float step = frame == WALK1 ? 1 : frame == WALK2 ? -1 : 0;
            float bob = step != 0 ? -1.5f : 0;
            float lUp = step > 0 ? 3 : 0, rUp = step < 0 ? 3 : 0;
            float open = frame == AIM ? 0.5f : frame == FIRE ? 1f : 0.15f;

            // stubby legs
            c.Limb(20, 42 + bob, 6, 18, 52 - lUp, 4.5f, dark);
            c.Limb(40, 42 + bob, 6, 42, 52 - rUp, 4.5f, dark);
            c.Ball(18, 54.5f - lUp, 5, 3, Sh(dark, 0.7f));
            c.Ball(42, 54.5f - rUp, 5, 3, Sh(dark, 0.7f));
            for (int k = -1; k <= 1; k++)
            {
                c.Limb(18 + k * 2.5f, 56 - lUp, 1, 18 + k * 3.5f, 57.5f - lUp, 0.7f, teeth);
                c.Limb(42 + k * 2.5f, 56 - rUp, 1, 42 + k * 3.5f, 57.5f - rUp, 0.7f, teeth);
            }

            // hunched body
            float ty = bob;
            c.Ball(30, 32 + ty, 21, 15, skin);
            c.Ball(30, 22 + ty, 15, 10, Sh(skin, 1.05f));
            // veins
            c.Line(16, 30 + ty, 22, 38 + ty, 1, dark);
            c.Line(44, 30 + ty, 38, 38 + ty, 1, dark);
            // small arms
            c.Limb(12, 32 + ty, 4, 8, 40 + ty, 3, skin);
            c.Limb(48, 32 + ty, 4, 52, 40 + ty, 3, skin);
            for (int k = -1; k <= 1; k++)
            {
                c.Limb(8, 41 + ty, 1, 8 + k * 1.8f, 44 + ty, 0.7f, teeth);
                c.Limb(52, 41 + ty, 1, 52 + k * 1.8f, 44 + ty, 0.7f, teeth);
            }

            // face: small eyes, horns, huge mouth
            float my = 28 + ty;
            float mh = 4 + open * 9;
            c.Ball(30, my, 12, mh, gum, false);
            c.Ball(30, my + mh * 0.3f, 10, mh * 0.6f, Col.Rgb(50, 6, 10), false);
            for (int t = 0; t < 7; t++)
            {
                float tx = 20 + t * 3.3f;
                float curve = (float)Math.Abs(t - 3) * 0.8f;
                c.Poly(new float[] { tx - 1.3f, my - mh + curve, tx + 1.3f, my - mh + curve, tx, my - mh + 4.5f + curve }, teeth);
                c.Poly(new float[] { tx - 1.3f, my + mh - curve, tx + 1.3f, my + mh - curve, tx, my + mh - 4.5f - curve }, teeth);
            }
            float ey = 15 + ty - open * 2;
            c.Ball(23, ey, 2.2f, 1.6f, Col.Rgb(255, 220, 60) | Col.EMISSIVE, false);
            c.Ball(37, ey, 2.2f, 1.6f, Col.Rgb(255, 220, 60) | Col.EMISSIVE, false);
            c.Line(20, ey - 3, 26, ey - 1.5f, 1.2f, Sh(dark, 0.7f));
            c.Line(40, ey - 3, 34, ey - 1.5f, 1.2f, Sh(dark, 0.7f));
            c.Poly(new float[] { 19, 14 + ty, 13, 5 + ty, 22, 11 + ty }, Col.Rgb(80, 60, 50), Col.Rgb(40, 30, 26));
            c.Poly(new float[] { 41, 14 + ty, 47, 5 + ty, 38, 11 + ty }, Col.Rgb(80, 60, 50), Col.Rgb(40, 30, 26));
            if (frame == PAIN) Splatter(c, 97, 12, 16, 12, 44, 34, Col.Rgb(150, 10, 20));
            c.Outline(Col.Rgb(30, 8, 12));
            return c.Done();
        }

        /// <summary>The boss: an armoured cyber-demon with a rocket cannon arm.</summary>
        static Image DrawWarden(int frame)
        {
            var c = new Canvas(76, 104);
            c.NoiseSeed = 41;
            int armor = Col.Rgb(78, 80, 92), armorD = Col.Rgb(46, 48, 56), flesh = Col.Rgb(150, 70, 56), bone = Col.Rgb(214, 200, 164);
            float step = frame == WALK1 ? 1 : frame == WALK2 ? -1 : 0;
            float bob = step != 0 ? -2 : 0;
            float lUp = step > 0 ? 5 : 0, rUp = step < 0 ? 5 : 0;
            bool firing = frame == AIM || frame == FIRE;

            // legs: armoured
            c.Limb(28, 62 + bob, 8, 25, 80 - lUp * 0.6f, 6.5f, armor);
            c.Limb(25, 80 - lUp * 0.6f, 6.5f, 27, 96 - lUp, 5.5f, armorD);
            c.Limb(48, 62 + bob, 8, 51, 80 - rUp * 0.6f, 6.5f, flesh);
            c.Limb(51, 80 - rUp * 0.6f, 6.5f, 49, 96 - rUp, 5.5f, Sh(flesh, 0.8f));
            c.Ball(27, 98.5f - lUp, 8, 4.5f, armorD);
            c.Ball(49, 98.5f - rUp, 8, 4.5f, Sh(flesh, 0.6f));

            float ty = bob;
            // torso
            c.Ball(38, 46 + ty, 19, 19, flesh);
            c.Ball(38, 40 + ty, 16, 13, armor);
            c.Rect(25, 36 + ty, 26, 3, armorD);
            c.Rect(25, 44 + ty, 26, 2, armorD);
            c.Ball(38, 44 + ty, 4.5f, 4.5f, Col.Rgb(255, 60, 20) | Col.EMISSIVE, false);
            c.Ball(38, 44 + ty, 2.5f, 2.5f, Col.Rgb(255, 220, 120) | Col.EMISSIVE, false);
            c.Rect(28, 56 + ty, 20, 4, Col.Rgb(60, 40, 30));

            // head with horns and visor
            float hy = 20 + ty;
            c.Ball(38, hy, 10, 10, flesh);
            c.Ball(38, hy - 2, 10.5f, 7.5f, armor);
            c.Poly(new float[] { 30, hy - 4, 17, hy - 20, 22, hy - 22, 33, hy - 8 }, bone, Sh(bone, 0.6f));
            c.Poly(new float[] { 46, hy - 4, 59, hy - 20, 54, hy - 22, 43, hy - 8 }, bone, Sh(bone, 0.6f));
            c.Rect(30, hy - 2, 16, 3.4f, Col.Rgb(255, 30, 20) | Col.EMISSIVE);
            c.Rect(31, hy - 1.4f, 14, 1.2f, Col.Rgb(255, 200, 160) | Col.EMISSIVE);
            c.Ball(38, hy + 6, 6, 3, Col.Rgb(40, 10, 8), false);
            for (int t = 0; t < 5; t++) c.Rect(34.5f + t * 1.6f, hy + 4.8f, 1, 2, bone);

            // left arm: claw
            c.Limb(20, 34 + ty, 6, 12, 50 + ty, 5, flesh);
            c.Limb(12, 50 + ty, 5, 12, 62 + ty, 4, Sh(flesh, 0.85f));
            for (int k = -1; k <= 1; k++) c.Limb(12, 63 + ty, 1.6f, 12 + k * 3, 69 + ty, 1, bone);

            // right arm: rocket cannon
            c.Ball(57, 34 + ty, 7, 7, armor);
            if (!firing)
            {
                c.Limb(59, 38 + ty, 5.5f, 63, 54 + ty, 5, armorD);
                c.Tube(58, 52 + ty, 11, 20, armor, true);
                c.Ball(63.5f, 72 + ty, 5, 2.5f, armorD);
                c.Ball(63.5f, 72 + ty, 3, 1.5f, Col.Rgb(20, 20, 22), false);
            }
            else
            {
                // cannon pointed at the player
                c.Limb(59, 38 + ty, 5.5f, 56, 48 + ty, 5, armorD);
                c.Ball(55, 50 + ty, 9, 9, armor);
                c.Ball(55, 50 + ty, 6, 6, armorD);
                c.Ball(55, 50 + ty, 4, 4, Col.Rgb(16, 16, 18), false);
                if (frame == FIRE)
                {
                    c.Glow(55, 50 + ty, 12, Col.Rgb(255, 255, 220), Col.Rgb(255, 100, 20));
                    c.Glow(55, 50 + ty, 6, Col.Rgb(255, 255, 255), Col.Rgb(255, 230, 150));
                }
                else c.Glow(55, 50 + ty, 3, Col.Rgb(255, 160, 60), Col.Rgb(200, 40, 10));
            }
            if (frame == PAIN) Splatter(c, 96, 16, 22, 26, 56, 60, Col.Rgb(150, 16, 10));
            c.Outline(Col.Rgb(12, 10, 12));
            return c.Done();
        }

        // ================================================================ items

        static void BuildItems()
        {
            Canvas c;
            // stimpack
            c = new Canvas(12, 10);
            c.Rect(1, 2, 10, 7, Col.Rgb(230, 230, 226), Col.Rgb(160, 160, 156));
            c.Rect(5, 3, 2, 5, Col.Rgb(210, 20, 20));
            c.Rect(3.5f, 4.5f, 5, 2, Col.Rgb(210, 20, 20));
            c.Outline(Col.Rgb(30, 30, 30));
            Items['h'] = c.Done();
            // medkit
            c = new Canvas(22, 15);
            c.Rect(1, 3, 20, 11, Col.Rgb(236, 236, 232), Col.Rgb(150, 150, 146));
            c.Rect(1, 3, 20, 2, Col.Rgb(250, 250, 250));
            c.Rect(9, 5, 4, 8, Col.Rgb(220, 20, 20));
            c.Rect(6, 7.5f, 10, 3, Col.Rgb(220, 20, 20));
            c.Rect(8, 1, 6, 2.5f, Col.Rgb(60, 60, 60));
            c.Outline(Col.Rgb(30, 30, 30));
            Items['m'] = c.Done();
            // health bonus (small glowing flask)
            c = new Canvas(9, 13);
            c.Ball(4.5f, 8.5f, 3.8f, 3.8f, Col.Rgb(60, 120, 255));
            c.Rect(3.5f, 2, 2, 4, Col.Rgb(150, 190, 255));
            c.Rect(3, 1, 3, 1.5f, Col.Rgb(120, 80, 40));
            c.Glow(4.5f, 8.5f, 2, Col.Rgb(200, 230, 255), Col.Rgb(80, 140, 255));
            c.Outline(Col.Rgb(10, 16, 40));
            Items['+'] = c.Done();
            // soul sphere (2 frames)
            for (int f = 0; f < 2; f++)
            {
                c = new Canvas(22, 22);
                c.Glow(11, 11, 10.5f, f == 0 ? Col.Rgb(200, 230, 255) : Col.Rgb(170, 210, 255), Col.Rgb(20, 60, 200));
                c.Ball(8, 9, 1.6f, 1.2f, Col.Rgb(10, 20, 80) | Col.EMISSIVE, false);
                c.Ball(14, 9, 1.6f, 1.2f, Col.Rgb(10, 20, 80) | Col.EMISSIVE, false);
                c.Ball(11, 14.5f, 3, 1.4f + f * 0.6f, Col.Rgb(10, 20, 80) | Col.EMISSIVE, false);
                Soulsphere[f] = c.Done();
            }
            Items['o'] = Soulsphere[0];
            // armor bonus (small helmet)
            c = new Canvas(12, 9);
            c.Ball(6, 6, 5, 4.5f, Col.Rgb(70, 190, 70));
            c.Rect(1, 6.5f, 10, 1.6f, Col.Rgb(40, 110, 40));
            c.Ball(6, 5, 2, 1, Col.Rgb(180, 255, 180) | Col.EMISSIVE, false);
            c.Outline(Col.Rgb(10, 30, 10));
            Items['a'] = c.Done();
            // armor vests
            Items['G'] = Vest(Col.Rgb(60, 170, 60));
            Items['U'] = Vest(Col.Rgb(60, 110, 230));
            // bullets: clip and box
            c = new Canvas(7, 12);
            c.Rect(1, 1, 5, 10, Col.Rgb(70, 70, 74), Col.Rgb(40, 40, 44));
            c.Rect(2, 0, 3, 2, Col.Rgb(220, 180, 60));
            c.Outline(Col.Rgb(10, 10, 10));
            Items['c'] = c.Done();
            c = new Canvas(20, 13);
            c.Rect(1, 4, 18, 9, Col.Rgb(96, 104, 58), Col.Rgb(60, 66, 36));
            for (int i = 0; i < 6; i++) { c.Rect(2.5f + i * 2.6f, 1, 1.8f, 4, Col.Rgb(230, 190, 70)); c.Rect(2.5f + i * 2.6f, 0.5f, 1.8f, 1, Col.Rgb(180, 110, 40)); }
            c.Rect(1, 7, 18, 1.2f, Col.Rgb(50, 50, 30));
            c.Outline(Col.Rgb(10, 10, 6));
            Items['C'] = c.Done();
            // shells
            c = new Canvas(14, 8);
            for (int i = 0; i < 4; i++)
            {
                c.Rect(1 + i * 3.2f, 1, 2.6f, 5, Col.Rgb(210, 40, 30), Col.Rgb(140, 20, 16));
                c.Rect(1 + i * 3.2f, 5.5f, 2.6f, 1.8f, Col.Rgb(210, 180, 80));
            }
            c.Outline(Col.Rgb(20, 6, 4));
            Items['e'] = c.Done();
            c = new Canvas(22, 13);
            c.Rect(1, 3, 20, 10, Col.Rgb(170, 36, 26), Col.Rgb(110, 20, 16));
            c.Rect(1, 3, 20, 2, Col.Rgb(210, 60, 40));
            c.Rect(6, 6.5f, 10, 3.5f, Col.Rgb(230, 210, 150));
            c.Rect(7, 7.5f, 8, 1.5f, Col.Rgb(120, 20, 16));
            c.Outline(Col.Rgb(20, 6, 4));
            Items['E'] = c.Done();
            // rockets
            c = new Canvas(8, 22);
            c.Tube(2, 5, 4, 14, Col.Rgb(110, 116, 100), true);
            c.Poly(new float[] { 2, 5.5f, 4, 0.5f, 6, 5.5f }, Col.Rgb(200, 40, 30), Col.Rgb(140, 20, 16));
            c.Poly(new float[] { 0.5f, 21, 2, 16, 2, 21 }, Col.Rgb(80, 80, 84));
            c.Poly(new float[] { 7.5f, 21, 6, 16, 6, 21 }, Col.Rgb(80, 80, 84));
            c.Outline(Col.Rgb(12, 12, 12));
            Items['q'] = c.Done();
            c = new Canvas(24, 16);
            c.Rect(1, 5, 22, 11, Col.Rgb(100, 84, 54), Col.Rgb(66, 52, 32));
            for (int i = 0; i < 4; i++)
            {
                c.Tube(3 + i * 5, 0.5f, 3.4f, 6, Col.Rgb(110, 116, 100), true);
                c.Poly(new float[] { 3 + i * 5, 1.2f, 4.7f + i * 5, -1.5f, 6.4f + i * 5, 1.2f }, Col.Rgb(200, 40, 30));
            }
            c.Rect(1, 9, 22, 1.2f, Col.Rgb(40, 30, 20));
            c.Outline(Col.Rgb(12, 10, 8));
            Items['Q'] = c.Done();
            // weapon pickups (side views)
            Items['S'] = GunPickup(0);
            Items['N'] = GunPickup(1);
            Items['L'] = GunPickup(2);
            // keycards
            int[] kc = { 0, Col.Rgb(240, 40, 30), Col.Rgb(40, 110, 250), Col.Rgb(250, 210, 30) };
            char[] kch = { ' ', 'r', 'b', 'y' };
            for (int k = 1; k < 4; k++)
            {
                c = new Canvas(12, 16);
                c.Glow(6, 8, 7.5f, Col.Scale(kc[k], 0.9f), Col.Scale(kc[k], 0.15f));
                c.Rect(2, 2, 8, 12, Col.Rgb(230, 230, 230));
                c.Rect(2, 2, 8, 4, kc[k] | Col.EMISSIVE);
                c.Rect(3.5f, 8, 5, 1, Col.Rgb(60, 60, 60));
                c.Rect(3.5f, 10, 5, 1, Col.Rgb(60, 60, 60));
                c.Outline(Col.Rgb(20, 20, 20));
                Keys[k] = c.Done();
                Items[kch[k]] = Keys[k];
            }
        }

        static Image Vest(int col)
        {
            var c = new Canvas(24, 20);
            c.NoiseSeed = col & 255;
            c.Poly(new float[] { 4, 2, 9, 1, 12, 5, 15, 1, 20, 2, 22, 8, 20, 19, 4, 19, 2, 8 }, Col.Scale(col, 1.2f), Col.Scale(col, 0.6f));
            c.Poly(new float[] { 9, 1, 12, 5, 15, 1, 14, 0, 10, 0 }, Col.Rgb(40, 40, 40));
            c.Rect(5, 10, 14, 1.4f, Col.Scale(col, 0.45f));
            c.Rect(5, 14, 14, 1.4f, Col.Scale(col, 0.45f));
            c.Rect(11.4f, 5, 1.2f, 14, Col.Scale(col, 0.4f));
            c.Outline(Col.Rgb(10, 14, 10));
            return c.Done();
        }

        static Image GunPickup(int kind)
        {
            var c = new Canvas(36, 12);
            int metal = Col.Rgb(64, 64, 70), wood = Col.Rgb(120, 76, 40);
            if (kind == 0)
            {
                c.Tube(4, 3, 26, 2.4f, metal, false);
                c.Rect(12, 5.2f, 9, 2.6f, wood);
                c.Poly(new float[] { 24, 4, 34, 5, 34, 10, 26, 8 }, wood, Col.Scale(wood, 0.7f));
                c.Rect(21, 3.5f, 4, 4, metal);
            }
            else if (kind == 1)
            {
                for (int i = 0; i < 3; i++) c.Tube(2, 2 + i * 1.6f, 18, 1.6f, Col.Scale(metal, 1.1f), false);
                c.Rect(18, 1.5f, 12, 7, metal);
                c.Rect(22, 8.5f, 3, 3, Col.Rgb(40, 40, 44));
                c.Rect(29, 3, 5, 4, Col.Rgb(90, 90, 96));
            }
            else
            {
                c.Tube(2, 1.5f, 32, 6.5f, Col.Rgb(80, 96, 70), false);
                c.Rect(12, 8, 4, 4, Col.Rgb(50, 50, 54));
                c.Rect(20, 8, 3, 3.5f, Col.Rgb(50, 50, 54));
                c.Ball(3, 4.75f, 1.5f, 2.6f, Col.Rgb(20, 20, 20), false);
            }
            c.Outline(Col.Rgb(10, 10, 10));
            return c.Done();
        }

        // ================================================================ decorations

        static void BuildDecor()
        {
            Canvas c;
            // explosive barrel
            c = new Canvas(20, 26);
            c.NoiseSeed = 51;
            c.Tube(2, 3, 16, 22, Col.Rgb(70, 110, 60), true);
            c.Rect(2, 9, 16, 2, Col.Rgb(40, 60, 34));
            c.Rect(2, 18, 16, 2, Col.Rgb(40, 60, 34));
            c.Rect(5, 12, 10, 4, Col.Rgb(210, 180, 30));
            c.Line(7, 12.5f, 9, 15.5f, 1, Col.Rgb(30, 30, 20));
            c.Line(11, 12.5f, 13, 15.5f, 1, Col.Rgb(30, 30, 20));
            c.Ball(10, 3.5f, 8, 2.2f, Col.Rgb(90, 255, 60) | Col.EMISSIVE, false);
            c.Ball(10, 3.5f, 5, 1.2f, Col.Rgb(200, 255, 160) | Col.EMISSIVE, false);
            c.Outline(Col.Rgb(10, 16, 8));
            Barrel = c.Done();
            // stone pillar
            c = new Canvas(22, 66);
            c.NoiseSeed = 52;
            c.NoiseAmt = 0.1f;
            c.Tube(4, 6, 14, 54, Col.Rgb(130, 124, 112), true);
            c.Rect(1, 0, 20, 6, Col.Rgb(150, 144, 130), Col.Rgb(100, 96, 88));
            c.Rect(1, 60, 20, 6, Col.Rgb(140, 134, 120), Col.Rgb(90, 86, 78));
            for (int i = 0; i < 4; i++) c.Rect(6 + i * 3, 8, 1, 50, Col.Rgb(90, 86, 78));
            c.Outline(Col.Rgb(20, 18, 16));
            Pillar = c.Done();
            // tall tech lamp
            c = new Canvas(12, 50);
            c.Tube(4.5f, 12, 3, 36, Col.Rgb(90, 92, 100), true);
            c.Rect(2, 46, 8, 4, Col.Rgb(60, 60, 66));
            c.Rect(2, 2, 8, 11, Col.Rgb(60, 60, 66));
            c.Rect(3, 3, 6, 9, Col.Rgb(230, 240, 255) | Col.EMISSIVE);
            c.Rect(4, 4, 2, 7, Col.Rgb(255, 255, 255) | Col.EMISSIVE);
            c.Outline(Col.Rgb(14, 14, 16));
            TechLamp = c.Done();
            // hanging ceiling lamp
            c = new Canvas(18, 14);
            c.Rect(8.5f, 0, 1, 5, Col.Rgb(50, 50, 50));
            c.Poly(new float[] { 3, 10, 6, 5, 12, 5, 15, 10 }, Col.Rgb(90, 90, 96), Col.Rgb(50, 50, 56));
            c.Ball(9, 10.5f, 5, 2.5f, Col.Rgb(255, 245, 210) | Col.EMISSIVE, false);
            CeilLamp = c.Done();
            // burning torch stand (3 flame frames)
            for (int f = 0; f < 3; f++)
            {
                c = new Canvas(14, 48);
                c.NoiseSeed = 60 + f;
                c.Tube(5.5f, 16, 3, 30, Col.Rgb(80, 60, 44), true);
                c.Rect(2, 44, 10, 4, Col.Rgb(60, 50, 40));
                c.Poly(new float[] { 1, 13, 13, 13, 10, 18, 4, 18 }, Col.Rgb(110, 100, 90), Col.Rgb(60, 56, 50));
                float fl = f * 1.3f;
                c.Glow(7, 9 - fl * 0.3f, 6.5f + (f == 1 ? 0.8f : 0), Col.Rgb(255, 230, 120), Col.Rgb(230, 60, 10));
                c.Glow(7 + (f - 1) * 1.2f, 4 - f * 0.5f, 3.5f, Col.Rgb(255, 250, 200), Col.Rgb(255, 120, 20));
                c.Glow(5 - f * 0.6f, 2 + (f % 2), 1.6f, Col.Rgb(255, 200, 100), Col.Rgb(255, 80, 10));
                Torch[f] = c.Done();
            }
            // dead marine
            c = new Canvas(46, 14);
            c.NoiseSeed = 70;
            c.Ball(23, 12, 20, 2.5f, Col.Rgb(120, 10, 10), false);
            c.Limb(8, 9, 3.5f, 20, 8, 4.5f, Col.Rgb(60, 110, 50));
            c.Limb(20, 8, 4.5f, 32, 9, 3.5f, Col.Rgb(70, 72, 60));
            c.Limb(32, 9, 3, 41, 10, 2.5f, Col.Rgb(70, 72, 60));
            c.Ball(6, 8, 3.5f, 3, Col.Rgb(60, 110, 50));
            Splatter(c, 71, 8, 6, 5, 30, 12, Col.Rgb(140, 12, 10));
            c.Outline(Col.Rgb(12, 8, 6));
            Corpse = c.Done();
            // blood pool
            c = new Canvas(28, 6);
            c.Ball(14, 3.5f, 13, 2.4f, Col.Rgb(110, 8, 8), false);
            c.Ball(12, 3.3f, 8, 1.4f, Col.Rgb(150, 16, 14), false);
            BloodPool = c.Done();
            // skull pile
            c = new Canvas(22, 14);
            c.NoiseSeed = 72;
            float[] sk = { 6, 10, 16, 10, 11, 6, 3, 12, 19, 12 };
            for (int i = 0; i < sk.Length; i += 2)
            {
                c.Ball(sk[i], sk[i + 1], 3.6f, 3.2f, Col.Rgb(214, 200, 168));
                c.Ball(sk[i] - 1.2f, sk[i + 1] - 0.2f, 0.9f, 1, Col.Rgb(20, 10, 8), false);
                c.Ball(sk[i] + 1.2f, sk[i + 1] - 0.2f, 0.9f, 1, Col.Rgb(20, 10, 8), false);
            }
            c.Outline(Col.Rgb(20, 14, 10));
            Skulls = c.Done();
            // exploded barrel remains
            c = new Canvas(20, 10);
            c.Ball(10, 8, 9, 2.5f, Col.Rgb(30, 60, 20), false);
            c.Tube(4, 3, 12, 6, Col.Rgb(50, 70, 44), true);
            c.Outline(Col.Rgb(8, 10, 6));
            BarrelDead = c.Done();
        }

        // ================================================================ effects

        static void BuildEffects()
        {
            Canvas c;
            for (int f = 0; f < 2; f++)
            {
                c = new Canvas(14, 14);
                c.Glow(7, 7, 6.8f, Col.Rgb(255, 250, 200), f == 0 ? Col.Rgb(255, 80, 10) : Col.Rgb(230, 40, 10));
                c.Glow(7 + (f == 0 ? -1 : 1), 6, 3, Col.Rgb(255, 255, 240), Col.Rgb(255, 200, 80));
                Fireball[f] = c.Done();
                c = new Canvas(10, 10);
                c.Ball(5, 5, 4, 4, Col.Rgb(120, 120, 110));
                c.Glow(5, 5, 3 + f * 0.6f, Col.Rgb(255, 255, 220), Col.Rgb(255, 140, 30));
                Rocket[f] = c.Done();
            }
            for (int f = 0; f < 6; f++)
            {
                c = new Canvas(40, 40);
                c.NoiseSeed = 80 + f;
                float r = 7 + f * 3.2f;
                float heat = 1 - f / 6f;
                if (f >= 3)
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * 0.9f + f;
                        c.Ball(20 + (float)Math.Cos(a) * r * 0.5f, 20 + (float)Math.Sin(a) * r * 0.5f - f, r * 0.45f, r * 0.45f,
                            Col.Scale(Col.Rgb(60, 50, 46), 0.6f + 0.1f * k % 3), true);
                    }
                c.Glow(20, 20, r * (0.5f + heat * 0.5f), Col.Lerp(Col.Rgb(255, 120, 30), Col.Rgb(255, 255, 220), heat),
                    Col.Lerp(Col.Rgb(90, 20, 5), Col.Rgb(255, 70, 10), heat));
                for (int k = 0; k < 5; k++)
                {
                    float a = k * 1.3f + f * 0.7f;
                    float rr = r * 0.55f;
                    c.Glow(20 + (float)Math.Cos(a) * rr, 20 + (float)Math.Sin(a) * rr, r * 0.35f * heat + 1,
                        Col.Rgb(255, 230, 140), Col.Rgb(240, 80, 10));
                }
                Explosion[f] = c.Done();
            }
            for (int f = 0; f < 3; f++)
            {
                c = new Canvas(10, 10);
                c.Ball(5, 5 - f, 2 + f * 1.2f, 2 + f * 1.2f, Col.Rgb(150 - f * 20, 146 - f * 20, 140 - f * 20));
                if (f == 0) c.Glow(5, 5, 2.2f, Col.Rgb(255, 255, 200), Col.Rgb(255, 180, 60));
                Puff[f] = c.Done();
                c = new Canvas(8, 8);
                c.Ball(4, 4 + f, 2.2f - f * 0.4f, 2.2f - f * 0.4f, Col.Rgb(180, 10, 10));
                c.Ball(2 + f, 2 + f * 1.5f, 1, 1, Col.Rgb(140, 8, 8), false);
                c.Ball(6 - f, 3 + f * 1.5f, 1, 1, Col.Rgb(140, 8, 8), false);
                Blood[f] = c.Done();
            }
        }

        /// <summary>All sprites, for the developer sprite sheet.</summary>
        public static List<Image> All()
        {
            var l = new List<Image>();
            l.AddRange(Ghoul); l.AddRange(Fiend); l.AddRange(Brute); l.AddRange(Warden);
            foreach (var kv in Items) l.Add(kv.Value);
            l.Add(Barrel); l.Add(Pillar); l.Add(TechLamp); l.Add(CeilLamp); l.AddRange(Torch); l.Add(Corpse); l.Add(BloodPool); l.Add(Skulls); l.Add(BarrelDead);
            l.AddRange(Fireball); l.AddRange(Rocket); l.AddRange(Explosion); l.AddRange(Puff); l.AddRange(Blood);
            return l;
        }
    }
}
