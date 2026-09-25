// TERMINAL HELL - the running level: actors, collision, combat, doors, secrets and pickups.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    sealed class GameMessage
    {
        public string Text;
        public int Color;
        public float Time;
    }

    sealed class World
    {
        public Map Map;
        public Player P;
        public Settings Settings;
        public List<Actor> Actors = new List<Actor>();
        readonly List<Actor> pending = new List<Actor>();
        public Random Rng = new Random();
        public float Time, LevelTime;
        public int Kills, TotalKills, ItemsTaken, TotalItems, Secrets, TotalSecrets;
        public int Score;           // points for this level: monsters killed, things picked up, secrets found
        public const int SecretScore = 1000;
        public float DamageMul = 1, AmmoMul = 1, Aggression = 1, ProjSpeedMul = 1;
        public List<DynLight> Lights = new List<DynLight>();
        readonly List<DynLight> lightPool = new List<DynLight>();
        public Monster BossAwake;
        public bool BossDead;
        public bool ExitTriggered, LevelDone;
        float exitTimer;
        public readonly List<GameMessage> Messages = new List<GameMessage>();
        public string Hint;
        public int HintColor;
        float flowTimer, noiseTimer;
        public LevelDef Def;

        public World(LevelDef def, Settings s, Player carry)
        {
            Def = def;
            Settings = s;
            switch (s.Difficulty)
            {
                case 0: DamageMul = 0.5f; AmmoMul = 1.5f; Aggression = 1.35f; ProjSpeedMul = 0.85f; break;
                case 2: DamageMul = 1.35f; AmmoMul = 1f; Aggression = 0.7f; ProjSpeedMul = 1.2f; break;
            }
            Map = Map.Load(def);
            P = new Player();
            if (carry != null) P.CopyInventoryFrom(carry);
            foreach (var sp in Map.Spawns) SpawnThing(sp.C, sp.X, sp.Y);
            if (def.Id == "E1M3") SpawnTowerBoss();
            SpawnBonusWeapon();
            MergePending();
            Map.BuildLightmap();
            TotalSecrets = Map.SecretCount;
            Map.BuildFlow((int)P.X, (int)P.Y);
        }

        /// <summary>The bat demon and the Elder Fire Demon boss don't get map characters - every printable one is already
        /// spoken for - so the tower's boss room is populated here instead, by level id.</summary>
        void SpawnTowerBoss()
        {
            Add(new Monster(MonsterDef.Bat, 57.5f, 17.5f));
            Add(new Monster(MonsterDef.Bat, 107.5f, 5.5f));
            Add(new Monster(MonsterDef.Bat, 113.5f, 15.5f));
            Add(new Monster(MonsterDef.FireDemon, 110.5f, 10.5f));
            TotalKills += 4;
        }

        /// <summary>Where one of the alternate weapons is found.</summary>
        public sealed class BonusWeapon
        {
            public string Level;
            public char Code;
            public float X, Y;
            public string Where;
            public BonusWeapon(string level, char code, float x, float y, string where) { Level = level; Code = code; X = x; Y = y; Where = where; }
        }

        /// <summary>
        /// The five alternate weapons (the saw, the double barrel, the laser pair, the grenade launcher) use the digits the
        /// wall textures also use, so - like the tower's bat demons - they don't get a map character of their own. Each one
        /// is placed here, once, by level id, on a pedestal in a room built for it; each is always found after the weapon
        /// that shares its slot (--dev-arsenal checks that, and that every one of them can be reached).
        /// </summary>
        public static readonly BonusWeapon[] BonusWeapons =
        {
            new BonusWeapon("E1M2", '5', 31.5f, 26.5f, "the maintenance workshop through the old fuel depot door, south of the runway"),
            new BonusWeapon("E1M3", '6', 94.5f, 3.5f, "the security armory off the last stretch of corridor before the boss"),
            new BonusWeapon("E2M1", '7', 53.5f, 15.5f, "the research wing through the cut in the cliff, east side of the courtyard"),
            new BonusWeapon("E2M2", '8', 47.5f, 17.5f, "the prototype vault behind the blue-key room, on a platform in a slime moat"),
            new BonusWeapon("E2M3", '9', 2.5f, 19.5f, "the far northwest corner of the west room, next to the blue keycard"),
        };

        void SpawnBonusWeapon()
        {
            foreach (var b in BonusWeapons)
            {
                if (b.Level != Def.Id) continue;
                SpawnItem(b.Code, b.X, b.Y, false);
                TotalItems++;
            }
        }

        void SpawnThing(char c, int x, int y)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            switch (c)
            {
                case '^': P.X = cx; P.Y = cy; P.Angle = (float)(-Math.PI / 2); return;
                case '>': P.X = cx; P.Y = cy; P.Angle = 0; return;
                case 'v': P.X = cx; P.Y = cy; P.Angle = (float)(Math.PI / 2); return;
                case '<': P.X = cx; P.Y = cy; P.Angle = (float)Math.PI; return;
                case '%': Add(new Barrel(cx, cy)); return;
                case '*':
                    {
                        var d = new Decor(Art.CeilLamp, cx, cy, false, 0.2f);
                        d.Z = 1 - Art.CeilLamp.H / 64f;
                        d.Glows = true;
                        Add(d);
                        Map.AddLight(cx, cy, 5.5f, 1.0f, Col.Rgb(255, 236, 200));
                        return;
                    }
                case '!':
                    {
                        var d = new Decor(Art.Torch[0], cx, cy, true, 0.2f);
                        d.Anim = Art.Torch;
                        Add(d);
                        Map.AddLight(cx, cy, 4.6f, 1.0f, Col.Rgb(255, 140, 50));
                        return;
                    }
                case 't':
                    {
                        Add(new Decor(Art.TechLamp, cx, cy, true, 0.2f));
                        Map.AddLight(cx, cy, 5.0f, 0.9f, Col.Rgb(200, 225, 255));
                        return;
                    }
                case '|': Add(new Decor(Art.Pillar, cx, cy, true, 0.3f)); return;
                case '&': Add(new Decor(Art.Corpse, cx, cy, false, 0.3f)); return;
                case 'x': Add(new Decor(Art.BloodPool, cx, cy, false, 0.3f)); return;
                case 'k': Add(new Decor(Art.Skulls, cx, cy, false, 0.3f)); return;
                // ---- the airport
                case 'd': Add(new Decor(Art.Desk, cx, cy, true, 0.46f)); return;
                case 'j': Add(new Decor(Art.SeatsEmpty, cx, cy, true, 0.44f)); return;
                case 'l': Add(new Decor(Art.SeatsTaken, cx, cy, true, 0.44f)); return;
                case '(': Add(new Decor(Art.SeatsWrecked, cx, cy, true, 0.4f)); return;
                case ')': Add(new Decor(Art.Rubble, cx, cy, true, 0.42f)); return;
                case 'n': Add(new Decor(Art.Plant, cx, cy, true, 0.22f)); return;
                case 's': Add(new Decor(Art.Luggage[(x * 3 + y * 5) & 1], cx, cy, false, 0.25f)); return;
                case 'u':
                    {
                        var t = new Decor(Art.Travelers[(x * 3 + y * 5) & 3], cx, cy, true, 0.2f);
                        // past the first level, everyone left standing has seen what came through the gate
                        t.Lines = Def.Id == "E1M1" ? TravelerLines : ScaredTravelerLines;
                        Add(t);
                        return;
                    }
                case '@': Add(new Decor(Art.DeadTravelers[(x + y * 2) % 3], cx, cy, false, 0.3f)); return;
                case '/': Add(new Decor(Art.Bin, cx, cy, true, 0.16f)); return;
                case 'T': Add(new Decor(Art.Cart, cx, cy, true, 0.4f)); return;
                case '?':
                    {
                        var d = new Decor(Art.Vending, cx, cy, true, 0.3f);
                        d.Glows = true;
                        Add(d);
                        Map.AddLight(cx, cy, 3.6f, 0.7f, Col.Rgb(200, 226, 255));
                        return;
                    }
                case 'f':
                    {
                        var d = new Decor(Art.Fire[0], cx, cy, true, 0.32f);
                        d.Anim = Art.Fire;
                        d.Glows = true;
                        Add(d);
                        Map.AddLight(cx, cy, 6.2f, 1.15f, Col.Rgb(255, 130, 46));
                        return;
                    }
                case '[': Add(new Marquee(cx, cy)); return;
                case ']':
                    {
                        var d = new Decor(Art.HellSign, cx, cy, false, 0.2f);
                        d.Scale = 1f / 44;
                        d.Z = 0.36f;
                        d.Glows = true;
                        Add(d);
                        Map.AddLight(cx, cy, 5.5f, 0.6f, Col.Rgb(255, 60, 40));
                        return;
                    }
                case '`':
                    {
                        // an evil soldier with something in his pocket
                        var m = new Monster(MonsterDef.Ghoul, cx, cy);
                        m.Angle = (float)(Rng.NextDouble() * Math.PI * 2);
                        m.Carries = 'r';
                        Add(m);
                        TotalKills++;
                        return;
                    }
            }
            var md = MonsterDef.For(c);
            if (md != null)
            {
                var m = new Monster(md, cx, cy);
                m.Angle = (float)Math.Atan2(P.Y - cy, P.X - cx);
                m.Angle = (float)(Rng.NextDouble() * Math.PI * 2);
                Add(m);
                TotalKills++;
                return;
            }
            if (Art.Items.ContainsKey(c))
            {
                SpawnItem(c, cx, cy, false);
                TotalItems++;
                return;
            }
            throw new InvalidOperationException("Unknown map character '" + c + "' at " + x + "," + y + " in " + Def.Id);
        }

        static readonly string[] TravelerLines =
        {
            "HAVE YOU SEEN GATE 14 ANYWHERE?",
            "MY FLIGHT'S DELAYED. AGAIN.",
            "IS THIS THE LINE FOR SECURITY?",
            "COFFEE HERE IS OVERPRICED, BUT WHATEVER.",
            "I HATE FLYING.",
            "DO THEY SERVE FOOD ON THIS AIRLINE?",
            "MY LUGGAGE BETTER NOT BE LOST THIS TIME.",
            "CAN'T WAIT TO GET HOME.",
            "EXCUSE ME, WHERE ARE THE RESTROOMS?",
            "THIS AIRPORT IS HUGE.",
            "DID YOU FEEL THAT? PROBABLY NOTHING.",
            "THEY CANCELLED THE FREE WIFI. UNBELIEVABLE.",
        };

        static readonly string[] ScaredTravelerLines =
        {
            "PLEASE, JUST LET US THROUGH.",
            "I DON'T KNOW WHAT THAT THING WAS.",
            "WE HEARD IT COMING FROM THE TERMINAL.",
            "IS IT STILL BEHIND YOU?",
            "THEY SAID THE RUNWAY WAS CLEAR. THEY LIED.",
            "MY HANDS WON'T STOP SHAKING.",
            "DON'T GO BACK IN THERE.",
            "I JUST WANT TO GET ON A PLANE. ANY PLANE.",
            "SOMETHING'S OUT THERE ON THE FIELD.",
            "KEEP YOUR VOICE DOWN.",
        };

        public const float PedestalScale = 1f / 52;

        /// <summary>How high a weapon sits when it is displayed on a pedestal.</summary>
        public static float PedestalTop { get { return Art.Pedestal.H * PedestalScale; } }

        /// <summary>Weapons left in the level stand on a lit pedestal; ones dropped by the dead lie where they fall.</summary>
        public static bool OnPedestal(char c, bool dropped)
        {
            return !dropped && (c == 'S' || c == 'N' || c == 'L' || c == 'W' || c == 'g'
                || c == '5' || c == '6' || c == '7' || c == '8' || c == '9');
        }

        public void SpawnItem(char c, float x, float y, bool dropped)
        {
            var it = new Item(c, x, y);
            it.Dropped = dropped;
            if (OnPedestal(c, dropped))
            {
                var ped = new Decor(Art.Pedestal, x, y, false, 0.3f);
                ped.Scale = PedestalScale;
                Add(ped);
                it.Z = PedestalTop;
                Map.AddLight(x, y, 3.2f, 0.5f, Col.Rgb(255, 200, 120));
            }
            Add(it);
        }

        public void Add(Actor a) { pending.Add(a); }

        void MergePending()
        {
            if (pending.Count > 0) { Actors.AddRange(pending); pending.Clear(); }
        }

        // ------------------------------------------------------------ frame update

        public void Update(float dt, PlayerInput inp)
        {
            Time += dt;
            if (!P.Dead && !ExitTriggered) LevelTime += dt;
            foreach (var l in Lights) lightPool.Add(l);
            Lights.Clear();
            noiseTimer -= dt;

            // monsters follow a distance field to the player
            flowTimer -= dt;
            int pcx = (int)P.X, pcy = (int)P.Y;
            if (flowTimer <= 0 || pcx != Map.FlowFromX || pcy != Map.FlowFromY)
            {
                flowTimer = 0.5f;
                Map.BuildFlow(pcx, pcy);
            }

            Audio.SetListener(P.X, P.Y, P.Angle);
            P.Update(this, dt, inp);

            for (int i = 0; i < Actors.Count; i++)
            {
                var a = Actors[i];
                if (!a.Remove) a.Update(this, dt);
            }
            MergePending();
            Actors.RemoveAll(a => a.Remove);

            UpdateDoors(dt);
            UpdatePushWalls(dt);
            UpdateSealDoors();
            UpdateIncident(dt);

            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                Messages[i].Time -= dt;
                if (Messages[i].Time <= 0) Messages.RemoveAt(i);
            }

            if (ExitTriggered)
            {
                exitTimer -= dt;
                if (exitTimer <= 0) LevelDone = true;
            }
            UpdateHint();
        }

        public void Message(string text, int color)
        {
            foreach (var m in Messages)
                if (m.Text == text) { m.Time = 3.5f; return; }
            Messages.Add(new GameMessage { Text = text, Color = color < 0 ? Col.Rgb(230, 220, 200) : color, Time = 3.5f });
            if (Messages.Count > 4) Messages.RemoveAt(0);
        }

        public void AddLight(float x, float y, float r, float cr, float cg, float cb)
        {
            DynLight l;
            if (lightPool.Count > 0) { l = lightPool[lightPool.Count - 1]; lightPool.RemoveAt(lightPool.Count - 1); }
            else l = new DynLight();
            l.X = x; l.Y = y; l.R = r; l.Cr = cr; l.Cg = cg; l.Cb = cb;
            Lights.Add(l);
        }

        public void AddMuzzleLight(float x, float y) { AddLight(x, y, 3.2f, 1.2f, 0.9f, 0.4f); }

        public void Shake(float amount) { P.ShakeAmt = Math.Min(1.2f, Math.Max(P.ShakeAmt, amount)); }

        // ------------------------------------------------------------ collision

        bool Blocked(Actor a, float x, float y)
        {
            float r = a.Radius;
            bool avoidsLava = a.Kind == ActorKind.Monster;
            var flyer = a as Monster;
            if (flyer != null && flyer.Def.Flies) avoidsLava = false;
            int x0 = (int)Math.Floor(x - r), x1 = (int)Math.Floor(x + r);
            int y0 = (int)Math.Floor(y - r), y1 = (int)Math.Floor(y + r);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (!Map.BlocksMove(cx, cy) && !(avoidsLava && Map.HurtFloorAt(cx, cy))) continue;
                    float nx = Math.Max(cx, Math.Min(x, cx + 1)), ny = Math.Max(cy, Math.Min(y, cy + 1));
                    float dx = x - nx, dy = y - ny;
                    if (dx * dx + dy * dy < r * r) return true;
                }
            foreach (var o in Actors)
            {
                if (!o.Solid || o == a) continue;
                float rr = r + o.Radius;
                float dx = x - o.X, dy = y - o.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 >= rr * rr) continue;
                float odx = a.X - o.X, ody = a.Y - o.Y;
                if (d2 < odx * odx + ody * ody) return true;   // allow separating when already overlapping
            }
            if (a != P && !P.Dead)
            {
                float rr = r + P.Radius;
                float dx = x - P.X, dy = y - P.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 < rr * rr)
                {
                    float odx = a.X - P.X, ody = a.Y - P.Y;
                    if (d2 < odx * odx + ody * ody) return true;
                }
            }
            return false;
        }

        /// <summary>Moves an actor with wall sliding.</summary>
        public void TryMove(Actor a, float dx, float dy)
        {
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6f) return;
            int n = (int)Math.Ceiling(len / 0.15f);
            float sx = dx / n, sy = dy / n;
            for (int i = 0; i < n; i++)
            {
                if (!Blocked(a, a.X + sx, a.Y)) a.X += sx;
                if (!Blocked(a, a.X, a.Y + sy)) a.Y += sy;
            }
        }

        /// <summary>True once the player has crossed to the far side of a one-way door, past the point of no return.</summary>
        bool PastSeal(Door d)
        {
            float pos = d.Horizontal ? P.Y : P.X;
            float dc = d.Horizontal ? d.Y : d.X;
            return d.SealPositive ? pos > dc + 0.6f : pos < dc - 0.6f;
        }

        void UpdateSealDoors()
        {
            foreach (var d in Map.Doors)
            {
                if (!d.SealBehind || d.Sealed || !d.HasOpened || d.State != DoorState.Closed) continue;
                if (!PastSeal(d)) continue;
                d.Sealed = true;
                Message("THE DOOR SEALS SHUT BEHIND YOU.", Col.Rgb(200, 210, 220));
            }
        }

        /// <summary>The talkative decor actor straight ahead of the player, close enough to address.</summary>
        Actor TalkTarget(Player p)
        {
            float dx = (float)Math.Cos(p.Angle), dy = (float)Math.Sin(p.Angle);
            Actor best = null; float bestD = 1.6f;
            foreach (var a in Actors)
            {
                var dec = a as Decor;
                if (dec == null || dec.Lines == null || dec.Lines.Length == 0) continue;
                float ox = a.X - p.X, oy = a.Y - p.Y;
                float t = ox * dx + oy * dy;
                if (t <= 0 || t > bestD) continue;
                float perp2 = ox * ox + oy * oy - t * t;
                if (perp2 > 0.35f * 0.35f) continue;
                bestD = t; best = a;
            }
            return best;
        }

        void Talk(Actor npc)
        {
            var lines = ((Decor)npc).Lines;
            Message(lines[Rng.Next(lines.Length)], Col.Rgb(210, 222, 235));
            Audio.Play(Sfx.MenuMove, 0.5f, 0, 1.15f, 0);
        }

        // ------------------------------------------------------------ doors and push walls

        bool DoorOccupied(Door d)
        {
            if (Overlaps(P, d.X, d.Y)) return true;
            foreach (var a in Actors)
                if ((a.Solid || a.Kind == ActorKind.Item) && Overlaps(a, d.X, d.Y)) return true;
            return false;
        }

        static bool Overlaps(Actor a, int cx, int cy)
        {
            return a.X + a.Radius > cx && a.X - a.Radius < cx + 1 && a.Y + a.Radius > cy && a.Y - a.Radius < cy + 1;
        }

        public void OpenDoor(Door d, bool byPlayer)
        {
            if (d.IsExit)
            {
                if (d.State != DoorState.Closed || ExitTriggered) return;
                d.State = DoorState.Opening;
                d.HasOpened = true;
                Audio.PlayAt(Sfx.DoorOpen, d.X + 0.5f, d.Y + 0.5f);
                Audio.Play(Sfx.Switch, 0.9f, 0, 1, 0);
                ExitTriggered = true;
                exitTimer = 0.75f;   // long enough to see the exit door swing open before the level ends
                return;
            }
            if (d.Sealed)
            {
                if (byPlayer)
                {
                    Message("IT WON'T BUDGE. SOMETHING SEALED IT SHUT.", Col.Rgb(255, 120, 90));
                    Audio.Play(Sfx.NoWay, 0.8f, 0, 1, 0);
                }
                return;
            }
            if (d.Key != 0 && !P.Keys[d.Key])
            {
                if (byPlayer)
                {
                    string[] names = { "", "RED", "BLUE", "YELLOW" };
                    if (d.Key == 4) Message("YOU NEED YOUR BOARDING PASS TO GET THROUGH THIS GATE.", Col.Rgb(90, 230, 210));
                    else Message("YOU NEED A " + names[d.Key] + " KEYCARD TO OPEN THIS DOOR.", Col.Rgb(255, 90, 60));
                    Audio.Play(Sfx.NoWay, 0.8f, 0, 1, 0);
                }
                return;
            }
            if (d.State == DoorState.Closed || d.State == DoorState.Closing)
            {
                d.State = DoorState.Opening;
                d.HasOpened = true;
                Audio.PlayAt(Sfx.DoorOpen, d.X + 0.5f, d.Y + 0.5f);
            }
            else if (d.State == DoorState.Open && byPlayer && !DoorOccupied(d))
            {
                d.State = DoorState.Closing;
                Audio.PlayAt(Sfx.DoorClose, d.X + 0.5f, d.Y + 0.5f);
            }
        }

        void UpdateDoors(float dt)
        {
            foreach (var d in Map.Doors)
            {
                switch (d.State)
                {
                    case DoorState.Opening:
                        d.Open += dt * 1.6f;
                        if (d.Open >= 1) { d.Open = 1; d.State = DoorState.Open; d.Timer = 4.5f; }
                        break;
                    case DoorState.Open:
                        d.Timer -= dt;
                        if (d.Timer <= 0)
                        {
                            if (DoorOccupied(d)) d.Timer = 1;
                            else { d.State = DoorState.Closing; Audio.PlayAt(Sfx.DoorClose, d.X + 0.5f, d.Y + 0.5f); }
                        }
                        break;
                    case DoorState.Closing:
                        if (DoorOccupied(d)) { d.State = DoorState.Opening; Audio.PlayAt(Sfx.DoorOpen, d.X + 0.5f, d.Y + 0.5f); break; }
                        d.Open -= dt * 1.6f;
                        if (d.Open <= 0) { d.Open = 0; d.State = DoorState.Closed; }
                        break;
                }
            }
        }

        void Push(PushWall p, int dx, int dy)
        {
            if (p.Active || p.Done) return;
            int nx = p.X + dx, ny = p.Y + dy;
            if (!Map.In(nx, ny) || Map.Kind[ny * Map.W + nx] != CellKind.Empty || CellHasSolid(nx, ny)) return;
            p.Active = true;
            p.DX = dx; p.DY = dy;
            int ni = ny * Map.W + nx;
            Map.Kind[ni] = CellKind.Push;
            Map.DoorIdx[ni] = Map.DoorIdx[p.Y * Map.W + p.X];
            Map.WallTex[ni] = (byte)p.Texture;
            Audio.PlayAt(Sfx.PushWall, p.X + 0.5f, p.Y + 0.5f);
            if (!p.Counted)
            {
                p.Counted = true;
                Secrets++;
                Score += SecretScore;
                Message("A SECRET IS REVEALED!", Col.Rgb(120, 255, 120));
                Audio.Play(Sfx.Secret, 0.7f, 0, 1, 0);
            }
        }

        bool CellHasSolid(int cx, int cy)
        {
            if (Overlaps(P, cx, cy)) return true;
            foreach (var a in Actors) if (a.Solid && Overlaps(a, cx, cy)) return true;
            return false;
        }

        void UpdatePushWalls(float dt)
        {
            foreach (var p in Map.PushWalls)
            {
                if (!p.Active) continue;
                p.Offset += dt * 1.0f;
                if (p.Offset < 1) continue;
                // arrived in the next cell
                int oi = p.Y * Map.W + p.X;
                Map.Kind[oi] = CellKind.Empty;
                Map.WallTex[oi] = 0;
                Map.DoorIdx[oi] = -1;
                p.X += p.DX; p.Y += p.DY;
                p.Offset = 0;
                p.Moved++;
                int nx = p.X + p.DX, ny = p.Y + p.DY;
                bool more = p.Moved < 2 && Map.In(nx, ny) && Map.Kind[ny * Map.W + nx] == CellKind.Empty && !CellHasSolid(nx, ny);
                if (more)
                {
                    int ni = ny * Map.W + nx;
                    Map.Kind[ni] = CellKind.Push;
                    Map.DoorIdx[ni] = Map.DoorIdx[p.Y * Map.W + p.X];
                    Map.WallTex[ni] = (byte)p.Texture;
                }
                else
                {
                    p.Active = false;
                    p.Done = true;
                }
            }
        }

        // ------------------------------------------------------------ using things

        /// <summary>Finds the first non-empty cell in front of the player within reach.</summary>
        bool UseTarget(out int cx, out int cy)
        {
            float dx = (float)Math.Cos(P.Angle), dy = (float)Math.Sin(P.Angle);
            int sx = (int)P.X, sy = (int)P.Y;
            for (float t = 0.1f; t < 1.35f; t += 0.05f)
            {
                cx = (int)(P.X + dx * t); cy = (int)(P.Y + dy * t);
                if (cx == sx && cy == sy) continue;
                if (Map.KindAt(cx, cy) != CellKind.Empty) return true;
            }
            cx = cy = -1;
            return false;
        }

        public void PlayerUse(Player p)
        {
            var npc = TalkTarget(p);
            if (npc != null) { Talk(npc); return; }
            int cx, cy;
            if (!UseTarget(out cx, out cy)) return;
            var k = Map.KindAt(cx, cy);
            if (k == CellKind.Door) { OpenDoor(Map.DoorAt(cx, cy), true); return; }
            if (k == CellKind.Push)
            {
                var pw = Map.PushAt(cx, cy);
                float dx = (float)Math.Cos(p.Angle), dy = (float)Math.Sin(p.Angle);
                if (Math.Abs(dx) > Math.Abs(dy)) Push(pw, Math.Sign(dx), 0); else Push(pw, 0, Math.Sign(dy));
                return;
            }
        }

        // ------------------------------------------------------------ the incident (levels with a calm start)

        public int Incident;            // 0 calm, 1 it is happening, 2 it has happened
        float incidentTime, chimeTimer = 14;

        /// <summary>The music this level should have playing right now.</summary>
        public int MusicNow() { return Incident != 0 && Def.MusicAfter >= 0 ? Def.MusicAfter : Def.Music; }

        /// <summary>Something goes wrong far away: the calm music stops, the building shakes and something roars.</summary>
        public void TriggerIncident()
        {
            if (Incident != 0) return;
            Incident = 1;
            incidentTime = 0;
            Music.Stop();
        }

        void UpdateIncident(float dt)
        {
            if (Def.Chime && Incident == 0)
            {
                chimeTimer -= dt;
                if (chimeTimer <= 0)
                {
                    chimeTimer = 38 + (float)Rng.NextDouble() * 22;
                    Audio.Play(Sfx.Chime, 0.32f, 0, 1, 0);      // the public address system
                }
            }
            if (Incident != 1) return;
            float before = incidentTime;
            incidentTime += dt;
            if (before < 0.9f && incidentTime >= 0.9f)
            {
                Audio.Play(Sfx.Explode, 1f, 0, 0.5f, 0);
                Shake(1.2f);
                P.DamageFlash = Math.Max(P.DamageFlash, 0.15f);
            }
            if (before < 1.5f && incidentTime >= 1.5f) Audio.Play(Sfx.Explode, 0.7f, 0.4f, 0.4f, 0);
            if (before < 2.4f && incidentTime >= 2.4f)
            {
                Audio.Play(Sfx.BossSight, 0.95f, 0, 0.6f, 0);
                Audio.Play(Sfx.Growl, 0.7f, -0.3f, 0.5f, 0);
                Shake(0.7f);
                Message("THE FLOOR TREMBLES. SOMETHING SCREAMS BEYOND THE GATE.", Col.Rgb(255, 120, 90));
            }
            if (before < 4.2f && incidentTime >= 4.2f)
            {
                Incident = 2;
                Music.Play(MusicNow());
            }
        }

        void UpdateHint()
        {
            Hint = null;
            if (P.Dead || ExitTriggered) return;
            var npc = TalkTarget(P);
            if (npc != null) { Hint = "[E] TALK"; HintColor = Col.Rgb(200, 220, 255); return; }
            int cx, cy;
            if (!UseTarget(out cx, out cy)) return;
            var k = Map.KindAt(cx, cy);
            if (k == CellKind.Door)
            {
                var d = Map.DoorAt(cx, cy);
                if (d.IsExit) { Hint = "[E] EXIT LEVEL"; HintColor = Col.Rgb(120, 255, 120); return; }
                if (d.Sealed) { Hint = "SEALED SHUT"; HintColor = Col.Rgb(180, 90, 80); return; }
                if (d.State == DoorState.Opening || d.State == DoorState.Open) return;
                if (d.Key != 0 && !P.Keys[d.Key])
                {
                    string[] names = { "", "RED KEYCARD", "BLUE KEYCARD", "YELLOW KEYCARD", "BOARDING PASS" };
                    int[] cols = { 0, Col.Rgb(255, 80, 60), Col.Rgb(90, 150, 255), Col.Rgb(255, 220, 60), Col.Rgb(90, 230, 210) };
                    Hint = "LOCKED - NEEDS " + names[d.Key];
                    HintColor = cols[d.Key];
                }
                else { Hint = "[E] OPEN DOOR"; HintColor = Col.Rgb(240, 230, 200); }
            }
        }

        // ------------------------------------------------------------ combat

        /// <summary>Wakes up monsters that can hear a noise at (x, y): sound flows through open space and open doors.</summary>
        public void Noise(float x, float y)
        {
            if (noiseTimer > 0) return;
            noiseTimer = 0.25f;
            int w = Map.W;
            var dist = new Dictionary<int, int>();
            var q = new Queue<int>();
            int s = (int)y * w + (int)x;
            dist[s] = 0;
            q.Enqueue(s);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int d = dist[c];
                if (d >= 22) continue;
                int cx = c % w, cy = c / w;
                for (int k = 0; k < 8; k += 2)
                {
                    int nx = cx + Map.DX8[k], ny = cy + Map.DY8[k];
                    if (!Map.In(nx, ny)) continue;
                    int j = ny * w + nx;
                    if (dist.ContainsKey(j)) continue;
                    var kind = Map.Kind[j];
                    if (kind == CellKind.Wall || kind == CellKind.Push) continue;
                    if (kind == CellKind.Door && Map.Doors[Map.DoorIdx[j]].Open < 0.3f) continue;
                    dist[j] = d + 1;
                    q.Enqueue(j);
                }
            }
            foreach (var a in Actors)
            {
                var m = a as Monster;
                if (m == null || m.State != MState.Idle || m.Ambush) continue;
                if (dist.ContainsKey((int)m.Y * w + (int)m.X)) m.Alert(this);
            }
        }

        public void PlayerFire(Player p, WeaponDef d, float refire, int shots)
        {
            float dx = (float)Math.Cos(p.Angle), dy = (float)Math.Sin(p.Angle);
            if (d.Melee)
            {
                MeleeSwing(p, d, 1f);
                return;
            }

            Audio.Play(d.Sound, 0.9f, 0, 1, 0);
            Noise(p.X, p.Y);
            if (d.Ray)
            {
                // a bolt of whatever the ray gun keeps inside it: fast, heavy, and it burns what it touches
                const float speed = 20;
                var bolt = new Projectile(p, p.X + dx * 0.35f, p.Y + dy * 0.35f, p.Angle, speed, Projectile.RAY);
                bolt.DmgMin = d.DmgMin; bolt.DmgMax = d.DmgMax;
                bolt.SplashDamage = 90; bolt.SplashRadius = 2.6f;
                bolt.Z = p.EyeZ - 0.1f + p.AimSlope * 0.35f;
                bolt.VZ = p.AimSlope * speed;
                Add(bolt);
                return;
            }
            if (d.Rocket)
            {
                // the rocket leaves along the look direction, including up / down
                const float speed = 22;
                var pr = new Projectile(p, p.X + dx * 0.35f, p.Y + dy * 0.35f, p.Angle, speed, Projectile.ROCKET);
                pr.DmgMin = d.DmgMin; pr.DmgMax = d.DmgMax;
                pr.SplashDamage = 140; pr.SplashRadius = 3.8f;
                pr.Z = p.EyeZ - 0.12f + p.AimSlope * 0.35f;
                pr.VZ = p.AimSlope * speed;
                Add(pr);
                return;
            }
            if (d.Grenade)
            {
                // lobbed along the look direction with a little lift, so it drops in a low arc and skips along the floor
                var pr = new Projectile(p, p.X + dx * 0.35f, p.Y + dy * 0.35f, p.Angle, Projectile.GrenadeSpeed, Projectile.GRENADE);
                pr.DmgMin = d.DmgMin; pr.DmgMax = d.DmgMax;
                pr.SplashDamage = 100; pr.SplashRadius = 1.7f;
                pr.Fuse = 2.5f;
                pr.Z = p.EyeZ - 0.13f;
                pr.VZ = Projectile.GrenadeLift + p.AimSlope * Projectile.GrenadeSpeed;
                Add(pr);
                return;
            }
            if (d.Knockback > 0)
            {
                // the double barrel shoves you back - half as hard if only one barrel had a shell in it
                float k = d.Knockback * shots / Math.Max(1, d.Barrels);
                p.VX -= dx * k; p.VY -= dy * k;
                p.ShakeAmt = Math.Min(1, p.ShakeAmt + 0.4f * shots / Math.Max(1, d.Barrels));
            }
            float spread = d.Pellets > 1 ? d.Spread : (refire > 0 ? d.Spread : d.Spread * 0.25f);
            int pellets = d.Pellets * Math.Max(1, shots);
            for (int i = 0; i < pellets; i++)
            {
                float a = p.Angle + (float)((Rng.NextDouble() - 0.5) * 2 * spread);
                float slope = p.AimSlope + (float)((Rng.NextDouble() - 0.5) * spread);
                Hitscan(p.X, p.Y, p.EyeZ, a, slope, 40, Rng.Next(d.DmgMin, d.DmgMax + 1), p, d.Falloff);
            }
        }

        public const float BeamRange = 32;
        float beamSparkTimer;

        /// <summary>The laser's continuous beam: while the trigger is held it is one unbroken red line from the muzzle to
        /// whatever it touches first, burning that for as long as it stays on it. The view draws the line itself
        /// (from the Player's BeamOn / BeamDist); this does the damage, the sparks where it lands, and the light.</summary>
        public void PlayerBeam(Player p, WeaponDef d, float dt)
        {
            float dx = (float)Math.Cos(p.Angle), dy = (float)Math.Sin(p.Angle);
            float z = p.EyeZ, slope = p.AimSlope;
            float best = Map.RayCast(p.X, p.Y, dx, dy, BeamRange);
            if (slope < -0.001f) best = Math.Min(best, z / -slope);
            else if (slope > 0.001f && !Map.IsOutdoor((int)(p.X + dx * best * 0.5f), (int)(p.Y + dy * best * 0.5f))) best = Math.Min(best, (1 - z) / slope);
            Actor hit = null;
            foreach (var a in Actors)
            {
                if (!a.Shootable) continue;
                float ox = a.X - p.X, oy = a.Y - p.Y;
                float t = ox * dx + oy * dy;
                if (t <= 0 || t > best + a.Radius) continue;
                float perp2 = ox * ox + oy * oy - t * t;
                float r = a.Radius + 0.06f;
                if (perp2 > r * r) continue;
                float th = t - (float)Math.Sqrt(Math.Max(0, r * r - perp2));
                if (th >= best) continue;
                float zt = z + slope * th;
                if (zt < a.Z - 0.08f || zt > a.Z + a.Height + 0.08f) continue;
                best = th; hit = a;
            }
            p.BeamDist = best;
            p.BeamOnBody = hit != null;
            if (hit != null)
            {
                hit.Damage(this, d.DmgPerSec * dt, p, false);
                var slowed = hit as Monster;
                if (slowed != null) slowed.SlowTime = 0.5f;   // the beam slows what it burns instead of staggering it
            }

            // where it lands: a shower of sparks (or a spray of blood), and red light on the walls around it
            float ex = p.X + dx * (best - 0.05f), ey = p.Y + dy * (best - 0.05f), ez = Math.Max(0.03f, Math.Min(0.97f, z + slope * best));
            beamSparkTimer -= dt;
            if (beamSparkTimer <= 0 && best < BeamRange - 0.1f)
            {
                beamSparkTimer = 0.045f;
                for (int i = 0; i < 2; i++)
                {
                    var sp = new Effect(hit != null && hit.Kind == ActorKind.Monster ? Art.Blood : Art.LaserSpark, ex, ey, ez - 0.03f, 0.22f + (float)Rng.NextDouble() * 0.15f);
                    sp.VX = -dx * 1.2f + (float)(Rng.NextDouble() - 0.5) * 2.4f;
                    sp.VY = -dy * 1.2f + (float)(Rng.NextDouble() - 0.5) * 2.4f;
                    sp.VZ = 0.4f + (float)Rng.NextDouble() * 1.4f;
                    sp.Gravity = 5;
                    sp.Scale = 1f / 90;
                    sp.Glow = hit == null || hit.Kind != ActorKind.Monster;
                    Add(sp);
                }
            }
            AddLight(ex, ey, 2.8f, 1.5f, 0.3f, 0.2f);
            AddLight(p.X + dx * 0.6f, p.Y + dy * 0.6f, 2.2f, 0.9f, 0.18f, 0.12f);
            Noise(p.X, p.Y);
        }

        /// <summary>The laser ray's shot, once charged: a flat arc of light that sweeps out from the player and burns every
        /// body it crosses - not just the first one - until the walls stop it.</summary>
        public void PlayerArc(Player p, WeaponDef d)
        {
            Audio.Play(d.Sound, 1f, 0, 1, 0);
            Noise(p.X, p.Y);
            Add(new LaserArc(this, p, d.DmgMin, d.DmgMax));
            p.ShakeAmt = Math.Min(1, p.ShakeAmt + 0.3f);
            AddLight(p.X, p.Y, 4.5f, 0.35f, 0.9f, 1.5f);
        }

        /// <summary>
        /// A bullet from (x, y, z) along a horizontal angle and a vertical slope (height change per unit of distance).
        /// It stops at the first wall, floor, ceiling or body in its way.
        /// </summary>
        void Hitscan(float x, float y, float z, float ang, float slope, float range, int dmg, Actor source, float falloff = 0)
        {
            float dx = (float)Math.Cos(ang), dy = (float)Math.Sin(ang);
            float wall = Map.RayCast(x, y, dx, dy, range);
            float best = wall;
            bool overWall = false;
            // floor / ceiling in the way?
            if (slope < -0.001f) best = Math.Min(best, z / -slope);
            else if (slope > 0.001f)
            {
                float tc = (1 - z) / slope;
                if (tc < best)
                {
                    float cxp = x + dx * tc, cyp = y + dy * tc;
                    if (!Map.IsOutdoor((int)cxp, (int)cyp)) best = tc;
                    else
                    {
                        // under the open sky the shot can sail over walls lower than it
                        int wx = (int)(x + dx * (wall + 0.01f)), wy = (int)(y + dy * (wall + 0.01f));
                        float wh = Map.In(wx, wy) ? Math.Max(1, Map.WallHeight[wy * Map.W + wx]) : 2;
                        if (z + slope * wall > wh) { overWall = true; best = range; }
                    }
                }
            }
            Actor hit = null;
            foreach (var a in Actors)
            {
                if (!a.Shootable || a == source) continue;
                float ox = a.X - x, oy = a.Y - y;
                float t = ox * dx + oy * dy;
                if (t <= 0 || t > best + a.Radius) continue;
                float perp2 = ox * ox + oy * oy - t * t;
                float r = a.Radius + 0.06f;
                if (perp2 > r * r) continue;
                float th = t - (float)Math.Sqrt(r * r - perp2);
                if (th >= best) continue;
                // is the bullet at the right height when it reaches the body? (a little forgiveness at low resolution)
                float zt = z + slope * th, zt2 = z + slope * t;
                float lo = a.Z - 0.06f, hi = a.Z + a.Height + 0.06f;
                if ((zt < lo && zt2 < lo) || (zt > hi && zt2 > hi)) continue;
                best = th; hit = a;
            }
            float hz = Math.Max(0.02f, Math.Min(0.98f, z + slope * best));
            if (hit != null)
            {
                // a short-range weapon's pellets lose their bite the further they travel: full up close, a tenth at the limit
                if (falloff > 0) dmg = Math.Max(1, (int)(dmg * Math.Max(0.1f, Math.Min(1f, 1.1f - best / falloff))));
                hit.Damage(this, dmg, source, false);
                if (hit.Kind == ActorKind.Monster) SpawnBlood(x + dx * best, y + dy * best, hz - 0.05f, 2);
                else SpawnPuff(x + dx * (best - 0.05f), y + dy * (best - 0.05f), hz);
            }
            else if (!overWall && best < range) SpawnPuff(x + dx * (best - 0.06f), y + dy * (best - 0.06f), hz);
        }

        public void SpawnPuff(float x, float y, float z)
        {
            // Z of an effect is the bottom of its sprite; the puff is about 0.2 tall
            var e = new Effect(Art.Puff, x, y, Math.Max(0, z - 0.1f), 0.3f);
            e.VZ = 0.4f;
            e.Scale = 1f / 48;
            Add(e);
        }

        public void SpawnPuffNear(float px, float py, float ang)
        {
            float a = ang + (float)((Rng.NextDouble() - 0.5) * 0.3);
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
            float t = Map.RayCast(px, py, dx, dy, 12);
            if (t < 12) SpawnPuff(px + dx * (t - 0.06f), py + dy * (t - 0.06f), 0.35f + (float)Rng.NextDouble() * 0.3f);
        }

        void SpawnBlood(float x, float y, float z, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var e = new Effect(Art.Blood, x, y, z, 0.45f + (float)Rng.NextDouble() * 0.2f);
                e.VX = (float)(Rng.NextDouble() - 0.5) * 1.5f;
                e.VY = (float)(Rng.NextDouble() - 0.5) * 1.5f;
                e.VZ = 0.5f + (float)Rng.NextDouble();
                e.Gravity = 5;
                e.Scale = 1f / 56;
                Add(e);
            }
        }

        public Actor ProjectileHit(Projectile pr)
        {
            float cz = pr.Z + 0.1f;
            if (pr.Owner != P && !P.Dead && cz < 0.8f)
            {
                float rr = pr.Radius + P.Radius;
                if ((pr.X - P.X) * (pr.X - P.X) + (pr.Y - P.Y) * (pr.Y - P.Y) < rr * rr) return P;
            }
            // a monster's own shot flies straight through its friends; only blasts hurt them (and anything parried back)
            bool fromMonster = pr.Owner != null && pr.Owner.Kind == ActorKind.Monster;
            bool blast = pr.SplashRadius > 0 || pr.Type == Projectile.ROCKET;
            foreach (var a in Actors)
            {
                if (!a.Shootable || a == pr.Owner) continue;
                if (fromMonster && !blast && a.Kind == ActorKind.Monster) continue;
                if (cz < a.Z - 0.1f || cz > a.Z + a.Height + 0.1f) continue;
                float rr = pr.Radius + a.Radius;
                if ((pr.X - a.X) * (pr.X - a.X) + (pr.Y - a.Y) * (pr.Y - a.Y) < rr * rr) return a;
            }
            return null;
        }

        public void Explode(float x, float y, float z, float damage, float radius, Actor source)
        {
            var e = new Effect(Art.Explosion, x, y, Math.Max(0, z - 0.45f), 0.6f);
            e.Scale = 1f / 38;
            e.Glow = true;
            e.Light = 4.2f;
            Add(e);
            Audio.PlayAt(Sfx.Explode, x, y, 1.2f, 0);
            foreach (var a in Actors)
            {
                if (!a.Shootable || a == source) continue;
                float d = a.DistTo(x, y) - a.Radius;
                if (d < 0) d = 0;
                if (d >= radius || !Map.LOS(x, y, a.X, a.Y)) continue;
                a.Damage(this, damage * (1 - d / radius), source, true);
            }
            if (!P.Dead)
            {
                float d = P.DistTo(x, y) - P.Radius;
                if (d < 0) d = 0;
                if (d < radius && Map.LOS(x, y, P.X, P.Y))
                {
                    float f = 1 - d / radius;
                    P.Hurt(this, damage * f * (source == P ? 0.75f : 1f), x, y);
                    float dist = Math.Max(0.1f, P.DistTo(x, y));
                    P.VX += (P.X - x) / dist * 6 * f; P.VY += (P.Y - y) / dist * 6 * f;
                }
                float sd = P.DistTo(x, y);
                Shake(Math.Min(1, 3.5f / (1 + sd * sd * 0.25f)));
            }
            Noise(x, y);
        }

        /// <summary>The ray gun's shot: an instant beam of hell light, and a heavy burst where it lands.</summary>
        public void PlayerRay(Player p, WeaponDef d)
        {
            Audio.Play(Sfx.RayGun, 1f, 0, 1f, 0);   // the discharge: the whine snapping into the shot
            float dx = (float)Math.Cos(p.Angle), dy = (float)Math.Sin(p.Angle);
            float slope = p.AimSlope, z = p.EyeZ - 0.05f;
            const float range = 40;
            float best = Map.RayCast(p.X, p.Y, dx, dy, range);
            if (slope < -0.001f) best = Math.Min(best, z / -slope);
            else if (slope > 0.001f) best = Math.Min(best, (1 - z) / slope);

            Actor hit = null;
            foreach (var a in Actors)
            {
                if (!a.Shootable) continue;
                float ox = a.X - p.X, oy = a.Y - p.Y;
                float t = ox * dx + oy * dy;
                if (t <= 0 || t > best + a.Radius) continue;
                float perp2 = ox * ox + oy * oy - t * t;
                float r = a.Radius + 0.1f;
                if (perp2 > r * r) continue;
                float th = t - (float)Math.Sqrt(Math.Max(0, r * r - perp2));
                if (th >= best) continue;
                float zt = z + slope * th;
                if (zt < a.Z - 0.12f || zt > a.Z + a.Height + 0.12f) continue;
                best = th; hit = a;
            }

            // the beam: a line of hot motes that fade almost at once
            int steps = Math.Max(2, (int)(best * 4));
            for (int i = 1; i <= steps; i++)
            {
                float t = best * i / steps;
                var mote = new Effect(Art.RayBolt, p.X + dx * t, p.Y + dy * t, z + slope * t - 0.05f, 0.14f + 0.06f * (1 - (float)i / steps));
                mote.Scale = 1f / 190;
                mote.Glow = true;
                Add(mote);
            }
            AddLight(p.X + dx * 0.6f, p.Y + dy * 0.6f, 5f, 1.3f, 0.5f, 1.1f);
            Noise(p.X, p.Y);
            if (hit != null) hit.Damage(this, Rng.Next(d.DmgMin, d.DmgMax + 1), p, false);
            // and the burst at the far end
            float bx = p.X + dx * Math.Max(0.4f, best - 0.2f), by = p.Y + dy * Math.Max(0.4f, best - 0.2f);
            Explode(bx, by, Math.Max(0.05f, Math.Min(0.95f, z + slope * best)), 260, 4.6f, p);
        }

        /// <summary>A punch: hits the first thing within reach in front of the player.</summary>
        public bool MeleeSwing(Player p, WeaponDef d, float damageMul)
        {
            float dx = (float)Math.Cos(p.Angle), dy = (float)Math.Sin(p.Angle);
            Actor best = null;
            float bestD = d.Saw ? 1.4f : 1.25f;
            foreach (var a in Actors)
            {
                if (!a.Shootable) continue;
                float ox = a.X - p.X, oy = a.Y - p.Y;
                float t = ox * dx + oy * dy;
                if (t <= 0) continue;
                float dd = (float)Math.Sqrt(ox * ox + oy * oy) - a.Radius;
                if (dd > bestD) continue;
                float perp = Math.Abs(ox * dy - oy * dx);
                if (perp > a.Radius + 0.25f) continue;
                best = a; bestD = dd;
            }
            if (best == null)
            {
                if (!d.Saw) Audio.Play(Sfx.Swing, 0.7f, 0, 1, 0);
                return false;
            }
            var bitten = best as Monster;
            bool winding = bitten != null && (bitten.State == MState.WindUp || bitten.State == MState.Fire);
            Audio.Play(d.Saw ? d.Sound : Sfx.Punch, 0.9f, 0, d.Saw ? 0.9f + (float)Rng.NextDouble() * 0.25f : 1, 0);
            float dmgMul = damageMul;
            float blow = Rng.Next(d.DmgMin, d.DmgMax + 1);
            if (!d.Saw && bitten != null)
            {
                // a fist does real harm only to the soldiers - it flattens a possessed in three punches, however the dice
                // fall - and against anything bigger or stranger it is close to useless
                if (bitten.Def == MonsterDef.Ghoul) blow = (bitten.Def.Health / 3f + 1f) * (1f + (float)Rng.NextDouble() * 0.2f);
                else dmgMul *= 0.3f;
            }
            best.Damage(this, Math.Max(1, blow * dmgMul), p, false);
            float hx = best.X - dx * best.Radius, hy = best.Y - dy * best.Radius;
            if (best.Kind == ActorKind.Monster) SpawnBlood(hx, hy, 0.45f, d.Saw ? 2 : 3);
            if (!d.Saw) return true;

            // the saw: every bite throws sparks, and it doesn't just stand a chance of making something flinch the way a bullet
            // does - anything short of a boss is knocked out of whatever it was doing, winding up an attack included, and
            // kept that way for as long as the blade stays in it
            p.SawBite = 0.16f;
            p.ShakeAmt = Math.Min(1, p.ShakeAmt + 0.06f);
            for (int i = 0; i < 2; i++)
            {
                var sp = new Effect(Art.LaserSpark, hx, hy, 0.35f + (float)Rng.NextDouble() * 0.2f, 0.25f);
                sp.VX = -dx * 1.5f + (float)(Rng.NextDouble() - 0.5) * 3;
                sp.VY = -dy * 1.5f + (float)(Rng.NextDouble() - 0.5) * 3;
                sp.VZ = 0.8f + (float)Rng.NextDouble() * 1.2f;
                sp.Gravity = 6;
                sp.Scale = 1f / 110;
                sp.Glow = true;
                Add(sp);
            }
            var mon = best as Monster;
            if (mon != null && mon.Alive && !mon.Def.Boss)
            {
                if (winding) mon.Interrupted++;
                mon.State = MState.Pain;
                mon.StateTime = Math.Max(mon.StateTime, mon.Def.PainTime + 0.08f);
                mon.ShotsLeft = 0;
                mon.Aiming = false;
                mon.Cooldown = Math.Max(mon.Cooldown, 0.5f);
            }
            return true;
        }

        /// <summary>The right-button jab: weaker than a committed punch, but it is what parries projectiles.</summary>
        public void PlayerPunch(Player p)
        {
            MeleeSwing(p, WeaponDef.All[0], 0.6f);
        }

        /// <summary>A projectile flying into the player's outstretched fist is knocked back at whoever fired it.</summary>
        public bool TryParry(Projectile pr)
        {
            if (P.ParryTime <= 0 || P.Dead || pr.Owner == (Actor)P) return false;
            float dx = pr.X - P.X, dy = pr.Y - P.Y;
            float d2 = dx * dx + dy * dy;
            if (d2 > 1.4f * 1.4f) return false;
            float dz = pr.Z - (P.EyeZ - 0.15f);
            if (dz > 0.8f || dz < -0.8f) return false;
            // it has to come at the fist, not at your back
            float dist = (float)Math.Sqrt(Math.Max(1e-4f, d2));
            if ((dx * (float)Math.Cos(P.Angle) + dy * (float)Math.Sin(P.Angle)) / dist < 0.3f) return false;
            pr.Parried(this, P);
            return true;
        }

        public void CheckPickups(Player p)
        {
            foreach (var a in Actors)
            {
                if (a.Kind != ActorKind.Item || a.Remove) continue;
                float rr = p.Radius + 0.32f;
                float dx = a.X - p.X, dy = a.Y - p.Y;
                if (dx * dx + dy * dy > rr * rr) continue;
                var it = (Item)a;
                if (p.Give(this, it.Code, it.Dropped))
                {
                    a.Remove = true;
                    if (!it.Dropped) { ItemsTaken++; Score += ItemScore(it.Code); }
                }
            }
        }

        /// <summary>Points for picking something up. Weapons and keys are worth the most, small bonuses the least.</summary>
        public static int ItemScore(char code)
        {
            switch (code)
            {
                case 'S': case 'N': case 'L': case 'W': case 'g':
                case '5': case '6': case '7': case '8': case '9': return 500;   // the weapons
                case '{': return 100;                                      // your boarding pass
                case 'w': return 60;                                       // soul cells (secret rooms only)
                case 'r': case 'b': case 'y': return 200;                  // keycards
                case 'o': case 'U': return 300;                            // soul orb, mega armor
                case 'G': return 150;                                      // combat armor
                case 'm': return 50;                                       // medikit
                case 'h': return 25;                                       // stimpack
                case '+': case 'a': return 10;                             // health / armor bonus
                default: return 25;                                        // ammo
            }
        }

        public void BossKilled(string name)
        {
            BossDead = true;
            Message("THE " + name + " HAS FALLEN! TAKE ITS KEY.", Col.Rgb(255, 220, 90));
            Shake(1.2f);
        }
    }
}
