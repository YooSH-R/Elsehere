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
                var beforePoints = TripoMasterHandBindingTests.WorldVertices(skin);
                float expected = Ratio(reference);
                Assert.That(Mathf.Abs(Ratio(candidate) - expected), Is.GreaterThan(.03f));
                CharacterHeadProportions.Match(candidate, reference);
                Assert.That(
                    Ratio(candidate),
                    Is.EqualTo(expected).Within(.001f),
                    "Use Little Mung's head/body proportion, not a square face or total-size resize."
                );
                var afterPoints = TripoMasterHandBindingTests.WorldVertices(skin);
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
                    var vertices = TripoMasterHandBindingTests.WorldVertices(s);
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
