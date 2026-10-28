using A305.EditorTools.Common;
using A305.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace A305.World.Tests
{
    public sealed class TutorialSavingConnectionTests
    {
        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        public void ProductionSceneTutorialCanSaveFinish(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var tutorial = BuilderTools.FindSingleInScene<TutorialDirector>(scene);
            Assert.That(tutorial, Is.Not.Null, name);
            SerializedProperty apiSettings = new SerializedObject(tutorial).FindProperty(
                "apiSettings"
            );
            Assert.That(apiSettings?.objectReferenceValue, Is.Not.Null, name);
        }
    }
}
