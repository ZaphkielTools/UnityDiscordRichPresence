using UnityEditor;
using UnityEngine;

namespace Zaphkiel.DiscordRichPresence
{
    [FilePath(PresenceSettings.AssetPath, FilePathAttribute.Location.ProjectFolder)]
    internal sealed class PresenceSettings : ScriptableSingleton<PresenceSettings>
    {
        public const string AssetPath = "UserSettings/DiscordRichPresence.asset";

        [SerializeField] bool showOnDiscord = true;
        [SerializeField] bool showProjectName = true;
        [SerializeField] bool showSceneName = true;
        [SerializeField] bool restartTimerOnSceneChange;
        [SerializeField] bool verboseLogging;

        public bool ShowOnDiscord { get => showOnDiscord; set => Set(ref showOnDiscord, value); }
        public bool ShowProjectName { get => showProjectName; set => Set(ref showProjectName, value); }
        public bool ShowSceneName { get => showSceneName; set => Set(ref showSceneName, value); }
        public bool RestartTimerOnSceneChange { get => restartTimerOnSceneChange; set => Set(ref restartTimerOnSceneChange, value); }
        public bool VerboseLogging { get => verboseLogging; set => Set(ref verboseLogging, value); }

        public PresenceOptions Options => new PresenceOptions(showProjectName, showSceneName);

        void Set(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            Save(true);
        }
    }
}
