// TERMINAL HELL - Linux: decodes the bytes a terminal sends into key, mouse, focus and reply events.
// Understands plain keys and xterm/VT sequences, the kitty keyboard protocol (press / repeat / release),
// SGR and X10 mouse reports, and the answers to the queries InputLinux sends at start-up.
// Pure code (no system calls), so the self test can run it anywhere.
using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalHell
{
    enum TtyEventType { Key, Mouse, FocusIn, FocusOut, KittyFlags, DeviceAttributes, ModeReport, CellSize }

    struct TtyEvent
    {
        public TtyEventType Type;
        public int Vk;          // Key: Input.VK_* code (0 = not a game key)
        public char Ch;         // Key: the character typed (0 = none)
        public int Action;      // Key: 1 press, 2 repeat, 3 release.  Mouse: 1 press, 3 release, 0 motion
        public int Mods;        // VtInput.ModShift / ModAlt / ModCtrl
        public bool ModsKnown;  // Key: the sequence says for sure whether Shift is held (letters, cursor/function keys)
        public bool Kitty;      // Key: kitty keyboard protocol event (the terminal also reports the release)
        public int Button;      // Mouse: 0 left, 1 middle, 2 right, 3 none, 4 wheel up, 5 wheel down
        public int X, Y;        // Mouse: 1-based position (cells, or pixels in SGR-pixel mode).  Replies: their values

        public override string ToString()
        {
            switch (Type)
            {
                case TtyEventType.Key:
                    return "key " + VtInput.KeyName(Vk) + (Ch >= 32 ? " '" + Ch + "'" : "") + " " + (Action == 3 ? "up" : Action == 2 ? "repeat" : "down") +
                        ((Mods & VtInput.ModShift) != 0 ? " +shift" : "") + ((Mods & VtInput.ModCtrl) != 0 ? " +ctrl" : "") + ((Mods & VtInput.ModAlt) != 0 ? " +alt" : "") +
                        (Kitty ? " (kitty)" : "");
                case TtyEventType.Mouse:
                    string b = Button == 4 ? "wheel up" : Button == 5 ? "wheel down" : Button == 0 ? "left" : Button == 1 ? "middle" : Button == 2 ? "right" : "none";
                    return "mouse " + (Action == 1 ? "press " : Action == 3 ? "release " : "move ") + b + " at " + X + "," + Y;
                case TtyEventType.ModeReport: return "mode " + X + " = " + Y;
                case TtyEventType.CellSize: return "cell " + X + "x" + Y + " px";
                case TtyEventType.KittyFlags: return "kitty flags " + X;
                default: return Type.ToString();
            }
        }
    }

    sealed class VtInput
    {
        public const int ModShift = 1, ModAlt = 2, ModCtrl = 4;
        /// <summary>How long an unfinished sequence may wait for the rest of its bytes. A lone ESC that stays alone this long is the Esc key.</summary>
        public const double Timeout = 0.05;

        public readonly List<TtyEvent> Events = new List<TtyEvent>();
        byte[] buf = new byte[4096];
        int len;
        double waitSince = -1;

        public void Feed(byte[] data, int n, double now)
        {
            if (len + n > buf.Length) Array.Resize(ref buf, Math.Max(buf.Length * 2, len + n));
            Array.Copy(data, 0, buf, len, n);
            len += n;
            Parse(now);
        }

        /// <summary>Once per frame: resolves an unfinished sequence (usually the Esc key) once it has waited long enough.</summary>
        public void Tick(double now)
        {
            if (len > 0) Parse(now);
        }

        void Parse(double now)
        {
            int i = 0;
            while (i < len)
            {
                int used = ParseOne(i);
                if (used == 0)
                {
                    // incomplete: wait for the rest, unless it has been waiting too long
                    if (waitSince < 0) waitSince = now;
                    if (now - waitSince < Timeout) break;
                    if (buf[i] == 0x1B) Key(Input.VK_ESCAPE, '\0', 0, false, 1, false);
                    used = 1;
                }
                waitSince = -1;
                i += used;
            }
            if (i > 0)
            {
                Array.Copy(buf, i, buf, 0, len - i);
                len -= i;
            }
        }

        /// <summary>Decodes the sequence at buf[i]. Returns the bytes used, or 0 when more bytes are needed.</summary>
        int ParseOne(int i)
        {
            byte b = buf[i];
            if (b == 0x1B)
            {
                if (i + 1 >= len) return 0;
                byte c = buf[i + 1];
                if (c == '[') return Csi(i);
                if (c == 'O')
                {
                    if (i + 2 >= len) return 0;
                    Ss3(buf[i + 2]);
                    return 3;
                }
                if (c == ']' || c == 'P' || c == '_' || c == '^') return SkipString(i);   // OSC / DCS / APC / PM answers
                if (c == 0x1B) { Key(Input.VK_ESCAPE, '\0', 0, false, 1, false); return 1; }
                if (c >= 0x20 && c < 0x7F) { Plain(c, ModAlt); return 2; }   // Alt + key
                if (c < 0x20 || c == 0x7F) { Control(c, ModAlt); return 2; }
                Key(Input.VK_ESCAPE, '\0', 0, false, 1, false);
                return 1;
            }
            if (b < 0x20 || b == 0x7F) { Control(b, 0); return 1; }
            if (b < 0x80) { Plain(b, 0); return 1; }
            // UTF-8 character
            int need = b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : b >= 0xC0 ? 2 : 1;
            if (need == 1) return 1;   // stray continuation byte
            if (i + need > len) return 0;
            string s = Encoding.UTF8.GetString(buf, i, need);
            if (s.Length > 0) Key(0, s[0], 0, false, 1, false);
            return need;
        }

        int SkipString(int i)
        {
            for (int j = i + 2; j < len; j++)
            {
                if (buf[j] == 0x07) return j + 1 - i;                                   // BEL
                if (buf[j] == 0x1B && j + 1 < len && buf[j + 1] == '\\') return j + 2 - i;   // ST
                if (j - i > 4096) return j - i;
            }
            return 0;
        }

        // ---------------------------------------------------------------- single bytes

        static readonly string shiftedDigits = ")!@#$%^&*(";

        void Plain(byte c, int mods)
        {
            char ch = (char)c;
            if (c >= 'a' && c <= 'z') { Key(c - 32, ch, mods, true, 1, false); return; }
            if (c >= 'A' && c <= 'Z') { Key(c, ch, mods | ModShift, true, 1, false); return; }
            if (c >= '0' && c <= '9') { Key(c, ch, mods, true, 1, false); return; }
            int d = shiftedDigits.IndexOf(ch);   // US layout: Shift+2 = '@' still selects weapon 2 while running
            if (d >= 0) { Key('0' + d, ch, mods | ModShift, true, 1, false); return; }
            switch (ch)
            {
                case ' ': Key(Input.VK_SPACE, ch, mods, false, 1, false); return;
                case '-': Key(Input.VK_OEM_MINUS, ch, mods, false, 1, false); return;
                case '_': Key(Input.VK_OEM_MINUS, ch, mods | ModShift, false, 1, false); return;
                case '=': Key(Input.VK_OEM_PLUS, ch, mods, false, 1, false); return;
                case '+': Key(Input.VK_OEM_PLUS, ch, mods | ModShift, false, 1, false); return;
                case '`': case '~': Key(Input.VK_OEM_3, ch, mods, false, 1, false); return;
            }
            Key(0, ch, mods, false, 1, false);
        }

        void Control(byte c, int mods)
        {
            switch (c)
            {
                case 0x0D: case 0x0A: Key(Input.VK_RETURN, '\0', mods, false, 1, false); return;
                case 0x09: Key(Input.VK_TAB, '\0', mods, false, 1, false); return;
                case 0x7F: case 0x08: Key(Input.VK_BACK, '\0', mods, false, 1, false); return;
                case 0x00: Key(Input.VK_SPACE, '\0', mods | ModCtrl, false, 1, false); return;
            }
            if (c >= 1 && c <= 26) Key('A' + c - 1, '\0', mods | ModCtrl, false, 1, false);   // Ctrl+letter (0x03 = Ctrl+C)
        }

        void Ss3(byte f)
        {
            int vk = CursorKey(f);
            if (f == 'M') vk = Input.VK_RETURN;   // keypad Enter in application mode
            if (vk != 0) Key(vk, '\0', 0, true, 1, false);
        }

        static int CursorKey(byte f)
        {
            switch ((char)f)
            {
                case 'A': return Input.VK_UP;
                case 'B': return Input.VK_DOWN;
                case 'C': return Input.VK_RIGHT;
                case 'D': return Input.VK_LEFT;
                case 'H': return Input.VK_HOME;
                case 'F': return Input.VK_END;
                case 'P': return Input.VK_F1;
                case 'Q': return Input.VK_F2;
                case 'R': return Input.VK_F3;
                case 'S': return Input.VK_F4;
            }
            return 0;
        }

        // ---------------------------------------------------------------- CSI sequences

        int Csi(int i)
        {
            int j = i + 2;
            if (j >= len) return 0;
            if (buf[j] == '[')
            {
                // Linux console function keys: ESC [ [ A..E
                if (j + 1 >= len) return 0;
                byte f = buf[j + 1];
                if (f >= 'A' && f <= 'E') Key(Input.VK_F1 + (f - 'A'), '\0', 0, true, 1, false);
                return j + 2 - i;
            }
            if (buf[j] == 'M')
            {
                // X10 mouse report: ESC [ M button x y (each byte + 32)
                if (j + 3 >= len) return 0;
                int cb = buf[j + 1] - 32;
                Mouse(cb, buf[j + 2] - 32, buf[j + 3] - 32, (cb & 3) == 3 ? 3 : 1, true);
                return j + 4 - i;
            }
            int start = j;
            while (j < len)
            {
                byte c = buf[j];
                if (c >= 0x40 && c <= 0x7E) break;              // final byte
                if (c < 0x20 || c > 0x7E || j - start > 64) return j - i;   // broken sequence: drop it
                j++;
            }
            if (j >= len) return 0;
            Dispatch(start, j, (char)buf[j]);
            return j + 1 - i;
        }

        // parsed parameters: ps[k][s] = sub-parameter s of parameter k, -1 when left out
        readonly List<int[]> ps = new List<int[]>();
        readonly List<int> sub = new List<int>();

        int P(int k, int s, int def)
        {
            if (k >= ps.Count || s >= ps[k].Length || ps[k][s] < 0) return def;
            return ps[k][s];
        }

        void Dispatch(int start, int end, char final)
        {
            char priv = '\0', inter = '\0';
            int p = start;
            if (p < end && buf[p] >= '<' && buf[p] <= '?') priv = (char)buf[p++];
            ps.Clear(); sub.Clear();
            int num = -1;
            bool any = false;
            for (; p < end; p++)
            {
                byte c = buf[p];
                if (c >= '0' && c <= '9') { num = (num < 0 ? 0 : Math.Min(num, 10000000)) * 10 + (c - '0'); any = true; }
                else if (c == ':') { sub.Add(num); num = -1; any = true; }
                else if (c == ';') { sub.Add(num); ps.Add(sub.ToArray()); sub.Clear(); num = -1; any = true; }
                else if (c >= 0x20 && c <= 0x2F) inter = (char)c;
            }
            if (any) { sub.Add(num); ps.Add(sub.ToArray()); }

            if (priv == '<')
            {
                if (final == 'M' || final == 'm') Mouse(P(0, 0, 0), P(1, 0, 1), P(2, 0, 1), final == 'M' ? 1 : 3, false);
                return;
            }
            if (priv == '?')
            {
                if (final == 'u') Emit(TtyEventType.KittyFlags, P(0, 0, 0), 0);
                else if (final == 'c') Emit(TtyEventType.DeviceAttributes, P(0, 0, 0), 0);
                else if (final == 'y' && inter == '$') Emit(TtyEventType.ModeReport, P(0, 0, 0), P(1, 0, 0));
                return;
            }
            if (priv != '\0') return;

            int mods = Mods(P(1, 0, 1));
            int action = P(1, 1, 1);   // kitty event type; plain terminals leave it out (= press)
            bool kitty = ps.Count > 1 && ps[1].Length > 1;
            switch (final)
            {
                case 'u':
                    {
                        int code = P(0, 0, 0);
                        int vk = KittyKey(code);
                        char ch = '\0';
                        if (action != 3 && code >= 32 && code < 127)
                            ch = (mods & ModShift) != 0 && code >= 'a' && code <= 'z' ? (char)(code - 32) : (char)code;
                        Key(vk, ch, mods, true, action, true);
                        return;
                    }
                case '~':
                    {
                        int vk = TildeKey(P(0, 0, 0));
                        if (vk != 0) Key(vk, '\0', mods, true, action, kitty);
                        return;
                    }
                case 't':
                    if (P(0, 0, 0) == 6) Emit(TtyEventType.CellSize, P(2, 0, 0), P(1, 0, 0));   // CSI 6 ; height ; width t
                    return;
                case 'I':
                    if (ps.Count == 0) Emit(TtyEventType.FocusIn, 0, 0);
                    return;
                case 'O':
                    if (ps.Count == 0) Emit(TtyEventType.FocusOut, 0, 0);
                    return;
                case 'Z':
                    Key(Input.VK_TAB, '\0', mods | ModShift, true, action, kitty);
                    return;
            }
            int cursor = CursorKey((byte)final);
            if (cursor != 0) Key(cursor, '\0', mods, true, action, kitty);
        }

        /// <summary>xterm / kitty modifier parameter (1 + bits) to our bits.</summary>
        static int Mods(int param)
        {
            int m = Math.Max(0, param - 1);
            return m & (ModShift | ModAlt | ModCtrl);
        }

        static int TildeKey(int n)
        {
            switch (n)
            {
                case 1: case 7: return Input.VK_HOME;
                case 4: case 8: return Input.VK_END;
                case 5: return Input.VK_PRIOR;
                case 6: return Input.VK_NEXT;
                case 11: return Input.VK_F1;
                case 12: return Input.VK_F2;
                case 13: return Input.VK_F3;
                case 14: return Input.VK_F4;
                case 15: return Input.VK_F5;
                case 23: return Input.VK_F11;
                case 24: return Input.VK_F12;
            }
            if (n >= 17 && n <= 21) return Input.VK_F1 + 5 + (n - 17);   // F6..F10
            return 0;
        }

        /// <summary>Kitty key codes: Unicode for text keys, private-use numbers for the rest.</summary>
        static int KittyKey(int code)
        {
            if (code >= 'a' && code <= 'z') return code - 32;
            if (code >= 'A' && code <= 'Z') return code;
            if (code >= '0' && code <= '9') return code;
            if (code >= 57399 && code <= 57408) return '0' + (code - 57399);   // keypad digits
            switch (code)
            {
                case 27: return Input.VK_ESCAPE;
                case 13: return Input.VK_RETURN;
                case 9: return Input.VK_TAB;
                case 127: case 8: return Input.VK_BACK;
                case 32: return Input.VK_SPACE;
                case '-': return Input.VK_OEM_MINUS;
                case '=': case '+': return Input.VK_OEM_PLUS;
                case '`': return Input.VK_OEM_3;
                case 57441: return Input.VK_LSHIFT;
                case 57447: return Input.VK_RSHIFT;
                case 57442: return Input.VK_LCONTROL;
                case 57448: return Input.VK_RCONTROL;
                case 57443: case 57449: return Input.VK_MENU;
                case 57414: return Input.VK_RETURN;   // keypad Enter
                case 57417: return Input.VK_LEFT;
                case 57418: return Input.VK_RIGHT;
                case 57419: return Input.VK_UP;
                case 57420: return Input.VK_DOWN;
                case 57421: return Input.VK_PRIOR;
                case 57422: return Input.VK_NEXT;
                case 57423: return Input.VK_HOME;
                case 57424: return Input.VK_END;
            }
            return 0;
        }

        // ---------------------------------------------------------------- output

        void Key(int vk, char ch, int mods, bool modsKnown, int action, bool kitty)
        {
            var e = new TtyEvent();
            e.Type = TtyEventType.Key;
            e.Vk = vk; e.Ch = ch; e.Mods = mods; e.ModsKnown = modsKnown; e.Action = action; e.Kitty = kitty;
            Events.Add(e);
        }

        void Mouse(int cb, int x, int y, int action, bool x10)
        {
            var e = new TtyEvent();
            e.Type = TtyEventType.Mouse;
            e.X = x; e.Y = y;
            e.Mods = ((cb & 4) != 0 ? ModShift : 0) | ((cb & 8) != 0 ? ModAlt : 0) | ((cb & 16) != 0 ? ModCtrl : 0);
            int btn = cb & 3;
            if ((cb & 64) != 0)
            {
                if (btn > 1) return;   // horizontal wheel
                e.Button = btn == 0 ? 4 : 5;
                e.Action = 1;
            }
            else if ((cb & 32) != 0)
            {
                e.Button = btn;
                e.Action = 0;       // motion (btn = the button held while moving, 3 = none)
            }
            else
            {
                e.Button = btn;
                e.Action = action;
                if (x10 && btn == 3) e.Action = 3;   // X10 release doesn't say which button
            }
            Events.Add(e);
        }

        void Emit(TtyEventType t, int x, int y)
        {
            var e = new TtyEvent();
            e.Type = t; e.X = x; e.Y = y;
            Events.Add(e);
        }

        public static string KeyName(int vk)
        {
            if ((vk >= 'A' && vk <= 'Z') || (vk >= '0' && vk <= '9')) return ((char)vk).ToString();
            if (vk >= Input.VK_F1 && vk <= Input.VK_F12) return "F" + (vk - Input.VK_F1 + 1);
            switch (vk)
            {
                case 0: return "(none)";
                case Input.VK_ESCAPE: return "ESC";
                case Input.VK_RETURN: return "ENTER";
                case Input.VK_TAB: return "TAB";
                case Input.VK_BACK: return "BACKSPACE";
                case Input.VK_SPACE: return "SPACE";
                case Input.VK_UP: return "UP";
                case Input.VK_DOWN: return "DOWN";
                case Input.VK_LEFT: return "LEFT";
                case Input.VK_RIGHT: return "RIGHT";
                case Input.VK_HOME: return "HOME";
                case Input.VK_END: return "END";
                case Input.VK_PRIOR: return "PAGEUP";
                case Input.VK_NEXT: return "PAGEDOWN";
                case Input.VK_SHIFT: return "SHIFT";
                case Input.VK_LSHIFT: return "LSHIFT";
                case Input.VK_RSHIFT: return "RSHIFT";
                case Input.VK_CONTROL: return "CTRL";
                case Input.VK_LCONTROL: return "LCTRL";
                case Input.VK_RCONTROL: return "RCTRL";
                case Input.VK_MENU: return "ALT";
                case Input.VK_OEM_MINUS: return "MINUS";
                case Input.VK_OEM_PLUS: return "PLUS";
                case Input.VK_OEM_3: return "BACKQUOTE";
            }
            return "#" + vk;
        }
    }
}
