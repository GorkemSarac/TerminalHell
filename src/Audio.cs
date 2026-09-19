// TERMINAL HELL - real-time stereo software mixer on top of the Windows waveOut API.
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TerminalHell
{
    enum Sfx
    {
        Pistol, Shotgun, Chaingun, Rocket, Explode, Punch, Swing, DoorOpen, DoorClose, Pickup, WeaponPickup, KeyPickup, PowerUp,
        PlayerPain, PlayerDeath, GhoulSight, GhoulPain, GhoulDeath, GhoulShot, FiendSight, FiendPain, FiendDeath, FiendThrow,
        FireHit, BruteSight, BrutePain, BruteDeath, BruteBite, BossSight, BossPain, BossDeath, BossStep, Switch, NoWay,
        PushWall, MenuMove, MenuSelect, DryFire, HurtFloor, Secret, WeaponUp, Growl, Count
    }

    static class Audio
    {
        public const int Rate = 22050;
        const int Frames = 384;       // per buffer (~17 ms)
        const int NumBuf = 4;

        sealed class Voice
        {
            public float[] Data;
            public double Pos, Step;
            public float L, R;
            public bool Active;
            public int Owner;
        }

        static IntPtr hwo;
        static IntPtr[] hdrs, bufs;
        static int hdrSize, flagsOffset;
        static Thread thread;
        static volatile bool running;
        static readonly object sync = new object();
        static readonly Voice[] voices = new Voice[40];
        static float[][] bank;
        static readonly float[] mixL = new float[Frames], mixR = new float[Frames];
        static readonly short[] pcm = new short[Frames * 2];

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
                var fmt = new WAVEFORMATEX();
                fmt.wFormatTag = 1;
                fmt.nChannels = 2;
                fmt.nSamplesPerSec = Rate;
                fmt.wBitsPerSample = 16;
                fmt.nBlockAlign = 4;
                fmt.nAvgBytesPerSec = Rate * 4;
                if (Native.waveOutOpen(out hwo, -1, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { hwo = IntPtr.Zero; return; }
                hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
                flagsOffset = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");
                hdrs = new IntPtr[NumBuf]; bufs = new IntPtr[NumBuf];
                for (int i = 0; i < NumBuf; i++)
                {
                    bufs[i] = Marshal.AllocHGlobal(Frames * 4);
                    hdrs[i] = Marshal.AllocHGlobal(hdrSize);
                    var h = new WAVEHDR();
                    h.lpData = bufs[i];
                    h.dwBufferLength = Frames * 4;
                    Marshal.StructureToPtr(h, hdrs[i], false);
                    Native.waveOutPrepareHeader(hwo, hdrs[i], hdrSize);
                }
                Enabled = true;
                running = true;
                thread = new Thread(Loop);
                thread.IsBackground = true;
                thread.Priority = ThreadPriority.AboveNormal;
                thread.Start();
            }
            catch
            {
                Enabled = false;
            }
        }

        public static void Shutdown()
        {
            if (!Enabled) return;
            running = false;
            if (thread != null) thread.Join(500);
            try
            {
                Native.waveOutReset(hwo);
                for (int i = 0; i < NumBuf; i++)
                {
                    Native.waveOutUnprepareHeader(hwo, hdrs[i], hdrSize);
                    Marshal.FreeHGlobal(hdrs[i]);
                    Marshal.FreeHGlobal(bufs[i]);
                }
                Native.waveOutClose(hwo);
            }
            catch { }
            Enabled = false;
        }

        // diagnostics (read by the audio stress test)
        public static int Underruns, WriteErrors, Exceptions, BuffersWritten;
        public static double MaxMixMs;
        public static string LastError;
        public static System.Collections.Generic.List<short> Recording;

        static void Loop()
        {
            bool[] started = new bool[NumBuf];
            var sw = new System.Diagnostics.Stopwatch();
            while (running)
            {
                bool did = false;
                try
                {
                    int done = 0;
                    for (int i = 0; i < NumBuf; i++)
                        if (!started[i] || (Marshal.ReadInt32(hdrs[i], flagsOffset) & 1) != 0) done++;
                    if (done == NumBuf && BuffersWritten > 0) Underruns++;
                    for (int i = 0; i < NumBuf; i++)
                    {
                        int flags = Marshal.ReadInt32(hdrs[i], flagsOffset);
                        if (started[i] && (flags & 1) == 0) continue;   // WHDR_DONE not set yet: still playing
                        sw.Restart();
                        MixInto(bufs[i]);
                        MaxMixMs = Math.Max(MaxMixMs, sw.Elapsed.TotalMilliseconds);
                        Marshal.WriteInt32(hdrs[i], flagsOffset, flags & ~1);
                        int rc = Native.waveOutWrite(hwo, hdrs[i], hdrSize);
                        if (rc != 0) { WriteErrors++; LastError = "waveOutWrite " + rc; }
                        started[i] = true;
                        BuffersWritten++;
                        did = true;
                    }
                }
                catch (Exception ex)
                {
                    Exceptions++;
                    LastError = ex.GetType().Name + ": " + ex.Message;
                }
                if (!did) Thread.Sleep(2);
            }
        }

        static void MixInto(IntPtr dest)
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
            Marshal.Copy(pcm, 0, dest, Frames * 2);
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
