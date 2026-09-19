// TERMINAL HELL - Linux: keyboards and mice read straight from the kernel (/dev/input/event*).
// Gives true key press / release events and raw mouse motion, and can grab the mouse so the desktop pointer
// stays put while playing (like the Windows version's cursor lock). Needs read access to the devices,
// which normally means being in the "input" group; without it the game uses what the terminal reports.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TerminalHell
{
    static class Evdev
    {
        public const int EV_KEY = 1, EV_REL = 2;
        public const int REL_X = 0, REL_Y = 1, REL_WHEEL = 8;
        public const int BTN_LEFT = 0x110, BTN_RIGHT = 0x111, BTN_MIDDLE = 0x112;

        public struct DeviceInfo
        {
            public string Handler, Name;
            public bool Keyboard, Mouse;
        }

        sealed class Dev
        {
            public int Fd;
            public string Name;
            public bool Keyboard, Mouse, Grabbable;
        }

        /// <summary>Called for every kernel event: (from a mouse device, type, code, value).</summary>
        public delegate void Handler(bool mouseDevice, int type, int code, int value);

        static readonly List<Dev> devs = new List<Dev>();
        static readonly byte[] evbuf = new byte[24 * 64];   // struct input_event is 24 bytes on 64-bit Linux
        static readonly object grabLock = new object();
        public static bool HasKeyboard, HasMouse, PermissionDenied, Grabbed;
        public static int Keyboards, Mice;

        /// <summary>Opens the keyboards and/or mice listed in /proc/bus/input/devices that we are allowed to read.</summary>
        public static void Open(bool keyboards, bool mice)
        {
            string text;
            try { text = File.ReadAllText("/proc/bus/input/devices"); }
            catch { return; }
            foreach (var d in Parse(text))
            {
                bool k = keyboards && d.Keyboard, m = mice && d.Mouse;
                if (!k && !m) continue;
                int fd = LibC.open("/dev/input/" + d.Handler, LibC.O_RDONLY | LibC.O_NONBLOCK | LibC.O_CLOEXEC, 0);
                if (fd < 0)
                {
                    int e = LibC.Errno;
                    if (e == LibC.EACCES || e == LibC.EPERM) PermissionDenied = true;
                    continue;
                }
                // never grab a device that is also a keyboard: its keys would stop reaching the terminal (and Alt+Tab)
                devs.Add(new Dev { Fd = fd, Name = d.Name, Keyboard = k, Mouse = m, Grabbable = m && !d.Keyboard });
                if (k) Keyboards++;
                if (m) Mice++;
            }
            HasKeyboard = Keyboards > 0;
            HasMouse = Mice > 0;
        }

        /// <summary>Picks keyboards and mice out of /proc/bus/input/devices. Pure, so the self test can check it.</summary>
        public static List<DeviceInfo> Parse(string text)
        {
            var list = new List<DeviceInfo>();
            foreach (var block in text.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = "", handlers = "", ev = "", key = "", rel = "";
                foreach (var line in block.Split('\n'))
                {
                    if (line.StartsWith("N: Name=")) name = line.Substring(8).Trim().Trim('"');
                    else if (line.StartsWith("H: Handlers=")) handlers = line.Substring(12);
                    else if (line.StartsWith("B: EV=")) ev = line.Substring(6);
                    else if (line.StartsWith("B: KEY=")) key = line.Substring(7);
                    else if (line.StartsWith("B: REL=")) rel = line.Substring(7);
                }
                string handler = null;
                foreach (var h in handlers.Split(' ')) if (h.StartsWith("event")) handler = h.Trim();
                if (handler == null) continue;
                var d = new DeviceInfo();
                d.Handler = handler;
                d.Name = name;
                // a keyboard: key events with auto-repeat, and it has W, A and Space
                d.Keyboard = Bit(ev, EV_KEY) && Bit(ev, 20) && Bit(key, 17) && Bit(key, 30) && Bit(key, 57);
                // a mouse: relative X/Y motion and a left button (touchpads report absolute positions instead)
                d.Mouse = Bit(ev, EV_REL) && Bit(rel, REL_X) && Bit(rel, REL_Y) && Bit(key, BTN_LEFT);
                if (d.Keyboard || d.Mouse) list.Add(d);
            }
            return list;
        }

        /// <summary>Bit n of a kernel bitmap printed as hex words, most significant word first (64-bit words).</summary>
        public static bool Bit(string bitmap, int n)
        {
            var words = bitmap.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int idx = words.Length - 1 - n / 64;
            if (idx < 0) return false;
            ulong w;
            if (!ulong.TryParse(words[idx], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out w)) return false;
            return ((w >> (n % 64)) & 1) != 0;
        }

        /// <summary>Reads everything waiting on every device.</summary>
        public static void Poll(Handler handler)
        {
            for (int d = devs.Count - 1; d >= 0; d--)
            {
                var dev = devs[d];
                while (true)
                {
                    int n = LibC.Read(dev.Fd, evbuf, 0, evbuf.Length);
                    if (n <= 0)
                    {
                        if (n < 0 && LibC.Errno == LibC.ENODEV) Remove(d);   // unplugged
                        break;
                    }
                    for (int o = 0; o + 24 <= n; o += 24)
                        handler(dev.Mouse, BitConverter.ToUInt16(evbuf, o + 16), BitConverter.ToUInt16(evbuf, o + 18), BitConverter.ToInt32(evbuf, o + 20));
                    if (n < evbuf.Length) break;
                }
            }
        }

        static void Remove(int d)
        {
            var dev = devs[d];
            LibC.close(dev.Fd);
            devs.RemoveAt(d);
            if (dev.Keyboard) Keyboards--;
            if (dev.Mouse) Mice--;
            HasKeyboard = Keyboards > 0;
            HasMouse = Mice > 0;
        }

        /// <summary>Takes the mice away from the desktop (true) or gives them back (false). Safe to call from any thread.</summary>
        public static void Grab(bool on)
        {
            lock (grabLock)
            {
                if (Grabbed == on) return;
                bool any = false;
                foreach (var dev in devs)
                    if (dev.Grabbable && LibC.Grab(dev.Fd, on)) any = true;
                Grabbed = on && any;
            }
        }

        public static bool CanGrab
        {
            get { foreach (var dev in devs) if (dev.Grabbable) return true; return false; }
        }

        public static void Close()
        {
            Grab(false);
            lock (grabLock)
            {
                foreach (var dev in devs) LibC.close(dev.Fd);
                devs.Clear();
                Keyboards = Mice = 0;
                HasKeyboard = HasMouse = false;
            }
        }
    }
}
