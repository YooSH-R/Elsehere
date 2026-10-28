using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoMasterHandBindingTests
    {
        [TestCase(HumanBodyBones.LeftHand)]
        [TestCase(HumanBodyBones.RightHand)]
        public void RebindingReturnsPosedSourceHandToMasterRestFrame(HumanBodyBones handId)
        {
            const string folder = "Assets/__TripoHandBindingTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var source = Object.Instantiate(master);
            GameObject candidate = null;
            try
            {
                var animator = source.GetComponent<Animator>();
                var hand = animator.GetBoneTransform(handId);
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var bones = hand.GetComponentsInChildren<Transform>().ToHashSet();
                var weights = skin.sharedMesh.boneWeights;
                var indices = Enumerable
                    .Range(0, weights.Length)
                    .Where(i =>
                    {
                        var w = weights[i];
                        return (bones.Contains(skin.bones[w.boneIndex0]) ? w.weight0 : 0)
                                + (bones.Contains(skin.bones[w.boneIndex1]) ? w.weight1 : 0)
                                + (bones.Contains(skin.bones[w.boneIndex2]) ? w.weight2 : 0)
                                + (bones.Contains(skin.bones[w.boneIndex3]) ? w.weight3 : 0)
                            > .999f;
                    })
                    .ToArray();
                Assert.That(indices.Length, Is.GreaterThan(10));
                var reference = WorldVertices(skin);
                hand.localRotation *= Quaternion.Euler(0, 0, 45);
                hand.position += new Vector3(.025f, 0, .015f);
                candidate = Object.Instantiate(TripoMasterRig.Build(source, master, folder));
                var actual = WorldVertices(candidate.GetComponentInChildren<SkinnedMeshRenderer>());
                float error = indices.Max(i => Vector3.Distance(reference[i], actual[i]));
                Assert.That(
                    error,
                    Is.LessThan(.002f),
                    "Hand surface must follow the master wrist and finger rest frames, not stay at the generated rig's old joints."
                );
            }
            finally
            {
                if (candidate != null)
                    Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RebindingMasterToItselfPreservesTheWholeSurface()
        {
            const string folder = "Assets/__TripoIdentityBindingTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            GameObject reference = Object.Instantiate(master);
            GameObject candidate = null;
            try
            {
                candidate = Object.Instantiate(TripoMasterRig.Build(master, master, folder));
                var expected = WorldVertices(
                    reference.GetComponentInChildren<SkinnedMeshRenderer>()
                );
                var actual = WorldVertices(candidate.GetComponentInChildren<SkinnedMeshRenderer>());
                Assert.That(actual.Length, Is.EqualTo(expected.Length));
                Assert.That(
                    expected.Select((v, i) => Vector3.Distance(v, actual[i])).Max(),
                    Is.LessThan(.00001f)
                );
            }
            finally
            {
                if (candidate != null)
                    Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(reference);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static Vector3[] WorldVertices(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh;
            var matrices = skin
                .bones.Select((b, i) => b.localToWorldMatrix * mesh.bindposes[i])
                .ToArray();
            var weights = mesh.boneWeights;
            return mesh
                .vertices.Select(
                    (v, i) =>
                    {
                        var w = weights[i];
                        return matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                            + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                            + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    }
                )
                .ToArray();
        }
    }
}
