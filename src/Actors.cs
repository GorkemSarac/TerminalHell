// TERMINAL HELL - everything that lives in the world: monsters, items, decorations, projectiles, effects.
using System;

namespace TerminalHell
{
    enum ActorKind { Monster, Item, Decor, Barrel, Projectile, Effect, Player }

    abstract class Actor
    {
        public ActorKind Kind;
        public float X, Y, Z;
        public float Radius = 0.3f;
        public bool Solid, Shootable, Remove;
        public float Health;
        public float Flash;
        public float Scale = 1f / 64;
        public int Tag;
        static int nextTag = 1;

        protected Actor() { Tag = nextTag++; }

        public virtual void Update(World w, float dt) { }
        public abstract Image Sprite(World w);
        public virtual bool Bright { get { return false; } }
        /// <summary>Height of the body above Z, used for vertical hit tests.</summary>
        public virtual float Height { get { return 0.5f; } }
        public virtual void Damage(World w, float amount, Actor source, bool splash) { }

        public float DistTo(float x, float y) { float dx = x - X, dy = y - Y; return (float)Math.Sqrt(dx * dx + dy * dy); }
    }

    // ================================================================ monsters

    enum AttackType { Hitscan, Projectile, Melee, Rockets }

    sealed class MonsterDef
    {
        public string Name;
        public char Code;
        public float Health, Speed, Radius;
        public float PainChance, PainTime = 0.25f;
        public AttackType Attack;
        public float Range = 20, MeleeRange = 1.2f;
        public float WindUp = 0.35f, CoolMin = 1.0f, CoolMax = 2.5f;
        public int DmgMin, DmgMax, Shots = 1;
        public float ProjSpeed = 8;
        public Sfx Sight, Pain, Death, AttackSnd;
        public char Drop;
        public float Scale = 1f / 64;
        public int Score = 100;     // points for killing one
        public int ProjType = Projectile.FIREBALL;
        public float DropChance = 0.5f;
        public Image[] Frames;
        public float MeleeChance;   // chance a ranged monster claws when close
        public bool Boss;
        public bool FireBoss;        // a second kind of boss: fireball volleys, a bat-summoning scream, a self-centred nova
        public bool Flies;           // hovers at HoverZ instead of walking the floor, and ignores lava
        public float HoverZ = 0.55f;
        public float Height = 0.9f; // from the sprite: floor to top of the head

        void MeasureHeight()
        {
            var img = Frames[Art.WALK1];
            int top = img.H;
            foreach (int t in img.ColTop) top = Math.Min(top, t);
            Height = (img.H - top) * Scale;
        }

        public static MonsterDef Ghoul, Fiend, Brute, Warden, Imp, Bat, FireDemon;

        public static void Build()
        {
            Ghoul = new MonsterDef
            {
                // fires a small, fast round: dodgeable if you are moving, and it can be parried
                Name = "POSSESSED", Code = 'z', Health = 38, Speed = 1.35f, Radius = 0.3f, PainChance = 0.7f, Attack = AttackType.Projectile,
                ProjType = Projectile.BULLET, ProjSpeed = 13f,
                Range = 16, WindUp = 0.65f, CoolMin = 2.3f, CoolMax = 4.2f, DmgMin = 5, DmgMax = 12, Shots = 1,
                Sight = Sfx.GhoulSight, Pain = Sfx.GhoulPain, Death = Sfx.GhoulDeath, AttackSnd = Sfx.GhoulShot, Drop = 'c', Frames = Art.Ghoul, Score = 100,
            };
            Fiend = new MonsterDef
            {
                Name = "FIRE DEMON", Code = 'i', Health = 75, Speed = 1.6f, Radius = 0.32f, PainChance = 0.6f, Attack = AttackType.Projectile,
                Range = 18, WindUp = 0.75f, CoolMin = 2.2f, CoolMax = 4.2f, DmgMin = 8, DmgMax = 20, ProjSpeed = 6.0f,
                Sight = Sfx.FiendSight, Pain = Sfx.FiendPain, Death = Sfx.FiendDeath, AttackSnd = Sfx.FiendThrow, Frames = Art.Fiend, MeleeChance = 0.8f, Score = 250,
            };
            Brute = new MonsterDef
            {
                Name = "GLUTTONY DEMON", Code = 'p', Health = 188, Speed = 2.5f, Radius = 0.42f, PainChance = 0.45f, Attack = AttackType.Melee,
                Range = 1.4f, MeleeRange = 1.25f, WindUp = 0.5f, CoolMin = 0.9f, CoolMax = 1.6f, DmgMin = 10, DmgMax = 28,
                Sight = Sfx.BruteSight, Pain = Sfx.BrutePain, Death = Sfx.BruteDeath, AttackSnd = Sfx.BruteBite, Frames = Art.Brute, Score = 500,
            };
            Warden = new MonsterDef
            {
                Name = "WARDEN", Code = 'K', Health = 2100, Speed = 1.3f, Radius = 0.62f, PainChance = 0.08f, Attack = AttackType.Rockets,
                Range = 30, WindUp = 1.65f, CoolMin = 1.8f, CoolMax = 3.0f, DmgMin = 30, DmgMax = 60, Shots = 3, ProjSpeed = 9,
                Sight = Sfx.BossSight, Pain = Sfx.BossPain, Death = Sfx.BossDeath, AttackSnd = Sfx.Rocket, Frames = Art.Warden,
                Scale = 1f / 58, Boss = true, Drop = 'y', DropChance = 1f, Score = 5000,   // the key it carries always drops
            };
            Imp = new MonsterDef
            {
                // small, fast, and only dangerous up close: it closes distance quickly and claws
                Name = "LESSER DEMON", Code = '\'', Health = 32, Speed = 2.9f, Radius = 0.22f, PainChance = 0.35f, Attack = AttackType.Melee,
                Range = 1.3f, MeleeRange = 1.0f, WindUp = 0.22f, CoolMin = 0.5f, CoolMax = 1.0f, DmgMin = 6, DmgMax = 14,
                Sight = Sfx.FiendSight, Pain = Sfx.GhoulPain, Death = Sfx.GhoulDeath, AttackSnd = Sfx.BruteBite, Drop = '\0',
                Frames = Art.Imp, Scale = 1f / 76, Score = 150,
            };
            Bat = new MonsterDef
            {
                // a small flier that keeps its distance and spits venom - the machine gun earns its keep here
                Name = "BAT DEMON", Code = '§', Health = 25, Speed = 2.4f, Radius = 0.2f, PainChance = 0.4f, Attack = AttackType.Projectile,
                ProjType = Projectile.SPIT, ProjSpeed = 9f, Range = 15, WindUp = 0.4f, CoolMin = 1.3f, CoolMax = 2.4f, DmgMin = 4, DmgMax = 10,
                Sight = Sfx.FiendSight, Pain = Sfx.GhoulPain, Death = Sfx.GhoulDeath, AttackSnd = Sfx.FiendThrow, Drop = '\0',
                Frames = Art.Bat, Scale = 1f / 90, Flies = true, HoverZ = 0.6f, Score = 120,
            };
            FireDemon = new MonsterDef
            {
                // a smaller Warden built around fireballs instead of rockets: a volley, a summoning scream, a nova
                Name = "ELDER FIRE DEMON", Code = '¶', Health = 1050, Speed = 1.4f, Radius = 0.5f, PainChance = 0.06f, Attack = AttackType.Projectile,
                ProjType = Projectile.FIREBALL, ProjSpeed = 7f, Range = 22, WindUp = 1.0f, CoolMin = 1.8f, CoolMax = 2.8f, DmgMin = 15, DmgMax = 32, Shots = 3,
                Sight = Sfx.BossSight, Pain = Sfx.BossPain, Death = Sfx.BossDeath, AttackSnd = Sfx.FiendThrow, Frames = Art.ElderFireDemon,
                Scale = 1f / 46, Boss = true, FireBoss = true, Drop = 'y', DropChance = 1f, Score = 3500,
            };
            Ghoul.MeasureHeight(); Fiend.MeasureHeight(); Brute.MeasureHeight(); Warden.MeasureHeight(); Imp.MeasureHeight(); Bat.MeasureHeight(); FireDemon.MeasureHeight();
        }

        public static MonsterDef For(char c)
        {
            switch (c)
            {
                case 'z': return Ghoul;
                case 'i': return Fiend;
                case 'p': return Brute;
                case 'K': return Warden;
                case '\'': return Imp;
                case '§': return Bat;         // no map characters - these two only turn up in saved games
                case '¶': return FireDemon;
            }
            return null;
        }
    }

    enum MState { Idle, Chase, WindUp, Fire, Pain, Dying, Dead }

    sealed class Monster : Actor
    {
        public MonsterDef Def;
        public MState State = MState.Idle;
        public float StateTime, Cooldown, Anim, SightTimer, StrafeTimer, GrowlTimer, StepTimer;
        public float Angle;
        public float AimX, AimY, AimZ;   // where the boss's sight laser rests, and what it fires at
        public bool Aiming, AimLocked;
        /// <summary>How long the boss holds its aim still before the rocket leaves: time enough to get out of the way.</summary>
        public const float AimLock = 0.55f;
        /// <summary>Which of the fire boss's three attacks comes next: 0 volley, 1 scream+summon, 2 self-centred nova.</summary>
        public int BossPattern = -1;   // so the first attack of the fight is the volley, not whatever (0+1)%3 would be
        public int StrafeDir = 1;
        public int ShotsLeft;
        public bool Ambush;
        public char Carries;         // an item this one always drops when it dies (a keycard, say)
        public int Interrupted;      // how many times a saw has knocked it out of an attack it was winding up
        public bool Summoned;        // called in by the Warden rather than placed on the map
        public float SlowTime;       // > 0: a beam is burning it, and it moves at less than half speed
        float attackDamage, attackTime = -1;   // damage taken within one instant (one blast, however many pellets): only a big one staggers
        float stuckTime;

        public Monster(MonsterDef d, float x, float y)
        {
            Kind = ActorKind.Monster;
            Def = d; X = x; Y = y;
            Radius = d.Radius;
            Health = d.Health;
            Solid = true; Shootable = true;
            Scale = d.Scale;
            Cooldown = 0.5f;
            if (d.Flies) Z = d.HoverZ;
        }

        public bool Alive { get { return State != MState.Dying && State != MState.Dead; } }

        public override float Height { get { return Def.Height; } }

        public override Image Sprite(World w)
        {
            var f = Def.Frames;
            switch (State)
            {
                case MState.Idle: return f[Art.WALK1];
                case MState.Chase: return f[((int)(Anim * (Def.Speed * 2.2f)) & 1) == 0 ? Art.WALK1 : Art.WALK2];
                case MState.WindUp: return f[Art.AIM];
                case MState.Fire: return f[Def.Attack == AttackType.Melee ? Art.FIRE : StateTime > 0.12f ? Art.FIRE : Art.AIM];
                case MState.Pain: return f[Art.PAIN];
                case MState.Dying:
                    int k = (int)(Anim / 0.13f);
                    return f[Math.Min(Art.DEAD, Art.DIE1 + k)];
                default: return f[Art.DEAD];
            }
        }

        public override bool Bright { get { return State == MState.Fire && Def.Attack != AttackType.Melee; } }

        public void Alert(World w)
        {
            if (State != MState.Idle) return;
            State = MState.Chase;
            Cooldown = 0.4f + (float)w.Rng.NextDouble() * 0.8f;
            Audio.PlayAt(Def.Sight, X, Y, Def.Boss ? 1.6f : 1f, Tag);
            if (Def.Boss) w.BossAwake = this;
        }

        public override void Damage(World w, float amount, Actor source, bool splash)
        {
            if (!Alive) return;
            if (Def.Boss && splash) amount *= 0.5f;   // the boss shrugs off splash damage
            Health -= amount;
            Flash = 0.6f;
            if (Health <= 0)
            {
                Die(w);
                return;
            }
            if (State == MState.Idle) Alert(w);
            // one attack is everything that lands in the same instant - all the pellets of a shotgun blast, a rocket and its
            // blast - and only an attack that takes more than half of its health staggers it. A stream of small hits never does
            bool sameAttack = w.Time == attackTime;
            float before = sameAttack ? attackDamage : 0;
            attackTime = w.Time;
            attackDamage = before + amount;
            float limit = Def.Health * 0.5f;
            bool bigHit = before <= limit && attackDamage > limit;
            // the boss cannot be staggered out of an attack once it has committed to one
            bool bossAttacking = Def.Boss && (State == MState.WindUp || State == MState.Fire);
            if (!bossAttacking && bigHit && State != MState.Fire)
            {
                State = MState.Pain;
                StateTime = Def.PainTime;
                Audio.PlayAt(Def.Pain, X, Y, 1, Tag);
            }
        }

        void Die(World w)
        {
            State = MState.Dying;
            Anim = 0;
            Solid = false;
            Shootable = false;
            Audio.PlayAt(Def.Death, X, Y, Def.Boss ? 2f : 1f, Tag);
            w.Kills++;
            w.Score += Def.Score;
            // not every corpse leaves something behind: ammo is meant to be worth looking for
            if (Carries != '\0') w.SpawnItem(Carries, X + 0.05f, Y + 0.05f, true);
            else if (Def.Drop != '\0' && w.Rng.NextDouble() < Def.DropChance) w.SpawnItem(Def.Drop, X + 0.05f, Y + 0.05f, true);
            DropForEmptyGun(w);
            if (Def.Boss) w.BossKilled(Def.Name);
        }

        /// <summary>Whoever dies leaves ammo for the gun the player is worst off for: of the weapons carried (never the ray gun,
        /// whose soul cells are rationed) the one whose ammo is lowest against its maximum. A gun that cannot fire at all always
        /// gets it; the fuller the lowest one is, the less often anything drops, and nothing does above about two thirds.</summary>
        void DropForEmptyGun(World w)
        {
            var p = w.P;
            float lowest = 2;
            var dry = new System.Collections.Generic.List<int>();
            for (int t = 0; t < Player.AmmoTypes; t++)
            {
                if (t == 3) continue;
                int need = 0;   // the fewest the cheapest gun of this type needs to fire at all
                for (int i = 0; i < Player.Weapons; i++)
                {
                    var d = WeaponDef.All[i];
                    if (!p.Has[i] || d.Ammo != t) continue;
                    int n = Math.Max(1, d.Barrels > 1 ? 1 : d.AmmoPerShot);
                    need = need == 0 ? n : Math.Min(need, n);
                }
                if (need == 0) continue;
                float frac = p.Ammo[t] < need ? 0 : (float)p.Ammo[t] / Player.MaxAmmo[t];
                if (frac < lowest - 0.001f) { lowest = frac; dry.Clear(); dry.Add(t); }
                else if (frac < lowest + 0.001f) dry.Add(t);
            }
            if (dry.Count == 0) return;
            if (lowest > 0 && w.Rng.NextDouble() >= 1 - lowest * 1.5f) return;
            char[] items = { 'c', 'e', 'q', '\0', '"' };
            w.SpawnItem(items[dry[w.Rng.Next(dry.Count)]], X - 0.12f, Y - 0.08f, true);
        }

        public override void Update(World w, float dt)
        {
            Flash = Math.Max(0, Flash - dt * 4);
            SlowTime -= dt;
            Anim += dt;
            if (Def.Flies && Alive) Z = Def.HoverZ + (float)Math.Sin(Anim * 1.7 + Tag) * 0.06f;
            var p = w.P;
            float dx = p.X - X, dy = p.Y - Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (State != MState.WindUp) Aiming = false;   // the sight laser only burns while the boss is taking aim
            switch (State)
            {
                case MState.Dying:
                    if (Anim > 0.13f * 4) State = MState.Dead;
                    return;
                case MState.Dead:
                    return;
                case MState.Idle:
                    SightTimer -= dt;
                    if (SightTimer <= 0)
                    {
                        SightTimer = 0.2f + (float)w.Rng.NextDouble() * 0.15f;
                        if (!p.Dead && CanSee(w, dist, dx, dy)) Alert(w);
                    }
                    return;
                case MState.Pain:
                    StateTime -= dt;
                    if (StateTime <= 0) State = MState.Chase;
                    return;
                case MState.WindUp:
                    Angle = (float)Math.Atan2(dy, dx);
                    StateTime -= dt;
                    if (Def.Boss && !Def.FireBoss && Def.Attack != AttackType.Melee && BossPattern == 0)
                    {
                        // the sight follows the player, then holds dead still for the last second before it fires
                        Aiming = true;
                        AimLocked = StateTime <= AimLock;
                        if (!AimLocked) { AimX = p.X; AimY = p.Y; AimZ = p.Z + 0.35f; }
                    }
                    if (StateTime <= 0)
                    {
                        PerformAttack(w, dist);
                        State = MState.Fire;
                        StateTime = Def.Attack == AttackType.Melee ? 0.3f : (Def.Boss ? 0.1f : 0.22f);
                    }
                    return;
                case MState.Fire:
                    StateTime -= dt;
                    if (StateTime <= 0)
                    {
                        if (ShotsLeft > 0 && !p.Dead && w.Map.LOS(X, Y, p.X, p.Y))
                        {
                            ShotsLeft--;
                            State = MState.WindUp;
                            StateTime = Def.Boss ? AimLock + 0.15f : 0.2f;
                        }
                        else
                        {
                            State = MState.Chase;
                            Cooldown = (Def.CoolMin + (float)w.Rng.NextDouble() * (Def.CoolMax - Def.CoolMin)) * w.Aggression;
                        }
                    }
                    return;
            }

            // ---- chase
            Cooldown -= dt;
            GrowlTimer -= dt;
            if (GrowlTimer <= 0)
            {
                GrowlTimer = 4 + (float)w.Rng.NextDouble() * 6;
                if (w.Rng.NextDouble() < 0.4) Audio.PlayAt(Def.Boss ? Sfx.BossSight : Sfx.Growl, X, Y, 0.5f, Tag);
            }
            if (p.Dead) { Anim = 0; return; }

            bool los = dist < Def.Range + 2 && w.Map.LOS(X, Y, p.X, p.Y);
            if (Cooldown <= 0 && los)
            {
                bool melee = dist < Def.MeleeRange + p.Radius;
                bool canRanged = Def.Attack != AttackType.Melee && dist < Def.Range;
                // closer targets get attacked more eagerly
                double chance = Def.Attack == AttackType.Melee ? 1 : Math.Max(0.25, 1 - dist / Def.Range);
                if (melee || (canRanged && w.Rng.NextDouble() < chance))
                {
                    State = MState.WindUp;
                    StateTime = Def.WindUp * (melee ? 0.6f : 1f);
                    ShotsLeft = Def.Shots - 1;
                    if (Def.FireBoss || Def.Attack == AttackType.Rockets)
                    {
                        // cycle through the three signature attacks - only the volley (or the rockets) is a multi-shot burst
                        BossPattern = (BossPattern + 1) % 3;
                        if (BossPattern != 0) ShotsLeft = 0;
                    }
                    Angle = (float)Math.Atan2(dy, dx);
                    return;
                }
                Cooldown = 0.3f;
            }

            // movement: straight at the player when visible and close, otherwise follow the flow field
            float tx, ty;
            if (los && dist < 7)
            {
                tx = dx / dist; ty = dy / dist;
                StrafeTimer -= dt;
                if (StrafeTimer <= 0) { StrafeTimer = 0.8f + (float)w.Rng.NextDouble() * 1.6f; StrafeDir = w.Rng.Next(3) - 1; }
                float keep = Def.Attack == AttackType.Melee ? 0 : Def.Boss ? 4.5f : 2.6f;
                if (dist < keep)
                {
                    // ranged attackers keep their distance: circle-strafe and back off a little
                    if (StrafeDir == 0) StrafeDir = 1;
                    float px = -dy / dist * StrafeDir, py = dx / dist * StrafeDir;
                    tx = px - tx * 0.6f; ty = py - ty * 0.6f;
                    float l = (float)Math.Sqrt(tx * tx + ty * ty); tx /= l; ty /= l;
                }
                else if (dist > 2.2f && Def.Attack != AttackType.Melee)
                {
                    tx += -ty * StrafeDir * 0.7f; ty += dx / dist * StrafeDir * 0.7f;
                    float l = (float)Math.Sqrt(tx * tx + ty * ty); tx /= l; ty /= l;
                }
            }
            else if (!FlowDir(w, out tx, out ty))
            {
                tx = dx / dist; ty = dy / dist;
            }

            float stop = Radius + p.Radius + 0.12f;
            if (dist > stop)
            {
                float sp = Def.Speed * dt * (SlowTime > 0 ? 0.45f : 1f);
                float ox = X, oy = Y;
                w.TryMove(this, tx * sp, ty * sp);
                float moved = (float)Math.Sqrt((X - ox) * (X - ox) + (Y - oy) * (Y - oy));
                if (moved < sp * 0.2f)
                {
                    stuckTime += dt;
                    if (stuckTime > 0.3f)
                    {
                        // wiggle sideways to get unstuck
                        stuckTime = 0;
                        StrafeDir = w.Rng.Next(2) * 2 - 1;
                        w.TryMove(this, -ty * StrafeDir * sp * 3, tx * StrafeDir * sp * 3);
                    }
                }
                else stuckTime = 0;
                Angle = (float)Math.Atan2(ty, tx);
                // open doors ahead
                int ax = (int)(X + tx * 0.7f), ay = (int)(Y + ty * 0.7f);
                var d = w.Map.DoorAt(ax, ay);
                if (d != null && d.Key == 0 && d.State != DoorState.Open && d.State != DoorState.Opening) w.OpenDoor(d, false);
                if (Def.Boss)
                {
                    StepTimer -= dt;
                    if (StepTimer <= 0) { StepTimer = 0.55f; Audio.PlayAt(Sfx.BossStep, X, Y, 0.9f, 0); w.Shake(0.5f / (1 + dist * 0.3f)); }
                }
            }
            else Anim = 0;
        }

        bool CanSee(World w, float dist, float dx, float dy)
        {
            if (dist > 26) return false;
            if (dist > 2.5f && !Ambush)
            {
                // front-facing field of view (about 200 degrees)
                float fx = (float)Math.Cos(Angle), fy = (float)Math.Sin(Angle);
                if ((dx * fx + dy * fy) / dist < -0.2f) return false;
            }
            return w.Map.LOS(X, Y, w.P.X, w.P.Y);
        }

        bool FlowDir(World w, out float tx, out float ty)
        {
            tx = ty = 0;
            var m = w.Map;
            int cx = (int)X, cy = (int)Y;
            if (!m.In(cx, cy)) return false;
            int best = m.Flow[cy * m.W + cx];
            int bx = -1, by = -1;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + Map.DX8[k], ny = cy + Map.DY8[k];
                if (!m.In(nx, ny)) continue;
                int v = m.Flow[ny * m.W + nx];
                if (v >= best) continue;
                if ((k & 1) == 1)
                {
                    // diagonal: both side cells must be open to avoid cutting corners
                    if (!m.MonsterPassable(cy * m.W + nx) || !m.MonsterPassable(ny * m.W + cx)) continue;
                }
                best = v; bx = nx; by = ny;
            }
            if (bx < 0) return false;
            float gx = bx + 0.5f - X, gy = by + 0.5f - Y;
            float l = (float)Math.Sqrt(gx * gx + gy * gy);
            if (l < 0.001f) return false;
            tx = gx / l; ty = gy / l;
            return true;
        }

        void PerformAttack(World w, float dist)
        {
            var p = w.P;
            if (p.Dead) return;
            if (Def.FireBoss && BossPattern != 0) { PerformFireBossSpecial(w); return; }
            if (Def.Attack == AttackType.Rockets && BossPattern != 0) { PerformWardenSpecial(w); return; }
            bool melee = dist < Def.MeleeRange + p.Radius;
            if (Def.Attack == AttackType.Melee || (melee && Def.MeleeChance > 0 && w.Rng.NextDouble() < Def.MeleeChance))
            {
                if (dist < Def.MeleeRange + p.Radius + 0.25f)
                {
                    Audio.PlayAt(Def.Attack == AttackType.Melee ? Sfx.BruteBite : Sfx.Punch, X, Y, 1, Tag);
                    p.Hurt(w, w.Rng.Next(Def.DmgMin, Def.DmgMax + 1), X, Y);
                }
                return;
            }
            Audio.PlayAt(Def.AttackSnd, X, Y, 1, Tag);
            float ang = (float)Math.Atan2(p.Y - Y, p.X - X);
            if (Def.Attack == AttackType.Hitscan)
            {
                w.AddMuzzleLight(X, Y);
                for (int s = 0; s < Math.Max(1, Def.Shots > 1 ? 1 : 1); s++)
                {
                    // accuracy falls off with distance and when the player moves fast
                    double speed = Math.Sqrt(p.VX * p.VX + p.VY * p.VY);
                    double hit = 0.78 - dist * 0.03 - speed * 0.06;
                    if (w.Rng.NextDouble() < Math.Max(0.1, hit)) p.Hurt(w, w.Rng.Next(Def.DmgMin, Def.DmgMax + 1), X, Y);
                    else w.SpawnPuffNear(p.X, p.Y, ang);
                }
            }
            else
            {
                // the boss fires at the spot its laser was resting on, not at wherever the player has got to since
                float tx = p.X, ty = p.Y, tz = p.Z + 0.3f;
                if (Def.Boss && Aiming) { tx = AimX; ty = AimY; tz = AimZ; }
                float aim = (float)Math.Atan2(ty - Y, tx - X);
                float spread = Def.Boss ? 0 : (float)(w.Rng.NextDouble() - 0.5) * (Def.ProjType == Projectile.BULLET ? 0.05f : 0.08f);
                int ptype = Def.Attack == AttackType.Rockets ? Projectile.ROCKET : Def.ProjType;
                var pr = new Projectile(this, X + (float)Math.Cos(aim) * (Radius + 0.1f), Y + (float)Math.Sin(aim) * (Radius + 0.1f), aim + spread,
                    Def.ProjSpeed * w.ProjSpeedMul, ptype);
                pr.DmgMin = Def.DmgMin; pr.DmgMax = Def.DmgMax;
                if (ptype == Projectile.BULLET) pr.Z = Math.Max(0.25f, Def.Height * 0.72f);   // out of the rifle, roughly chest high
                if (Def.Boss) { pr.Z = 0.55f; pr.SplashDamage = 60; pr.SplashRadius = 2.2f; }
                // these things are taller than the player: aim down at the middle of them, not over their head
                float reach = (float)Math.Sqrt((tx - X) * (tx - X) + (ty - Y) * (ty - Y));
                float travel = Math.Max(0.3f, reach) / Math.Max(1f, Def.ProjSpeed * w.ProjSpeedMul);
                pr.VZ = (tz - pr.Z) / travel;
                w.Add(pr);
            }
        }

        /// <summary>The Warden's other two attacks: a ring of flame stamped out along the ground that only a jump clears, and a
        /// call for help that brings two random demons out of the floor beside it.</summary>
        void PerformWardenSpecial(World w)
        {
            Audio.PlayAt(Def.Sight, X, Y, 1.8f, Tag);
            if (BossPattern == 1)
            {
                Audio.PlayAt(Sfx.Explode, X, Y, 1.4f, Tag);
                w.Shake(0.7f);
                w.Add(new FlameRing(w, this, 22, 34));
                w.Message("THE GROUND BURNS - JUMP!", Col.Rgb(255, 150, 60));
                return;
            }
            w.Shake(0.5f);
            // two of any of the ordinary demons, at random - though never more than four of its summons standing at once
            int alive = 0;
            foreach (var a in w.Actors) { var m = a as Monster; if (m != null && m.Alive && m.Summoned) alive++; }
            var pool = new[] { MonsterDef.Ghoul, MonsterDef.Fiend, MonsterDef.Brute, MonsterDef.Imp, MonsterDef.Bat };
            for (int i = 0; i < 2 && alive < 4; i++)
            {
                for (int tries = 0; tries < 12; tries++)
                {
                    float a = (float)(w.Rng.NextDouble() * Math.PI * 2);
                    float r = 2.2f + (float)w.Rng.NextDouble() * 1.8f;
                    float sx = X + (float)Math.Cos(a) * r, sy = Y + (float)Math.Sin(a) * r;
                    int cx = (int)sx, cy = (int)sy;
                    if (!w.Map.In(cx, cy) || w.Map.BlocksMove(cx, cy) || !w.Map.LOS(X, Y, sx, sy)) continue;
                    var brute = new Monster(pool[w.Rng.Next(pool.Length)], sx, sy);
                    brute.Summoned = true;
                    brute.Alert(w);
                    w.Add(brute);
                    w.Explode(sx, sy, 0.3f, 0, 0.5f, this);
                    alive++;
                    break;
                }
            }
        }

        /// <summary>The Elder Fire Demon's other two attacks: a bat-summoning scream, and a self-centred fire nova.</summary>
        void PerformFireBossSpecial(World w)
        {
            if (BossPattern == 1)
            {
                Audio.PlayAt(Def.Sight, X, Y, 1.8f, Tag);
                w.Shake(0.5f);
                for (int i = 0; i < 3; i++)
                {
                    float a = (float)(w.Rng.NextDouble() * Math.PI * 2);
                    float r = 2.2f + (float)w.Rng.NextDouble() * 1.6f;
                    float sx = X + (float)Math.Cos(a) * r, sy = Y + (float)Math.Sin(a) * r;
                    int cx = (int)sx, cy = (int)sy;
                    if (!w.Map.In(cx, cy) || w.Map.BlocksMove(cx, cy)) continue;
                    var bat = new Monster(MonsterDef.Bat, sx, sy);
                    bat.Alert(w);
                    w.Add(bat);
                }
            }
            else
            {
                Audio.PlayAt(Def.AttackSnd, X, Y, 1.4f, Tag);
                w.Explode(X, Y, 0.5f, Def.DmgMax * 1.5f, 4.5f, this);
            }
        }
    }

    // ================================================================ items and decorations

    sealed class Item : Actor
    {
        public char Code;
        public bool Dropped;
        public Item(char c, float x, float y)
        {
            Kind = ActorKind.Item;
            Code = c; X = x; Y = y;
            Radius = 0.25f;
            // pickups are drawn at a fixed height in the world, whatever the size of their artwork,
            // so that everything worth walking over is big enough to notice
            Image im;
            if (Art.Items.TryGetValue(c, out im) && im != null && im.H > 0) Scale = WorldHeight(c) / im.H;
        }

        static float WorldHeight(char c)
        {
            switch (c)
            {
                case 'S': case 'N': case 'L': case 'W': case 'g':
                case '5': case '6': case '7': case '8': case '9': return 0.24f;    // weapons (they sit on a pedestal)
                case 'r': case 'b': case 'y': return 0.33f;                        // keycards
                case '{': return 0.3f;                                             // the boarding pass
                case 'o': case 'U': case 'G': return 0.45f;                        // soul orb and armour
                case 'm': case 'C': case 'E': case 'Q': return 0.32f;              // the big boxes
                default: return 0.25f;                                             // stimpacks, clips, shells, bonuses
            }
        }

        public override Image Sprite(World w)
        {
            if (Code == 'o') return Art.Soulsphere[((int)(w.Time * 6)) & 1];
            Image im;
            return Art.Items.TryGetValue(Code, out im) ? im : Art.Items['h'];
        }

        /// <summary>Pickups are lit by themselves: in a dark room you can still see what is worth collecting.</summary>
        public override bool Bright { get { return true; } }

        public override void Update(World w, float dt)
        {
            if (Code == 'o' || Code == 'r' || Code == 'b' || Code == 'y' || Code == '+' || Code == '{')
                Z = 0.06f + 0.05f * (float)Math.Sin(w.Time * 3 + Tag);
        }
    }

    sealed class Decor : Actor
    {
        public Image Img;
        public Image[] Anim;
        public bool Glows;
        public string[] Lines;    // if set, pressing E while facing this one shows a random line of chatter

        public Decor(Image img, float x, float y, bool solid, float radius)
        {
            Kind = ActorKind.Decor;
            Img = img; X = x; Y = y; Solid = solid; Radius = radius;
        }

        public override Image Sprite(World w)
        {
            if (Anim != null) return Anim[((int)(w.Time * 10 + Tag)) % Anim.Length];
            return Img;
        }

        public override bool Bright { get { return Glows; } }
    }

    /// <summary>The sign hanging from the ceiling of the calm terminal, its text sliding by.</summary>
    sealed class Marquee : Actor
    {
        public Marquee(float x, float y)
        {
            Kind = ActorKind.Decor;
            X = x; Y = y; Solid = false; Radius = 0.2f;
            Scale = 1f / 42;
            Z = 0.68f;
        }

        public override Image Sprite(World w) { return Art.MarqueeFrame(w.Time); }
        public override bool Bright { get { return true; } }
    }

    sealed class Barrel : Actor
    {
        float fuse = -1;
        Actor lastHit;

        public Barrel(float x, float y)
        {
            Kind = ActorKind.Barrel;
            X = x; Y = y; Radius = 0.26f; Solid = true; Shootable = true; Health = 20;
        }

        public override Image Sprite(World w) { return Art.Barrel; }

        public override float Height { get { return Art.Barrel.H * Scale; } }

        public override void Damage(World w, float amount, Actor source, bool splash)
        {
            if (fuse >= 0) return;
            Health -= amount;
            lastHit = source;
            if (Health <= 0) fuse = splash ? 0.12f + (float)w.Rng.NextDouble() * 0.1f : 0.05f;
        }

        public override void Update(World w, float dt)
        {
            if (fuse < 0) return;
            fuse -= dt;
            if (fuse <= 0)
            {
                Remove = true;
                Shootable = false;
                w.Add(new Decor(Art.BarrelDead, X, Y, false, 0.2f));
                w.Explode(X, Y, 0.35f, 110, 2.8f, this);
            }
        }
    }

    // ================================================================ projectiles and effects

    sealed class Projectile : Actor
    {
        public const int FIREBALL = 0, ROCKET = 1, BULLET = 2, RAY = 3, SPIT = 4, GRENADE = 5;

        public Actor Owner;
        public float VX, VY, VZ;  // VZ: climb rate (the player's rockets follow the look angle)
        public int Type;
        public int DmgMin = 8, DmgMax = 20;
        public float SplashDamage, SplashRadius;
        public float Fuse;        // grenades only: explodes on its own once this reaches zero
        float life = 8;

        public Projectile(Actor owner, float x, float y, float angle, float speed, int type)
        {
            Kind = ActorKind.Projectile;
            Owner = owner; X = x; Y = y; Type = type;
            VX = (float)Math.Cos(angle) * speed; VY = (float)Math.Sin(angle) * speed;
            Radius = type == BULLET ? 0.08f : type == SPIT ? 0.1f : type == GRENADE ? 0.14f : 0.12f;
            if (type == GRENADE) Scale = 1f / 64;
            Z = 0.4f;
        }

        public override Image Sprite(World w)
        {
            int f = ((int)(w.Time * 12 + Tag)) & 1;
            switch (Type)
            {
                case ROCKET: return Art.Rocket[f];
                case BULLET: return Art.Bullet[f];
                case RAY: return Art.RayBolt[f];
                case SPIT: return Art.Spit[f];
                case GRENADE: return Art.Grenade[(((int)spin) & 3) + (FuseLit(w) ? 4 : 0)];
                default: return Art.Fireball[f];
            }
        }

        public override bool Bright { get { return true; } }

        public override void Update(World w, float dt)
        {
            life -= dt;
            if (life <= 0) { Remove = true; return; }
            if (Type == GRENADE) { UpdateGrenade(w, dt); return; }
            float speed = (float)Math.Sqrt(VX * VX + VY * VY);
            int steps = Math.Max(1, (int)(speed * dt / 0.08f) + 1);
            float sx = VX * dt / steps, sy = VY * dt / steps, sz = VZ * dt / steps;
            for (int i = 0; i < steps; i++)
            {
                X += sx; Y += sy; Z += sz;
                int cx = (int)Math.Floor(X), cy = (int)Math.Floor(Y);
                if (w.Map.BlocksMove(cx, cy))
                {
                    X -= sx * 0.6f; Y -= sy * 0.6f;
                    Impact(w, null);
                    return;
                }
                // Z is the bottom of the sprite; its centre is about 0.1 higher
                if (Z + 0.1f < 0.03f || (Z + 0.1f > 0.97f && !w.Map.IsOutdoor(cx, cy)))
                {
                    Z = Math.Max(0, Math.Min(0.85f, Z));
                    Impact(w, null);
                    return;
                }
                if (w.TryParry(this)) return;
                var hit = w.ProjectileHit(this);
                if (hit != null) { Impact(w, hit); return; }
            }
            if (Type == ROCKET && w.Rng.NextDouble() < 0.6)
                w.Add(new Effect(Art.Puff, X - VX * 0.02f, Y - VY * 0.02f, Z + 0.02f, 0.3f));
            // (a bullet carries no light of its own: a moving light repaints half the screen for very little gain)
            if (Type == RAY) w.AddLight(X, Y, 3.0f, 1.0f, 0.25f, 0.75f);
            else if (Type == BULLET) { }
            else w.AddLight(X, Y, Type == FIREBALL ? 2.6f : 2.2f, 1.0f, 0.45f, 0.12f);
        }

        // a grenade leaves the launcher at this speed, along the look direction plus this much lift: a low lob that
        // drops a few metres out and skips along the floor, not a mortar shot at the ceiling
        public const float GrenadeSpeed = 10.5f, GrenadeLift = 1.5f;
        const float GrenadeGravity = 7.5f, GrenadeBounce = 0.56f, GrenadeWall = 0.65f, GrenadeRoll = 5.0f;
        public int Bounces;       // how many times it has come off the floor (the tests count them)
        float spin, trailTimer;

        /// <summary>The grenade launcher's alt-fire: arcs out, bounces off the floor, the walls and the ceiling, rolls to a
        /// stop, and goes off when its fuse runs out - or at once if it touches a monster on the way.</summary>
        void UpdateGrenade(World w, float dt)
        {
            Fuse -= dt;
            float speed = (float)Math.Sqrt(VX * VX + VY * VY);
            int steps = Math.Max(1, (int)((speed + Math.Abs(VZ)) * dt / 0.05f) + 1);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                bool rolling = Z <= 0 && VZ == 0;
                if (!rolling) VZ -= GrenadeGravity * h;
                // one axis at a time, so it glances off a wall's face instead of stopping dead against it
                float nx = X + VX * h;
                if (w.Map.BlocksMove((int)Math.Floor(nx), (int)Math.Floor(Y))) { Clink(w, Math.Abs(VX)); VX = -VX * GrenadeWall; }
                else X = nx;
                float ny = Y + VY * h;
                if (w.Map.BlocksMove((int)Math.Floor(X), (int)Math.Floor(ny))) { Clink(w, Math.Abs(VY)); VY = -VY * GrenadeWall; }
                else Y = ny;
                if (!rolling)
                {
                    Z += VZ * h;
                    if (Z <= 0)
                    {
                        Z = 0;
                        if (VZ < -0.6f)
                        {
                            Clink(w, -VZ);
                            VZ = -VZ * GrenadeBounce;
                            VX *= 0.82f; VY *= 0.82f;
                            Bounces++;
                        }
                        else VZ = 0;   // too slow to come off the floor again: from here on it rolls
                    }
                    else if (Z + 0.14f > 0.98f && !w.Map.IsOutdoor((int)X, (int)Y))
                    {
                        Z = 0.84f;
                        if (VZ > 0) { Clink(w, VZ); VZ = -VZ * 0.5f; }
                    }
                }
                else
                {
                    float sp = (float)Math.Sqrt(VX * VX + VY * VY);
                    if (sp > 0) { float ns = Math.Max(0, sp - GrenadeRoll * h); VX *= ns / sp; VY *= ns / sp; }
                }
                var hit = w.ProjectileHit(this);
                if (hit != null) { Impact(w, hit); return; }
            }
            spin += speed * dt * 2.2f;
            if (Fuse <= 0) { Impact(w, null); return; }
            // a thin smoke trail while it is in the air, and the fuse light blinking faster as it runs down
            trailTimer -= dt;
            if (trailTimer <= 0 && Z > 0.03f)
            {
                trailTimer = 0.05f;
                var e = new Effect(Art.Puff, X, Y, Z, 0.35f);
                e.Scale = 1f / 130;
                e.VZ = 0.15f;
                w.Add(e);
            }
            if (FuseLit(w)) w.AddLight(X, Y, 1.6f, 1.2f, 0.25f, 0.12f);
        }

        /// <summary>The fuse light on the grenade: slow blinks at first, then faster and faster as it runs out.</summary>
        bool FuseLit(World w)
        {
            float period = 0.07f + Math.Max(0, Fuse) * 0.12f;
            return ((int)(w.Time / period + Tag)) % 2 == 0;
        }

        void Clink(World w, float speed)
        {
            if (speed > 0.5f) Audio.PlayAt(Sfx.GrenadeBounce, X, Y, Math.Min(0.9f, 0.25f + speed * 0.12f), 0);
        }

        /// <summary>Knocked back by the player's fist: it flies home, faster and angrier.</summary>
        public void Parried(World w, Player p)
        {
            float speed = (float)Math.Sqrt(VX * VX + VY * VY) * 1.4f;
            float ang = p.Angle;
            VX = (float)Math.Cos(ang) * speed;
            VY = (float)Math.Sin(ang) * speed;
            VZ = p.AimSlope * speed * 0.5f;
            Owner = p;
            DmgMin = (int)(DmgMin * 1.5f);
            DmgMax = (int)(DmgMax * 1.5f);
            SplashDamage *= 1.3f;
            life = Math.Max(life, 3);
            Audio.Play(Sfx.Parry, 0.9f, 0, 1, 0);
            p.ShakeAmt = Math.Min(1, p.ShakeAmt + 0.22f);
            p.GrinTime = 1.2f;
            w.Score += 50;
            w.AddLight(X, Y, 3.2f, 1f, 0.9f, 0.55f);
            var e = new Effect(Art.Explosion, X, Y, Z, 0.22f);
            e.Scale = 1f / 150;
            e.Glow = true;
            e.Light = 1.6f;
            w.Add(e);
            w.Message("PARRY!", Col.Rgb(255, 230, 140));
        }

        void Impact(World w, Actor hit)
        {
            Remove = true;
            if (hit != null)
            {
                int dmg = w.Rng.Next(DmgMin, DmgMax + 1);
                if (hit == (Actor)w.P) w.P.Hurt(w, dmg, X - VX, Y - VY);
                else hit.Damage(w, dmg, Owner, false);
            }
            if (Type == BULLET)
            {
                w.SpawnPuff(X, Y, Z);
                Audio.PlayAt(Sfx.FireHit, X, Y, 0.3f, 0);
            }
            else if (Type == ROCKET || SplashRadius > 0)
            {
                w.Explode(X, Y, Z, SplashDamage > 0 ? SplashDamage : 100, SplashRadius > 0 ? SplashRadius : 2.6f, Owner);
            }
            else
            {
                Audio.PlayAt(Sfx.FireHit, X, Y);
                var e = new Effect(Art.Explosion, X, Y, Z - 0.1f, 0.35f);
                e.Scale = 1f / 110;
                e.Glow = true;
                e.Light = 2.2f;
                w.Add(e);
            }
        }
    }

    sealed class Effect : Actor
    {
        public Image[] Frames;
        public float Life, Age;
        public float VX, VY, VZ, Gravity;
        public bool Glow;
        public float Light;
        public float LR = 1.4f, LG = 0.7f, LB = 0.25f;   // the colour of that light (a fiery orange unless told otherwise)

        public Effect(Image[] frames, float x, float y, float z, float life)
        {
            Kind = ActorKind.Effect;
            Frames = frames; X = x; Y = y; Z = z; Life = life;
        }

        public override Image Sprite(World w)
        {
            int f = (int)(Age / Life * Frames.Length);
            if (f >= Frames.Length) f = Frames.Length - 1;
            return Frames[f];
        }

        public override bool Bright { get { return Glow; } }

        public override void Update(World w, float dt)
        {
            Age += dt;
            if (Age >= Life) { Remove = true; return; }
            if (VX != 0 || VY != 0 || VZ != 0 || Gravity != 0)
            {
                VZ -= Gravity * dt;
                float nx = X + VX * dt, ny = Y + VY * dt;
                if (!w.Map.BlocksMove((int)nx, (int)ny)) { X = nx; Y = ny; } else { VX = VY = 0; }
                Z += VZ * dt;
                if (Z < 0) { Z = 0; VX *= 0.5f; VY *= 0.5f; VZ = 0; }
            }
            if (Light > 0)
            {
                float k = 1 - Age / Life;
                w.AddLight(X, Y, Light * (0.6f + k * 0.4f), LR * k, LG * k, LB * k);
            }
        }
    }

    /// <summary>
    /// The laser ray's shot: a flat arc of light that sweeps outwards from where the player stood, about seventy degrees
    /// wide, and burns every body its edge passes over - not just the first one - until the walls stop it. It has no
    /// sprite of its own: each frame it lays a line of short-lived motes along its leading edge.
    /// </summary>
    /// <summary>
    /// The Warden's ground fire: a ring that spreads outwards from where it stood with no limit but the walls - each
    /// direction goes on until it meets one - and burns anything standing on the floor as it passes. Jumping clears it.
    /// It has no sprite: it lays short-lived flames along itself as it goes.
    /// </summary>
    sealed class FlameRing : Actor
    {
        public const float Speed = 4.4f, MaxRange = 80f, ClearHeight = 0.15f;
        const int Samples = 288;
        readonly float[] wall = new float[Samples];
        readonly float farthest;
        readonly int dmgMin, dmgMax;
        float r = 0.9f, emit;
        bool hitPlayer;

        public FlameRing(World w, Actor owner, int dmgMin, int dmgMax)
        {
            Kind = ActorKind.Effect;
            X = owner.X; Y = owner.Y;
            this.dmgMin = dmgMin; this.dmgMax = dmgMax;
            for (int k = 0; k < Samples; k++)
            {
                float a = (float)(2 * Math.PI * k / Samples);
                wall[k] = w.Map.RayCast(X, Y, (float)Math.Cos(a), (float)Math.Sin(a), MaxRange);
            }
            // a pillar stops the fire, and leaves a shadow behind it the width of the pillar
            foreach (var act in w.Actors)
            {
                var pil = act as Decor;
                if (pil == null || !pil.Solid || pil.Img != Art.Pillar) continue;
                float px = pil.X - X, py = pil.Y - Y;
                float pd = (float)Math.Sqrt(px * px + py * py);
                if (pd < 0.3f) continue;
                float bearing = (float)Math.Atan2(py, px), half = (float)Math.Atan2(0.36f, pd);
                for (int k = 0; k < Samples; k++)
                {
                    float da = (float)(2 * Math.PI * k / Samples) - bearing;
                    while (da > Math.PI) da -= (float)(2 * Math.PI);
                    while (da < -Math.PI) da += (float)(2 * Math.PI);
                    if (Math.Abs(da) <= half) wall[k] = Math.Min(wall[k], Math.Max(0.1f, pd - 0.3f));
                }
            }
            for (int k = 0; k < Samples; k++) farthest = Math.Max(farthest, wall[k]);
        }

        float WallAt(float a)
        {
            float t = a / (float)(2 * Math.PI);
            t -= (float)Math.Floor(t);
            int k0 = (int)(t * Samples) % Samples, k1 = (k0 + 1) % Samples;
            return Math.Min(wall[k0], wall[k1]);
        }

        public override Image Sprite(World w) { return null; }

        public override void Update(World w, float dt)
        {
            r += Speed * dt;
            var p = w.P;
            if (!p.Dead && !hitPlayer)
            {
                float dx = p.X - X, dy = p.Y - Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (Math.Abs(d - r) < 0.42f && d < WallAt((float)Math.Atan2(dy, dx)) && p.Z < ClearHeight)
                {
                    hitPlayer = true;
                    p.Hurt(w, w.Rng.Next(dmgMin, dmgMax + 1), X, Y);
                }
            }
            emit -= dt;
            if (emit <= 0)
            {
                emit = 0.06f;
                int n = Math.Max(24, Math.Min(360, (int)(2 * Math.PI * r / 0.55f)));
                float jitter = (float)(w.Rng.NextDouble() * 2 * Math.PI / n);
                for (int i = 0; i < n; i++)
                {
                    float a = (float)(2 * Math.PI * i / n) + jitter;
                    if (r >= WallAt(a)) continue;
                    float fx = X + (float)Math.Cos(a) * r, fy = Y + (float)Math.Sin(a) * r;
                    float px = fx - p.X, py = fy - p.Y;
                    if (px * px + py * py > 16 * 16) continue;
                    var fl = new Effect(Art.Fireball, fx, fy, 0, 0.24f);
                    fl.Scale = 1f / 70;
                    fl.Glow = true;
                    fl.VZ = 0.25f;
                    if ((i & 7) == 0) fl.Light = 2.6f;
                    w.Add(fl);
                }
            }
            if (r > farthest || r > MaxRange) Remove = true;
        }
    }

    sealed class LaserArc : Actor
    {
        public const float HalfAngle = 0.62f, Speed = 15f, Range = 18f;
        const int Samples = 33;
        readonly float oz, ang;
        readonly float[] wall = new float[Samples];
        readonly bool[] stopped = new bool[Samples];
        public readonly System.Collections.Generic.List<Actor> Hit = new System.Collections.Generic.List<Actor>();
        readonly Actor owner;
        readonly int dmgMin, dmgMax;
        float r = 0.35f;

        public LaserArc(World w, Player p, int dmgMin, int dmgMax)
        {
            Kind = ActorKind.Effect;
            owner = p; X = p.X; Y = p.Y; oz = p.EyeZ - 0.12f; ang = p.Angle;
            this.dmgMin = dmgMin; this.dmgMax = dmgMax;
            for (int k = 0; k < Samples; k++)
            {
                float a = ang - HalfAngle + 2 * HalfAngle * k / (Samples - 1);
                wall[k] = w.Map.RayCast(X, Y, (float)Math.Cos(a), (float)Math.Sin(a), Range);
            }
        }

        /// <summary>How far the arc gets in a direction before a wall stops it (the nearer of the two samples either side,
        /// so it never bleeds round the corner of a doorway).</summary>
        float WallAt(float a)
        {
            float k = (a - (ang - HalfAngle)) / (2 * HalfAngle) * (Samples - 1);
            int k0 = Math.Max(0, Math.Min(Samples - 1, (int)Math.Floor(k))), k1 = Math.Min(Samples - 1, k0 + 1);
            return Math.Min(wall[k0], wall[k1]);
        }

        public override Image Sprite(World w) { return null; }

        public override void Update(World w, float dt)
        {
            r += Speed * dt;
            // everything the edge has swept over, if it is in front of the player and has a clear line back to them
            foreach (var a in w.Actors)
            {
                if (!a.Shootable || a == owner || Hit.Contains(a)) continue;
                float dx = a.X - X, dy = a.Y - Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist - a.Radius > r || dist - a.Radius > Range) continue;
                float da = (float)Math.Atan2(dy, dx) - ang;
                while (da > Math.PI) da -= (float)(2 * Math.PI);
                while (da < -Math.PI) da += (float)(2 * Math.PI);
                if (Math.Abs(da) > HalfAngle + (float)Math.Atan2(a.Radius, Math.Max(0.3f, dist))) continue;
                if (!w.Map.LOS(X, Y, a.X, a.Y)) continue;
                Hit.Add(a);
                // the slicer's edge cuts deepest up close: double damage at point blank, easing to the plain figure by four tiles
                a.Damage(w, w.Rng.Next(dmgMin, dmgMax + 1) * (1f + Math.Max(0f, 1f - (dist - a.Radius) / 4f)), owner, false);
                var flare = new Effect(Art.ArcMote, a.X, a.Y, Math.Max(0, oz - 0.12f), 0.22f);
                flare.Scale = 1f / 36;
                flare.Glow = true;
                w.Add(flare);
            }
            // the edge itself: a line of motes, with a fainter one just behind it so it reads as a band of light
            float spacing = Math.Max(0.14f, Math.Min(0.4f, r * 0.05f));
            int n = Math.Max(8, (int)(2 * HalfAngle * r / spacing));
            bool alive = false;
            for (int i = 0; i <= n; i++)
            {
                float a = ang - HalfAngle + 2 * HalfAngle * i / n;
                float reach = WallAt(a);
                if (r >= reach) continue;
                alive = true;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                var m = new Effect(Art.ArcMote, X + ca * r, Y + sa * r, oz, 0.07f);
                m.Scale = 1f / 150;
                m.Glow = true;
                w.Add(m);
                if (r > 1.2f && (i & 1) == 0)
                {
                    var tail = new Effect(Art.ArcMote, X + ca * (r - 0.3f), Y + sa * (r - 0.3f), oz + 0.01f, 0.045f);
                    tail.Scale = 1f / 230;
                    tail.Glow = true;
                    w.Add(tail);
                }
            }
            // sparks where it hits the walls
            for (int k = 0; k < Samples; k += 2)
            {
                if (stopped[k] || r < wall[k]) continue;
                stopped[k] = true;
                if (wall[k] >= Range - 0.1f) continue;
                float a = ang - HalfAngle + 2 * HalfAngle * k / (Samples - 1);
                var sp = new Effect(Art.ArcSpark, X + (float)Math.Cos(a) * (wall[k] - 0.08f), Y + (float)Math.Sin(a) * (wall[k] - 0.08f), oz, 0.3f);
                sp.VZ = 0.6f;
                sp.Gravity = 3;
                sp.Scale = 1f / 70;
                sp.Glow = true;
                w.Add(sp);
            }
            float mid = Math.Min(r, WallAt(ang) - 0.2f);
            if (mid > 0) w.AddLight(X + (float)Math.Cos(ang) * mid, Y + (float)Math.Sin(ang) * mid, 3.6f, 0.35f, 1.0f, 1.6f);
            if (!alive || r > Range) Remove = true;
        }
    }
}
