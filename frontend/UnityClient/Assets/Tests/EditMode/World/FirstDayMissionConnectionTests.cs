using System.Linq;
using A305.EditorTools.Common;
using A305.Mission;
using A305.Npc;
using A305.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace A305.World.Tests
{
    public sealed class FirstDayMissionConnectionTests
    {
        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        public void ProductionSceneHasOneConnectedFirstDayMission(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var controller = BuilderTools.FindSingleInScene<FirstDayMissionController>(scene);
            Assert.That(controller, Is.Not.Null);
            var fields = new SerializedObject(controller);
            foreach (string field in new[] { "view", "apiSettings", "dialogue", "player" })
                Assert.That(
                    fields.FindProperty(field).objectReferenceValue,
                    Is.Not.Null,
                    name + ": " + field
                );
            Assert.That(
                fields.FindProperty("sceneCode").stringValue,
                Is.EqualTo(name.Replace("Scene", "").ToUpperInvariant())
            );
            if (name == "TownScene")
                Assert.That(fields.FindProperty("rain").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void ShopHasStaffAndReturnDoorAndIsEnabledForBuild()
        {
            const string path = "Assets/Scenes/Production/ShopScene.unity";
            var scene = EditorSceneManager.OpenScene(path);
            Assert.That(
                BuilderTools
                    .FindAllInScene<NpcIdentity>(scene)
                    .Count(npc => npc.Code == "SHOP_STAFF"),
                Is.EqualTo(1)
            );
            var exit = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(door => door.DoorwayId == "TownDoor");
            Assert.That(exit.DestinationScene, Is.EqualTo("TownScene"));
            Assert.That(exit.DestinationDoorwayId, Is.EqualTo("ShopDoor"));
            Assert.That(
                EditorBuildSettings.scenes.Any(item => item.enabled && item.path == path),
                Is.True
            );
        }
    }
}
