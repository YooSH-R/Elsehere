using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>앉는 동안 서 있는 자세 보정만 껐다가, 일어서면 켜져 있던 것만 다시 켜는지.</summary>
    public class StandingPoseCorrectionsTests
    {
        [Test]
        public void Suspend_TurnsOffOnlyStandingCorrections_AndRestoreTurnsOnOnlyThoseThatWereOn()
        {
            var visual = new GameObject("Visual");
            var model = new GameObject("Model", typeof(Animator));
            model.transform.SetParent(visual.transform);
            try
            {
                var animator = model.GetComponent<Animator>();
                var ground = model.AddComponent<LittleMungGroundContact>();
                var terrain = model.AddComponent<LittleMungTerrainContact>();
                var stairs = model.AddComponent<LittleMungStairLocomotion>();
                var arms = model.AddComponent<LittleMungArmClearance>();
                arms.enabled = false;

                StandingPoseCorrections suspended = StandingPoseCorrections.Suspend(
                    visual.transform
                );

                Assert.That(ground.enabled, Is.False);
                Assert.That(terrain.enabled, Is.False);
                Assert.That(stairs.enabled, Is.False);
                Assert.That(animator.enabled, Is.True, "자세 보정이 아닌 동작은 그대로 둡니다.");

                suspended.Restore();

                Assert.That(ground.enabled, Is.True);
                Assert.That(terrain.enabled, Is.True);
                Assert.That(stairs.enabled, Is.True);
                Assert.That(arms.enabled, Is.False, "앉기 전에 꺼져 있던 보정은 켜지 않습니다.");
            }
            finally
            {
                Object.DestroyImmediate(visual);
            }
        }
    }
}
