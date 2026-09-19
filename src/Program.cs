// TERMINAL HELL - entry point.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TerminalHell
{
    static class Program
    {
        public const string Version = "1.0.0";

        [STAThread]
        static int Main(string[] args)
        {
            var argl = new List<string>(args);
            if (argl.Contains("--help") || argl.Contains("-h") || argl.Contains("/?"))
            {
                Console.WriteLine("TERMINAL HELL " + Version + " - a first person shooter for your terminal");
                Console.WriteLine();
                Console.WriteLine("  terminalhell              start the game");
                Console.WriteLine("  terminalhell --ascii      start in ASCII display mode");
                Console.WriteLine("  terminalhell --legacy     start in 16 color mode (fastest, for old consoles)");
                Console.WriteLine("  terminalhell --level N    jump straight to level N (1-3)");
                Console.WriteLine("  terminalhell --nomouse    keyboard only (arrow keys turn)");
                Console.WriteLine("  terminalhell --nosound    disable audio");
                Console.WriteLine();
                Console.WriteLine("Settings are stored in " + Settings.Dir);
                return 0;
            }
            if (argl.Contains("--version")) { Console.WriteLine(Version); return 0; }

            // developer tools that render to PNG files without touching the console
            if (argl.Count > 0 && argl[0].StartsWith("--dev-"))
                return DevTool(argl);

            var settings = Settings.Load();
            if (argl.Contains("--ascii")) settings.Display = DisplayMode.Ascii;
            if (argl.Contains("--legacy")) settings.Display = DisplayMode.Legacy;
            if (argl.Contains("--hd")) settings.Display = DisplayMode.HD;
            Input.NoMouse = argl.Contains("--nomouse") || argl.Contains("--autotest");
            if (argl.Contains("--smooth")) settings.CrispFont = false;
            if (argl.Contains("--crisp")) settings.CrispFont = true;
            if (argl.Contains("--fullscreen")) settings.Resolution = 3;
            int ti = argl.IndexOf("--tol");
            if (ti >= 0 && ti + 1 < argl.Count) VtPresenter.Tolerance = int.Parse(argl[ti + 1]);
            int ri = argl.IndexOf("--res");
            if (ri >= 0 && ri + 1 < argl.Count) { int r; if (int.TryParse(argl[ri + 1], out r)) settings.Resolution = Math.Max(0, Math.Min(3, r)); }
            int startLevel = -1;
            int li = argl.IndexOf("--level");
            if (li >= 0 && li + 1 < argl.Count) { int n; if (int.TryParse(argl[li + 1], out n)) startLevel = Math.Max(0, Math.Min(2, n - 1)); }

            try
            {
                Term.Init(settings);
                LoadingScreen();
                Tex.Build();
                Art.Build();
                MonsterDef.Build();
                Input.Init();
                if (!argl.Contains("--nosound")) Audio.Init();
                var game = new Game(settings);
                game.StartLevel = startLevel;
                int ai = argl.IndexOf("--autotest");
                if (ai >= 0 && ai + 2 < argl.Count)
                {
                    game.AutoTestSeconds = float.Parse(argl[ai + 1], CultureInfo.InvariantCulture);
                    game.AutoTestLog = argl[ai + 2];
                    if (game.StartLevel < 0) game.StartLevel = 0;
                    game.AutoTestIdle = argl.Contains("--idle");
                }
                game.God = argl.Contains("--god");
                game.tolFixed = argl.Contains("--tol");
                int wi = argl.IndexOf("--warp");
                if (wi >= 0 && wi + 3 < argl.Count)
                    game.Warp = new[] { float.Parse(argl[wi + 1], CultureInfo.InvariantCulture), float.Parse(argl[wi + 2], CultureInfo.InvariantCulture), float.Parse(argl[wi + 3], CultureInfo.InvariantCulture) };
                game.Run();
                return 0;
            }
            catch (Exception ex)
            {
                Shutdown();
                string log = Path.Combine(Settings.Dir, "crash.log");
                try
                {
                    Directory.CreateDirectory(Settings.Dir);
                    File.WriteAllText(log, DateTime.Now + "\r\n" + ex);
                }
                catch { }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("TERMINAL HELL crashed: " + ex.Message);
                Console.ResetColor();
                Console.WriteLine("Details were written to " + log);
                return 1;
            }
            finally
            {
                Shutdown();
            }
        }

        static bool shutDown;

        static void Shutdown()
        {
            if (shutDown) return;
            shutDown = true;
            try { Input.Shutdown(); } catch { }
            try { Audio.Shutdown(); } catch { }
            try { Term.Restore(); } catch { }
        }

        static void LoadingScreen()
        {
            if (!Term.VtOk) return;
            int cols, rows;
            Term.GetSize(out cols, out rows);
            string t = "LOADING TERMINAL HELL...";
            Term.Write("\x1b[0m\x1b[2J\x1b[" + (rows / 2) + ";" + Math.Max(1, (cols - t.Length) / 2) + "H\x1b[38;2;255;120;60m" + t);
        }

        static int DevTool(List<string> a)
        {
            var inv = CultureInfo.InvariantCulture;
            Tex.Build();
            Art.Build();
            MonsterDef.Build();
            switch (a[0])
            {
                case "--dev-textures":
                    {
                        var list = new List<Image>();
                        for (int i = 1; i < Tex.WALL_COUNT; i++) list.Add(Tex.Walls[i]);
                        for (int i = 0; i < Tex.FLAT_COUNT; i++) list.Add(Tex.Flats[i]);
                        DebugTools.SaveSheet(list, a[1], 3, 8);
                        return 0;
                    }
                case "--dev-sprites":
                    DebugTools.SaveSheet(Art.All(), a[1], 4, 10);
                    return 0;
                case "--dev-weapons":
                    {
                        // each weapon at rest, firing, and mid-animation, drawn exactly as in game (136x84 view)
                        float[] times = { 9, 0.01f, 0.05f, 0.2f, 0.5f };
                        const int vw = 136, vh = 84;
                        var sheet = new int[vw * 5 * vh * 5];
                        var scr = new Screen();
                        scr.Resize(vw, vh / 2);
                        var p = new Player();
                        for (int w = 0; w < 5; w++)
                            for (int k = 0; k < times.Length; k++)
                            {
                                for (int y = 0; y < vh; y++)
                                    for (int x = 0; x < vw; x++)
                                        scr.Pix[y * vw + x] = y < vh / 2 ? Col.Rgb(70, 66, 64) : Col.Rgb(96, 84, 70);
                                p.Weapon = w; p.FireAnim = times[k];
                                ViewModel.Draw(scr, vh, p, 1 / 60f, k * 0.37f, 1.11f, 1);
                                for (int y = 0; y < vh; y++)
                                    for (int x = 0; x < vw; x++)
                                        sheet[(w * vh + y) * vw * 5 + k * vw + x] = scr.Pix[y * vw + x];
                            }
                        DebugTools.SavePng(sheet, vw * 5, vh * 5, a[1], 2);
                        return 0;
                    }
                case "--dev-frame":
                    {
                        // --dev-frame out.png cols rows level x y angle [simSeconds] [hd|ascii] [weapon 0-4] [pitch]
                        var s = new Settings();
                        var g = new Game(s);
                        g.DebugFrame(int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4]) - 1, float.Parse(a[5], inv), float.Parse(a[6], inv), float.Parse(a[7], inv),
                            a.Count > 8 ? float.Parse(a[8], inv) : 0, a[1], a.Count > 9 && a[9] == "ascii", a.Count > 10 ? int.Parse(a[10]) : -1, a.Count > 11 ? float.Parse(a[11], inv) : 0);
                        return 0;
                    }
                case "--dev-icon":
                    {
                        // a pixel-art demon skull, saved as a multi-size .ico (PNG entries)
                        var c = new Canvas(32, 32);
                        c.NoiseSeed = 5;
                        c.Rect(0, 0, 32, 32, Col.Rgb(26, 6, 4), Col.Rgb(70, 10, 6));
                        c.Glow(16, 30, 14, Col.Rgb(255, 170, 40), Col.Rgb(120, 20, 5));
                        int bone = Col.Rgb(222, 206, 170);
                        c.Poly(new float[] { 6, 11, 1, 1, 10, 7 }, bone, Col.Rgb(150, 130, 100));
                        c.Poly(new float[] { 26, 11, 31, 1, 22, 7 }, bone, Col.Rgb(150, 130, 100));
                        c.Ball(16, 14, 11, 10, bone);
                        c.Ball(16, 22, 7, 5.5f, Col.Scale(bone, 0.9f));
                        c.Ball(11.5f, 14.5f, 3.2f, 3, Col.Rgb(255, 60, 20) | Col.EMISSIVE, false);
                        c.Ball(20.5f, 14.5f, 3.2f, 3, Col.Rgb(255, 60, 20) | Col.EMISSIVE, false);
                        c.Ball(11.5f, 14.5f, 1.2f, 1.2f, Col.Rgb(255, 240, 160) | Col.EMISSIVE, false);
                        c.Ball(20.5f, 14.5f, 1.2f, 1.2f, Col.Rgb(255, 240, 160) | Col.EMISSIVE, false);
                        c.Poly(new float[] { 16, 18, 14, 21, 18, 21 }, Col.Rgb(30, 10, 8));
                        for (int t = 0; t < 5; t++) c.Rect(11.2f + t * 2.1f, 23.5f, 1.3f, 3, Col.Rgb(245, 235, 210));
                        c.Outline(Col.Rgb(10, 4, 4));
                        var img = c.Done();
                        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
                        var pngs = new List<byte[]>();
                        foreach (int sz in sizes)
                            using (var bmp = new System.Drawing.Bitmap(sz, sz))
                            {
                                for (int y = 0; y < sz; y++)
                                    for (int x = 0; x < sz; x++)
                                    {
                                        int p = img.Px[(y * 32 / sz) * 32 + x * 32 / sz];
                                        bmp.SetPixel(x, y, System.Drawing.Color.FromArgb((p & Col.OPAQUE) != 0 ? 255 : 0, System.Drawing.Color.FromArgb(p & 0xFFFFFF)));
                                    }
                                using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); pngs.Add(ms.ToArray()); }
                            }
                        using (var fs = new FileStream(a[1], FileMode.Create))
                        using (var bw = new BinaryWriter(fs))
                        {
                            bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
                            int offset = 6 + 16 * sizes.Length;
                            for (int i = 0; i < sizes.Length; i++)
                            {
                                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                                bw.Write((byte)0); bw.Write((byte)0); bw.Write((short)1); bw.Write((short)32);
                                bw.Write(pngs[i].Length); bw.Write(offset);
                                offset += pngs[i].Length;
                            }
                            foreach (var png in pngs) bw.Write(png);
                        }
                        DebugTools.SavePng(img.Px, 32, 32, a[1] + ".png", 8);
                        return 0;
                    }
                case "--dev-audio-stress":
                    {
                        // plays the game's worst case (chaingun + monsters + music) on the real device and records the output
                        Audio.Recording = new List<short>();
                        Audio.Init();
                        if (!Audio.Enabled) { Console.WriteLine("no audio device"); return 2; }
                        Music.Play(1);
                        var rng = new Random(3);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        double nextShot = 1, nextMonster = 0.5, nextPistol = 9;
                        Sfx[] monster = { Sfx.GhoulSight, Sfx.FiendThrow, Sfx.GhoulShot, Sfx.FireHit, Sfx.GhoulPain, Sfx.Explode, Sfx.BruteSight };
                        while (sw.Elapsed.TotalSeconds < 12)
                        {
                            double t = sw.Elapsed.TotalSeconds;
                            if (t >= nextShot && t < 8) { Audio.Play(Sfx.Chaingun, 0.9f, 0, 1, 0); nextShot += 0.105; }
                            if (t >= nextMonster) { Audio.PlayAt(monster[rng.Next(monster.Length)], (float)rng.NextDouble() * 6, (float)rng.NextDouble() * 6, 1, rng.Next(1, 6)); nextMonster += 0.4; }
                            if (t >= nextPistol) { Audio.Play(Sfx.Pistol, 0.9f, 0, 1, 0); nextPistol += 0.7; }
                            System.Threading.Thread.Sleep(1);
                        }
                        short[] data;
                        lock (Audio.Recording) data = Audio.Recording.ToArray();
                        Audio.Shutdown();
                        using (var bw = new BinaryWriter(File.Create(a[1])))
                        {
                            bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + data.Length * 2); bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                            bw.Write(16); bw.Write((short)1); bw.Write((short)2); bw.Write(Audio.Rate); bw.Write(Audio.Rate * 4); bw.Write((short)4); bw.Write((short)16);
                            bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(data.Length * 2);
                            foreach (var s in data) bw.Write(s);
                        }
                        var bank = SfxBank.Build();
                        var sb = new System.Text.StringBuilder("sound peaks:");
                        for (int k = 0; k < bank.Length; k++)
                        {
                            float pk = 0; bool bad = false;
                            if (bank[k] != null) foreach (var v in bank[k]) { if (float.IsNaN(v) || float.IsInfinity(v)) bad = true; else pk = Math.Max(pk, Math.Abs(v)); }
                            sb.Append(" " + (Sfx)k + "=" + (bank[k] == null ? "MISSING" : bad ? "NAN" : pk.ToString("0.00", inv)));
                        }
                        Console.WriteLine(sb.ToString());
                        Console.WriteLine("buffers " + Audio.BuffersWritten + "  underruns " + Audio.Underruns + "  writeErrors " + Audio.WriteErrors + "  exceptions " + Audio.Exceptions +
                            "  maxMixMs " + Audio.MaxMixMs.ToString("0.00", inv) + "  lastError " + Audio.LastError);
                        // loudness per half second and how much of it is clipped
                        int win = Audio.Rate;   // half a second of stereo samples
                        for (int i = 0; i + win <= data.Length; i += win)
                        {
                            double sum = 0; int clip = 0, peak = 0;
                            for (int k = i; k < i + win; k++) { int v = Math.Abs((int)data[k]); sum += (double)v * v; if (v >= 29900) clip++; if (v > peak) peak = v; }
                            Console.WriteLine(string.Format(inv, "{0,5:0.0}s  rms {1,6:0}  peak {2,6}  clipped {3,5:0.0}%", i / (double)(Audio.Rate * 2), Math.Sqrt(sum / win), peak, clip * 100.0 / win));
                        }
                        return 0;
                    }
                case "--dev-aim":
                    {
                        // vertical aiming: the same shot at different look angles either hits the ghoul or flies over / under it
                        float[] slopes = { 0f, 0.35f, -0.2f, -0.05f };
                        foreach (float slope in slopes)
                        {
                            var w = new World(Levels.All()[0], new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            // hub room: player at (8.5, 13.5) looking north, a ghoul 4 cells ahead
                            w.P.X = 8.5f; w.P.Y = 13.5f; w.P.Angle = (float)(-Math.PI / 2);
                            var g = new Monster(MonsterDef.Ghoul, 8.5f, 9.5f);
                            g.Health = 1000;
                            w.Actors.Add(g);
                            w.P.AimSlope = slope;
                            var d = WeaponDef.All[1];
                            for (int i = 0; i < 20; i++) { w.PlayerFire(w.P, d, 0); }
                            Console.WriteLine(string.Format(inv, "aim slope {0,5:0.00}: ghoul took {1,4:0} damage ({2})", slope, 1000 - g.Health,
                                1000 - g.Health > 0 ? "hit" : "missed"));
                        }
                        Console.WriteLine("ghoul height " + MonsterDef.Ghoul.Height.ToString("0.00", inv) + ", eye height 0.5, distance 4");
                        return 0;
                    }
                case "--dev-check":
                    {
                        // proves every level can be finished: collects reachable keys until the exit is reachable
                        int failures = 0;
                        foreach (var def in Levels.All())
                        {
                            var m = Map.Load(def);
                            var keyAt = new Dictionary<int, int>();
                            int start = -1;
                            var things = new List<Spawn>();
                            foreach (var sp in m.Spawns)
                            {
                                int i = sp.Y * m.W + sp.X;
                                if (sp.C == 'r') keyAt[i] = 1; else if (sp.C == 'b') keyAt[i] = 2; else if (sp.C == 'y') keyAt[i] = 3;
                                else if (sp.C == 'K') keyAt[i] = 3;   // the boss drops the yellow key
                                if ("^>v<".IndexOf(sp.C) >= 0) start = i;
                                else things.Add(sp);
                            }
                            var keys = new bool[4];
                            bool[] reach = null;
                            for (int pass = 0; pass < 5; pass++)
                            {
                                reach = new bool[m.W * m.H];
                                var q = new Queue<int>();
                                reach[start] = true; q.Enqueue(start);
                                while (q.Count > 0)
                                {
                                    int c = q.Dequeue();
                                    for (int k = 0; k < 8; k += 2)
                                    {
                                        int nx = c % m.W + Map.DX8[k], ny = c / m.W + Map.DY8[k];
                                        if (!m.In(nx, ny)) continue;
                                        int j = ny * m.W + nx;
                                        if (reach[j]) continue;
                                        var kind = m.Kind[j];
                                        bool ok = kind == CellKind.Empty || kind == CellKind.Push ||
                                                  (kind == CellKind.Door && (m.Doors[m.DoorIdx[j]].Key == 0 || keys[m.Doors[m.DoorIdx[j]].Key]));
                                        if (!ok) continue;
                                        reach[j] = true; q.Enqueue(j);
                                    }
                                }
                                bool got = false;
                                foreach (var kv in keyAt) if (reach[kv.Key] && !keys[kv.Value]) { keys[kv.Value] = true; got = true; }
                                if (!got) break;
                            }
                            bool exit = false;
                            for (int i = 0; i < m.W * m.H; i++)
                            {
                                if (m.Kind[i] != CellKind.Wall || m.WallTex[i] != Tex.EXIT_OFF) continue;
                                for (int k = 0; k < 8; k += 2)
                                {
                                    int nx = i % m.W + Map.DX8[k], ny = i / m.W + Map.DY8[k];
                                    if (m.In(nx, ny) && reach[ny * m.W + nx]) exit = true;
                                }
                            }
                            var stranded = new List<string>();
                            foreach (var t in things) if (!reach[t.Y * m.W + t.X]) stranded.Add(t.C + "@" + t.X + "," + t.Y);
                            Console.WriteLine(def.Id + ": exit " + (exit ? "REACHABLE" : "NOT REACHABLE") + "  keys " + (keys[1] ? "R" : "-") + (keys[2] ? "B" : "-") + (keys[3] ? "Y" : "-") +
                                (stranded.Count > 0 ? "  unreachable things: " + string.Join(" ", stranded.ToArray()) : "  all things reachable"));
                            if (!exit) failures++;
                        }
                        return failures;
                    }
                case "--dev-soak":
                    {
                        // random bot: runs each level for a few simulated minutes and renders frames to catch crashes
                        var rng = new Random(7);
                        var ren = new Renderer();
                        var scr = new Screen();
                        scr.Resize(120, 40);
                        var cam = new Camera();
                        foreach (var def in Levels.All())
                        {
                            var s = new Settings();
                            var w = new World(def, s, null);
                            w.DamageMul = 0;
                            w.P.Has[2] = w.P.Has[3] = w.P.Has[4] = true;
                            w.P.Ammo[0] = 200; w.P.Ammo[1] = 50; w.P.Ammo[2] = 50;
                            var open = new List<int>();
                            for (int i = 0; i < w.Map.W * w.Map.H; i++) if (w.Map.Kind[i] == CellKind.Empty) open.Add(i);
                            var inp = new PlayerInput();
                            int frames = 0, exits = 0, onLava = 0;
                            for (int f = 0; f < 9000; f++)
                            {
                                if (f % 20 == 0)
                                {
                                    inp = new PlayerInput();
                                    inp.Forward = rng.Next(3) - 1; inp.Strafe = rng.Next(3) - 1;
                                    inp.Fire = rng.Next(3) == 0; inp.Run = rng.Next(2) == 0;
                                    inp.SelectSlot = rng.Next(8) == 0 ? rng.Next(1, 6) : 0;
                                }
                                inp.Turn = (float)(rng.NextDouble() - 0.5) * 0.1f;
                                inp.Use = rng.Next(10) == 0;
                                if (f % 300 == 0)
                                {
                                    int c = open[rng.Next(open.Count)];
                                    w.P.X = c % w.Map.W + 0.5f; w.P.Y = c / w.Map.W + 0.5f;
                                }
                                if (w.P.Dead) { w.P.Dead = false; w.P.HP = 100; }
                                w.Update(1 / 30f, inp);
                                foreach (var act in w.Actors)
                                {
                                    var mon = act as Monster;
                                    if (mon != null && mon.Alive && w.Map.HurtFloorAt((int)mon.X, (int)mon.Y)) onLava++;
                                }
                                if (w.LevelDone) { exits++; w.LevelDone = false; w.ExitTriggered = false; }
                                if (f % 10 == 0)
                                {
                                    cam.X = w.P.X; cam.Y = w.P.Y; cam.Angle = w.P.Angle; cam.Pitch = (float)(rng.NextDouble() - 0.5) * 0.6f;
                                    var list = new List<SpriteInst>();
                                    foreach (var act in w.Actors)
                                    {
                                        var img = act.Sprite(w);
                                        if (img == null) continue;
                                        list.Add(new SpriteInst { X = act.X, Y = act.Y, Z = act.Z, Img = img, Scale = act.Scale, FullBright = act.Bright });
                                    }
                                    ren.Render(scr.Pix, scr.W, 70, w.Map, cam, list, w.Lights, 0);
                                    frames++;
                                }
                            }
                            // targeted: push every secret wall from its open side and render while it slides
                            var w2 = new World(def, s, null);
                            w2.DamageMul = 0;
                            foreach (var pw in w2.Map.PushWalls)
                            {
                                for (int k = 0; k < 8; k += 2)
                                {
                                    int ox = pw.X - Map.DX8[k], oy = pw.Y - Map.DY8[k];
                                    if (!w2.Map.In(ox, oy) || w2.Map.Kind[oy * w2.Map.W + ox] != CellKind.Empty) continue;
                                    w2.P.X = ox + 0.5f; w2.P.Y = oy + 0.5f;
                                    w2.P.Angle = (float)Math.Atan2(Map.DY8[k], Map.DX8[k]);
                                    var use = new PlayerInput(); use.Use = true;
                                    w2.Update(1 / 30f, use);
                                    for (int f = 0; f < 120; f++)
                                    {
                                        w2.Update(1 / 30f, new PlayerInput());
                                        cam.X = w2.P.X; cam.Y = w2.P.Y; cam.Angle = w2.P.Angle + (float)Math.Sin(f * 0.1) * 0.5f;
                                        ren.Render(scr.Pix, scr.W, 70, w2.Map, cam, null, null, 0);
                                    }
                                    Console.WriteLine("  push wall at " + ox + "," + oy + " -> now at " + pw.X + "," + pw.Y + " moved " + pw.Moved + " done " + pw.Done);
                                    break;
                                }
                            }
                            Console.WriteLine("  secrets found by pushing: " + w2.Secrets + "/" + w2.TotalSecrets);
                            // targeted: open every unlocked door
                            int opened = 0;
                            foreach (var d in w2.Map.Doors)
                            {
                                if (d.Key != 0) continue;
                                w2.OpenDoor(d, true);
                                for (int f = 0; f < 30; f++) w2.Update(1 / 30f, new PlayerInput());
                                if (d.Open >= 1) opened++;
                            }
                            Console.WriteLine("  unlocked doors that opened: " + opened);

                            int alive = 0;
                            foreach (var act in w.Actors) { var m = act as Monster; if (m != null && m.Alive) alive++; }
                            Console.WriteLine(def.Id + ": ok  kills " + w.Kills + "/" + w.TotalKills + "  alive " + alive + "  secrets " + w.Secrets + "/" + w.TotalSecrets +
                                "  items " + w.ItemsTaken + "/" + w.TotalItems + "  exits " + exits + "  actors " + w.Actors.Count + "  frames " + frames + "  monster-frames on lava/slime " + onLava);
                        }
                        return 0;
                    }
                case "--dev-levels":
                    {
                        // loads every level and reports counts (validates the map data)
                        foreach (var def in Levels.All())
                        {
                            var w = new World(def, new Settings(), null);
                            Console.WriteLine(def.Id + " " + def.Map[0].Length + "x" + def.Map.Length + "  monsters " + w.TotalKills + "  items " + w.TotalItems + "  secrets " + w.TotalSecrets +
                                "  start " + w.P.X + "," + w.P.Y + "  lights " + w.Map.Lights.Count);
                        }
                        return 0;
                    }
            }
            Console.WriteLine("unknown dev tool");
            return 1;
        }
    }
}
