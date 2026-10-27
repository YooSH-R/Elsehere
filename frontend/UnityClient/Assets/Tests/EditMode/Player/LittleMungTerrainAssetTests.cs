using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Player.Tests
{
    public sealed class LittleMungTerrainAssetTests
    {
        [Test]
        public void TerrainSkinPreservesGeometryAndOnlyCopiesWeights()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx"
            );
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/LittleMung/LittleMungTerrainPlayer.prefab"
            );
            var originals = source.GetComponentsInChildren<SkinnedMeshRenderer>();
            var copies = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(copies.Length, Is.EqualTo(originals.Length));
            for (int index = 0; index < originals.Length; index++)
            {
                var before = originals[index].sharedMesh;
                var after = copies[index].sharedMesh;
                Assert.That(after, Is.Not.SameAs(before));
                Assert.That(after.vertices, Is.EqualTo(before.vertices));
                Assert.That(after.triangles, Is.EqualTo(before.triangles));
                Assert.That(after.uv, Is.EqualTo(before.uv));
                Assert.That(after.normals, Is.EqualTo(before.normals));
                Assert.That(after.bindposes, Is.EqualTo(before.bindposes));
                Assert.That(after.blendShapeCount, Is.EqualTo(before.blendShapeCount));
                var originalWeights = before.boneWeights;
                var weights = after.boneWeights;
                Assert.That(
                    weights.Where((w, i) => !w.Equals(originalWeights[i])).Count(),
                    Is.GreaterThan(0)
                );
                foreach (var w in weights)
                {
                    Assert.That(
                        w.weight0 + w.weight1 + w.weight2 + w.weight3,
                        Is.EqualTo(1).Within(.0001f)
                    );
                    Assert.That(
                        Mathf.Min(w.weight0, w.weight1, w.weight2, w.weight3),
                        Is.GreaterThanOrEqualTo(0)
                    );
                }
            }
        }
    }
}
