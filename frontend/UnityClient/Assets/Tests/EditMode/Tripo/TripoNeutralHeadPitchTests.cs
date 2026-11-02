using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class TripoNeutralHeadPitchTests
    {
        [Test]
        public void DifferentHeadJointPositionDoesNotRotateTheNeutralHeadSurface()
        {
            const string folder = "Assets/__NeutralHeadPitchTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var source = Object.Instantiate(master);
            Mesh mesh = null;
            GameObject candidate = null;
            try
            {
                var animator = source.GetComponent<Animator>();
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                int headIndex = System.Array.IndexOf(skin.bones, head);
                var weights = skin.sharedMesh.boneWeights;
                int[] indices = Enumerable
                    .Range(0, weights.Length)
                    .Where(i =>
                    {
                        var w = weights[i];
                        return (w.boneIndex0 == headIndex ? w.weight0 : 0)
                                + (w.boneIndex1 == headIndex ? w.weight1 : 0)
                                + (w.boneIndex2 == headIndex ? w.weight2 : 0)
                                + (w.boneIndex3 == headIndex ? w.weight3 : 0)
                            > .999f;
                    })
                    .ToArray();
                Assert.That(indices.Length, Is.GreaterThan(10));
                var expected = TripoMasterHandBindingTests.WorldVertices(skin);
                mesh = Object.Instantiate(skin.sharedMesh);
                skin.sharedMesh = mesh;
                // Generated joint position differs, while the visible neutral mesh
                // stays unchanged. Hip-to-head lean must not become facial pitch.
                Vector3 lateral =
                    animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                    - animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                Vector3 forward = Vector3.Cross(lateral, Vector3.up).normalized;
                head.position += forward * .08f;
                mesh.vertices = expected.Select(skin.transform.InverseTransformPoint).ToArray();
                mesh.bindposes = skin
                    .bones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix)
                    .ToArray();
                candidate = Object.Instantiate(TripoMasterRig.Build(source, master, folder));
                var actual = TripoMasterHandBindingTests.WorldVertices(
                    candidate.GetComponentInChildren<SkinnedMeshRenderer>()
                );
                Vector3 expectedCenter =
                    indices.Aggregate(Vector3.zero, (sum, i) => sum + expected[i]) / indices.Length;
                Vector3 actualCenter =
                    indices.Aggregate(Vector3.zero, (sum, i) => sum + actual[i]) / indices.Length;
                float error = indices.Max(i =>
                    Vector3.Angle(expected[i] - expectedCenter, actual[i] - actualCenter)
                );
                Assert.That(
                    error,
                    Is.LessThan(.1f),
                    "Preserve neutral head orientation independently of the transfer frame."
                );
                Assert.That(TripoMasterHeadAudit.Inspect(source, candidate).Errors, Is.Empty);
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
