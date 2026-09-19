// TERMINAL HELL - Windows audio output: plays the mixer's blocks through the waveOut API.
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TerminalHell
{
    static class AudioDevice
    {
        const int NumBuf = 4;

        static IntPtr hwo;
        static IntPtr[] hdrs, bufs;
        static int hdrSize, flagsOffset;
        static Thread thread;
        static volatile bool running;
        static readonly short[] pcm = new short[Audio.Frames * 2];

        public static bool Start()
        {
            var fmt = new WAVEFORMATEX();
            fmt.wFormatTag = 1;
            fmt.nChannels = 2;
            fmt.nSamplesPerSec = Audio.Rate;
            fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 4;
            fmt.nAvgBytesPerSec = Audio.Rate * 4;
            if (Native.waveOutOpen(out hwo, -1, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { hwo = IntPtr.Zero; return false; }
            hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
            flagsOffset = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");
            hdrs = new IntPtr[NumBuf]; bufs = new IntPtr[NumBuf];
            for (int i = 0; i < NumBuf; i++)
            {
                bufs[i] = Marshal.AllocHGlobal(Audio.Frames * 4);
                hdrs[i] = Marshal.AllocHGlobal(hdrSize);
                var h = new WAVEHDR();
                h.lpData = bufs[i];
                h.dwBufferLength = Audio.Frames * 4;
                Marshal.StructureToPtr(h, hdrs[i], false);
                Native.waveOutPrepareHeader(hwo, hdrs[i], hdrSize);
            }
            running = true;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.AboveNormal;
            thread.Start();
            return true;
        }

        public static void Stop()
        {
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
        }

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
                    if (done == NumBuf && Audio.BuffersWritten > 0) Audio.Underruns++;
                    for (int i = 0; i < NumBuf; i++)
                    {
                        int flags = Marshal.ReadInt32(hdrs[i], flagsOffset);
                        if (started[i] && (flags & 1) == 0) continue;   // WHDR_DONE not set yet: still playing
                        sw.Restart();
                        Audio.Mix(pcm);
                        Marshal.Copy(pcm, 0, bufs[i], Audio.Frames * 2);
                        Audio.MaxMixMs = Math.Max(Audio.MaxMixMs, sw.Elapsed.TotalMilliseconds);
                        Marshal.WriteInt32(hdrs[i], flagsOffset, flags & ~1);
                        int rc = Native.waveOutWrite(hwo, hdrs[i], hdrSize);
                        if (rc != 0) { Audio.WriteErrors++; Audio.LastError = "waveOutWrite " + rc; }
                        started[i] = true;
                        Audio.BuffersWritten++;
                        did = true;
                    }
                }
                catch (Exception ex)
                {
                    Audio.Exceptions++;
                    Audio.LastError = ex.GetType().Name + ": " + ex.Message;
                }
                if (!did) Thread.Sleep(2);
            }
        }
    }
}
