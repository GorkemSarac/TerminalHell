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
        public Image[] Frames;
        public float MeleeChance;   // chance a ranged monster claws when close
        public bool Boss;
        public float Height = 0.9f; // from the sprite: floor to top of the head

        void MeasureHeight()
        {
            var img = Frames[Art.WALK1];
            int top = img.H;
            foreach (int t in img.ColTop) top = Math.Min(top, t);
            Height = (img.H - top) * Scale;
        }

        public static MonsterDef Ghoul, Fiend, Brute, Warden;

        public static void Build()
        {
            Ghoul = new MonsterDef
            {
                Name = "GHOUL", Code = 'z', Health = 30, Speed = 1.35f, Radius = 0.3f, PainChance = 0.7f, Attack = AttackType.Hitscan,
                Range = 16, WindUp = 0.65f, CoolMin = 2.0f, CoolMax = 3.8f, DmgMin = 3, DmgMax = 10, Shots = 1,
                Sight = Sfx.GhoulSight, Pain = Sfx.GhoulPain, Death = Sfx.GhoulDeath, AttackSnd = Sfx.GhoulShot, Drop = 'c', Frames = Art.Ghoul,
            };
            Fiend = new MonsterDef
            {
                Name = "FIEND", Code = 'i', Health = 60, Speed = 1.6f, Radius = 0.32f, PainChance = 0.6f, Attack = AttackType.Projectile,
                Range = 18, WindUp = 0.75f, CoolMin = 2.2f, CoolMax = 4.2f, DmgMin = 8, DmgMax = 20, ProjSpeed = 6.0f,
                Sight = Sfx.FiendSight, Pain = Sfx.FiendPain, Death = Sfx.FiendDeath, AttackSnd = Sfx.FiendThrow, Frames = Art.Fiend, MeleeChance = 0.8f,
            };
            Brute = new MonsterDef
            {
                Name = "BRUTE", Code = 'p', Health = 150, Speed = 2.5f, Radius = 0.42f, PainChance = 0.45f, Attack = AttackType.Melee,
                Range = 1.4f, MeleeRange = 1.25f, WindUp = 0.5f, CoolMin = 0.9f, CoolMax = 1.6f, DmgMin = 10, DmgMax = 28,
                Sight = Sfx.BruteSight, Pain = Sfx.BrutePain, Death = Sfx.BruteDeath, AttackSnd = Sfx.BruteBite, Frames = Art.Brute,
            };
            Warden = new MonsterDef
            {
                Name = "WARDEN", Code = 'K', Health = 1400, Speed = 1.3f, Radius = 0.62f, PainChance = 0.08f, Attack = AttackType.Rockets,
                Range = 30, WindUp = 0.9f, CoolMin = 1.8f, CoolMax = 3.0f, DmgMin = 30, DmgMax = 60, Shots = 2, ProjSpeed = 9,
                Sight = Sfx.BossSight, Pain = Sfx.BossPain, Death = Sfx.BossDeath, AttackSnd = Sfx.Rocket, Frames = Art.Warden,
                Scale = 1f / 58, Boss = true, Drop = 'y',
            };
            Ghoul.MeasureHeight(); Fiend.MeasureHeight(); Brute.MeasureHeight(); Warden.MeasureHeight();
        }

        public static MonsterDef For(char c)
        {
            switch (c)
            {
                case 'z': return Ghoul;
                case 'i': return Fiend;
                case 'p': return Brute;
                case 'K': return Warden;
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
        public int StrafeDir = 1;
        public int ShotsLeft;
        public bool Ambush;
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
            if (w.Rng.NextDouble() < Def.PainChance && State != MState.Fire)
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
            if (Def.Drop != '\0') w.SpawnItem(Def.Drop, X + 0.05f, Y + 0.05f, true);
            if (Def.Boss) w.BossKilled();
        }

        public override void Update(World w, float dt)
        {
            Flash = Math.Max(0, Flash - dt * 4);
            Anim += dt;
            var p = w.P;
            float dx = p.X - X, dy = p.Y - Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
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
                    if (StateTime <= 0)
                    {
                        PerformAttack(w, dist);
                        State = MState.Fire;
                        StateTime = Def.Attack == AttackType.Melee ? 0.3f : 0.22f;
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
                            StateTime = Def.Boss ? 0.45f : 0.2f;
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
                float sp = Def.Speed * dt;
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
                float spread = (float)(w.Rng.NextDouble() - 0.5) * 0.08f;
                var pr = new Projectile(this, X + (float)Math.Cos(ang) * (Radius + 0.1f), Y + (float)Math.Sin(ang) * (Radius + 0.1f), ang + spread,
                    Def.ProjSpeed * w.ProjSpeedMul, Def.Attack == AttackType.Rockets ? 1 : 0);
                pr.DmgMin = Def.DmgMin; pr.DmgMax = Def.DmgMax;
                if (Def.Boss) { pr.Z = 0.55f; pr.SplashDamage = 60; pr.SplashRadius = 2.2f; }
                w.Add(pr);
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
        }

        public override Image Sprite(World w)
        {
            if (Code == 'o') return Art.Soulsphere[((int)(w.Time * 6)) & 1];
            Image im;
            return Art.Items.TryGetValue(Code, out im) ? im : Art.Items['h'];
        }

        public override bool Bright { get { return Code == 'o' || Code == 'r' || Code == 'b' || Code == 'y' || Code == '+'; } }

        public override void Update(World w, float dt)
        {
            if (Code == 'o' || Code == 'r' || Code == 'b' || Code == 'y' || Code == '+')
                Z = 0.06f + 0.05f * (float)Math.Sin(w.Time * 3 + Tag);
        }
    }

    sealed class Decor : Actor
    {
        public Image Img;
        public Image[] Anim;
        public bool Glows;

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
        public Actor Owner;
        public float VX, VY, VZ;  // VZ: climb rate (the player's rockets follow the look angle)
        public int Type;          // 0 fireball, 1 rocket
        public int DmgMin = 8, DmgMax = 20;
        public float SplashDamage, SplashRadius;
        float life = 8;

        public Projectile(Actor owner, float x, float y, float angle, float speed, int type)
        {
            Kind = ActorKind.Projectile;
            Owner = owner; X = x; Y = y; Type = type;
            VX = (float)Math.Cos(angle) * speed; VY = (float)Math.Sin(angle) * speed;
            Radius = 0.12f;
            Z = 0.4f;
        }

        public override Image Sprite(World w)
        {
            int f = ((int)(w.Time * 12 + Tag)) & 1;
            return Type == 0 ? Art.Fireball[f] : Art.Rocket[f];
        }

        public override bool Bright { get { return true; } }

        public override void Update(World w, float dt)
        {
            life -= dt;
            if (life <= 0) { Remove = true; return; }
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
                var hit = w.ProjectileHit(this);
                if (hit != null) { Impact(w, hit); return; }
            }
            if (Type == 1 && w.Rng.NextDouble() < 0.6)
                w.Add(new Effect(Art.Puff, X - VX * 0.02f, Y - VY * 0.02f, Z + 0.02f, 0.3f));
            w.AddLight(X, Y, Type == 0 ? 2.6f : 2.2f, 1.0f, 0.45f, 0.12f);
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
            if (Type == 1 || SplashRadius > 0)
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
                w.AddLight(X, Y, Light * (0.6f + k * 0.4f), 1.4f * k, 0.7f * k, 0.25f * k);
            }
        }
    }
}
