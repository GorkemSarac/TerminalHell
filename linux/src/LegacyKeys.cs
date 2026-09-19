// TERMINAL HELL - Linux: held keys for terminals that only report key presses.
// A plain terminal sends a key once, then (after the keyboard's repeat delay) again and again while it is held,
// and nothing at all when it is released. So a key counts as held until its next repeat is overdue.
// The repeat delay and rate are learned from what actually arrives.
// Pure code (no system calls), so the self test can run it anywhere.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    sealed class LegacyKeys
    {
        public double RepeatDelay = 0.5;     // first press -> first repeat (X11 default 0.66 s, GNOME 0.5 s)
        public double RepeatGap = 0.033;     // between repeats (30 per second is typical)

        readonly bool[] held = new bool[256], timedOutFirst = new bool[256];
        readonly int[] count = new int[256];
        readonly double[] lastAt = new double[256], firstGap = new double[256], releaseAt = new double[256];

        public bool Held(int vk) { return held[vk & 255]; }

        /// <summary>A key arrived. Returns true for a new press, false for an auto-repeat of a key already held.</summary>
        public bool Press(int vk, double now)
        {
            vk &= 255;
            bool fresh = !held[vk];
            if (fresh)
            {
                // the first repeat came just after we let the key go: this keyboard's repeat delay is longer than our guess
                if (timedOutFirst[vk] && now - releaseAt[vk] < 0.2)
                {
                    double gap = now - lastAt[vk];
                    if (gap > 0.15 && gap < 1.2) RepeatDelay += (gap - RepeatDelay) * 0.7;
                }
                count[vk] = 1;
            }
            else
            {
                double gap = now - lastAt[vk];
                if (count[vk] == 1) firstGap[vk] = gap;
                else if (gap < 0.25)
                {
                    // steady auto-repeat: learn the rate, and the delay from the gap before the first repeat
                    RepeatGap += (Math.Max(0.008, Math.Min(0.12, gap)) - RepeatGap) * 0.2;
                    if (count[vk] == 2 && firstGap[vk] > 0.15 && firstGap[vk] < 1.2) RepeatDelay += (firstGap[vk] - RepeatDelay) * 0.5;
                }
                count[vk]++;
            }
            timedOutFirst[vk] = false;
            held[vk] = true;
            lastAt[vk] = now;
            releaseAt[vk] = now + (count[vk] == 1 ? RepeatDelay + 0.08 : RepeatGap * 2.5 + 0.03);
            return fresh;
        }

        /// <summary>The key is known to be up (for example the terminal reported its release after all).</summary>
        public void Release(int vk)
        {
            vk &= 255;
            held[vk] = false;
            timedOutFirst[vk] = false;
            count[vk] = 0;
        }

        /// <summary>Adds the keys whose next repeat is overdue (they have been let go) to released.</summary>
        public void Expire(double now, List<int> released)
        {
            for (int vk = 1; vk < 256; vk++)
                if (held[vk] && now > releaseAt[vk])
                {
                    timedOutFirst[vk] = count[vk] == 1;
                    held[vk] = false;
                    count[vk] = 0;
                    released.Add(vk);
                }
        }

        public void Clear()
        {
            Array.Clear(held, 0, 256);
            Array.Clear(timedOutFirst, 0, 256);
            Array.Clear(count, 0, 256);
        }
    }
}
