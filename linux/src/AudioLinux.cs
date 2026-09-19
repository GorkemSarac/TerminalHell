// TERMINAL HELL - Linux audio output: plays the mixer's blocks through PulseAudio (which PipeWire also provides)
// or, failing that, straight through ALSA. Both libraries are loaded at run time: without them the game is silent, not broken.
// (the Windows version is src/AudioWin.cs; the mixer itself is shared, in src/Audio.cs)
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace TerminalHell
{
    static class AudioDevice
    {
        public static string Backend = "none";

        static IntPtr pulse, alsa;
        static Thread thread;
        static volatile bool running;
        static readonly short[] pcm = new short[Audio.Frames * 2];

        public static bool Start()
        {
            LibC.Setup();
            if (!OpenPulse() && !OpenAlsa()) return false;
            running = true;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.AboveNormal;
            thread.Name = "audio";
            thread.Start();
            return true;
        }

        public static void Stop()
        {
            running = false;
            // a write blocks for at most one buffer; if the sound server hangs, leave the rest to process exit
            if (thread != null && !thread.Join(1000)) return;
            try
            {
                if (pulse != IntPtr.Zero) { Pulse.pa_simple_free(pulse); pulse = IntPtr.Zero; }
                if (alsa != IntPtr.Zero) { Alsa.snd_pcm_drop(alsa); Alsa.snd_pcm_close(alsa); alsa = IntPtr.Zero; }
            }
            catch { }
        }

        static bool OpenPulse()
        {
            try
            {
                var spec = new Pulse.SampleSpec { format = Pulse.SAMPLE_S16LE, rate = Audio.Rate, channels = 2 };
                // ~50 ms of queued audio: low latency, and pa_simple_write blocks when it is full, which paces the mixer
                var attr = new Pulse.BufferAttr { maxlength = uint.MaxValue, tlength = (uint)(Audio.Rate * 4 / 20), prebuf = uint.MaxValue, minreq = uint.MaxValue, fragsize = uint.MaxValue };
                int err;
                pulse = Pulse.pa_simple_new(null, "TERMINAL HELL", Pulse.STREAM_PLAYBACK, null, "game audio", ref spec, IntPtr.Zero, ref attr, out err);
                if (pulse == IntPtr.Zero) return false;
                Backend = "PulseAudio";
                return true;
            }
            catch (Exception)   // library not installed
            {
                pulse = IntPtr.Zero;
                return false;
            }
        }

        static bool OpenAlsa()
        {
            try
            {
                IntPtr h;
                if (Alsa.snd_pcm_open(out h, "default", Alsa.STREAM_PLAYBACK, 0) < 0) return false;
                if (Alsa.snd_pcm_set_params(h, Alsa.FORMAT_S16_LE, Alsa.ACCESS_RW_INTERLEAVED, 2, (uint)Audio.Rate, 1, 60000) < 0)
                {
                    Alsa.snd_pcm_close(h);
                    return false;
                }
                alsa = h;
                Backend = "ALSA";
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void Loop()
        {
            var sw = new Stopwatch();
            while (running)
            {
                try
                {
                    sw.Restart();
                    Audio.Mix(pcm);
                    Audio.MaxMixMs = Math.Max(Audio.MaxMixMs, sw.Elapsed.TotalMilliseconds);
                    if (Write()) Audio.BuffersWritten++;
                    else
                    {
                        Audio.WriteErrors++;
                        Audio.LastError = Backend + " write failed";
                        Thread.Sleep(20);
                    }
                }
                catch (Exception ex)
                {
                    Audio.Exceptions++;
                    Audio.LastError = ex.GetType().Name + ": " + ex.Message;
                    Thread.Sleep(20);
                }
            }
        }

        /// <summary>Blocks until the sound server has room: this is what paces the mixer.</summary>
        static unsafe bool Write()
        {
            fixed (short* p = pcm)
            {
                if (pulse != IntPtr.Zero)
                {
                    int err;
                    return Pulse.pa_simple_write(pulse, p, new UIntPtr((uint)(pcm.Length * 2)), out err) >= 0;
                }
                short* q = p;
                int left = Audio.Frames;
                while (left > 0 && running)
                {
                    long n = (long)Alsa.snd_pcm_writei(alsa, q, new UIntPtr((uint)left));
                    if (n < 0)
                    {
                        if (n == -32) Audio.Underruns++;   // EPIPE
                        if (Alsa.snd_pcm_recover(alsa, (int)n, 1) < 0) return false;
                        continue;
                    }
                    left -= (int)n;
                    q += n * 2;
                }
                return true;
            }
        }

        static class Pulse
        {
            const string Lib = "libpulse-simple.so.0";
            public const int SAMPLE_S16LE = 3, STREAM_PLAYBACK = 1;

            [StructLayout(LayoutKind.Sequential)]
            public struct SampleSpec { public int format; public uint rate; public byte channels; }

            [StructLayout(LayoutKind.Sequential)]
            public struct BufferAttr { public uint maxlength, tlength, prebuf, minreq, fragsize; }

            [DllImport(Lib)]
            public static extern IntPtr pa_simple_new(string server, string name, int dir, string dev, string streamName,
                ref SampleSpec ss, IntPtr channelMap, ref BufferAttr attr, out int error);
            [DllImport(Lib)] public static extern unsafe int pa_simple_write(IntPtr s, short* data, UIntPtr bytes, out int error);
            [DllImport(Lib)] public static extern void pa_simple_free(IntPtr s);
        }

        static class Alsa
        {
            const string Lib = "libasound.so.2";
            public const int STREAM_PLAYBACK = 0, FORMAT_S16_LE = 2, ACCESS_RW_INTERLEAVED = 3;

            [DllImport(Lib)] public static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);
            [DllImport(Lib)] public static extern int snd_pcm_set_params(IntPtr pcm, int format, int access, uint channels, uint rate, int softResample, uint latencyUs);
            [DllImport(Lib)] public static extern unsafe IntPtr snd_pcm_writei(IntPtr pcm, short* buffer, UIntPtr frames);
            [DllImport(Lib)] public static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);
            [DllImport(Lib)] public static extern int snd_pcm_drop(IntPtr pcm);
            [DllImport(Lib)] public static extern int snd_pcm_close(IntPtr pcm);
        }
    }
}
