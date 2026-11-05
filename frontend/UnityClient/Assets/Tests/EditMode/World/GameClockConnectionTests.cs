using A305.EditorTools.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace A305.World.Tests
{
    public sealed class GameClockConnectionTests
    {
        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        [TestCase("PoliceStationScene")]
        public void ProductionSceneSendsGameClockBeats(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var driver = BuilderTools.FindSingleInScene<GameClockDriver>(scene);
            Assert.That(driver, Is.Not.Null, name);
            SerializedProperty apiSettings = new SerializedObject(driver).FindProperty(
                "apiSettings"
            );
            Assert.That(apiSettings?.objectReferenceValue, Is.Not.Null, name);
        }
    }
}
