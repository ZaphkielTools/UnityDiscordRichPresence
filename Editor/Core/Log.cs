using System.Collections.Generic;
using UnityEngine;

namespace Zaphkiel.DiscordRichPresence
{
    internal static class Log
    {
        const string Prefix = "[Discord Rich Presence] ";
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static bool Verbose { get; set; }

        public static void Info(string message)
        {
            if (Verbose) Debug.Log(Prefix + message);
        }

        public static void WarningOnce(string message)
        {
            if (Reported.Add(message)) Debug.LogWarning(Prefix + message);
        }
    }
}
