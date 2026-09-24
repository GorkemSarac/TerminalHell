// TERMINAL HELL - entry point.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TerminalHell
{
    static class Program
    {
        // major.minor.patch - patch for fixes, minor for new features (keep linux/TerminalHell.Linux.csproj in step)
        public const string Version = "1.13.1";

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
#if !LINUX
                Console.WriteLine("  terminalhell --legacy     start in 16 color mode (fastest, for old consoles)");
#endif
                Console.WriteLine("  terminalhell --level N    jump straight to level N (1-3)");
                Console.WriteLine("  terminalhell --nomouse    keyboard only (arrow keys turn)");
                Console.WriteLine("  terminalhell --nosound    disable audio");
#if LINUX
                Console.WriteLine("  terminalhell --no-evdev   never read keyboards and mice from /dev/input");
                Console.WriteLine("  terminalhell --no-kitty   don't use the kitty keyboard protocol (key releases)");
                Console.WriteLine("  terminalhell --input      show which keyboard and mouse input this terminal gives the game");
#endif
                Console.WriteLine();
                Console.WriteLine("Settings are stored in " + Settings.Dir);
                return 0;
            }
            if (argl.Contains("--version")) { Console.WriteLine(Version); return 0; }
#if LINUX
            if (argl.Contains("--input")) return InputCheck.Run(argl);
#endif

            // developer tools that render to PNG files without touching the console
            if (argl.Count > 0 && argl[0].StartsWith("--dev-"))
                return DevTool(argl);

            var settings = Settings.Load();
            Updater.CheckInBackground(settings);
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
            if (ri >= 0 && ri + 1 < argl.Count) { int r; if (int.TryParse(argl[ri + 1], out r)) settings.Resolution = Math.Max(1, Math.Min(3, r)); }
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
                    // a scripted run is not a player's session: keep it away from their save slot
                    SaveGame.PathOverride = Path.Combine(Path.GetTempPath(), "terminalhell-autotest-save.txt");
                    if (game.StartLevel < 0) game.StartLevel = 0;
                    game.AutoTestIdle = argl.Contains("--idle");
                }
                game.God = argl.Contains("--god");
                game.StartWithAutomap = argl.Contains("--automap");
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
            // the tools start levels, and starting a level autosaves: never over the player's own slot
            SaveGame.PathOverride = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "terminalhell-devtool-save.txt");
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
                    if (a.Count > 2 && a[2] == "airport") DebugTools.SaveSheet(Art.AirportSprites(), a[1], 4, 5);
                    else DebugTools.SaveSheet(Art.All(), a[1], 4, 10);
                    return 0;
                case "--dev-arsenal":
                    return DevArsenal();
                case "--dev-anim":
                    {
                        // --dev-anim out.png weapon [frames] [width]: one weapon through its whole firing animation, frame by
                        // frame, as the game draws it (the laser beam on, the saw revving, the charge weapons winding up first)
                        int wi = int.Parse(a[2]);
                        int nf = a.Count > 3 ? int.Parse(a[3]) : 10;
                        int vw = a.Count > 4 ? int.Parse(a[4]) : 200, vh = vw * 3 / 5;
                        var def = WeaponDef.All[wi];
                        float charge = def.ChargeTime;
                        float span = charge + def.Anim + 0.1f;
                        int gc = Math.Min(nf, 4), gr = (nf + gc - 1) / gc;
                        var sheet = new int[vw * gc * vh * gr];
                        var scr = new Screen();
                        scr.Resize(vw, vh / 2);
                        var p = new Player();
                        p.Weapon = wi;
                        for (int k = 0; k < nf; k++)
                        {
                            float t = span * k / Math.Max(1, nf - 1);
                            for (int y = 0; y < vh; y++)
                                for (int x = 0; x < vw; x++)
                                    scr.Pix[y * vw + x] = y < vh / 2 ? Col.Rgb(70, 66, 64) : Col.Rgb(96, 84, 70);
                            p.Charge = t < charge ? t : 0;
                            p.FireAnim = t < charge ? 9 : t - charge;
                            p.BeamOn = def.Beam && k > 0;
                            p.BeamDist = 6; p.BeamOnBody = (k & 1) == 0;
                            p.SawRev = def.Saw ? Math.Min(1, t * 4) : 0;
                            p.SawBite = def.Saw && (k & 2) != 0 ? 0.1f : 0;
                            p.LastShots = 2;
                            p.PunchAnim = 9;
                            ViewModel.Draw(scr, vh, p, 1 / 30f, t, 1.11f, 1);
                            for (int y = 0; y < vh; y++)
                                for (int x = 0; x < vw; x++)
                                    sheet[((k / gc) * vh + y) * vw * gc + (k % gc) * vw + x] = (x == 0 || y == 0) ? 0 : scr.Pix[y * vw + x];
                        }
                        DebugTools.SavePng(sheet, vw * gc, vh * gr, a[1], 1);
                        return 0;
                    }
                case "--dev-weapons":
                    {
                        // each weapon at rest, firing, and at four points of the parry swing, drawn exactly as in game (136x84 view)
                        float[] times = { 9, 0.01f, 0.05f, 0.2f, 9, 9, 9, 9 };
                        float[] punch = { 9, 9, 9, 9, 0.06f, 0.13f, 0.20f, 0.29f };
                        const int vw = 136, vh = 84;
                        var sheet = new int[vw * times.Length * vh * Player.Weapons];
                        var scr = new Screen();
                        scr.Resize(vw, vh / 2);
                        var p = new Player();
                        for (int w = 0; w < Player.Weapons; w++)
                            for (int k = 0; k < times.Length; k++)
                            {
                                for (int y = 0; y < vh; y++)
                                    for (int x = 0; x < vw; x++)
                                        scr.Pix[y * vw + x] = y < vh / 2 ? Col.Rgb(70, 66, 64) : Col.Rgb(96, 84, 70);
                                p.Weapon = w; p.FireAnim = times[k];
                                p.PunchAnim = punch[k];
                                p.ParryTime = punch[k] < 1 ? 0.2f : 0;
                                ViewModel.Draw(scr, vh, p, 1 / 60f, k * 0.37f, 1.11f, 1);
                                for (int y = 0; y < vh; y++)
                                    for (int x = 0; x < vw; x++)
                                        sheet[(w * vh + y) * vw * times.Length + k * vw + x] = scr.Pix[y * vw + x];
                            }
                        DebugTools.SavePng(sheet, vw * times.Length, vh * Player.Weapons, a[1], 2);
                        return 0;
                    }
                case "--dev-frame":
                    {
                        // --dev-frame out.png cols rows level x y angle [simSeconds] [hd|ascii] [weapon 0-10] [pitch] [fireSeconds]
                        var s = new Settings();
                        var g = new Game(s);
                        if (a.Count > 12) g.DebugFireFor = float.Parse(a[12], inv);
                        g.DebugFrame(int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4]) - 1, float.Parse(a[5], inv), float.Parse(a[6], inv), float.Parse(a[7], inv),
                            a.Count > 8 ? float.Parse(a[8], inv) : 0, a[1], a.Count > 9 && a[9] == "ascii", a.Count > 10 ? int.Parse(a[10]) : -1, a.Count > 11 ? float.Parse(a[11], inv) : 0);
                        return 0;
                    }
#if LINUX
                case "--dev-linux-selftest":
                    return LinuxSelfTest.Run();
#else
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
#endif
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
                case "--dev-music":
                    {
                        // --dev-music [seconds] : the level of every track, rendered offline
                        float secs = a.Count > 1 ? float.Parse(a[1], inv) : 24f;
                        for (int t = 0; t < 8; t++) Console.WriteLine(Music.Measure(t, secs, null));
                        return 0;
                    }
                case "--dev-aim":
                    {
                        // vertical aiming: the same shot at different look angles either hits the ghoul or flies over / under it
                        float[] slopes = { 0f, 0.35f, -0.2f, -0.05f };
                        foreach (float slope in slopes)
                        {
                            var w = new World(Levels.ById("E2M1"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            // hub room: player at (8.5, 13.5) looking north, a ghoul 4 cells ahead
                            w.P.X = 8.5f; w.P.Y = 13.5f; w.P.Angle = (float)(-Math.PI / 2);
                            var g = new Monster(MonsterDef.Ghoul, 8.5f, 9.5f);
                            g.Health = 1000;
                            w.Actors.Add(g);
                            w.P.AimSlope = slope;
                            var d = WeaponDef.All[2];
                            for (int i = 0; i < 20; i++) { w.PlayerFire(w.P, d, 0, 1); }
                            Console.WriteLine(string.Format(inv, "aim slope {0,5:0.00}: ghoul took {1,4:0} damage ({2})", slope, 1000 - g.Health,
                                1000 - g.Health > 0 ? "hit" : "missed"));
                        }
                        Console.WriteLine("ghoul height " + MonsterDef.Ghoul.Height.ToString("0.00", inv) + ", eye height 0.5, distance 4");
                        return 0;
                    }
                case "--dev-end":
                    {
                        // --dev-end out.png cols rows [intermission|victory|title|options|burn] [seconds into the screen]
                        var g = new Game(new Settings());
                        g.DebugEndScreen(int.Parse(a[2]), int.Parse(a[3]), a.Count > 4 ? a[4] : "intermission", a.Count > 5 ? float.Parse(a[5], inv) : 9f, a[1]);
                        return 0;
                    }
                case "--dev-menu":
                    {
                        // clicking a value in the options: the "<" side must turn it down, the ">" side up
                        int value = 5;
                        var s = new Settings();
                        var scr = new Screen();
                        scr.Resize(80, 24);
                        var m = new Menu("TEST");
                        m.AddValue("VOLUME", () => value.ToString(), d => value += d);
                        m.Add("BACK", () => { });
                        int failures = 0;
                        m.Draw(scr, 2, true);   // the draw is what works out where the "<" and ">" are
                        Input.MouseCellY = m.ItemRows[0];
                        int mid = (m.ValueLeft[0] + m.ValueRight[0]) / 2;
                        int[] xs = { m.ValueLeft[0], m.ValueRight[0], mid, mid + 1, m.ItemX };
                        string[] names = { "the < arrow", "the > arrow", "the left half of the value", "the right half of the value", "the label" };
                        int[] want = { -1, 1, -1, 1, 1 };
                        for (int i = 0; i < xs.Length; i++)
                        {
                            int before = value;
                            Input.MouseCellClick = true;
                            Input.MouseCellX = xs[i];
                            m.Update();
                            Input.MouseCellClick = false;
                            int got = value - before;
                            Console.WriteLine("clicking " + names[i] + " at column " + xs[i] + ": " + (got > 0 ? "+" : "") + got + (got == want[i] ? "" : "   WRONG, expected " + want[i]));
                            if (got != want[i]) failures++;
                        }
                        Console.WriteLine("default volumes: sound " + s.SfxVolume + ", music " + s.MusicVolume);

                        // the update check only ever moves forward: a local build ahead of the published one is left alone
                        string[][] versions =
                        {
                            new[] { "1.3.0", "1.2.0", "yes" }, new[] { "1.2.0", "1.3.0", "no" }, new[] { "1.2.0", "1.2.0", "no" },
                            new[] { "1.10.0", "1.9.9", "yes" }, new[] { "2.0.0", "1.9.9", "yes" }, new[] { "1.2.1", "1.2.0", "yes" },
                            new[] { "", "1.2.0", "no" }, new[] { "not a version", "1.2.0", "no" }, new[] { "v1.4.0", "1.3.9", "yes" },
                        };
                        foreach (var v in versions)
                        {
                            string got = Updater.IsNewer(v[0], v[1]) ? "yes" : "no";
                            if (got == v[2]) continue;
                            Console.WriteLine("is \"" + v[0] + "\" newer than " + v[1] + "? said " + got + ", expected " + v[2]);
                            failures++;
                        }
                        Console.WriteLine("update version comparison: " + versions.Length + " cases checked");
                        return failures;
                    }
                case "--dev-moves":
                    {
                        // the parry: a fireball thrown at the player either hits, or is knocked back at whoever threw it
                        int bad = 0;
                        for (int parry = 0; parry < 2; parry++)
                        {
                            var w = new World(Levels.ById("E2M1"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            w.P.X = 8.5f; w.P.Y = 13.5f; w.P.Angle = (float)(-Math.PI / 2);   // looking north
                            // the thrower stands well back and keeps to itself: a brute never shoots, so only this one fireball is in play
                            var thrower = new Monster(MonsterDef.Brute, 8.5f, 6.5f);
                            w.Actors.Add(thrower);
                            var shot = new Projectile(thrower, 8.5f, 11.0f, (float)(Math.PI / 2), 6, Projectile.FIREBALL);
                            shot.DmgMin = 10; shot.DmgMax = 10;
                            shot.Z = 0.45f;
                            w.Add(shot);
                            int hpBefore = w.P.HP;
                            float throwerBefore = thrower.Health;
                            var inp = new PlayerInput();
                            for (int f = 0; f < 45; f++)
                            {
                                // punch when it is about a metre away
                                inp.Parry = parry == 1 && Math.Abs(shot.Y - w.P.Y) < 1.2f && !shot.Remove;
                                w.Update(1 / 30f, inp);
                            }
                            int hpLost = hpBefore - w.P.HP;
                            float hurtBack = throwerBefore - thrower.Health;
                            if (parry == 0)
                            {
                                Console.WriteLine("no parry: the player took " + hpLost + " damage, the thrower took " + hurtBack.ToString("0", inv));
                                if (hpLost <= 0) { Console.WriteLine("  WRONG: the fireball should have hit the player"); bad++; }
                            }
                            else
                            {
                                Console.WriteLine("parried : the player took " + hpLost + " damage, the thrower took " + hurtBack.ToString("0", inv));
                                if (hpLost > 0) { Console.WriteLine("  WRONG: a parried fireball should not hit the player"); bad++; }
                                if (hurtBack <= 0) { Console.WriteLine("  WRONG: a parried fireball should come back at whoever threw it"); bad++; }
                            }
                        }

                        // jumping: space lifts the player off the floor and gravity brings them back
                        {
                            var w = new World(Levels.ById("E2M1"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            var inp = new PlayerInput();
                            inp.Jump = true;
                            w.Update(1 / 30f, inp);
                            inp.Jump = false;
                            float peak = 0;
                            bool landed = false;
                            for (int f = 0; f < 40; f++)
                            {
                                w.Update(1 / 30f, inp);
                                peak = Math.Max(peak, w.P.Z);
                                if (f > 5 && w.P.OnGround) { landed = true; break; }
                            }
                            Console.WriteLine("jump    : rose " + peak.ToString("0.00", inv) + " and " + (landed ? "landed again" : "never came down"));
                            if (peak < 0.15f) { Console.WriteLine("  WRONG: a jump should lift the player off the floor"); bad++; }
                            if (peak > Player.MaxJumpZ + 0.01f) { Console.WriteLine("  WRONG: a jump should never reach the ceiling"); bad++; }
                            if (!landed) { Console.WriteLine("  WRONG: the player should come back down"); bad++; }
                        }

                        // lava burns underfoot, but not while you are in the air above it
                        for (int air = 0; air < 2; air++)
                        {
                            var w = new World(Levels.ById("E2M3"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            w.P.X = 8.5f; w.P.Y = 22.5f;            // a lava tile in the west field of E1M3
                            if (air == 1) { w.P.Z = 0.35f; w.P.OnGround = false; }
                            int hpBefore = w.P.HP;
                            var still = new PlayerInput();
                            // three frames: long enough for a burn tick, short enough that gravity has not landed them yet
                            for (int f = 0; f < 3; f++) w.Update(1 / 30f, still);
                            int hpLost = hpBefore - w.P.HP;
                            Console.WriteLine((air == 1 ? "in air  " : "standing") + ": lava did " + hpLost + " damage");
                            if (air == 0 && hpLost <= 0) { Console.WriteLine("  WRONG: standing in lava should burn"); bad++; }
                            if (air == 1 && hpLost > 0) { Console.WriteLine("  WRONG: lava should not reach a player in the air"); bad++; }
                        }
                        // soldiers are taller than the player: their shots have to come down to the middle of them
                        {
                            var w = new World(Levels.ById("E2M1"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            w.P.X = 8.5f; w.P.Y = 13.5f; w.P.Angle = (float)(-Math.PI / 2);
                            var ghoul = new Monster(MonsterDef.Ghoul, 8.5f, 7.5f);
                            ghoul.Alert(w);
                            w.Actors.Add(ghoul);
                            int hpBefore = w.P.HP;
                            var still = new PlayerInput();
                            for (int f = 0; f < 300 && w.P.HP == hpBefore; f++) w.Update(1 / 30f, still);
                            Console.WriteLine("shot at : the player took " + (hpBefore - w.P.HP) + " damage from a soldier six metres away");
                            if (w.P.HP == hpBefore) { Console.WriteLine("  WRONG: the shots are missing the player entirely"); bad++; }
                        }

                        // a soldier's bullet passes through its friends; a rocket's blast does not
                        for (int rocket = 0; rocket < 2; rocket++)
                        {
                            var w = new World(Levels.ById("E2M1"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            w.P.X = 8.5f; w.P.Y = 13.5f;
                            var shooter = new Monster(MonsterDef.Ghoul, 8.5f, 8.5f);
                            var friend = new Monster(MonsterDef.Ghoul, 8.5f, 10.5f);
                            friend.Health = 500;   // so one shot can't finish it and hide the result
                            w.Actors.Add(shooter);
                            w.Actors.Add(friend);
                            var shot = new Projectile(shooter, 8.5f, 9.0f, (float)(Math.PI / 2), 10, rocket == 1 ? Projectile.ROCKET : Projectile.BULLET);
                            shot.DmgMin = 10; shot.DmgMax = 10;
                            if (rocket == 1) { shot.SplashDamage = 60; shot.SplashRadius = 2.2f; }
                            shot.Z = 0.45f;
                            w.Add(shot);
                            float before = friend.Health;
                            var still = new PlayerInput();
                            for (int f = 0; f < 20; f++) w.Update(1 / 30f, still);
                            float lost = before - friend.Health;
                            Console.WriteLine((rocket == 1 ? "rocket  " : "bullet  ") + ": the monster in the way took " + lost.ToString("0", inv) + " damage");
                            if (rocket == 0 && lost > 0) { Console.WriteLine("  WRONG: a soldier's bullet should pass through its friends"); bad++; }
                            if (rocket == 1 && lost <= 0) { Console.WriteLine("  WRONG: a rocket's blast should still catch them"); bad++; }
                        }

                        // the ray gun: holding the trigger winds it up, then it spends one cell and blows a hole in things
                        {
                            var w = new World(Levels.ById("E2M1"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            w.P.X = 8.5f; w.P.Y = 13.5f; w.P.Angle = (float)(-Math.PI / 2);
                            w.P.Has[10] = true; w.P.Weapon = 10; w.P.Ammo[3] = 2;
                            var target = new Monster(MonsterDef.Brute, 8.5f, 10.5f);
                            target.Health = 400;
                            w.Actors.Add(target);
                            var hold = new PlayerInput();
                            hold.Fire = true;
                            float charged = 0;
                            for (int f = 0; f < 60; f++)
                            {
                                w.Update(1 / 30f, hold);
                                charged = Math.Max(charged, w.P.Charge);
                                if (w.P.Ammo[3] < 2) break;
                            }
                            for (int f = 0; f < 10; f++) w.Update(1 / 30f, new PlayerInput());
                            Console.WriteLine("ray gun : wound up to " + charged.ToString("0.00", inv) + "s, cells left " + w.P.Ammo[3] +
                                ", the target took " + (400 - target.Health).ToString("0", inv) + " damage");
                            if (w.P.Ammo[3] != 1) { Console.WriteLine("  WRONG: one shot should spend exactly one cell"); bad++; }
                            if (charged < Player.RayChargeTime * 0.8f) { Console.WriteLine("  WRONG: it should wind up before firing"); bad++; }
                            if (400 - target.Health < 100) { Console.WriteLine("  WRONG: the bolt and its burst should hurt"); bad++; }
                        }

                        // the Warden: its laser follows the player, holds still for a second, and three rockets
                        // then land on the spot the laser was resting on rather than on the player
                        {
                            var w = new World(Levels.ById("E2M3"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            w.P.X = 21.5f; w.P.Y = 11.5f; w.P.Angle = (float)(-Math.PI / 2);
                            var boss = new Monster(MonsterDef.Warden, 21.5f, 6.5f);
                            boss.Alert(w);
                            w.Actors.Add(boss);
                            var strafe = new PlayerInput();
                            strafe.Strafe = 1;      // keep moving: a laser that follows and one that holds then differ
                            var seen = new List<int>();
                            int rockets = 0, tracking = 0, holding = 0, drifted = 0, stuck = 0, offTarget = 0;
                            float lockX = 0, lockY = 0;
                            bool wasLocked = false;
                            for (int f = 0; f < 300 && rockets < 3; f++)
                            {
                                w.Update(1 / 30f, strafe);
                                if (boss.Aiming && !boss.AimLocked)
                                {
                                    tracking++;
                                    wasLocked = false;
                                    if (Math.Abs(boss.AimX - w.P.X) > 0.02f || Math.Abs(boss.AimY - w.P.Y) > 0.02f) stuck++;
                                }
                                else if (boss.Aiming)
                                {
                                    if (!wasLocked) { lockX = boss.AimX; lockY = boss.AimY; wasLocked = true; }
                                    holding++;
                                    if (Math.Abs(boss.AimX - lockX) > 0.001f || Math.Abs(boss.AimY - lockY) > 0.001f) drifted++;
                                }
                                foreach (var act in w.Actors)
                                {
                                    var pr = act as Projectile;
                                    if (pr == null || pr.Owner != (Actor)boss || seen.Contains(pr.Tag)) continue;
                                    seen.Add(pr.Tag);
                                    rockets++;
                                    float wx = lockX - boss.X, wy = lockY - boss.Y;
                                    float wl = (float)Math.Sqrt(wx * wx + wy * wy), vl = (float)Math.Sqrt(pr.VX * pr.VX + pr.VY * pr.VY);
                                    if (wl < 0.01f || vl < 0.01f || (wx * pr.VX + wy * pr.VY) / (wl * vl) < 0.999f) offTarget++;
                                }
                            }
                            float heldSeconds = holding / 3f / 30f;
                            Console.WriteLine("warden  : " + rockets + " rockets, the laser followed for " + (tracking / 30f).ToString("0.0", inv) +
                                "s and held for " + heldSeconds.ToString("0.0", inv) + "s before each one");
                            if (rockets < 3) { Console.WriteLine("  WRONG: the Warden should fire three rockets in one attack"); bad++; }
                            if (stuck > 1) { Console.WriteLine("  WRONG: the laser should follow the player until it locks"); bad++; }
                            if (drifted > 0) { Console.WriteLine("  WRONG: a locked laser should not move again"); bad++; }
                            if (heldSeconds < Monster.AimLock * 0.8f) { Console.WriteLine("  WRONG: it should hold its aim for about a second"); bad++; }
                            if (offTarget > 0) { Console.WriteLine("  WRONG: the rockets should fly at the spot the laser held"); bad++; }
                        }

                        // the Warden cannot be staggered out of an attack: repeated hits mid-windup never flip it to Pain
                        {
                            var w = new World(Levels.ById("E2M3"), new Settings(), null);
                            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                            w.P.X = 21.5f; w.P.Y = 11.5f;
                            var boss = new Monster(MonsterDef.Warden, 21.5f, 6.5f);
                            boss.Alert(w);
                            w.Actors.Add(boss);
                            var idle = new PlayerInput();
                            int staggered = 0;
                            for (int f = 0; f < 200; f++)
                            {
                                w.Update(1 / 30f, idle);
                                if (boss.State == MState.WindUp || boss.State == MState.Fire) { boss.Damage(w, 5, w.P, false); if (boss.State == MState.Pain) staggered++; }
                            }
                            Console.WriteLine("uninterr: " + staggered + " times staggered out of an attack");
                            if (staggered > 0) { Console.WriteLine("  WRONG: hitting the boss mid-attack should not interrupt it"); bad++; }
                        }

                        Console.WriteLine(bad == 0 ? "parry, jump, shooting, the ray gun and the Warden behave" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-runway":
                    {
                        // the runway is a straight line: the shotgun checkpoint and the exit room are both locked,
                        // the rubble by the start genuinely blocks the room's own approach, and the traveller is scared
                        int bad = 0;
                        var w = new World(Levels.ById("E1M2"), new Settings(), null);
                        Door yellow = null, red = null;
                        foreach (var d in w.Map.Doors) { if (d.Key == 3) yellow = d; if (d.Key == 1) red = d; }
                        if (yellow == null || red == null) { Console.WriteLine("missing the checkpoint or exit door"); return 1; }
                        w.OpenDoor(yellow, true);
                        Console.WriteLine("checkpoint: " + (yellow.State == DoorState.Closed ? "stays shut without the yellow key" : "OPENED WITHOUT A KEY"));
                        if (yellow.State != DoorState.Closed) bad++;
                        w.OpenDoor(red, true);
                        Console.WriteLine("exit room : " + (red.State == DoorState.Closed ? "stays shut without the red key" : "OPENED WITHOUT A KEY"));
                        if (red.State != DoorState.Closed) bad++;

                        Decor npc = null;
                        foreach (var act in w.Actors) { var dc = act as Decor; if (dc != null && dc.Lines != null) npc = dc; }
                        bool scared = npc != null && Array.IndexOf(npc.Lines, "DON'T GO BACK IN THERE.") >= 0;
                        Console.WriteLine("traveller : " + (npc == null ? "none found" : scared ? "scared" : "calm, not scared"));
                        if (!scared) { Console.WriteLine("  WRONG: after the incident, they should be scared, not making small talk"); bad++; }

                        // the rubble at the start room's own threshold blocks it from that side
                        w.P.X = 9.5f; w.P.Y = 9.5f; w.P.Angle = (float)(Math.PI / 2);
                        var fwd = new PlayerInput(); fwd.Forward = 1;
                        for (int f = 0; f < 60; f++) w.Update(1 / 30f, fwd);
                        Console.WriteLine("rubble    : blocked at y=" + w.P.Y.ToString("0.0", inv) + " (wanted to reach the keycard at y=13)");
                        if (w.P.Y > 11.5f) { Console.WriteLine("  WRONG: the rubble should stop the player reaching the keycard from the room side"); bad++; }

                        Console.WriteLine(bad == 0 ? "the runway behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-tower":
                    {
                        // the checkpoint needs both keys (each in its own room), the exit is a normal door with the
                        // portal glimpsed on the wall past it, the bat hovers, and the boss cycles its three attacks
                        int bad = 0;
                        var w = new World(Levels.ById("E1M3"), new Settings(), null);
                        Door red = null, blue = null, exitDoor = null;
                        foreach (var d in w.Map.Doors) { if (d.Key == 1) red = d; if (d.Key == 2) blue = d; if (d.IsExit) exitDoor = d; }
                        if (red == null || blue == null || exitDoor == null) { Console.WriteLine("missing the checkpoint doors or the exit"); return 1; }
                        w.OpenDoor(red, true);
                        Console.WriteLine("checkpoint: red door " + (red.State == DoorState.Closed ? "stays shut without the key" : "OPENED WITHOUT A KEY"));
                        if (red.State != DoorState.Closed) bad++;
                        w.OpenDoor(blue, true);
                        Console.WriteLine("checkpoint: blue door " + (blue.State == DoorState.Closed ? "stays shut without the key" : "OPENED WITHOUT A KEY"));
                        if (blue.State != DoorState.Closed) bad++;

                        // the two keycards live in separate rooms, not the same one
                        int rx = -1, ry = -1, bx = -1, by = -1;
                        foreach (var sp in w.Map.Spawns) { if (sp.C == 'r') { rx = sp.X; ry = sp.Y; } if (sp.C == 'b') { bx = sp.X; by = sp.Y; } }
                        float keyDist = (float)Math.Sqrt((rx - bx) * (rx - bx) + (ry - by) * (ry - by));
                        Console.WriteLine("keys    : red at " + rx + "," + ry + ", blue at " + bx + "," + by + " (" + keyDist.ToString("0.0", inv) + " apart)");
                        if (rx < 0 || bx < 0 || keyDist < 8) { Console.WriteLine("  WRONG: the two keycards should be well apart, in separate rooms"); bad++; }

                        Console.WriteLine("exit    : texture " + exitDoor.Texture + (exitDoor.Texture == Tex.DOOR_EXIT ? " (normal exit)" : " (WRONG)"));
                        if (exitDoor.Texture != Tex.DOOR_EXIT) bad++;
                        int portalTex = -1;
                        for (int dx = -1; dx <= 1 && portalTex < 0; dx++)
                            for (int dy = -1; dy <= 1 && portalTex < 0; dy++)
                            {
                                int wx = exitDoor.X + dx, wy = exitDoor.Y + dy;
                                if (!w.Map.In(wx, wy) || w.Map.Kind[wy * w.Map.W + wx] != CellKind.Wall) continue;
                                if (w.Map.WallTex[wy * w.Map.W + wx] == Tex.HELL_PORTAL) portalTex = Tex.HELL_PORTAL;
                            }
                        Console.WriteLine("portal  : " + (portalTex == Tex.HELL_PORTAL ? "glimpsed on a wall past the exit" : "NOT FOUND near the exit"));
                        if (portalTex != Tex.HELL_PORTAL) bad++;

                        // the bat demon hovers instead of walking the floor
                        var w2 = new World(Levels.ById("E1M3"), new Settings(), null);
                        w2.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                        var bat = new Monster(MonsterDef.Bat, 10.5f, 10.5f);
                        w2.Actors.Add(bat);
                        var idle = new PlayerInput();
                        for (int f = 0; f < 30; f++) w2.Update(1 / 30f, idle);
                        Console.WriteLine("bat     : hovers at z=" + bat.Z.ToString("0.00", inv));
                        if (bat.Z < 0.3f) { Console.WriteLine("  WRONG: the bat demon should hover well above the floor"); bad++; }

                        // the Elder Fire Demon: a volley, a scream that summons bats, and a self-centred nova that spares itself
                        var w3 = new World(Levels.ById("E1M3"), new Settings(), null);
                        w3.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                        w3.P.X = 110.5f; w3.P.Y = 14.5f;
                        var boss = new Monster(MonsterDef.FireDemon, 110.5f, 10.5f);
                        boss.Alert(w3);
                        w3.Actors.Add(boss);
                        float bossHpBefore = boss.Health;
                        int batsBefore = 0;
                        foreach (var act in w3.Actors) { var bm = act as Monster; if (bm != null && bm.Def == MonsterDef.Bat) batsBefore++; }
                        var order = new List<int>();
                        int last = -1;
                        for (int f = 0; f < 1200 && order.Count < 3; f++)
                        {
                            if (w3.P.Dead) { w3.P.Dead = false; w3.P.HP = 100; }   // the boss should keep fighting, not idle at a corpse
                            w3.Update(1 / 30f, idle);
                            if (boss.State == MState.Fire && boss.BossPattern != last) { order.Add(boss.BossPattern); last = boss.BossPattern; }
                        }
                        string seq = "";
                        for (int i = 0; i < order.Count; i++) seq += (i > 0 ? "," : "") + order[i];
                        Console.WriteLine("boss    : pattern order " + seq);
                        if (order.Count < 3 || order[0] != 0 || order[1] != 1 || order[2] != 2) { Console.WriteLine("  WRONG: it should cycle volley, scream, nova in that order"); bad++; }
                        int batsAfter = 0;
                        foreach (var act in w3.Actors) { var bm = act as Monster; if (bm != null && bm.Def == MonsterDef.Bat) batsAfter++; }
                        Console.WriteLine("boss    : bats before " + batsBefore + ", after the scream " + batsAfter);
                        if (batsAfter <= batsBefore) { Console.WriteLine("  WRONG: the scream should summon more bat demons"); bad++; }
                        Console.WriteLine("boss    : health before the nova " + bossHpBefore.ToString("0", inv) + ", after " + boss.Health.ToString("0", inv));
                        if (boss.Health < bossHpBefore - 1) { Console.WriteLine("  WRONG: the nova should not damage the boss itself"); bad++; }

                        Console.WriteLine(bad == 0 ? "the tower behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-imp":
                    {
                        // the lesser demon: fast, melee only, weak, and undeterred by the odd hit while closing in
                        int bad = 0;
                        var w = new World(Levels.ById("E1M2"), new Settings(), null);
                        w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                        w.P.X = 8.5f; w.P.Y = 8.5f;
                        var imp = new Monster(MonsterDef.Imp, 8.5f, 3.5f);
                        imp.Alert(w);
                        w.Actors.Add(imp);
                        int hpBefore = w.P.HP;
                        var idle = new PlayerInput();
                        for (int f = 0; f < 200 && w.P.HP == hpBefore; f++) w.Update(1 / 30f, idle);
                        Console.WriteLine("melee   : the player took " + (hpBefore - w.P.HP) + " damage, closed to " + imp.DistTo(w.P.X, w.P.Y).ToString("0.00", inv));
                        if (w.P.HP == hpBefore) { Console.WriteLine("  WRONG: it should catch up and claw the player"); bad++; }
                        Console.WriteLine("stats   : health " + MonsterDef.Imp.Health + " (weak) speed " + MonsterDef.Imp.Speed + " (faster than a brute's " + MonsterDef.Brute.Speed + ")");
                        if (MonsterDef.Imp.Speed <= MonsterDef.Brute.Speed) { Console.WriteLine("  WRONG: it should run faster than the brute"); bad++; }
                        if (MonsterDef.Imp.Health >= MonsterDef.Ghoul.Health) { Console.WriteLine("  WRONG: it should be weaker than a ghoul"); bad++; }
                        Console.WriteLine(bad == 0 ? "the lesser demon behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-automap-aim":
                    {
                        // AimSlope has to stay correct for shots even while the full 3D pass is skipped for the automap
                        int bad = 0;
                        var g = new Game(new Settings());
                        var w = new World(Levels.ById("E2M1"), new Settings(), null);
                        w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
                        w.P.X = 8.5f; w.P.Y = 13.5f; w.P.Angle = (float)(-Math.PI / 2); w.P.Pitch = 0.1f;
                        var target = new Monster(MonsterDef.Ghoul, 8.5f, 9.5f);
                        target.Health = 1000;
                        w.Actors.Add(target);
                        var still = new PlayerInput();
                        for (int f = 0; f < 5; f++) w.Update(1 / 30f, still);
                        g.DebugAutomapAim(w, 160, 46);
                        Console.WriteLine("aim slope with automap on: " + w.P.AimSlope.ToString("0.000", inv));
                        for (int i = 0; i < 20; i++) w.PlayerFire(w.P, WeaponDef.All[2], 0, 1);
                        Console.WriteLine("shots landed: " + (1000 - target.Health > 0));
                        if (1000 - target.Health <= 0) { Console.WriteLine("  WRONG: aiming should still work with the map open"); bad++; }
                        Console.WriteLine(bad == 0 ? "automap aim behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-talk":
                    {
                        int bad = 0;
                        var w = new World(Levels.ById("E1M1"), new Settings(), null);
                        Decor npc = null;
                        foreach (var act in w.Actors) { var d = act as Decor; if (d != null && d.Lines != null) npc = d; }
                        if (npc == null) { Console.WriteLine("no talkative traveller found"); return 1; }
                        w.P.X = npc.X - 1.0f; w.P.Y = npc.Y; w.P.Angle = 0;
                        int before = w.Messages.Count;
                        w.PlayerUse(w.P);
                        Console.WriteLine("talk    : messages before " + before + " after " + w.Messages.Count);
                        if (w.Messages.Count <= before) { Console.WriteLine("  WRONG: talking should show a line"); bad++; }
                        Console.WriteLine(bad == 0 ? "npc chatter behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-seal":
                    {
                        // the boarding gate: opens freely with the pass, then seals shut for good once the
                        // player has actually walked through it into the ruined terminal
                        int bad = 0;
                        var w = new World(Levels.ById("E1M1"), new Settings(), null);
                        w.P.Keys[4] = true;
                        Door seal = null;
                        foreach (var d in w.Map.Doors) if (d.SealBehind) seal = d;
                        if (seal == null) { Console.WriteLine("no seal door found"); return 1; }
                        var idle = new PlayerInput();
                        w.P.X = seal.X + 0.5f; w.P.Y = seal.Y + 1.5f;   // the concourse side
                        w.OpenDoor(seal, true);
                        for (int f = 0; f < 20; f++) w.Update(1 / 30f, idle);
                        Console.WriteLine("peek    : opened and stepped back, state " + seal.State + " sealed " + seal.Sealed);
                        if (seal.Sealed) { Console.WriteLine("  WRONG: stepping back without going through should not seal it"); bad++; }
                        w.OpenDoor(seal, true);
                        for (int f = 0; f < 10; f++) w.Update(1 / 30f, idle);
                        w.P.Y = seal.Y - 1.5f;   // walk through, to the ruin side
                        for (int f = 0; f < 200 && !seal.Sealed; f++) w.Update(1 / 30f, idle);
                        Console.WriteLine("through : sealed " + seal.Sealed);
                        if (!seal.Sealed) { Console.WriteLine("  WRONG: it should seal once the player has passed through and it closes"); bad++; }
                        w.OpenDoor(seal, true);
                        Console.WriteLine("retry   : state " + seal.State);
                        if (seal.State == DoorState.Opening) { Console.WriteLine("  WRONG: a sealed door should never open again"); bad++; }
                        Console.WriteLine(bad == 0 ? "the seal door behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-secret2":
                    {
                        // the blue keycard hidden off the baggage hall (a lone check-in desk marks the wall), and the door it opens
                        int bad = 0;
                        var w = new World(Levels.ById("E1M1"), new Settings(), null);
                        PushWall pw = null;
                        foreach (var p in w.Map.PushWalls) if (p.X == 53 && p.Y == 21) pw = p;
                        if (pw == null) { Console.WriteLine("no push wall off the baggage hall"); return 1; }
                        Item key = null;
                        foreach (var act in w.Actors) { var it = act as Item; if (it != null && it.Code == 'b') key = it; }
                        Console.WriteLine("found   : push wall at " + pw.X + "," + pw.Y + ", blue key at " + (key == null ? "none" : key.X + "," + key.Y));
                        if (key == null) { Console.WriteLine("  WRONG: the secret should hold a blue keycard"); bad++; }
                        Door blue = null;
                        foreach (var d in w.Map.Doors) if (d.Key == 2) blue = d;
                        Console.WriteLine("door    : blue door at " + (blue == null ? "none" : blue.X + "," + blue.Y));
                        if (blue == null) { Console.WriteLine("  WRONG: the blue key should open something"); bad++; }
                        // walk there under real movement (not a teleport) so anything standing in the way - like
                        // the desk itself - would actually block it, the way it did for the player
                        w.P.X = pw.X - 4f; w.P.Y = pw.Y + 0.5f; w.P.Angle = 0;
                        var walk = new PlayerInput(); walk.Forward = 1;
                        for (int f = 0; f < 90 && w.P.X < pw.X - 0.7f; f++) w.Update(1 / 30f, walk);
                        Console.WriteLine("walked  : reached " + w.P.X.ToString("0.0", inv) + "," + w.P.Y.ToString("0.0", inv) + " (wall at " + pw.X + "," + pw.Y + ")");
                        if (w.P.X < pw.X - 1.3f) { Console.WriteLine("  WRONG: something is blocking the approach to the secret wall"); bad++; }
                        var use = new PlayerInput(); use.Use = true;
                        w.Update(1 / 30f, use);
                        for (int f = 0; f < 60; f++) w.Update(1 / 30f, new PlayerInput());
                        Console.WriteLine("pushed  : active " + pw.Active + " done " + pw.Done + " moved " + pw.Moved);
                        if (!pw.Done || pw.Moved == 0) { Console.WriteLine("  WRONG: the secret wall should slide open"); bad++; }
                        Console.WriteLine(bad == 0 ? "the check-in secret behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-exit":
                    {
                        // pressing E on the exit opens a distinct door, and the level ends shortly after
                        var w = new World(Levels.ById("E2M1"), new Settings(), null);
                        Door exit = null;
                        foreach (var d in w.Map.Doors) if (d.IsExit) exit = d;
                        if (exit == null) { Console.WriteLine("no exit door found"); return 1; }
                        int bad = 0;
                        Console.WriteLine("before  : state " + exit.State + " open " + exit.Open.ToString("0.00", inv) + " triggered " + w.ExitTriggered);
                        w.OpenDoor(exit, true);
                        var idle = new PlayerInput();
                        for (int f = 0; f < 6; f++) w.Update(1 / 30f, idle);
                        Console.WriteLine("after   : state " + exit.State + " open " + exit.Open.ToString("0.00", inv) + " triggered " + w.ExitTriggered + " done " + w.LevelDone);
                        if (exit.State != DoorState.Opening || exit.Open <= 0) { Console.WriteLine("  WRONG: the exit door should be swinging open"); bad++; }
                        if (!w.ExitTriggered) { Console.WriteLine("  WRONG: pressing the exit should trigger the level end sequence"); bad++; }
                        for (int f = 0; f < 30 && !w.LevelDone; f++) w.Update(1 / 30f, idle);
                        Console.WriteLine("later   : done " + w.LevelDone);
                        if (!w.LevelDone) { Console.WriteLine("  WRONG: the level should finish shortly after"); bad++; }
                        Console.WriteLine(bad == 0 ? "the exit door behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-terminal":
                    {
                        // the airport level: an unarmed start, a gate that wants the boarding pass, the incident, a soldier with a keycard,
                        // the pistol, a save taken after the incident, and old saves finding their way to the hell episode
                        int bad = 0;
                        SaveGame.PathOverride = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "terminalhell-airporttest.txt");
                        var all = Levels.All();
                        var def = Levels.ById("E1M1");
                        var carry = new Player(); carry.MakeUnarmed();
                        var s = new Settings();
                        var w = new World(def, s, carry);
                        var armed = new List<string>();
                        for (int i = 0; i < Player.Weapons; i++) if (w.P.Has[i]) armed.Add(i.ToString());
                        Console.WriteLine("start   : weapons " + string.Join(",", armed.ToArray()) + ", bullets " + w.P.Ammo[0] + ", holding " + w.P.Def.Name);
                        if (armed.Count != 1 || armed[0] != "0" || w.P.Ammo[0] != 0 || w.P.Weapon != 0) { Console.WriteLine("  WRONG: the airport starts with fists and nothing else"); bad++; }

                        Door gate = null;
                        foreach (var d in w.Map.Doors) if (d.Key == 4) gate = d;
                        Item pass = null; Monster carrier = null;
                        foreach (var act in w.Actors)
                        {
                            var it = act as Item; if (it != null && it.Code == '{') pass = it;
                            var m = act as Monster; if (m != null && m.Carries == 'r') carrier = m;
                        }
                        if (gate == null || pass == null || carrier == null) { Console.WriteLine("  WRONG: the level needs a boarding gate, a pass and a soldier with the red keycard"); return 1; }

                        var idle = new PlayerInput();
                        w.OpenDoor(gate, true);
                        Console.WriteLine("gate    : " + (gate.State == DoorState.Closed ? "stays shut without the pass" : "OPENED WITHOUT A PASS"));
                        if (gate.State != DoorState.Closed) bad++;
                        // a minute of calm: the public address chime may sound, and nothing else happens
                        for (int f = 0; f < 1800; f++) w.Update(1 / 30f, idle);
                        if (w.Incident != 0) { Console.WriteLine("  WRONG: nothing should happen until the pass is picked up"); bad++; }

                        w.P.X = pass.X; w.P.Y = pass.Y;
                        w.Update(1 / 30f, idle);
                        for (int f = 0; f < 200; f++) w.Update(1 / 30f, idle);
                        int after = Music.Current;
                        Console.WriteLine("pass    : incident " + w.Incident + ", music now " + after + " (calm " + def.Music + ", after " + def.MusicAfter + "), pass key " + w.P.Keys[4]);
                        if (!w.P.Keys[4]) { Console.WriteLine("  WRONG: picking it up should give the pass"); bad++; }
                        if (w.Incident != 2) { Console.WriteLine("  WRONG: the incident should have run its course"); bad++; }
                        if (after != def.MusicAfter) { Console.WriteLine("  WRONG: the music should turn ominous"); bad++; }
                        if (w.MusicNow() != def.MusicAfter) { Console.WriteLine("  WRONG: a loaded game after the incident should start the ominous music"); bad++; }
                        w.OpenDoor(gate, true);
                        Console.WriteLine("gate    : " + (gate.State == DoorState.Opening ? "opens with the pass" : "STILL SHUT"));
                        if (gate.State != DoorState.Opening) bad++;

                        // the soldier who carries the key
                        carrier.Damage(w, 1000, w.P, false);
                        for (int f = 0; f < 10; f++) w.Update(1 / 30f, idle);
                        bool keyOnFloor = false;
                        foreach (var act in w.Actors) { var it = act as Item; if (it != null && it.Code == 'r' && it.Dropped) keyOnFloor = true; }
                        Console.WriteLine("soldier : " + (keyOnFloor ? "dropped the red keycard" : "DROPPED NOTHING"));
                        if (!keyOnFloor) bad++;

                        // the pistol on its pedestal
                        w.P.Give(w, 'g', false);
                        Console.WriteLine("pistol  : has " + w.P.Has[2] + ", bullets " + w.P.Ammo[0]);
                        if (!w.P.Has[2] || w.P.Ammo[0] < 10) { Console.WriteLine("  WRONG: the pistol should come with bullets"); bad++; }

                        // the ceiling sign really slides
                        var f0 = (int[])Art.MarqueeFrame(0).Px.Clone();
                        var f1 = Art.MarqueeFrame(0.5f).Px;
                        int changed = 0;
                        for (int i = 0; i < f0.Length; i++) if (f0[i] != f1[i]) changed++;
                        Console.WriteLine("sign    : " + changed + " pixels moved in half a second");
                        if (changed < 50) { Console.WriteLine("  WRONG: the sign should scroll"); bad++; }

                        // saving after the incident keeps it, and the carried keycard
                        var head = new SaveHeader { Level = Levels.IndexOf("E1M1"), Difficulty = s.Difficulty };
                        var w3 = new World(def, s, carry);
                        w3.TriggerIncident();
                        for (int f = 0; f < 200; f++) w3.Update(1 / 30f, idle);
                        SaveGame.Save(head, w3);
                        var head2 = SaveGame.ReadHeader();
                        var w4 = head2 == null ? null : SaveGame.Load(head2, all, s);
                        int carriers = 0;
                        if (w4 != null) foreach (var act in w4.Actors) { var m = act as Monster; if (m != null && m.Carries == 'r') carriers++; }
                        Console.WriteLine("save    : incident " + (w4 == null ? -1 : w4.Incident) + ", soldiers still carrying the key " + carriers);
                        if (w4 == null || w4.Incident != 2 || carriers != 1) { Console.WriteLine("  WRONG: the save should keep the incident and the key carrier"); bad++; }

                        // an old save (a bare level number) is one of the hell levels
                        var text = System.IO.File.ReadAllText(SaveGame.Path).Replace("levelid E1M1", "level 1");
                        System.IO.File.WriteAllText(SaveGame.Path, text);
                        var old = SaveGame.ReadHeader();
                        Console.WriteLine("old save: level 1 is now " + (old == null ? "unreadable" : all[old.Level].Id));
                        if (old == null || all[old.Level].Id != "E2M2") { Console.WriteLine("  WRONG: level 1 of the old numbering was the toxic refinery"); bad++; }

                        // the two episodes
                        Console.WriteLine("episodes: " + Levels.Episodes.Length + ", first levels " + all[Levels.FirstOf(0)].Id + " and " + all[Levels.FirstOf(1)].Id);
                        if (all[Levels.FirstOf(0)].Id != "E1M1" || all[Levels.FirstOf(1)].Id != "E2M1") { Console.WriteLine("  WRONG: the episodes start at E1M1 and E2M1"); bad++; }

                        Console.WriteLine(bad == 0 ? "the airport behaves" : bad + " problem(s)");
                        return bad;
                    }
                case "--dev-save":
                    {
                        // play the middle of a level, save it, load it back, and check that nothing changed
                        SaveGame.PathOverride = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "terminalhell-savetest.txt");
                        var s = new Settings();
                        var levels = Levels.All();
                        var w = new World(levels[Levels.IndexOf("E2M2")], s, null);
                        w.P.Has[4] = w.P.Has[10] = true;
                        w.P.Ammo[1] = 20; w.P.Ammo[3] = 15;
                        var rng = new Random(11);
                        var inp = new PlayerInput();
                        for (int f = 0; f < 900; f++)
                        {
                            if (f % 15 == 0)
                            {
                                inp = new PlayerInput();
                                inp.Forward = rng.Next(3) - 1; inp.Strafe = rng.Next(3) - 1;
                                inp.Fire = rng.Next(3) == 0; inp.Run = rng.Next(2) == 0;
                                inp.SelectSlot = rng.Next(10) == 0 ? rng.Next(1, Player.Slots + 1) : 0;
                            }
                            inp.Turn = (float)(rng.NextDouble() - 0.5) * 0.2f;
                            inp.Use = rng.Next(6) == 0;
                            inp.Jump = rng.Next(30) == 0;
                            inp.Parry = rng.Next(15) == 0;
                            if (w.P.Dead) { w.P.Dead = false; w.P.HP = 100; }
                            w.Update(1 / 30f, inp);
                        }
                        // open a door and push a secret wall, so both are saved part way through
                        foreach (var d in w.Map.Doors) if (d.Key == 0) { w.OpenDoor(d, true); break; }
                        foreach (var pw in w.Map.PushWalls)
                        {
                            for (int k = 0; k < 8; k += 2)
                            {
                                int ox = pw.X - Map.DX8[k], oy = pw.Y - Map.DY8[k];
                                if (!w.Map.In(ox, oy) || w.Map.Kind[oy * w.Map.W + ox] != CellKind.Empty) continue;
                                w.P.X = ox + 0.5f; w.P.Y = oy + 0.5f;
                                w.P.Angle = (float)Math.Atan2(Map.DY8[k], Map.DX8[k]);
                                var use = new PlayerInput(); use.Use = true;
                                w.Update(1 / 30f, use);
                                break;
                            }
                            break;
                        }
                        for (int f = 0; f < 40; f++) w.Update(1 / 30f, new PlayerInput());

                        var head = new SaveHeader
                        {
                            Level = Levels.IndexOf("E2M2"), TotalKills = 5, TotalKillsMax = 9, TotalSecrets = 1, TotalSecretsMax = 3,
                            TotalScore = 1234, TotalTime = 99.5f, Difficulty = s.Difficulty,
                        };
                        if (!SaveGame.Save(head, w)) { Console.WriteLine("could not write the save file"); return 1; }
                        var head2 = SaveGame.ReadHeader();
                        if (head2 == null) { Console.WriteLine("could not read the save header"); return 1; }
                        var w2 = SaveGame.Load(head2, levels, s);
                        if (w2 == null) { Console.WriteLine("could not load the save"); return 1; }

                        int bad = 0;
                        bad += SaveCmp("level", head.Level, head2.Level);
                        bad += SaveCmp("total score", head.TotalScore, head2.TotalScore);
                        bad += SaveCmp("total time", head.TotalTime.ToString("0.0", inv), head2.TotalTime.ToString("0.0", inv));
                        bad += SaveCmp("score", w.Score, w2.Score);
                        bad += SaveCmp("kills", w.Kills, w2.Kills);
                        bad += SaveCmp("monsters in the level", w.TotalKills, w2.TotalKills);
                        bad += SaveCmp("items taken", w.ItemsTaken, w2.ItemsTaken);
                        bad += SaveCmp("secrets", w.Secrets, w2.Secrets);
                        bad += SaveCmp("level time", w.LevelTime.ToString("0.0", inv), w2.LevelTime.ToString("0.0", inv));
                        bad += SaveCmp("player position", Pos(w.P.X, w.P.Y), Pos(w2.P.X, w2.P.Y));
                        bad += SaveCmp("health", w.P.HP, w2.P.HP);
                        bad += SaveCmp("armor", w.P.Armor, w2.P.Armor);
                        bad += SaveCmp("weapon", w.P.Weapon, w2.P.Weapon);
                        bad += SaveCmp("ammo", string.Join(",", Array.ConvertAll(w.P.Ammo, x => x.ToString())), string.Join(",", Array.ConvertAll(w2.P.Ammo, x => x.ToString())));
                        bad += SaveCmp("weapons held", Flags(w.P.Has), Flags(w2.P.Has));
                        bad += SaveCmp("keys", Flags(w.P.Keys), Flags(w2.P.Keys));
                        bad += SaveCmp("monsters", Actors(w, ActorKind.Monster), Actors(w2, ActorKind.Monster));
                        bad += SaveCmp("items", Actors(w, ActorKind.Item), Actors(w2, ActorKind.Item));
                        bad += SaveCmp("barrels", Actors(w, ActorKind.Barrel), Actors(w2, ActorKind.Barrel));
                        bad += SaveCmp("doors", Doors(w), Doors(w2));
                        bad += SaveCmp("secret walls", Pushes(w), Pushes(w2));
                        bad += SaveCmp("map cells", Cells(w), Cells(w2));
                        Console.WriteLine(bad == 0 ? "save and load match" : bad + " difference(s) after loading");
                        SaveGame.Delete();
                        return bad;
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
                                else if (sp.C == '{') keyAt[i] = 4;   // the boarding pass
                                else if (sp.C == '`') keyAt[i] = 1;   // a soldier who carries the red keycard
                                else if (sp.C == 'K') keyAt[i] = 3;   // the boss drops the yellow key
                                if ("^>v<".IndexOf(sp.C) >= 0) start = i;
                                else things.Add(sp);
                            }
                            // the tower's boss isn't a map character (see World.SpawnTowerBoss) - stand it in here too
                            if (def.Id == "E1M3") keyAt[10 * m.W + 110] = 3;
                            var keys = new bool[5];
                            bool[] reach = null;
                            for (int pass = 0; pass < 6; pass++)
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
                            foreach (var xd in m.Doors) if (xd.IsExit && reach[xd.Y * m.W + xd.X]) exit = true;
                            var stranded = new List<string>();
                            foreach (var t in things) if (!reach[t.Y * m.W + t.X]) stranded.Add(t.C + "@" + t.X + "," + t.Y);
                            Console.WriteLine(def.Id + ": exit " + (exit ? "REACHABLE" : "NOT REACHABLE") + "  keys " + (keys[1] ? "R" : "-") + (keys[2] ? "B" : "-") + (keys[3] ? "Y" : "-") + (keys[4] ? "P" : "-") +
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
                            for (int wi = 1; wi < Player.Weapons; wi++) w.P.Has[wi] = true;
                            w.P.Ammo[0] = 200; w.P.Ammo[1] = 50; w.P.Ammo[2] = 50; w.P.Ammo[3] = 60; w.P.Ammo[4] = 100;
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
                                    inp.SelectSlot = rng.Next(8) == 0 ? rng.Next(1, Player.Slots + 1) : 0;
                                }
                                inp.Turn = (float)(rng.NextDouble() - 0.5) * 0.1f;
                                inp.Use = rng.Next(10) == 0;
                                inp.Jump = rng.Next(25) == 0;
                                inp.Parry = rng.Next(14) == 0;
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
                            Console.WriteLine(def.Id + ": ok  kills " + w.Kills + "/" + w.TotalKills + "  score " + w.Score + "  alive " + alive + "  secrets " + w.Secrets + "/" + w.TotalSecrets +
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

        // ---------------------------------------------------------------- --dev-arsenal

        /// <summary>A clean copy of the tower's boss arena - a big open room - with nothing alive in it and a player who can't
        /// be hurt, for trying the weapons out in.</summary>
        static World Range()
        {
            var w = new World(Levels.ById("E1M3"), new Settings(), null);
            w.Actors.RemoveAll(x => x.Kind == ActorKind.Monster);
            w.DamageMul = 0;
            return w;
        }

        static void Hold(World w, PlayerInput inp, float seconds)
        {
            for (float t = 0; t < seconds; t += 1 / 30f) w.Update(1 / 30f, inp);
        }

        static Monster Dummy(World w, MonsterDef d, float x, float y, float health)
        {
            var m = new Monster(d, x, y);
            m.Health = health;
            m.State = MState.Pain; m.StateTime = 999;   // held still, so it is where the test put it when the shot arrives
            w.Actors.Add(m);
            return m;
        }

        /// <summary>
        /// The alternate weapons, each doing what it says: slot keys toggle between a slot's two weapons; the double barrel
        /// spends two shells (one if that's all there is) and shoves you back; the laser drains cells while it burns; the laser
        /// ray charges, then sweeps an arc that hits everything in front of you but not behind a wall or behind you; grenades
        /// fly in a low arc, bounce several times and go off on their fuse, or at once against a monster; the saw keeps a
        /// monster from ever finishing an attack but can't stop a boss. Then the campaign: every alternate weapon can be
        /// reached, and is never found before the weapon that shares its slot.
        /// </summary>
        static int DevArsenal()
        {
            var inv = CultureInfo.InvariantCulture;
            int bad = 0;
            var idle = new PlayerInput();
            var fire = new PlayerInput(); fire.Fire = true;

            // ---- slot keys: the second press toggles, a different slot remembers which of its two was last used
            {
                var w = Range();
                w.P.Has[4] = w.P.Has[5] = true; w.P.Ammo[1] = 20; w.P.Weapon = 2;
                var press = new int[] { 3, 3, 2, 3 };
                var expect = new int[] { 4, 5, 2, 5 };
                string got = "";
                for (int i = 0; i < press.Length; i++)
                {
                    var k = new PlayerInput(); k.SelectSlot = press[i];
                    w.Update(1 / 30f, k);
                    Hold(w, idle, 0.5f);
                    got += (i > 0 ? " " : "") + WeaponDef.All[w.P.Weapon].Name;
                    if (w.P.Weapon != expect[i]) bad++;
                }
                Console.WriteLine("slots   : 3, 3, 2, 3 -> " + got);
                if (got != "SHOTGUN DOUBLE SHOTGUN PISTOL DOUBLE SHOTGUN") Console.WriteLine("  WRONG: expected SHOTGUN, DOUBLE SHOTGUN, PISTOL, DOUBLE SHOTGUN");
            }

            // ---- the double barrel: two shells, a shove backwards; with one shell left, one shell
            {
                var w = Range();
                w.P.X = 110.5f; w.P.Y = 18.5f; w.P.Angle = (float)(-Math.PI / 2);
                w.P.Has[5] = true; w.P.Weapon = 5; w.P.Ammo[1] = 5;
                float y0 = w.P.Y;
                w.Update(1 / 30f, fire);
                int after2 = w.P.Ammo[1]; int shots2 = w.P.LastShots;
                Hold(w, idle, 0.4f);
                float shove = w.P.Y - y0;
                Hold(w, idle, 1.2f);
                w.P.Ammo[1] = 1;
                w.Update(1 / 30f, fire);
                Console.WriteLine("dbl bar : 5 shells -> " + after2 + " (" + shots2 + " barrels), shoved back " + shove.ToString("0.00", inv) +
                    ", 1 shell -> " + w.P.Ammo[1] + " (" + w.P.LastShots + " barrel)");
                if (after2 != 3 || shots2 != 2) { Console.WriteLine("  WRONG: both barrels should go off, two shells"); bad++; }
                if (shove < 0.12f) { Console.WriteLine("  WRONG: it should shove the player back"); bad++; }
                if (w.P.Ammo[1] != 0 || w.P.LastShots != 1) { Console.WriteLine("  WRONG: with one shell it should fire the one"); bad++; }
            }

            // ---- the laser: a second of burn drains about 12 cells and kills what it's on
            {
                var w = Range();
                w.P.X = 110.5f; w.P.Y = 18.5f; w.P.Angle = (float)(-Math.PI / 2);
                w.P.Has[6] = true; w.P.Weapon = 6; w.P.Ammo[4] = 50;
                var g = Dummy(w, MonsterDef.Ghoul, 110.5f, 13.5f, 60);
                int on = 0; float dist = 0;
                for (int f = 0; f < 30; f++) { w.Update(1 / 30f, fire); if (w.P.BeamOn) on++; if (f == 5) dist = w.P.BeamDist; }
                int used = 50 - w.P.Ammo[4];
                Hold(w, idle, 0.1f);
                Console.WriteLine("laser   : beam on " + on + "/30 frames, reaching " + dist.ToString("0.0", inv) + ", " + used + " cells, target " +
                    (g.Alive ? "still alive (" + g.Health.ToString("0", inv) + ")" : "dead") + ", beam off after release " + !w.P.BeamOn);
                if (on < 28) { Console.WriteLine("  WRONG: the beam should stay on while the trigger is held"); bad++; }
                if (Math.Abs(dist - 4.7f) > 0.4f) { Console.WriteLine("  WRONG: the beam should end on the ghoul"); bad++; }
                if (used < 10 || used > 14) { Console.WriteLine("  WRONG: it should burn about 12 cells a second"); bad++; }
                if (g.Alive) { Console.WriteLine("  WRONG: a second of beam should kill a 60hp target"); bad++; }
                if (w.P.BeamOn) { Console.WriteLine("  WRONG: the beam should cut out when the trigger is let go"); bad++; }
            }

            // ---- the laser ray: charges, then one arc hits both targets in front, not the one behind a wall or behind you
            {
                var w = Range();
                w.P.X = 106.5f; w.P.Y = 10.5f; w.P.Angle = (float)Math.PI;
                w.P.Has[7] = true; w.P.Weapon = 7; w.P.Ammo[4] = 20;
                var front = Dummy(w, MonsterDef.Ghoul, 104.5f, 9.5f, 1000);
                var wide = Dummy(w, MonsterDef.Ghoul, 104.5f, 12.0f, 1000);
                var walled = Dummy(w, MonsterDef.Ghoul, 100.5f, 12.5f, 1000);
                var behind = Dummy(w, MonsterDef.Ghoul, 109.5f, 10.5f, 1000);
                float firedAt = -1;
                for (int f = 0; f < 40 && firedAt < 0; f++) { w.Update(1 / 30f, fire); if (w.P.Ammo[4] < 20) firedAt = f / 30f; }
                Hold(w, idle, 1.6f);
                Func<Monster, string> hurt = m => m.Health < 1000 ? "hit" : "missed";
                Console.WriteLine("laser ry: fired after " + firedAt.ToString("0.00", inv) + "s charge, " + (20 - w.P.Ammo[4]) + " cells; ahead " + hurt(front) +
                    ", off to the side " + hurt(wide) + ", behind a wall " + hurt(walled) + ", behind you " + hurt(behind));
                if (firedAt < 0.75f || firedAt > 1.0f) { Console.WriteLine("  WRONG: it should charge for about 0.85s first"); bad++; }
                if (w.P.Ammo[4] != 15) { Console.WriteLine("  WRONG: one arc should take 5 cells"); bad++; }
                if (front.Health >= 1000 || wide.Health >= 1000) { Console.WriteLine("  WRONG: the arc should hit everything in front"); bad++; }
                if (walled.Health < 1000) { Console.WriteLine("  WRONG: walls should stop it"); bad++; }
                if (behind.Health < 1000) { Console.WriteLine("  WRONG: it should only go forwards"); bad++; }
                w.P.Ammo[4] = 4;
                Hold(w, idle, 1.2f);
                Hold(w, fire, 1.2f);
                Console.WriteLine("laser ry: with 4 cells: " + (w.P.Ammo[4] == 4 ? "won't fire" : "FIRED"));
                if (w.P.Ammo[4] != 4) bad++;
            }

            // ---- grenades: a low lob that bounces along and goes off on its fuse; straight away against a monster
            {
                var w = Range();
                w.P.X = 110.5f; w.P.Y = 19.5f; w.P.Angle = (float)(-Math.PI / 2);
                w.P.Has[9] = true; w.P.Weapon = 9; w.P.Ammo[2] = 5;
                w.Update(1 / 30f, fire);
                Projectile gr = null;
                foreach (var act in w.Actors) { var pr = act as Projectile; if (pr != null && pr.Type == Projectile.GRENADE) gr = pr; }
                float top = 0, life = 0, lastY = 0; int bounces = 0;
                while (gr != null && !gr.Remove && life < 5)
                {
                    top = Math.Max(top, gr.Z); bounces = gr.Bounces; lastY = gr.Y;
                    w.Update(1 / 30f, idle); life += 1 / 30f;
                }
                Console.WriteLine("grenade : peaked at " + top.ToString("0.00", inv) + " high, bounced " + bounces + " times, went off after " +
                    life.ToString("0.0", inv) + "s, " + (19.5f - lastY).ToString("0.0", inv) + " tiles out");
                if (gr == null) { Console.WriteLine("  WRONG: no grenade"); bad++; }
                if (top > 0.8f) { Console.WriteLine("  WRONG: it should be a low lob, not a mortar shot"); bad++; }
                if (bounces < 3) { Console.WriteLine("  WRONG: it should bounce along the floor several times"); bad++; }
                if (life < 2.2f || life > 2.8f) { Console.WriteLine("  WRONG: with nothing to hit it should go off on its fuse"); bad++; }
                if (19.5f - lastY < 6) { Console.WriteLine("  WRONG: it should carry a fair way"); bad++; }

                var w2 = Range();
                w2.P.X = 110.5f; w2.P.Y = 19.5f; w2.P.Angle = (float)(-Math.PI / 2);
                w2.P.Has[9] = true; w2.P.Weapon = 9; w2.P.Ammo[2] = 5;
                var target = Dummy(w2, MonsterDef.Brute, 110.5f, 16.5f, 1000);
                w2.Update(1 / 30f, fire);
                float t = 0;
                while (target.Health >= 1000 && t < 3) { w2.Update(1 / 30f, idle); t += 1 / 30f; }
                Console.WriteLine("grenade : against a monster three tiles out it went off after " + t.ToString("0.00", inv) + "s");
                if (t > 0.6f) { Console.WriteLine("  WRONG: it should go off as soon as it touches a monster"); bad++; }
            }

            // ---- the saw: a soldier winding up a shot never gets it off while the blade is in it; a boss shrugs it off
            {
                var w = Range();
                w.P.X = 110.5f; w.P.Y = 15.5f; w.P.Angle = (float)(-Math.PI / 2);
                w.P.Has[1] = true; w.P.Weapon = 1;
                var g = Dummy(w, MonsterDef.Ghoul, 110.5f, 14.3f, 1000);
                g.Alert(w);
                g.State = MState.WindUp; g.StateTime = 0.6f;
                int fired = 0;
                for (int f = 0; f < 60; f++) { w.Update(1 / 30f, fire); if (g.State == MState.Fire) fired++; }
                Console.WriteLine("saw     : soldier winding up: interrupted " + g.Interrupted + "x, got a shot off " + fired + "x in 2s, took " +
                    (1000 - g.Health).ToString("0", inv) + " damage, blade at " + (w.P.SawRev * 100).ToString("0", inv) + "% revs");
                if (g.Interrupted < 1 || fired > 0) { Console.WriteLine("  WRONG: the saw should stop it ever firing"); bad++; }
                if (1000 - g.Health < 100) { Console.WriteLine("  WRONG: the saw should chew through it"); bad++; }

                var w2 = Range();
                w2.P.X = 110.5f; w2.P.Y = 15.5f; w2.P.Angle = (float)(-Math.PI / 2);
                w2.P.Has[1] = true; w2.P.Weapon = 1;
                var boss = Dummy(w2, MonsterDef.FireDemon, 110.5f, 14.0f, 700);
                boss.Alert(w2);
                boss.State = MState.WindUp; boss.StateTime = 0.6f;
                bool flinched = false;
                for (int f = 0; f < 15; f++) { w2.Update(1 / 30f, fire); if (boss.State == MState.Pain) flinched = true; }
                Console.WriteLine("saw     : a boss winding up: " + (flinched ? "FLINCHED" : "kept going") + ", took " + (700 - boss.Health).ToString("0", inv) + " damage");
                if (flinched || boss.Health >= 700) bad++;
            }

            // ---- the campaign: every alternate weapon reachable, and never found before the weapon sharing its slot
            {
                var levels = Levels.All();
                var codeToWeapon = new Dictionary<char, int> { { 'g', 2 }, { 'S', 4 }, { 'N', 3 }, { 'L', 8 }, { 'W', 10 }, { '5', 1 }, { '6', 5 }, { '7', 6 }, { '8', 7 }, { '9', 9 } };
                foreach (int start in new[] { Levels.FirstOf(0), Levels.FirstOf(1) })
                {
                    var have = new bool[Player.Weapons];
                    have[0] = true;
                    if (!levels[start].Unarmed) have[2] = true;
                    string order = "";
                    for (int li = start; li < levels.Length; li++)
                    {
                        var def = levels[li];
                        var m = Map.Load(def);
                        int startCell = -1;
                        var found = new List<KeyValuePair<int, int>>();   // weapon, cell
                        foreach (var sp in m.Spawns)
                        {
                            if ("^>v<".IndexOf(sp.C) >= 0) startCell = sp.Y * m.W + sp.X;
                            int wi;
                            if (codeToWeapon.TryGetValue(sp.C, out wi)) found.Add(new KeyValuePair<int, int>(wi, sp.Y * m.W + sp.X));
                        }
                        foreach (var b in World.BonusWeapons)
                            if (b.Level == def.Id) found.Add(new KeyValuePair<int, int>(codeToWeapon[b.Code], (int)b.Y * m.W + (int)b.X));
                        var open = Reach(m, startCell, false);
                        var all = Reach(m, startCell, true);
                        // primaries first, then the rest - a secondary in the same level as its primary has to be behind a lock
                        found.Sort((p, q) => WeaponDef.All[p.Key].SlotPos.CompareTo(WeaponDef.All[q.Key].SlotPos));
                        foreach (var kv in found)
                        {
                            var d = WeaponDef.All[kv.Key];
                            if (!all[kv.Value]) { Console.WriteLine("  WRONG: the " + d.Name + " in " + def.Id + " can't be reached"); bad++; }
                            if (d.SlotPos == 1)
                            {
                                int primary = WeaponDef.BySlot[d.Slot, 0];
                                bool earlier = have[primary];
                                bool sameLevelGated = false;
                                foreach (var kp in found) if (kp.Key == primary && open[kp.Value] && !open[kv.Value]) sameLevelGated = true;
                                if (!earlier && !sameLevelGated)
                                {
                                    Console.WriteLine("  WRONG: the " + d.Name + " (" + def.Id + ") can be found before the " + WeaponDef.All[primary].Name);
                                    bad++;
                                }
                            }
                            if (!have[kv.Key]) order += (order.Length > 0 ? ", " : "") + d.Name + " " + def.Id;
                            have[kv.Key] = true;
                        }
                    }
                    Console.WriteLine("from " + levels[start].Id + ": " + order);
                }
            }

            Console.WriteLine(bad == 0 ? "the arsenal behaves" : bad + " problem(s)");
            return bad;
        }

        /// <summary>Which cells can be walked to from the start: through unlocked doors only, or with every key in hand.</summary>
        static bool[] Reach(Map m, int start, bool allKeys)
        {
            var reach = new bool[m.W * m.H];
            if (start < 0) return reach;
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
                    bool ok = kind == CellKind.Empty || kind == CellKind.Push || (kind == CellKind.Door && (allKeys || m.Doors[m.DoorIdx[j]].Key == 0));
                    if (!ok) continue;
                    reach[j] = true; q.Enqueue(j);
                }
            }
            return reach;
        }

        // ---------------------------------------------------------------- helpers for --dev-save

        static int SaveCmp(string what, object before, object after)
        {
            bool same = before.ToString() == after.ToString();
            if (!same) Console.WriteLine("  DIFFERENT " + what + ": saved " + before + ", loaded " + after);
            return same ? 0 : 1;
        }

        static string Pos(float x, float y)
        {
            var inv = CultureInfo.InvariantCulture;
            return x.ToString("0.00", inv) + "," + y.ToString("0.00", inv);
        }

        static string Flags(bool[] f)
        {
            var sb = new System.Text.StringBuilder();
            foreach (bool b in f) sb.Append(b ? '1' : '0');
            return sb.ToString();
        }

        /// <summary>Every actor of one kind as text, sorted, so two worlds can be compared.</summary>
        static string Actors(World w, ActorKind kind)
        {
            var list = new List<string>();
            foreach (var a in w.Actors)
            {
                if (a.Kind != kind || a.Remove) continue;
                var m = a as Monster;
                var it = a as Item;
                string tag = m != null ? m.Def.Code + " " + m.State + " " + m.Health.ToString("0", CultureInfo.InvariantCulture)
                    : it != null ? it.Code + (it.Dropped ? " dropped" : "")
                    : a.Health.ToString("0", CultureInfo.InvariantCulture);
                list.Add(Pos(a.X, a.Y) + " z" + a.Z.ToString("0.00", CultureInfo.InvariantCulture) + " " + tag);
            }
            list.Sort(StringComparer.Ordinal);
            return list.Count + ": " + string.Join(" | ", list.ToArray());
        }

        static string Doors(World w)
        {
            var list = new List<string>();
            foreach (var d in w.Map.Doors) list.Add(d.State + ":" + d.Open.ToString("0.00", CultureInfo.InvariantCulture));
            return string.Join(",", list.ToArray());
        }

        static string Pushes(World w)
        {
            var list = new List<string>();
            foreach (var p in w.Map.PushWalls) list.Add(p.X + "," + p.Y + " moved " + p.Moved + (p.Done ? " done" : "") + (p.Counted ? " counted" : ""));
            return string.Join(",", list.ToArray());
        }

        static string Cells(World w)
        {
            var counts = new int[4];
            foreach (var k in w.Map.Kind) counts[(int)k]++;
            return "empty " + counts[0] + ", wall " + counts[1] + ", door " + counts[2] + ", secret " + counts[3];
        }
    }
}
