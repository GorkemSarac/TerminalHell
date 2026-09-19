// TERMINAL HELL - Linux input: fills the shared Input state from the terminal and, when allowed, from the kernel.
//   Keyboard, best first:
//     1. kitty keyboard protocol (kitty, foot, Ghostty, Alacritty, WezTerm with enable_kitty_keyboard): real key releases
//     2. /dev/input keyboards (player in the "input" group): real key releases straight from the kernel
//     3. any other terminal: releases are guessed from the keyboard's auto-repeat (LegacyKeys)
//   Mouse look, best first:
//     1. /dev/input mice, grabbed while playing so the desktop pointer stays put (needs the terminal's focus reports)
//     2. the terminal's mouse reports: turning follows the pointer and keeps going while it is near the left/right edge
// (the Windows version is src/InputWin.cs; the key/mouse state itself is shared, in src/Input.cs)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace TerminalHell
{
    static partial class Input
    {
        static readonly VtInput parser = new VtInput();
        static readonly LegacyKeys legacy = new LegacyKeys();
        static readonly List<int> expired = new List<int>();
        static readonly byte[] rbuf = new byte[4096];
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly Evdev.Handler kernelHandler = OnKernel;
        static readonly int[] kernelKeys = KernelKeyMap();
        static readonly List<string> notices = new List<string>();

        /// <summary>--input: receives a line for every event.</summary>
        public static Action<string> Trace;

        public static bool KittyActive, FocusReports, PixelMouse, DetectDone;
        static bool noKitty, noEvdev, pixelSupported;
        static double lastTime, initTime, lastUpdate;
        static double ttyKeyAt = -1, kernelPressAt = -1;
        static bool termL, termR, termM, evL, evR, evM;
        static bool skipDelta;
        static bool havePtr, havePrev;
        static double ptrX, ptrY, prevX, prevY, lookX, lookY;

        const double EdgeZone = 0.12;     // outer 12% of the window width on each side keeps turning
        const double EdgeTurn = 1100;     // turning speed at the very edge, in mouse "pixels" per second

        static bool KeysFromKernel { get { return Evdev.HasKeyboard && !KittyActive; } }

        public static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            noKitty = Array.IndexOf(args, "--no-kitty") >= 0;
            noEvdev = Array.IndexOf(args, "--no-evdev") >= 0;
            if (Term.Env("TERM") == "linux") FocusReports = true;   // the Linux console: there is no other window to switch to

            var sb = new StringBuilder();
            sb.Append("\x1b[?1004h");                                                  // focus in / out reports
            if (!NoMouse) sb.Append("\x1b[?1000h\x1b[?1002h\x1b[?1003h\x1b[?1006h");   // clicks, drags and all motion, SGR coordinates
            if (!noKitty) { sb.Append("\x1b[>11u"); Term.KittyPushed = true; }        // kitty: disambiguate + event types + all keys
            // questions: kitty flags, focus and SGR-pixel mouse support, cell size, and device attributes last (every terminal answers it)
            sb.Append("\x1b[?u\x1b[?1004$p\x1b[?1016$p\x1b[16t\x1b[c");
            Term.Write(sb.ToString());
            initTime = lastTime = clock.Elapsed.TotalSeconds;
            Volatile.Write(ref lastUpdate, initTime);

            if (!noEvdev)
            {
                try { Evdev.Open(true, !NoMouse); }
                catch { }
            }
            if (Evdev.CanGrab)
            {
                var t = new Thread(Watchdog);
                t.IsBackground = true;
                t.Name = "mouse watchdog";
                t.Start();
            }
        }

        /// <summary>If the game stops updating (a hang, a debugger) the desktop gets its mouse back.</summary>
        static void Watchdog()
        {
            while (true)
            {
                Thread.Sleep(250);
                if (Evdev.Grabbed && clock.Elapsed.TotalSeconds - Volatile.Read(ref lastUpdate) > 1.0) Evdev.Grab(false);
            }
        }

        public static void Shutdown()
        {
            Release();
            Evdev.Close();   // the terminal modes are switched back by Term.Restore
        }

        static partial void ClearPlatformKeys()
        {
            legacy.Clear();
            termL = termR = termM = evL = evR = evM = false;
        }

        public static void Update()
        {
            double now = clock.Elapsed.TotalSeconds;
            BeginFrame(now);
            // ---- what the terminal sent
            for (int k = 0; k < 64; k++)
            {
                int n = LibC.Read(0, rbuf, 0, rbuf.Length);
                if (n <= 0) break;
                parser.Feed(rbuf, n, now);
                if (n < rbuf.Length) break;
            }
            EndFrame(now);
        }

        static bool prevL, prevR;
        static double frameDt;

        /// <summary>Start of a frame: forget last frame's hits and deltas. (The self test drives BeginFrame / FeedTerminal / EndFrame directly.)</summary>
        internal static void BeginFrame(double now)
        {
            Array.Clear(hit, 0, 256);
            Array.Clear(rep, 0, 256);
            Typed.Clear();
            MouseDX = MouseDY = Wheel = 0;
            LHit = RHit = false;
            MouseCellMoved = MouseCellClick = false;
            AnyKeyHit = false;
            prevL = LButton; prevR = RButton;
            frameDt = Math.Min(0.1, Math.Max(0, now - lastTime));
            lastTime = now;
            Volatile.Write(ref lastUpdate, now);
        }

        internal static void FeedTerminal(byte[] data, double now)
        {
            parser.Feed(data, data.Length, now);
        }

        /// <summary>Self test only: back to a freshly started game in a terminal we know nothing about yet.</summary>
        internal static void ResetForTest(double now)
        {
            KittyActive = FocusReports = PixelMouse = DetectDone = pixelSupported = false;
            Focused = true;
            CaptureWanted = NoMouse = Captured = false;
            ClearKeys();
            initTime = lastTime = now;
            ttyKeyAt = kernelPressAt = -1;
            havePtr = havePrev = false;
            lookX = lookY = 0;
            notices.Clear();
        }

        internal static void EndFrame(double now)
        {
            double dt = frameDt;
            parser.Tick(now);
            for (int k = 0; k < parser.Events.Count; k++) OnTerminal(parser.Events[k], now);
            parser.Events.Clear();
            if (!DetectDone && now - initTime > 1.5) FinishDetection();   // a terminal that never answered

            // ---- straight from the kernel
            Evdev.Poll(kernelHandler);

            // ---- keys from plain terminals: let go once their auto-repeat stops
            expired.Clear();
            legacy.Expire(now, expired);
            foreach (int vk in expired) KeyUp(vk);

            // ---- without focus reports: a kernel key press the terminal never sees means another window has the keyboard
            if (kernelPressAt >= 0)
            {
                if (ttyKeyAt >= kernelPressAt - 0.1) kernelPressAt = -1;
                else if (now - kernelPressAt > 0.3)
                {
                    kernelPressAt = -1;
                    if (Focused) { Focused = false; ClearKeys(); }
                }
            }

            if (Evdev.Grabbed) { LButton = evL; RButton = evR; MButton = evM; }
            else { LButton = termL; RButton = termR; MButton = termM; }
            if (!Focused) { LButton = RButton = MButton = false; MouseDX = MouseDY = Wheel = 0; }
            LHit = LButton && !prevL;
            RHit = RButton && !prevR;

            // ---- mouse look
            bool want = CaptureWanted && Focused && !NoMouse;
            if (want) Capture(dt); else Release();
            if (!Captured) { MouseDX = MouseDY = 0; }
            if (skipDelta) { MouseDX = MouseDY = 0; skipDelta = false; }
        }

        // ================================================================ terminal events

        static void OnTerminal(TtyEvent e, double now)
        {
            if (Trace != null) Trace(e.ToString());
            switch (e.Type)
            {
                case TtyEventType.Key:
                    OnTerminalKey(e, now);
                    break;
                case TtyEventType.Mouse:
                    OnTerminalMouse(e);
                    break;
                case TtyEventType.FocusIn:
                    FocusReports = true;
                    Focused = true;
                    break;
                case TtyEventType.FocusOut:
                    FocusReports = true;
                    if (Focused) { Focused = false; ClearKeys(); }
                    Release();
                    break;
                case TtyEventType.KittyFlags:
                    // the terminal speaks the kitty keyboard protocol; "report event types" (2) means real releases
                    if ((e.X & 2) != 0 && !KittyActive) { KittyActive = true; ClearKeys(); }
                    break;
                case TtyEventType.ModeReport:
                    bool known = e.Y >= 1 && e.Y <= 3;
                    if (e.X == 1004 && known) FocusReports = true;
                    if (e.X == 1016 && known) pixelSupported = true;
                    break;
                case TtyEventType.CellSize:
                    Term.SetCellSize(e.X, e.Y);
                    break;
                case TtyEventType.DeviceAttributes:
                    FinishDetection();
                    break;
            }
        }

        static void FinishDetection()
        {
            if (DetectDone) return;
            DetectDone = true;
            // SGR-pixel mouse reports make pointer look smooth, but we need the cell size to find the cell under the pointer
            if (pixelSupported && Term.CellSizeKnown && !NoMouse)
            {
                Term.Write("\x1b[?1016h");
                PixelMouse = true;
            }
            if (!KittyActive && !Evdev.HasKeyboard)
                notices.Add("KEY RELEASES ARE GUESSED IN THIS TERMINAL - SEE terminalhell --input");
            if (!NoMouse && !MouseFromKernel)
                notices.Add("MOUSE LOOK: PUSH THE POINTER TO A SIDE EDGE TO KEEP TURNING");
        }

        static bool MouseFromKernel { get { return Evdev.HasMouse && Evdev.CanGrab && FocusReports; } }

        /// <summary>Hints about this terminal for the player, handed out once (shown when the first level starts).</summary>
        public static List<string> TakeNotices()
        {
            var l = new List<string>();
            if (!DetectDone) return l;
            l.AddRange(notices);
            notices.Clear();
            return l;
        }

        static void OnTerminalKey(TtyEvent e, double now)
        {
            if (e.Action != 3 && e.Ch >= 32) Typed.Add(e.Ch);
            if (e.Action == 1 && e.Vk == 'C' && (e.Mods & VtInput.ModCtrl) != 0) Term.QuitNow();
            // the terminal has the keyboard
            ttyKeyAt = now;
            if (!Focused && !FocusReports) Focused = true;
            if (KeysFromKernel || e.Vk == 0) return;   // (the kernel reports every key, releases included)
            int vk = e.Vk;
            if (e.Action == 3) { KeyUp(vk); legacy.Release(vk); return; }   // a real release (kitty protocol)
            if (KittyActive) { KeyDown(vk); return; }                        // press or repeat: its release will be reported
            // a press or an auto-repeat with no release to follow (a plain terminal, or a kitty protocol
            // terminal that doesn't report event types)
            legacy.Press(vk, now);
            KeyDown(vk);
            if (e.ModsKnown)
            {
                if ((e.Mods & VtInput.ModShift) != 0) { legacy.Press(VK_SHIFT, now); KeyDown(VK_SHIFT); }
                else if (legacy.Held(VK_SHIFT)) { legacy.Release(VK_SHIFT); KeyUp(VK_SHIFT); }
            }
        }

        static void OnTerminalMouse(TtyEvent e)
        {
            int cx, cy;
            if (PixelMouse)
            {
                ptrX = e.X; ptrY = e.Y;
                cx = Math.Max(0, e.X - 1) / Term.CellW;
                cy = Math.Max(0, e.Y - 1) / Term.CellH;
            }
            else
            {
                ptrX = (e.X - 0.5) * Term.CellW;
                ptrY = (e.Y - 0.5) * Term.CellH;
                cx = e.X - 1; cy = e.Y - 1;
            }
            havePtr = true;
            if (cx != MouseCellX || cy != MouseCellY) MouseCellMoved = true;
            MouseCellX = cx; MouseCellY = cy;
            if (e.Button == 4) { Wheel++; return; }
            if (e.Button == 5) { Wheel--; return; }
            if (e.Action == 1)
            {
                if (!FocusReports) Focused = true;   // clicking a window gives it the focus
                if (e.Button == 0) { termL = true; MouseCellClick = true; }
                else if (e.Button == 1) termM = true;
                else if (e.Button == 2) termR = true;
            }
            else if (e.Action == 3)
            {
                if (e.Button == 0 || e.Button == 3) termL = false;
                if (e.Button == 1 || e.Button == 3) termM = false;
                if (e.Button == 2 || e.Button == 3) termR = false;
            }
        }

        // ================================================================ kernel events

        static void OnKernel(bool mouseDevice, int type, int code, int value)
        {
            if (type == Evdev.EV_REL)
            {
                if (!Evdev.Grabbed || !Focused) return;
                if (code == Evdev.REL_X) MouseDX += value;
                else if (code == Evdev.REL_Y) MouseDY += value;
                else if (code == Evdev.REL_WHEEL && value != 0) Wheel += value > 0 ? 1 : -1;
                return;
            }
            if (type != Evdev.EV_KEY) return;
            if (code >= Evdev.BTN_LEFT && code <= Evdev.BTN_MIDDLE)
            {
                if (!Evdev.Grabbed) return;   // not playing: the terminal reports clicks for the menus
                bool on = value != 0;
                if (code == Evdev.BTN_LEFT) evL = on;
                else if (code == Evdev.BTN_RIGHT) evR = on;
                else evM = on;
                return;
            }
            if (!KeysFromKernel || code < 0 || code >= kernelKeys.Length) return;
            int vk = kernelKeys[code];
            if (vk == 0) return;
            if (Trace != null) Trace("kernel key " + VtInput.KeyName(vk) + (value == 0 ? " up" : value == 2 ? " repeat" : " down"));
            if (!Focused) return;
            if (value == 0) { KeyUp(vk); return; }
            KeyDown(vk);
            // without focus reports, check that the terminal sees this key too (or another window has the keyboard)
            if (value == 1 && !FocusReports && Echoes(vk) && kernelPressAt < 0) kernelPressAt = lastTime;
        }

        static bool Echoes(int vk)
        {
            return (vk >= 'A' && vk <= 'Z') || (vk >= '0' && vk <= '9') || vk == VK_SPACE || vk == VK_RETURN || vk == VK_ESCAPE ||
                   vk == VK_TAB || vk == VK_BACK || (vk >= VK_LEFT && vk <= VK_DOWN);
        }

        /// <summary>Linux key codes (input-event-codes.h) to our virtual keys. By position: WASD stays WASD on any layout.</summary>
        static int[] KernelKeyMap()
        {
            var m = new int[256];
            m[1] = VK_ESCAPE;
            for (int i = 0; i < 9; i++) m[2 + i] = '1' + i;
            m[11] = '0'; m[12] = VK_OEM_MINUS; m[13] = VK_OEM_PLUS; m[14] = VK_BACK; m[15] = VK_TAB;
            const string row1 = "QWERTYUIOP", row2 = "ASDFGHJKL", row3 = "ZXCVBNM";
            for (int i = 0; i < row1.Length; i++) m[16 + i] = row1[i];
            for (int i = 0; i < row2.Length; i++) m[30 + i] = row2[i];
            for (int i = 0; i < row3.Length; i++) m[44 + i] = row3[i];
            m[28] = VK_RETURN; m[29] = VK_LCONTROL; m[41] = VK_OEM_3; m[42] = VK_LSHIFT; m[54] = VK_RSHIFT;
            m[56] = VK_MENU; m[57] = VK_SPACE;
            for (int i = 0; i < 10; i++) m[59 + i] = VK_F1 + i;   // F1..F10
            m[87] = VK_F11; m[88] = VK_F12;
            m[71] = '7'; m[72] = '8'; m[73] = '9'; m[75] = '4'; m[76] = '5'; m[77] = '6'; m[79] = '1'; m[80] = '2'; m[81] = '3'; m[82] = '0';
            m[96] = VK_RETURN; m[97] = VK_RCONTROL; m[100] = VK_MENU;
            m[102] = VK_HOME; m[103] = VK_UP; m[104] = VK_PRIOR; m[105] = VK_LEFT; m[106] = VK_RIGHT; m[107] = VK_END; m[108] = VK_DOWN; m[109] = VK_NEXT;
            return m;
        }

        // ================================================================ shared state helpers

        static void KeyDown(int vk)
        {
            vk &= 255;
            if (vk == 0) return;
            if (!down[vk]) { hit[vk] = true; AnyKeyHit = true; }
            down[vk] = true;
            rep[vk] = true;
            if (vk == VK_LSHIFT || vk == VK_RSHIFT) down[VK_SHIFT] = true;
            if (vk == VK_LCONTROL || vk == VK_RCONTROL) down[VK_CONTROL] = true;
        }

        static void KeyUp(int vk)
        {
            vk &= 255;
            down[vk] = false;
            if (vk == VK_LSHIFT || vk == VK_RSHIFT) down[VK_SHIFT] = down[VK_LSHIFT] || down[VK_RSHIFT];
            if (vk == VK_LCONTROL || vk == VK_RCONTROL) down[VK_CONTROL] = down[VK_LCONTROL] || down[VK_RCONTROL];
        }

        // ================================================================ mouse look

        static void Capture(double dt)
        {
            // kernel mouse: grab it, but only once no button is held in the terminal (so the desktop never misses a release)
            if (MouseFromKernel && !Evdev.Grabbed && !termL && !termR && !termM)
            {
                Evdev.Grab(true);
                if (Evdev.Grabbed) { skipDelta = true; evL = evR = evM = false; }
            }
            if (!Evdev.Grabbed)
            {
                // terminal pointer: turn with its motion, and keep turning while it sits near the left or right edge
                if (!Captured) havePrev = false;
                if (havePtr)
                {
                    if (havePrev) { lookX += ptrX - prevX; lookY += ptrY - prevY; }
                    prevX = ptrX; prevY = ptrY;
                    havePrev = true;
                    double fx = ptrX / Math.Max(1.0, Term.Cols * (double)Term.CellW);
                    double edge = fx < EdgeZone ? (fx - EdgeZone) / EdgeZone : fx > 1 - EdgeZone ? (fx - (1 - EdgeZone)) / EdgeZone : 0;
                    lookX += Math.Max(-1, Math.Min(1, edge)) * EdgeTurn * dt;
                }
                MouseDX = (int)lookX; lookX -= MouseDX;
                MouseDY = (int)lookY; lookY -= MouseDY;
            }
            Captured = true;
        }

        public static void Release()
        {
            if (Evdev.Grabbed) { Evdev.Grab(false); evL = evR = evM = false; }
            if (!Captured) return;
            Captured = false;
            lookX = lookY = 0;
        }

        // ================================================================ diagnostics

        public static string Describe()
        {
            string keys = KittyActive ? "kitty" : KeysFromKernel ? "kernel" : "guessed";
            string mouse = NoMouse ? "off" : MouseFromKernel ? "kernel" : PixelMouse ? "pointer-px" : "pointer";
            return "keys=" + keys + " mouse=" + mouse + " focus=" + (FocusReports ? "yes" : "no") + (Evdev.PermissionDenied ? " evdev=denied" : "");
        }

        /// <summary>Plain-language summary for terminalhell --input.</summary>
        public static List<string> Status()
        {
            var l = new List<string>();
            l.Add("keyboard   : " + (KittyActive ? "kitty keyboard protocol - real key releases [OK]" :
                                     KeysFromKernel ? "read from /dev/input - real key releases [OK]" :
                                     "releases guessed from key repeat (holding two keys at once won't work well)"));
            if (Evdev.HasKeyboard || Evdev.HasMouse)
                l.Add("/dev/input : " + Evdev.Keyboards + " keyboard(s), " + Evdev.Mice + " mouse/mice readable");
            else if (Evdev.PermissionDenied)
                l.Add("/dev/input : not allowed (you are not in the \"input\" group)");
            else
                l.Add("/dev/input : " + (noEvdev ? "not used (--no-evdev)" : "no keyboards or mice found"));
            l.Add("mouse look : " + (NoMouse ? "off (--nomouse)" :
                                     MouseFromKernel ? "raw mouse from /dev/input, grabbed while playing [OK]" :
                                     "follows the pointer inside the window" + (PixelMouse ? " (pixel precise)" : "") + "; push it to a side edge to keep turning"));
            l.Add("focus      : " + (FocusReports ? "the terminal reports focus changes [OK]" : "no focus reports (the game can't tell when you switch windows)"));
            l.Add("cell size  : " + Term.CellW + " x " + Term.CellH + " pixels" + (Term.CellSizeKnown ? "" : " (a guess: the terminal didn't say)"));

            if (!KittyActive && !KeysFromKernel)
            {
                l.Add("");
                l.Add("For proper key releases, use a terminal with the kitty keyboard protocol: kitty, foot, Ghostty,");
                l.Add("Alacritty, or WezTerm with 'enable_kitty_keyboard = true' in its config. Or allow /dev/input:");
            }
            if (Evdev.PermissionDenied && !MouseFromKernel)
            {
                if (KittyActive || KeysFromKernel) l.Add("");
                l.Add("  sudo usermod -aG input $USER      (then log out and back in)");
                l.Add("  That gives real key releases and proper mouse look in any terminal. Note that it lets every");
                l.Add("  program you run read your keyboard and mouse directly.");
            }
            else if (Evdev.HasMouse && !FocusReports)
            {
                l.Add("");
                l.Add("/dev/input works, but this terminal doesn't report focus changes, so the game won't grab the mouse.");
            }
            return l;
        }
    }
}
