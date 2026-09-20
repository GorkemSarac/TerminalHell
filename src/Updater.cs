// TERMINAL HELL - looks up the published version on GitHub and, if it is newer than this build, offers to install it.
// The check runs on a background thread with a short timeout, so a slow or missing connection never delays the game.
// A build that is newer than the published one (a local dev build) is left alone.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace TerminalHell
{
    static class Updater
    {
        public const string Repo = "GorkemSarac/TerminalHell";   // GitHub "owner/repository" (change with tools\set-repo.ps1)
        const string Branch = "main";

        /// <summary>The published version, once it is known to be newer than this one. Null otherwise.</summary>
        public static string Newest;

        public static bool Available { get { return Newest != null; } }

        public static void CheckInBackground(Settings s)
        {
            if (!s.UpdateCheck) return;
            try
            {
                var t = new Thread(Check);
                t.IsBackground = true;
                t.Name = "update check";
                t.Start();
            }
            catch { }
        }

        static void Check()
        {
            try
            {
                // GitHub needs TLS 1.2, which .NET Framework 4 does not pick by default
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
                var req = (HttpWebRequest)WebRequest.Create("https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/version.txt");
                req.Timeout = 4000;
                req.ReadWriteTimeout = 4000;
                req.UserAgent = "TerminalHell/" + Program.Version;
                string published;
                using (var resp = req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                    published = (reader.ReadLine() ?? "").Trim();
                if (IsNewer(published, Program.Version)) Newest = published;
            }
            catch { }   // no connection, no repository, no problem
        }

        /// <summary>True when version a is higher than version b ("1.10.0" beats "1.9.9"). Anything unreadable counts as not newer.</summary>
        public static bool IsNewer(string a, string b)
        {
            var pa = Parts(a);
            var pb = Parts(b);
            if (pa == null || pb == null) return false;
            for (int i = 0; i < 3; i++)
            {
                if (pa[i] > pb[i]) return true;
                if (pa[i] < pb[i]) return false;
            }
            return false;
        }

        static int[] Parts(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            var f = v.Trim().TrimStart('v', 'V').Split('.');
            if (f.Length < 2) return null;
            var n = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (i >= f.Length) { n[i] = 0; continue; }
                int x;
                if (!int.TryParse(f[i], out x) || x < 0) return null;
                n[i] = x;
            }
            return n;
        }

        /// <summary>Hands over to the installer in its own window. The game has to quit so its files can be replaced.</summary>
        public static bool Install()
        {
            try
            {
#if LINUX
                var psi = new ProcessStartInfo("/bin/sh",
                    "-c \"curl -fsSL https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/linux/install.sh | sh\"");
#else
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/install.ps1 | iex\"");
#endif
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }
    }
}
