// TERMINAL HELL - input state the game reads (keys, mouse, focus). Each platform fills it in its own Update():
//   Windows: InputWin.cs (console input events + raw mouse input)
//   Linux  : linux/src/InputLinux.cs (terminal escape sequences, optionally /dev/input)
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    static partial class Input
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

        public static bool Down(int vk) { return down[vk & 255]; }
        public static bool Hit(int vk) { return hit[vk & 255]; }
        public static bool Rep(int vk) { return rep[vk & 255]; }
        public static bool Shift { get { return down[VK_SHIFT] || down[VK_LSHIFT] || down[VK_RSHIFT]; } }

        public static void ClearKeys()
        {
            Array.Clear(down, 0, 256);
            LButton = RButton = MButton = false;
            ClearPlatformKeys();
        }

        /// <summary>Lets the platform code forget its own copies of held keys and buttons.</summary>
        static partial void ClearPlatformKeys();
    }
}
