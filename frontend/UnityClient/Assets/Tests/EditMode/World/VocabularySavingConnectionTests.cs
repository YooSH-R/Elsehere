using A305.EditorTools.Common;
using A305.Learning;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    public sealed class VocabularySavingConnectionTests
    {
        private const string VocabularyPrefabPath =
            "Assets/Prefabs/Learning/VocabularyScreen.prefab";

        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        public void ProductionSceneLearningCanReachVocabularyServer(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var learning = BuilderTools.FindSingleInScene<LearningController>(scene);
            Assert.That(learning, Is.Not.Null, name);
            SerializedProperty apiSettings = new SerializedObject(learning).FindProperty(
                "apiSettings"
            );
            Assert.That(apiSettings?.objectReferenceValue, Is.Not.Null, name);
        }

        [TestCase("memoInput")]
        [TestCase("memoSaveButton")]
        [TestCase("statusText")]
        public void VocabularyScreenHasMemoAndStatusControls(string field)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VocabularyPrefabPath);
            var view = prefab.GetComponent<VocabularyView>();
            SerializedProperty property = new SerializedObject(view).FindProperty(field);
            Assert.That(property?.objectReferenceValue, Is.Not.Null, field);
        }
    }
}
