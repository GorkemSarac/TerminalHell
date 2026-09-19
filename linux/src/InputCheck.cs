// TERMINAL HELL - Linux: `terminalhell --input` shows what this terminal and system give the game
// (key releases, focus reports, mouse modes, /dev/input access) and every key and mouse event as it happens.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TerminalHell
{
    static class InputCheck
    {
        public static int Run(List<string> args)
        {
            var log = new List<string>();
            Term.Init(new Settings());
            try
            {
                Input.Trace = line => { log.Add(line); if (log.Count > 300) log.RemoveRange(0, 150); };
                Input.Init();
                bool look = false;
                long lookX = 0, lookY = 0;
                while (true)
                {
                    Input.CaptureWanted = look;
                    Input.Update();
                    if (Input.Hit(Input.VK_ESCAPE) || Input.Hit('Q')) break;
                    if (Input.Hit('M')) { look = !look; lookX = lookY = 0; }
                    if (look) { lookX += Input.MouseDX; lookY += Input.MouseDY; }
                    Draw(log, look, lookX, lookY);
                    Thread.Sleep(30);
                }
            }
            finally
            {
                Input.Trace = null;
                Input.Shutdown();
                Term.Restore();
            }
            return 0;
        }

        static void Draw(List<string> log, bool look, long lookX, long lookY)
        {
            int cols, rows;
            Term.GetSize(out cols, out rows);
            var lines = new List<string>();
            lines.Add("\x1b[1;38;2;255;120;60mTERMINAL HELL - INPUT CHECK\x1b[0m     Esc or Q: quit    M: " + (look ? "stop the mouse look test" : "test mouse look"));
            lines.Add("");
            lines.Add("terminal   : " + Term.Env("TERM") + (Term.Env("TERM_PROGRAM") != "-" ? " (" + Term.Env("TERM_PROGRAM") + ")" : "") + ", " + cols + " x " + rows + " cells");
            lines.AddRange(Input.Status());
            lines.Add("");

            var held = new StringBuilder();
            for (int vk = 1; vk < 256; vk++)
                if (Input.Down(vk)) held.Append(VtInput.KeyName(vk)).Append(' ');
            lines.Add("keys held  : " + (held.Length > 0 ? held.ToString() : "-"));
            lines.Add("mouse      : cell " + Input.MouseCellX + "," + Input.MouseCellY + "   buttons " + (Input.LButton ? "L" : "-") + (Input.MButton ? "M" : "-") + (Input.RButton ? "R" : "-") +
                (look ? "   look test: moved " + lookX + ", " + lookY + (Input.Captured ? "" : " (not captured: click the window?)") : ""));
            lines.Add("");
            lines.Add("\x1b[38;2;160;160;160mevents (newest last):\x1b[0m");
            int room = Math.Max(0, rows - lines.Count - 1);
            for (int i = Math.Max(0, log.Count - room); i < log.Count; i++) lines.Add("  " + log[i]);

            var sb = new StringBuilder("\x1b[H");
            for (int i = 0; i < lines.Count && i < rows; i++)
            {
                string l = lines[i];
                sb.Append(l).Append("\x1b[K");
                if (i < rows - 1 && i < lines.Count - 1) sb.Append("\r\n");
            }
            sb.Append("\x1b[J");
            Term.Write(sb.ToString());
        }
    }
}
