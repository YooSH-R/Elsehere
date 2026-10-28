using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoMotionPreviewTests
    {
        [TestCase(TripoReviewMotion.Arms, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand)]
        [TestCase(TripoReviewMotion.Elbows, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand)]
        [TestCase(TripoReviewMotion.Knees, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot)]
        public void Apply_OnHumanoidInstance_MovesBonesAndDisposeRestoresPose(
            TripoReviewMotion motion,
            HumanBodyBones joint,
            HumanBodyBones tip
        )
        {
            var root = new GameObject("MotionFixture");
            var mapped = new System.Collections.Generic.Dictionary<HumanBodyBones, Transform>();
            Transform Add(HumanBodyBones bone, Transform parent, Vector3 position)
            {
                var item = new GameObject(bone.ToString()).transform;
                item.SetParent(parent, false);
                item.localPosition = position;
                mapped.Add(bone, item);
                return item;
            }
            var hips = Add(HumanBodyBones.Hips, root.transform, new Vector3(0, 1, 0));
            var spine = Add(HumanBodyBones.Spine, hips, new Vector3(0, 0.2f, 0));
            var chest = Add(HumanBodyBones.Chest, spine, new Vector3(0, 0.2f, 0));
            var neck = Add(HumanBodyBones.Neck, chest, new Vector3(0, 0.2f, 0));
            Add(HumanBodyBones.Head, neck, new Vector3(0, 0.15f, 0));
            foreach (bool left in new[] { true, false })
            {
                float sign = left ? -1 : 1;
                var arm = Add(
                    left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                    chest,
                    new Vector3(sign * 0.2f, 0.1f, 0)
                );
                var forearm = Add(
                    left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                    arm,
                    new Vector3(sign * 0.3f, 0, 0)
                );
                Add(
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                    forearm,
                    new Vector3(sign * 0.25f, 0, 0)
                );
                var leg = Add(
                    left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                    hips,
                    new Vector3(sign * 0.1f, -0.05f, 0)
                );
                var shin = Add(
                    left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                    leg,
                    new Vector3(0, -0.4f, 0)
                );
                Add(
                    left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                    shin,
                    new Vector3(0, -0.4f, 0.03f)
                );
            }
            var human = new System.Collections.Generic.List<HumanBone>();
            foreach (var item in mapped)
                human.Add(
                    new HumanBone
                    {
                        boneName = item.Value.name,
                        humanName = HumanTrait.BoneName[(int)item.Key],
                        limit = new HumanLimit { useDefaultValues = true },
                    }
                );
            var skeleton = new System.Collections.Generic.List<SkeletonBone>();
            foreach (Transform bone in root.GetComponentsInChildren<Transform>())
                skeleton.Add(
                    new SkeletonBone
                    {
                        name = bone.name,
                        position = bone.localPosition,
                        rotation = bone.localRotation,
                        scale = bone.localScale,
                    }
                );
            Avatar avatar = null;
            try
            {
                avatar = AvatarBuilder.BuildHumanAvatar(
                    root,
                    new HumanDescription
                    {
                        human = human.ToArray(),
                        skeleton = skeleton.ToArray(),
                        upperArmTwist = 0.5f,
                        lowerArmTwist = 0.5f,
                        upperLegTwist = 0.5f,
                        lowerLegTwist = 0.5f,
                        armStretch = 0.05f,
                        legStretch = 0.05f,
                    }
                );
                Assert.That(avatar.isValid && avatar.isHuman, Is.True);
                root.AddComponent<Animator>().avatar = avatar;
                root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector3[] viewDirections =
                {
                    Vector3.forward,
                    Vector3.left,
                    Vector3.right,
                    Vector3.back,
                };
                for (int view = 0; view < viewDirections.Length; view++)
                    Assert.That(
                        Vector3.Angle(
                            TripoModelPreview.GetViewDirection(root, view),
                            root.transform.TransformDirection(viewDirections[view])
                        ),
                        Is.LessThan(0.01f),
                        "방향 버튼은 회전된 캐릭터의 정면과 신체 좌우를 따라야 합니다."
                    );
                Transform elbow = mapped[joint];
                Quaternion before = elbow.localRotation;
                Vector3 hand = mapped[tip].position;
                using (var preview = new TripoMotionPreview(root))
                {
                    preview.Apply(motion, 0.5f);
                    Assert.That(Quaternion.Angle(before, elbow.localRotation), Is.GreaterThan(1f));
                    Assert.That(
                        Vector3.Distance(hand, mapped[tip].position),
                        Is.GreaterThan(0.01f)
                    );
                }
                Assert.That(Quaternion.Angle(before, elbow.localRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(hand, mapped[tip].position), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (avatar != null)
                    Object.DestroyImmediate(avatar);
            }
        }

        [TestCase(TripoReviewMotion.Arms)]
        [TestCase(TripoReviewMotion.Elbows)]
        [TestCase(TripoReviewMotion.Knees)]
        public void SampleMuscles_CyclesWithoutChangingRestOrFace(TripoReviewMotion motion)
        {
            var rest = new float[HumanTrait.MuscleCount];
            int jaw = System.Array.IndexOf(HumanTrait.MuscleName, "Jaw Close");
            Assert.That(jaw, Is.GreaterThanOrEqualTo(0));
            rest[jaw] = 0.25f;
            var snapshot = (float[])rest.Clone();
            Assert.That(TripoMotionPreview.SampleMuscles(rest, motion, 0), Is.EqualTo(snapshot));
            float[] bent = TripoMotionPreview.SampleMuscles(rest, motion, 0.5f);
            Assert.That(
                bent,
                Is.Not.EqualTo(snapshot),
                "중간 프레임에서 실제 관절 값이 달라져야 합니다."
            );
            Assert.That(bent[jaw], Is.EqualTo(0.25f));
            Assert.That(rest, Is.EqualTo(snapshot));
            Assert.That(TripoMotionPreview.SampleMuscles(rest, motion, 1), Is.EqualTo(snapshot));
            foreach (float value in bent)
                Assert.That(value, Is.InRange(-1f, 1f));
        }
    }
}
