// TERMINAL HELL - the campaign: two episodes. Episode 1 is on Earth (an airport under attack), episode 2 is the descent into Hell.
//
// Map legend (one character per cell):
//   walls   1 stone  2 brick  3 tech  4 wood  5 marble  6 flesh  7 computer  8 rust  9 skulls  0 rock (tall cliff)
//           A terminal wall  F window onto the apron  H departures board  I scorched wall  J broken window  M dead board
//           O safety-room wall  P safety-room wall with a first-aid cross  V concrete  Z hangar cladding
//           X exit switch and door (opens once thrown, styled differently from a normal door)
//           $ secret push wall (use it to slide it away)
//   doors   D door   R/B/Y red/blue/yellow locked doors
//           } boarding gate (needs the boarding pass; seals shut for good once you're through it)
//   floors  . floor A   _ floor B   - floor C   , outdoor (open sky)   ~ lava   : outdoor lava   = toxic slime   ; outdoor slime
//           # metal walkway (safe to cross over lava or slime)
//   player  ^ > v <  start position and facing
//   enemies z ghoul (rifle)   ` a ghoul carrying the red keycard   i fiend (fireballs)   p brute (melee)   K the Warden (boss)
//   items   h stimpack  m medikit  + health bonus  o soul orb  a armor bonus  G green armor  U blue armor
//           c clip  C box of bullets  e shells  E box of shells  q rocket  Q box of rockets
//           w soul cells (ray gun ammo: secret rooms only)
//           " energy cell  \ box of energy cells (ammo for the laser and the laser ray)
//           the saw, double barrel, laser, laser ray and grenade launcher have no map character: World.BonusWeapons places them
//           g pistol  S shotgun  N minigun  L rocket launcher  W ray gun  r/b/y keycards  { boarding pass
//   decor   % explosive barrel  * ceiling lamp  ! torch  t tech lamp  | pillar  & corpse  x blood pool  k skulls
//           d check-in desk  j/l waiting seats (empty / taken)  ( wrecked seats  ) rubble  n plant  s luggage  T luggage cart
//           u traveller  @ dead traveller  ? vending machine  / bin  f fire  [ scrolling ceiling sign  ] the ruined sign
using System;

namespace TerminalHell
{
    static class Levels
    {
        /// <summary>The two episodes, in the order the menu lists them.</summary>
        public static readonly EpisodeDef[] Episodes =
        {
            new EpisodeDef
            {
                Name = "EARTH", Blurb = "AN AIRPORT ON A QUIET MORNING",
                EndTitle = "TO BE CONTINUED",
                Ending = new[]
                {
                    "YOU STAND ON THE APRON AND THE SKY IS STILL BLUE.",
                    "",
                    "BEHIND YOU THE TERMINAL BURNS. IN FRONT OF YOU,",
                    "THE RUNWAY, AND EVERYTHING THAT CAME THROUGH IT.",
                    "",
                    "THE WAY BACK HOME IS LONGER THAN IT WAS THIS MORNING.",
                },
            },
            new EpisodeDef
            {
                Name = "HELL", Blurb = "THE DESCENT",
                EndTitle = "VICTORY",
                Ending = new[]
                {
                    "THE WARDEN IS DEAD, THE HELL GATE COLLAPSES INTO ASH BEHIND YOU.",
                    "",
                    "YOU CRAWL BACK THROUGH THE REFINERY, PAST THE OUTPOST,",
                    "INTO A GREY DAWN THAT SMELLS OF SMOKE AND SULPHUR.",
                    "",
                    "BUT AN EVIL STILL LINGERS IN THE DEEP BENEATH, NOW FREE...",
                },
            },
        };

        /// <summary>Every level, episode 1 first. Each one knows which episode it belongs to.</summary>
        public static LevelDef[] All()
        {
            var list = new[] { Terminal(), Runway(), Tower(), Hell1(), Hell2(), Hell3() };
            list[0].Episode = 0; list[1].Episode = 0; list[2].Episode = 0;
            for (int i = 3; i < list.Length; i++) list[i].Episode = 1;
            return list;
        }

        public static int IndexOf(string id)
        {
            var all = All();
            for (int i = 0; i < all.Length; i++) if (all[i].Id == id) return i;
            return -1;
        }

        public static LevelDef ById(string id)
        {
            int i = IndexOf(id);
            if (i < 0) throw new ArgumentException("no level " + id);
            return All()[i];
        }

        /// <summary>The first level of an episode.</summary>
        public static int FirstOf(int episode)
        {
            var all = All();
            for (int i = 0; i < all.Length; i++) if (all[i].Episode == episode) return i;
            return 0;
        }

        static LevelDef Terminal()
        {
            return new LevelDef
            {
                Id = "E1M1", Name = "TERMINAL", Music = 5, MusicAfter = 6, Sky = 2, Unarmed = true, Chime = true,
                Intro = "A QUIET MORNING AT THE AIRPORT.",
                FloorA = Tex.F_AIR, CeilA = Tex.C_AIR, FloorB = Tex.F_WRECK, CeilB = Tex.C_WRECK, FloorC = Tex.F_SAFE, CeilC = Tex.C_SAFE, FloorOut = Tex.F_APRON,
                Ambient = Col.Rgb(70, 70, 76), SkyLight = Col.Rgb(214, 224, 244), FogColor = Col.Rgb(30, 28, 34), FogDensity = 0.022f, Par = "5:00",
                Map = new[]
                {
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVIIIJJJIIIMMIIIJJIIMIIIJJIIIIIIIIJJIIMIMMIIJJIIVVVVVVVVVV",
                "VVOOPPPOO___________f______________h_Ia_______@____m_IVVVVVVVVVV",
                "VVO-----O__*___f_)_______(___f___s___I_sf_______*_(_fIVVVVVVVVVV",
                "VVO-c---O____@_________s___`____@____I______z___@____IVVVVVVVVVV",
                "VVP--+--O___(_*_______)________)_________*___)______cIVVVVVVVVVV",
                "VVP-----R_______s___@_____*______f_____(@________z___IVVVVVVVVVV",
                "VVO--*m-O__f_z_____]____@___)_____(_______)__g____*@_IVVVVVVVVVV",
                "VVO-----O_____@__@____s_______@______I_________)_____IVVVVVVVVVV",
                "VVOOOOOOO______(_______f__@_____*__f_I_c___@______)_sIVVVVVVVVVV",
                "VVVVVVVVI___f____________)___________I___(___*z______IVVVVVVVVVV",
                "VVVVVVVVIIIIIIAAAAH}HAAAAAIIIIIIIIIIIIf_____s______f_IVVVVVVVVVV",
                "VVVVVVVVVVVVVVA..........AVVVVVVVVVVVI_______________IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF/..u.....jAVVVVVVVVVVVIIIIIIIIDIIIIIIIIVVVVVVVVVV",
                "VVVVVVVVVVVVVVFn...[....lAVVVVVVVVVVVVVI_____________IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF..*...*u..HVVVVVVVVVVVVVI_f_____*_____IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF..........AVVVVVVVVVVVVVI____T________IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF.u..{....jAVVVVVVVVVVVVVI___z_@__s____IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF..........AVVVVVVVVIIIIII_s__*_____z_dI..VVVVVVVV",
                "VVVVVVVVVVVVVVF......u.s.HVVVVVVVVI_m__I__s____@_____$bwVVVVVVVV",
                "VVVVVVVVVVVVVVFn...[....jAVVVVVVVVIG_w_$*____s______sI..VVVVVVVV",
                "VVVVVVVVVVVVVVF..*...*..lAVVVVVVVVI_c__I_@_______T___IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF.s........AVVVVVVVVIIIIII___sh_____*@_IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF..u.......HVVVVVVVVVVVVVI_______)__s__IVVVVVVVVVV",
                "VVVVVVVVVVVVVVF.........jAVVVVVVVVVVVVVI_T_______z___IVVVVVVVVVV",
                "VVVVVVVVVVVVVVFn...[s...lAVVVVVVVVVVVVVI______z____T_IVVVVVVVVVV",
                "VVVVVVVVVVVVVVA..*...*../AVVVVVVVVVVVVVI__)*_________IVVVVVVVVVV",
                "VVVVVVVVVAAAHAA..........AAHAAAVVVVVVVVI_c_____sC___fIVVVVVVVVVV",
                "VVVVVVVVVA.?/............../?.AVVVVVVVVI_____________IVVVVVVVVVV",
                "VVVVVVVVVF....................AVVVVVVVVVVVVVVVDVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVFn...u..............dAVVVVV,,,,,@,,,,,,,,,,,,,ZZZZZZ,,Z",
                "VVVVVVVVVF...*.....*.u..s*.u.dAVVVVV,,,,,,,,,,,,,,,,,,,Z,z,,Z,aZ",
                "VVVVVVVVVF...................dHVVVVV,,,,,),,,,,,m,,,T,,Baw,qZ,,Z",
                "VVVVVVVVVF....j.l..[..j.l..u.dAVVVVV,,,,,,,,z,,,,,@,,,,Z,,z,Z,,Z",
                "VVVVVVVVVF........u...........AVVVVV,,,,%,,,,,,,,s,,,,,ZZZZZZ,,Z",
                "VVVVVVVVVF...................dAVVVVV,,,,,,,,VVVV,,,,,z,,,,),%,,Z",
                "VVVVVVVVVFn........u......u..dAVVVVV,,,s,,,,VVVV,,,,,,,,T,,,,,,Z",
                "VVVVVVVVVF...*l.j..*..l.j*.u.dAVVVVV,,,,,,,,VVVV,,,,,,,,,,,,,,,X",
                "VVVVVVVVVF...................dAVVVVV,c,,,,,,,,,,,,%,,,,,,,,,,,,Z",
                "VVVVVVVVVF..u......[s.........HVVVVV,,,,,,T,,,,),,,,,,,,,,z,,,,Z",
                "VVVVVVVVVF.s.............u.../AVVVVV,,,,,,,,,,,,,,,VVV,,,,,,,,GZ",
                "VVVVVVVVVF....j.l.....j.l.....AVVVVV,,,,,,,,,,,,z,,VVV,,,,,,,s,Z",
                "VVVVVVVVVFn..*.....*.....*s...AVVVVV,,T,,,,,,,,,,,,VVV,%,s,,,,,Z",
                "VVVVVVVVVF.....s...^.........nAVVVVV,,,,,,,,,,f,,,,,,@,,,,,C,a,Z",
                "VVVVVVVVVA....................AVVVVV,,,,,,,,,,,,,,,,,,,,,,,,,,,Z",
                "VVVVVVVVVAFFFFFFFFFFFFFFFFFFFFAVVVVVZZZZZZZZZZZZZZZZZZZZZZZZZZZZ",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                },
            };
        }

        static LevelDef Runway()
        {
            return new LevelDef
            {
                Id = "E1M2", Name = "RUNWAY", Music = 7, Sky = 3,
                Intro = "THE WAY DEEPER IN IS BLOCKED. THE ONLY WAY IS OUT ONTO THE FIELD.",
                FloorA = Tex.F_AIR, CeilA = Tex.C_AIR, FloorB = Tex.F_GRATE, CeilB = Tex.C_STONE, FloorOut = Tex.F_RUNWAY,
                Ambient = Col.Rgb(96, 96, 100), SkyLight = Col.Rgb(150, 152, 156), FogColor = Col.Rgb(70, 70, 74), FogDensity = 0.03f, Par = "5:30",
                // the runway looks like it keeps going past where the map actually ends - it doesn't
                WallOverrides = new[]
                {
                    new[] { 65, 16, Tex.RUNWAY_END }, new[] { 65, 17, Tex.RUNWAY_END }, new[] { 65, 18, Tex.RUNWAY_END },
                    new[] { 65, 19, Tex.RUNWAY_END }, new[] { 65, 20, Tex.RUNWAY_END }, new[] { 65, 21, Tex.RUNWAY_END }, new[] { 65, 22, Tex.RUNWAY_END },
                },
                Map = new[]
                {
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVAAAAAAAAAAAAAVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVA...........AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVA.n.........AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVA...........AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVA..^.....u..AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVA...........AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVA...........AAAAAAAAAVVVVVVVVVVVVVVVVVVVAAAAAAAAAAAVVVVVVVVVVVVVVVVVV",
                "VVVA...........A___*___AVVVVVVVVVVVVVVVVVVVA.........AVVVVVVVVVVVVVVVVVV",
                "VVVAAAAAA%.AADAA__c_s__AVVVVVVVVVVVVVVVVVVVA.m*....c.AVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVV))V...A___y___AVVVVVVVVVVVVVVVVVVVA..z......XVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVV..V...A_z_____AVVVVVVVVVVVVVVVVVVVA......z..AVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVr.V...A_____z_AVVVVVVVVVVVVVVVVVVVA...'..*..AVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVV.*V...A_______AVVVVVVVVVVVVVVVVVVVA.........AVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVV..V...AAAADAAAAVVZZZZZZZZZVVVVVVVVAAAAARAAAAAVVVVVVVVVVVVVVVVVV",
                "VV,,,,,,,,,,,,,,,,,,,,,,,,Z_______Z,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,VVVVVVV",
                "VV,,,,,,,,,,,,,,,,,,,,,,,,Z*__z___Z,%,,,T,s,,,',,,*,,,,,,,',z,,,,VVVVVVV",
                "VV,,,,,,,,,,,,,,,,,,,,,,,,Z_e_____Z,,,',,,,,,,,,,,,,T,,,,,,,,,),,VVVVVVV",
                "VV,,,,,,,,,,,,,,,,,,,,,,,,Y___S___D,,,,,,,,,,,,,,,,,,,',,,,,,,,s,VVVVVVV",
                "VV,,,,,,,,,,,,,,,,,,,,,,,,Z_____E_Z,,,*,,,',,,,,,,,,,,,,,,,,*',,,VVVVVVV",
                "VV,,,,,,,,,,,,,,,,,,,,,,,,Z___z__*Z,z,,,,,,,),,,%,',,,,,s,,,,,,,,VVVVVVV",
                "VV,,,,,,,,,,,,,,,,,,,,,,,,Z_______Z,,,,,,,,,,,,,,,,,,,,,%,,,,,,,,VVVVVVV",
                "VVVVVVVVVVVVVVVZZZZDZZZZZZZZZZZZZZZVVVVVVVVVVVVVVVVVVVVV$VVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVZ%______s__s____d_tZVVVVVVVVVVVVVVVVVVV_____VVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVZ_____________'____ZVVVVVVVVVVVVVVVVVVV_aw__VVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVZ%__*______*_*____hZVVVVVVVVVVVVVVVVVVV___U_VVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVZ_____z_____'______ZVVVVVVVVVVVVVVVVVVV_____VVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVZ_____________'_d__ZVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVZ_%q___T_dd_d_)__'tZVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVZZZZZZZZZZZZZZZZZZZZVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                },
            };
        }

        static LevelDef Tower()
        {
            return new LevelDef
            {
                Id = "E1M3", Name = "TOWER", Music = 8, Sky = 4,
                Intro = "PART OF THE CEILING IS GONE. THE TOWER IS RIGHT THERE, TOO CLOSE TO IGNORE.",
                FloorA = Tex.F_AIR, CeilA = Tex.C_AIR, FloorB = Tex.F_METAL, CeilB = Tex.C_PANEL, FloorC = Tex.F_WRECK, CeilC = Tex.C_WRECK, FloorOut = Tex.F_APRON,
                Ambient = Col.Rgb(112, 106, 108), SkyLight = Col.Rgb(232, 192, 160), FogColor = Col.Rgb(36, 22, 18), FogDensity = 0.026f, Par = "8:30",
                // the exit itself is a normal door - the portal is just what's glowing on the wall past it
                WallOverrides = new[] { new[] { 124, 10, Tex.HELL_PORTAL } },
                Map = new[]
                {
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV33333333333333333333333333333333VVVVVVV",
                "VVVVVVVVVVVVVVVVVVVAAAAAAAAAVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3333333333333333VVVVV3t_|_|___|_|_t33,,,,,,,,,,,,,,,3VVVVVVV",
                "VVAAAAAAAAAAAAAAAAVA.......AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3______________3VVVVV3_____________33,,,,,,,,,,,,,,,3VVVVVVV",
                "VVA..............AVA.*.r...AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3_C_____m_'__t_3VVVVV3___*_____*___33,m,,,,,,,,,,,,,3VVVVVVV",
                "VVA.u.,,,,,......AVA.....|.AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3__z__%_____z__3VVVVV3mE__'______E_33,,,|,,,,,,,|,,,3VVVVVVV",
                "VVA...,,,,,..z...AVA.z.....AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3_t____________3VVVVV3________'____33,,,,,,,,,,,,,,,3VVVVVVV",
                "VVA...,,,,,......AVA.......AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3__a|'_____%_e_3VVVVV3_z_e_____e_z_33,,,,,,,,,,,,,,,3VVVVVVV",
                "VVA..h,,,,,......AVA.......AVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3______________3VVVVV3%___________%33,,,,,,,,,,,,,,,3VVVVVVV",
                "VVA...,,,,,......AVAAAADAAAAVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV33333333D3333333VVVVV3333333D33333333,,,,,,,,,,,,,,,3333333V",
                "VVA.....)........A..z..........z........z3333______________________________________z__________________3,,,,,,,,,,,,,,,3----33V",
                "VVA..............D......................__R.B________________________________________________________zD,!,,,,,,,,,,,!,Y----X3V",
                "VVA..............A.........c....C......._3333__'______________________________________%___'___________3,,,,,,,,,,,,,,,3----33V",
                "VVA.........'....AVVVVVVVVVVVVAAAADAAAAVVVVVVVVVVV3333333D3333333VVVVVVVVVVVVVVVVVVV33$333VVVVVVVVVVVV3,,,,,,,,,,,,,,,3333333V",
                "VVA..^........c..AVVVVVVVVVVVVA.......AVVVVVVVVVVV3_____________3VVVVVVVVVVVVVVVVVVV3____3VVVVVVVVVVVV3,,,,,,,,,,,,,,,3VVVVVVV",
                "VVA..............AVVVVVVVVVVVVA.......AVVVVVVVVVVV3__t_______t__3VVVVVVVVVVVVVVVVVVV3_w__3VVVVVVVVVVVV3,,,,,,,,,,,,,,,3VVVVVVV",
                "VVA..............AVVVVVVVVVVVVA.'.....AVVVVVVVVVVV3___C__N__C___3VVVVVVVVVVVVVVVVVVV3__a_3VVVVVVVVVVVV3,,,,,,,,,,,,,,,3VVVVVVV",
                "VVAAAAAAAAAAAAAAAAVVVVVVVVVVVVA.....|.AVVVVVVVVVVV3_____________3VVVVVVVVVVVVVVVVVVV3____3VVVVVVVVVVVV3,,,,,,,,,,,,,,,3VVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVA.*.b...AVVVVVVVVVVV3__z_______'__3VVVVVVVVVVVVVVVVVVV333333VVVVVVVVVVVV3,,,|,,,,,,,|,,,3VVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVA.......AVVVVVVVVVVV3_____%_______3VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3,h,,,,,,,,,,,G,3VVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVA.......AVVVVVVVVVVV3_____________3VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3,,,,,,,,,,,,,,,3VVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVAAAAAAAAAVVVVVVVVVVV333333333333333VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV3,,,,,,,,,,,,,,,3VVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV33333333333333333VVVVVVV",
                "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
                },
            };
        }


        static LevelDef Hell1()
        {
            return new LevelDef
            {
                Id = "E2M1", Name = "OUTPOST OF HELL", Music = 1,
                Intro = "THE PORTAL DROPS YOU AT A RUINED OUTPOST. THIS IS HELL'S FRONT DOOR.",
                FloorA = Tex.F_TILE, CeilA = Tex.C_PANEL, FloorB = Tex.F_METAL, CeilB = Tex.C_STONE, FloorOut = Tex.F_DIRT,
                Ambient = Col.Rgb(82, 79, 86), SkyLight = Col.Rgb(240, 190, 160), FogColor = Col.Rgb(10, 4, 3), FogDensity = 0.05f, Par = "2:30",
                Map = new[]
                {
                "3333333333333333333330000000000000000000000000000000000000",
                "333333337X733333333330000000000000000000000000000000000000",
                "33337t.......t7333332,,,,,,,,,,,,,,,,,,0000000000000000000",
                "33333.z.....i.3333332,,,,,,,,,22222222,0000000000000000000",
                "33337..c......7333332,,,z,,,,,2..i...2,0000000000000000000",
                "33333.*.....*.3333332,,,,,,,,,2.m..r.2,0000000000000000000",
                "333333333R33333333332,,,,,,,,,2..*.i.2,0000000000000000000",
                "33.............%33332,,|,,,,,,2......2,0000000000000000000",
                "33.z....*.....z.33332,,,,,,,,,2.a....2,0000000000000000000",
                "33...|......|...33332,,,,,,,,,222D2222,0033333333333333330",
                "33..............D___D,,,,,,,,,,!,,,!,,,003t_dd___dd____t30",
                "33........z.....33332,,,,,,,,,,,,,,,,,,003______________70",
                "33...|......|...33332,,,,,,z,,,,,,,,,,,003______&i___\"__70",
                "33.......*......33332,,,,,,,,,,,,,,,,,,003__z__|___|____70",
                "33c............h33332,,,,,,,,%,,,,,,,,,,,3______________70",
                "33333D333333333333332,,,;;;;;,%,,,,,,,,,,D___*___*__z__\\70",
                "3333___33333333333332,,,;;;;;,,,,i,,,,,,,3______________70",
                "3333___33333333333332,,,;;;;;,,,,,,,,,,003__z__|___|____70",
                "3333_*_3333%___C33332,,,;;;;;,,,,,,,,,,003______x____\"__70",
                "3333___3333_____33332,,,;;;;;,,,,,,,,,,003_%_____i______70",
                "3333___3333__z__33332,,,,e,,,,,,,,,,|,,003t_dd______%__t30",
                "33......&.3_____33332,,,,,%,%,,,,,,,,,,0033333333333333330",
                "33...*....3e_*__33332,,,,,,C,,,,,,,,,,,0000000000000000000",
                "33........D_____3__G2,,,,,,,,,,,,,,z,,,0000000000000000000",
                "33...^....3_____$_w_2,,,,,,,,,,,,,,,,,,0000000000000000000",
                "33a......c3%___%3aa_2,&,,,,,,,,,,,,,,m,0000000000000000000",
                "33........3_____33332,,x,,,,,,,,,,,,,,,0000000000000000000",
                "333333333333333333332,,,,,,,,,,,,,,,,,,0000000000000000000",
                "3333333333333333333330000000000000000000000000000000000000",
                "3333333333333333333330000000000000000000000000000000000000",
                },
            };
        }

        static LevelDef Hell2()
        {
            return new LevelDef
            {
                Id = "E2M2", Name = "HELL LABS", Music = 2, Sky = 1,
                Intro = "DEEPER IN, SOMETHING WAS BEING MADE HERE. IT DIDN'T STOP WHEN THE MAKERS DID.",
                FloorA = Tex.F_METAL, CeilA = Tex.C_PANEL, FloorB = Tex.F_GRATE, CeilB = Tex.C_STONE, FloorOut = Tex.F_GRATE,
                Ambient = Col.Rgb(68, 78, 68), SkyLight = Col.Rgb(135, 125, 165), FogColor = Col.Rgb(4, 12, 4), FogDensity = 0.072f, Par = "3:30",
                Map = new[]
                {
                "888888888888888888888888888888888888888888888888888888888",
                "8888888888888888887X7888888888888888888888888888888888888",
                "888888888888%______________%88888888888888888888888888888",
                "888888888888_p_t________t_p_88888888888888888888888888888",
                "888888888888____========____88888888888888888888888888888",
                "888888888888____========____88888888888888888888888888888",
                "88o_E8888888____========____88888888888888888888888888888",
                "88w*_8888888__it________ti__88888888888888888888888888888",
                "88___8888888m_______z______E88888888888888888888888888888",
                "88___88888888888888B8888888888888888888888888888888888888",
                "888$88888888888888...888888888888888888888888888888888888",
                "8*_____88888888888.*.88888888888888787888%............\"88",
                "8_p__z_88888888888...888888888888....b.88.i..........p.78",
                "8______888888888888D8888888888888...*..88..==========..78",
                "8______8h......................C8.z....78..==========..78",
                "8_====_8..|.z.*.......p..*...|..8......88..==t....t==..78",
                "8_====_D.......%...e...%...z....8.....G78..==...*..==..78",
                "8_=C_=_8..........z.............8......D...##....\\.==..78",
                "8_====_8;;;;,,;;;;;;;;;;;;,,;;;;8....z.78..==......==..78",
                "8_====_8;;;;,,;;;;;;;;;;;;,,;;;;8......88..==t\"...t==..78",
                "8e____*8........................8&.*...78..==========..78",
                "8______8............aa..%.......D......88..==========..78",
                "8____p_8..|...*.....i....*...|..8..i...78.i..........p.78",
                "8______8.z......%.............z.8......88%............\"88",
                "8m____C8e......................a8.....t888888888888888888",
                "8888888888888888833D338888888888888$888888888888888888888",
                "888888888888888883___3888888888888..q88888888888888888888",
                "888888888888888883_*_3888888888888w..88888888888888888888",
                "888888888888888883_^_3888888888888.*.88888888888888888888",
                "888888888888888883__a3888888888888..Q88888888888888888888",
                "888888888888888883333388888888888888888888888888888888888",
                "888888888888888888888888888888888888888888888888888888888",
                },
            };
        }

        static LevelDef Hell3()
        {
            return new LevelDef
            {
                Id = "E2M3", Name = "GATES OF HELL", Music = 3,
                Intro = "THE LAST GATE IS OPEN. THE WARDEN IS WAITING BEYOND IT.",
                FloorA = Tex.F_HELL, CeilA = Tex.C_FLESH, FloorB = Tex.F_MARBLE, CeilB = Tex.C_STONE, FloorOut = Tex.F_HELL,
                Ambient = Col.Rgb(98, 64, 58), SkyLight = Col.Rgb(240, 150, 115), FogColor = Col.Rgb(26, 4, 2), FogDensity = 0.052f, Par = "5:00",
                Map = new[]
                {
                "666666666666666666555X5556666666666666666666666",
                "6666666666666666665!___!56666666666666666666666",
                "6666666666666666665_____56666666666666666666666",
                "666655555555555555555Y5555555555555555556666666",
                "66660m,,!,,,,,,,,,,,,,,,,,,,,,,,,,!,,,m06666666",
                "66660,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,06666666",
                "66660,,,,,,,|,,,,i,,,K,,,i,,,,|,,,,,,,,06666666",
                "66660,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,06666666",
                "66660,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,06666666",
                "66660:::::,,::::::::,,,::::::::,,::::::06666666",
                "66660,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,06666666",
                "66660,,,,,,,,,,,,,,,,q,,,,,,,,,,,,,,,,,06666666",
                "66660,,,,,,,,,:::,,h,q,h,,:::,,,,,,,,,,06666666",
                "66660,,,,,,,|,:::,,,,,,,,,:::,|,,,,,,,,06666666",
                "66660,,,,,,,,,,,,,,,,U,,,,,,,,,,,,,,,,,06666666",
                "66660,Q,!,,,,,,,,,,,,,,,,,,,,,,,,,!,,E,06666666",
                "666655555555555555555D5555555555555555556666666",
                "6000000000000005!_i_____i_!566!...6........6666",
                "60::::::::::::05_Q_______Q_566.q..6.r.....m6666",
                "60,,,,::::::::05__q_____q__566....6.....z..6666",
                "60,,i,,,::::::05!_m_____h_!566....6.\\......6666",
                "60,,,Q:,::::::9999955R55999999....6...p....6666",
                "60:::::,::,,E:9C_!________!_a9..z.6!......!6666",
                "60:::::,::,p,:9~~__________~~9....6........6666",
                "60:::::,#,,,,:9~~_i________~~9....6666666.66666",
                "60:::::,::::::9~~____++____~~9...!6........6666",
                "60:::::,,,,,,,D##___z______##D.+..6..\"..e..6666",
                "60:::::,::::::9~~__k____k__~~9.+..6...p....6666",
                "60:::::,::::::9~~____e_____~~9....6...L....6wo6",
                "60:::::k::::::9__!________!__9....6........$..6",
                "60:::::,::::::9999555D55559999...i6.......C6wU6",
                "60:::::,::::::::::5!____e56666.....z.......6666",
                "60,i,\",,,,p,,:::W:5______56666z.........!..6666",
                "60q,,,,,,,,,m:::::5__^__!56666.......k...i.6666",
                "6000000000000000005a____c56666..m..........6666",
                "66666666666666666655555555666666666666666666666",
                },
            };
        }
    }
}
