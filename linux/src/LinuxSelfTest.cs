// TERMINAL HELL - Linux: `terminalhell --dev-linux-selftest` checks the pure parts of the Linux input code
// (escape sequence decoding, guessed key releases, /dev/input device detection) without needing a terminal.
using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalHell
{
    static class LinuxSelfTest
    {
        static int checks, fails;

        public static int Run()
        {
            Sequences();
            SplitAndTimeout();
            Mouse();
            Replies();
            GuessedReleases();
            Devices();
            InputState();
            Console.WriteLine((checks - fails) + "/" + checks + " checks passed");
            return fails;
        }

        static void Check(bool ok, string what)
        {
            checks++;
            if (ok) return;
            fails++;
            Console.WriteLine("FAIL: " + what);
        }

        static List<TtyEvent> Feed(string s)
        {
            var p = new VtInput();
            var b = Encoding.UTF8.GetBytes(s);
            p.Feed(b, b.Length, 0);
            p.Tick(1);   // long after: resolves a trailing lone ESC
            return new List<TtyEvent>(p.Events);
        }

        static void Key(string input, int vk, int action, int mods, bool kitty, string what)
        {
            var ev = Feed(input);
            bool ok = ev.Count == 1 && ev[0].Type == TtyEventType.Key && ev[0].Vk == vk && ev[0].Action == action && ev[0].Mods == mods && ev[0].Kitty == kitty;
            Check(ok, what + ": got " + string.Join(" | ", ev.ConvertAll(e => e.ToString()).ToArray()));
        }

        static void Sequences()
        {
            Key("w", 'W', 1, 0, false, "plain w");
            Key("W", 'W', 1, VtInput.ModShift, false, "shifted W");
            Key("@", '2', 1, VtInput.ModShift, false, "shift+2 as @");
            Key(" ", Input.VK_SPACE, 1, 0, false, "space");
            Key("\r", Input.VK_RETURN, 1, 0, false, "enter");
            Key("\x7f", Input.VK_BACK, 1, 0, false, "backspace");
            Key("\t", Input.VK_TAB, 1, 0, false, "tab");
            Key("\x03", 'C', 1, VtInput.ModCtrl, false, "ctrl+c");
            Key("\x1b", Input.VK_ESCAPE, 1, 0, false, "lone escape");
            Key("\x1bw", 'W', 1, VtInput.ModAlt, false, "alt+w");
            Key("\x1b[A", Input.VK_UP, 1, 0, false, "CSI up");
            Key("\x1bOB", Input.VK_DOWN, 1, 0, false, "SS3 down");
            Key("\x1b[1;2C", Input.VK_RIGHT, 1, VtInput.ModShift, false, "shift+right");
            Key("\x1b[1;5D", Input.VK_LEFT, 1, VtInput.ModCtrl, false, "ctrl+left");
            Key("\x1bOP", Input.VK_F1, 1, 0, false, "F1");
            Key("\x1b[15~", Input.VK_F5, 1, 0, false, "F5");
            Key("\x1b[24~", Input.VK_F12, 1, 0, false, "F12");
            Key("\x1b[[E", Input.VK_F5, 1, 0, false, "Linux console F5");
            Key("\x1b[Z", Input.VK_TAB, 1, VtInput.ModShift, false, "shift+tab");
            Key("\x1b[5~", Input.VK_PRIOR, 1, 0, false, "page up");
            // kitty keyboard protocol
            Key("\x1b[119u", 'W', 1, 0, true, "kitty w press");
            Key("\x1b[119;1:2u", 'W', 2, 0, true, "kitty w repeat");
            Key("\x1b[119;1:3u", 'W', 3, 0, true, "kitty w release");
            Key("\x1b[119;2u", 'W', 1, VtInput.ModShift, true, "kitty shift+w");
            Key("\x1b[57441;2u", Input.VK_LSHIFT, 1, VtInput.ModShift, true, "kitty left shift press");
            Key("\x1b[57441;1:3u", Input.VK_LSHIFT, 3, 0, true, "kitty left shift release");
            Key("\x1b[99;5u", 'C', 1, VtInput.ModCtrl, true, "kitty ctrl+c");
            Key("\x1b[27u", Input.VK_ESCAPE, 1, 0, true, "kitty escape");
            Key("\x1b[13;1:3u", Input.VK_RETURN, 3, 0, true, "kitty enter release");
            Key("\x1b[1;1:3A", Input.VK_UP, 3, 0, true, "kitty up release");
            Key("\x1b[15;1:3~", Input.VK_F5, 3, 0, true, "kitty F5 release");
            Key("\x1b[57399u", '0', 1, 0, true, "kitty keypad 0");

            var e = Feed("\xe9");
            Check(e.Count == 1 && e[0].Ch == '\xe9' && e[0].Vk == 0, "UTF-8 character");
            e = Feed("wasd");
            Check(e.Count == 4 && e[0].Vk == 'W' && e[1].Vk == 'A' && e[2].Vk == 'S' && e[3].Vk == 'D', "four keys in one read");
            e = Feed("\x1b]11;rgb:0000/0000/0000\x07w");
            Check(e.Count == 1 && e[0].Vk == 'W', "an OSC answer is skipped");
            e = Feed("\x1b[I\x1b[O");
            Check(e.Count == 2 && e[0].Type == TtyEventType.FocusIn && e[1].Type == TtyEventType.FocusOut, "focus in / out");
        }

        static void SplitAndTimeout()
        {
            var p = new VtInput();
            byte[] a = Encoding.ASCII.GetBytes("\x1b["), b = Encoding.ASCII.GetBytes("A");
            p.Feed(a, a.Length, 0);
            p.Tick(0.01);
            Check(p.Events.Count == 0, "half a sequence waits");
            p.Feed(b, b.Length, 0.02);
            Check(p.Events.Count == 1 && p.Events[0].Vk == Input.VK_UP, "sequence split over two reads");

            p = new VtInput();
            var esc = new byte[] { 0x1B };
            p.Feed(esc, 1, 0);
            p.Tick(0.02);
            Check(p.Events.Count == 0, "a lone ESC waits for more bytes");
            p.Tick(0.06);
            Check(p.Events.Count == 1 && p.Events[0].Vk == Input.VK_ESCAPE, "a lone ESC becomes the Escape key after the timeout");

            p = new VtInput();
            var mouse = Encoding.ASCII.GetBytes("\x1b[<0;12");
            var rest = Encoding.ASCII.GetBytes(";7M");
            p.Feed(mouse, mouse.Length, 0);
            p.Feed(rest, rest.Length, 0.01);
            Check(p.Events.Count == 1 && p.Events[0].Type == TtyEventType.Mouse && p.Events[0].X == 12 && p.Events[0].Y == 7, "mouse report split over two reads");
        }

        static void MouseCase(string input, int button, int action, int x, int y, string what)
        {
            var ev = Feed(input);
            bool ok = ev.Count == 1 && ev[0].Type == TtyEventType.Mouse && ev[0].Button == button && ev[0].Action == action && ev[0].X == x && ev[0].Y == y;
            Check(ok, what + ": got " + string.Join(" | ", ev.ConvertAll(e => e.ToString()).ToArray()));
        }

        static void Mouse()
        {
            MouseCase("\x1b[<0;10;5M", 0, 1, 10, 5, "SGR left press");
            MouseCase("\x1b[<0;10;5m", 0, 3, 10, 5, "SGR left release");
            MouseCase("\x1b[<2;3;4M", 2, 1, 3, 4, "SGR right press");
            MouseCase("\x1b[<35;11;6M", 3, 0, 11, 6, "SGR motion without buttons");
            MouseCase("\x1b[<32;11;6M", 0, 0, 11, 6, "SGR drag with the left button");
            MouseCase("\x1b[<64;1;1M", 4, 1, 1, 1, "SGR wheel up");
            MouseCase("\x1b[<65;1;1M", 5, 1, 1, 1, "SGR wheel down");
            MouseCase("\x1b[<0;1250;690M", 0, 1, 1250, 690, "SGR pixel coordinates");
            MouseCase("\x1b[M" + (char)32 + (char)37 + (char)39, 0, 1, 5, 7, "X10 press");
            MouseCase("\x1b[M" + (char)35 + (char)37 + (char)39, 3, 3, 5, 7, "X10 release");
        }

        static void Replies()
        {
            var e = Feed("\x1b[?11u");
            Check(e.Count == 1 && e[0].Type == TtyEventType.KittyFlags && e[0].X == 11, "kitty flags answer");
            e = Feed("\x1b[?62;22;52c");
            Check(e.Count == 1 && e[0].Type == TtyEventType.DeviceAttributes, "device attributes answer");
            e = Feed("\x1b[?1004;2$y");
            Check(e.Count == 1 && e[0].Type == TtyEventType.ModeReport && e[0].X == 1004 && e[0].Y == 2, "mode report answer");
            e = Feed("\x1b[6;18;9t");
            Check(e.Count == 1 && e[0].Type == TtyEventType.CellSize && e[0].X == 9 && e[0].Y == 18, "cell size answer");
            e = Feed("\x1b[?11u\x1b[?1004;1$y\x1b[?1016;2$y\x1b[6;20;10t\x1b[?62;22c");
            Check(e.Count == 5, "all start-up answers in one read");
        }

        static void GuessedReleases()
        {
            var k = new LegacyKeys();
            var rel = new List<int>();
            Check(k.Press('W', 0), "first press is new");
            k.Expire(0.3, rel);
            Check(rel.Count == 0 && k.Held('W'), "held through the repeat delay");
            k.Expire(0.7, rel);
            Check(rel.Count == 1 && rel[0] == 'W', "a single tap is let go after the repeat delay");

            // held for a second on a keyboard with a 0.5 s delay and 30 repeats per second
            k = new LegacyKeys();
            rel.Clear();
            k.Press('A', 10);
            double t = 10.5;
            for (; t < 11.5; t += 1 / 30.0)
            {
                Check(!k.Press('A', t), "auto-repeat is not a new press");
                k.Expire(t + 0.01, rel);
            }
            Check(rel.Count == 0, "held while repeats keep coming");
            k.Expire(t + 0.2, rel);
            Check(rel.Count == 1, "let go soon after the repeats stop");

            // a keyboard with a longer delay (0.66 s): learned after the first hold
            k = new LegacyKeys();
            rel.Clear();
            k.Press('D', 0);
            k.Expire(0.62, rel);                       // our guess (0.58 s) ran out before the first repeat
            k.Press('D', 0.66);
            for (double r = 0.66 + 1 / 30.0; r < 1.2; r += 1 / 30.0) k.Press('D', r);
            Check(k.RepeatDelay > 0.6, "learns a longer repeat delay (now " + k.RepeatDelay.ToString("0.000") + ")");
        }

        static void Devices()
        {
            const string proc =
                "I: Bus=0011 Vendor=0001 Product=0001 Version=ab41\n" +
                "N: Name=\"AT Translated Set 2 keyboard\"\n" +
                "H: Handlers=sysrq kbd event3 leds \n" +
                "B: PROP=0\n" +
                "B: EV=120013\n" +
                "B: KEY=402000000 3803078f800d001 feffffdfffefffff fffffffffffffffe\n" +
                "\n" +
                "I: Bus=0003 Vendor=046d Product=c077 Version=0111\n" +
                "N: Name=\"Logitech USB Optical Mouse\"\n" +
                "H: Handlers=mouse0 event5 \n" +
                "B: EV=17\n" +
                "B: KEY=ff0000 0 0 0 0\n" +
                "B: REL=903\n" +
                "\n" +
                "I: Bus=0018 Vendor=06cb Product=7e7e Version=0100\n" +
                "N: Name=\"SynPS/2 Synaptics TouchPad\"\n" +
                "H: Handlers=mouse1 event6 \n" +
                "B: EV=b\n" +
                "B: KEY=e520 10000 0 0 0 0\n" +
                "B: ABS=660800011000003\n" +
                "\n" +
                "I: Bus=0019 Vendor=0000 Product=0001 Version=0000\n" +
                "N: Name=\"Power Button\"\n" +
                "H: Handlers=kbd event0 \n" +
                "B: EV=3\n" +
                "B: KEY=10000000000000 0\n";
            var list = Evdev.Parse(proc);
            Check(list.Count == 2, "finds one keyboard and one mouse (found " + list.Count + ")");
            if (list.Count == 2)
            {
                Check(list[0].Handler == "event3" && list[0].Keyboard && !list[0].Mouse, "the keyboard is event3");
                Check(list[1].Handler == "event5" && list[1].Mouse && !list[1].Keyboard, "the mouse is event5");
            }
            Check(Evdev.Bit("ff0000 0 0 0 0", 0x110) && !Evdev.Bit("ff0000 0 0 0 0", 0x100), "bitmap words are 64 bits, most significant first");
        }

        /// <summary>One game frame of input with the given terminal bytes (null = nothing arrived).</summary>
        static void Frame(double t, string bytes)
        {
            Input.BeginFrame(t);
            if (bytes != null) Input.FeedTerminal(Encoding.UTF8.GetBytes(bytes), t);
            Input.EndFrame(t);
        }

        /// <summary>The whole input path (bytes -> events -> the key and mouse state the game reads), per kind of terminal.</summary>
        static void InputState()
        {
            // a plain terminal: releases are guessed
            Input.ResetForTest(0);
            Frame(0.0, "\x1b[?62;22c");
            Check(Input.DetectDone && !Input.KittyActive, "plain terminal detected");
            Frame(0.1, "w");
            Check(Input.Down('W') && Input.Hit('W'), "plain: w goes down");
            Frame(0.4, null);
            Check(Input.Down('W') && !Input.Hit('W'), "plain: w held through the repeat delay");
            Frame(0.8, null);
            Check(!Input.Down('W'), "plain: w let go when no repeat came");
            Frame(0.9, "W");
            Check(Input.Down('W') && Input.Hit('W') && Input.Shift, "plain: uppercase W means Shift is held");
            Frame(0.95, "w");
            Check(Input.Down('W') && !Input.Hit('W') && !Input.Shift, "plain: a lowercase repeat lets Shift go");

            // a kitty protocol terminal: real releases, several keys at once
            Input.ResetForTest(10);
            Frame(10.0, "\x1b[?11u\x1b[?1004;2$y\x1b[?1016;0$y\x1b[?62;22c");
            Check(Input.KittyActive && Input.FocusReports && !Input.PixelMouse, "kitty terminal detected");
            Frame(10.1, "\x1b[119u");
            Check(Input.Down('W') && Input.Hit('W'), "kitty: w goes down");
            Frame(12.0, null);
            Check(Input.Down('W'), "kitty: w stays down without any repeats");
            Frame(12.1, "\x1b[100u");
            Check(Input.Down('W') && Input.Down('D'), "kitty: two keys held at once");
            Frame(12.2, "\x1b[119;1:3u");
            Check(!Input.Down('W') && Input.Down('D'), "kitty: w released, d still held");
            Frame(12.3, "\x1b[57441;2u");
            Check(Input.Shift, "kitty: shift down");
            Frame(12.4, "\x1b[57441;1:3u");
            Check(!Input.Shift, "kitty: shift up");
            Frame(12.5, "\x1b[O");
            Check(!Input.Focused && !Input.Down('D'), "focus out lets every key go");
            Frame(12.6, "\x1b[I");
            Check(Input.Focused, "focus back in");

            // kitty protocol without event types: releases are still guessed, so Esc works every time
            Input.ResetForTest(20);
            Frame(20.0, "\x1b[?1u\x1b[?62c");
            Check(!Input.KittyActive, "kitty without event types isn't trusted for releases");
            Frame(20.1, "\x1b[27u");
            Check(Input.Hit(Input.VK_ESCAPE), "escape as CSI u hits");
            Frame(21.0, null);
            Frame(21.1, "\x1b[27u");
            Check(Input.Hit(Input.VK_ESCAPE), "escape as CSI u hits again later");

            // the mouse: clicks for menus, pointer look while playing
            Input.ResetForTest(30);
            Term.Cols = 100;
            Frame(30.0, "\x1b[?62c");
            Frame(30.1, "\x1b[<0;10;5M");
            Check(Input.LButton && Input.LHit && Input.MouseCellClick && Input.MouseCellX == 9 && Input.MouseCellY == 4, "click on cell 9,4");
            Frame(30.2, "\x1b[<0;10;5m");
            Check(!Input.LButton, "button released");
            Input.CaptureWanted = true;
            Frame(30.3, "\x1b[<35;50;20M");
            Frame(30.4, "\x1b[<35;60;20M");
            Check(Input.Captured && Input.MouseDX == 10 * Term.CellW, "pointer look: 10 cells to the right (got " + Input.MouseDX + ")");
            Frame(30.5, "\x1b[<35;99;20M");
            int turned = 0;
            for (int i = 0; i < 10; i++) { Frame(30.6 + i * 0.016, null); turned += Input.MouseDX; }
            Check(turned > 100, "pointer at the right edge keeps turning right (got " + turned + ")");
            Input.CaptureWanted = false;
            Frame(31.0, "\x1b[<64;50;20M");
            Check(!Input.Captured && Input.Wheel == 1, "wheel up in a menu");
            Input.ResetForTest(40);
        }
    }
}
