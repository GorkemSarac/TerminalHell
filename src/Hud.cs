// TERMINAL HELL - status bar, face, messages, crosshair, screen effects and automap.
using System;

namespace TerminalHell
{
    static class Hud
    {
        static readonly int Panel = Col.Rgb(38, 34, 31), PanelHi = Col.Rgb(92, 84, 74), PanelLo = Col.Rgb(22, 20, 18);
        static readonly int Red = Col.Rgb(236, 36, 28), RedDim = Col.Rgb(90, 22, 18), Label = Col.Rgb(168, 156, 140);
        static readonly int Yellow = Col.Rgb(255, 214, 80), Gray = Col.Rgb(90, 84, 78), White = Col.Rgb(240, 236, 226);

        public static int Rows(int screenRows)
        {
            if (screenRows >= 28) return 5;
            if (screenRows >= 16) return 3;
            return 2;
        }

        // ------------------------------------------------------------ status bar

        static int faceLook, faceTimer;
        static float faceClock;

        public static void DrawStatusBar(Screen s, World w, int top, float dt)
        {
            int rows = s.Rows - top;
            var p = w.P;
            // panel background with a bevelled top edge
            for (int y = top; y < s.Rows; y++)
                for (int x = 0; x < s.Cols; x++)
                {
                    int shade = Col.Scale(Panel, 1 + (Noise.Hashf(x, y, 5) - 0.5f) * 0.12f);
                    s.Put(x, y, ' ', shade, shade);
                }
            for (int x = 0; x < s.Cols; x++) s.Put(x, top, '\u2580', PanelHi, Panel);

            faceClock += dt;
            if (faceClock > 1.4f) { faceClock = 0; faceLook = w.Rng.Next(3) - 1; }
            if (p.HurtDirTime > 0.5f)
            {
                float rel = Norm(p.HurtDirAngle - p.Angle);
                faceLook = rel > 0.5f ? 1 : rel < -0.5f ? -1 : 0;
            }

            if (rows >= 5) DrawFull(s, w, top);
            else DrawCompact(s, w, top, rows);
        }

        static float Norm(float a)
        {
            while (a > Math.PI) a -= (float)(2 * Math.PI);
            while (a < -Math.PI) a += (float)(2 * Math.PI);
            return a;
        }

        static void BigNumber(Screen s, int x, int y, string text, int col, int bg)
        {
            for (int i = 0; i < text.Length; i++)
                for (int gx = 0; gx < 3; gx++)
                    for (int r = 0; r < 3; r++)
                    {
                        char ch = BigDigits.Cell(text[i], gx, r);
                        s.Put(x + i * 4 + gx, y + r, ch, col, bg);
                    }
        }

        static void Center(Screen s, int x, int w, int y, string text, int fg, int bg)
        {
            s.Print(x + (w - text.Length) / 2, y, text, fg, bg);
        }

        static void DrawFull(Screen s, World w, int top)
        {
            var p = w.P;
            bool table = s.Cols >= 96;
            int[] widths = table ? new[] { 13, 17, 11, 9, 17, 5, 15 } : new[] { 13, 17, 11, 9, 17, 5 };
            int sum = 0;
            foreach (int wd in widths) sum += wd;
            int gap = Math.Max(1, (s.Cols - sum) / (widths.Length + 1));
            int x = Math.Max(0, (s.Cols - sum - gap * (widths.Length - 1)) / 2);
            int y = top + 1;
            int[] xs = new int[widths.Length];
            for (int i = 0; i < widths.Length; i++) { xs[i] = x; x += widths[i] + gap; }

            // separators
            for (int i = 1; i < widths.Length; i++)
            {
                int sx = xs[i] - (gap + 1) / 2;
                for (int yy = top + 1; yy < s.Rows; yy++) s.Put(sx, yy, '\u2502', PanelLo, Panel);
            }

            // ammo for the current weapon
            var d = p.Def;
            if (d.Ammo >= 0) BigNumber(s, xs[0] + 1, y, Pad(p.Ammo[d.Ammo], 3), Red, Panel);
            else Center(s, xs[0], widths[0], y + 1, "--", RedDim, Panel);
            Center(s, xs[0], widths[0], y + 3, "AMMO", Label, Panel);

            // health
            int hc = p.HP <= 25 && ((int)(w.Time * 4) & 1) == 0 ? Col.Rgb(255, 120, 90) : Red;
            BigNumber(s, xs[1], y, Pad(p.HP, 3) + "%", hc, Panel);
            Center(s, xs[1], widths[1], y + 3, "HEALTH", Label, Panel);

            // arms
            for (int i = 0; i < Player.Weapons; i++)
            {
                int ax = xs[2] + 1 + (i % 3) * 3, ay = y + i / 3;
                int fg = p.Has[i] ? Yellow : Gray;
                int bg = i == p.Weapon ? Col.Rgb(90, 60, 30) : Panel;
                s.Put(ax, ay, (char)('1' + i), fg, bg);
            }
            s.Print(xs[2] + 1, y + 2, (d.Name + "        ").Substring(0, 9), White, Panel);
            Center(s, xs[2], widths[2], y + 3, "ARMS", Label, Panel);

            // face
            DrawFace(s, w, xs[3] + 1, y);
            Center(s, xs[3], widths[3], y + 3, w.Def.Id, Label, Panel);

            // armor
            BigNumber(s, xs[4], y, Pad(p.Armor, 3) + "%", Red, Panel);
            int ac = p.ArmorType == 2 ? Col.Rgb(110, 160, 255) : p.ArmorType == 1 ? Col.Rgb(110, 220, 110) : Label;
            Center(s, xs[4], widths[4], y + 3, "ARMOR", ac, Panel);

            // keys
            int[] kc = { 0, Col.Rgb(240, 50, 40), Col.Rgb(60, 120, 255), Col.Rgb(250, 210, 40) };
            for (int k = 1; k <= 3; k++)
            {
                bool has = p.Keys[k];
                s.Put(xs[5] + 1, y + k - 1, has ? '\u2590' : '\u00B7', has ? kc[k] : Gray, Panel);
                s.Put(xs[5] + 2, y + k - 1, has ? '\u2588' : ' ', has ? kc[k] : Gray, Panel);
                s.Put(xs[5] + 3, y + k - 1, has ? '\u258C' : ' ', has ? kc[k] : Gray, Panel);
            }
            Center(s, xs[5], widths[5], y + 3, "KEYS", Label, Panel);

            if (table)
            {
                // one line per ammo type: the last one only once the ray gun turns up
                for (int a = 0; a < Player.AmmoTypes; a++)
                {
                    if (a == 3 && !p.Has[5] && p.Ammo[3] == 0)
                    {
                        int kills = w.TotalKills > 0 ? w.Kills * 100 / w.TotalKills : 100;
                        s.Print(xs[6] + 1, y + a, "KILLS " + Pad(kills, 3) + "%", Label, Panel);
                        break;
                    }
                    bool cur = d.Ammo == a;
                    int fg = cur ? Yellow : Label;
                    string line = Player.AmmoNames[a] + " " + Pad(p.Ammo[a], 3) + "/" + Pad(Player.MaxAmmo[a], 3);
                    s.Print(xs[6] + 1, y + a, line, fg, Panel);
                }
            }
        }

        static void DrawCompact(Screen s, World w, int top, int rows)
        {
            var p = w.P;
            int y = top + (rows > 2 ? 1 : 1);
            int x = 1;
            x = Seg(s, x, y, "HP ", Pad(p.HP, 3) + "%", p.HP <= 25 ? Col.Rgb(255, 120, 90) : Red);
            int ac = p.ArmorType == 2 ? Col.Rgb(110, 160, 255) : p.ArmorType == 1 ? Col.Rgb(110, 220, 110) : Red;
            x = Seg(s, x, y, "AR ", Pad(p.Armor, 3) + "%", ac);
            var d = p.Def;
            x = Seg(s, x, y, d.Name + " ", d.Ammo >= 0 ? p.Ammo[d.Ammo].ToString() : "--", Yellow);
            int[] kc = { 0, Col.Rgb(240, 50, 40), Col.Rgb(60, 120, 255), Col.Rgb(250, 210, 40) };
            for (int k = 1; k <= 3; k++) if (p.Keys[k]) { s.Put(x, y, '\u25A0', kc[k], Panel); x += 2; }
            if (rows >= 3)
            {
                // tiny one-line face
                string f = p.Dead ? "(x_x)" : p.OuchTime > 0 ? "(O_O)" : p.GrinTime > 0 ? "(^_^)" : p.HP < 30 ? "(;_;)" : faceLook < 0 ? "(o_o )" : faceLook > 0 ? "( o_o)" : "(o_o)";
                s.Print(s.Cols - f.Length - 2, y, f, Col.Rgb(220, 180, 150), Panel);
                s.Print(1, y + 1, w.Def.Id + "  KILLS " + w.Kills + "/" + w.TotalKills, Label, Panel);
            }
        }

        static int Seg(Screen s, int x, int y, string label, string value, int col)
        {
            s.Print(x, y, label, Label, Panel);
            s.Print(x + label.Length, y, value, col, Panel);
            return x + label.Length + value.Length + 3;
        }

        static string Pad(int v, int n)
        {
            string t = v.ToString();
            return t.Length >= n ? t : new string(' ', n - t.Length) + t;
        }

        /// <summary>The marine's face: health, look direction and mood in 3 rows of ASCII art.</summary>
        static void DrawFace(Screen s, World w, int x, int y)
        {
            var p = w.P;
            int skin = Col.Rgb(222, 172, 136), helm = Col.Rgb(80, 150, 64), eye = Col.Rgb(250, 250, 250), blood = Col.Rgb(210, 20, 16);
            if (p.HP < 40) skin = Col.Lerp(skin, blood, 0.35f);
            string top = " .---. ";
            string eyes;
            string mouth = " \\ - / ";
            char e = 'o';
            if (p.Dead) { e = 'x'; mouth = " \\ _ / "; }
            else if (p.OuchTime > 0) { e = 'O'; mouth = " \\ O / "; }
            else if (p.GrinTime > 0) { mouth = " \\___/ "; }
            else if (p.Refire > 8) { e = '>'; mouth = " \\ = / "; }
            else if (p.HP < 25) { e = '-'; mouth = " \\ ~ / "; }
            else if (p.HP < 50) { mouth = " \\ ~ / "; }
            int look = p.Dead ? 0 : faceLook;
            char e2 = e == '>' ? '<' : e;
            if (look < 0) eyes = "(" + e + " " + e2 + "  )";
            else if (look > 0) eyes = "(  " + e + " " + e2 + ")";
            else eyes = "( " + e + " " + e2 + " )";

            for (int i = 0; i < 7; i++)
            {
                s.Put(x + i, y, top[i], helm, Panel);
                char c = eyes[i];
                s.Put(x + i, y + 1, c, c == '(' || c == ')' ? skin : eye, Panel);
                s.Put(x + i, y + 2, mouth[i], i == 3 || mouth[i] == '_' ? Col.Rgb(200, 90, 80) : skin, Panel);
            }
            // blood splats as health drops
            if (p.HP < 70) s.Put(x + 5, y, ',', blood, Panel);
            if (p.HP < 45) { s.Put(x + 1, y + 2, '\'', blood, Panel); s.Put(x + 2, y, '"', blood, Panel); }
            if (p.HP < 20) { s.Put(x, y + 1, ';', blood, Panel); s.Put(x + 6, y + 2, ',', blood, Panel); }
        }

        // ------------------------------------------------------------ overlays in the view

        public static void DrawMessages(Screen s, World w, int viewRows, bool big)
        {
            // pickups and hints are drawn in the game's own pixel font, a good deal larger than a text line
            int py = 2, cellY = 0;
            foreach (var m in w.Messages)
            {
                int c = m.Time < 0.6f ? Col.Lerp(0x202020, m.Color, m.Time / 0.6f) : m.Color;
                float alpha = Math.Min(1, m.Time / 0.4f);
                if (big && MiniFont.TextWidth(m.Text, 1) + 6 <= s.W && py + 7 < viewRows * 2)
                {
                    MiniFont.Draw(s.Pix, s.W, s.H, m.Text, 3, py, 1, c, Col.Scale(c, 0.6f), Col.Rgb(20, 8, 6), alpha);
                    py += 8;
                    cellY = py / 2;
                }
                else s.Print(1, cellY++, m.Text, c, Screen.Transparent);   // small text, or too long to draw big
            }
            if (w.Hint != null && !w.P.Dead)
            {
                int hy = Math.Max(2, viewRows - 3);
                string t = " " + w.Hint + " ";
                s.PrintCenter(hy, t, w.HintColor, Col.Rgb(20, 18, 16));
            }
        }

        public static void Crosshair(Screen s, int cx, int cy, bool target)
        {
            int c = target ? Col.Rgb(255, 60, 40) : Col.Rgb(230, 230, 210);
            float a = 0.75f;
            s.BlendPix(cx - 2, cy, c, a); s.BlendPix(cx - 3, cy, c, a * 0.6f);
            s.BlendPix(cx + 2, cy, c, a); s.BlendPix(cx + 3, cy, c, a * 0.6f);
            s.BlendPix(cx, cy - 2, c, a); s.BlendPix(cx, cy + 2, c, a);
            if (target) s.BlendPix(cx, cy, c, 0.9f);
        }

        /// <summary>Full-screen tints: damage, pickups, low health, death, damage direction.</summary>
        public static void ScreenEffects(Screen s, World w, int viewH)
        {
            var p = w.P;
            int W = s.W;
            float dmg = Math.Min(0.65f, p.DamageFlash * 0.55f);
            float pick = p.PickupFlash * 0.22f;
            float dead = p.Dead ? Math.Min(0.55f, p.DeadTime * 0.4f) : 0;
            bool low = !p.Dead && p.HP <= 25;
            float pulse = low ? (0.5f + 0.5f * (float)Math.Sin(w.Time * 5)) * 0.35f : 0;
            float rel = Norm(p.HurtDirAngle - p.Angle);
            float dirA = p.HurtDirTime > 0 ? Math.Min(1, p.HurtDirTime * 1.5f) : 0;
            if (dmg <= 0.001f && pick <= 0.001f && dead <= 0 && !low && dirA <= 0) return;
            int red = Col.Rgb(200, 0, 0), gold = Col.Rgb(255, 210, 90);
            for (int y = 0; y < viewH; y++)
            {
                float vy = (y + 0.5f) / viewH * 2 - 1;
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    int c = s.Pix[i];
                    if (dmg > 0 || dead > 0) c = Col.Lerp(c, red, Math.Max(dmg, dead));
                    if (pick > 0) c = Col.Lerp(c, gold, pick);
                    float vx = (x + 0.5f) / W * 2 - 1;
                    if (low)
                    {
                        float edge = Math.Max(0, (vx * vx + vy * vy) - 0.35f);
                        if (edge > 0) c = Col.Lerp(c, red, Math.Min(0.8f, edge * pulse * 1.6f));
                    }
                    if (dirA > 0)
                    {
                        // red glow on the side the damage came from
                        float e = 0;
                        if (rel > 0.6f && rel < 2.5f) e = vx;          // right
                        else if (rel < -0.6f && rel > -2.5f) e = -vx;  // left
                        else if (Math.Abs(rel) <= 0.6f) e = -vy;       // front: top edge
                        else e = vy;                                    // behind: bottom edge
                        if (e > 0.6f) c = Col.Lerp(c, Col.Rgb(255, 20, 10), (e - 0.6f) * 2.2f * dirA);
                    }
                    s.Pix[i] = c;
                }
            }
        }

        /// <summary>Big pixel-font text centered horizontally.</summary>
        public static void BigText(Screen s, string text, int y, int maxScale, int colTop, int colBot)
        {
            BigText(s, text, y, maxScale, colTop, colBot, 1f);
        }

        public static void BigText(Screen s, string text, int y, int maxScale, int colTop, int colBot, float alpha)
        {
            int scale = maxScale;
            while (scale > 1 && PixFont.TextWidth(text, scale) > s.W - 4) scale--;
            int w = PixFont.TextWidth(text, scale);
            PixFont.Draw(s.Pix, s.W, s.H, text, (s.W - w) / 2, y, scale, colTop, colBot, 0x100808, alpha);
        }

        // ------------------------------------------------------------ automap

        public static void Automap(Screen s, World w, int viewH)
        {
            var m = w.Map;
            var p = w.P;
            int W = s.W;
            for (int i = 0; i < W * viewH; i++) s.Pix[i] = Col.Scale(s.Pix[i], 0.22f);
            int sc = Math.Max(2, Math.Min(5, viewH / 20));
            float ox = W / 2f - p.X * sc, oy = viewH / 2f - p.Y * sc;
            for (int cy = 0; cy < m.H; cy++)
                for (int cx = 0; cx < m.W; cx++)
                {
                    int i = cy * m.W + cx;
                    if (!m.Seen[i]) continue;
                    int px = (int)(ox + cx * sc), py = (int)(oy + cy * sc);
                    if (px + sc < 0 || py + sc < 0 || px >= W || py >= viewH) continue;
                    int c = -1;
                    var k = m.Kind[i];
                    if (k == CellKind.Wall || k == CellKind.Push)
                    {
                        // only draw walls that border open space
                        bool edge = false;
                        for (int d = 0; d < 8 && !edge; d += 2)
                        {
                            int nx = cx + Map.DX8[d], ny = cy + Map.DY8[d];
                            if (m.In(nx, ny) && m.Kind[ny * m.W + nx] != CellKind.Wall && m.Kind[ny * m.W + nx] != CellKind.Push) edge = true;
                        }
                        if (!edge) continue;
                        c = m.WallTex[i] == Tex.EXIT_OFF || m.WallTex[i] == Tex.EXIT_ON ? Col.Rgb(80, 255, 80) : Col.Rgb(190, 60, 40);
                    }
                    else if (k == CellKind.Door)
                    {
                        var d = m.Doors[m.DoorIdx[i]];
                        c = d.Key == 1 ? Col.Rgb(255, 60, 40) : d.Key == 2 ? Col.Rgb(70, 130, 255) : d.Key == 3 ? Col.Rgb(255, 220, 50) : Col.Rgb(220, 200, 120);
                    }
                    else
                    {
                        var f = m.Floor[i];
                        c = f == FloorKind.Lava || f == FloorKind.LavaOut ? Col.Rgb(120, 50, 10) : f == FloorKind.Nukage || f == FloorKind.NukageOut ? Col.Rgb(30, 90, 20) : Col.Rgb(34, 30, 28);
                    }
                    s.FillPix(px, py, sc, sc, c);
                }
            // player arrow
            int ppx = (int)(ox + p.X * sc), ppy = (int)(oy + p.Y * sc);
            float dx = (float)Math.Cos(p.Angle), dy = (float)Math.Sin(p.Angle);
            for (int t = -2; t <= 4; t++) s.SetPix((int)(ppx + dx * t), (int)(ppy + dy * t), Col.Rgb(255, 255, 255));
            s.SetPix((int)(ppx + dx * 2 - dy * 2), (int)(ppy + dy * 2 + dx * 2), Col.Rgb(255, 255, 255));
            s.SetPix((int)(ppx + dx * 2 + dy * 2), (int)(ppy + dy * 2 - dx * 2), Col.Rgb(255, 255, 255));
        }
    }
}
