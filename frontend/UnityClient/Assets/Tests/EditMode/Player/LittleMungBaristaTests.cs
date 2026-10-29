using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Player.Tests
{
    public sealed class LittleMungBaristaTests
    {
        private const string ParentPath =
            "Assets/Prefabs/Player/LittleMung/LittleMungStairPlayer.prefab";
        private const string VariantPath =
            "Assets/Prefabs/Player/LittleMung/LittleMungBaristaPlayer.prefab";

        [Test]
        public void BaristaInheritsPlayerRigAndMovementInsteadOfDuplicatingThem()
        {
            var parent = AssetDatabase.LoadAssetAtPath<GameObject>(ParentPath);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            Assert.That(variant, Is.Not.Null, "A playable barista variant must exist.");
            Assert.That(
                PrefabUtility.GetPrefabAssetType(variant),
                Is.EqualTo(PrefabAssetType.Variant)
            );
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(variant), Is.SameAs(parent));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(variant);
            try
            {
                Assert.That(instance.GetComponentsInChildren<Animator>().Length, Is.EqualTo(1));
                var animator = instance.GetComponentInChildren<Animator>();
                var original = parent.GetComponentInChildren<Animator>();
                Assert.That(animator.avatar, Is.SameAs(original.avatar));
                Assert.That(
                    animator.runtimeAnimatorController,
                    Is.SameAs(original.runtimeAnimatorController)
                );
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>())
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(
                        component,
                        ParentPath
                    );
                    Assert.That(
                        source,
                        Is.Not.Null,
                        component.GetType().Name + " must be inherited."
                    );
                }
                var originalSkin = parent.GetComponentInChildren<SkinnedMeshRenderer>();
                var baseSkin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(
                    baseSkin.sharedMesh,
                    Is.Not.SameAs(originalSkin.sharedMesh),
                    "The reference design replaces hair and clothing faces on a separate appearance mesh."
                );
                Assert.That(baseSkin.sharedMaterial, Is.Not.SameAs(originalSkin.sharedMaterial));
                Assert.That(
                    instance.GetComponentsInChildren<SkinnedMeshRenderer>().Length,
                    Is.GreaterThan(1)
                );
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    foreach (var bone in skin.bones)
                    {
                        Assert.That(bone, Is.Not.Null);
                        Assert.That(
                            PrefabUtility.GetCorrespondingObjectFromSourceAtPath(bone, ParentPath),
                            Is.Not.Null,
                            skin.name + " must bind to the inherited skeleton."
                        );
                    }
                }
                foreach (var change in PrefabUtility.GetPropertyModifications(instance))
                {
                    // Unity records root placement defaults for every instance; these are not rig overrides.
                    if (PrefabUtility.IsDefaultOverride(change))
                        continue;
                    if (
                        change.target is Animator
                        || change.target is MonoBehaviour
                        || change.target is Transform
                    )
                        Assert.Fail(
                            "Behaviour or skeleton override blocks inheritance: "
                                + change.propertyPath
                        );
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void AppearanceBindPosesMatchTheSharedParentRig()
        {
            var parent = AssetDatabase.LoadAssetAtPath<GameObject>(ParentPath);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            var original = parent.GetComponentInChildren<SkinnedMeshRenderer>();
            var parentRoot = parent.GetComponentInChildren<Animator>().transform;
            var variantRoot = variant.GetComponentInChildren<Animator>().transform;
            var expected = new Dictionary<string, Matrix4x4>();
            for (int i = 0; i < original.bones.Length; i++)
                expected[original.bones[i].name] =
                    parentRoot.worldToLocalMatrix
                    * original.transform.localToWorldMatrix
                    * original.sharedMesh.bindposes[i].inverse;
            foreach (var skin in variant.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = skin.sharedMesh;
                Assert.That(mesh.bindposes.Length, Is.EqualTo(skin.bones.Length));
                foreach (var weight in mesh.boneWeights)
                    Assert.That(
                        weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3,
                        Is.EqualTo(1f).Within(.0001f),
                        skin.name
                    );
                for (int i = 0; i < skin.bones.Length; i++)
                {
                    var actual =
                        variantRoot.worldToLocalMatrix
                        * skin.transform.localToWorldMatrix
                        * mesh.bindposes[i].inverse;
                    Assert.That(expected.ContainsKey(skin.bones[i].name), Is.True);
                    for (int element = 0; element < 16; element++)
                        Assert.That(
                            actual[element],
                            Is.EqualTo(expected[skin.bones[i].name][element]).Within(.0002f),
                            skin.name
                                + "/"
                                + skin.bones[i].name
                                + ": re-export/rebind after rest rig changes."
                        );
                }
            }
        }
    }
}
