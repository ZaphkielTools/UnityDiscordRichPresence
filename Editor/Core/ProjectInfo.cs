using System;
using System.IO;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Zaphkiel.DiscordRichPresence
{
    internal enum ProjectKind
    {
        Unity,
        VRChatAvatar,
        VRChatWorld,
    }

    internal static class ProjectInfo
    {
        public const string WorldsPackage = "com.vrchat.worlds";
        public const string AvatarsPackage = "com.vrchat.avatars";
        static ProjectKind? kind;

        public static ProjectKind Kind => kind ??= DetectKind(HasPackage);

        public static string Name =>
            string.IsNullOrWhiteSpace(Application.productName)
                ? Path.GetFileName(Path.GetDirectoryName(Application.dataPath))
                : Application.productName;

        public static ProjectKind DetectKind(Func<string, bool> hasPackage)
        {
            if (hasPackage(WorldsPackage)) return ProjectKind.VRChatWorld;
            if (hasPackage(AvatarsPackage)) return ProjectKind.VRChatAvatar;
            return ProjectKind.Unity;
        }

        static bool HasPackage(string name) => PackageInfo.FindForAssetPath("Packages/" + name) != null;
    }
}
