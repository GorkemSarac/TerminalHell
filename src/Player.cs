// TERMINAL HELL - the player: movement, health/armor, inventory and weapons.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    sealed class WeaponDef
    {
        public string Name;
        public int Slot;
        public int Ammo = -1;        // -1 none, 0 bullets, 1 shells, 2 rockets, 3 soul cells
        public float Cooldown;
        public int Pellets = 1;
        public float Spread;
        public int DmgMin, DmgMax;
        public bool Melee, Rocket, Ray;
        public Sfx Sound;
        public float Anim;           // length of the firing animation

        public static readonly WeaponDef[] All =
        {
            new WeaponDef { Name = "FIST", Slot = 1, Cooldown = 0.42f, DmgMin = 8, DmgMax = 22, Melee = true, Sound = Sfx.Punch, Anim = 0.42f },
            new WeaponDef { Name = "PISTOL", Slot = 2, Ammo = 0, Cooldown = 0.34f, Spread = 0.02f, DmgMin = 10, DmgMax = 16, Sound = Sfx.Pistol, Anim = 0.25f },
            new WeaponDef { Name = "SHOTGUN", Slot = 3, Ammo = 1, Cooldown = 0.95f, Pellets = 7, Spread = 0.085f, DmgMin = 7, DmgMax = 14, Sound = Sfx.Shotgun, Anim = 0.9f },
            new WeaponDef { Name = "MINIGUN", Slot = 4, Ammo = 0, Cooldown = 0.105f, Spread = 0.04f, DmgMin = 10, DmgMax = 15, Sound = Sfx.Chaingun, Anim = 0.1f },
            new WeaponDef { Name = "LAUNCHER", Slot = 5, Ammo = 2, Cooldown = 0.8f, DmgMin = 40, DmgMax = 80, Rocket = true, Sound = Sfx.Rocket, Anim = 0.5f },
            new WeaponDef { Name = "RAY GUN", Slot = 6, Ammo = 3, Cooldown = 0.9f, DmgMin = 90, DmgMax = 140, Ray = true, Sound = Sfx.RayGun, Anim = 0.5f },
        };
    }

    struct PlayerInput
    {
        public float Forward, Strafe, Turn, Look;
        public bool Run, Fire, Use, Jump, Parry;
        public int SelectSlot;     // 1..6, 0 none
        public int Cycle;          // -1 / +1 from the mouse wheel
    }

    sealed class Player : Actor
    {
        public const int Weapons = 6, AmmoTypes = 4;

        public float Angle, Pitch, VX, VY;
        public int HP = 100, Armor, ArmorType;
        public readonly int[] Ammo = new int[AmmoTypes];
        public static readonly int[] MaxAmmo = { 200, 50, 50, 12 };
        public static readonly string[] AmmoNames = { "BULL", "SHEL", "RCKT", "SOUL" };
        public readonly bool[] Has = new bool[Weapons];
        public readonly bool[] Keys = new bool[5];      // 1 red, 2 blue, 3 yellow, 4 boarding pass
        public int Weapon = 1, Pending = -1, LastWeapon = 0;
        public bool Dead;
        public float DeadTime;
        public float Cooldown, FireAnim = 9, SwitchPos, Refire;
        public float BobPhase, BobAmt;
        public float DamageFlash, PickupFlash, ShakeAmt;
        public float HurtDirTime, HurtDirAngle;
        public float HurtFloorTimer, PainSoundTimer;
        public float GrinTime, OuchTime, RampageTime;
        public float EyeZ = 0.5f;
        public float AimSlope;    // height change per unit distance of a shot through the crosshair (set by the view)
        public float SwayX;       // weapon lag when turning with the mouse
        bool fireWasDown, dryClicked;
        public float MuzzleTime;
        public float VZ;          // jumping: Z is how high the feet are off the floor
        public bool OnGround = true;
        public float ParryTime;   // > 0: the weapon is swung out and projectiles that reach it are knocked back
        public float ParryCool;
        public float PunchAnim = 9;
        public float Charge;      // the ray gun winds up while the trigger is held

        public const float RayChargeTime = 1.2f;

        // a hop of about a third of a metre: enough to clear a lava tile with a run-up, never enough to reach the ceiling
        public const float JumpSpeed = 2.9f, Gravity = 12f, MaxJumpZ = 0.4f;
        public const float ParryWindow = 0.28f, ParryCooldown = 0.5f;

        public Player()
        {
            Kind = ActorKind.Player;
            Radius = 0.25f;
            Has[0] = Has[1] = true;
            Ammo[0] = 50;
        }

        public WeaponDef Def { get { return WeaponDef.All[Weapon]; } }

        public void ResetForNewGame()
        {
            HP = 100; Armor = 0; ArmorType = 0;
            for (int i = 0; i < Weapons; i++) Has[i] = i < 2;
            for (int i = 0; i < AmmoTypes; i++) Ammo[i] = 0;
            Ammo[0] = 50;
            Weapon = 1; Pending = -1;
        }

        /// <summary>Bare fists and nothing else: how the airport starts.</summary>
        public void MakeUnarmed()
        {
            for (int i = 0; i < Weapons; i++) Has[i] = i == 0;
            for (int i = 0; i < AmmoTypes; i++) Ammo[i] = 0;
            Weapon = 0; Pending = -1;
        }

        public Player CloneInventory()
        {
            var p = new Player();
            p.HP = HP; p.Armor = Armor; p.ArmorType = ArmorType;
            Array.Copy(Ammo, p.Ammo, AmmoTypes);
            Array.Copy(Has, p.Has, Weapons);
            p.Weapon = Weapon;
            return p;
        }

        public void CopyInventoryFrom(Player o)
        {
            HP = Math.Max(o.HP, 1); Armor = o.Armor; ArmorType = o.ArmorType;
            Array.Copy(o.Ammo, Ammo, AmmoTypes);
            Array.Copy(o.Has, Has, Weapons);
            Weapon = o.Weapon;
        }

        public override Image Sprite(World w) { return null; }

        bool HasAmmoFor(int wi)
        {
            var d = WeaponDef.All[wi];
            return d.Ammo < 0 || Ammo[d.Ammo] > 0;
        }

        int BestWeapon()
        {
            int[] pref = { 5, 3, 2, 4, 1, 0 };
            foreach (int wi in pref)
                if (Has[wi] && HasAmmoFor(wi) && wi != 4) return wi;
            return Has[4] && HasAmmoFor(4) ? 4 : 0;
        }

        public void SelectWeapon(int wi)
        {
            if (wi < 0 || wi >= Weapons || !Has[wi] || wi == Weapon && Pending < 0) return;
            if (!HasAmmoFor(wi)) return;
            Pending = wi;
        }

        public void Update(World w, float dt, PlayerInput inp)
        {
            DamageFlash = Math.Max(0, DamageFlash - dt * 1.6f);
            PickupFlash = Math.Max(0, PickupFlash - dt * 3f);
            ShakeAmt = Math.Max(0, ShakeAmt - dt * 2.5f);
            HurtDirTime = Math.Max(0, HurtDirTime - dt);
            GrinTime -= dt; OuchTime -= dt; PainSoundTimer -= dt;
            MuzzleTime -= dt;

            if (Dead)
            {
                DeadTime += dt;
                EyeZ = Math.Max(0.12f, EyeZ - dt * 0.5f);
                VX *= 0.9f; VY *= 0.9f;
                w.TryMove(this, VX * dt, VY * dt);
                return;
            }

            // ---- looking
            Angle += inp.Turn;
            if (Angle > Math.PI) Angle -= (float)(2 * Math.PI);
            if (Angle < -Math.PI) Angle += (float)(2 * Math.PI);
            Pitch = Math.Max(-0.42f, Math.Min(0.42f, Pitch + inp.Look));
            SwayX = Math.Max(-0.6f, Math.Min(0.6f, SwayX + inp.Turn * 0.5f)) * (float)Math.Exp(-dt * 9);

            // ---- movement with acceleration and friction
            float dx = (float)Math.Cos(Angle), dy = (float)Math.Sin(Angle);
            float rx = -dy, ry = dx;
            float wx = dx * inp.Forward + rx * inp.Strafe, wy = dy * inp.Forward + ry * inp.Strafe;
            float wl = (float)Math.Sqrt(wx * wx + wy * wy);
            float maxSpeed = inp.Run ? 6.4f : 4.1f;
            if (wl > 1) { wx /= wl; wy /= wl; }
            wx *= maxSpeed; wy *= maxSpeed;
            float k = Math.Min(1, dt * (wl > 0.01f ? 11 : 13));
            VX += (wx - VX) * k; VY += (wy - VY) * k;
            float ox = X, oy = Y;
            w.TryMove(this, VX * dt, VY * dt);
            if (dt > 0) { VX = (X - ox) / dt * 0.5f + VX * 0.5f; VY = (Y - oy) / dt * 0.5f + VY * 0.5f; }

            float speed = (float)Math.Sqrt(VX * VX + VY * VY);
            float s = Math.Min(1, speed / 4.1f);
            BobPhase += dt * (6 + 4 * s) * (s > 0.05f ? 1 : 0);
            BobAmt += (s - BobAmt) * Math.Min(1, dt * 8);

            // ---- jumping: a short hop, enough to clear a lava tile with a run-up
            if (inp.Jump && OnGround)
            {
                VZ = JumpSpeed;
                OnGround = false;
                Audio.Play(Sfx.Jump, 0.5f, 0, 1, 0);
            }
            if (!OnGround)
            {
                VZ -= Gravity * dt;
                Z += VZ * dt;
                if (Z <= 0)
                {
                    Z = 0;
                    OnGround = true;
                    if (VZ < -2.5f) Audio.Play(Sfx.Land, 0.45f, 0, 1, 0);
                    VZ = 0;
                }
                else if (Z > MaxJumpZ && VZ > 0) { Z = MaxJumpZ; VZ = 0; }   // the ceiling is low: never rise into it
            }
            EyeZ = 0.5f + Z + (w.Settings.HeadBob && OnGround ? (float)Math.Sin(BobPhase * 2) * 0.018f * BobAmt : 0);

            // ---- the parry: a fist thrown out with the right button
            ParryTime -= dt;
            ParryCool -= dt;
            PunchAnim += dt;
            if (inp.Parry && ParryCool <= 0 && !Dead)
            {
                ParryTime = ParryWindow;
                ParryCool = ParryCooldown;
                PunchAnim = 0;
                w.PlayerPunch(this);
            }

            // ---- hurt floors (not while in the air over them)
            var fk = w.Map.In((int)X, (int)Y) ? w.Map.Floor[(int)Y * w.Map.W + (int)X] : FloorKind.A;
            bool lava = (fk == FloorKind.Lava || fk == FloorKind.LavaOut) && Z < 0.15f;
            bool slime = (fk == FloorKind.Nukage || fk == FloorKind.NukageOut) && Z < 0.15f;
            if (lava || slime)
            {
                HurtFloorTimer -= dt;
                if (HurtFloorTimer <= 0)
                {
                    HurtFloorTimer = 0.6f;
                    Audio.Play(Sfx.HurtFloor, 0.8f, 0, 1, 0);
                    Hurt(w, lava ? 10 : 5, float.NaN, float.NaN);
                }
            }
            else HurtFloorTimer = 0;

            // ---- weapon switching
            if (inp.SelectSlot > 0)
            {
                int wi = inp.SelectSlot - 1;
                if (!Has[wi]) { }
                else if (!HasAmmoFor(wi)) w.Message("NO AMMO FOR THE " + WeaponDef.All[wi].Name, Col.Rgb(255, 120, 60));
                else SelectWeapon(wi);
            }
            if (inp.Cycle != 0)
            {
                int wi = Pending >= 0 ? Pending : Weapon;
                for (int i = 0; i < Weapons; i++)
                {
                    wi = (wi + inp.Cycle + Weapons) % Weapons;
                    if (Has[wi] && HasAmmoFor(wi)) { SelectWeapon(wi); break; }
                }
            }

            if (Pending >= 0)
            {
                SwitchPos += dt * 6;
                if (SwitchPos >= 1)
                {
                    SwitchPos = 1;
                    if (Pending != Weapon) { LastWeapon = Weapon; Weapon = Pending; Audio.Play(Sfx.WeaponUp, 0.5f, 0, 1, 0); }
                    Pending = -1;
                }
            }
            else if (SwitchPos > 0) SwitchPos = Math.Max(0, SwitchPos - dt * 6);

            // ---- firing
            Cooldown -= dt;
            FireAnim += dt;
            if (!inp.Fire) { dryClicked = false; Refire = 0; }
            if (Def.Ray)
            {
                // the ray gun winds up while the trigger is held, then lets go by itself
                bool ready = Cooldown <= 0 && Pending < 0 && SwitchPos < 0.25f;
                if (inp.Fire && ready && HasAmmoFor(Weapon))
                {
                    if (Charge <= 0) Audio.Play(Sfx.RayGun, 0.85f, 0, 0.55f, 0);   // the winding whine
                    Charge += dt;
                    if (Charge >= RayChargeTime)
                    {
                        Charge = 0;
                        Ammo[Def.Ammo]--;
                        Cooldown = Def.Cooldown;
                        FireAnim = 0;
                        MuzzleTime = 0.12f;
                        w.PlayerRay(this, Def);
                    }
                }
                else
                {
                    if (inp.Fire && ready && !dryClicked) { Audio.Play(Sfx.DryFire, 0.7f, 0, 1, 0); dryClicked = true; }
                    Charge = Math.Max(0, Charge - dt * 2.5f);
                }
            }
            else if (inp.Fire && Cooldown <= 0 && Pending < 0 && SwitchPos < 0.25f)
            {
                var d = Def;
                if (!HasAmmoFor(Weapon))
                {
                    if (!dryClicked) { Audio.Play(Sfx.DryFire, 0.7f, 0, 1, 0); dryClicked = true; }
                    int b = BestWeapon();
                    if (b != Weapon) SelectWeapon(b);
                    Cooldown = 0.3f;
                }
                else
                {
                    if (d.Ammo >= 0) Ammo[d.Ammo]--;
                    Cooldown = d.Cooldown;
                    FireAnim = 0;
                    w.PlayerFire(this, d, Refire);
                    Refire += 1;
                    if (!d.Melee) MuzzleTime = d.Rocket ? 0.09f : 0.06f;
                }
            }
            fireWasDown = inp.Fire;

            if (inp.Use) w.PlayerUse(this);
            w.CheckPickups(this);
        }

        public void Hurt(World w, float amount, float fromX, float fromY)
        {
            amount *= w.DamageMul;
            if (Dead || amount <= 0) return;
            int dmg = (int)Math.Ceiling(amount);
            if (ArmorType > 0 && Armor > 0)
            {
                int save = ArmorType == 1 ? dmg / 3 : dmg / 2;
                if (save > Armor) save = Armor;
                Armor -= save;
                if (Armor == 0) ArmorType = 0;
                dmg -= save;
            }
            HP -= dmg;
            DamageFlash = Math.Min(1, DamageFlash + 0.25f + dmg / 40f);
            ShakeAmt = Math.Min(1, ShakeAmt + dmg / 60f);
            if (dmg >= 20) OuchTime = 1.0f;
            if (!float.IsNaN(fromX))
            {
                HurtDirAngle = (float)Math.Atan2(fromY - Y, fromX - X);
                HurtDirTime = 0.8f;
            }
            if (HP <= 0)
            {
                HP = 0;
                Dead = true;
                DeadTime = 0;
                Audio.Play(Sfx.PlayerDeath, 1, 0, 1, 0);
                return;
            }
            if (PainSoundTimer <= 0) { Audio.Play(Sfx.PlayerPain, 0.9f, 0, 1, 0); PainSoundTimer = 0.35f; }
        }

        /// <summary>Returns true if the item was taken.</summary>
        public bool Give(World w, char c, bool dropped)
        {
            float am = w.AmmoMul;
            switch (c)
            {
                case 'h': if (HP >= 100) return false; HP = Math.Min(100, HP + 10); w.Message("PICKED UP A STIMPACK.", -1); break;
                case 'm': if (HP >= 100) return false; HP = Math.Min(100, HP + 25); w.Message(HP < 50 ? "PICKED UP A MEDIKIT THAT YOU REALLY NEED!" : "PICKED UP A MEDIKIT.", -1); break;
                case '+': HP = Math.Min(200, HP + 2); w.Message("PICKED UP A HEALTH BONUS.", -1); break;
                case 'o': HP = Math.Min(200, HP + 100); w.Message("SOUL ORB! +100 HEALTH", Col.Rgb(120, 180, 255)); Audio.Play(Sfx.PowerUp); GrinTime = 2; break;
                case 'a':
                    Armor = Math.Min(200, Armor + 2); if (ArmorType == 0) ArmorType = 1;
                    w.Message("PICKED UP AN ARMOR BONUS.", -1); break;
                case 'G': if (Armor >= 100) return false; Armor = 100; ArmorType = 1; w.Message("PICKED UP THE COMBAT ARMOR.", -1); break;
                case 'U': if (Armor >= 200) return false; Armor = 200; ArmorType = 2; w.Message("PICKED UP THE MEGA ARMOR!", Col.Rgb(120, 180, 255)); break;
                case 'c': if (!AddAmmo(0, (int)((dropped ? 5 : 10) * am))) return false; w.Message("PICKED UP A CLIP.", -1); break;
                case 'C': if (!AddAmmo(0, (int)(50 * am))) return false; w.Message("PICKED UP A BOX OF BULLETS.", -1); break;
                case 'e': if (!AddAmmo(1, (int)(4 * am))) return false; w.Message("PICKED UP 4 SHOTGUN SHELLS.", -1); break;
                case 'E': if (!AddAmmo(1, (int)(20 * am))) return false; w.Message("PICKED UP A BOX OF SHELLS.", -1); break;
                case 'q': if (!AddAmmo(2, (int)(1 * am + 0.5f))) return false; w.Message("PICKED UP A ROCKET.", -1); break;
                case 'Q': if (!AddAmmo(2, (int)(5 * am))) return false; w.Message("PICKED UP A BOX OF ROCKETS.", -1); break;
                case 'g': GiveWeapon(w, 1, 0, 20, "YOU GOT THE PISTOL!"); break;
                case 'S': GiveWeapon(w, 2, 1, 8, "YOU GOT THE SHOTGUN!"); break;
                case 'N': GiveWeapon(w, 3, 0, 20, "YOU GOT THE MINIGUN!"); break;
                case 'L': GiveWeapon(w, 4, 2, 2, "YOU GOT THE ROCKET LAUNCHER!"); break;
                case 'W': GiveWeapon(w, 5, 3, 2, "YOU GOT THE RAY GUN! IT IS WARM."); break;
                case 'w': if (!AddAmmo(3, 1)) return false; w.Message("PICKED UP A SOUL CELL.", Col.Rgb(255, 150, 230)); break;
                case 'r': Keys[1] = true; w.Message("PICKED UP A RED KEYCARD.", Col.Rgb(255, 80, 60)); Audio.Play(Sfx.KeyPickup); break;
                case 'b': Keys[2] = true; w.Message("PICKED UP A BLUE KEYCARD.", Col.Rgb(90, 150, 255)); Audio.Play(Sfx.KeyPickup); break;
                case 'y': Keys[3] = true; w.Message("PICKED UP A YELLOW KEYCARD.", Col.Rgb(255, 220, 60)); Audio.Play(Sfx.KeyPickup); break;
                case '{':
                    Keys[4] = true;
                    w.Message("YOU HAVE YOUR BOARDING PASS.", Col.Rgb(90, 230, 210));
                    Audio.Play(Sfx.KeyPickup);
                    w.TriggerIncident();
                    break;
                default: return false;
            }
            if ("orbySNLWg{".IndexOf(c) < 0) Audio.Play(Sfx.Pickup, 0.8f, 0, 1, 0);
            PickupFlash = Math.Min(1, PickupFlash + 0.5f);
            return true;
        }

        bool AddAmmo(int type, int n)
        {
            if (Ammo[type] >= MaxAmmo[type]) return false;
            bool wasEmpty = Ammo[type] == 0;
            Ammo[type] = Math.Min(MaxAmmo[type], Ammo[type] + Math.Max(1, n));
            // switch away from the fist when ammo shows up (like the classics)
            if (wasEmpty && Weapon == 0 && Pending < 0)
            {
                if (type == 0 && Has[1]) SelectWeapon(Has[3] ? 3 : 1);
                if (type == 1 && Has[2]) SelectWeapon(2);
                if (type == 3 && Has[5]) SelectWeapon(5);
            }
            return true;
        }

        void GiveWeapon(World w, int wi, int ammoType, int ammo, string msg)
        {
            bool had = Has[wi];
            Has[wi] = true;
            // soul cells are rationed: the difficulty's ammo bonus doesn't apply to them
            Ammo[ammoType] = Math.Min(MaxAmmo[ammoType], Ammo[ammoType] + (ammoType == 3 ? ammo : (int)(ammo * w.AmmoMul)));
            w.Message(msg, Col.Rgb(255, 230, 120));
            Audio.Play(Sfx.WeaponPickup);
            GrinTime = 2.5f;
            if (!had) SelectWeapon(wi);
        }
    }
}
