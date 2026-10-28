using A305.EditorTools.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace A305.World.Tests
{
    public sealed class LastSceneConnectionTests
    {
        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        public void ProductionSceneArrivalCanSaveLastScene(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var arrival = BuilderTools.FindSingleInScene<SpaceArrival>(scene);
            Assert.That(arrival, Is.Not.Null, name);
            SerializedProperty apiSettings = new SerializedObject(arrival).FindProperty(
                "apiSettings"
            );
            Assert.That(apiSettings?.objectReferenceValue, Is.Not.Null, name);
        }
    }
}
