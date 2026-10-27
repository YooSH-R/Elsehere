using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace A305.Tripo.Tests
{
    public class TripoRigControlsTests
    {
        [Test]
        public void PoseSession_MovingHandleMovesTip_AndDisposeRestoresRestPose()
        {
            var owner = new GameObject("Character");
            var upper = new GameObject("Upper").transform;
            upper.SetParent(owner.transform, false);
            var lower = new GameObject("Lower").transform;
            lower.SetParent(upper, false);
            lower.localPosition = Vector3.right;
            var hand = new GameObject("Hand").transform;
            hand.SetParent(lower, false);
            hand.localPosition = Vector3.right;
            var animator = owner.AddComponent<Animator>();
            Avatar avatar = AvatarBuilder.BuildGenericAvatar(owner, "");
            animator.avatar = avatar;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rig = new GameObject("Controls");
            rig.transform.SetParent(owner.transform, false);
            var builder = owner.AddComponent<RigBuilder>();
            builder.layers.Add(new RigLayer(rig.AddComponent<Rig>()));
            var control = TripoRigControls
                .AddLimb(rig, "Hand", upper, lower, hand)
                .GetComponent<TwoBoneIKConstraint>();
            Vector3 rest = hand.position;
            try
            {
                using (var session = new TripoPoseSession(builder))
                {
                    control.data.target.position = new Vector3(1f, 1f, 0f);
                    session.Evaluate();
                    Assert.That(
                        Vector3.Distance(hand.position, control.data.target.position),
                        Is.LessThan(0.02f)
                    );
                }
                Assert.That(Vector3.Distance(hand.position, rest), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(avatar);
            }
        }

        [Test]
        public void AddLimb_CreatesReadyHandlesWithoutMovingBones_AndPreservesEditedTarget()
        {
            var owner = new GameObject("Character");
            var upper = new GameObject("Upper");
            upper.transform.SetParent(owner.transform, false);
            var lower = new GameObject("Lower");
            lower.transform.SetParent(upper.transform, false);
            lower.transform.localPosition = Vector3.right;
            var hand = new GameObject("Hand");
            hand.transform.SetParent(lower.transform, false);
            hand.transform.localPosition = Vector3.right;
            var rig = new GameObject("Controls");
            rig.transform.SetParent(owner.transform, false);
            try
            {
                Vector3 rest = hand.transform.position;
                var control = TripoRigControls.AddLimb(
                    rig,
                    "LeftHand",
                    upper.transform,
                    lower.transform,
                    hand.transform
                );
                Assert.That(control, Is.Not.Null);
                var constraint = control.GetComponent<TwoBoneIKConstraint>();
                Assert.That(constraint.data.root, Is.SameAs(upper.transform));
                Assert.That(constraint.data.tip, Is.SameAs(hand.transform));
                Assert.That(constraint.data.target.position, Is.EqualTo(rest));
                Assert.That(hand.transform.position, Is.EqualTo(rest));
                constraint.data.target.position += Vector3.up;
                var repeated = TripoRigControls.AddLimb(
                    rig,
                    "LeftHand",
                    upper.transform,
                    lower.transform,
                    hand.transform
                );
                Assert.That(repeated, Is.SameAs(control));
                Assert.That(constraint.data.target.position, Is.EqualTo(rest + Vector3.up));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
