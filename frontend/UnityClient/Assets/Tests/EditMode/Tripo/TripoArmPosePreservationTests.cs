using A305.EditorTools.Tripo;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class TripoArmPosePreservationTests
    {
        [Test]
        public void ClearancePreservesAuthoredElbowBendAndWristPoseDuringRunning()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/Characters/Female.prefab"
            );
            var raw = Object.Instantiate(prefab);
            var corrected = Object.Instantiate(prefab);
            try
            {
                var first = raw.GetComponentInChildren<Animator>();
                var second = corrected.GetComponentInChildren<Animator>();
                Object.DestroyImmediate(first.GetComponent<LittleMungArmClearance>());
                using var before = new TripoMotionPreview(raw);
                using var after = new TripoMotionPreview(corrected);
                before.Apply(TripoReviewMotion.Run, .25f);
                after.Apply(TripoReviewMotion.Run, .25f);
                foreach (
                    var joint in new[]
                    {
                        HumanBodyBones.LeftLowerArm,
                        HumanBodyBones.RightLowerArm,
                        HumanBodyBones.LeftHand,
                        HumanBodyBones.RightHand,
                    }
                )
                    Assert.That(
                        Quaternion.Angle(
                            first.GetBoneTransform(joint).localRotation,
                            second.GetBoneTransform(joint).localRotation
                        ),
                        Is.LessThan(.1f),
                        joint
                            + ": shoulder clearance must not straighten the elbow or counter-rotate the wrist."
                    );
            }
            finally
            {
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(corrected);
            }
        }
    }
}
