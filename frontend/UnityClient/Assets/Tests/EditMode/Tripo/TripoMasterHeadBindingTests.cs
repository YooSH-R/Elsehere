using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoMasterHeadBindingTests
    {
        [Test]
        public void HeadAuditRejectsTiltedSurfaceEvenWithMatchingMasterBones()
        {
            const string root = "Assets/Art/Characters/Tripo/LittleMung_HotelStaff_male_336ca328";
            const string folder = "Assets/__TripoHeadAuditTest";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Source.fbx");
            var tilted = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "/MasterRig/Visual.prefab"
            );
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            try
            {
                Assert.That(TripoMasterRig.Inspect(tilted, master).Passed, Is.True);
                Assert.That(
                    TripoMasterHeadAudit.Inspect(source, tilted).Passed,
                    Is.False,
                    "Correct master bones do not imply correct face orientation."
                );
                var fixedModel = TripoMasterRig.Build(source, master, folder);
                Assert.That(TripoMasterHeadAudit.Inspect(source, fixedModel).Errors, Is.Empty);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void DifferentGeneratedHeadRollMustNotTiltFaceSurface()
        {
            const string folder = "Assets/__TripoHeadBindingTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var source = Object.Instantiate(master);
            GameObject candidate = null;
            Mesh mesh = null;
            try
            {
                var animator = source.GetComponent<Animator>();
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                int headIndex = System.Array.IndexOf(skin.bones, head);
                var group = animator
                    .GetBoneTransform(HumanBodyBones.Neck)
                    .GetComponentsInChildren<Transform>()
                    .ToHashSet();
                mesh = Object.Instantiate(skin.sharedMesh);
                skin.sharedMesh = mesh;
                var weights = mesh.boneWeights;
                var indices = Enumerable
                    .Range(0, weights.Length)
                    .Where(i =>
                    {
                        var w = weights[i];
                        return (group.Contains(skin.bones[w.boneIndex0]) ? w.weight0 : 0)
                                + (group.Contains(skin.bones[w.boneIndex1]) ? w.weight1 : 0)
                                + (group.Contains(skin.bones[w.boneIndex2]) ? w.weight2 : 0)
                                + (group.Contains(skin.bones[w.boneIndex3]) ? w.weight3 : 0)
                            > .99f;
                    })
                    .ToArray();
                Assert.That(indices.Length, Is.GreaterThan(50));
                foreach (int i in indices)
                    weights[i] = new BoneWeight { boneIndex0 = headIndex, weight0 = 1 };
                mesh.boneWeights = weights;
                var expected = TripoMasterHandBindingTests.WorldVertices(skin);
                head.localRotation *= Quaternion.Euler(0, 0, 20);
                mesh.vertices = expected.Select(skin.transform.InverseTransformPoint).ToArray();
                mesh.bindposes = skin
                    .bones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix)
                    .ToArray();
                candidate = Object.Instantiate(TripoMasterRig.Build(source, master, folder));
                var actual = TripoMasterHandBindingTests.WorldVertices(
                    candidate.GetComponentInChildren<SkinnedMeshRenderer>()
                );
                Assert.That(
                    indices.Max(i => Vector3.Distance(expected[i], actual[i])),
                    Is.LessThan(.0001f),
                    "Generated head bone roll must not rotate the neutral face when connecting the master."
                );
            }
            finally
            {
                if (candidate != null)
                    Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(source);
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
