using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class CafeFemaleFaceAssetTests
    {
        [Test]
        public void PreviewKeepsOriginalBodyAndRigWhileAddingRealFaceDeformations()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Barista.prefab"
            );
            var preview = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/CafeFemaleFacePreview.prefab"
            );
            Assert.That(preview, Is.Not.Null, "The six-pose copy must exist.");
            Assert.That(preview.GetComponent<CharacterFaceController>(), Is.Not.Null);
            var original = source.GetComponentInChildren<SkinnedMeshRenderer>();
            var copy = preview.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(copy.sharedMesh, Is.Not.SameAs(original.sharedMesh));
            Assert.That(copy.sharedMesh.vertices, Is.EqualTo(original.sharedMesh.vertices));
            Assert.That(copy.sharedMesh.boneWeights, Is.EqualTo(original.sharedMesh.boneWeights));
            Assert.That(copy.sharedMesh.bindposes, Is.EqualTo(original.sharedMesh.bindposes));
            Assert.That(
                preview.GetComponentInChildren<Animator>().avatar,
                Is.SameAs(source.GetComponentInChildren<Animator>().avatar)
            );
            Assert.That(original.sharedMesh.blendShapeCount, Is.Zero);
            foreach (
                string name in new[]
                {
                    "mouthSmileLeft",
                    "mouthSmileRight",
                    "eyeBlinkLeft",
                    "eyeBlinkRight",
                    "viseme_AA",
                    "viseme_FF",
                    "viseme_TH",
                }
            )
            {
                int index = copy.sharedMesh.GetBlendShapeIndex(name);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), name);
                var offsets = new Vector3[copy.sharedMesh.vertexCount];
                copy.sharedMesh.GetBlendShapeFrameVertices(index, 0, offsets, null, null);
                float maximum = 0;
                for (int i = 0; i < offsets.Length; i++)
                    maximum = Mathf.Max(maximum, offsets[i].magnitude);
                Assert.That(maximum, Is.GreaterThan(.0001f), name + " must deform vertices.");
            }
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/Scenes/Sandbox/CafeFemaleFacePreview.unity"
                ),
                Is.Not.Null
            );
        }
    }
}
