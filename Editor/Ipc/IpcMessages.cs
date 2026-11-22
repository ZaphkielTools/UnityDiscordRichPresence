using System;

namespace Zaphkiel.DiscordRichPresence
{
    [Serializable]
    internal sealed class IpcMessage
    {
        public string cmd;
        public string evt;
        public string nonce;
        public IpcMessageData data;
    }

    [Serializable]
    internal sealed class IpcMessageData
    {
        public int code;
        public string message;
        public IpcUser user;
    }

    [Serializable]
    internal sealed class IpcUser
    {
        public string username;
        public string global_name;

        public string DisplayName => string.IsNullOrEmpty(global_name) ? username : global_name;
    }

    internal static class IpcCommands
    {
        public const int ProtocolVersion = 1;

        public static string Handshake(string applicationId) =>
            new JsonWriter().BeginObject().Property("v", ProtocolVersion).Property("client_id", applicationId).EndObject().ToString();

        public static string SetActivity(int processId, string activityJson, string nonce)
        {
            var json = new JsonWriter().BeginObject().Property("cmd", "SET_ACTIVITY").BeginObject("args").Property("pid", processId);
            if (activityJson != null) json.RawProperty("activity", activityJson);
            return json.EndObject().Property("nonce", nonce).EndObject().ToString();
        }
    }
}
