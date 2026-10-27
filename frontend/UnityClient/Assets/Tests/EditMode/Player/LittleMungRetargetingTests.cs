using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    public sealed class LittleMungRetargetingTests
    {
        [Test]
        public void SourceMotionsShareTheActualRestAvatar()
        {
            const string folder = "Assets/Art/Player/LittleMung/Motions/";
            var avatar = AssetDatabase
                .LoadAllAssetsAtPath(folder + "Reference.fbx")
                .OfType<Avatar>()
                .Single();
            Assert.That(avatar.isValid && avatar.isHuman, Is.True);
            foreach (string name in new[] { "Walk", "Run", "Jump" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(folder + name + ".fbx");
                Assert.That(
                    importer.avatarSetup,
                    Is.EqualTo(ModelImporterAvatarSetup.CopyFromOther)
                );
                Assert.That(importer.sourceAvatar, Is.EqualTo(avatar));
            }
            var model = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Reference.fbx")
            );
            try
            {
                var animator = model.GetComponent<Animator>();
                float left = KneeAngle(
                    animator,
                    HumanBodyBones.LeftUpperLeg,
                    HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot
                );
                float right = KneeAngle(
                    animator,
                    HumanBodyBones.RightUpperLeg,
                    HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot
                );
                Assert.That(left, Is.LessThan(10f));
                Assert.That(right, Is.LessThan(10f));
                Assert.That(Mathf.Abs(left - right), Is.LessThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [TestCase("Walk")]
        [TestCase("Run")]
        public void PlayerClipRetainsImportedMusclesWithoutAnkleOverrides(string name)
        {
            var source = AssetDatabase
                .LoadAllAssetsAtPath("Assets/Art/Player/LittleMung/Motions/" + name + ".fbx")
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__"));
            var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Art/Player/LittleMung/" + name + ".anim"
            );
            var bindings = AnimationUtility.GetCurveBindings(source);
            Assert.That(bindings.Length, Is.GreaterThan(0));
            Assert.That(
                AnimationUtility.GetCurveBindings(copy).Length,
                Is.EqualTo(bindings.Length)
            );
            foreach (var binding in bindings)
            {
                var expected = AnimationUtility.GetEditorCurve(source, binding);
                var actual = AnimationUtility.GetEditorCurve(copy, binding);
                Assert.That(actual, Is.Not.Null, binding.propertyName);
                Assert.That(actual.length, Is.EqualTo(expected.length), binding.propertyName);
                for (int i = 0; i <= 8; i++)
                    Assert.That(
                        actual.Evaluate(source.length * i / 8f),
                        Is.EqualTo(expected.Evaluate(source.length * i / 8f)).Within(0.00001f),
                        binding.propertyName
                    );
            }
        }

        private static float KneeAngle(
            Animator animator,
            HumanBodyBones upper,
            HumanBodyBones lower,
            HumanBodyBones foot
        )
        {
            Vector3 knee = animator.GetBoneTransform(lower).position;
            return 180f
                - Vector3.Angle(
                    animator.GetBoneTransform(upper).position - knee,
                    animator.GetBoneTransform(foot).position - knee
                );
        }

        [TestCase("Assets/Art/Player/LittleMung/LittleMung.fbx")]
        [TestCase("Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx")]
        public void WalkKeepsShoeHeadingAlignedWithBody(string modelPath)
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            var graph = PlayableGraph.Create("Foot heading regression");
            try
            {
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Vector3 bindRight = (
                    animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                    - animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position
                ).normalized;
                Vector3 bindForward = Vector3.Cross(bindRight, Vector3.up).normalized;
                var feet = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
                var localForwards = feet.Select(id =>
                        animator.GetBoneTransform(id).InverseTransformDirection(bindForward)
                    )
                    .ToArray();
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Art/Player/LittleMung/Walk.anim"
                );
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph, "Walk", animator).SetSourcePlayable(playable);
                graph.Play();
                float maximum = 0;
                Quaternion initialLeg = animator
                    .GetBoneTransform(HumanBodyBones.LeftUpperLeg)
                    .localRotation;
                float legMotion = 0;
                for (int frame = 0; frame < 60; frame++)
                {
                    playable.SetTime(clip.length * frame / 60.0);
                    graph.Evaluate(0);
                    legMotion = Mathf.Max(
                        legMotion,
                        Quaternion.Angle(
                            initialLeg,
                            animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation
                        )
                    );
                    Vector3 right = Vector3
                        .ProjectOnPlane(
                            animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                                - animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position,
                            Vector3.up
                        )
                        .normalized;
                    Vector3 forward = Vector3.Cross(right, Vector3.up);
                    for (int side = 0; side < feet.Length; side++)
                    {
                        Vector3 shoe = animator
                            .GetBoneTransform(feet[side])
                            .TransformDirection(localForwards[side]);
                        float yaw = Mathf.Abs(
                            Mathf.Atan2(Vector3.Dot(shoe, right), Vector3.Dot(shoe, forward))
                                * Mathf.Rad2Deg
                        );
                        maximum = Mathf.Max(maximum, yaw);
                    }
                }
                Debug.Log($"Walking maximum shoe heading error: {maximum:F1} degrees");
                Assert.That(
                    legMotion,
                    Is.GreaterThan(5f),
                    "The test must evaluate a moving leg, not an unanimated bind pose."
                );
                Assert.That(
                    maximum,
                    Is.LessThan(30f),
                    "Normal walking must not turn either shoe sideways relative to the body."
                );
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(model);
            }
        }
    }
}
