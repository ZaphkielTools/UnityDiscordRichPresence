using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zaphkiel.DiscordRichPresence
{
    [InitializeOnLoad]
    internal static class PresenceService
    {
        public const string ApplicationId = "956944656032219146";
        const double PollInterval = 0.25;
        const string KeyPrefix = "Zaphkiel.DiscordRichPresence.";
        const string HandleKey = KeyPrefix + "PipeHandle";
        const string ServerKey = KeyPrefix + "PipeServer";
        const string UserKey = KeyPrefix + "User";
        const string SentKey = KeyPrefix + "Sent";

        static readonly PresenceSync Sync;
        static double nextPoll;

        static PresenceService()
        {
            if (!IsSupported || Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess()) return;
            using (var process = Process.GetCurrentProcess())
                Sync = new PresenceSync(new DiscordIpcClient(ApplicationId, process.Id, WindowsPipeTransport.OpenDiscord));
            Resume();
            EditorApplication.update += Update;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Suspend;
            EditorApplication.quitting += Shutdown;
        }

        public static bool IsSupported => Application.platform == RuntimePlatform.WindowsEditor;
        public static bool IsRunning => Sync != null;
        public static ConnectionState State => Sync?.Client.State ?? ConnectionState.Disconnected;
        public static string UserName => Sync?.Client.UserName;
        public static string LastError => Sync?.Client.LastError;
        public static bool WasRejected => Sync?.Client.WasRejected ?? false;

        public static Activity BuildActivity()
        {
            var settings = PresenceSettings.instance;
            long start = settings.RestartTimerOnSceneChange ? SessionClock.SceneStart : SessionClock.EditorStart;
            var editor = new EditorSnapshot(ProjectInfo.Name, SceneManager.GetActiveScene().name, Application.unityVersion, ProjectInfo.Kind, start);
            return ActivityBuilder.Build(editor, settings.Options);
        }

        static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll) return;
            nextPoll = now + PollInterval;
            var settings = PresenceSettings.instance;
            Log.Verbose = settings.VerboseLogging;
            Sync.Enabled = settings.ShowOnDiscord;
            Sync.Publish(BuildActivity().ToJson());
            Sync.Tick(now);
        }

        static void OnActiveSceneChanged(Scene previous, Scene current) => SessionClock.MarkSceneStart();

        static void Suspend()
        {
            Sync.Client.Poll();
            if (Sync.Client.State == ConnectionState.Ready && Sync.Client.Transport is WindowsPipeTransport pipe && pipe.TryDetach(out long handle))
            {
                SessionState.SetString(HandleKey, handle.ToString(CultureInfo.InvariantCulture));
                SessionState.SetInt(ServerKey, unchecked((int)pipe.ServerProcessId));
                SessionState.SetString(UserKey, Sync.Client.UserName ?? "");
                SessionState.SetString(SentKey, Sync.Sent ?? "");
                Sync.Client.Release();
                return;
            }
            Sync.Dispose();
        }

        static void Resume()
        {
            string handle = SessionState.GetString(HandleKey, "");
            SessionState.EraseString(HandleKey);
            if (!long.TryParse(handle, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)) return;
            var transport = WindowsPipeTransport.Attach(value, unchecked((uint)SessionState.GetInt(ServerKey, 0)));
            if (transport == null) return;
            Sync.Client.Resume(transport, SessionState.GetString(UserKey, ""));
            Sync.Resume(SessionState.GetString(SentKey, ""));
            Log.Info("Kept the Discord connection across the domain reload.");
        }

        static void Shutdown() => Sync.Dispose();
    }
}
