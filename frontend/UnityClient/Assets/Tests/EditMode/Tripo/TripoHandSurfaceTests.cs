using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoHandSurfaceTests
    {
        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void EquivalentHandSurfaceIsPreservedAcrossGeneratedBoneAxesAndThumbOrigins(
            bool left,
            bool moveThumbOrigin
        )
        {
            const string folder = "Assets/__TripoHandSurfaceTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var source = Object.Instantiate(master);
            GameObject output = null;
            Mesh changed = null;
            try
            {
                var animator = source.GetComponent<Animator>();
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var hand = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand
                );
                var original = TripoMasterHandBindingTests.WorldVertices(skin);
                var target = animator.GetBoneTransform(
                    moveThumbOrigin
                        ? (
                            left
                                ? HumanBodyBones.LeftThumbProximal
                                : HumanBodyBones.RightThumbProximal
                        )
                        : (
                            left
                                ? HumanBodyBones.LeftIndexIntermediate
                                : HumanBodyBones.RightIndexIntermediate
                        )
                );
                var children = target.Cast<Transform>().ToArray();
                var positions = children.Select(t => t.position).ToArray();
                var rotations = children.Select(t => t.rotation).ToArray();
                if (moveThumbOrigin)
                    target.position += new Vector3(.025f, .015f, 0);
                else
                    target.rotation *= Quaternion.Euler(0, 0, 65);
                for (int i = 0; i < children.Length; i++)
                    children[i].SetPositionAndRotation(positions[i], rotations[i]);
                changed = Object.Instantiate(skin.sharedMesh);
                changed.bindposes = skin
                    .bones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix)
                    .ToArray();
                skin.sharedMesh = changed;
                output = Object.Instantiate(TripoMasterRig.Build(source, master, folder));
                var actual = TripoMasterHandBindingTests.WorldVertices(
                    output.GetComponentInChildren<SkinnedMeshRenderer>()
                );
                var group = hand.GetComponentsInChildren<Transform>().ToHashSet();
                var weights = changed.boneWeights;
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
                Assert.That(indices.Length, Is.GreaterThan(10));
                Assert.That(
                    indices.Max(i => Vector3.Distance(original[i], actual[i])),
                    Is.LessThan(.002f),
                    "Equivalent hand geometry must not be stretched by a generated bone roll or thumb base convention."
                );
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (output != null)
                    Object.DestroyImmediate(output);
                if (changed != null)
                    Object.DestroyImmediate(changed);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
