using System.Linq;
using A305.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public sealed class PasserbyOutfitTests
    {
        private const string Folder = "Assets/Prefabs/Characters/Mannequin/Npc/";
        private const string BasePath =
            "Assets/Prefabs/Characters/Mannequin/LittleMung_Base_Male_170.prefab";

        [Test]
        public void OutfitInheritsBaseFaceAndUsesOnlyItsSkeleton()
        {
            var parent = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            var child = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Passerby_Male.prefab"
            );
            Assert.That(child, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(child), Is.SameAs(parent));
            var sourceSkins = parent.GetComponentsInChildren<SkinnedMeshRenderer>();
            var skins = child.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var source in sourceSkins)
                Assert.That(
                    skins.Any(s => s.sharedMesh == source.sharedMesh),
                    Is.True,
                    source.name
                );
            var animator = child.GetComponentInChildren<Animator>();
            Assert.That(
                animator.avatar,
                Is.SameAs(parent.GetComponentInChildren<Animator>().avatar)
            );
            Assert.That(child.GetComponentInChildren<CharacterFaceController>(), Is.Not.Null);
            foreach (string name in new[] { "Suit_Navy_Upper" })
            {
                var skin = skins.Single(s => s.name == name);
                Assert.That(skin.sharedMesh.vertexCount, Is.GreaterThan(100));
                Assert.That(
                    skin.bones.All(b => b != null && b.IsChildOf(child.transform)),
                    Is.True
                );
                Assert.That(
                    skin.sharedMesh.boneWeights.Length,
                    Is.EqualTo(skin.sharedMesh.vertexCount)
                );
                foreach (var weight in skin.sharedMesh.boneWeights)
                    Assert.That(
                        weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3,
                        Is.EqualTo(1).Within(.0001f)
                    );
            }
        }

        [Test]
        public void JacketDoesNotFollowLegsFingersOrHead()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Passerby_Male.prefab"
            );
            var skin = prefab
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(r => r.name == "Suit_Navy_Upper");
            var allowed = new[]
            {
                "Hips",
                "Spine",
                "Chest",
                "UpperChest",
                "Neck",
                "Neck_Twist_A",
                "Left_Shoulder",
                "Right_Shoulder",
                "Left_UpperArm",
                "Right_UpperArm",
                "Left_LowerArm",
                "Right_LowerArm",
            };
            foreach (var row in skin.sharedMesh.boneWeights)
            {
                var indices = new[]
                {
                    row.boneIndex0,
                    row.boneIndex1,
                    row.boneIndex2,
                    row.boneIndex3,
                };
                var weights = new[] { row.weight0, row.weight1, row.weight2, row.weight3 };
                for (int k = 0; k < 4; k++)
                    if (weights[k] > .00001f)
                        Assert.That(
                            allowed,
                            Does.Contain(skin.bones[indices[k]].name),
                            "Unrelated bone pulls jacket vertices"
                        );
            }
        }

        [Test]
        public void PasserbyVariantRetainsBehaviorAndConnectsItsAnimator()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Passerby_Male.prefab"
            );
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                PrefabUtility.GetCorrespondingObjectFromSource(prefab),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(BasePath))
            );
            Assert.That(
                prefab.GetComponent<CharacterController>().height,
                Is.EqualTo(1.7f).Within(.001)
            );
            Assert.That(
                prefab
                    .GetComponents<MonoBehaviour>()
                    .Any(c => c.GetType().FullName == "A305.Npc.Passerby"),
                Is.True
            );
            var driver = new SerializedObject(prefab.GetComponent<CharacterAnimationDriver>());
            Assert.That(
                driver.FindProperty("animator").objectReferenceValue,
                Is.SameAs(prefab.GetComponentInChildren<Animator>())
            );
            Assert.That(prefab.GetComponentInChildren<CharacterFaceController>(), Is.Not.Null);
            foreach (string name in new[] { "HairSlot", "UpperSlot", "PantsSlot", "ShoesSlot" })
                Assert.That(
                    prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == name),
                    Is.EqualTo(1),
                    name
                );
        }
    }
}
