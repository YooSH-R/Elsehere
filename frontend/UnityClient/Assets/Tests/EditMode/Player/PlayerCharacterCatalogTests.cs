using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Player.Tests
{
    public sealed class PlayerCharacterCatalogTests
    {
        [TestCase("F", "F")]
        [TestCase("M", "M")]
        [TestCase("unknown", "F")]
        [TestCase(null, "F")]
        public void ResolvesTheSelectedCharacterOrTheConfiguredDefault(
            string selected,
            string expected
        )
        {
            var female = ScriptableObject.CreateInstance<PlayerCharacterDefinition>();
            var male = ScriptableObject.CreateInstance<PlayerCharacterDefinition>();
            var catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
            try
            {
                SetId(female, "F");
                SetId(male, "M");
                var data = new SerializedObject(catalog);
                data.FindProperty("defaultCharacter").objectReferenceValue = female;
                SerializedProperty entries = data.FindProperty("characters");
                entries.arraySize = 2;
                entries.GetArrayElementAtIndex(0).objectReferenceValue = female;
                entries.GetArrayElementAtIndex(1).objectReferenceValue = male;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(catalog.Resolve(selected), Is.SameAs(expected == "F" ? female : male));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(female);
                Object.DestroyImmediate(male);
            }
        }

        private static void SetId(PlayerCharacterDefinition definition, string id)
        {
            var data = new SerializedObject(definition);
            data.FindProperty("characterId").stringValue = id;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
