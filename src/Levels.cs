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
            var list = new[] { Terminal(), Hell1(), Hell2(), Hell3() };
            list[0].Episode = 0;
            for (int i = 1; i < list.Length; i++) list[i].Episode = 1;
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
                "VVVVVVVVVVVVVVF..........AVVVVVVVVIIIIII_s__*_____z__I..VVVVVVVV",
                "VVVVVVVVVVVVVVF......u.s.HVVVVVVVVI_m__I__s____@____d$bwVVVVVVVV",
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


        static LevelDef Hell1()
        {
            return new LevelDef
            {
                Id = "E2M1", Name = "OUTPOST GATE", Music = 1,
                Intro = "THE RELAY OUTPOST WENT DARK THREE HOURS AGO.",
                FloorA = Tex.F_TILE, CeilA = Tex.C_PANEL, FloorB = Tex.F_METAL, CeilB = Tex.C_STONE, FloorOut = Tex.F_DIRT,
                Ambient = Col.Rgb(82, 79, 86), SkyLight = Col.Rgb(240, 190, 160), FogColor = Col.Rgb(10, 4, 3), FogDensity = 0.05f, Par = "2:30",
                Map = new[]
                {
                "3333333333333333333330000000000000000000",
                "333333337X733333333330000000000000000000",
                "33337t.......t7333332,,,,,,,,,,,,,,,,,,0",
                "33333.z.....i.3333332,,,,,,,,,22222222,0",
                "33337..c......7333332,,,z,,,,,2..i...2,0",
                "33333.*.....*.3333332,,,,,,,,,2.m..r.2,0",
                "333333333R33333333332,,,,,,,,,2..*.i.2,0",
                "33.............%33332,,|,,,,,,2......2,0",
                "33.z....*.....z.33332,,,,,,,,,2.a....2,0",
                "33...|......|...33332,,,,,,,,,222D2222,0",
                "33..............D___D,,,,,,,,,,!,,,!,,,0",
                "33........z.....33332,,,,,,,,,,,,,,,,,,0",
                "33...|......|...33332,,,,,,z,,,,,,,,,,,0",
                "33.......*......33332,,,,,,,,,,,,,,,,,,0",
                "33c............h33332,,,,,,,,%,,,,,,,,,0",
                "33333D333333333333332,,,;;;;;,%,,,,,,,,0",
                "3333___33333333333332,,,;;;;;,,,,i,,,,,0",
                "3333___33333333333332,,,;;;;;,,,,,,,,,,0",
                "3333_*_3333%___C33332,,,;;;;;,,,,,,,,,,0",
                "3333___3333_____33332,,,;;;;;,,,,,,,,,,0",
                "3333___3333__z__33332,,,,e,,,,,,,,,,|,,0",
                "33......&.3_____33332,,,,,%,%,,,,,,,,,,0",
                "33...*....3e_*__33332,,,,,,S,,,,,,,,,,,0",
                "33........D_____3__G2,,,,,,,,,,,,,,z,,,0",
                "33...^....3_____$_w_2,,,,,,,,,,,,,,,,,,0",
                "33a......c3%___%3aa_2,&,,,,,,,,,,,,,,m,0",
                "33........3_____33332,,x,,,,,,,,,,,,,,,0",
                "333333333333333333332,,,,,,,,,,,,,,,,,,0",
                "3333333333333333333330000000000000000000",
                "3333333333333333333330000000000000000000",
                },
            };
        }

        static LevelDef Hell2()
        {
            return new LevelDef
            {
                Id = "E2M2", Name = "TOXIC REFINERY", Music = 2, Sky = 1,
                Intro = "THE REFINERY PUMPS SOMETHING THAT IS NOT OIL.",
                FloorA = Tex.F_METAL, CeilA = Tex.C_PANEL, FloorB = Tex.F_GRATE, CeilB = Tex.C_STONE, FloorOut = Tex.F_GRATE,
                Ambient = Col.Rgb(68, 78, 68), SkyLight = Col.Rgb(135, 125, 165), FogColor = Col.Rgb(4, 12, 4), FogDensity = 0.072f, Par = "3:30",
                Map = new[]
                {
                "8888888888888888888888888888888888888888",
                "8888888888888888887X78888888888888888888",
                "888888888888%______________%888888888888",
                "888888888888_p_t________t_p_888888888888",
                "888888888888____========____888888888888",
                "888888888888____========____888888888888",
                "88o_E8888888____========____888888888888",
                "88w*_8888888__it________ti__888888888888",
                "88___8888888m_______z______E888888888888",
                "88___88888888888888B88888888888888888888",
                "888$88888888888888...8888888888888888888",
                "8*_____88888888888.*.8888888888888878788",
                "8_p__z_88888888888...888888888888....b.8",
                "8______888888888888D8888888888888...*..8",
                "8______8h......................C8.z....7",
                "8_====_8..|.z.*.......p..*...|..8......8",
                "8_====_D.......%...e...%...z....8.....G7",
                "8_=N_=_8..........z.............8......8",
                "8_====_8;;;;,,;;;;;;;;;;;;,,;;;;8....z.7",
                "8_====_8;;;;,,;;;;;;;;;;;;,,;;;;8......8",
                "8e____*8........................8&.*...7",
                "8______8............aa..%.......D......8",
                "8____p_8..|...*.....i....*...|..8..i...7",
                "8______8.z......%.............z.8......8",
                "8m____C8e......................a8.....t8",
                "8888888888888888833D338888888888888$8888",
                "888888888888888883___3888888888888..q888",
                "888888888888888883_*_3888888888888w..888",
                "888888888888888883_^_3888888888888.*.888",
                "888888888888888883__a3888888888888..Q888",
                "8888888888888888833333888888888888888888",
                "8888888888888888888888888888888888888888",
                },
            };
        }

        static LevelDef Hell3()
        {
            return new LevelDef
            {
                Id = "E2M3", Name = "GATES OF HELL", Music = 3,
                Intro = "THE GATE IS OPEN. THE WARDEN IS WAITING.",
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
                "60000000000000066665___5666666!...6........6666",
                "60::::::::::::066665!_!5666666.q..6.r.....m6666",
                "60,,,,::::::::066665___5666666....6.....z..6666",
                "60,,i,,,::::::066665___5666666....6........6666",
                "60,,,Q:,::::::9999955R55999999....6...p....6666",
                "60:::::,::,,E:9C_!________!_a9..z.6!......!6666",
                "60:::::,::,p,:9~~__________~~9....6........6666",
                "60:::::,#,,,,:9~~_i________~~9....6666666.66666",
                "60:::::,::::::9~~____++____~~9...!6........6666",
                "60:::::,,,,,,,D##___z______##D.+..6.....e..6666",
                "60:::::,::::::9~~__k____k__~~9.+..6...p....6666",
                "60:::::,::::::9~~____e_____~~9....6...L....6wo6",
                "60:::::k::::::9__!________!__9....6........$..6",
                "60:::::,::::::9999555D55559999...i6.......C6wU6",
                "60:::::,::::::::::5!____e56666.....z.......6666",
                "60,i,,,,,,p,,:::W:5______56666z.........!..6666",
                "60q,,,,,,,,,m:::::5__^__!56666.......k...i.6666",
                "6000000000000000005a____c56666..m..........6666",
                "66666666666666666655555555666666666666666666666",
                },
            };
        }
    }
}
