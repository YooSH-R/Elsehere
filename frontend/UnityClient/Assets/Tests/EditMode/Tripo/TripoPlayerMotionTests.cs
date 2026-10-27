using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoPlayerMotionTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void SkinMustExistAndMatchItsBindPoseCount(bool removeSkin)
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Player/LittleMung/LittleMung.fbx"
                )
            );
            try
            {
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                if (removeSkin)
                    Object.DestroyImmediate(skin);
                else
                    skin.bones = new Transform[0];
                Assert.That(TripoModelReview.InspectRig(root).Passed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PersistentModelCannotBeAnimatedByTheReview()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Player/LittleMung/LittleMung.fbx"
            );
            Assert.Throws<System.ArgumentException>(() => new TripoMotionPreview(asset));
            Assert.That(TripoModelReview.InspectRig(null).Passed, Is.False);
        }

        [Test]
        public void ValidAvatarDoesNotHideSkinBonesOutsideItsSkeleton()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Player/LittleMung/LittleMung.fbx"
                )
            );
            var foreign = new GameObject("UnrelatedBone");
            try
            {
                Assert.That(TripoModelReview.InspectRig(root).Passed, Is.True);
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                var bones = skin.bones;
                bones[0] = foreign.transform;
                skin.bones = bones;
                Assert.That(root.GetComponentInChildren<Animator>().avatar.isValid, Is.True);
                var report = TripoModelReview.InspectRig(root);
                Assert.That(
                    report.Passed,
                    Is.False,
                    "Valid Avatar alone cannot approve disconnected skin."
                );
                Assert.That(string.Join(" ", report.Errors), Does.Contain("UnrelatedBone"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(foreign);
            }
        }

        [TestCase("LittleMung.fbx", TripoReviewMotion.Walk)]
        [TestCase("LittleMung.fbx", TripoReviewMotion.Run)]
        [TestCase("LittleMung.fbx", TripoReviewMotion.Rise)]
        [TestCase("LittleMung.fbx", TripoReviewMotion.Fall)]
        [TestCase("LittleMung.fbx", TripoReviewMotion.Land)]
        [TestCase("AdjustedRig/LittleMungAdjusted.fbx", TripoReviewMotion.Walk)]
        public void PlayerMotionDeformsTheGeneratedSkinAndRestoresTheCopy(
            string file,
            TripoReviewMotion motion
        )
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Player/LittleMung/" + file
            );
            var root = Object.Instantiate(asset);
            var baked = new Mesh();
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                animator.enabled = false; // Workbench preview starts with the Animator disabled.
                var avatar = animator.avatar;
                var controller = animator.runtimeAnimatorController;
                var transforms = root.GetComponentsInChildren<Transform>();
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                var sourceMesh = skin.sharedMesh;
                using (var preview = new TripoMotionPreview(root))
                {
                    preview.Apply(motion, 0.1f);
                    skin.BakeMesh(baked);
                    var before = baked.vertices;
                    preview.Apply(motion, 0.7f);
                    skin.BakeMesh(baked);
                    var after = baked.vertices;
                    float change = before.Select((v, i) => Vector3.Distance(v, after[i])).Max();
                    Assert.That(
                        change,
                        Is.GreaterThan(0.001f),
                        "The actual skinned mesh must move between clip frames."
                    );
                    Assert.That(
                        animator.avatar,
                        Is.SameAs(avatar),
                        "Retarget with this model's own Avatar."
                    );
                    preview.Apply(TripoReviewMotion.Knees, 0.5f);
                    preview.Apply(motion, 0.1f);
                    skin.BakeMesh(baked);
                    var returned = baked.vertices;
                    Assert.That(
                        before.Select((v, i) => Vector3.Distance(v, returned[i])).Max(),
                        Is.LessThan(0.0001f),
                        "Scrubbing back must be deterministic after changing motion."
                    );
                }
                Assert.That(animator.enabled, Is.False);
                Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
                Assert.That(skin.sharedMesh, Is.SameAs(sourceMesh));
                for (int i = 0; i < transforms.Length; i++)
                {
                    Assert.That(
                        Vector3.Distance(transforms[i].localPosition, positions[i]),
                        Is.LessThan(0.00001f)
                    );
                    Assert.That(
                        Quaternion.Angle(transforms[i].localRotation, rotations[i]),
                        Is.LessThan(0.05f)
                    );
                }
                Assert.That(EditorUtility.IsDirty(asset), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(baked);
            }
        }
    }
}
