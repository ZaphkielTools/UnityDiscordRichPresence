using System.Linq;
using NUnit.Framework;

namespace Zaphkiel.DiscordRichPresence.Tests
{
    sealed class ActivityBuilderTests
    {
        const long Start = 1795314441;
        static readonly PresenceOptions ShowAll = new PresenceOptions(true, true);

        static EditorSnapshot Editor(string scene = "MainScene", ProjectKind kind = ProjectKind.VRChatAvatar, string version = "6000.0.67f1") =>
            new EditorSnapshot("MyProject", scene, version, kind, Start);

        [Test]
        public void Build_ShowsSceneProjectAndStart()
        {
            var activity = ActivityBuilder.Build(Editor(), ShowAll);
            Assert.AreEqual("Scene : MainScene", activity.Details);
            Assert.AreEqual("Project : MyProject", activity.State);
            Assert.AreEqual(Start, activity.Start);
            Assert.AreEqual("Unity 6000.0.67f1", activity.LargeText);
        }

        [Test]
        public void HiddenNames_AreLeftOutOfTheJson()
        {
            string json = ActivityBuilder.Build(Editor(), new PresenceOptions(false, false)).ToJson();
            StringAssert.DoesNotContain("details", json);
            StringAssert.DoesNotContain("state", json);
            StringAssert.DoesNotContain("MyProject", json);
            StringAssert.DoesNotContain("MainScene", json);
        }

        [TestCase("")]
        [TestCase(null)]
        public void UnsavedScene_IsUntitled(string scene) =>
            Assert.AreEqual("Scene : Untitled", ActivityBuilder.Build(Editor(scene), ShowAll).Details);

        [TestCase("6000.0.67f1", "unity-6")]
        [TestCase("6000.3.2f1", "unity-6")]
        [TestCase("2022.3.22f1", "unity")]
        [TestCase("2019.4.31f1", "unity")]
        public void LargeImage_FollowsTheEditorVersion(string version, string key) =>
            Assert.AreEqual(key, ActivityBuilder.Build(Editor(version: version), ShowAll).LargeImage);

        [TestCase(ProjectKind.VRChatAvatar, "vrc-avatar", "VRC Avatar")]
        [TestCase(ProjectKind.VRChatWorld, "vrc-world", "VRC World")]
        public void SmallImage_ShowsTheVRChatProjectType(ProjectKind kind, string key, string text)
        {
            var activity = ActivityBuilder.Build(Editor(kind: kind), ShowAll);
            Assert.AreEqual(key, activity.SmallImage);
            Assert.AreEqual(text, activity.SmallText);
        }

        [Test]
        public void PlainUnityProject_HasNoSmallImage() =>
            StringAssert.DoesNotContain("small_", ActivityBuilder.Build(Editor(kind: ProjectKind.Unity), ShowAll).ToJson());

        [Test]
        public void ToJson_WritesDiscordFieldNames() =>
            Assert.AreEqual(
                "{\"details\":\"Scene : MainScene\",\"state\":\"Project : MyProject\",\"timestamps\":{\"start\":1795314441},\"assets\":{\"large_image\":\"unity-6\",\"large_text\":\"Unity 6000.0.67f1\",\"small_image\":\"vrc-avatar\",\"small_text\":\"VRC Avatar\"}}",
                ActivityBuilder.Build(Editor(), ShowAll).ToJson());

        [Test]
        public void Clamp_CutsLongTextWithAnEllipsis()
        {
            string clamped = ActivityBuilder.Clamp(new string('a', 300));
            Assert.AreEqual(ActivityBuilder.MaxTextLength, clamped.Length);
            Assert.IsTrue(clamped.EndsWith("…"));
            Assert.AreEqual("short", ActivityBuilder.Clamp("short"));
        }

        [Test]
        public void Clamp_KeepsSurrogatePairsWhole()
        {
            string text = new string('a', ActivityBuilder.MaxTextLength - 2) + string.Concat(Enumerable.Repeat("😀", 10));
            string clamped = ActivityBuilder.Clamp(text);
            Assert.IsFalse(char.IsHighSurrogate(clamped[clamped.Length - 2]));
            Assert.LessOrEqual(clamped.Length, ActivityBuilder.MaxTextLength);
        }
    }

    sealed class ProjectInfoTests
    {
        [Test]
        public void DetectKind_PrefersWorldsOverAvatars() =>
            Assert.AreEqual(ProjectKind.VRChatWorld, ProjectInfo.DetectKind(_ => true));

        [Test]
        public void DetectKind_FindsAvatarProjects() =>
            Assert.AreEqual(ProjectKind.VRChatAvatar, ProjectInfo.DetectKind(name => name == ProjectInfo.AvatarsPackage));

        [Test]
        public void DetectKind_DefaultsToUnity() =>
            Assert.AreEqual(ProjectKind.Unity, ProjectInfo.DetectKind(_ => false));
    }
}
