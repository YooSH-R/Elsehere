using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoTorsoWeightTests
    {
        [Test]
        public void GeneratedJacketFrontDoesNotFollowArmAndSourceWeightsStayUntouched()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Tripo/LittleMung_Casual_Male_41b6e942_Review/Source.fbx"
            );
            var animator = source.GetComponentInChildren<Animator>();
            var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = skin.sharedMesh.boneWeights;
            var repaired = TripoTorsoWeights.Prepare(skin, animator);
            foreach (int index in new[] { 2980, 5534 })
            {
                Assert.That(
                    ArmWeight(original[index], skin.bones),
                    Is.GreaterThan(.12f),
                    "Fixture must reproduce the generated jacket leak."
                );
                Assert.That(
                    ArmWeight(repaired[index], skin.bones),
                    Is.LessThan(.01f),
                    "Central jacket must move with the torso, not the arms."
                );
            }
            Assert.That(skin.sharedMesh.boneWeights, Is.EqualTo(original));
            for (int i = 0; i < original.Length; i++)
            {
                var w = repaired[i];
                Assert.That(
                    w.weight0 + w.weight1 + w.weight2 + w.weight3,
                    Is.EqualTo(1).Within(.0001f)
                );
                if (
                    skin.bones[original[i].boneIndex0].name.Contains("Foot")
                    || skin.bones[original[i].boneIndex0].name.Contains("Hand")
                )
                    Assert.That(repaired[i], Is.EqualTo(original[i]));
            }
        }

        private static float ArmWeight(BoneWeight w, Transform[] bones)
        {
            float total = 0;
            int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
            for (int i = 0; i < 4; i++)
                if (bones[ids[i]].name.Contains("Arm") || bones[ids[i]].name.Contains("Shoulder"))
                    total += values[i];
            return total;
        }
    }
}
