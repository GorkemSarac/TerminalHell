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
        float time, stateTime, introTime;
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
                        if (presentAvg > 13 && VtPresenter.Tolerance < 15) { VtPresenter.Tolerance++; presentAvg = 10.5; }
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

        /// <summary>Developer aid: renders one in-game frame (with HUD) to a PNG without a console.</summary>
        public void DebugFrame(int cols, int rows, int level, float x, float y, float angleDeg, float simSeconds, string outPath, bool ascii, int weapon, float pitch)
        {
            scr.Resize(cols, rows);
            NewGame(level);
            world.P.X = x; world.P.Y = y; world.P.Angle = (float)(angleDeg * Math.PI / 180);
            world.P.Pitch = pitch;
            if (weapon >= 0) { for (int i = 0; i < Player.Weapons; i++) world.P.Has[i] = true; world.P.Weapon = weapon; }
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
            if (which == "title")
            {
                S.BestScore = totalScore; S.BestTime = totalTime;
                state = GState.Title;
                menus.Clear();
                menus.Add(MainMenu());
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
            Music.Play(levels[i].Music);
            world.Message(levels[i].Intro, Col.Rgb(255, 190, 110));
#if LINUX
            foreach (var note in Input.TakeNotices())
            {
                world.Message(note, Col.Rgb(255, 210, 120));
                world.Messages[world.Messages.Count - 1].Time = 9f;   // what this terminal can't do: worth reading once
            }
#endif
            Input.ClearKeys();
        }

        void RestartLevel()
        {
            world = new World(levels[levelIndex], S, carry);
            state = GState.Playing;
            introTime = 2.5f;
            menus.Clear();
            Input.ClearKeys();
        }

        void FinishLevel()
        {
            state = GState.Intermission;
            stateTime = 0;
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
            m.Add("NEW GAME", () => menus.Add(DifficultyMenu()));
            var saved = SaveGame.ReadHeader();
            if (saved != null) m.Add("CONTINUE  " + saved.Describe(levels), () => LoadSaved());
            m.Add("OPTIONS", () => menus.Add(OptionsMenu()));
            m.Add("CONTROLS", () => menus.Add(ControlsMenu()));
            m.Add("QUIT", () => quit = true);
            m.OnBack = () => { };
            return m;
        }

        Menu DifficultyMenu()
        {
            var m = new Menu("CHOOSE YOUR FATE");
            m.Add("EASY   - ROOKIE", () => { S.Difficulty = 0; S.Save(); NewGame(0); });
            m.Add("NORMAL - SOLDIER", () => { S.Difficulty = 1; S.Save(); NewGame(0); });
            m.Add("HARD   - BERSERKER", () => { S.Difficulty = 2; S.Save(); NewGame(0); });
            m.Sel = S.Difficulty;
            return m;
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
            m.AddValue("SHOW FPS", () => OnOff(S.ShowFps), d => S.ShowFps = !S.ShowFps);
            m.Items.Add(new MenuItem { Text = "", Spacer = true });
            m.Add("BACK", () => CloseTopMenu());
            m.OnBack = () => S.Save();
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
        void SaveNow()
        {
            if (world == null) return;
            var h = new SaveHeader
            {
                Level = levelIndex,
                TotalKills = totalKills, TotalKillsMax = totalKillsMax,
                TotalSecrets = totalSecrets, TotalSecretsMax = totalSecretsMax,
                TotalScore = totalScore, TotalTime = totalTime,
                Difficulty = S.Difficulty,
            };
            bool ok = SaveGame.Save(h, world);
            menus.Clear();
            Input.ClearKeys();
            world.Message(ok ? "GAME SAVED." : "COULD NOT WRITE THE SAVE FILE.", ok ? Col.Rgb(140, 255, 140) : Col.Rgb(255, 120, 60));
        }

        /// <summary>Picks the saved game back up, exactly where it was left.</summary>
        void LoadSaved()
        {
            var h = SaveGame.ReadHeader();
            if (h == null) return;
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
            Music.Play(levels[levelIndex].Music);
            Input.ClearKeys();
            world.Message("GAME LOADED - " + levels[levelIndex].Id + ": " + levels[levelIndex].Name, Col.Rgb(140, 255, 140));
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
            fire.Update(dt);

            if (Input.Hit(Input.VK_F5))
            {
                S.Display = S.Display == DisplayMode.HD ? DisplayMode.Ascii : DisplayMode.HD;
                S.Save();
                string[] names = { "HD PIXELS", "ASCII", "16 COLORS" };
                if (world != null) world.Message("DISPLAY: " + names[(int)S.Display], -1);
            }
            if (Input.Hit(Input.VK_F12)) Screenshot();

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
                        else if (levelIndex + 1 < levels.Length) StartLevelAt(levelIndex + 1);
                        else
                        {
                            state = GState.Victory;
                            stateTime = 0;
                            Music.Play(0);
                            // the episode is finished: keep the run if it beats the best one so far
                            if (totalScore > S.BestScore)
                            {
                                S.BestScore = totalScore;
                                S.BestTime = totalTime;
                                S.Save();
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
            for (int i = 0; i < Player.Weapons; i++) if (Input.Hit('1' + i)) inp.SelectSlot = i + 1;
            if (Input.Wheel != 0) inp.Cycle = Input.Wheel > 0 ? -1 : 1;
            if (Input.Hit('Q') && world.P.Has[world.P.LastWeapon]) inp.SelectSlot = world.P.LastWeapon + 1;

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
            // once the episode has been finished, the best run stays on the title screen
            if (S.BestScore > 0 && ty + 1 < scr.Rows)
            {
                scr.PrintCenter(ty + 1, "BEST SCORE " + Num(S.BestScore) + "   IN " + FormatTime(S.BestTime), Col.Rgb(255, 210, 120), Screen.Transparent);
                titleBottom = ty + 2;
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
            cam.Angle = p.Angle + (shake > 0 ? (float)(Math.Sin(time * 53) * 0.012 * shake) : 0);
            cam.Pitch = (S.MouseLook ? p.Pitch : p.Pitch) + (shake > 0 ? (float)(Math.Sin(time * 71) * 0.02 * shake) : 0);
            cam.EyeZ = p.EyeZ;
            float phys = (scr.W / (float)Math.Max(1, viewH)) * Term.CellAspect;
            double baseHalf = S.Fov * Math.PI / 360;
            double hor = 2 * Math.Atan(Math.Tan(baseHalf) * phys / 1.6);
            cam.Fov = (float)Math.Max(55, Math.Min(118, hor * 180 / Math.PI));
            ren.Aspect = Term.CellAspect;
            ren.Time = world.Time;

            BuildSprites();
            // no scene-wide muzzle flash: brightening everything forces the console to redraw every cell
            ren.Render(scr.Pix, scr.W, viewH, world.Map, cam, sprites, world.Lights, 0);
            // the centre of the screen is where shots go: its slope depends on how far the horizon is sheared
            p.AimSlope = (ren.Horizon - viewH * 0.5f) / ren.ProjY;
            if (!automap) DrawWeapon(viewH, dt);
            Hud.ScreenEffects(scr, world, viewH);
            if (automap) Hud.Automap(scr, world, viewH);
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

            Hud.DrawMessages(scr, world, viewRows);
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
                scr.Print(bx - 11, by, "THE WARDEN", Col.Rgb(255, 120, 80), Screen.Transparent);
                for (int i = 0; i < bw; i++) scr.Put(bx + i, by, i < filled ? '█' : '░', i < filled ? Col.Rgb(220, 30, 20) : Col.Rgb(80, 30, 26), Col.Rgb(20, 4, 4));
            }
            Hud.DrawStatusBar(scr, world, viewRows, dt);
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
            ViewModel.Draw(scr, viewH, p, dt, time, Term.CellAspect, Math.Min(1.4f, l));
        }

        void DrawIntermission()
        {
            int W = scr.W, H = scr.H;
            for (int i = 0; i < W * H; i++) scr.Pix[i] = Col.Rgb(10, 3, 3);
            fire.Draw(scr, H * 2 / 3, H, 0.6f);
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
                string next = levelIndex + 1 < levels.Length ? "NEXT: " + levels[levelIndex + 1].Id + " - " + levels[levelIndex + 1].Name : "THE WAY OUT IS OPEN...";
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
            Hud.BigText(scr, "VICTORY", 3, sc, Col.Rgb(255, 240, 170), Col.Rgb(210, 60, 20));
            string[] story =
            {
                "THE WARDEN IS DEAD, THE HELL GATE COLLAPSES INTO ASH BEHIND YOU.",
                "",
                "YOU CRAWL BACK THROUGH THE REFINERY, PAST THE OUTPOST,",
                "INTO A GREY DAWN THAT SMELLS OF SMOKE AND SULPHUR.",
                "",
                "BUT AN EVIL STILL LINGERS IN THE DEEP BENEATH, NOW FREE...",
                "",
                "KILLS " + totalKills + "/" + totalKillsMax + "    SECRETS " + totalSecrets + "/" + totalSecretsMax + "    TIME " + FormatTime(totalTime),
                "TOTAL SCORE  " + Num(totalScore),
                "",
                "THANK YOU FOR PLAYING TERMINAL HELL",
            };
            int row = (3 + 7 * sc) / 2 + 2;
            int shown = (int)(stateTime * 3);
            for (int i = 0; i < story.Length && i < shown; i++)
            {
                bool gold = i == story.Length - 1 || story[i].StartsWith("TOTAL SCORE");
                scr.PrintCenter(row + i, story[i], gold ? Col.Rgb(255, 200, 90) : Col.Rgb(230, 210, 190), Screen.Transparent);
            }
            if (stateTime > 4 && ((int)(time * 2) & 1) == 0)
                scr.PrintCenter(Math.Min(scr.Rows - 1, row + story.Length + 2), "PRESS ENTER", Col.Rgb(255, 240, 220), Screen.Transparent);
        }
    }
}
