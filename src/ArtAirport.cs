// TERMINAL HELL - sprites for the earth episode: travellers, seats, luggage, fires, the check-in desk and the terminal's signs.
using System;
using System.Collections.Generic;

namespace TerminalHell
{
    static partial class Art
    {
        public static Image[] Travelers = new Image[4], DeadTravelers = new Image[3], Luggage = new Image[2];
        public static Image SeatsEmpty, SeatsTaken, SeatsWrecked, Desk, Plant, Vending, Bin, Rubble, Cart, HellSign, Ticket;
        public static Image[] Fire = new Image[3];

        // the scrolling sign hanging from the ceiling of the calm part of the terminal
        const int MarqueeW = 112, MarqueeH = 11;
        const string MarqueeText = "   WELCOME TO THE TERMINAL   ";
        static Image marquee;
        static int marqueeOffset = -1;

        /// <summary>The ceiling sign, text sliding from right to left. All signs share one image.</summary>
        public static Image MarqueeFrame(float time)
        {
            int total = MarqueeText.Length * 7;
            int off = (int)(time * 22) % total;
            if (marquee != null && off == marqueeOffset) return marquee;
            if (marquee == null) marquee = new Image(MarqueeW, MarqueeH);
            marqueeOffset = off;
            int amber = Col.Rgb(255, 188, 40), frame = Col.Rgb(74, 78, 90), back = Col.Rgb(16, 16, 24);
            for (int y = 0; y < MarqueeH; y++)
                for (int x = 0; x < MarqueeW; x++)
                {
                    bool edge = x == 0 || y == 0 || x == MarqueeW - 1 || y == MarqueeH - 1;
                    marquee.Px[y * MarqueeW + x] = (edge ? frame : back) | Col.OPAQUE | (edge ? 0 : Col.EMISSIVE);
                }
            for (int x = 1; x < MarqueeW - 1; x++)
            {
                int sx = (x + off) % total;
                int ci = sx / 7, gx = sx % 7;
                if (gx > 5) continue;
                // bold: every lit dot of the font is drawn one pixel wider, so the letters survive being seen from a distance
                for (int gy = 0; gy < 7; gy++)
                    if (PixFont.Pixel(MarqueeText[ci], gx, gy) || (gx > 0 && PixFont.Pixel(MarqueeText[ci], gx - 1, gy)))
                        marquee.Px[(2 + gy) * MarqueeW + x] = amber | Col.OPAQUE | Col.EMISSIVE;
            }
            marquee.ComputeExtents();
            return marquee;
        }

        static void Text(Canvas c, string s, int x0, int y0, int col)
        {
            for (int i = 0; i < s.Length; i++)
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                        if (PixFont.Pixel(s[i], gx, gy)) c.Plot(x0 + i * 6 + gx, y0 + gy, col);
        }

        /// <summary>Bold lettering: each dot of the font is one pixel wider, letters seven pixels apart.</summary>
        static void BoldText(Canvas c, string s, int x0, int y0, int col)
        {
            for (int i = 0; i < s.Length; i++)
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                        if (PixFont.Pixel(s[i], gx, gy)) { c.Plot(x0 + i * 7 + gx, y0 + gy, col); c.Plot(x0 + i * 7 + gx + 1, y0 + gy, col); }
        }

        static void BuildAirport()
        {
            for (int k = 0; k < 4; k++) Travelers[k] = Traveler(k);
            for (int k = 0; k < 3; k++) DeadTravelers[k] = DeadTraveler(k);
            SeatsEmpty = SeatRow(false);
            SeatsTaken = SeatRow(true);
            SeatsWrecked = WreckedSeats();
            Luggage[0] = LuggageA();
            Luggage[1] = LuggageB();
            Desk = CheckInDesk();
            Plant = PottedPlant();
            Vending = VendingMachine();
            Bin = TrashBin();
            Rubble = RubblePile();
            Cart = LuggageCart();
            for (int f = 0; f < 3; f++) Fire[f] = FireFrame(f);
            HellSign = BuildHellSign();
            Ticket = BuildTicket();
            Items['{'] = Ticket;
            Items['g'] = GunPickup(3);
        }

        /// <summary>Just the earth episode's sprites, for the developer sprite sheet.</summary>
        public static List<Image> AirportSprites()
        {
            var l = new List<Image>();
            l.AddRange(Travelers); l.AddRange(DeadTravelers); l.AddRange(Luggage); l.AddRange(Fire);
            l.Add(SeatsEmpty); l.Add(SeatsTaken); l.Add(SeatsWrecked); l.Add(Desk); l.Add(Plant); l.Add(Vending); l.Add(Bin); l.Add(Rubble); l.Add(Cart);
            l.Add(HellSign); l.Add(Ticket); l.Add(MarqueeFrame(0)); l.Add(Items['g']);
            return l;
        }

        // ---------------------------------------------------------------- people

        static readonly int[] SkinTones = { Col.Rgb(224, 172, 140), Col.Rgb(198, 142, 108), Col.Rgb(150, 100, 74), Col.Rgb(236, 196, 166) };

        static void Outfit(int kind, out int hair, out int top, out int pants)
        {
            switch (kind & 3)
            {
                case 0: hair = Col.Rgb(38, 30, 26); top = Col.Rgb(46, 52, 74); pants = Col.Rgb(38, 42, 58); break;        // suit
                case 1: hair = Col.Rgb(70, 42, 26); top = Col.Rgb(186, 44, 52); pants = Col.Rgb(44, 44, 52); break;       // red coat
                case 2: hair = Col.Rgb(184, 160, 96); top = Col.Rgb(56, 140, 150); pants = Col.Rgb(150, 132, 100); break; // tourist
                default: hair = Col.Rgb(60, 44, 36); top = Col.Rgb(110, 116, 124); pants = Col.Rgb(58, 74, 110); break;   // hooded top
            }
        }

        /// <summary>A traveller standing about with their bags: a businessman, a woman in a red coat, a tourist, someone with a wheeled case.</summary>
        static Image Traveler(int kind)
        {
            var c = new Canvas(34, 62);
            c.NoiseSeed = 210 + kind;
            int hair, top, pants, shoe = Col.Rgb(36, 30, 28);
            Outfit(kind, out hair, out top, out pants);
            int skin = SkinTones[kind & 3];
            c.Limb(14, 38, 4f, 13.5f, 56, 3.4f, pants);
            c.Limb(20, 38, 4f, 20.5f, 56, 3.4f, pants);
            c.Ball(13.5f, 58.5f, 4.2f, 2.6f, shoe);
            c.Ball(21, 58.5f, 4.2f, 2.6f, shoe);
            if (kind == 2) c.Rect(9, 19, 16, 19, Col.Rgb(214, 120, 36), Col.Rgb(150, 80, 24));           // backpack, seen at the sides
            if (kind == 1) c.Ball(17, 16, 6.8f, 8f, hair);                                              // long hair behind the coat
            c.Ball(17, 28, 8.6f, 10.6f, top);
            if (kind == 0)
            {
                c.Poly(new float[] { 14.5f, 19.5f, 19.5f, 19.5f, 17, 33 }, Col.Rgb(238, 238, 242));
                c.Rect(16.4f, 21, 1.5f, 11, Col.Rgb(176, 30, 38));
            }
            if (kind == 1) c.Rect(16.4f, 19.5f, 1.2f, 17, Col.Scale(top, 0.6f));
            if (kind == 3) c.Ball(17, 19.5f, 5f, 2.6f, Col.Scale(top, 0.75f));                          // hood
            c.Limb(9.5f, 22, 3.2f, 8, 36, 2.8f, top);
            c.Limb(24.5f, 22, 3.2f, 26, 36, 2.8f, top);
            c.Ball(8, 38, 2.6f, 2.6f, skin);
            c.Ball(26, 38, 2.6f, 2.6f, skin);
            c.Ball(17, 12.5f, 5.4f, 6.2f, skin);
            c.Ball(17, 9.4f, 5.8f, 4.2f, hair);
            if (kind == 1)
            {
                c.Limb(11.4f, 12, 2.4f, 10.5f, 23, 2.0f, hair);
                c.Limb(22.6f, 12, 2.4f, 23.5f, 23, 2.0f, hair);
            }
            if (kind == 2)
            {
                int hat = Col.Rgb(216, 198, 142);
                c.Ball(17, 8.2f, 8f, 2.1f, hat);
                c.Ball(17, 6.4f, 4.9f, 3.2f, hat);
            }
            c.Ball(15, 13.3f, 0.7f, 0.7f, Col.Rgb(30, 24, 22), false);
            c.Ball(19, 13.3f, 0.7f, 0.7f, Col.Rgb(30, 24, 22), false);
            if (kind == 0)
            {
                c.Line(26, 40, 28, 38, 1, Col.Rgb(60, 44, 30));
                c.Rect(23, 40, 10, 8, Col.Rgb(112, 74, 40), Col.Rgb(74, 48, 26));                       // briefcase
                c.Rect(26, 42, 4, 1.4f, Col.Rgb(210, 180, 90));
            }
            if (kind == 1) c.Rect(24.5f, 33, 8, 7, Col.Rgb(70, 30, 40), Col.Rgb(44, 18, 26));          // handbag
            if (kind == 3)
            {
                c.Line(4.5f, 40, 7.5f, 37.5f, 1, Col.Rgb(150, 150, 156));
                c.Rect(0.5f, 41, 9, 17, Col.Rgb(178, 48, 48), Col.Rgb(112, 28, 28));                    // wheeled case
                c.Rect(1.5f, 44, 7, 1.2f, Col.Rgb(240, 220, 200));
                c.Ball(2.5f, 59, 1.2f, 1.2f, Col.Rgb(30, 30, 30), false);
                c.Ball(7.5f, 59, 1.2f, 1.2f, Col.Rgb(30, 30, 30), false);
            }
            c.Outline(Col.Rgb(20, 16, 14));
            c.GroundShade(0.1f);
            return c.Done();
        }

        /// <summary>Someone who did not get out: on the floor in a pool of blood, a bag beside them.</summary>
        static Image DeadTraveler(int kind)
        {
            var c = new Canvas(50, 14);
            c.NoiseSeed = 230 + kind;
            int hair, top, pants;
            Outfit(kind, out hair, out top, out pants);
            int skin = SkinTones[(kind + 1) & 3];
            c.Ball(25, 12, 22, 2.6f, Col.Rgb(112, 8, 8), false);
            c.Ball(22, 12, 14, 1.6f, Col.Rgb(150, 14, 12), false);
            c.Limb(12, 9, 4.0f, 26, 8, 4.6f, top);
            c.Limb(26, 8, 4.0f, 39, 10, 3.2f, pants);
            c.Limb(39, 10, 3.0f, 47, 11, 2.4f, pants);
            c.Limb(15, 6, 2.0f, 10, 12, 1.8f, top);
            c.Ball(7.5f, 8, 3.6f, 3.4f, skin);
            c.Ball(7f, 6.4f, 3.7f, 2.4f, hair);
            if (kind != 1) c.Rect(31, 0, 8, 5, kind == 0 ? Col.Rgb(112, 74, 40) : Col.Rgb(214, 120, 36));   // a dropped bag
            Splatter(c, 233 + kind, 10, 6, 5, 32, 12, Col.Rgb(140, 12, 10));
            c.Outline(Col.Rgb(12, 8, 6));
            return c.Done();
        }

        // ---------------------------------------------------------------- furniture

        /// <summary>A row of three waiting-area seats; taken ones have someone sitting in them.</summary>
        static Image SeatRow(bool occupied)
        {
            var c = new Canvas(58, 34);
            c.NoiseSeed = 240;
            int frame = Col.Rgb(70, 74, 84), shell = Col.Rgb(50, 84, 150), cushion = Col.Rgb(70, 108, 176);
            c.Rect(2, 27, 54, 2.5f, Col.Rgb(120, 124, 134), Col.Rgb(80, 84, 94));
            c.Rect(6, 29, 2.5f, 5, frame);
            c.Rect(27.7f, 29, 2.5f, 5, frame);
            c.Rect(49.5f, 29, 2.5f, 5, frame);
            int[] shirts = { Col.Rgb(190, 60, 50), Col.Rgb(70, 130, 90), Col.Rgb(210, 190, 80) };
            for (int i = 0; i < 3; i++)
            {
                float x0 = 3 + i * 18;
                float cx = x0 + 8.5f;
                c.Rect(x0 + 1, 11, 15, 13, Col.Scale(shell, 1.1f), Col.Scale(shell, 0.72f));        // back rest
                if (occupied && i != 1)
                {
                    int hair, top, pants;
                    Outfit(i * 2 + 1, out hair, out top, out pants);
                    c.Limb(cx - 3, 24, 2.8f, cx - 3, 31, 2.4f, pants);
                    c.Limb(cx + 3, 24, 2.8f, cx + 3, 31, 2.4f, pants);
                    c.Ball(cx, 16, 6f, 7.4f, shirts[i]);
                    c.Ball(cx, 5.8f, 3.9f, 4.5f, SkinTones[i + 1]);
                    c.Ball(cx, 3.8f, 4.1f, 2.7f, hair);
                    c.Limb(cx - 6, 14, 2f, cx - 5, 22, 1.8f, shirts[i]);
                    c.Limb(cx + 6, 14, 2f, cx + 5, 22, 1.8f, shirts[i]);
                }
                c.Rect(x0, 23, 17, 4.6f, cushion, Col.Scale(cushion, 0.7f));                         // seat
                c.Rect(x0 + 16, 19, 1.6f, 8, frame);                                                // arm rest
            }
            c.Rect(2.4f, 19, 1.6f, 8, frame);
            c.Outline(Col.Rgb(14, 16, 22));
            return c.Done();
        }

        /// <summary>What is left of a row of seats: one on its side, one scorched, the frame bent.</summary>
        static Image WreckedSeats()
        {
            var c = new Canvas(50, 26);
            c.NoiseSeed = 250;
            int shell = Col.Rgb(40, 52, 82), soot = Col.Rgb(26, 22, 22);
            c.Ball(25, 24, 22, 2.4f, soot, false);
            // the one still standing, burnt black at the top
            c.Rect(29, 8, 15, 13, Col.Rgb(56, 60, 70), soot);
            c.Rect(28, 20, 17, 4, Col.Rgb(40, 56, 92), Col.Rgb(24, 26, 34));
            c.Rect(29, 24, 2.4f, 2, Col.Rgb(60, 62, 70));
            c.Rect(42, 24, 2.4f, 2, Col.Rgb(60, 62, 70));
            // the one on its side
            c.Poly(new float[] { 2, 21, 18, 12, 22, 21, 6, 25 }, shell, Col.Scale(shell, 0.6f));
            c.Poly(new float[] { 4, 17, 10, 6, 15, 8, 9, 20 }, Col.Scale(shell, 1.2f), shell);
            c.Line(20, 22, 26, 18, 1.4f, Col.Rgb(90, 92, 100));
            c.Line(23, 24, 29, 24, 1.2f, Col.Rgb(90, 92, 100));
            Splatter(c, 251, 6, 5, 12, 26, 24, Col.Rgb(120, 10, 10));
            c.Outline(Col.Rgb(10, 10, 12));
            return c.Done();
        }

        static Image CheckInDesk()
        {
            var c = new Canvas(64, 36);
            c.NoiseSeed = 260;
            c.Rect(0, 15, 64, 21, Col.Rgb(238, 240, 244), Col.Rgb(170, 176, 188));          // front of the counter
            c.Rect(0, 11, 64, 5, Col.Rgb(78, 84, 98), Col.Rgb(46, 50, 60));                 // its top
            c.Rect(0, 30, 64, 6, Col.Rgb(52, 56, 66));                                      // kick plate
            c.Rect(5, 17, 54, 9, Col.Rgb(40, 78, 150), Col.Rgb(30, 60, 120));
            Text(c, "CHECK-IN", 8, 18, Col.Rgb(250, 250, 255) | Col.EMISSIVE);
            // a screen on a stalk, and a scale belt
            c.Rect(45, 4, 15, 9, Col.Rgb(28, 30, 36));
            c.Rect(46.5f, 5.5f, 12, 6, Col.Rgb(96, 190, 255) | Col.EMISSIVE);
            c.Rect(51.5f, 12, 2, 1.5f, Col.Rgb(60, 62, 70));
            c.Rect(6, 6, 22, 5, Col.Rgb(44, 46, 52));
            c.Rect(7, 7, 20, 2, Col.Rgb(120, 124, 132));
            c.Outline(Col.Rgb(14, 16, 22));
            return c.Done();
        }

        static Image PottedPlant()
        {
            var c = new Canvas(24, 46);
            c.NoiseSeed = 270;
            c.Rect(3, 30, 18, 3, Col.Rgb(170, 96, 62), Col.Rgb(120, 62, 40));
            c.Tube(4, 33, 16, 13, Col.Rgb(158, 84, 54), true);
            c.Ball(12, 30.5f, 8, 1.6f, Col.Rgb(50, 34, 24), false);
            float[][] leaf = { new[] { 12f, 5f }, new[] { 4f, 12f }, new[] { 20f, 11f }, new[] { 5f, 21f }, new[] { 19f, 21f }, new[] { 12f, 15f } };
            for (int i = 0; i < leaf.Length; i++)
            {
                int g = Col.Rgb(46 + i * 4, 96 - i * 5, 44);
                c.Limb(12, 31, 1.3f, leaf[i][0], leaf[i][1], 0.8f, Col.Rgb(56, 74, 38));
                c.Ball(leaf[i][0], leaf[i][1], 4.6f, 3.4f, g);
                c.Ball(leaf[i][0] - 1, leaf[i][1] - 1, 2f, 1.4f, Col.Scale(g, 1.4f));
            }
            c.Outline(Col.Rgb(10, 22, 10));
            return c.Done();
        }

        static Image LuggageA()
        {
            var c = new Canvas(34, 24);
            c.NoiseSeed = 280;
            c.Rect(1, 9, 19, 14, Col.Rgb(46, 86, 170), Col.Rgb(28, 52, 110));
            c.Rect(8, 6.5f, 5, 2.5f, Col.Rgb(20, 24, 40));
            c.Rect(1, 13, 19, 1.4f, Col.Rgb(210, 214, 226));
            c.Rect(5, 1, 12, 8, Col.Rgb(190, 44, 48), Col.Rgb(120, 26, 30));
            c.Rect(9, 0, 4, 1.8f, Col.Rgb(20, 24, 40));
            c.Ball(27, 18, 6.4f, 5, Col.Rgb(70, 110, 76));
            c.Rect(22, 14, 10, 1.2f, Col.Rgb(30, 50, 34));
            c.Outline(Col.Rgb(12, 12, 16));
            return c.Done();
        }

        static Image LuggageB()
        {
            var c = new Canvas(34, 18);
            c.NoiseSeed = 281;
            c.Ball(9, 10, 7.4f, 7.4f, Col.Rgb(214, 122, 38));
            c.Rect(5, 12, 8, 3, Col.Rgb(150, 82, 26));
            c.Rect(14, 6, 18, 11, Col.Rgb(44, 44, 50), Col.Rgb(24, 24, 28));
            c.Rect(20, 3.5f, 6, 2.6f, Col.Rgb(20, 20, 24));
            c.Rect(15, 11, 16, 1.2f, Col.Rgb(160, 160, 170));
            c.Outline(Col.Rgb(12, 12, 14));
            return c.Done();
        }

        static Image VendingMachine()
        {
            var c = new Canvas(24, 48);
            c.NoiseSeed = 290;
            c.Rect(1, 1, 22, 47, Col.Rgb(54, 84, 160), Col.Rgb(30, 46, 100));
            c.Rect(2, 2, 20, 4, Col.Rgb(240, 60, 56) | Col.EMISSIVE);
            c.Rect(3, 8, 15, 30, Col.Rgb(16, 20, 30));
            int[] cans = { Col.Rgb(240, 60, 60), Col.Rgb(240, 240, 240), Col.Rgb(80, 200, 90), Col.Rgb(250, 190, 40), Col.Rgb(70, 150, 240) };
            for (int row = 0; row < 3; row++)
            {
                c.Rect(3.5f, 15 + row * 9.5f, 14, 1, Col.Rgb(150, 154, 162));
                for (int k = 0; k < 5; k++) c.Rect(4.5f + k * 2.8f, 9.5f + row * 9.5f, 2.2f, 5.2f, cans[(k + row * 2) % 5] | Col.EMISSIVE);
            }
            c.Rect(19, 9, 3, 10, Col.Rgb(150, 154, 162));
            for (int k = 0; k < 4; k++) c.Rect(19.5f, 10 + k * 2.2f, 2, 1.2f, Col.Rgb(40, 42, 50));
            c.Rect(19, 22, 3, 2, Col.Rgb(10, 10, 12));
            c.Rect(3, 40, 15, 6, Col.Rgb(10, 12, 18));
            c.Outline(Col.Rgb(10, 12, 20));
            return c.Done();
        }

        static Image TrashBin()
        {
            var c = new Canvas(14, 20);
            c.NoiseSeed = 300;
            c.Tube(1.5f, 4, 11, 16, Col.Rgb(112, 118, 128), true);
            c.Ball(7, 4.5f, 5.6f, 1.8f, Col.Rgb(150, 154, 162));
            c.Ball(7, 4.6f, 4.2f, 1.2f, Col.Rgb(14, 14, 16), false);
            c.Rect(2, 12, 10, 1.4f, Col.Rgb(70, 76, 86));
            c.Outline(Col.Rgb(10, 12, 16));
            return c.Done();
        }

        /// <summary>Broken concrete with rebar sticking out of it, for cover in the ruined part of the terminal.</summary>
        static Image RubblePile()
        {
            var c = new Canvas(44, 26);
            c.NoiseSeed = 310;
            c.NoiseAmt = 0.12f;
            c.Ball(22, 24, 20, 2.4f, Col.Rgb(40, 36, 34), false);
            float[][] chunks =
            {
                new[] { 2f, 24f, 6f, 12f, 16f, 10f, 20f, 24f },
                new[] { 14f, 24f, 18f, 6f, 30f, 5f, 34f, 24f },
                new[] { 28f, 24f, 32f, 13f, 42f, 15f, 43f, 24f },
                new[] { 10f, 16f, 15f, 9f, 24f, 12f, 21f, 20f },
            };
            for (int i = 0; i < chunks.Length; i++)
            {
                int g = 96 + i * 16;
                c.Poly(chunks[i], Col.Rgb(g + 30, g + 28, g + 20), Col.Rgb(g - 26, g - 28, g - 34));
            }
            c.Line(20, 12, 25, 1, 1.1f, Col.Rgb(120, 70, 44));
            c.Line(33, 9, 30, 0, 1.1f, Col.Rgb(120, 70, 44));
            c.Line(8, 14, 5, 6, 1.0f, Col.Rgb(120, 70, 44));
            Splatter(c, 311, 5, 6, 14, 30, 24, Col.Rgb(20, 16, 14));
            c.Outline(Col.Rgb(14, 12, 12));
            return c.Done();
        }

        static Image LuggageCart()
        {
            var c = new Canvas(52, 30);
            c.NoiseSeed = 320;
            c.Rect(0, 21, 52, 4, Col.Rgb(140, 144, 152), Col.Rgb(84, 88, 98));
            c.Ball(9, 27, 3.6f, 3.6f, Col.Rgb(28, 28, 32));
            c.Ball(43, 27, 3.6f, 3.6f, Col.Rgb(28, 28, 32));
            c.Rect(0, 8, 1.6f, 14, Col.Rgb(110, 114, 122));
            c.Rect(50.4f, 8, 1.6f, 14, Col.Rgb(110, 114, 122));
            int[] cols = { Col.Rgb(44, 84, 166), Col.Rgb(184, 46, 50), Col.Rgb(50, 50, 56), Col.Rgb(210, 120, 40), Col.Rgb(70, 120, 80) };
            float x = 3;
            for (int i = 0; i < 5; i++)
            {
                float w = 8 + Noise.Hashf(i, 1, 321) * 5, h = 9 + Noise.Hashf(i, 2, 321) * 6;
                c.Rect(x, 21 - h, w, h, Col.Scale(cols[i], 1.15f), Col.Scale(cols[i], 0.65f));
                x += w + 1;
            }
            c.Rect(8, 6, 14, 6, Col.Rgb(214, 120, 40), Col.Rgb(140, 76, 24));
            c.Outline(Col.Rgb(10, 10, 14));
            return c.Done();
        }

        // ---------------------------------------------------------------- fire

        /// <summary>A fire in wreckage: three frames of flickering flame over a black heap.</summary>
        static Image FireFrame(int f)
        {
            var c = new Canvas(30, 44);
            c.NoiseSeed = 330 + f;
            int hot = Col.Rgb(255, 236, 130), red = Col.Rgb(220, 46, 8);
            c.Ball(15, 41, 13, 4, Col.Rgb(26, 22, 20));
            c.Ball(9, 39, 5, 3, Col.Rgb(38, 34, 32));
            c.Ball(21, 40, 5, 2.6f, Col.Rgb(34, 30, 30));
            float s = f - 1;
            c.Glow(15, 34, 11, Col.Rgb(255, 210, 80), Col.Rgb(160, 40, 6));
            // three tongues, taller in the middle, leaning a little differently on each frame
            c.Poly(new float[] { 4, 40, 6 + s, 22 - f, 9, 30, 11, 40 }, red | Col.EMISSIVE, hot);
            c.Poly(new float[] { 19, 40, 22 - s, 20 + f, 25, 32, 26, 40 }, red | Col.EMISSIVE, hot);
            c.Poly(new float[] { 8, 40, 12 + s * 2, 8 + f * 2, 15, 22 + f, 17 + s, 6 + (2 - f) * 2, 20, 26, 22, 40 }, red | Col.EMISSIVE, hot);
            c.Poly(new float[] { 11, 40, 14 + s, 20 + f, 16, 28, 19, 40 }, Col.Rgb(255, 214, 90) | Col.EMISSIVE, Col.Rgb(255, 252, 214));
            c.Ball(13 + s * 2, 8 + f, 1.4f, 1.4f, Col.Rgb(255, 170, 60) | Col.EMISSIVE, false);   // a spark
            return c.Done();
        }

        // ---------------------------------------------------------------- signs and pickups

        /// <summary>The sign at the ruined end of the terminal: the same words, one more, red - and hanging by one chain.</summary>
        static Image BuildHellSign()
        {
            const int W = 178, H = 30;
            var flat = new Canvas(W, 14);
            flat.NoiseSeed = 340;
            flat.Rect(0, 0, W, 14, Col.Rgb(20, 14, 14));
            flat.Rect(0, 0, W, 1, Col.Rgb(84, 74, 70));
            flat.Rect(0, 13, W, 1, Col.Rgb(60, 52, 50));
            BoldText(flat, "WELCOME TO TERMINAL", 4, 3, Col.Rgb(236, 232, 218) | Col.EMISSIVE);
            BoldText(flat, "HELL", 4 + 20 * 7, 3, Col.Rgb(255, 36, 24) | Col.EMISSIVE);
            // dead segments in the lettering
            for (int i = 0; i < 20; i++)
            {
                int x = 4 + (int)(Noise.Hashf(i, 1, 341) * 134), y = 3 + (int)(Noise.Hashf(i, 2, 341) * 7);
                flat.Plot(x, y, Col.Rgb(20, 14, 14));
            }
            var c = new Canvas(W, H);
            // the panel hangs straight so the lettering stays readable; only the chains say it is broken
            for (int x = 0; x < W; x++)
                for (int y = 0; y < 14; y++) c.Plot(x, y + 7, flat.Get(x, y));
            c.Line(12, 0, 12, 7, 1, Col.Rgb(140, 140, 148));                   // the chain that is still holding
            c.Line(13, 0, 13, 7, 1, Col.Rgb(70, 70, 76));
            c.Line(W - 14, 0, W - 22, 6, 1, Col.Rgb(140, 140, 148));         // and the one that isn't, swinging free
            // drips of red under "HELL"
            for (int k = 0; k < 4; k++)
            {
                int x = 4 + 20 * 7 + k * 7 + 2;
                int top = 7 + 14;
                int len = 3 + (int)(Noise.Hashf(k, 3, 342) * 5);
                for (int y = top; y < top + len && y < H; y++) c.Plot(x, y, Col.Rgb(190, 20, 14) | Col.EMISSIVE);
            }
            return c.Done();
        }

        /// <summary>Your boarding pass, lying on the floor.</summary>
        static Image BuildTicket()
        {
            var c = new Canvas(20, 14);
            c.NoiseSeed = 350;
            c.Glow(10, 7, 9, Col.Rgb(255, 250, 200), Col.Rgb(90, 120, 200));
            c.Rect(1.5f, 2, 17, 10, Col.Rgb(248, 248, 252), Col.Rgb(206, 210, 224));
            c.Rect(1.5f, 2, 17, 3, Col.Rgb(40, 110, 224) | Col.EMISSIVE);
            for (int i = 0; i < 9; i++) c.Rect(3 + i * 1.5f, 7, i % 3 == 0 ? 1f : 0.6f, 4, Col.Rgb(26, 26, 32));
            c.Rect(14, 6, 3, 1, Col.Rgb(210, 40, 40));
            c.Rect(13.4f, 5, 0.6f, 6.4f, Col.Rgb(150, 154, 170));
            c.Outline(Col.Rgb(12, 16, 30));
            return c.Done();
        }
    }
}
