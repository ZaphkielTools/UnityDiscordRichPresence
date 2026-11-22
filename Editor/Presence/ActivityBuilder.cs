using System;

namespace Zaphkiel.DiscordRichPresence
{
    internal readonly struct PresenceOptions
    {
        public readonly bool ShowProjectName;
        public readonly bool ShowSceneName;

        public PresenceOptions(bool showProjectName, bool showSceneName)
        {
            ShowProjectName = showProjectName;
            ShowSceneName = showSceneName;
        }
    }

    internal readonly struct EditorSnapshot
    {
        public readonly string ProjectName;
        public readonly string SceneName;
        public readonly string UnityVersion;
        public readonly ProjectKind Kind;
        public readonly long Start;

        public EditorSnapshot(string projectName, string sceneName, string unityVersion, ProjectKind kind, long start)
        {
            ProjectName = projectName;
            SceneName = sceneName;
            UnityVersion = unityVersion;
            Kind = kind;
            Start = start;
        }
    }

    internal static class ActivityBuilder
    {
        public const int MaxTextLength = 128;
        public const string ScenePrefix = "Scene : ";
        public const string ProjectPrefix = "Project : ";
        public const string UntitledScene = "Untitled";

        public static Activity Build(in EditorSnapshot editor, in PresenceOptions options) => new Activity
        {
            Details = options.ShowSceneName ? Clamp(ScenePrefix + (string.IsNullOrEmpty(editor.SceneName) ? UntitledScene : editor.SceneName)) : null,
            State = options.ShowProjectName ? Clamp(ProjectPrefix + editor.ProjectName) : null,
            Start = editor.Start,
            LargeImage = Artwork.UnityLogo(editor.UnityVersion),
            LargeText = Clamp("Unity " + editor.UnityVersion),
            SmallImage = Artwork.Badge(editor.Kind),
            SmallText = Artwork.BadgeText(editor.Kind),
        };

        public static string Clamp(string text)
        {
            if (text.Length <= MaxTextLength) return text;
            int cut = MaxTextLength - 1;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            return text.Substring(0, cut) + "…";
        }
    }

    internal static class Artwork
    {
        public const string UnityLogoKey = "unity";
        public const string Unity6LogoKey = "unity-6";
        public const string AvatarBadgeKey = "vrc-avatar";
        public const string WorldBadgeKey = "vrc-world";

        public static string UnityLogo(string unityVersion) =>
            unityVersion != null && unityVersion.StartsWith("6000.", StringComparison.Ordinal) ? Unity6LogoKey : UnityLogoKey;

        public static string Badge(ProjectKind kind) => kind switch
        {
            ProjectKind.VRChatWorld => WorldBadgeKey,
            ProjectKind.VRChatAvatar => AvatarBadgeKey,
            _ => null,
        };

        public static string BadgeText(ProjectKind kind) => kind switch
        {
            ProjectKind.VRChatWorld => "VRC World",
            ProjectKind.VRChatAvatar => "VRC Avatar",
            _ => null,
        };
    }
}
