// TERMINAL HELL - real-time stereo software mixer. The platform's AudioDevice pulls mixed blocks from it:
//   Windows: AudioWin.cs (waveOut)
//   Linux  : linux/src/AudioLinux.cs (PulseAudio / PipeWire, or ALSA)
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    enum Sfx
    {
        Pistol, Shotgun, Chaingun, Rocket, Explode, Punch, Swing, DoorOpen, DoorClose, Pickup, WeaponPickup, KeyPickup, PowerUp,
        PlayerPain, PlayerDeath, GhoulSight, GhoulPain, GhoulDeath, GhoulShot, FiendSight, FiendPain, FiendDeath, FiendThrow,
        FireHit, BruteSight, BrutePain, BruteDeath, BruteBite, BossSight, BossPain, BossDeath, BossStep, Switch, NoWay,
        PushWall, MenuMove, MenuSelect, DryFire, HurtFloor, Secret, WeaponUp, Growl, Jump, Land, Parry, RayGun, Count
    }

    static class Audio
    {
        public const int Rate = 22050;
        public const int Frames = 384;       // per mixed block (~17 ms)

        sealed class Voice
        {
            public float[] Data;
            public double Pos, Step;
            public float L, R;
            public bool Active;
            public int Owner;
        }

        static readonly object sync = new object();
        static readonly Voice[] voices = new Voice[40];
        static float[][] bank;
        static readonly float[] mixL = new float[Frames], mixR = new float[Frames];

        public static float SfxVolume = 0.8f, MusicVolume = 0.5f;
        public static bool Enabled;
        static float lisX, lisY, lisAng;
        static readonly Random rng = new Random(1234);

        public static void Init()
        {
            for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
            try
            {
                bank = SfxBank.Build();
                Music.Init();
                Enabled = AudioDevice.Start();
            }
            catch
            {
                Enabled = false;
            }
        }

        public static void Shutdown()
        {
            if (!Enabled) return;
            AudioDevice.Stop();
            Enabled = false;
        }

        // diagnostics (read by the audio stress test)
        public static int Underruns, WriteErrors, Exceptions, BuffersWritten;
        public static double MaxMixMs;
        public static string LastError;
        public static List<short> Recording;

        /// <summary>Mixes the next Frames stereo frames into pcm (interleaved 16-bit). Called from the device's audio thread.</summary>
        public static void Mix(short[] pcm)
        {
            Array.Clear(mixL, 0, Frames);
            Array.Clear(mixR, 0, Frames);
            lock (sync)
            {
                foreach (var v in voices)
                {
                    if (!v.Active) continue;
                    var d = v.Data;
                    double pos = v.Pos, step = v.Step;
                    float l = v.L * SfxVolume, r = v.R * SfxVolume;
                    for (int i = 0; i < Frames; i++)
                    {
                        int ip = (int)pos;
                        if (ip >= d.Length - 1) { v.Active = false; break; }
                        float f = (float)(pos - ip);
                        float s = d[ip] + (d[ip + 1] - d[ip]) * f;
                        mixL[i] += s * l;
                        mixR[i] += s * r;
                        pos += step;
                    }
                    v.Pos = pos;
                }
            }
            Music.Render(mixL, mixR, Frames, MusicVolume);
            for (int i = 0; i < Frames; i++)
            {
                float a = mixL[i], b = mixR[i];
                if (!(a > -100 && a < 100)) a = 0;     // NaN guard
                if (!(b > -100 && b < 100)) b = 0;
                // limiter: instant attack, ~0.2 s release, so stacked gunshots duck instead of distorting
                float pk = Math.Max(Math.Abs(a), Math.Abs(b));
                limEnv = pk > limEnv ? pk : limEnv * 0.99985f + pk * 0.00015f;
                float g = limEnv > 0.9f ? 0.9f / limEnv : 1f;
                pcm[i * 2] = (short)(SoftClip(a * g) * 30000);
                pcm[i * 2 + 1] = (short)(SoftClip(b * g) * 30000);
            }
            var rec = Recording;
            if (rec != null) lock (rec) rec.AddRange(pcm);
        }

        static float limEnv;

        /// <summary>Smooth saturation above 0.75 (continuous, no hard corner).</summary>
        static float SoftClip(float x)
        {
            float ax = x < 0 ? -x : x;
            if (ax <= 0.75f) return x;
            float y = 0.75f + 0.25f * (float)Math.Tanh((ax - 0.75f) / 0.25f);
            return x < 0 ? -y : y;
        }

        public static void SetListener(float x, float y, float angle) { lisX = x; lisY = y; lisAng = angle; }

        public static void Play(Sfx s) { Play(s, 1, 0, 1, 0); }

        public static void Play(Sfx s, float vol, float pan, float pitch, int owner)
        {
            if (!Enabled || bank == null) return;
            var data = bank[(int)s];
            if (data == null || vol <= 0.01f) return;
            if (pan < -1) pan = -1; if (pan > 1) pan = 1;
            float l = vol * (float)Math.Sqrt((1 - pan) * 0.5), r = vol * (float)Math.Sqrt((1 + pan) * 0.5);
            lock (sync)
            {
                Voice best = null;
                if (owner != 0)
                    foreach (var v in voices) if (v.Active && v.Owner == owner) { best = v; break; }   // a monster only has one voice
                if (best == null)
                {
                    // at most 3 copies of the same sound (rapid fire): restart the oldest copy instead
                    int same = 0; Voice oldest = null;
                    foreach (var v in voices)
                        if (v.Active && v.Data == data) { same++; if (oldest == null || v.Pos > oldest.Pos) oldest = v; }
                    if (same >= 3) best = oldest;
                }
                if (best == null) foreach (var v in voices) if (!v.Active) { best = v; break; }
                if (best == null)
                {
                    // steal the voice that has played the longest
                    double most = -1;
                    foreach (var v in voices) if (v.Pos / v.Data.Length > most) { most = v.Pos / v.Data.Length; best = v; }
                }
                best.Data = data;
                best.Pos = 0;
                best.Step = pitch * (0.97 + rng.NextDouble() * 0.06);
                best.L = l; best.R = r;
                best.Owner = owner;
                best.Active = true;
            }
        }

        /// <summary>Plays a sound at a world position: distance attenuation and stereo panning.</summary>
        public static void PlayAt(Sfx s, float x, float y, float vol, int owner)
        {
            float dx = x - lisX, dy = y - lisY;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            float att = 1f / (1 + d * d * 0.012f);
            if (d > 28) return;
            float pan = 0;
            if (d > 0.3f)
            {
                float rx = -(float)Math.Sin(lisAng), ry = (float)Math.Cos(lisAng);
                pan = (dx * rx + dy * ry) / d * 0.75f;
            }
            Play(s, vol * att, pan, 1, owner);
        }

        public static void PlayAt(Sfx s, float x, float y) { PlayAt(s, x, y, 1, 0); }

        public static void StopAll()
        {
            lock (sync) foreach (var v in voices) v.Active = false;
        }
    }
}
