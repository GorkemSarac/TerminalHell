// TERMINAL HELL - the player: movement, health/armor, inventory and weapons.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    sealed class WeaponDef
    {
        public string Name;
        public int Slot;             // 1..6: which key selects this weapon's slot
        public int SlotPos;          // 0 primary, 1 secondary - which one a re-press of the slot key toggles to
        public int Ammo = -1;        // -1 none, 0 bullets, 1 shells, 2 rockets, 3 soul cells, 4 energy cells
        public float Cooldown;
        public int Pellets = 1;
        public float Spread;
        public int DmgMin, DmgMax;
        public bool Melee, Rocket, Ray;
        public Sfx Sound;
        public float Anim;           // length of the firing animation
        public int Barrels = 1;      // shells spent per shot (the double barrel fires 2, or 1 if that's all that's left)
        public int AmmoPerShot = 1;  // ammo a single shot needs before it can fire at all (the laser slicer takes a handful of cells)
        public int AmmoUse = 1;      // ammo one shot spends (the machine gun burns 2 bullets per shot, though it fires only one)
        public float Falloff;        // > 0: pellets lose damage with distance, down to a tenth of it at this many tiles (double barrel)
        public float Knockback;      // shoves the player backwards on firing
        public bool Saw;             // a continuous melee weapon that forces enemies out of whatever they were doing
        public bool Beam;            // a laser held down: drains ammo and deals damage per second instead of per shot
        public float DrainPerSec, DmgPerSec;
        public bool Arc;             // the laser slicer: charges up, then sweeps a horizontal arc of light through everything in front
        public float ChargeTime;     // > 0: the trigger winds the weapon up for this long and it fires by itself (ray gun, laser slicer)
        public bool Grenade;         // the launcher's alt-fire: a bouncing, fused projectile instead of a straight rocket

        public static readonly WeaponDef[] All =
        {
            new WeaponDef { Name = "FIST", Slot = 1, SlotPos = 0, Cooldown = 0.42f, DmgMin = 3, DmgMax = 8, Melee = true, Sound = Sfx.Punch, Anim = 0.42f },
            new WeaponDef { Name = "SAW", Slot = 1, SlotPos = 1, Cooldown = 0.12f, DmgMin = 7, DmgMax = 12, Melee = true, Saw = true, Sound = Sfx.SawBite, Anim = 0.14f },
            new WeaponDef { Name = "PISTOL", Slot = 2, SlotPos = 0, Ammo = 0, Cooldown = 0.34f, Spread = 0.02f, DmgMin = 10, DmgMax = 16, Sound = Sfx.Pistol, Anim = 0.25f },
            new WeaponDef { Name = "MACHINE GUN", Slot = 2, SlotPos = 1, Ammo = 0, Cooldown = 0.105f, Spread = 0.11f, DmgMin = 8, DmgMax = 12, AmmoUse = 2, Sound = Sfx.Chaingun, Anim = 0.1f },
            new WeaponDef { Name = "SHOTGUN", Slot = 3, SlotPos = 0, Ammo = 1, Cooldown = 0.95f, Pellets = 7, Spread = 0.085f, DmgMin = 7, DmgMax = 14, Sound = Sfx.Shotgun, Anim = 0.9f },
            new WeaponDef { Name = "DOUBLE SHOTGUN", Slot = 3, SlotPos = 1, Ammo = 1, Cooldown = 1.3f, Pellets = 9, Spread = 0.19f, DmgMin = 10, DmgMax = 18,
                Barrels = 2, Knockback = 4.8f, Falloff = 9f, Sound = Sfx.DoubleShotgun, Anim = 1.25f },
            new WeaponDef { Name = "BEAM RIFLE", Slot = 4, SlotPos = 0, Ammo = 4, Cooldown = 0, DmgPerSec = 90, DrainPerSec = 12, Beam = true, Sound = Sfx.Laser, Anim = 0.12f },
            new WeaponDef { Name = "LASER SLICER", Slot = 4, SlotPos = 1, Ammo = 4, AmmoPerShot = 10, ChargeTime = 1.3f, Cooldown = 1.1f, DmgMin = 110, DmgMax = 150,
                Arc = true, Sound = Sfx.LaserArc, Anim = 0.6f },
            new WeaponDef { Name = "LAUNCHER", Slot = 5, SlotPos = 1, Ammo = 2, Cooldown = 0.8f, DmgMin = 40, DmgMax = 80, Rocket = true, Sound = Sfx.Rocket, Anim = 0.5f },
            new WeaponDef { Name = "GRENADE LAUNCHER", Slot = 5, SlotPos = 0, Ammo = 2, Cooldown = 0.7f, DmgMin = 45, DmgMax = 85, Grenade = true, Sound = Sfx.GrenadeLaunch, Anim = 0.6f },
            new WeaponDef { Name = "RAY GUN", Slot = 6, SlotPos = 0, Ammo = 3, ChargeTime = Player.RayChargeTime, Cooldown = 0.9f, DmgMin = 180, DmgMax = 280, Ray = true, Sound = Sfx.RayGun, Anim = 0.5f },
        };

        /// <summary>The two weapon indices that live in a slot (1..6); -1 where there is no second one (the ray gun).</summary>
        public static readonly int[,] BySlot = BuildBySlot();

        static int[,] BuildBySlot()
        {
            var t = new int[7, 2];
            for (int s = 0; s < 7; s++) { t[s, 0] = -1; t[s, 1] = -1; }
            for (int i = 0; i < All.Length; i++) t[All[i].Slot, All[i].SlotPos] = i;
            return t;
        }
    }

    struct PlayerInput
    {
        public float Forward, Strafe, Turn, Look;
        public bool Run, Fire, Use, Jump, Parry;
        public int SelectSlot;        // 1..6, 0 none: pressing a slot key again toggles to the other weapon in it
        public int SelectWeapon;      // 1 + a specific weapon index, 0 none: used by the "last weapon" quick-switch
        public int Cycle;             // -1 / +1 from the mouse wheel
    }

    sealed class Player : Actor
    {
        public const int Weapons = 11, AmmoTypes = 5, Slots = 6;

        public float Angle, Pitch, VX, VY;
        public int HP = 100, Armor, ArmorType;
        public readonly int[] Ammo = new int[AmmoTypes];
        public static readonly int[] MaxAmmo = { 200, 50, 50, 12, 100 };
        public static readonly string[] AmmoNames = { "BULL", "SHEL", "RCKT", "SOUL", "CELL" };
        public readonly bool[] Has = new bool[Weapons];
        public readonly bool[] Keys = new bool[5];      // 1 red, 2 blue, 3 yellow, 4 boarding pass
        /// <summary>Which of the two weapons in each slot (1..6) a slot key last chose - remembered between levels.</summary>
        public readonly int[] ActiveSlotPos = new int[Slots + 1];
        public int Weapon = 2, Pending = -1, LastWeapon = 0;
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
        float beamDrain, humTimer, revTimer;
        public float MuzzleTime;
        public float VZ;          // jumping: Z is how high the feet are off the floor
        public bool OnGround = true;
        public float ParryTime;   // > 0: the weapon is swung out and projectiles that reach it are knocked back
        public float ParryCool;
        public float PunchAnim = 9;
        public float Charge;      // the ray gun and the laser ray wind up while the trigger is held
        // the laser's beam, for the view to draw: on while it burns, how far it reaches, and whether it is cutting into a body
        public bool BeamOn, BeamOnBody;
        public float BeamDist;
        public float SawRev;      // 0..1: how fast the saw's blade is turning
        public float SawBite;     // > 0: the saw is chewing into something right now (sparks, judder)
        public int LastShots = 2; // how many barrels the double barrel last fired (the reload ejects that many shells)

        public const float RayChargeTime = 2.4f;

        // a hop of about a third of a metre: enough to clear a lava tile with a run-up, never enough to reach the ceiling
        public const float JumpSpeed = 2.9f, Gravity = 12f, MaxJumpZ = 0.4f;
        public const float ParryWindow = 0.28f, ParryCooldown = 0.5f;

        public Player()
        {
            Kind = ActorKind.Player;
            Radius = 0.25f;
            Has[0] = Has[2] = true;
            Ammo[0] = 50;
        }

        public WeaponDef Def { get { return WeaponDef.All[Weapon]; } }

        public void ResetForNewGame()
        {
            HP = 100; Armor = 0; ArmorType = 0;
            for (int i = 0; i < Weapons; i++) Has[i] = i == 0 || i == 2;
            for (int i = 0; i < AmmoTypes; i++) Ammo[i] = 0;
            for (int i = 0; i < ActiveSlotPos.Length; i++) ActiveSlotPos[i] = 0;
            Ammo[0] = 50;
            Weapon = 2; Pending = -1;
        }

        /// <summary>Bare fists and nothing else: how the airport starts.</summary>
        public void MakeUnarmed()
        {
            for (int i = 0; i < Weapons; i++) Has[i] = i == 0;
            for (int i = 0; i < AmmoTypes; i++) Ammo[i] = 0;
            for (int i = 0; i < ActiveSlotPos.Length; i++) ActiveSlotPos[i] = 0;
            Weapon = 0; Pending = -1;
        }

        public Player CloneInventory()
        {
            var p = new Player();
            p.HP = HP; p.Armor = Armor; p.ArmorType = ArmorType;
            Array.Copy(Ammo, p.Ammo, AmmoTypes);
            Array.Copy(Has, p.Has, Weapons);
            Array.Copy(ActiveSlotPos, p.ActiveSlotPos, ActiveSlotPos.Length);
            p.Weapon = Weapon;
            return p;
        }

        public void CopyInventoryFrom(Player o)
        {
            HP = Math.Max(o.HP, 1); Armor = o.Armor; ArmorType = o.ArmorType;
            Array.Copy(o.Ammo, Ammo, AmmoTypes);
            Array.Copy(o.Has, Has, Weapons);
            Array.Copy(o.ActiveSlotPos, ActiveSlotPos, ActiveSlotPos.Length);
            Weapon = o.Weapon;
        }

        public override Image Sprite(World w) { return null; }

        /// <summary>Out of ammo: click once, and reach for the best weapon that still has some.</summary>
        void DryFire()
        {
            if (!dryClicked) { Audio.Play(Sfx.DryFire, 0.7f, 0, 1, 0); dryClicked = true; }
            int b = BestWeapon();
            if (b != Weapon) SelectWeapon(b);
            Cooldown = 0.3f;
        }

        public bool HasAmmoFor(int wi)
        {
            var d = WeaponDef.All[wi];
            return d.Ammo < 0 || Ammo[d.Ammo] >= Math.Max(1, d.AmmoPerShot);
        }

        int BestWeapon()
        {
            // the strongest weapon with ammo, but a rapid-fire one (machine gun, laser) only once nothing else is left
            int[] pref = { 10, 9, 8, 7, 5, 4, 2, 1, 0 };
            foreach (int wi in pref)
                if (Has[wi] && HasAmmoFor(wi)) return wi;
            if (Has[3] && HasAmmoFor(3)) return 3;
            if (Has[6] && HasAmmoFor(6)) return 6;
            return 0;
        }

        public void SelectWeapon(int wi)
        {
            if (wi < 0 || wi >= Weapons || !Has[wi] || wi == Weapon && Pending < 0) return;
            if (!HasAmmoFor(wi)) return;
            ActiveSlotPos[WeaponDef.All[wi].Slot] = WeaponDef.All[wi].SlotPos;
            Pending = wi;
        }

        /// <summary>A slot key (1..6) was pressed: toggle to the slot's other weapon if already on this one,
        /// otherwise switch to whichever of the slot's weapons was last active (falling back to the one carried).</summary>
        public void SelectSlot(World w, int slot)
        {
            if (slot < 1 || slot > Slots) return;
            int primary = WeaponDef.BySlot[slot, 0], secondary = WeaponDef.BySlot[slot, 1];
            int cur = Pending >= 0 ? Pending : Weapon;
            int target;
            if (WeaponDef.All[cur].Slot == slot && secondary >= 0)
                target = cur == primary ? secondary : primary;
            else
            {
                target = ActiveSlotPos[slot] == 1 && secondary >= 0 ? secondary : primary;
                if (target < 0 || !Has[target]) target = (target == primary ? secondary : primary);
            }
            if (target < 0 || !Has[target]) return;
            if (!HasAmmoFor(target)) { w.Message("NO AMMO FOR THE " + WeaponDef.All[target].Name, Col.Rgb(255, 120, 60)); return; }
            SelectWeapon(target);
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
            if (inp.SelectSlot > 0) SelectSlot(w, inp.SelectSlot);
            if (inp.SelectWeapon > 0)
            {
                int wi = inp.SelectWeapon - 1;
                if (Has[wi] && HasAmmoFor(wi)) SelectWeapon(wi);
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
            var wd = Def;
            bool canShoot = Pending < 0 && SwitchPos < 0.25f;
            if (!wd.Beam) { BeamOn = false; humTimer = 0; }
            SawBite = Math.Max(0, SawBite - dt);
            if (wd.ChargeTime > 0)
            {
                // the ray gun and the laser slicer wind up while the trigger is held, then let go by themselves
                bool ready = Cooldown <= 0 && canShoot;
                if (inp.Fire && ready && HasAmmoFor(Weapon))
                {
                    if (Charge <= 0) Audio.Play(wd.Ray ? Sfx.RayCharge : Sfx.LaserCharge, 0.85f, 0, 1f, 0);   // the winding whine
                    Charge += dt;
                    if (Charge >= wd.ChargeTime)
                    {
                        Charge = 0;
                        Ammo[wd.Ammo] -= wd.AmmoPerShot;
                        Cooldown = wd.Cooldown;
                        FireAnim = 0;
                        MuzzleTime = 0.12f;
                        if (wd.Ray) w.PlayerRay(this, wd); else w.PlayerArc(this, wd);
                    }
                }
                else
                {
                    if (inp.Fire && ready && !HasAmmoFor(Weapon)) DryFire();
                    Charge = Math.Max(0, Charge - dt * 2.5f);
                }
            }
            else if (wd.Beam)
            {
                // the laser: no discrete shots - a steady line of light that drains cells for as long as the trigger is held
                if (inp.Fire && canShoot && Ammo[wd.Ammo] > 0)
                {
                    if (!BeamOn) Audio.Play(wd.Sound, 0.6f, 0, 1, 0);
                    humTimer -= dt;
                    if (humTimer <= 0) { Audio.Play(Sfx.LaserHum, 0.5f, 0, 1, 0); humTimer = 0.3f; }
                    BeamOn = true;
                    beamDrain += wd.DrainPerSec * dt;
                    while (beamDrain >= 1f && Ammo[wd.Ammo] > 0) { Ammo[wd.Ammo]--; beamDrain -= 1f; }
                    FireAnim = 0;
                    MuzzleTime = 0.05f;
                    w.PlayerBeam(this, wd, dt);
                }
                else
                {
                    if (BeamOn) Audio.Play(Sfx.DryFire, 0.35f, 0, 1.6f, 0);   // the beam cutting out
                    BeamOn = false;
                    humTimer = 0;
                    if (inp.Fire && canShoot && Ammo[wd.Ammo] <= 0) DryFire();
                }
            }
            else if (inp.Fire && Cooldown <= 0 && canShoot)
            {
                if (!HasAmmoFor(Weapon)) DryFire();
                else
                {
                    int shots = 1;
                    if (wd.Ammo >= 0)
                    {
                        shots = Math.Max(1, Math.Min(wd.Barrels, Ammo[wd.Ammo]));
                        Ammo[wd.Ammo] -= Math.Min(Ammo[wd.Ammo], Math.Max(shots, wd.AmmoUse));
                    }
                    LastShots = shots;
                    Cooldown = wd.Cooldown;
                    FireAnim = 0;
                    w.PlayerFire(this, wd, Refire, shots);
                    Refire += 1;
                    if (!wd.Melee) MuzzleTime = wd.Rocket || wd.Grenade ? 0.09f : 0.06f;
                }
            }
            // the saw's motor: it revs while the trigger is down and runs down slowly once it's let go
            if (wd.Saw && inp.Fire && canShoot)
            {
                SawRev = Math.Min(1, SawRev + dt * 5);
                revTimer -= dt;
                if (revTimer <= 0) { Audio.Play(Sfx.Saw, 0.55f, 0, 0.9f + SawRev * 0.2f, 0); revTimer = 0.24f; }
            }
            else
            {
                SawRev = Math.Max(0, SawRev - dt * 1.5f);
                revTimer = 0;
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
                case '"': if (!AddAmmo(4, (int)(20 * am))) return false; w.Message("PICKED UP AN ENERGY CELL.", -1); break;
                case '\\': if (!AddAmmo(4, (int)(60 * am))) return false; w.Message("PICKED UP A BOX OF ENERGY CELLS.", -1); break;
                case 'g': GiveWeapon(w, 2, 0, 20, "YOU GOT THE PISTOL!"); break;
                case 'S': GiveWeapon(w, 4, 1, 8, "YOU GOT THE SHOTGUN!"); break;
                case 'N': GiveWeapon(w, 3, 0, 20, "YOU GOT THE MACHINE GUN!"); break;
                case 'L': GiveWeapon(w, 8, 2, 2, "YOU GOT THE ROCKET LAUNCHER!"); break;
                case 'W': GiveWeapon(w, 10, 3, 2, "YOU GOT THE RAY GUN! IT IS WARM."); break;
                case '5': GiveWeapon(w, 1, -1, 0, "YOU FOUND A SAW."); break;
                case '6': GiveWeapon(w, 5, 1, 8, "YOU FOUND THE DOUBLE BARREL SHOTGUN!"); break;
                case '7': GiveWeapon(w, 6, 4, 40, "YOU FOUND THE BEAM RIFLE!"); break;
                case '8': GiveWeapon(w, 7, 4, 30, "YOU FOUND THE LASER SLICER!"); break;
                case '9': GiveWeapon(w, 9, 2, 3, "YOU FOUND THE GRENADE LAUNCHER!"); break;
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
            if ("orbySNLWg{56789".IndexOf(c) < 0) Audio.Play(Sfx.Pickup, 0.8f, 0, 1, 0);
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
                if (type == 0 && Has[2]) SelectWeapon(Has[3] ? 3 : 2);
                if (type == 1 && Has[4]) SelectWeapon(Has[5] ? 5 : 4);
                if (type == 3 && Has[10]) SelectWeapon(10);
                if (type == 4 && (Has[6] || Has[7])) SelectWeapon(Has[6] ? 6 : 7);
            }
            return true;
        }

        void GiveWeapon(World w, int wi, int ammoType, int ammo, string msg)
        {
            bool had = Has[wi];
            Has[wi] = true;
            // soul cells are rationed: the difficulty's ammo bonus doesn't apply to them
            if (ammoType >= 0) Ammo[ammoType] = Math.Min(MaxAmmo[ammoType], Ammo[ammoType] + (ammoType == 3 ? ammo : (int)(ammo * w.AmmoMul)));
            w.Message(msg, Col.Rgb(255, 230, 120));
            Audio.Play(Sfx.WeaponPickup);
            GrinTime = 2.5f;
            if (!had) SelectWeapon(wi);
        }
    }
}
