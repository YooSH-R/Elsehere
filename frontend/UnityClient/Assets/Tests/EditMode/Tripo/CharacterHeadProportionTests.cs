using System.Linq;
using A305.EditorTools.Characters;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class CharacterHeadProportionTests
    {
        [Test]
        public void NarrowHeadMatchesReferenceWidthWithoutChangingHeightBodyOrRig()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                TripoMasterWorkflow.VisualTemplate
            );
            var reference = Object.Instantiate(source);
            var candidate = Object.Instantiate(source);
            var owned = new System.Collections.Generic.List<Mesh>();
            try
            {
                var rig = candidate.GetComponentInChildren<Animator>();
                var head = rig.GetBoneTransform(HumanBodyBones.Head);
                var skins = candidate.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skin in skins)
                {
                    var mesh = Object.Instantiate(skin.sharedMesh);
                    owned.Add(mesh);
                    skin.sharedMesh = mesh;
                    var points = mesh.vertices;
                    for (int i = 0; i < points.Length; i++)
                    {
                        var matrix = RestMatrix(skin, mesh.boneWeights[i]);
                        var world = matrix.MultiplyPoint3x4(points[i]);
                        world.x = Mathf.Lerp(
                            world.x,
                            head.position.x,
                            .3f * HeadWeight(skin, head, mesh.boneWeights[i])
                        );
                        points[i] = matrix.inverse.MultiplyPoint3x4(world);
                    }
                    mesh.vertices = points;
                }
                var beforeBones = rig.GetComponentsInChildren<Transform>()
                    .Select(t => t.localToWorldMatrix)
                    .ToArray();
                var beforePoints = skins.Select(SkinTestGeometry.WorldVertices).ToArray();
                float height = CharacterHeadProportions.Ratio(candidate);
                float expected = CharacterHeadProportions.HeadBounds(reference).size.x;
                Assert.That(
                    CharacterHeadProportions.HeadBounds(candidate).size.x,
                    Is.LessThan(expected * .8f)
                );
                CharacterHeadProportions.MatchWidth(candidate, reference);
                Assert.That(
                    CharacterHeadProportions.HeadBounds(candidate).size.x,
                    Is.EqualTo(expected).Within(.001f),
                    "머리 높이만 같고 얼굴 폭이 좁은 사본도 맞춰야 합니다."
                );
                Assert.That(
                    CharacterHeadProportions.Ratio(candidate),
                    Is.EqualTo(height).Within(.00001f)
                );
                for (int s = 0; s < skins.Length; s++)
                {
                    var after = SkinTestGeometry.WorldVertices(skins[s]);
                    for (int i = 0; i < after.Length; i++)
                        if (HeadWeight(skins[s], head, skins[s].sharedMesh.boneWeights[i]) == 0)
                            Assert.That(
                                Vector3.Distance(beforePoints[s][i], after[i]),
                                Is.LessThan(.00001f)
                            );
                    Assert.That(skins[s].sharedMesh.boneWeights, Is.EqualTo(owned[s].boneWeights));
                    Assert.That(skins[s].sharedMesh.bindposes, Is.EqualTo(owned[s].bindposes));
                }
                Assert.That(
                    rig.GetComponentsInChildren<Transform>().Select(t => t.localToWorldMatrix),
                    Is.EqualTo(beforeBones)
                );
                Assert.That(
                    CharacterHeadProportions.MatchWidth(candidate, reference),
                    Is.EqualTo(1).Within(.0001f)
                );
            }
            finally
            {
                foreach (var skin in candidate.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (
                        !EditorUtility.IsPersistent(skin.sharedMesh)
                        && !owned.Contains(skin.sharedMesh)
                    )
                        Object.DestroyImmediate(skin.sharedMesh);
                foreach (var mesh in owned)
                    Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(reference);
            }
        }

        private static Matrix4x4 RestMatrix(SkinnedMeshRenderer skin, BoneWeight w)
        {
            int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] weights = { w.weight0, w.weight1, w.weight2, w.weight3 };
            var result = Matrix4x4.zero;
            for (int k = 0; k < 4; k++)
            {
                var matrix =
                    skin.bones[ids[k]].localToWorldMatrix * skin.sharedMesh.bindposes[ids[k]];
                for (int i = 0; i < 16; i++)
                    result[i] += matrix[i] * weights[k];
            }
            return result;
        }

        [Test]
        public void SmallHeadCopyMatchesLittleMungWithoutChangingBodyOrBones()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var reference = Object.Instantiate(original);
            var candidate = Object.Instantiate(original);
            Mesh small = null;
            try
            {
                var rig = candidate.GetComponentInChildren<Animator>();
                var skin = candidate.GetComponentInChildren<SkinnedMeshRenderer>();
                var head = rig.GetBoneTransform(HumanBodyBones.Head);
                var source = skin.sharedMesh;
                small = Object.Instantiate(source);
                skin.sharedMesh = small;
                var vertices = small.vertices;
                var weights = small.boneWeights;
                for (int i = 0; i < vertices.Length; i++)
                    if (HeadWeight(skin, head, weights[i]) > .8f)
                        vertices[i] = Vector3.Lerp(
                            skin.transform.InverseTransformPoint(head.position),
                            vertices[i],
                            .7f
                        );
                small.vertices = vertices;
                var beforeBones = rig.GetComponentsInChildren<Transform>()
                    .Select(t => t.localToWorldMatrix)
                    .ToArray();
                var beforePoints = SkinTestGeometry.WorldVertices(skin);
                float expected = Ratio(reference);
                Assert.That(Mathf.Abs(Ratio(candidate) - expected), Is.GreaterThan(.03f));
                CharacterHeadProportions.Match(candidate, reference);
                Assert.That(
                    Ratio(candidate),
                    Is.EqualTo(expected).Within(.001f),
                    "Use Little Mung's head/body proportion, not a square face or total-size resize."
                );
                var afterPoints = SkinTestGeometry.WorldVertices(skin);
                for (int i = 0; i < weights.Length; i++)
                    if (HeadWeight(skin, head, weights[i]) == 0)
                        Assert.That(
                            Vector3.Distance(beforePoints[i], afterPoints[i]),
                            Is.LessThan(.00001f)
                        );
                Assert.That(
                    rig.GetComponentsInChildren<Transform>().Select(t => t.localToWorldMatrix),
                    Is.EqualTo(beforeBones)
                );
                Assert.That(skin.sharedMesh.boneWeights, Is.EqualTo(source.boneWeights));
                Assert.That(skin.sharedMesh, Is.Not.SameAs(source));
            }
            finally
            {
                foreach (var s in candidate.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (!EditorUtility.IsPersistent(s.sharedMesh) && s.sharedMesh != small)
                        Object.DestroyImmediate(s.sharedMesh);
                if (small != null)
                    Object.DestroyImmediate(small);
                Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(reference);
            }
        }

        private static float HeadWeight(SkinnedMeshRenderer skin, Transform head, BoneWeight w)
        {
            int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] amounts = { w.weight0, w.weight1, w.weight2, w.weight3 };
            return Enumerable
                .Range(0, 4)
                .Where(i => skin.bones[ids[i]] == head || skin.bones[ids[i]].IsChildOf(head))
                .Sum(i => amounts[i]);
        }

        private static float Ratio(GameObject model)
        {
            var rig = model.GetComponentInChildren<Animator>();
            var head = rig.GetBoneTransform(HumanBodyBones.Head);
            var points = model
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .SelectMany(s =>
                {
                    var vertices = SkinTestGeometry.WorldVertices(s);
                    return vertices.Select(
                        (v, i) =>
                            (point: v, head: HeadWeight(s, head, s.sharedMesh.boneWeights[i]) > .8f)
                    );
                })
                .ToArray();
            var heads = points.Where(p => p.head).Select(p => p.point.y).ToArray();
            return (heads.Max() - heads.Min())
                / (points.Max(p => p.point.y) - points.Min(p => p.point.y));
        }
    }
}
