using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Zaphkiel.DiscordRichPresence
{
    internal static class PreferencesPage
    {
        public const string SettingsPath = "Preferences/Discord Rich Presence";
        const string AssemblyName = "Zaphkiel.Editor.DiscordRichPresence";
        const string StyleSheetPath = "UI/DiscordRichPresence.uss";
        const long RefreshMilliseconds = 500;

        [SettingsProvider]
        static SettingsProvider Create() => new SettingsProvider(SettingsPath, SettingsScope.User)
        {
            label = "Discord Rich Presence",
            keywords = new HashSet<string> { "Discord", "Rich Presence", "Activity", "Scene", "Project" },
            activateHandler = (_, root) => Build(root),
        };

        static void Build(VisualElement root)
        {
            var settings = PresenceSettings.instance;
            var page = Styled(new VisualElement(), "drp");
            var sheet = LoadStyleSheet();
            if (sheet) page.styleSheets.Add(sheet);
            root.Add(page);

            page.Add(Styled(new Label("Discord Rich Presence"), "drp__title"));

            var status = Styled(new VisualElement(), "drp__status");
            var statusIcon = Styled(new Image(), "drp__status-icon");
            var statusText = Styled(new Label(), "drp__status-text");
            status.Add(statusIcon);
            status.Add(statusText);
            page.Add(status);

            page.Add(CreateToggle("Show on Discord", "Off hides this project from Discord. Other projects keep their own setting.", settings.ShowOnDiscord, v => settings.ShowOnDiscord = v));
            var details = new VisualElement();
            details.Add(CreateToggle("Show project name", "Sends the product name from Player Settings.", settings.ShowProjectName, v => settings.ShowProjectName = v));
            details.Add(CreateToggle("Show scene name", "Sends the name of the active scene.", settings.ShowSceneName, v => settings.ShowSceneName = v));
            details.Add(CreateToggle("Restart timer on scene change", "Counts from the last scene change instead of the editor launch.", settings.RestartTimerOnSceneChange, v => settings.RestartTimerOnSceneChange = v));
            details.Add(CreateToggle("Verbose logging", "Logs connections and disconnections to the Console.", settings.VerboseLogging, v => settings.VerboseLogging = v));
            page.Add(details);

            page.Add(Styled(new Label("Sent to Discord"), "drp__header"));
            var preview = Styled(new VisualElement(), "drp__preview");
            page.Add(preview);

            page.Add(Styled(new Label($"These settings apply to this project only and are saved in {PresenceSettings.AssetPath}."), "drp__note"));

            void Refresh()
            {
                var (text, icon) = DescribeStatus();
                statusText.text = text;
                statusIcon.image = EditorGUIUtility.FindTexture(icon);
                details.SetEnabled(settings.ShowOnDiscord);
                FillPreview(preview);
            }

            Refresh();
            page.schedule.Execute(Refresh).Every(RefreshMilliseconds);
        }

        static (string text, string icon) DescribeStatus()
        {
            const string Info = "console.infoicon.sml";
            const string Warn = "console.warnicon.sml";
            if (!PresenceService.IsSupported) return ("Windows only: this platform has no Discord pipe support yet.", Warn);
            if (!PresenceService.IsRunning) return ("Not running in this editor process.", Info);
            if (!PresenceSettings.instance.ShowOnDiscord) return ("Off for this project.", Info);
            switch (PresenceService.State)
            {
                case ConnectionState.Ready:
                    string user = PresenceService.UserName;
                    return (string.IsNullOrEmpty(user) ? "Connected to Discord." : $"Connected to Discord as {user}.", "TestPassed");
                case ConnectionState.Handshaking:
                    return ("Connecting to Discord…", Info);
                default:
                    return PresenceService.WasRejected
                        ? ($"Discord refused the connection. {PresenceService.LastError}", "console.erroricon.sml")
                        : ("Waiting for the Discord desktop app.", Warn);
            }
        }

        static void FillPreview(VisualElement preview)
        {
            preview.Clear();
            if (!PresenceSettings.instance.ShowOnDiscord)
            {
                preview.Add(Styled(new Label("Nothing: the activity is cleared."), "drp__row-value"));
                return;
            }
            var activity = PresenceService.BuildActivity();
            AddRow(preview, "Details", activity.Details ?? "Hidden");
            AddRow(preview, "State", activity.State ?? "Hidden");
            AddRow(preview, "Timer", "Since " + DateTimeOffset.FromUnixTimeSeconds(activity.Start).ToLocalTime().ToString("g"));
            AddRow(preview, "Large image", $"{activity.LargeImage} ({activity.LargeText})");
            AddRow(preview, "Small image", activity.SmallImage == null ? "None" : $"{activity.SmallImage} ({activity.SmallText})");
        }

        static void AddRow(VisualElement parent, string name, string value)
        {
            var row = Styled(new VisualElement(), "drp__row");
            row.Add(Styled(new Label(name), "drp__row-name"));
            row.Add(Styled(new Label(value), "drp__row-value"));
            parent.Add(row);
        }

        static Toggle CreateToggle(string label, string tooltip, bool value, Action<bool> setter)
        {
            var toggle = new Toggle(label) { value = value, tooltip = tooltip };
            toggle.RegisterValueChangedCallback(e => setter(e.newValue));
            return toggle;
        }

        static T Styled<T>(T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }

        static StyleSheet LoadStyleSheet()
        {
            string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(AssemblyName);
            if (string.IsNullOrEmpty(asmdef)) return null;
            string folder = Path.GetDirectoryName(asmdef)?.Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<StyleSheet>($"{folder}/{StyleSheetPath}");
        }
    }
}
