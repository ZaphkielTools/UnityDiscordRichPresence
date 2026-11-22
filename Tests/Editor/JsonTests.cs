using System;
using NUnit.Framework;
using UnityEngine;

namespace Zaphkiel.DiscordRichPresence.Tests
{
    sealed class JsonWriterTests
    {
        [Serializable]
        sealed class Holder
        {
            public string v = null;
        }

        static string Write(string value) => new JsonWriter().BeginObject().Property("v", value).EndObject().ToString();

        [Test]
        public void Members_AreSeparatedAndNested() =>
            Assert.AreEqual(
                "{\"a\":1,\"b\":{\"c\":\"d\"},\"e\":\"f\"}",
                new JsonWriter().BeginObject().Property("a", 1).BeginObject("b").Property("c", "d").EndObject().Property("e", "f").EndObject().ToString());

        [Test]
        public void OptionalProperty_SkipsNullAndEmpty() =>
            Assert.AreEqual(
                "{\"b\":\"x\"}",
                new JsonWriter().BeginObject().OptionalProperty("a", null).OptionalProperty("b", "x").OptionalProperty("c", "").EndObject().ToString());

        [Test]
        public void RawProperty_IsInsertedAsIs() =>
            Assert.AreEqual("{\"a\":{\"b\":2}}", new JsonWriter().BeginObject().RawProperty("a", "{\"b\":2}").EndObject().ToString());

        [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [TestCase("C:\\Scenes", "\"C:\\\\Scenes\"")]
        [TestCase("a\nb\tc\r", "\"a\\nb\\tc\\r\"")]
        [TestCase("\u0001", "\"\\u0001\"")]
        [TestCase("Scène ✦ 世界", "\"Scène ✦ 世界\"")]
        public void Strings_AreEscaped(string value, string expected) =>
            Assert.AreEqual("{\"v\":" + expected + "}", Write(value));

        [TestCase("Scène \"Alpha\" \\ 1\n2\t\u0002 😀")]
        public void Output_ReadsBackWithJsonUtility(string value) =>
            Assert.AreEqual(value, JsonUtility.FromJson<Holder>(Write(value)).v);
    }

    sealed class IpcCommandsTests
    {
        [Test]
        public void Handshake_HasVersionAndClientId() =>
            Assert.AreEqual("{\"v\":1,\"client_id\":\"123\"}", IpcCommands.Handshake("123"));

        [Test]
        public void SetActivity_WrapsTheActivityWithThePid() =>
            Assert.AreEqual(
                "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":7,\"activity\":{\"state\":\"x\"}},\"nonce\":\"3\"}",
                IpcCommands.SetActivity(7, "{\"state\":\"x\"}", "3"));

        [Test]
        public void SetActivity_WithoutActivity_ClearsIt() =>
            Assert.AreEqual("{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":7},\"nonce\":\"1\"}", IpcCommands.SetActivity(7, null, "1"));
    }
}
