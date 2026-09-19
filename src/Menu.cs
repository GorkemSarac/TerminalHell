// TERMINAL HELL - simple keyboard/mouse driven menus and the title screen fire effect.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    sealed class MenuItem
    {
        public string Text;
        public Func<string> Value;        // optional value shown on the right ("< ON >")
        public Action<int> Adjust;        // left / right
        public Action Activate;           // enter / click
        public bool Spacer;
    }

    sealed class Menu
    {
        public string Title;
        public readonly List<MenuItem> Items = new List<MenuItem>();
        public int Sel;
        public Action OnBack;
        public string[] Lines;            // free text (help screens)
        public int[] ItemRows = new int[0];
        public int ItemX, ItemW;

        public Menu(string title) { Title = title; }

        public MenuItem Add(string text, Action activate)
        {
            var it = new MenuItem { Text = text, Activate = activate };
            Items.Add(it);
            return it;
        }

        public MenuItem AddValue(string text, Func<string> value, Action<int> adjust)
        {
            var it = new MenuItem { Text = text, Value = value, Adjust = adjust };
            Items.Add(it);
            return it;
        }

        public void Move(int d)
        {
            if (Items.Count == 0) return;
            for (int i = 0; i < Items.Count; i++)
            {
                Sel = (Sel + d + Items.Count) % Items.Count;
                if (!Items[Sel].Spacer) break;
            }
            Audio.Play(Sfx.MenuMove, 0.6f, 0, 1, 0);
        }

        /// <summary>Handles input. Returns false when the menu wants to close (Esc).</summary>
        public bool Update()
        {
            if (Input.Rep(Input.VK_UP) || Input.Rep('W')) Move(-1);
            if (Input.Rep(Input.VK_DOWN) || Input.Rep('S')) Move(1);
            if (Input.Wheel != 0) Move(Input.Wheel > 0 ? -1 : 1);
            if (Items.Count > 0 && Input.MouseCellMoved)
                for (int i = 0; i < ItemRows.Length && i < Items.Count; i++)
                    if (ItemRows[i] == Input.MouseCellY && !Items[i].Spacer && Input.MouseCellX >= ItemX - 2 && Input.MouseCellX < ItemX + ItemW + 2)
                    {
                        if (Sel != i) { Sel = i; Audio.Play(Sfx.MenuMove, 0.4f, 0, 1, 0); }
                    }
            var it = Items.Count > 0 ? Items[Sel] : null;
            if (it != null && it.Adjust != null)
            {
                if (Input.Rep(Input.VK_LEFT) || Input.Rep('A')) { it.Adjust(-1); Audio.Play(Sfx.MenuMove, 0.6f, 0, 1, 0); }
                if (Input.Rep(Input.VK_RIGHT) || Input.Rep('D')) { it.Adjust(1); Audio.Play(Sfx.MenuMove, 0.6f, 0, 1, 0); }
            }
            bool click = Input.MouseCellClick && it != null && Sel < ItemRows.Length && ItemRows[Sel] == Input.MouseCellY;
            if (Input.Hit(Input.VK_RETURN) || Input.Hit(Input.VK_SPACE) || click)
            {
                if (it != null && it.Activate != null) { Audio.Play(Sfx.MenuSelect, 0.7f, 0, 1, 0); it.Activate(); }
                else if (it != null && it.Adjust != null) { it.Adjust(1); Audio.Play(Sfx.MenuMove, 0.6f, 0, 1, 0); }
                else if (Items.Count == 0) return false;
            }
            if (Input.Hit(Input.VK_ESCAPE) || Input.Hit(Input.VK_BACK)) return false;
            return true;
        }

        public void Draw(Screen s, int top, bool boxed)
        {
            int w = 0;
            foreach (var it in Items)
            {
                int len = it.Text.Length + (it.Value != null ? 18 : 0);
                if (len > w) w = len;
            }
            if (Lines != null) foreach (var l in Lines) w = Math.Max(w, l.Length);
            w = Math.Max(w, Title.Length) + 6;
            if (w > s.Cols - 2) w = s.Cols - 2;
            int h = Items.Count + (Lines != null ? Lines.Length + 1 : 0) + 4;
            int x0 = (s.Cols - w) / 2;
            int y0 = top;
            if (y0 + h > s.Rows) y0 = Math.Max(0, s.Rows - h);
            int bg = Col.Rgb(20, 14, 12), border = Col.Rgb(150, 40, 20);
            if (boxed)
            {
                for (int y = y0; y < y0 + h && y < s.Rows; y++)
                    for (int x = x0; x < x0 + w; x++)
                    {
                        char c = ' ';
                        if (y == y0 || y == y0 + h - 1) c = '═';
                        if (x == x0 || x == x0 + w - 1) c = '║';
                        if (y == y0 && x == x0) c = '╔';
                        if (y == y0 && x == x0 + w - 1) c = '╗';
                        if (y == y0 + h - 1 && x == x0) c = '╚';
                        if (y == y0 + h - 1 && x == x0 + w - 1) c = '╝';
                        s.Put(x, y, c, border, bg);
                    }
                string t = " " + Title + " ";
                s.Print(x0 + (w - t.Length) / 2, y0, t, Col.Rgb(255, 200, 80), bg);
            }
            int row = y0 + 2;
            if (Lines != null)
            {
                foreach (var l in Lines)
                {
                    int c = l.StartsWith("  ") ? Col.Rgb(220, 210, 190) : Col.Rgb(255, 170, 70);
                    s.Print(x0 + 3, row++, l, c, boxed ? bg : Screen.Transparent);
                }
                row++;
            }
            ItemRows = new int[Items.Count];
            ItemX = x0 + 3; ItemW = w - 6;
            int pulse = Col.Lerp(Col.Rgb(255, 60, 30), Col.Rgb(255, 200, 80), 0.5f + 0.5f * (float)Math.Sin(Environment.TickCount / 150.0));
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                ItemRows[i] = row;
                if (it.Spacer) { row++; continue; }
                bool sel = i == Sel;
                int fg = sel ? Col.Rgb(255, 240, 200) : Col.Rgb(170, 150, 130);
                int rowBg = sel ? Col.Rgb(70, 20, 12) : (boxed ? bg : Screen.Transparent);
                if (sel) for (int x = x0 + 1; x < x0 + w - 1; x++) s.Put(x, row, ' ', fg, rowBg);
                if (sel) s.Put(x0 + 1, row, '►', pulse, rowBg);
                if (it.Value != null)
                {
                    s.Print(x0 + 3, row, it.Text, fg, rowBg);
                    string v = "< " + it.Value() + " >";
                    s.Print(x0 + w - 3 - v.Length, row, v, sel ? Col.Rgb(255, 210, 90) : Col.Rgb(200, 160, 90), rowBg);
                }
                else
                {
                    s.Print(x0 + (w - it.Text.Length) / 2, row, it.Text, fg, rowBg);
                }
                row++;
            }
        }
    }

    /// <summary>The classic "Doom fire" cellular automaton, used behind the title and between levels.</summary>
    sealed class FireFx
    {
        const int FW = 160, FH = 90;
        readonly byte[] buf = new byte[FW * FH];
        readonly Random rng = new Random();
        float acc;
        public bool Dying;
        static readonly int[] pal =
        {
            0x070707, 0x1F0707, 0x2F0F07, 0x470F07, 0x571707, 0x671F07, 0x771F07, 0x8F2707, 0x9F2F07, 0xAF3F07, 0xBF4707, 0xC74707,
            0xDF4F07, 0xDF5707, 0xDF5707, 0xD75F07, 0xD7670F, 0xCF6F0F, 0xCF770F, 0xCF7F0F, 0xCF8717, 0xC78717, 0xC78F17, 0xC7971F,
            0xBF9F1F, 0xBF9F1F, 0xBFA727, 0xBFA727, 0xBFAF2F, 0xB7AF2F, 0xB7B72F, 0xB7B737, 0xCFCF6F, 0xDFDF9F, 0xEFEFC7, 0xFFFFFF,
        };

        public FireFx()
        {
            for (int x = 0; x < FW; x++) buf[(FH - 1) * FW + x] = 35;
            for (int i = 0; i < 80; i++) Step();
        }

        void Step()
        {
            for (int x = 0; x < FW; x++)
                for (int y = 1; y < FH; y++)
                {
                    int src = y * FW + x;
                    int p = buf[src];
                    if (p == 0) { buf[src - FW] = 0; continue; }
                    int r = rng.Next(4);
                    int dst = src - r + 1;
                    if (dst < FW) dst = FW;
                    if (dst >= FW * FH) dst = FW * FH - 1;
                    int v = p - (r & 1);
                    buf[dst - FW] = (byte)(v < 0 ? 0 : v);
                }
            if (Dying)
                for (int x = 0; x < FW; x++) { int i = (FH - 1) * FW + x; if (buf[i] > 0 && rng.Next(3) == 0) buf[i]--; }
        }

        public void Update(float dt)
        {
            acc += dt;
            int n = 0;
            while (acc > 1 / 30f && n++ < 3) { acc -= 1 / 30f; Step(); }
            if (acc > 0.2f) acc = 0;
        }

        /// <summary>Stretches the fire over the pixel rows [y0, y1) of the screen, blending by brightness.</summary>
        public void Draw(Screen s, int y0, int y1, float strength)
        {
            int h = y1 - y0;
            if (h <= 0) return;
            for (int y = y0; y < y1; y++)
            {
                int fy = (y - y0) * FH / h;
                for (int x = 0; x < s.W; x++)
                {
                    int fx = x * FW / s.W;
                    int v = buf[fy * FW + fx];
                    if (v == 0) continue;
                    int c = pal[Math.Min(35, v)];
                    int i = y * s.W + x;
                    s.Pix[i] = strength >= 1 ? Col.Add(s.Pix[i], c) : Col.Add(s.Pix[i], Col.Scale(c, strength));
                }
            }
        }
    }
}
