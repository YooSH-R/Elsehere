using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class BaseEyeLightingTests
    {
        [TestCase("LittleMung_Business_Male")]
        [TestCase("LittleMung_MiddleAge_Male")]
        [TestCase("LittleMung_MiddleAge_Female")]
        [TestCase("LittleMung_BlackYoung_Male")]
        [TestCase("LittleMung_Grandmother")]
        public void EyePatchesReceiveTheSameShadowsAsTheFace(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Review/" + name + ".prefab"
            );
            var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
            var eyes = skins
                .Where(s =>
                    s != body && AssetDatabase.GetAssetPath(s.sharedMesh).Contains("/Face/Blink/")
                )
                .ToArray();
            Assert.That(eyes.Length, Is.EqualTo(10));
            bool receives =
                body.receiveShadows
                && !body.sharedMaterial.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF");
            Assert.That(receives, Is.True, "검수 얼굴은 실제 씬의 그림자를 받아야 합니다.");
            foreach (var skin in eyes)
            {
                Assert.That(skin.receiveShadows, Is.EqualTo(receives), skin.name);
                Assert.That(
                    skin.sharedMaterial.GetFloat("_ReceiveShadows"),
                    Is.EqualTo(receives ? 1 : 0),
                    skin.name
                );
                Assert.That(
                    skin.sharedMaterial.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"),
                    Is.EqualTo(!receives),
                    skin.name
                );
                Assert.That(skin.shadowCastingMode, Is.EqualTo(body.shadowCastingMode), skin.name);
            }
        }
    }
}
