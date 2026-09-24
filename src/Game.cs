// TERMINAL HELL - game states, menus, frame loop and composition of each frame.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TerminalHell
{
    sealed class Game
    {
        enum GState { Title, Playing, Intermission, Victory }

        readonly Settings S;
        readonly Screen scr = new Screen();
        readonly VtPresenter vt = new VtPresenter();
        LegacyPresenter legacy;
        readonly Renderer ren = new Renderer();
        readonly List<SpriteInst> sprites = new List<SpriteInst>();
        readonly List<SpriteInst> spritePool = new List<SpriteInst>();
        readonly FireFx fire = new FireFx();
        readonly LevelDef[] levels;
        readonly List<Menu> menus = new List<Menu>();
        readonly Camera cam = new Camera();

        GState state;
        World world;
        Player carry;
        int levelIndex;
        bool quit, automap, wasFocused = true, fireLock;
        float automapZoom = 1f;
        float time, animTime, stateTime, introTime;
        string notice;                   // a short line under the menus: "SETTINGS RESET." and the like
        float noticeTime;
        int[] burnPix = new int[0];       // the frozen last frame of play, for the burn into the score screen
        int burnW, burnH;
        const float BurnTime = 1.1f;
        float fps, fpsAcc;
        int fpsFrames;
        DisplayMode lastMode;
        int statKills, statItems, statSecrets, statScore;
        float statTime;
        int totalKills, totalKillsMax, totalSecrets, totalSecretsMax, totalScore;
        float totalTime;
        public int StartLevel = -1;
        public float AutoTestSeconds;      // > 0: play a scripted run, log performance and quit
        public string AutoTestLog;
        public bool AutoTestIdle;
        public bool God;
        public bool StartWithAutomap;   // --automap: for measuring the automap's render cost
        public float[] Warp;
        double atChars, atFrames, atTime, atWorst, atDraw, atPresent;
        double presentAvg = 10.5;
        public bool tolFixed;   // set when --tol is given on the command line

        public Game(Settings s)
        {
            S = s;
            levels = Levels.All();
            lastMode = S.Display;
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            Audio.SfxVolume = S.SfxVolume / 10f;
            Audio.MusicVolume = S.MusicVolume / 10f * 0.7f;
        }

        // ================================================================ main loop

        public void Run()
        {
#if !LINUX
            Native.timeBeginPeriod(1);
#endif
            try
            {
                var sw = Stopwatch.StartNew();
                double last = sw.Elapsed.TotalSeconds;
                if (StartLevel >= 0) { S.Difficulty = Math.Max(0, S.Difficulty); NewGame(StartLevel); }
                else OpenTitle();
                if (StartWithAutomap) automap = true;
                while (!quit)
                {
                    double now = sw.Elapsed.TotalSeconds;
                    float dt = (float)Math.Min(0.05, now - last);
                    last = now;

                    Input.Update();
                    int cols, rows;
                    Term.GetSize(out cols, out rows);
                    if (cols != scr.Cols || rows != scr.Rows) { scr.Resize(cols, rows); vt.Invalidate(); }

                    Update(dt);
                    if (quit) break;
                    double t0 = sw.Elapsed.TotalSeconds;
                    Draw(dt);
                    double t1 = sw.Elapsed.TotalSeconds;
                    Present();
                    double t2 = sw.Elapsed.TotalSeconds;
                    // adaptive colour tolerance: exact colours when the console keeps up, coarser when it struggles
                    if (!tolFixed)
                    {
                        presentAvg += ((t2 - t1) * 1000 - presentAvg) * 0.05;
                        // a bigger console (full screen, high DPI) has far more cells to redraw, so it needs a
                        // wider tolerance ceiling to actually reach a steady frame time instead of pegging at 15
                        if (presentAvg > 13 && VtPresenter.Tolerance < 22) { VtPresenter.Tolerance++; presentAvg = 10.5; }
                        else if (presentAvg < 7 && VtPresenter.Tolerance > 7) { VtPresenter.Tolerance--; presentAvg = 10.5; }
                    }
                    if (AutoTestSeconds > 0 && state == GState.Playing && stateTime > 0.5f) { atDraw += t1 - t0; atPresent += t2 - t1; }

                    fpsFrames++;
                    fpsAcc += dt;
                    if (fpsAcc >= 0.5f) { fps = fpsFrames / fpsAcc; fpsFrames = 0; fpsAcc = 0; }

                    if (AutoTestSeconds > 0 && state == GState.Playing && stateTime > 0.5f)
                    {
                        atFrames++; atTime += dt; atChars += vt.LastChars;
                        atWorst = Math.Max(atWorst, sw.Elapsed.TotalSeconds - now);
                        if (atTime >= AutoTestSeconds)
                        {
                            File.WriteAllText(AutoTestLog, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "cols={0} rows={1} aspect={2:0.00} classic={3} vt={4} mode={5} tol=" + VtPresenter.Tolerance + "\r\nfps={6:0.0} worstFrameMs={7:0.0} avgChars={8:0} drawMs={13:0.0} presentMs={14:0.0}\r\nhp={9} kills={10} pos={11:0.0},{12:0.0}\r\n",
                                scr.Cols, scr.Rows, Term.CellAspect, Term.IsClassicConsole, Term.VtOk, S.Display,
                                atFrames / atTime, atWorst * 1000, atChars / atFrames, world.P.HP, world.Kills, world.P.X, world.P.Y, atDraw / atFrames * 1000, atPresent / atFrames * 1000) + Term.Diag() + "\r\n");
                            quit = true;
                        }
                    }

                    // cap at ~62 fps
                    while (sw.Elapsed.TotalSeconds - now < 1.0 / 62) Thread.Sleep(1);
                }
            }
            finally
            {
#if !LINUX
                Native.timeEndPeriod(1);
#endif
            }
        }

        void Present()
        {
            var mode = Term.VtOk ? S.Display : DisplayMode.Legacy;
            if (mode != lastMode) { vt.Invalidate(); lastMode = mode; }
            if (mode == DisplayMode.Legacy)
            {
                if (legacy == null) legacy = new LegacyPresenter();
                legacy.Present(scr);
            }
            else vt.Present(scr, mode == DisplayMode.Ascii);
        }

        /// <summary>Developer aid: runs the automap's camera-only path against a caller-supplied world, so
        /// --dev-automap-aim can check that AimSlope stays right when the full 3D pass is skipped.</summary>
        public void DebugAutomapAim(World w, int cols, int rows)
        {
            world = w;
            automap = true;
            scr.Resize(cols, rows);
            int viewH = (scr.Rows - Hud.Rows(scr.Rows)) * 2;
            cam.X = w.P.X; cam.Y = w.P.Y; cam.Angle = w.P.Angle; cam.Pitch = w.P.Pitch; cam.EyeZ = w.P.EyeZ;
            cam.Fov = S.Fov;
            ren.Aspect = Term.CellAspect;
            ren.PrepareCameraOnly(scr.W, viewH, cam);
            w.P.AimSlope = (ren.Horizon - viewH * 0.5f) / ren.ProjY;
        }

        /// <summary>Developer aid: renders one in-game frame (with HUD) to a PNG without a console.</summary>
        public void DebugFrame(int cols, int rows, int level, float x, float y, float angleDeg, float simSeconds, string outPath, bool ascii, int weapon, float pitch)
        {
            scr.Resize(cols, rows);
            NewGame(level);
            world.P.X = x; world.P.Y = y; world.P.Angle = (float)(angleDeg * Math.PI / 180);
            world.P.Pitch = pitch;
            if (weapon >= 0)
            {
                for (int i = 0; i < Player.Weapons; i++) world.P.Has[i] = true;
                world.P.Weapon = weapon;
                for (int k = 1; k < world.P.Keys.Length; k++) world.P.Keys[k] = true;
                world.P.Ammo[0] = 50;
            }
            var inp = new PlayerInput();
            for (float t = 0; t < simSeconds; t += 1 / 30f) { world.Update(1 / 30f, inp); introTime -= 1 / 30f; }
            Draw(1 / 30f);
            if (ascii)
            {
                // emulate the ASCII presenter by converting pixel cells to glyph cells
                const string ramp = " .,:;-=+*oa#%&@";
                for (int r = 0; r < scr.Rows; r++)
                    for (int c = 0; c < scr.Cols; c++)
                    {
                        int i = r * scr.Cols + c;
                        if (scr.TCh[i] != '\0') continue;
                        int top = scr.Pix[r * 2 * scr.W + c], bot = scr.Pix[(r * 2 + 1) * scr.W + c];
                        int rr = (Col.R(top) + Col.R(bot)) / 2, gg = (Col.G(top) + Col.G(bot)) / 2, bb = (Col.B(top) + Col.B(bot)) / 2;
                        int lum = (rr * 3 + gg * 6 + bb) / 10;
                        int idx = (int)(Math.Pow(lum / 255.0, 0.8) * (ramp.Length - 1) + 0.5);
                        int mx = Math.Max(1, Math.Max(rr, Math.Max(gg, bb)));
                        float k = 0.45f + 0.55f * 255f / mx;
                        scr.Put(c, r, ramp[Math.Min(ramp.Length - 1, idx)], Col.Rgb((int)(rr * k), (int)(gg * k), (int)(bb * k)), 0);
                    }
            }
            DebugTools.SaveTerminalShot(scr, outPath);
        }

        /// <summary>Developer aid: renders the title, end-of-level or end-of-game screen with sample numbers.</summary>
        public void DebugEndScreen(int cols, int rows, string which, float t, string outPath)
        {
            scr.Resize(cols, rows);
            levelIndex = 0;
            statKills = 87; statItems = 64; statSecrets = 50; statScore = 12450; statTime = 214;
            totalKills = 120; totalKillsMax = 142; totalSecrets = 5; totalSecretsMax = 8; totalScore = 48350; totalTime = 731;
            if (which == "title" || which == "options" || which == "episodes")
            {
                for (int e = 0; e < Settings.Episodes; e++) { S.BestScores[e] = totalScore - e * 9000; S.BestTimes[e] = totalTime - e * 300; S.Beaten[e] = true; }
                state = GState.Title;
                menus.Clear();
                menus.Add(MainMenu());
                if (which == "options") menus.Add(OptionsMenu());
                if (which == "episodes") menus.Add(EpisodeMenu());
            }
            else if (which == "burn")
            {
                // a frame of play, frozen, part way through burning into the score screen (t = 0..1)
                NewGame(0);
                var inp = new PlayerInput();
                for (float s = 0; s < 1.5f; s += 1 / 30f) { world.Update(1 / 30f, inp); introTime -= 1 / 30f; }
                Draw(1 / 30f);
                if (burnPix.Length != scr.Pix.Length) burnPix = new int[scr.Pix.Length];
                Array.Copy(scr.Pix, burnPix, scr.Pix.Length);
                burnW = scr.W; burnH = scr.H;
                state = GState.Intermission;
                stateTime = -BurnTime * (1 - Math.Max(0, Math.Min(1, t)));
                Draw(1 / 30f);
                DebugTools.SaveTerminalShot(scr, outPath);
                return;
            }
            else state = which == "victory" ? GState.Victory : GState.Intermission;
            stateTime = t;
            time = t;
            Draw(1 / 30f);
            DebugTools.SaveTerminalShot(scr, outPath);
        }

        // ================================================================ state changes

        void OpenTitle()
        {
            state = GState.Title;
            stateTime = 0;
            world = null;
            menus.Clear();
            menus.Add(MainMenu());
            Music.Play(0);
            fire.Dying = false;
        }

        void NewGame(int level)
        {
            carry = new Player();
            if (levels[level].Unarmed) carry.MakeUnarmed();
            totalKills = totalKillsMax = totalSecrets = totalSecretsMax = totalScore = 0;
            totalTime = 0;
            StartLevelAt(level);
        }

        void StartLevelAt(int i)
        {
            levelIndex = i;
            world = new World(levels[i], S, carry);
            if (God) world.DamageMul = 0;
            carry = world.P.CloneInventory();
            state = GState.Playing;
            stateTime = 0;
            introTime = 4f;
            automap = false;
            menus.Clear();
            Music.Play(world.MusicNow());
            world.Message(levels[i].Intro, Col.Rgb(255, 190, 110));
#if LINUX
            foreach (var note in Input.TakeNotices())
            {
                world.Message(note, Col.Rgb(255, 210, 120));
                world.Messages[world.Messages.Count - 1].Time = 9f;   // what this terminal can't do: worth reading once
            }
#endif
            Input.ClearKeys();
            WriteSave();   // the start of every level is kept, so the title screen always offers the run back
        }

        void RestartLevel()
        {
            world = new World(levels[levelIndex], S, carry);
            state = GState.Playing;
            introTime = 2.5f;
            menus.Clear();
            Music.Play(world.MusicNow());
            Input.ClearKeys();
        }

        void FinishLevel()
        {
            // freeze the last frame of play: the score screen burns in from it
            if (burnPix.Length != scr.Pix.Length) burnPix = new int[scr.Pix.Length];
            Array.Copy(scr.Pix, burnPix, scr.Pix.Length);
            burnW = scr.W; burnH = scr.H;
            Audio.Play(Sfx.HurtFloor, 0.5f, 0, 0.65f, 0);
            state = GState.Intermission;
            stateTime = -BurnTime;
            statKills = world.TotalKills > 0 ? world.Kills * 100 / world.TotalKills : 100;
            statItems = world.TotalItems > 0 ? world.ItemsTaken * 100 / world.TotalItems : 100;
            statSecrets = world.TotalSecrets > 0 ? world.Secrets * 100 / world.TotalSecrets : 100;
            statTime = world.LevelTime;
            statScore = world.Score;
            totalKills += world.Kills; totalKillsMax += world.TotalKills;
            totalSecrets += world.Secrets; totalSecretsMax += world.TotalSecrets;
            totalScore += world.Score;
            totalTime += world.LevelTime;
            carry = world.P.CloneInventory();
            Music.Play(4);
            Input.CaptureWanted = false;
        }

        // ================================================================ menus

        Menu MainMenu()
        {
            var m = new Menu("MAIN MENU");
            var saved = SaveGame.ReadHeader();
            if (saved != null) m.Add("CONTINUE GAME  " + saved.Describe(levels), () => LoadSaved());
            // the episode picker only shows up once the whole game has been beaten at least once; until then, straight into episode 1
            m.Add("NEW GAME", () => { if (S.Beaten[Levels.Episodes.Length - 1]) menus.Add(EpisodeMenu()); else menus.Add(DifficultyMenu(Levels.FirstOf(0))); });
            m.Add("OPTIONS", () => menus.Add(OptionsMenu()));
            m.Add("CONTROLS", () => menus.Add(ControlsMenu()));
            m.Add("QUIT", () => quit = true);
            m.OnBack = () => { };
            return m;
        }

        Menu EpisodeMenu()
        {
            var m = new Menu("CHOOSE AN EPISODE");
            for (int e = 0; e < Levels.Episodes.Length; e++)
            {
                int first = Levels.FirstOf(e);
                var ep = Levels.Episodes[e];
                m.Add("EPISODE " + (e + 1) + " - " + ep.Name + "  " + ep.Blurb, () => menus.Add(DifficultyMenu(first)));
            }
            m.Sel = lastEpisode;
            return m;
        }

        int lastEpisode;

        Menu DifficultyMenu(int firstLevel)
        {
            lastEpisode = levels[firstLevel].Episode;
            var m = new Menu("CHOOSE YOUR FATE");
            m.Add("EASY   - ROOKIE", () => { S.Difficulty = 0; S.Save(); NewGame(firstLevel); });
            m.Add("NORMAL - SOLDIER", () => { S.Difficulty = 1; S.Save(); NewGame(firstLevel); });
            m.Add("HARD   - BERSERKER", () => { S.Difficulty = 2; S.Save(); NewGame(firstLevel); });
            m.Sel = S.Difficulty;
            return m;
        }

        /// <summary>The level that follows the one being played, or -1 at the end of its episode.</summary>
        int NextLevel()
        {
            return levelIndex + 1 < levels.Length && levels[levelIndex + 1].Episode == levels[levelIndex].Episode ? levelIndex + 1 : -1;
        }

        Menu PauseMenu()
        {
            var m = new Menu("PAUSED");
            m.Add("RESUME", () => menus.Clear());
            m.Add("SAVE GAME", () => SaveNow());
            if (SaveGame.Exists) m.Add("LOAD GAME", () => LoadSaved());
            m.Add("OPTIONS", () => menus.Add(OptionsMenu()));
            m.Add("CONTROLS", () => menus.Add(ControlsMenu()));
            m.Add("RESTART LEVEL", () => RestartLevel());
            m.Add("QUIT TO TITLE", () => OpenTitle());
            m.Add("QUIT GAME", () => quit = true);
            return m;
        }

        static string OnOff(bool b) { return b ? "ON" : "OFF"; }

        static string Bar(float v, float max)
        {
            int n = (int)Math.Round(v / max * 10);
            return new string('█', Math.Max(0, Math.Min(10, n))) + new string('░', 10 - Math.Max(0, Math.Min(10, n)));
        }

        Menu OptionsMenu()
        {
            var m = new Menu("OPTIONS");
            string[] modes = { "HD PIXELS", "ASCII", "16 COLORS" };
            string[] res = { "LOW", "MEDIUM", "HIGH", "FULL SCREEN" };
            m.AddValue("DISPLAY MODE", () => modes[(int)S.Display], d => { S.Display = S.Display == DisplayMode.HD ? DisplayMode.Ascii : DisplayMode.HD; });
            if (Term.IsClassicConsole)
            {
                m.AddValue("RESOLUTION", () => res[S.Resolution], d => { S.Resolution = Math.Max(0, Math.Min(3, S.Resolution + d)); Term.ApplyVideo(S); });
                m.AddValue("FONT", () => S.CrispFont ? "CRISP" : "SMOOTH", d => { S.CrispFont = !S.CrispFont; Term.ApplyVideo(S); });
            }
            else
            {
                m.AddValue("RESOLUTION", () => "CTRL +/- ZOOM", d => { });
            }
            m.AddValue("MOUSE SPEED", () => Bar(S.MouseSens, 3f) + " " + S.MouseSens.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                d => { S.MouseSens = Math.Max(0.1f, Math.Min(5f, (float)Math.Round(S.MouseSens + d * 0.1f, 1))); });
            m.AddValue("INVERT MOUSE", () => OnOff(S.InvertY), d => S.InvertY = !S.InvertY);
            m.AddValue("LOOK UP/DOWN", () => OnOff(S.MouseLook), d => S.MouseLook = !S.MouseLook);
            m.AddValue("FIELD OF VIEW", () => S.Fov.ToString(), d => S.Fov = Math.Max(60, Math.Min(100, S.Fov + d * 2)));
            m.AddValue("CROSSHAIR", () => OnOff(S.Crosshair), d => S.Crosshair = !S.Crosshair);
            m.AddValue("HEAD BOB", () => OnOff(S.HeadBob), d => S.HeadBob = !S.HeadBob);
            m.AddValue("SOUND VOLUME", () => Bar(S.SfxVolume, 10), d => { S.SfxVolume = Math.Max(0, Math.Min(10, S.SfxVolume + d)); ApplyVolumes(); Audio.Play(Sfx.Pistol, 0.6f, 0, 1, 0); });
            m.AddValue("MUSIC VOLUME", () => Bar(S.MusicVolume, 10), d => { S.MusicVolume = Math.Max(0, Math.Min(10, S.MusicVolume + d)); ApplyVolumes(); });
            m.AddValue("PICKUP TEXT", () => S.BigPickupText ? "LARGE" : "SMALL", d => S.BigPickupText = !S.BigPickupText);
            m.AddValue("SHOW FPS", () => OnOff(S.ShowFps), d => S.ShowFps = !S.ShowFps);
            m.AddValue("CHECK FOR UPDATES", () => OnOff(S.UpdateCheck), d => S.UpdateCheck = !S.UpdateCheck);
            m.Items.Add(new MenuItem { Text = "", Spacer = true });
            m.Add("RESET SETTINGS", () => menus.Add(ConfirmMenu("PUT EVERY SETTING BACK TO NORMAL?",
                "SETTINGS RESET.", () => { S.ResetToDefaults(); ApplyVolumes(); Term.ApplyVideo(S); })));
            m.Add("RESET SAVED GAME", () => menus.Add(ConfirmMenu("ERASE THE SAVED GAME AND ALL RECORDS?",
                "SAVED GAME AND RECORDS ERASED.", () => { SaveGame.Delete(); S.ClearProgress(); RefreshRootMenu(); })));
            m.Items.Add(new MenuItem { Text = "", Spacer = true });
            m.Add("BACK", () => CloseTopMenu());
            m.OnBack = () => S.Save();
            return m;
        }

        /// <summary>A yes/no box in front of anything that cannot be undone. NO is what it opens on.</summary>
        Menu ConfirmMenu(string question, string done, Action act)
        {
            var m = new Menu("ARE YOU SURE?");
            m.Lines = new[] { question };
            m.Add("NO - LEAVE IT ALONE", () => CloseTopMenu());
            m.Add("YES", () =>
            {
                act();
                CloseTopMenu();
                notice = done;
                noticeTime = 3.5f;
                if (world != null) world.Message(done, Col.Rgb(255, 200, 120));
            });
            return m;
        }

        Menu ControlsMenu()
        {
            var m = new Menu("CONTROLS");
            m.Lines = new[]
            {
                "MOVE",
                "  W A S D ........ MOVE / STRAFE",
                "  SHIFT .......... RUN",
                "  SPACE .......... JUMP",
                "  MOUSE .......... LOOK AROUND",
                "  ARROW KEYS ..... MOVE / TURN",
                "ACTION",
                "  LEFT CLICK / F . FIRE",
                "  RIGHT CLICK .... PUNCH - TIME IT TO PARRY A PROJECTILE",
                "  E .............. OPEN DOORS, SWITCHES, SECRETS",
                "  1-6 / WHEEL .... CHOOSE WEAPON   Q  LAST WEAPON",
                "OTHER",
                "  TAB ............ AUTOMAP",
                "  ESC ............ PAUSE MENU",
                "  F5 ............. HD PIXELS / ASCII",
                "  F12 ............ SCREENSHOT",
                "  " + (Term.FullscreenKey + " ").PadRight(16, '.') + " FULLSCREEN",
            };
            m.Add("BACK", () => CloseTopMenu());
            return m;
        }

        /// <summary>Writes the one save slot: where you are, what you carry, and the state of the whole level.</summary>
        bool WriteSave()
        {
            if (world == null) return false;
            var h = new SaveHeader
            {
                Level = levelIndex,
                TotalKills = totalKills, TotalKillsMax = totalKillsMax,
                TotalSecrets = totalSecrets, TotalSecretsMax = totalSecretsMax,
                TotalScore = totalScore, TotalTime = totalTime,
                Difficulty = S.Difficulty,
            };
            return SaveGame.Save(h, world);
        }

        void SaveNow()
        {
            if (world == null) return;
            bool ok = WriteSave();
            menus.Clear();
            Input.ClearKeys();
            world.Message(ok ? "GAME SAVED." : "COULD NOT WRITE THE SAVE FILE.", ok ? Col.Rgb(140, 255, 140) : Col.Rgb(255, 120, 60));
        }

        /// <summary>Picks the saved game back up, exactly where it was left.</summary>
        void LoadSaved()
        {
            var h = SaveGame.ReadHeader();
            if (h == null)
            {
                if (world != null) world.Message("THERE IS NO SAVED GAME.", Col.Rgb(255, 120, 60));
                return;
            }
            S.Difficulty = h.Difficulty;   // the run keeps the difficulty it was started on
            var w = SaveGame.Load(h, levels, S);
            if (w == null)
            {
                if (world != null) world.Message("THE SAVED GAME COULD NOT BE READ.", Col.Rgb(255, 120, 60));
                return;
            }
            world = w;
            if (God) world.DamageMul = 0;
            levelIndex = h.Level;
            totalKills = h.TotalKills; totalKillsMax = h.TotalKillsMax;
            totalSecrets = h.TotalSecrets; totalSecretsMax = h.TotalSecretsMax;
            totalScore = h.TotalScore; totalTime = h.TotalTime;
            carry = world.P.CloneInventory();
            state = GState.Playing;
            stateTime = 0;
            introTime = 1.6f;
            automap = false;
            fireLock = true;
            menus.Clear();
            Music.Play(world.MusicNow());
            Input.ClearKeys();
            world.Message("GAME LOADED - " + levels[levelIndex].Id + ": " + levels[levelIndex].Name, Col.Rgb(140, 255, 140));
        }

        /// <summary>Once the save has been thrown away the menu underneath must stop offering to load it.</summary>
        void RefreshRootMenu()
        {
            if (menus.Count == 0) return;
            if (state == GState.Title) menus[0] = MainMenu();
            else if (state == GState.Playing) menus[0] = PauseMenu();
        }

        void CloseTopMenu()
        {
            if (menus.Count == 0) return;
            var m = menus[menus.Count - 1];
            if (state == GState.Title && menus.Count == 1) return;   // the title always keeps its main menu
            menus.RemoveAt(menus.Count - 1);
            if (m.OnBack != null) m.OnBack();
            Input.ClearKeys();
        }

        // ================================================================ update

        void Update(float dt)
        {
            time += dt;
            stateTime += dt;
            if (noticeTime > 0) noticeTime -= dt;
            // the weapon and the world clock stop while a menu is up, so nothing moves in a paused frame
            if (menus.Count == 0) animTime += dt;
            fire.Update(dt);

            if (Input.Hit(Input.VK_F5))
            {
                S.Display = S.Display == DisplayMode.HD ? DisplayMode.Ascii : DisplayMode.HD;
                S.Save();
                string[] names = { "HD PIXELS", "ASCII", "16 COLORS" };
                if (world != null) world.Message("DISPLAY: " + names[(int)S.Display], -1);
            }
            if (Input.Hit(Input.VK_F12)) Screenshot();
            // a newer version has been published: U from the title screen hands over to the installer
            if (state == GState.Title && Updater.Available && Input.Hit('U') && Updater.Install()) quit = true;

            if (menus.Count > 0)
            {
                Input.CaptureWanted = false;
                fireLock = true;   // the click that closes a menu must not also fire the gun
                var m = menus[menus.Count - 1];
                if (!m.Update())
                {
                    if (state == GState.Playing && menus.Count == 1) { menus.Clear(); if (m.OnBack != null) m.OnBack(); Input.ClearKeys(); }
                    else CloseTopMenu();
                }
                return;
            }

            switch (state)
            {
                case GState.Title:
                    menus.Add(MainMenu());
                    break;
                case GState.Playing:
                    UpdatePlaying(dt);
                    break;
                case GState.Intermission:
                    if (stateTime > 0.8f && (Input.Hit(Input.VK_RETURN) || Input.Hit(Input.VK_SPACE) || Input.MouseCellClick || Input.LHit || Input.Hit(Input.VK_ESCAPE)))
                    {
                        if (stateTime < 2.9f) stateTime = 2.9f;   // skip the count-up
                        else if (NextLevel() >= 0) StartLevelAt(NextLevel());
                        else
                        {
                            // the episode is finished: the highest score and the fastest run are kept separately, per episode
                            int ep = levels[levelIndex].Episode;
                            bool better = !S.Beaten[ep];
                            S.Beaten[ep] = true;
                            if (totalScore > S.BestScores[ep]) { S.BestScores[ep] = totalScore; better = true; }
                            if (S.BestTimes[ep] <= 0 || totalTime < S.BestTimes[ep]) { S.BestTimes[ep] = totalTime; better = true; }
                            if (better) S.Save();

                            int nextEp = ep + 1;
                            if (nextEp < Levels.Episodes.Length)
                            {
                                // straight into the next episode: fresh totals, same difficulty, whatever is still in the player's hands
                                totalKills = totalKillsMax = totalSecrets = totalSecretsMax = totalScore = 0;
                                totalTime = 0;
                                StartLevelAt(Levels.FirstOf(nextEp));
                            }
                            else
                            {
                                state = GState.Victory;
                                stateTime = 0;
                                Music.Play(0);
                            }
                        }
                    }
                    break;
                case GState.Victory:
                    // not before the total score is on screen
                    if (stateTime > 4 && (Input.Hit(Input.VK_RETURN) || Input.Hit(Input.VK_ESCAPE) || Input.MouseCellClick)) OpenTitle();
                    break;
            }
        }

        void UpdatePlaying(float dt)
        {
            Input.CaptureWanted = true;
            bool focused = Input.Focused || AutoTestSeconds > 0;
            if (Input.Hit(Input.VK_ESCAPE) || (wasFocused && !focused && !world.P.Dead))
            {
                wasFocused = focused;
                menus.Add(PauseMenu());
                Input.CaptureWanted = false;
                return;
            }
            wasFocused = focused;
            if (Input.Hit(Input.VK_TAB)) automap = !automap;
            if (Input.Hit(Input.VK_F4)) { S.ShowFps = !S.ShowFps; S.Save(); }

            var inp = new PlayerInput();
            bool up = Input.Down('W') || Input.Down(Input.VK_UP), down = Input.Down('S') || Input.Down(Input.VK_DOWN);
            inp.Forward = (up ? 1 : 0) - (down ? 1 : 0);
            inp.Strafe = (Input.Down('D') ? 1 : 0) - (Input.Down('A') ? 1 : 0);
            inp.Run = Input.Shift;
            float keyTurn = (Input.Down(Input.VK_RIGHT) ? 1 : 0) - (Input.Down(Input.VK_LEFT) ? 1 : 0);
            inp.Turn = Input.MouseDX * 0.0022f * S.MouseSens + keyTurn * 2.6f * dt * (inp.Run ? 1.4f : 1);
            if (S.MouseLook) inp.Look = -Input.MouseDY * 0.0016f * S.MouseSens * (S.InvertY ? -1 : 1);
            else world.P.Pitch *= Math.Max(0, 1 - dt * 8);
            if (Input.Hit(Input.VK_END)) world.P.Pitch = 0;
            inp.Fire = Input.LButton || Input.Down('F');
            if (fireLock)
            {
                if (inp.Fire) inp.Fire = false;
                else fireLock = false;
            }
            inp.Use = Input.Hit('E');
            inp.Jump = Input.Hit(Input.VK_SPACE);
            inp.Parry = Input.RHit;
            for (int i = 0; i < Player.Slots; i++) if (Input.Hit('1' + i)) inp.SelectSlot = i + 1;
            if (Input.Wheel != 0)
            {
                if (automap) automapZoom = Math.Max(0.4f, Math.Min(3f, automapZoom + Input.Wheel * 0.15f));
                else inp.Cycle = Input.Wheel > 0 ? -1 : 1;
            }
            if (Input.Hit('Q') && world.P.Has[world.P.LastWeapon]) inp.SelectWeapon = world.P.LastWeapon + 1;

            if (Warp != null && stateTime < 0.1f)
            {
                world.P.X = Warp[0]; world.P.Y = Warp[1]; world.P.Angle = (float)(Warp[2] * Math.PI / 180);
            }
            if (AutoTestSeconds > 0 && AutoTestIdle)
            {
                inp = new PlayerInput();
                inp.Fire = stateTime > 2.5f && ((int)(stateTime * 3) % 4 == 0);
                wasFocused = true;
                focused = true;
            }
            else if (AutoTestSeconds > 0)
            {
                // scripted run: walk to the hub, look around and shoot
                float t = stateTime;
                inp = new PlayerInput();
                inp.Forward = t < 3.2f ? 1 : (t > 5 && t < 6.5f ? 1 : 0);
                inp.Turn = (t > 3.2f && t < 5f) ? dt * 1.2f : (t > 6.5f ? dt * (float)Math.Sin(t) * 1.5f : 0);
                inp.Fire = t > 3.5f && ((int)(t * 2) % 3 == 0);
                inp.Use = t > 2.8f && t < 2.9f;
                inp.Run = t > 5;
                wasFocused = true;
                focused = true;
            }

            if (world.P.Dead)
            {
                inp = new PlayerInput();
                if (world.P.DeadTime > 1.3f && (Input.Hit(Input.VK_RETURN) || Input.Hit(Input.VK_SPACE) || Input.LHit || Input.Hit('E')))
                {
                    RestartLevel();
                    return;
                }
            }

            world.Update(dt, inp);
            introTime -= dt;
            if (world.LevelDone) FinishLevel();
        }

        void Screenshot()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "TerminalHell");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "shot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                DebugTools.SaveTerminalShot(scr, file);
                if (world != null) world.Message("SCREENSHOT SAVED TO PICTURES" + Path.DirectorySeparatorChar + "TERMINALHELL", -1);
            }
            catch { }
        }

        // ================================================================ drawing

        void Draw(float dt)
        {
            scr.ClearText();
            if (menus.Count > 0) dt = 0;   // a paused frame is a still frame
            switch (state)
            {
                case GState.Title: DrawTitle(); break;
                case GState.Playing: DrawPlaying(dt); break;
                case GState.Intermission: DrawIntermission(); break;
                case GState.Victory: DrawVictory(); break;
            }
            if (menus.Count > 0)
            {
                var m = menus[menus.Count - 1];
                if (state == GState.Playing)
                {
                    scr.DarkenPix(0, 0, scr.W, scr.H, 0.45f);
                    m.Draw(scr, Math.Max(1, scr.Rows / 5), true);
                }
                else if (state == GState.Title)
                {
                    int top = Math.Max(titleBottom + 1, scr.Rows / 2);
                    m.Draw(scr, Math.Min(top, Math.Max(0, scr.Rows - m.Items.Count - 6 - (m.Lines != null ? m.Lines.Length + 1 : 0))), true);
                }
                else m.Draw(scr, scr.Rows / 4, true);
            }
            if (noticeTime > 0 && state != GState.Playing)
                scr.PrintCenter(scr.Rows - 2, " " + notice + " ", Col.Rgb(255, 210, 130), Col.Rgb(40, 12, 8));
            if (S.ShowFps)
            {
                string f = ((int)(fps + 0.5f)) + " FPS " + scr.Cols + "x" + scr.Rows;
                scr.Print(scr.Cols - f.Length - 1, 0, f, Col.Rgb(120, 255, 120), Col.Rgb(0, 0, 0));
            }
        }

        int titleBottom;

        void DrawTitle()
        {
            int W = scr.W, H = scr.H;
            for (int y = 0; y < H; y++)
            {
                int c = Col.Lerp(Col.Rgb(6, 2, 4), Col.Rgb(40, 6, 4), (float)y / H);
                for (int x = 0; x < W; x++) scr.Pix[y * W + x] = c;
            }
            fire.Draw(scr, H * 11 / 20, H, 0.9f);
            // logo
            int s1 = Math.Max(1, Math.Min(6, (W - 6) / PixFont.TextWidth("TERMINAL", 1)));
            int s2 = Math.Max(1, Math.Min(9, (W - 6) / PixFont.TextWidth("HELL", 1)));
            s2 = Math.Min(s2, Math.Max(1, (H / 2 - 7 * s1 - 4) / 7));
            int y0 = Math.Max(2, H / 14);
            float wob = (float)Math.Sin(time * 2) * 0.1f;
            Hud.BigText(scr, "TERMINAL", y0, s1, Col.Rgb(230, 230, 220), Col.Rgb(130, 120, 110));
            int y1 = y0 + 7 * s1 + Math.Max(2, s1);
            Hud.BigText(scr, "HELL", y1, s2, Col.Lerp(Col.Rgb(255, 230, 120), Col.Rgb(255, 255, 200), wob + 0.1f), Col.Rgb(190, 20, 8));
            int ty = (y1 + 7 * s2) / 2 + 1;
            titleBottom = ty + 1;
            string sub = "A FIRST PERSON SHOOTER FOR YOUR TERMINAL";
            if (ty < scr.Rows) scr.PrintCenter(ty, sub, Col.Rgb(200, 150, 110), Screen.Transparent);
            // once an episode has been finished, its best run stays on the title screen
            for (int e = 0; e < Levels.Episodes.Length; e++)
            {
                if ((S.BestScores[e] <= 0 && S.BestTimes[e] <= 0) || titleBottom >= scr.Rows) continue;
                string best = Levels.Episodes[e].Name + "   ";
                if (S.BestScores[e] > 0) best += "BEST SCORE " + Num(S.BestScores[e]);
                if (S.BestTimes[e] > 0) best += (S.BestScores[e] > 0 ? "     " : "") + "BEST TIME " + FormatTime(S.BestTimes[e]);
                scr.PrintCenter(titleBottom, best, Col.Rgb(255, 210, 120), Screen.Transparent);
                titleBottom++;
            }
            if (S.Beaten[1] && titleBottom < scr.Rows)
            {
                scr.PrintCenter(titleBottom, "THE WARDEN HAS FALLEN ONCE", Col.Rgb(210, 120, 90), Screen.Transparent);
                titleBottom++;
            }
            if (Updater.Available)
            {
                string up = " VERSION " + Updater.Newest + " IS OUT - PRESS U TO UPDATE ";
                if (scr.Cols > up.Length + 2) scr.PrintCenter(Math.Max(0, scr.Rows - 3), up, Col.Rgb(255, 240, 200), Col.Rgb(90, 30, 10));
            }
            string foot = " WASD MOVE  -  MOUSE LOOK  -  CLICK FIRE  -  RIGHT CLICK PARRY  -  SPACE JUMP  -  E USE ";
            if (scr.Cols > foot.Length + 2) scr.PrintCenter(scr.Rows - 1, foot, Col.Rgb(230, 200, 170), Col.Rgb(24, 8, 6));
#if LINUX
            // the game can't resize a Linux terminal itself: point out the zoom keys while the picture is coarse
            string tip = " TIP: MAXIMIZE THIS WINDOW AND ZOOM OUT (CTRL -) FOR MORE DETAIL ";
            if (scr.Cols < 150 && scr.Cols > tip.Length + 2) scr.PrintCenter(scr.Rows - 2, tip, Col.Rgb(255, 210, 120), Col.Rgb(40, 12, 8));
#endif
            scr.Print(0, scr.Rows - 1, " v" + Program.Version + " ", Col.Rgb(170, 140, 120), Col.Rgb(24, 8, 6));
        }

        void DrawPlaying(float dt)
        {
            var p = world.P;
            int hudRows = Hud.Rows(scr.Rows);
            int viewRows = scr.Rows - hudRows;
            int viewH = viewRows * 2;

            // camera with screen shake
            float shake = p.ShakeAmt;
            cam.X = p.X; cam.Y = p.Y;
            cam.Angle = p.Angle + (shake > 0 ? (float)(Math.Sin(animTime * 53) * 0.012 * shake) : 0);
            cam.Pitch = (S.MouseLook ? p.Pitch : p.Pitch) + (shake > 0 ? (float)(Math.Sin(animTime * 71) * 0.02 * shake) : 0);
            cam.EyeZ = p.EyeZ;
            float phys = (scr.W / (float)Math.Max(1, viewH)) * Term.CellAspect;
            double baseHalf = S.Fov * Math.PI / 360;
            double hor = 2 * Math.Atan(Math.Tan(baseHalf) * phys / 1.6);
            cam.Fov = (float)Math.Max(55, Math.Min(118, hor * 180 / Math.PI));
            ren.Aspect = Term.CellAspect;
            ren.Time = world.Time;

            if (automap)
            {
                // the automap covers the whole view, so the full wall/floor/sprite pass would be wasted work:
                // just the camera numbers a shot needs, plus a flat backdrop for the map overlay to darken
                ren.PrepareCameraOnly(scr.W, viewH, cam);
                int bg = Col.Rgb(18, 16, 20);
                for (int i = 0; i < scr.W * viewH; i++) scr.Pix[i] = bg;
            }
            else
            {
                BuildSprites();
                // no scene-wide muzzle flash: brightening everything forces the console to redraw every cell
                ren.Render(scr.Pix, scr.W, viewH, world.Map, cam, sprites, world.Lights, 0);
            }
            // the centre of the screen is where shots go: its slope depends on how far the horizon is sheared
            p.AimSlope = (ren.Horizon - viewH * 0.5f) / ren.ProjY;
            if (!automap) DrawBossLaser(viewH);
            if (!automap) DrawWeapon(viewH, dt);
            Hud.ScreenEffects(scr, world, viewH);
            if (automap) Hud.Automap(scr, world, viewH, automapZoom);
            else if (S.Crosshair && !p.Dead)
            {
                bool target = false;
                if (ren.CenterTag != 0)
                    foreach (var a in world.Actors)
                        if (a.Tag == ren.CenterTag) { var m = a as Monster; target = m != null && m.Alive; break; }
                Hud.Crosshair(scr, scr.W / 2, viewH / 2, target);
            }

            // pixel-art titles
            if (introTime > 0 && !automap)
            {
                float a = Math.Min(1, introTime);
                int sc = Math.Max(1, Math.Min(3, viewH / 40));
                Hud.BigText(scr, world.Def.Id + ": " + world.Def.Name, Math.Max(2, viewH / 6), sc, Col.Rgb(255, 220, 140), Col.Rgb(200, 40, 16), a);
            }
            if (p.Dead)
            {
                int sc = Math.Max(1, Math.Min(5, viewH / 22));
                Hud.BigText(scr, "YOU DIED", viewH / 3, sc, Col.Rgb(255, 60, 40), Col.Rgb(120, 0, 0));
            }

            Hud.DrawMessages(scr, world, viewRows, S.BigPickupText);
            if (p.Dead && p.DeadTime > 1.3f)
                scr.PrintCenter(Math.Min(viewRows - 2, viewRows * 2 / 3), " PRESS FIRE OR ENTER TO TRY AGAIN ", Col.Rgb(255, 220, 200), Col.Rgb(60, 0, 0));
            if (automap)
            {
                scr.PrintCenter(0, " " + world.Def.Id + ": " + world.Def.Name + " ", Col.Rgb(255, 210, 120), Col.Rgb(30, 10, 8));
                string st = " KILLS " + world.Kills + "/" + world.TotalKills + "   ITEMS " + world.ItemsTaken + "/" + world.TotalItems + "   SECRETS " + world.Secrets + "/" + world.TotalSecrets +
                    "   SCORE " + Num(world.Score) + " ";
                scr.PrintCenter(viewRows - 1, st, Col.Rgb(220, 200, 180), Col.Rgb(30, 10, 8));
            }
            var boss = world.BossAwake;
            if (boss != null && boss.Alive)
            {
                int bw = Math.Min(40, scr.Cols - 20);
                int filled = (int)Math.Ceiling(bw * Math.Max(0, boss.Health) / boss.Def.Health);
                int bx = (scr.Cols - bw) / 2;
                int by = world.Messages.Count > 0 ? world.Messages.Count : 1;
                string bossName = "THE " + boss.Def.Name;
                scr.Print(bx - bossName.Length - 1, by, bossName, Col.Rgb(255, 120, 80), Screen.Transparent);
                for (int i = 0; i < bw; i++) scr.Put(bx + i, by, i < filled ? '█' : '░', i < filled ? Col.Rgb(220, 30, 20) : Col.Rgb(80, 30, 26), Col.Rgb(20, 4, 4));
            }
            Hud.DrawStatusBar(scr, world, viewRows, dt);
        }

        /// <summary>The Warden's sight: a red beam from its shoulder to the spot it is about to put a rocket on.
        /// It follows the player, then stops dead for a second - that second is the warning.</summary>
        void DrawBossLaser(int viewH)
        {
            int W = scr.W;
            foreach (var a in world.Actors)
            {
                var mo = a as Monster;
                if (mo == null || !mo.Aiming || !mo.Alive) continue;
                float ax = mo.X, ay = mo.Y, az = mo.Z + mo.Def.Height * 0.62f;
                float bx = mo.AimX, by = mo.AimY, bz = mo.AimZ;
                float len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (len < 0.2f) continue;
                float beat = mo.AimLocked ? 0.5f + 0.5f * (float)Math.Sin(world.Time * 30) : 0.25f;
                int hot = Col.Lerp(Col.Rgb(210, 18, 10), Col.Rgb(255, 190, 160), beat);
                int steps = Math.Max(16, (int)(len * 18));
                for (int i = 0; i <= steps; i++)
                {
                    float t = 0.16f + (1 - 0.16f) * i / steps;     // starts clear of its own body
                    float sx, sy, depth;
                    if (!ren.Project(ax + (bx - ax) * t, ay + (by - ay) * t, az + (bz - az) * t, out sx, out sy, out depth)) continue;
                    int x = (int)sx, y = (int)sy;
                    if (x < 0 || x >= W || y < 0 || y >= viewH || depth >= ren.ZBuf[x]) continue;
                    int i0 = y * W + x;
                    scr.Pix[i0] = Col.Add(Col.Scale(scr.Pix[i0], 0.3f), hot);
                    if (y + 1 < viewH) scr.Pix[i0 + W] = Col.Add(Col.Scale(scr.Pix[i0 + W], 0.75f), Col.Scale(hot, 0.35f));
                }
                // the spot itself, so it is obvious where the rocket is going to land
                float mx, my, md;
                if (!ren.Project(bx, by, bz, out mx, out my, out md) || md >= 40) continue;
                float r = Math.Max(1.5f, 0.28f / md * ren.Proj);
                for (int y = (int)(my - r); y <= (int)(my + r); y++)
                {
                    if (y < 0 || y >= viewH) continue;
                    for (int x = (int)(mx - r); x <= (int)(mx + r); x++)
                    {
                        if (x < 0 || x >= W || md >= ren.ZBuf[x]) continue;
                        float d = (float)Math.Sqrt((x - mx) * (x - mx) + (y - my) * (y - my) * Term.CellAspect * Term.CellAspect);
                        if (d > r || d < r * 0.45f) continue;
                        int i0 = y * W + x;
                        scr.Pix[i0] = Col.Add(Col.Scale(scr.Pix[i0], 0.35f), Col.Scale(hot, 0.85f));
                    }
                }
            }
        }

        void BuildSprites()
        {
            sprites.Clear();
            int n = 0;
            foreach (var a in world.Actors)
            {
                var img = a.Sprite(world);
                if (img == null) continue;
                if (n >= spritePool.Count) spritePool.Add(new SpriteInst());
                var si = spritePool[n++];
                si.X = a.X; si.Y = a.Y; si.Z = a.Z;
                si.Img = img;
                si.Scale = a.Scale;
                si.Flash = a.Flash > 0 ? Math.Min(0.7f, a.Flash) : 0;
                si.FullBright = a.Bright;
                var m = a as Monster;
                si.Tag = m != null && m.Alive ? a.Tag : 0;
                sprites.Add(si);
            }
        }

        void DrawWeapon(int viewH, float dt)
        {
            var p = world.P;
            if (p.Dead) return;
            // the weapon is lit by the room it is in (light map plus nearby fireballs / explosions)
            float lr, lg, lb;
            world.Map.SampleLight(p.X, p.Y, out lr, out lg, out lb);
            float l = Math.Max(0.45f, Math.Min(1.2f, (lr + lg + lb) / 3 * 1.1f));
            foreach (var dl in world.Lights)
            {
                float d2 = (dl.X - p.X) * (dl.X - p.X) + (dl.Y - p.Y) * (dl.Y - p.Y);
                if (d2 < dl.R * dl.R) l += (1 - d2 / (dl.R * dl.R)) * 0.3f;
            }
            ViewModel.Draw(scr, viewH, p, dt, animTime, Term.CellAspect, Math.Min(1.4f, l));
        }

        /// <summary>The frozen last frame of play burning away from the bottom up, into the score screen.</summary>
        void DrawBurn(float k)
        {
            int W = scr.W, H = scr.H;
            int ember = Col.Rgb(255, 238, 180), fireCol = Col.Rgb(225, 70, 12), ash = Col.Rgb(16, 7, 6);
            for (int y = 0; y < H; y++)
            {
                float row = 1 - (float)y / H;             // the floor catches first, the ceiling goes last
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float edge = row * 0.72f + Noise.Fbm(x, y, 13, 4, 3, 21) * 0.22f + Noise.Hashf(x, y, 7) * 0.06f;
                    float burn = k * 1.3f - edge;
                    if (burn <= 0) { scr.Pix[i] = burnPix[i]; continue; }
                    if (burn < 0.18f)
                    {
                        int hot = Col.Lerp(ember, fireCol, burn / 0.18f);
                        scr.Pix[i] = Col.Add(Col.Scale(burnPix[i], 0.35f), hot);
                    }
                    else scr.Pix[i] = Col.Lerp(ash, Col.Rgb(10, 3, 3), Math.Min(1, (burn - 0.18f) * 3));
                }
            }
            // the fire grows into exactly the band the score screen uses, so nothing jumps when the burn ends
            fire.Draw(scr, H - (int)(H * (0.16f + (EndFireHeight - 0.16f) * k)), H, EndFireStrength * Math.Min(1, k * 1.6f));
        }

        // where the fire sits on the screens after a level: the burn transition grows into the same band
        const float EndFireHeight = 1f / 3f, EndFireStrength = 0.6f;

        void DrawIntermission()
        {
            if (stateTime < 0 && burnW == scr.W && burnH == scr.H && burnPix.Length == scr.Pix.Length)
            {
                DrawBurn(1 + stateTime / BurnTime);
                return;
            }
            int W = scr.W, H = scr.H;
            for (int i = 0; i < W * H; i++) scr.Pix[i] = Col.Rgb(10, 3, 3);
            fire.Draw(scr, H - (int)(H * EndFireHeight), H, EndFireStrength);
            var def = levels[levelIndex];
            int sc = Math.Max(1, Math.Min(4, W / 70));
            Hud.BigText(scr, def.Id + " COMPLETE", 3, sc, Col.Rgb(255, 230, 150), Col.Rgb(200, 40, 16));
            int row = Math.Max(3, (3 + 7 * sc) / 2 + 2);
            float t = stateTime;
            string[] labels = { "KILLS", "ITEMS", "SECRETS" };
            int[] vals = { statKills, statItems, statSecrets };
            for (int i = 0; i < 3; i++)
            {
                float k = Math.Min(1, Math.Max(0, (t - 0.3f - i * 0.5f) / 0.5f));
                scr.PrintCenter(row + i * 2, StatLine(labels[i], (int)(vals[i] * k) + "%"), Col.Rgb(240, 220, 200), Screen.Transparent);
            }
            // the score counts up last, after the three percentages
            float sk = Math.Min(1, Math.Max(0, (t - 0.3f - 1.5f) / 0.6f));
            scr.PrintCenter(row + 6, StatLine("SCORE", Num((int)(statScore * sk))), Col.Rgb(255, 220, 120), Screen.Transparent);
            if (t > 2.3f)
            {
                scr.PrintCenter(row + 8, StatLine("TIME", FormatTime(statTime)), Col.Rgb(240, 220, 200), Screen.Transparent);
                scr.PrintCenter(row + 9, StatLine("PAR", def.Par), Col.Rgb(170, 150, 130), Screen.Transparent);
            }
            if (t > 2.8f)
            {
                int nl = NextLevel();
                int nextEpIdx = levels[levelIndex].Episode + 1;
                string next = nl >= 0 ? "NEXT: " + levels[nl].Id + " - " + levels[nl].Name
                    : nextEpIdx < Levels.Episodes.Length ? "NEXT: EPISODE " + (nextEpIdx + 1) + " - " + Levels.Episodes[nextEpIdx].Name
                    : "THE WAY OUT IS OPEN...";
                scr.PrintCenter(row + 12, next, Col.Rgb(255, 170, 80), Screen.Transparent);
                if (((int)(time * 2) & 1) == 0) scr.PrintCenter(Math.Min(scr.Rows - 2, row + 14), "PRESS ENTER TO CONTINUE", Col.Rgb(255, 240, 220), Screen.Transparent);
            }
        }

        static string FormatTime(float s)
        {
            int t = (int)s;
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        /// <summary>A score with thousands separators: 24500 -> "24,500".</summary>
        static string Num(int n)
        {
            return n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>One intermission line: "KILLS........   100%" (all of them the same width, so the dots line up).</summary>
        static string StatLine(string label, string value)
        {
            return label.PadRight(13, '.') + value.PadLeft(7);
        }

        void DrawVictory()
        {
            int W = scr.W, H = scr.H;
            for (int y = 0; y < H; y++)
            {
                int c = Col.Lerp(Col.Rgb(2, 2, 8), Col.Rgb(50, 10, 6), (float)y / H);
                for (int x = 0; x < W; x++) scr.Pix[y * W + x] = c;
            }
            fire.Dying = true;
            fire.Draw(scr, H / 2, H, 0.8f);
            int sc = Math.Max(1, Math.Min(6, W / 50));
            var episode = Levels.Episodes[Math.Max(0, Math.Min(Levels.Episodes.Length - 1, levels[levelIndex].Episode))];
            sc = Math.Max(1, Math.Min(sc, (W - 6) / PixFont.TextWidth(episode.EndTitle, 1)));
            Hud.BigText(scr, episode.EndTitle, 3, sc, Col.Rgb(255, 240, 170), Col.Rgb(210, 60, 20));
            var story = new List<string>(episode.Ending);
            story.Add("");
            story.Add("KILLS " + totalKills + "/" + totalKillsMax + "    SECRETS " + totalSecrets + "/" + totalSecretsMax + "    TIME " + FormatTime(totalTime));
            story.Add("TOTAL SCORE  " + Num(totalScore));
            story.Add("");
            if (levels[levelIndex].Episode == Levels.Episodes.Length - 1) story.Add("THANK YOU FOR PLAYING TERMINAL HELL");
            int row = (3 + 7 * sc) / 2 + 2;
            int shown = (int)(stateTime * 3);
            for (int i = 0; i < story.Count && i < shown; i++)
            {
                bool gold = i == story.Count - 1 || story[i].StartsWith("TOTAL SCORE");
                scr.PrintCenter(row + i, story[i], gold ? Col.Rgb(255, 200, 90) : Col.Rgb(230, 210, 190), Screen.Transparent);
            }
            if (stateTime > 4 && ((int)(time * 2) & 1) == 0)
                scr.PrintCenter(Math.Min(scr.Rows - 1, row + story.Count + 2), "PRESS ENTER", Col.Rgb(255, 240, 220), Screen.Transparent);
        }
    }
}
