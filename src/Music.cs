// TERMINAL HELL - tiny procedural music sequencer (original riffs: distorted bass, drums, dark pad).
using System;

namespace TerminalHell
{
    static class Music
    {
        const int R = Audio.Rate;

        sealed class Track
        {
            public float Bpm;
            public int Root;            // midi note of the key
            public string[] Bass;       // 16 steps per bar: '.' rest, '-' hold, 0-9a-f semitone offsets
            public string[] Drums;      // K kick, S snare, H hat, X kick+hat, Y snare+hat, C crash, '.' none
            public int[] Order;         // pairs: pattern index, transpose
            public float Drive = 6;
            public float PadVol = 0.07f;
            public float BassVol = 0.22f;
            public bool Fifth = true;
            public string[] Lead;       // optional bell melody, same layout as Bass but always in the key of Root (notes are not transposed)
            public float LeadVol = 0.12f;
            public int[] Chord;         // pad intervals, three or four notes (default: a minor triad)
        }

        static Track[] tracks;
        static volatile int current = -1;
        static int playing = -1;
        static long sample;
        static int step = -1, orderPos;
        static float[] kick, snare, hat, crash;
        static readonly float[][] dData = new float[6][];
        static readonly int[] dPos = new int[6];
        static readonly float[] dPan = new float[6];
        // bass state
        static double bph1, bph2;
        static float bFreq, bEnv, bDecay = 0.1f, bLp;
        // pad state
        static float padRoot = -1, padTarget = -1, padMix;
        static double[] pph = new double[16];
        // bell voices for the melody
        const int LeadVoices = 4;
        static readonly double[] leadPh1 = new double[LeadVoices], leadPh2 = new double[LeadVoices], leadPh3 = new double[LeadVoices];
        static readonly float[] leadFreq = new float[LeadVoices], leadAge = new float[LeadVoices];
        static int leadNext;
        static float fade = 1;
        static uint rs = 777;
        static readonly int[] PadIntervals = { 0, 3, 7 };

        static float Rnd() { rs = rs * 1664525 + 1013904223; return ((rs >> 8) & 0xFFFF) / 32768f - 1f; }

        public static void Init()
        {
            kick = new float[(int)(0.35f * R)];
            {
                double ph = 0;
                for (int i = 0; i < kick.Length; i++)
                {
                    float t = (float)i / R;
                    double f = 45 + 110 * Math.Exp(-t / 0.04);
                    ph += f / R;
                    kick[i] = (float)Math.Tanh(Math.Sin(ph * 2 * Math.PI) * 2.5) * (float)Math.Exp(-t / 0.12) + (i < 60 ? Rnd() * 0.3f : 0);
                }
            }
            snare = new float[(int)(0.25f * R)];
            {
                float lp = 0;
                for (int i = 0; i < snare.Length; i++)
                {
                    float t = (float)i / R;
                    float n = Rnd();
                    lp += (n - lp) * 0.45f;
                    snare[i] = lp * 0.9f * (float)Math.Exp(-t / 0.07) + (float)Math.Sin(t * 2 * Math.PI * 185) * 0.5f * (float)Math.Exp(-t / 0.03);
                }
            }
            hat = new float[(int)(0.06f * R)];
            {
                float prev = 0;
                for (int i = 0; i < hat.Length; i++)
                {
                    float t = (float)i / R;
                    float n = Rnd();
                    hat[i] = (n - prev) * 0.35f * (float)Math.Exp(-t / 0.015);
                    prev = n;
                }
            }
            crash = new float[(int)(1.2f * R)];
            {
                float prev = 0;
                for (int i = 0; i < crash.Length; i++)
                {
                    float t = (float)i / R;
                    float n = Rnd();
                    crash[i] = (n - prev) * 0.3f * (float)Math.Exp(-t / 0.4);
                    prev = n;
                }
            }

            tracks = new Track[7];
            // 0: title - slow and ominous
            tracks[0] = new Track
            {
                Bpm = 76, Root = 40, Drive = 4, PadVol = 0.1f, BassVol = 0.2f,
                Bass = new[] { "0---------------", "..........1-----", "0---------------", "..........6---5-" },
                Drums = new[] { "K...........S...", "K.........K.S...", "C...............", "K...........S.S." },
                Order = new[] { 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 3, -2 },
            };
            // 1: level one - driving
            tracks[1] = new Track
            {
                Bpm = 138, Root = 40,
                Bass = new[] { "0.0.0.3.0.0.5.3.", "0.0.0.3.0.0.6.5.", "0.0.c.0.0.a.0.7.", "5-5-3-3-0.0.0.0." },
                Drums = new[] { "X.H.Y.H.X.X.Y.H.", "X.H.Y.H.X.X.Y.HH", "C.H.Y.H.X.H.Y.H.", "X.X.Y.Y.X.X.YYYY" },
                Order = new[] { 0, 0, 1, 0, 0, 0, 1, 0, 2, 0, 2, 0, 0, 5, 3, 0 },
            };
            // 2: level two - groovy and heavy
            tracks[2] = new Track
            {
                Bpm = 116, Root = 38, Drive = 7,
                Bass = new[] { "0..0..7.0..0.5.3", "0..0..7.0..0.8.7", "3-3-5-5-6-6-5-3-", "0.0.0.0.1.1.1.1." },
                Drums = new[] { "K..K..S.K..K.S..", "K..K..S.K..KSS.S", "C...S...K...S...", "KKKKS.S.KKKKSSSS" },
                Order = new[] { 0, 0, 1, 0, 0, 0, 1, 0, 2, 0, 2, 2, 3, 0, 3, 0 },
            };
            // 3: hell - fast and chromatic
            tracks[3] = new Track
            {
                Bpm = 156, Root = 40, Drive = 8,
                Bass = new[] { "0.1.0.1.3.1.0.--", "0.1.0.1.6.5.3.1.", "0000000011110000", "0.0.3.0.0.6.0.7." },
                Drums = new[] { "X.H.Y.H.X.H.Y.H.", "X.X.Y.X.X.X.Y.YY", "XHXHYHXHXHXHYHYH", "C.X.Y.X.X.X.Y.X." },
                Order = new[] { 0, 0, 1, 0, 0, 0, 1, 0, 2, 0, 2, 0, 3, 0, 3, 5 },
            };
            // 4: intermission - calm
            tracks[4] = new Track
            {
                Bpm = 92, Root = 45, Drive = 2, PadVol = 0.11f, BassVol = 0.16f, Fifth = false,
                Bass = new[] { "0...7...c...7...", "5...c...h...c...", "3...a...f...a...", "7...e...j...e..." },
                Drums = new[] { "K.......S.......", "K.......S...H...", "K.......S.......", "K...K...S...S.S." },
                Order = new[] { 0, 0, 1, 0, 2, 0, 3, 0 },
            };
            // 5: the airport before anything goes wrong - soft pads, a slow bass and a glockenspiel, no drums
            tracks[5] = new Track
            {
                Bpm = 84, Root = 36, Drive = 1, PadVol = 0.2f, BassVol = 0.11f, Fifth = false, LeadVol = 0.15f,
                Chord = new[] { 0, 7, 12, 14 },
                Bass = new[] { "0-------7-------", "0-------7-------", "0-------7-------", "0-------7-------" },
                Drums = new[] { "................", "................", "................", "................" },
                Lead = new[] { "c.......9...7...", "9...c...e.......", "c...9...7...9...", "7...9...c...e..." },
                Order = new[] { 0, 0, 1, 9, 2, 5, 3, 7 },
            };
            // 6: the airport once it has been reached - a low heartbeat, a diminished pad, bells a semitone apart
            tracks[6] = new Track
            {
                Bpm = 104, Root = 38, Drive = 5, PadVol = 0.15f, BassVol = 0.2f, LeadVol = 0.1f,
                Chord = new[] { 0, 3, 6 },
                Bass = new[] { "0...0...1.......", "0...0.0.1...6...", "0.0.0.0.5...6...", "0.......1.......", },
                Drums = new[] { "K...........S...", "K.......K...S...", "K.....K.K...S.S.", "K...........S..C" },
                Lead = new[] { "................", "....c...........", "................", "..........d.e..." },
                Order = new[] { 0, 0, 1, 0, 0, 0, 2, 0, 1, 0, 0, 0, 3, 0, 3, -1 },
            };
        }

        public static void Play(int track)
        {
            current = track;
        }

        public static void Stop() { current = -1; }

        /// <summary>The track that has been asked for (-1 for silence).</summary>
        public static int Current { get { return current; } }

        /// <summary>Developer aid: renders a track offline and reports its level, so a bad mix or a NaN is caught without listening.</summary>
        public static string Measure(int track, float seconds, string wavPath)
        {
            Init();
            Play(track);
            int frames = 1024, total = (int)(seconds * R);
            var l = new float[frames]; var r = new float[frames];
            var pcm = new System.Collections.Generic.List<short>();
            double sum = 0; float peak = 0; int bad = 0; long n = 0;
            for (int done = 0; done < total; done += frames)
            {
                Array.Clear(l, 0, frames); Array.Clear(r, 0, frames);
                Render(l, r, frames, 1f);
                for (int i = 0; i < frames; i++)
                {
                    float v = (l[i] + r[i]) * 0.5f;
                    if (float.IsNaN(v) || float.IsInfinity(v)) { bad++; v = 0; }
                    sum += v * v; peak = Math.Max(peak, Math.Abs(v)); n++;
                    pcm.Add((short)(Math.Max(-1, Math.Min(1, v)) * 32000));
                }
            }
            if (wavPath != null)
            {
                using (var fs = new System.IO.FileStream(wavPath, System.IO.FileMode.Create))
                using (var bw = new System.IO.BinaryWriter(fs))
                {
                    bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + pcm.Count * 2); bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                    bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(R); bw.Write(R * 2); bw.Write((short)2); bw.Write((short)16);
                    bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(pcm.Count * 2);
                    foreach (short s in pcm) bw.Write(s);
                }
            }
            return "track " + track + ": rms " + Math.Sqrt(sum / Math.Max(1, n)).ToString("0.000") + ", peak " + peak.ToString("0.000") + ", bad samples " + bad;
        }

        static int Note(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'z') return 10 + (c - 'a');
            return 0;
        }

        static float Midi(float n) { return 440f * (float)Math.Pow(2, (n - 69) / 12.0); }

        static void Trigger(float[] d, float pan)
        {
            for (int i = 0; i < dData.Length; i++)
                if (dData[i] == null) { dData[i] = d; dPos[i] = 0; dPan[i] = pan; return; }
            dData[0] = d; dPos[0] = 0; dPan[0] = pan;
        }

        /// <summary>Called on the audio thread: adds music into the mix buffers.</summary>
        public static void Render(float[] L, float[] Rr, int frames, float volume)
        {
            if (tracks == null) return;
            int want = current;
            if (want != playing)
            {
                // fade out, then switch
                fade -= frames / (0.4f * R);
                if (fade <= 0 || playing < 0)
                {
                    playing = want; fade = 1; sample = 0; step = -1; orderPos = 0;
                    bEnv = 0; padRoot = padTarget = -1;
                    for (int v = 0; v < LeadVoices; v++) leadFreq[v] = 0;
                    for (int i = 0; i < dData.Length; i++) dData[i] = null;
                }
            }
            if (playing < 0 || volume <= 0) return;
            var tr = tracks[playing];
            double stepLen = 60.0 / tr.Bpm / 4 * R;
            float vol = volume * (want == playing ? 1 : Math.Max(0, fade));

            for (int i = 0; i < frames; i++, sample++)
            {
                int s = (int)(sample / stepLen);
                if (s != step)
                {
                    step = s;
                    int bar = s / 16, st = s % 16;
                    int oi = (bar % (tr.Order.Length / 2)) * 2;
                    int pat = tr.Order[oi], trans = tr.Order[oi + 1];
                    string bl = tr.Bass[pat], dl = tr.Drums[pat];
                    char bc = bl[st];
                    if (bc != '.' && bc != '-')
                    {
                        bFreq = Midi(tr.Root + trans + Note(bc));
                        bEnv = 1;
                        // longer notes if followed by holds
                        int hold = 1;
                        while (st + hold < 16 && bl[st + hold] == '-') hold++;
                        bDecay = hold > 1 ? (float)(hold * stepLen / R) * 0.7f : 0.09f;
                    }
                    else if (bc == '.') bDecay = Math.Min(bDecay, 0.03f);
                    char dc = dl[st];
                    if (dc == 'K' || dc == 'X') Trigger(kick, 0);
                    if (dc == 'S' || dc == 'Y') Trigger(snare, -0.1f);
                    if (dc == 'H' || dc == 'X' || dc == 'Y') Trigger(hat, 0.35f);
                    if (dc == 'C') { Trigger(crash, -0.3f); Trigger(kick, 0); }
                    if (tr.Lead != null)
                    {
                        char lc = tr.Lead[pat][st];
                        if (lc != '.' && lc != '-')
                        {
                            int v = leadNext; leadNext = (leadNext + 1) % LeadVoices;
                            leadFreq[v] = Midi(tr.Root + 24 + Note(lc));
                            leadAge[v] = 0;
                            leadPh1[v] = leadPh2[v] = leadPh3[v] = 0;
                        }
                    }
                    if (st == 0) padTarget = tr.Root + trans + 12;
                }

                // bass: two detuned saws (root + fifth), overdriven and low-passed
                float bass = 0;
                if (bEnv > 0.001f)
                {
                    bph1 += bFreq / R; bph2 += bFreq * (tr.Fifth ? 1.4983 : 2.003) / R;
                    float s1 = (float)((bph1 - Math.Floor(bph1)) * 2 - 1), s2 = (float)((bph2 - Math.Floor(bph2)) * 2 - 1);
                    float raw = (s1 + s2 * 0.7f) * bEnv;
                    float dist = (float)Math.Tanh(raw * tr.Drive) * 0.8f;
                    bLp += (dist - bLp) * 0.22f;
                    bass = bLp * tr.BassVol;
                    bEnv *= (float)Math.Exp(-1.0 / (bDecay * R));
                }

                // pad: minor triad that glides between roots
                float pad = 0;
                if (padTarget > 0)
                {
                    if (padRoot < 0) padRoot = padTarget;
                    padRoot += (padTarget - padRoot) * 0.0005f;
                    float lfo = 0.6f + 0.4f * (float)Math.Sin(sample * 2 * Math.PI * 0.15 / R);
                    var chord = tr.Chord ?? PadIntervals;
                    for (int k = 0; k < chord.Length; k++)
                    {
                        double f = Midi(padRoot + chord[k]);
                        pph[k] += f / R; pph[k + 8] += f * 1.004 / R;
                        pad += (float)(Math.Sin(pph[k] * 2 * Math.PI) + Math.Sin(pph[k + 8] * 2 * Math.PI) * 0.8);
                    }
                    pad *= tr.PadVol * (chord.Length == 3 ? 0.25f : 0.19f) * lfo;
                }

                // the melody: a soft bell (a fundamental and two inharmonic partials that die away faster)
                float lead = 0;
                if (tr.Lead != null)
                    for (int v = 0; v < LeadVoices; v++)
                    {
                        if (leadFreq[v] <= 0 || leadAge[v] > 2.5f) continue;
                        float age = leadAge[v];
                        leadAge[v] += 1f / R;
                        leadPh1[v] += leadFreq[v] / R; leadPh2[v] += leadFreq[v] * 2.76 / R; leadPh3[v] += leadFreq[v] * 5.4 / R;
                        float att = Math.Min(1, age / 0.004f);
                        lead += att * ((float)Math.Sin(leadPh1[v] * 2 * Math.PI) * (float)Math.Exp(-age / 1.0)
                                     + (float)Math.Sin(leadPh2[v] * 2 * Math.PI) * 0.28f * (float)Math.Exp(-age / 0.35)
                                     + (float)Math.Sin(leadPh3[v] * 2 * Math.PI) * 0.1f * (float)Math.Exp(-age / 0.15));
                    }
                lead *= tr.LeadVol;

                float dl2 = 0, dr2 = 0;
                for (int k = 0; k < dData.Length; k++)
                {
                    var d = dData[k];
                    if (d == null) continue;
                    float v = d[dPos[k]++] * 0.45f;
                    dl2 += v * (1 - dPan[k]); dr2 += v * (1 + dPan[k]);
                    if (dPos[k] >= d.Length) dData[k] = null;
                }

                L[i] += (bass + pad + lead * 0.9f + dl2) * vol;
                Rr[i] += (bass + pad * 0.9f + lead + dr2) * vol;
            }
        }
    }
}
