using System.Linq;
using A305.EditorTools.Characters;
using A305.EditorTools.Player;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class PlayerHeadProportionTests
    {
        [Test]
        public void CatalogUsesTheMatchedCopyAndKeepsTheOriginalBodyAndRig()
        {
            var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                "Assets/Data/Player/Characters/M.asset"
            );
            Assert.That(
                AssetDatabase.GetAssetPath(definition.VisualPrefab),
                Is.EqualTo(PlayerHeadProportionBuilder.PrefabPath)
            );
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerHeadProportionBuilder.SourcePath
            );
            var copy = Object.Instantiate(definition.VisualPrefab);
            var female = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Player/Characters/Female.prefab"
                )
            );
            try
            {
                var rig = copy.GetComponentInChildren<Animator>();
                var sourceRig = source.GetComponentInChildren<Animator>();
                Assert.That(rig.avatar, Is.SameAs(sourceRig.avatar));
                Assert.That(
                    rig.runtimeAnimatorController,
                    Is.SameAs(sourceRig.runtimeAnimatorController)
                );
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Art/Player/LittleMung/Idle.anim"
                );
                idle.SampleAnimation(rig.gameObject, 0);
                idle.SampleAnimation(female.GetComponentInChildren<Animator>().gameObject, 0);
                Assert.That(
                    Vector3.Distance(
                        CharacterHeadProportions.HeadBounds(copy).size,
                        CharacterHeadProportions.HeadBounds(female).size
                    ),
                    Is.LessThan(.005f)
                );
                Assert.That(female.transform.localScale, Is.EqualTo(Vector3.one * 1.6f));
                var sourceSkins = source.GetComponentsInChildren<SkinnedMeshRenderer>();
                var copies = copy.GetComponentsInChildren<SkinnedMeshRenderer>();
                Transform head = sourceRig.GetBoneTransform(HumanBodyBones.Head);
                for (int s = 0; s < sourceSkins.Length; s++)
                {
                    var original = sourceSkins[s].sharedMesh;
                    var result = copies[s].sharedMesh;
                    Assert.That(result, Is.Not.SameAs(original));
                    Assert.That(result.boneWeights, Is.EqualTo(original.boneWeights));
                    Assert.That(result.bindposes, Is.EqualTo(original.bindposes));
                    Assert.That(result.triangles, Is.EqualTo(original.triangles));
                    var before = original.vertices;
                    var after = result.vertices;
                    for (int i = 0; i < before.Length; i++)
                    {
                        BoneWeight w = original.boneWeights[i];
                        int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                        float[] amounts = { w.weight0, w.weight1, w.weight2, w.weight3 };
                        if (
                            !Enumerable
                                .Range(0, 4)
                                .Any(k =>
                                    amounts[k] > 0
                                    && (
                                        sourceSkins[s].bones[ids[k]] == head
                                        || sourceSkins[s].bones[ids[k]].IsChildOf(head)
                                    )
                                )
                        )
                            Assert.That(after[i], Is.EqualTo(before[i]));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(female);
            }
        }

        [Test]
        public void MaleHeadMatchesFemaleDimensionsWhileBodyRigAndSourcesRemainUnchanged()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/Characters/Male.prefab"
            );
            var reference = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Player/Characters/Female.prefab"
                )
            );
            var copy = Object.Instantiate(source);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Art/Player/LittleMung/Idle.anim"
            );
            idle.SampleAnimation(copy.GetComponentInChildren<Animator>().gameObject, 0);
            idle.SampleAnimation(reference.GetComponentInChildren<Animator>().gameObject, 0);
            var skins = copy.GetComponentsInChildren<SkinnedMeshRenderer>();
            var originals = skins.Select(s => s.sharedMesh).ToArray();
            var points = originals.Select(m => m.vertices).ToArray();
            var rig = copy.GetComponentInChildren<Animator>();
            var bones = rig.GetComponentsInChildren<Transform>();
            var matrices = bones.Select(b => b.localToWorldMatrix).ToArray();
            var head = rig.GetBoneTransform(HumanBodyBones.Head);
            try
            {
                CharacterHeadProportions.MatchDimensions(copy, reference);
                Bounds expected = CharacterHeadProportions.HeadBounds(reference);
                Bounds actual = CharacterHeadProportions.HeadBounds(copy);
                for (int axis = 0; axis < 3; axis++)
                    Assert.That(
                        actual.size[axis],
                        Is.EqualTo(expected.size[axis]).Within(.005f),
                        "머리 외곽 치수는 여성 기준에 맞아야 합니다."
                    );
                Assert.That(copy.transform.localScale, Is.EqualTo(Vector3.one * 1.6f));
                Assert.That(reference.transform.localScale, Is.EqualTo(Vector3.one * 1.6f));
                Assert.That(bones.Select(b => b.localToWorldMatrix), Is.EqualTo(matrices));
                for (int s = 0; s < skins.Length; s++)
                {
                    Mesh result = skins[s].sharedMesh;
                    Assert.That(result, Is.Not.SameAs(originals[s]));
                    Assert.That(
                        originals[s].vertices,
                        Is.EqualTo(points[s]),
                        "원본 메시를 수정하면 안 됩니다."
                    );
                    Assert.That(result.triangles, Is.EqualTo(originals[s].triangles));
                    Assert.That(result.uv, Is.EqualTo(originals[s].uv));
                    Assert.That(result.boneWeights, Is.EqualTo(originals[s].boneWeights));
                    Assert.That(result.bindposes, Is.EqualTo(originals[s].bindposes));
                    var after = result.vertices;
                    for (int i = 0; i < after.Length; i++)
                    {
                        BoneWeight w = result.boneWeights[i];
                        int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                        float[] amounts = { w.weight0, w.weight1, w.weight2, w.weight3 };
                        bool belongsToHead = Enumerable
                            .Range(0, 4)
                            .Any(k =>
                                amounts[k] > 0
                                && (
                                    skins[s].bones[ids[k]] == head
                                    || skins[s].bones[ids[k]].IsChildOf(head)
                                )
                            );
                        if (!belongsToHead)
                            Assert.That(
                                after[i],
                                Is.EqualTo(points[s][i]),
                                "머리 가중치가 없는 몸통·팔·발 정점을 보존해야 합니다."
                            );
                    }
                }
                var fitted = skins.Select(s => s.sharedMesh).ToArray();
                Assert.That(
                    CharacterHeadProportions.MatchDimensions(copy, reference),
                    Is.EqualTo(Vector3.one)
                );
                Assert.That(
                    skins.Select(s => s.sharedMesh),
                    Is.EqualTo(fitted),
                    "다시 실행해도 머리가 더 커지면 안 됩니다."
                );
            }
            finally
            {
                foreach (var skin in skins)
                    if (!EditorUtility.IsPersistent(skin.sharedMesh))
                        Object.DestroyImmediate(skin.sharedMesh);
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(reference);
            }
        }
    }
}
