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

        [Test]
        public void MaleSharesTheFemaleMasterRigAndAnimation()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(
                "Assets/Data/Player/Characters/Catalog.asset"
            );
            PlayerCharacterDefinition female = catalog.Resolve("F");
            PlayerCharacterDefinition male = catalog.Resolve("M");
            var femaleAnimator = female.VisualPrefab.GetComponentInChildren<Animator>();
            var maleAnimator = male.VisualPrefab.GetComponentInChildren<Animator>();

            Assert.That(male.CharacterId, Is.EqualTo("M"));
            Assert.That(maleAnimator, Is.Not.Null, "M must use the animated male visual.");
            Assert.That(maleAnimator.avatar, Is.SameAs(femaleAnimator.avatar));
            Assert.That(
                maleAnimator.runtimeAnimatorController,
                Is.SameAs(femaleAnimator.runtimeAnimatorController)
            );
        }

        [Test]
        public void EveryCharacterMovesWithTheSharedPlayerMovement()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/Player.prefab"
            );
            var littleMungMovement = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/Data/Player/LittleMung/Movement.asset"
            );
            var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(
                "Assets/Data/Player/Characters/Catalog.asset"
            );
            var mover = new SerializedObject(player.GetComponent<PlayerMover>());

            Assert.That(
                mover.FindProperty("movementSettings").objectReferenceValue,
                Is.SameAs(littleMungMovement)
            );
            foreach (string id in new[] { "F", "M" })
            {
                var definition = new SerializedObject(catalog.Resolve(id));
                Assert.That(
                    definition.FindProperty("movementSettings"),
                    Is.Null,
                    $"{id} must not replace the shared Player movement."
                );
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
