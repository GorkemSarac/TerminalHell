// TERMINAL HELL - keyboard (console input events) and mouse (raw input deltas + cursor lock).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TerminalHell
{
    static class Input
    {
        public const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        public const int VK_ESCAPE = 0x1B, VK_SPACE = 0x20, VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24;
        public const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        public const int VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72, VK_F4 = 0x73, VK_F5 = 0x74, VK_F10 = 0x79, VK_F11 = 0x7A, VK_F12 = 0x7B;
        public const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
        public const int VK_OEM_MINUS = 0xBD, VK_OEM_PLUS = 0xBB, VK_OEM_3 = 0xC0;

        static readonly bool[] down = new bool[256];
        static readonly bool[] hit = new bool[256];
        static readonly bool[] rep = new bool[256];
        public static readonly List<char> Typed = new List<char>();

        public static int MouseDX, MouseDY, Wheel;
        public static bool LButton, RButton, MButton, LHit, RHit;
        public static int MouseCellX = -1, MouseCellY = -1;
        public static bool MouseCellMoved, MouseCellClick;
        public static bool Focused = true;
        public static bool Captured;
        public static bool CaptureWanted;
        public static bool NoMouse;
        public static bool AnyKeyHit;

        static readonly INPUT_RECORD[] records = new INPUT_RECORD[128];
        static IntPtr rawHwnd;
        static bool rawOk, rawAbsolute;
        static readonly byte[] rawBuf = new byte[256];
        static int parkX, parkY;
        static bool skipDelta;
        static bool rawL, rawR, rawM;
        static bool conL, conR;

        public static void Init()
        {
            if (NoMouse) return;
            try
            {
                const int HWND_MESSAGE = -3;
                rawHwnd = Native.CreateWindowExW(0, "STATIC", "TerminalHellInput", 0, 0, 0, 0, 0, new IntPtr(HWND_MESSAGE), IntPtr.Zero, Native.GetModuleHandle(null), IntPtr.Zero);
                if (rawHwnd != IntPtr.Zero)
                {
                    var dev = new RAWINPUTDEVICE[1];
                    dev[0].usUsagePage = 1;
                    dev[0].usUsage = 2;
                    dev[0].dwFlags = Native.RIDEV_INPUTSINK;
                    dev[0].hwndTarget = rawHwnd;
                    rawOk = Native.RegisterRawInputDevices(dev, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                }
            }
            catch { rawOk = false; }
        }

        public static void Shutdown()
        {
            Release();
            if (rawHwnd != IntPtr.Zero) { Native.DestroyWindow(rawHwnd); rawHwnd = IntPtr.Zero; }
        }

        public static bool Down(int vk) { return down[vk & 255]; }
        public static bool Hit(int vk) { return hit[vk & 255]; }
        public static bool Rep(int vk) { return rep[vk & 255]; }
        public static bool Shift { get { return down[VK_SHIFT] || down[VK_LSHIFT] || down[VK_RSHIFT]; } }

        public static void ClearKeys()
        {
            Array.Clear(down, 0, 256);
            LButton = RButton = MButton = false;
            rawL = rawR = rawM = conL = conR = false;
        }

        public static void Update()
        {
            Array.Clear(hit, 0, 256);
            Array.Clear(rep, 0, 256);
            Typed.Clear();
            MouseDX = MouseDY = Wheel = 0;
            LHit = RHit = false;
            MouseCellMoved = MouseCellClick = false;
            AnyKeyHit = false;
            bool prevL = LButton, prevR = RButton;

            // ---- console input records (keyboard, console mouse, focus)
            int avail;
            while (Native.GetNumberOfConsoleInputEvents(Term.In, out avail) && avail > 0)
            {
                int read;
                if (!Native.ReadConsoleInputW(Term.In, records, records.Length, out read) || read <= 0) break;
                for (int i = 0; i < read; i++)
                {
                    var r = records[i];
                    if (r.EventType == Native.KEY_EVENT)
                    {
                        int vk = r.VirtualKeyCode & 255;
                        if (r.KeyDown != 0)
                        {
                            if (!down[vk]) { hit[vk] = true; AnyKeyHit = true; }
                            down[vk] = true;
                            rep[vk] = true;
                            if (r.UnicodeChar >= 32) Typed.Add((char)r.UnicodeChar);
                            // key events only arrive while our console has focus: self-correct the window handle
                            if (!Focused)
                            {
                                IntPtr fg = Native.GetForegroundWindow();
                                if (fg != IntPtr.Zero) Term.GameHwnd = fg;
                                Focused = true;
                            }
                        }
                        else down[vk] = false;
                        if (vk == VK_LSHIFT || vk == VK_RSHIFT) down[VK_SHIFT] = r.KeyDown != 0;
                    }
                    else if (r.EventType == Native.MOUSE_EVENT)
                    {
                        if (r.MouseX != MouseCellX || r.MouseY != MouseCellY) MouseCellMoved = true;
                        MouseCellX = r.MouseX; MouseCellY = r.MouseY;
                        if ((r.MouseEventFlags & Native.MOUSE_WHEELED) != 0)
                        {
                            if (!rawOk) Wheel += ((int)r.ButtonState >> 16) > 0 ? 1 : -1;
                        }
                        else
                        {
                            bool l = (r.ButtonState & 1) != 0, rr = (r.ButtonState & 2) != 0;
                            if (l && !conL) MouseCellClick = true;
                            conL = l; conR = rr;
                        }
                    }
                    else if (r.EventType == Native.FOCUS_EVENT)
                    {
                        if (r.SetFocus == 0) ClearKeys();
                    }
                }
            }

            // ---- raw mouse
            if (rawOk)
            {
                MSG msg;
                while (Native.PeekMessageW(out msg, rawHwnd, 0, 0, Native.PM_REMOVE))
                {
                    if (msg.message == Native.WM_INPUT) ReadRaw(msg.lParam);
                    Native.DispatchMessageW(ref msg);
                }
            }

            // ---- focus
            bool fgNow = Term.IsForeground();
            if (!fgNow && Focused) ClearKeys();
            Focused = fgNow;

            if (rawOk && !rawAbsolute) { LButton = rawL; RButton = rawR; MButton = rawM; }
            else { LButton = conL; RButton = conR; }
            if (!Focused) { LButton = RButton = MButton = false; MouseDX = MouseDY = Wheel = 0; }
            LHit = LButton && !prevL;
            RHit = RButton && !prevR;

            // ---- cursor capture for mouse look
            bool want = CaptureWanted && Focused && !NoMouse;
            if (want) Capture(); else Release();
            if (!Captured) { MouseDX = MouseDY = 0; }
            if (skipDelta) { MouseDX = MouseDY = 0; skipDelta = false; }
        }

        static void ReadRaw(IntPtr hRaw)
        {
            uint size = (uint)rawBuf.Length;
            int hs = 8 + 2 * IntPtr.Size;
            uint got = Native.GetRawInputData(hRaw, Native.RID_INPUT, rawBuf, ref size, (uint)hs);
            if (got == 0xFFFFFFFF || got < hs + 20) return;
            uint type = BitConverter.ToUInt32(rawBuf, 0);
            if (type != Native.RIM_TYPEMOUSE) return;
            ushort flags = BitConverter.ToUInt16(rawBuf, hs);
            ushort btn = BitConverter.ToUInt16(rawBuf, hs + 4);
            short data = BitConverter.ToInt16(rawBuf, hs + 6);
            int lx = BitConverter.ToInt32(rawBuf, hs + 12), ly = BitConverter.ToInt32(rawBuf, hs + 16);
            if ((flags & 1) != 0)
            {
                // absolute coordinates (remote desktop, tablets): fall back to cursor deltas
                rawAbsolute = true;
            }
            else
            {
                MouseDX += lx; MouseDY += ly;
            }
            if ((btn & 0x0001) != 0) rawL = true;
            if ((btn & 0x0002) != 0) rawL = false;
            if ((btn & 0x0004) != 0) rawR = true;
            if ((btn & 0x0008) != 0) rawR = false;
            if ((btn & 0x0010) != 0) rawM = true;
            if ((btn & 0x0020) != 0) rawM = false;
            if ((btn & 0x0400) != 0) Wheel += data > 0 ? 1 : -1;
        }

        static bool ParkPoint(out RECT clip)
        {
            clip = new RECT();
            RECT rc;
            if (!Native.GetClientRect(Term.GameHwnd, out rc)) return false;
            var tl = new POINT { X = rc.Left, Y = rc.Top };
            Native.ClientToScreen(Term.GameHwnd, ref tl);
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            if (w < 20 || h < 20) return false;
            // park the cursor low in the status bar area so it doesn't cover the view
            parkX = tl.X + w * 3 / 4;
            parkY = tl.Y + h - Math.Max(4, h / 30);
            clip.Left = parkX; clip.Top = parkY; clip.Right = parkX + 1; clip.Bottom = parkY + 1;
            return true;
        }

        static void Capture()
        {
            RECT clip;
            if (!ParkPoint(out clip)) { Release(); return; }
            if (!Captured) skipDelta = true;
            if (rawOk && !rawAbsolute)
            {
                Native.ClipCursor(ref clip);
            }
            else
            {
                POINT p;
                if (Native.GetCursorPos(out p) && Captured)
                {
                    MouseDX = p.X - parkX; MouseDY = p.Y - parkY;
                }
                Native.SetCursorPos(parkX, parkY);
            }
            Captured = true;
        }

        public static void Release()
        {
            if (!Captured) return;
            Captured = false;
            Native.ClipCursorNull(IntPtr.Zero);
        }
    }
}
