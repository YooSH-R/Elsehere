using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoMasterFootBindingTests
    {
        [Test]
        public void FootSurfaceAuditRejectsDistortedShoeWithCorrectMasterBones()
        {
            const string folder = "Assets/__TripoFootSurfaceTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            GameObject instance = null;
            Mesh changed = null;
            try
            {
                instance = Object.Instantiate(TripoMasterRig.Build(master, master, folder));
                Assert.That(TripoMasterFootAudit.Inspect(master, instance).Errors, Is.Empty);
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                changed = Object.Instantiate(skin.sharedMesh);
                skin.sharedMesh = changed;
                var foot = instance
                    .GetComponent<Animator>()
                    .GetBoneTransform(HumanBodyBones.LeftFoot)
                    .GetComponentsInChildren<Transform>()
                    .ToHashSet();
                var vertices = changed.vertices;
                var weights = changed.boneWeights;
                int changedCount = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    float weight =
                        (foot.Contains(skin.bones[w.boneIndex0]) ? w.weight0 : 0)
                        + (foot.Contains(skin.bones[w.boneIndex1]) ? w.weight1 : 0)
                        + (foot.Contains(skin.bones[w.boneIndex2]) ? w.weight2 : 0)
                        + (foot.Contains(skin.bones[w.boneIndex3]) ? w.weight3 : 0);
                    if (weight > .999f)
                    {
                        vertices[i] += Vector3.up * .06f;
                        changedCount++;
                    }
                }
                Assert.That(
                    changedCount,
                    Is.GreaterThan(20),
                    "The fixture must actually distort shoe vertices."
                );
                changed.vertices = vertices;
                Assert.That(TripoMasterRig.Inspect(instance, master).Errors, Is.Empty);
                Assert.That(
                    TripoMasterFootAudit.Inspect(master, instance).Errors,
                    Has.Some.Contains("발 형태")
                );
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
                if (changed != null)
                    Object.DestroyImmediate(changed);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes)]
        [TestCase(HumanBodyBones.RightFoot, HumanBodyBones.RightToes)]
        public void RebindingPreservesShoeShapeWhenSourceBoneAxesDiffer(
            HumanBodyBones footId,
            HumanBodyBones toesId
        )
        {
            const string folder = "Assets/__TripoFootBindingTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var source = Object.Instantiate(master);
            GameObject result = null;
            Mesh mesh = null;
            try
            {
                var animator = source.GetComponent<Animator>();
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var reference = TripoMasterHandBindingTests.WorldVertices(skin);
                var foot = animator.GetBoneTransform(footId);
                var group = foot.GetComponentsInChildren<Transform>().ToHashSet();
                var weights = skin.sharedMesh.boneWeights;
                var indices = Enumerable
                    .Range(0, weights.Length)
                    .Where(i =>
                    {
                        var w = weights[i];
                        return (group.Contains(skin.bones[w.boneIndex0]) ? w.weight0 : 0)
                                + (group.Contains(skin.bones[w.boneIndex1]) ? w.weight1 : 0)
                                + (group.Contains(skin.bones[w.boneIndex2]) ? w.weight2 : 0)
                                + (group.Contains(skin.bones[w.boneIndex3]) ? w.weight3 : 0)
                            > .999f;
                    })
                    .ToArray();
                Assert.That(indices.Length, Is.GreaterThan(20));
                foot.localRotation *= Quaternion.Euler(0, 55, 25);
                animator.GetBoneTransform(toesId).localRotation *= Quaternion.Euler(40, 0, 0);
                mesh = Object.Instantiate(skin.sharedMesh);
                mesh.vertices = reference.Select(skin.transform.InverseTransformPoint).ToArray();
                mesh.bindposes = skin
                    .bones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix)
                    .ToArray();
                skin.sharedMesh = mesh;
                result = Object.Instantiate(TripoMasterRig.Build(source, master, folder));
                var actual = TripoMasterHandBindingTests.WorldVertices(
                    result.GetComponentInChildren<SkinnedMeshRenderer>()
                );
                Assert.That(
                    indices.Max(i => Vector3.Distance(reference[i], actual[i])),
                    Is.LessThan(.0001f),
                    "A different generated foot/toe bone roll must not twist or shear the shoe surface."
                );
            }
            finally
            {
                if (result != null)
                    Object.DestroyImmediate(result);
                Object.DestroyImmediate(source);
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
