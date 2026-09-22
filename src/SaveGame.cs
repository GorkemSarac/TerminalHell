// TERMINAL HELL - the single save slot: a plain text file holding the run and everything in the level.
// Saving writes where the player is, what they carry, and the state of every monster, item, door and secret.
// Loading rebuilds the level from the map and then puts all of that back.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TerminalHell
{
    /// <summary>The run itself: which level, and the totals carried between levels.</summary>
    sealed class SaveHeader
    {
        public int Level;
        public int TotalKills, TotalKillsMax, TotalSecrets, TotalSecretsMax, TotalScore;
        public float TotalTime;
        public int Difficulty;
        public string When = "";

        /// <summary>One line for the menu: "E1M2   SCORE 12,450   19 SEP 21:40".</summary>
        public string Describe(LevelDef[] levels)
        {
            string id = Level >= 0 && Level < levels.Length ? levels[Level].Id : "E1M?";
            return id + "   SCORE " + TotalScore.ToString("N0", CultureInfo.InvariantCulture) + (When.Length > 0 ? "   " + When : "");
        }
    }

    static class SaveGame
    {
        const string Magic = "terminalhell-save-1";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Set by the save test so that it never touches the player's own save.</summary>
        public static string PathOverride;

        public static string Path
        {
            get { return PathOverride != null ? PathOverride : System.IO.Path.Combine(Settings.Dir, "save1.txt"); }
        }

        public static bool Exists
        {
            get { try { return File.Exists(Path); } catch { return false; } }
        }

        public static void Delete()
        {
            try { if (File.Exists(Path)) File.Delete(Path); } catch { }
        }

        /// <summary>Reads just the header, for the menu. Null when there is no readable save.</summary>
        public static SaveHeader ReadHeader()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                var h = new SaveHeader();
                bool magic = false;
                int byId = -1, legacy = -1;
                foreach (var line in File.ReadAllLines(Path))
                {
                    var f = line.Split(' ');
                    if (f[0] == Magic) { magic = true; continue; }
                    if (f[0] == "levelid") byId = Levels.IndexOf(f.Length > 1 ? f[1] : "");
                    else if (f[0] == "level") legacy = Int(f, 1);
                    else if (f[0] == "totals")
                    {
                        h.TotalKills = Int(f, 1); h.TotalKillsMax = Int(f, 2);
                        h.TotalSecrets = Int(f, 3); h.TotalSecretsMax = Int(f, 4);
                        h.TotalScore = Int(f, 5); h.TotalTime = Flt(f, 6);
                        h.Difficulty = Int(f, 7);
                    }
                    else if (f[0] == "when") h.When = line.Substring(5);
                    else if (f[0] == "player") break;
                }
                // saves from before there were two episodes only had a number, and those levels are now the hell episode
                h.Level = byId >= 0 ? byId : legacy >= 0 ? Levels.FirstOf(1) + legacy : -1;
                return magic && h.Level >= 0 ? h : null;
            }
            catch { return null; }
        }

        // ================================================================ saving

        public static bool Save(SaveHeader h, World w)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(Magic);
                sb.AppendLine("when " + DateTime.Now.ToString("dd MMM HH:mm", Inv).ToUpperInvariant());
                sb.AppendLine("levelid " + w.Def.Id);
                sb.AppendLine(string.Join(" ", new[] { "totals", S(h.TotalKills), S(h.TotalKillsMax), S(h.TotalSecrets), S(h.TotalSecretsMax),
                    S(h.TotalScore), F(h.TotalTime), S(h.Difficulty) }));
                sb.AppendLine(string.Join(" ", new[] { "world", S(w.Score), S(w.Kills), S(w.TotalKills), S(w.ItemsTaken), S(w.TotalItems),
                    S(w.Secrets), S(w.TotalSecrets), F(w.LevelTime), S(w.BossDead ? 1 : 0), S(w.ExitTriggered ? 1 : 0) }));

                var p = w.P;
                sb.AppendLine(string.Join(" ", new[] { "player", F(p.X), F(p.Y), F(p.Z), F(p.Angle), F(p.Pitch), S(p.HP), S(p.Armor), S(p.ArmorType), S(p.Weapon) }));
                var ammo = new List<string>(); ammo.Add("ammo");
                for (int i = 0; i < Player.AmmoTypes; i++) ammo.Add(S(p.Ammo[i]));
                sb.AppendLine(string.Join(" ", ammo.ToArray()));
                var has = new List<string>(); has.Add("has");
                for (int i = 0; i < Player.Weapons; i++) has.Add(p.Has[i] ? "1" : "0");
                sb.AppendLine(string.Join(" ", has.ToArray()));
                var keys = new List<string>(); keys.Add("keys");
                for (int i = 0; i < p.Keys.Length; i++) keys.Add(p.Keys[i] ? "1" : "0");
                sb.AppendLine(string.Join(" ", keys.ToArray()));
                sb.AppendLine("incident " + w.Incident);

                for (int i = 0; i < w.Map.Doors.Count; i++)
                {
                    var d = w.Map.Doors[i];
                    sb.AppendLine(string.Join(" ", new[] { "door", S(i), S((int)d.State), F(d.Open), F(d.Timer) }));
                }
                for (int i = 0; i < w.Map.PushWalls.Count; i++)
                {
                    var pw = w.Map.PushWalls[i];
                    sb.AppendLine(string.Join(" ", new[] { "push", S(i), S(pw.X), S(pw.Y), S(pw.DX), S(pw.DY), F(pw.Offset), S(pw.Moved),
                        S(pw.Active ? 1 : 0), S(pw.Done ? 1 : 0), S(pw.Counted ? 1 : 0) }));
                }

                foreach (var a in w.Actors)
                {
                    if (a.Remove) continue;
                    var m = a as Monster;
                    if (m != null)
                    {
                        sb.AppendLine(string.Join(" ", new[] { "monster", m.Def.Code.ToString(), F(m.X), F(m.Y), F(m.Angle), F(m.Health), S((int)m.State),
                            m.Carries == '\0' ? "0" : m.Carries.ToString() }));
                        continue;
                    }
                    var it = a as Item;
                    if (it != null)
                    {
                        sb.AppendLine(string.Join(" ", new[] { "item", it.Code.ToString(), F(it.X), F(it.Y), S(it.Dropped ? 1 : 0), F(it.Z) }));
                        continue;
                    }
                    var b = a as Barrel;
                    if (b != null) sb.AppendLine(string.Join(" ", new[] { "barrel", F(b.X), F(b.Y), F(b.Health) }));
                }

                Directory.CreateDirectory(Settings.Dir);
                File.WriteAllText(Path, sb.ToString());
                return true;
            }
            catch { return false; }
        }

        // ================================================================ loading

        /// <summary>Rebuilds the saved level. Returns null if the save can't be read.</summary>
        public static World Load(SaveHeader h, LevelDef[] levels, Settings settings)
        {
            try
            {
                var lines = File.ReadAllLines(Path);
                if (lines.Length == 0 || lines[0].Trim() != Magic) return null;
                if (h.Level < 0 || h.Level >= levels.Length) return null;

                var w = new World(levels[h.Level], settings, null);
                // everything that can move, be picked up or blown up is restored from the file instead
                w.Actors.RemoveAll(a => a.Kind == ActorKind.Monster || a.Kind == ActorKind.Item || a.Kind == ActorKind.Barrel);
                w.TotalKills = 0; w.TotalItems = 0;

                var p = w.P;
                foreach (var line in lines)
                {
                    var f = line.Split(' ');
                    switch (f[0])
                    {
                        case "world":
                            w.Score = Int(f, 1); w.Kills = Int(f, 2); w.TotalKills = Int(f, 3); w.ItemsTaken = Int(f, 4);
                            w.TotalItems = Int(f, 5); w.Secrets = Int(f, 6); w.TotalSecrets = Int(f, 7); w.LevelTime = Flt(f, 8);
                            w.BossDead = Int(f, 9) != 0; w.ExitTriggered = Int(f, 10) != 0;
                            break;
                        case "player":
                            p.X = Flt(f, 1); p.Y = Flt(f, 2); p.Z = Flt(f, 3); p.Angle = Flt(f, 4); p.Pitch = Flt(f, 5);
                            p.HP = Int(f, 6); p.Armor = Int(f, 7); p.ArmorType = Int(f, 8); p.Weapon = Int(f, 9);
                            p.OnGround = p.Z <= 0;
                            break;
                        case "ammo":
                            for (int i = 0; i < Player.AmmoTypes && i + 1 < f.Length; i++) p.Ammo[i] = Int(f, i + 1);
                            break;
                        case "has":
                            for (int i = 0; i < Player.Weapons && i + 1 < f.Length; i++) p.Has[i] = Int(f, i + 1) != 0;
                            break;
                        case "keys":
                            for (int i = 0; i < p.Keys.Length && i + 1 < f.Length; i++) p.Keys[i] = Int(f, i + 1) != 0;
                            break;
                        case "incident":
                            w.Incident = Int(f, 1) != 0 ? 2 : 0;      // one that was still going on counts as over
                            break;
                        case "door":
                            {
                                int i = Int(f, 1);
                                if (i < 0 || i >= w.Map.Doors.Count) break;
                                var d = w.Map.Doors[i];
                                d.State = (DoorState)Int(f, 2);
                                d.Open = Flt(f, 3);
                                d.Timer = Flt(f, 4);
                                break;
                            }
                        case "push":
                            RestorePushWall(w, f);
                            break;
                        case "monster":
                            {
                                var def = MonsterDef.For(f[1][0]);
                                if (def == null) break;
                                var m = new Monster(def, Flt(f, 2), Flt(f, 3));
                                m.Angle = Flt(f, 4);
                                m.Health = Flt(f, 5);
                                m.State = (MState)Int(f, 6);
                                if (f.Length > 7 && f[7] != "0" && f[7].Length > 0) m.Carries = f[7][0];
                                if (!m.Alive) { m.Solid = false; m.Shootable = false; m.State = MState.Dead; }
                                if (def.Boss && m.Alive && m.Health < def.Health) w.BossAwake = m;
                                w.Actors.Add(m);
                                break;
                            }
                        case "item":
                            {
                                var it = new Item(f[1][0], Flt(f, 2), Flt(f, 3));
                                it.Dropped = Int(f, 4) != 0;
                                // the pedestals came back with the level: put the weapon back on top of its own
                                it.Z = f.Length > 5 ? Flt(f, 5) : (World.OnPedestal(it.Code, it.Dropped) ? World.PedestalTop : 0);
                                w.Actors.Add(it);
                                break;
                            }
                        case "barrel":
                            {
                                var b = new Barrel(Flt(f, 1), Flt(f, 2));
                                b.Health = Flt(f, 3);
                                w.Actors.Add(b);
                                break;
                            }
                    }
                }
                w.Map.BuildFlow((int)p.X, (int)p.Y);
                return w;
            }
            catch { return null; }
        }

        /// <summary>Puts a secret wall back where it had slid to, clearing the cells it came from.</summary>
        static void RestorePushWall(World w, string[] f)
        {
            int i = Int(f, 1);
            if (i < 0 || i >= w.Map.PushWalls.Count) return;
            var pw = w.Map.PushWalls[i];
            int toX = Int(f, 2), toY = Int(f, 3);
            pw.DX = Int(f, 4); pw.DY = Int(f, 5);
            pw.Offset = Flt(f, 6);
            pw.Moved = Int(f, 7);
            pw.Active = Int(f, 8) != 0;
            pw.Done = Int(f, 9) != 0;
            pw.Counted = Int(f, 10) != 0;
            var map = w.Map;
            // the cells it has already left
            int cx = pw.X, cy = pw.Y;
            for (int step = 0; step < pw.Moved && map.In(cx, cy); step++)
            {
                int ci = cy * map.W + cx;
                map.Kind[ci] = CellKind.Empty;
                map.WallTex[ci] = 0;
                map.DoorIdx[ci] = -1;
                cx += pw.DX; cy += pw.DY;
            }
            pw.X = toX; pw.Y = toY;
            Occupy(map, toX, toY, pw.Texture, i);
            // while it is still sliding it fills the cell ahead of it as well
            if (pw.Active) Occupy(map, toX + pw.DX, toY + pw.DY, pw.Texture, i);
        }

        static void Occupy(Map map, int x, int y, int texture, int index)
        {
            if (!map.In(x, y)) return;
            int i = y * map.W + x;
            map.Kind[i] = CellKind.Push;
            map.WallTex[i] = (byte)texture;
            map.DoorIdx[i] = (short)index;
        }

        // ================================================================ helpers

        static string S(int v) { return v.ToString(Inv); }
        static string F(float v) { return v.ToString("0.###", Inv); }

        static int Int(string[] f, int i)
        {
            int v;
            return i < f.Length && int.TryParse(f[i], NumberStyles.Integer, Inv, out v) ? v : 0;
        }

        static float Flt(string[] f, int i)
        {
            float v;
            return i < f.Length && float.TryParse(f[i], NumberStyles.Float, Inv, out v) ? v : 0;
        }
    }
}
