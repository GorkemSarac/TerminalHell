// TERMINAL HELL - every sound effect is synthesized at startup (noise, oscillators, filters, formants).
using System;

namespace TerminalHell
{
    static class SfxBank
    {
        const int R = Audio.Rate;
        static uint seed = 22222;

        static float Rnd()
        {
            seed = seed * 1664525 + 1013904223;
            return ((seed >> 8) & 0xFFFF) / 32768f - 1f;
        }

        static float[] Buf(float sec) { return new float[(int)(sec * R) + 2]; }

        /// <summary>State variable filter (returns band-pass, low-pass or high-pass).</summary>
        sealed class Svf
        {
            float low, band;
            public float Process(float x, float cutoff, float q, int mode)
            {
                // this filter blows up (NaN) when f gets close to 2 - q: keep it well inside the stable range
                float f = (float)(2 * Math.Sin(Math.PI * Math.Min(cutoff, R * 0.16f) / R));
                low += f * band;
                float high = x - low - q * band;
                band += f * high;
                return mode == 0 ? band : mode == 1 ? low : high;
            }
        }

        static void Normalize(float[] b, float peak)
        {
            // never let a NaN / infinity reach the mixer: it would silence every other sound
            for (int i = 0; i < b.Length; i++) if (!(b[i] > -1e6f && b[i] < 1e6f)) b[i] = 0;
            float m = 0.0001f;
            foreach (var v in b) m = Math.Max(m, Math.Abs(v));
            for (int i = 0; i < b.Length; i++) b[i] *= peak / m;
            // tiny fade at the very end to avoid clicks
            int fade = Math.Min(64, b.Length);
            for (int i = 0; i < fade; i++) b[b.Length - 1 - i] *= i / (float)fade;
        }

        static float Env(float t, float attack, float decay)
        {
            if (t < attack) return t / attack;
            return (float)Math.Exp(-(t - attack) / decay);
        }

        static float Dist(float x, float drive) { return (float)Math.Tanh(x * drive); }

        /// <summary>Filtered noise burst with a falling cutoff.</summary>
        static void NoiseBurst(float[] b, float start, float amp, float c0, float c1, float attack, float decay, float len)
        {
            var f = new Svf();
            int s0 = (int)(start * R), n = (int)(len * R);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / R;
                float c = c0 + (c1 - c0) * Math.Min(1, t / len);
                b[s0 + i] += f.Process(Rnd(), c, 0.7f, 1) * amp * Env(t, attack, decay);
            }
        }

        /// <summary>Sine with exponential pitch sweep, used for thumps and booms.</summary>
        static void Thump(float[] b, float start, float amp, float f0, float f1, float decay, float len)
        {
            int s0 = (int)(start * R), n = (int)(len * R);
            double ph = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / R;
                double f = f1 + (f0 - f1) * Math.Exp(-t / (len * 0.3));
                ph += 2 * Math.PI * f / R;
                b[s0 + i] += (float)Math.Sin(ph) * amp * Env(t, 0.002f, decay);
            }
        }

        static void Tone(float[] b, float start, float amp, float freq, float len, int wave, float decay)
        {
            int s0 = (int)(start * R), n = (int)(len * R);
            double ph = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / R;
                ph += freq / R;
                double p = ph - Math.Floor(ph);
                float v = wave == 0 ? (float)Math.Sin(p * 2 * Math.PI) : wave == 1 ? (p < 0.5 ? 1 : -1) : (float)(p * 2 - 1);
                b[s0 + i] += v * amp * Env(t, 0.003f, decay) * Math.Min(1, (n - i) / 80f);
            }
        }

        /// <summary>
        /// A crude "voice": a buzzy source with pitch contour through two formant filters.
        /// Good enough for grunts, groans, roars and screeches.
        /// </summary>
        static float[] Voice(float len, float f0, float f1, float fm1, float fm2, float noise, float vibrato, float drive, float attack, float decay, float rough)
        {
            var b = Buf(len);
            var a = new Svf(); var c = new Svf(); var lp = new Svf();
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / R;
                float u = t / len;
                float f = f0 + (f1 - f0) * u;
                f *= 1 + (float)Math.Sin(t * 2 * Math.PI * 7) * vibrato + Rnd() * rough;
                ph += f / R;
                float saw = (float)((ph - Math.Floor(ph)) * 2 - 1);
                float src = saw * (1 - noise) + Rnd() * noise;
                float v = a.Process(src, fm1 * (1 - 0.2f * u), 0.25f, 0) + 0.7f * c.Process(src, fm2, 0.3f, 0);
                v = lp.Process(v, 3000, 0.7f, 1);
                b[i] = Dist(v * drive, 1) * Env(t, attack, decay);
            }
            Normalize(b, 0.9f);
            return b;
        }

        public static float[][] Build()
        {
            var bank = new float[(int)Sfx.Count][];
            float[] b;

            // ---- weapons
            b = Buf(0.35f);
            NoiseBurst(b, 0, 1.0f, 5000, 900, 0.001f, 0.045f, 0.3f);
            Thump(b, 0, 0.9f, 160, 45, 0.06f, 0.2f);
            for (int i = 0; i < b.Length; i++) b[i] = Dist(b[i], 2.2f);
            Normalize(b, 0.8f);
            bank[(int)Sfx.Pistol] = b;

            b = Buf(0.28f);
            NoiseBurst(b, 0, 1.0f, 6000, 1200, 0.001f, 0.035f, 0.25f);
            Thump(b, 0, 0.8f, 190, 60, 0.05f, 0.15f);
            for (int i = 0; i < b.Length; i++) b[i] = Dist(b[i], 2.5f);
            Normalize(b, 0.7f);
            bank[(int)Sfx.Chaingun] = b;

            b = Buf(0.28f);
            NoiseBurst(b, 0, 1.0f, 4200, 900, 0.001f, 0.05f, 0.25f);
            Thump(b, 0, 0.7f, 140, 50, 0.07f, 0.2f);
            for (int i = 0; i < b.Length; i++) b[i] = Dist(b[i], 2f);
            Normalize(b, 0.65f);
            bank[(int)Sfx.GhoulShot] = b;

            b = Buf(1.0f);
            NoiseBurst(b, 0, 1.2f, 4000, 350, 0.001f, 0.14f, 0.55f);
            Thump(b, 0, 1.2f, 110, 32, 0.16f, 0.45f);
            // pump: two clicks
            NoiseBurst(b, 0.55f, 0.35f, 2500, 1500, 0.001f, 0.012f, 0.05f);
            NoiseBurst(b, 0.68f, 0.4f, 1800, 1200, 0.001f, 0.015f, 0.05f);
            for (int i = 0; i < b.Length; i++) b[i] = Dist(b[i], 2.4f);
            Normalize(b, 0.9f);
            bank[(int)Sfx.Shotgun] = b;

            b = Buf(0.9f);
            {
                var f = new Svf();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    float c = 300 + 1600 * Math.Min(1, t / 0.5f);
                    b[i] = f.Process(Rnd(), c, 0.3f, 0) * Env(t, 0.02f, 0.3f);
                }
                Thump(b, 0, 0.5f, 90, 40, 0.1f, 0.3f);
                Normalize(b, 0.75f);
            }
            bank[(int)Sfx.Rocket] = b;

            b = Buf(1.6f);
            NoiseBurst(b, 0, 1.4f, 1400, 120, 0.002f, 0.35f, 1.5f);
            Thump(b, 0, 1.4f, 90, 28, 0.35f, 1.0f);
            for (int k = 0; k < 40; k++)
            {
                float t = 0.02f + (Rnd() * 0.5f + 0.5f) * 0.6f;
                NoiseBurst(b, t, 0.3f * (1 - t), 3000, 1500, 0.001f, 0.01f, 0.03f);
            }
            for (int i = 0; i < b.Length; i++) b[i] = Dist(b[i], 1.8f);
            Normalize(b, 0.95f);
            bank[(int)Sfx.Explode] = b;

            b = Buf(0.5f);
            NoiseBurst(b, 0, 0.8f, 1500, 500, 0.002f, 0.12f, 0.45f);
            Thump(b, 0, 0.8f, 120, 40, 0.12f, 0.3f);
            Normalize(b, 0.7f);
            bank[(int)Sfx.FireHit] = b;

            b = Buf(0.2f);
            Thump(b, 0, 1, 170, 60, 0.05f, 0.15f);
            NoiseBurst(b, 0, 0.5f, 2000, 400, 0.001f, 0.02f, 0.06f);
            Normalize(b, 0.8f);
            bank[(int)Sfx.Punch] = b;

            b = Buf(0.22f);
            {
                var f = new Svf();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    b[i] = f.Process(Rnd(), 600 + 2400 * t / 0.22f, 0.5f, 0) * (float)Math.Sin(Math.PI * t / 0.22f);
                }
                Normalize(b, 0.4f);
            }
            bank[(int)Sfx.Swing] = b;

            b = Buf(0.12f);
            NoiseBurst(b, 0, 0.6f, 3000, 2000, 0.001f, 0.008f, 0.03f);
            NoiseBurst(b, 0.05f, 0.4f, 2000, 1500, 0.001f, 0.01f, 0.04f);
            Normalize(b, 0.5f);
            bank[(int)Sfx.DryFire] = b;

            // the ray gun: a rising whine that snaps into a discharge
            b = Buf(0.6f);
            {
                var f = new Svf();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    float ph = 2 * (float)Math.PI * (220 + 900 * t) * t;
                    float tone = (float)(Math.Sin(ph) + 0.5 * Math.Sin(ph * 1.5 + Math.Sin(ph * 0.5)));
                    float body = f.Process(Rnd() * 0.6f + tone, 900 + 2600 * Math.Min(1, t / 0.12f), 0.6f, 1);
                    b[i] = body * Env(t, 0.004f, 0.11f);
                }
                Thump(b, 0.01f, 0.7f, 150, 40, 0.05f, 0.22f);
                for (int i = 0; i < b.Length; i++) b[i] = Dist(b[i], 2.0f);
                Normalize(b, 0.8f);
            }
            bank[(int)Sfx.RayGun] = b;

            // parry: a hard metal clang with a short ring
            b = Buf(0.5f);
            {
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    float ring = 0;
                    float[] partials = { 1840, 2790, 3960, 5210 };
                    for (int k = 0; k < partials.Length; k++)
                        ring += (float)Math.Sin(2 * Math.PI * partials[k] * t) * (float)Math.Exp(-t / (0.09f - k * 0.015f)) / (k + 1);
                    b[i] = ring * 0.6f;
                }
                NoiseBurst(b, 0, 0.8f, 6000, 1800, 0.0005f, 0.012f, 0.06f);
                Thump(b, 0, 0.5f, 260, 90, 0.02f, 0.08f);
                Normalize(b, 0.8f);
            }
            bank[(int)Sfx.Parry] = b;

            // jump and landing: cloth and boots, no voice
            b = Buf(0.3f);
            NoiseBurst(b, 0, 0.5f, 900, 2600, 0.02f, 0.06f, 0.25f);
            Normalize(b, 0.35f);
            bank[(int)Sfx.Jump] = b;

            b = Buf(0.25f);
            Thump(b, 0, 0.9f, 120, 45, 0.01f, 0.09f);
            NoiseBurst(b, 0, 0.5f, 1800, 500, 0.001f, 0.03f, 0.12f);
            Normalize(b, 0.5f);
            bank[(int)Sfx.Land] = b;

            b = Buf(0.45f);
            NoiseBurst(b, 0, 0.5f, 2500, 1500, 0.001f, 0.01f, 0.04f);
            NoiseBurst(b, 0.18f, 0.6f, 1800, 900, 0.001f, 0.02f, 0.06f);
            Thump(b, 0.18f, 0.4f, 200, 80, 0.04f, 0.1f);
            Normalize(b, 0.5f);
            bank[(int)Sfx.WeaponUp] = b;

            // ---- doors / world
            bank[(int)Sfx.DoorOpen] = Machine(0.9f, 70, false);
            bank[(int)Sfx.DoorClose] = Machine(0.9f, 60, true);

            b = Buf(1.3f);
            {
                var f = new Svf();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    float mod = 0.6f + 0.4f * (float)Math.Sin(t * 37) * (float)Math.Sin(t * 13);
                    b[i] = f.Process(Rnd(), 260, 0.5f, 1) * mod * Math.Min(1, t * 20) * Math.Min(1, (1.3f - t) * 5);
                }
                Normalize(b, 0.8f);
            }
            bank[(int)Sfx.PushWall] = b;

            b = Buf(0.35f);
            NoiseBurst(b, 0, 0.6f, 3000, 2000, 0.001f, 0.01f, 0.03f);
            Thump(b, 0.04f, 0.8f, 150, 70, 0.08f, 0.2f);
            NoiseBurst(b, 0.12f, 0.5f, 2200, 1800, 0.001f, 0.012f, 0.04f);
            Normalize(b, 0.7f);
            bank[(int)Sfx.Switch] = b;

            b = Buf(0.35f);
            Tone(b, 0, 0.5f, 110, 0.3f, 1, 0.2f);
            Tone(b, 0, 0.3f, 116, 0.3f, 2, 0.2f);
            Normalize(b, 0.55f);
            bank[(int)Sfx.NoWay] = b;

            b = Buf(0.25f);
            {
                var f = new Svf();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    b[i] = f.Process(Rnd(), 5000, 0.5f, 2) * Env(t, 0.005f, 0.06f) * (0.6f + 0.4f * Rnd());
                }
                Normalize(b, 0.4f);
            }
            bank[(int)Sfx.HurtFloor] = b;

            // ---- pickups / ui
            b = Buf(0.16f);
            Tone(b, 0, 0.5f, 988, 0.06f, 1, 0.05f);
            Tone(b, 0.06f, 0.5f, 1480, 0.09f, 1, 0.06f);
            Normalize(b, 0.4f);
            bank[(int)Sfx.Pickup] = b;

            b = Buf(0.5f);
            {
                float[] notes = { 392, 523, 659, 784, 1046 };
                for (int k = 0; k < notes.Length; k++) Tone(b, k * 0.05f, 0.5f, notes[k], 0.2f, 1, 0.08f);
                Normalize(b, 0.45f);
            }
            bank[(int)Sfx.WeaponPickup] = b;

            b = Buf(0.9f);
            Tone(b, 0, 0.5f, 1046, 0.8f, 0, 0.3f);
            Tone(b, 0, 0.35f, 1568, 0.8f, 0, 0.25f);
            Tone(b, 0.12f, 0.35f, 2093, 0.7f, 0, 0.2f);
            Normalize(b, 0.5f);
            bank[(int)Sfx.KeyPickup] = b;

            b = Buf(1.0f);
            {
                double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    float f = 300 + 1300 * t + (float)Math.Sin(t * 60) * 40;
                    ph += f / R;
                    b[i] = (float)Math.Sin(ph * 2 * Math.PI) * 0.6f * Env(t, 0.05f, 0.5f) + (float)Math.Sin(ph * 4 * Math.PI) * 0.25f * Env(t, 0.1f, 0.4f);
                }
                Normalize(b, 0.55f);
            }
            bank[(int)Sfx.PowerUp] = b;

            b = Buf(0.9f);
            {
                float[] notes = { 523, 659, 784, 1046, 784, 1046, 1318 };
                for (int k = 0; k < notes.Length; k++) Tone(b, k * 0.08f, 0.5f, notes[k], 0.25f, 1, 0.12f);
                Normalize(b, 0.45f);
            }
            bank[(int)Sfx.Secret] = b;

            b = Buf(0.05f);
            Tone(b, 0, 0.6f, 660, 0.04f, 1, 0.02f);
            Normalize(b, 0.3f);
            bank[(int)Sfx.MenuMove] = b;

            b = Buf(0.2f);
            Tone(b, 0, 0.6f, 440, 0.08f, 1, 0.05f);
            Tone(b, 0.07f, 0.6f, 880, 0.1f, 1, 0.06f);
            Normalize(b, 0.4f);
            bank[(int)Sfx.MenuSelect] = b;

            // ---- voices
            bank[(int)Sfx.PlayerPain] = Voice(0.3f, 150, 95, 650, 1150, 0.15f, 0.02f, 3, 0.01f, 0.12f, 0.02f);
            bank[(int)Sfx.PlayerDeath] = Voice(1.3f, 190, 55, 700, 1200, 0.2f, 0.04f, 4, 0.02f, 0.5f, 0.05f);
            bank[(int)Sfx.GhoulSight] = Voice(0.9f, 95, 75, 380, 820, 0.25f, 0.06f, 4, 0.08f, 0.45f, 0.08f);
            bank[(int)Sfx.GhoulPain] = Voice(0.25f, 120, 90, 450, 900, 0.3f, 0.02f, 4, 0.01f, 0.1f, 0.05f);
            bank[(int)Sfx.GhoulDeath] = Voice(0.8f, 110, 45, 400, 750, 0.35f, 0.08f, 5, 0.01f, 0.35f, 0.12f);
            bank[(int)Sfx.FiendSight] = Voice(0.7f, 320, 520, 1300, 2500, 0.35f, 0.08f, 5, 0.03f, 0.3f, 0.1f);
            bank[(int)Sfx.FiendPain] = Voice(0.25f, 380, 300, 1200, 2300, 0.35f, 0.03f, 5, 0.01f, 0.1f, 0.08f);
            bank[(int)Sfx.FiendDeath] = Voice(0.9f, 420, 120, 1100, 2100, 0.4f, 0.1f, 5, 0.01f, 0.4f, 0.1f);
            bank[(int)Sfx.BruteSight] = Voice(1.0f, 70, 55, 300, 600, 0.45f, 0.05f, 6, 0.05f, 0.5f, 0.15f);
            bank[(int)Sfx.BrutePain] = Voice(0.3f, 85, 70, 320, 640, 0.45f, 0.03f, 6, 0.01f, 0.12f, 0.12f);
            bank[(int)Sfx.BruteDeath] = Voice(1.1f, 80, 35, 300, 560, 0.5f, 0.08f, 6, 0.01f, 0.5f, 0.15f);
            bank[(int)Sfx.BossSight] = Voice(1.6f, 50, 40, 220, 480, 0.4f, 0.04f, 7, 0.1f, 0.8f, 0.1f);
            bank[(int)Sfx.BossPain] = Voice(0.4f, 60, 50, 240, 500, 0.45f, 0.03f, 7, 0.01f, 0.15f, 0.1f);
            bank[(int)Sfx.BossDeath] = Voice(2.5f, 55, 20, 230, 470, 0.5f, 0.06f, 7, 0.02f, 1.2f, 0.12f);
            bank[(int)Sfx.Growl] = Voice(0.6f, 70, 65, 320, 700, 0.5f, 0.1f, 5, 0.1f, 0.3f, 0.2f);

            b = Voice(0.45f, 330, 280, 1200, 2400, 0.6f, 0.05f, 4, 0.01f, 0.2f, 0.2f);
            {
                // fireball throw: add a whoosh
                var f = new Svf();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / R;
                    b[i] = b[i] * 0.5f + f.Process(Rnd(), 400 + 2000 * t, 0.4f, 0) * Env(t, 0.03f, 0.15f) * 1.5f;
                }
                Normalize(b, 0.7f);
            }
            bank[(int)Sfx.FiendThrow] = b;

            b = Buf(0.45f);
            for (int k = 0; k < 3; k++) NoiseBurst(b, k * 0.07f, 0.7f, 2500, 800, 0.001f, 0.03f, 0.08f);
            Thump(b, 0, 0.6f, 120, 50, 0.1f, 0.3f);
            Normalize(b, 0.8f);
            bank[(int)Sfx.BruteBite] = b;

            b = Buf(0.6f);
            Thump(b, 0, 1.0f, 80, 30, 0.12f, 0.4f);
            NoiseBurst(b, 0, 0.5f, 900, 200, 0.001f, 0.08f, 0.3f);
            Tone(b, 0.01f, 0.25f, 310, 0.4f, 2, 0.1f);
            Tone(b, 0.01f, 0.2f, 467, 0.4f, 1, 0.08f);
            Normalize(b, 0.9f);
            bank[(int)Sfx.BossStep] = b;

            return bank;
        }

        static float[] Machine(float len, float hum, bool closing)
        {
            var b = Buf(len);
            var f = new Svf();
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / R;
                ph += hum * (1 + 0.1f * t) / R;
                float sq = (ph - Math.Floor(ph)) < 0.5 ? 0.5f : -0.5f;
                float n = f.Process(Rnd(), 500, 0.6f, 1);
                float env = Math.Min(1, t * 12) * Math.Min(1, (len - t) * 6);
                b[i] = (sq * 0.4f + n * 0.9f) * env * 0.7f;
            }
            NoiseBurst(b, 0, 0.6f, 1500, 600, 0.001f, 0.03f, 0.1f);
            if (closing)
            {
                Thump(b, len - 0.22f, 1.2f, 110, 40, 0.1f, 0.2f);
                NoiseBurst(b, len - 0.22f, 0.8f, 1200, 300, 0.001f, 0.06f, 0.2f);
            }
            Normalize(b, 0.6f);
            return b;
        }
    }
}
