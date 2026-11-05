using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class TripoHandTransferTests
    {
        [TestCase("BodySurface.asset")]
        [TestCase("Face/FaceSurface.asset")]
        [TestCase("Face/Blink/FaceWithBaseEyes.asset")]
        public void ReviewedPoliceHandUsesMasterSurfaceAndClosedWrists(string relativePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Review/LittleMung_Police_Male.prefab"
            );
            var skin = prefab
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var report = TripoHandTemplateAudit.Inspect(
                AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath),
                skin,
                AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/Art/Characters/ReviewCorrections/ServiceNpcs/LittleMung_Police_Male/"
                        + relativePath
                )
            );
            Assert.That(report.Errors, Is.Empty);
        }

        [Test]
        public void PolicePalmSurfaceRetainsItsShapeWhenFingerJointSpacingDiffers()
        {
            const string folder = "Assets/__PoliceHandShapeTest";
            var source = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Characters/Tripo/NPC/LittleMung_Police_Male/LittleMung_Police_Male.fbx"
                )
            );
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            GameObject candidate = null;
            try
            {
                candidate = Object.Instantiate(TripoMasterRig.Build(source, master, folder));
                var input = source.GetComponentInChildren<Animator>();
                var output = candidate.GetComponentInChildren<Animator>();
                float expectedScale =
                    Vector3.Distance(
                        output.GetBoneTransform(HumanBodyBones.Hips).position,
                        output.GetBoneTransform(HumanBodyBones.Head).position
                    )
                    / Vector3.Distance(
                        input.GetBoneTransform(HumanBodyBones.Hips).position,
                        input.GetBoneTransform(HumanBodyBones.Head).position
                    );
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var before = SkinTestGeometry.WorldVertices(skin);
                var after = SkinTestGeometry.WorldVertices(
                    candidate.GetComponentInChildren<SkinnedMeshRenderer>()
                );
                var weights = skin.sharedMesh.boneWeights;
                foreach (var id in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                {
                    var hand = input.GetBoneTransform(id);
                    int[] selected = Enumerable
                        .Range(0, weights.Length)
                        .Where(i =>
                        {
                            var w = weights[i];
                            return (skin.bones[w.boneIndex0].IsChildOf(hand) ? w.weight0 : 0)
                                    + (skin.bones[w.boneIndex1].IsChildOf(hand) ? w.weight1 : 0)
                                    + (skin.bones[w.boneIndex2].IsChildOf(hand) ? w.weight2 : 0)
                                    + (skin.bones[w.boneIndex3].IsChildOf(hand) ? w.weight3 : 0)
                                > .9999f;
                        })
                        .ToArray();
                    Assert.That(selected.Length, Is.GreaterThan(30));
                    float maximumError = 0;
                    foreach (int first in selected)
                    foreach (int second in selected)
                        maximumError = Mathf.Max(
                            maximumError,
                            Mathf.Abs(
                                Vector3.Distance(after[first], after[second])
                                    - Vector3.Distance(before[first], before[second])
                                        * expectedScale
                            )
                        );
                    Assert.That(
                        maximumError,
                        Is.LessThan(.0001f),
                        id + ": master finger spacing must not crush or fold the palm."
                    );
                    var targetHand = output.GetBoneTransform(id);
                    var alignment = targetHand.rotation * Quaternion.Inverse(hand.rotation);
                    float positionError = selected.Max(i =>
                        Vector3.Distance(
                            after[i],
                            targetHand.position
                                + alignment * (before[i] - hand.position) * expectedScale
                        )
                    );
                    Assert.That(
                        positionError,
                        Is.LessThan(.0001f),
                        id + ": preserve the normal source palm orientation relative to its wrist."
                    );
                }
            }
            finally
            {
                if (candidate != null)
                    Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(180f, .5f)]
        public void DifferentPalmOrientationPreservesWristThickness(
            float twist,
            float alongForearm = 1f
        )
        {
            const string folder = "Assets/__HandTransferTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var source = Object.Instantiate(master);
            var mesh = new Mesh();
            GameObject candidate = null;
            try
            {
                var animator = source.GetComponentInChildren<Animator>();
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var wrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                Vector3 axis = (wrist.position - forearm.position).normalized;
                Vector3 across = Vector3.Cross(axis, Vector3.up).normalized * .01f;
                Vector3 depth = Vector3.Cross(axis, across);
                Vector3 center = Vector3.Lerp(forearm.position, wrist.position, alongForearm);
                var expected = new[]
                {
                    center + across,
                    center + depth,
                    center - across,
                    center - depth,
                };
                // 표면은 그대로 두고 생성기가 추정한 손 본 방향만 다르게 만든다.
                // 반씩 섞인 손목 가중치에서 반대 방향 회전이 두께를 없애면 안 된다.
                wrist.rotation = Quaternion.AngleAxis(twist, axis) * wrist.rotation;
                mesh.vertices = expected.Select(skin.transform.InverseTransformPoint).ToArray();
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.normals = Enumerable
                    .Repeat(skin.transform.InverseTransformDirection(axis), 4)
                    .ToArray();
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.boneWeights = Enumerable
                    .Repeat(
                        new BoneWeight
                        {
                            boneIndex0 = System.Array.IndexOf(skin.bones, forearm),
                            weight0 = alongForearm < 1f ? 1f : .5f,
                            boneIndex1 = System.Array.IndexOf(skin.bones, wrist),
                            weight1 = alongForearm < 1f ? 0f : .5f,
                        },
                        4
                    )
                    .ToArray();
                mesh.bindposes = skin
                    .bones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix)
                    .ToArray();
                skin.sharedMesh = mesh;
                candidate = Object.Instantiate(TripoMasterRig.Build(source, master, folder));
                var actual = SkinTestGeometry.WorldVertices(
                    candidate.GetComponentInChildren<SkinnedMeshRenderer>()
                );
                for (int first = 0; first < expected.Length; first++)
                for (int second = first + 1; second < expected.Length; second++)
                    Assert.That(
                        Vector3.Distance(actual[first], actual[second]),
                        Is.EqualTo(Vector3.Distance(expected[first], expected[second]))
                            .Within(.00001f),
                        $"Palm twist {twist}: wrist cross section {first}-{second} must retain its thickness."
                    );
                Assert.That(TripoMasterRig.Inspect(candidate, master).Errors, Is.Empty);
                if (alongForearm < 1f)
                    Assert.That(
                        Vector3.Angle(expected[0] - expected[2], actual[0] - actual[2]),
                        Is.EqualTo(90f).Within(.1f),
                        "The forearm must share the twist instead of concentrating it at the wrist seam."
                    );
            }
            finally
            {
                if (candidate != null)
                    Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(mesh);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
