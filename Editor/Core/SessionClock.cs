using System;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;

namespace Zaphkiel.DiscordRichPresence
{
    internal static class SessionClock
    {
        const string SceneStartKey = "Zaphkiel.DiscordRichPresence.SceneStart";
        static long editorStart;

        public static long EditorStart
        {
            get
            {
                if (editorStart != 0) return editorStart;
                using var process = Process.GetCurrentProcess();
                editorStart = new DateTimeOffset(process.StartTime).ToUnixTimeSeconds();
                return editorStart;
            }
        }

        public static long SceneStart =>
            long.TryParse(SessionState.GetString(SceneStartKey, ""), NumberStyles.None, CultureInfo.InvariantCulture, out long start)
                ? start
                : EditorStart;

        public static void MarkSceneStart() =>
            SessionState.SetString(SceneStartKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
    }
}
