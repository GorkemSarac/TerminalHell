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
        public int SfxVolume = 8;           // 0..10
        public int MusicVolume = 5;         // 0..10
        public bool ShowFps;
        public int Difficulty = 1;          // 0 easy, 1 normal, 2 hard
        public bool PixelDouble;            // render at half resolution (faster terminals)

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
                        case "sfxvolume": s.SfxVolume = Clamp(Int(v, 8), 0, 10); break;
                        case "musicvolume": s.MusicVolume = Clamp(Int(v, 5), 0, 10); break;
                        case "showfps": s.ShowFps = Bool(v, false); break;
                        case "difficulty": s.Difficulty = Clamp(Int(v, 1), 0, 2); break;
                        case "pixeldouble": s.PixelDouble = Bool(v, false); break;
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
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { }
        }

        static int Int(string v, int def) { int r; return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : def; }
        static float Float(string v, float def) { float r; return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : def; }
        static bool Bool(string v, bool def) { bool r; return bool.TryParse(v, out r) ? r : def; }
        static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
    }
}
