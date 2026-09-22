// TERMINAL HELL - persistent user settings (%LOCALAPPDATA%\TerminalHell\settings.cfg).
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace TerminalHell
{
    enum DisplayMode { HD = 0, Ascii = 1, Legacy = 2 }

    sealed class Settings
    {
        public DisplayMode Display = DisplayMode.HD;
        public int Resolution = 1;          // classic console: 0 low, 1 medium, 2 high (window sizes), 3 full screen
        public bool CrispFont = true;       // classic console: bitmap "Terminal" font (pixel exact) instead of Consolas
        public float MouseSens = 1.0f;
        public bool InvertY;
        public bool MouseLook = true;       // vertical look with the mouse
        public bool Crosshair = true;
        public bool HeadBob = true;
        public int Fov = 74;
        public int SfxVolume = 2;           // 0..10
        public int MusicVolume = 2;         // 0..10
        public bool ShowFps;
        public int Difficulty = 1;          // 0 easy, 1 normal, 2 hard
        public bool PixelDouble;            // render at half resolution (faster terminals)
        public bool BigPickupText = true;   // pickup messages in the game's pixel font instead of a plain text line
        public const int Episodes = 2;
        // records, one set per episode (0 earth, 1 hell): the best score and the fastest run are tracked separately
        public readonly int[] BestScores = new int[Episodes];
        public readonly float[] BestTimes = new float[Episodes];
        public readonly bool[] Beaten = new bool[Episodes];     // the episode has been finished at least once
        public bool UpdateCheck = true;     // ask GitHub whether a newer version has been published

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TerminalHell"); }
        }

        static string FilePath { get { return Path.Combine(Dir, "settings.cfg"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "display": s.Display = (DisplayMode)Clamp(Int(v, 0), 0, 2); break;
                        case "resolution": s.Resolution = Clamp(Int(v, 1), 0, 3); break;
                        case "crispfont": s.CrispFont = Bool(v, true); break;
                        case "mousesens": s.MouseSens = Math.Max(0.1f, Math.Min(5f, Float(v, 1))); break;
                        case "inverty": s.InvertY = Bool(v, false); break;
                        case "mouselook": s.MouseLook = Bool(v, true); break;
                        case "crosshair": s.Crosshair = Bool(v, true); break;
                        case "headbob": s.HeadBob = Bool(v, true); break;
                        case "fov": s.Fov = Clamp(Int(v, 74), 60, 100); break;
                        case "sfxvolume": s.SfxVolume = Clamp(Int(v, 2), 0, 10); break;
                        case "musicvolume": s.MusicVolume = Clamp(Int(v, 2), 0, 10); break;
                        case "showfps": s.ShowFps = Bool(v, false); break;
                        case "difficulty": s.Difficulty = Clamp(Int(v, 1), 0, 2); break;
                        case "pixeldouble": s.PixelDouble = Bool(v, false); break;
                        case "bigpickuptext": s.BigPickupText = Bool(v, true); break;
                        // the keys without a number are from before there were two episodes: those runs were in hell
                        case "bestscore": case "bestscore1": s.BestScores[1] = Math.Max(0, Int(v, 0)); break;
                        case "besttime": case "besttime1": s.BestTimes[1] = Math.Max(0, Float(v, 0)); break;
                        case "beaten": case "beaten1": s.Beaten[1] = Bool(v, false); break;
                        case "bestscore0": s.BestScores[0] = Math.Max(0, Int(v, 0)); break;
                        case "besttime0": s.BestTimes[0] = Math.Max(0, Float(v, 0)); break;
                        case "beaten0": s.Beaten[0] = Bool(v, false); break;
                        case "updatecheck": s.UpdateCheck = Bool(v, true); break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("# TERMINAL HELL settings");
                sb.AppendLine("display=" + (int)Display);
                sb.AppendLine("resolution=" + Resolution);
                sb.AppendLine("crispfont=" + CrispFont);
                sb.AppendLine("mousesens=" + MouseSens.ToString("0.00", CultureInfo.InvariantCulture));
                sb.AppendLine("inverty=" + InvertY);
                sb.AppendLine("mouselook=" + MouseLook);
                sb.AppendLine("crosshair=" + Crosshair);
                sb.AppendLine("headbob=" + HeadBob);
                sb.AppendLine("fov=" + Fov);
                sb.AppendLine("sfxvolume=" + SfxVolume);
                sb.AppendLine("musicvolume=" + MusicVolume);
                sb.AppendLine("showfps=" + ShowFps);
                sb.AppendLine("difficulty=" + Difficulty);
                sb.AppendLine("pixeldouble=" + PixelDouble);
                sb.AppendLine("bigpickuptext=" + BigPickupText);
                for (int e = 0; e < Episodes; e++)
                {
                    sb.AppendLine("bestscore" + e + "=" + BestScores[e]);
                    sb.AppendLine("besttime" + e + "=" + BestTimes[e].ToString("0.0", CultureInfo.InvariantCulture));
                    sb.AppendLine("beaten" + e + "=" + Beaten[e]);
                }
                sb.AppendLine("updatecheck=" + UpdateCheck);
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { }
        }

        /// <summary>Puts every preference back to what a fresh install uses. The records and the save slot are
        /// not preferences, so they are left alone (SAVE RESET clears those).</summary>
        public void ResetToDefaults()
        {
            Copy(new Settings());
            Save();
        }

        /// <summary>Wipes the records: the best score, the fastest run, and having finished the episode.</summary>
        public void ClearProgress()
        {
            for (int e = 0; e < Episodes; e++) { BestScores[e] = 0; BestTimes[e] = 0; Beaten[e] = false; }
            Save();
        }

        void Copy(Settings s)
        {
            Display = s.Display; Resolution = s.Resolution; CrispFont = s.CrispFont;
            MouseSens = s.MouseSens; InvertY = s.InvertY; MouseLook = s.MouseLook;
            Crosshair = s.Crosshair; HeadBob = s.HeadBob; Fov = s.Fov;
            SfxVolume = s.SfxVolume; MusicVolume = s.MusicVolume; ShowFps = s.ShowFps;
            Difficulty = s.Difficulty; PixelDouble = s.PixelDouble; BigPickupText = s.BigPickupText;
            UpdateCheck = s.UpdateCheck;
        }

        static int Int(string v, int def) { int r; return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : def; }
        static float Float(string v, float def) { float r; return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : def; }
        static bool Bool(string v, bool def) { bool r; return bool.TryParse(v, out r) ? r : def; }
        static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
    }
}
