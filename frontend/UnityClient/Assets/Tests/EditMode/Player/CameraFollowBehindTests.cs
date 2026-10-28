using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>걷는 동안 카메라가 캐릭터 뒤로 천천히 돌아가 등을 보게 하는 동작.</summary>
    public class CameraFollowBehindTests
    {
        private const float Tolerance = 0.01f;

        private static readonly CameraOrbitLimits Limits = new CameraOrbitLimits(
            minDistanceMeters: 2f,
            maxDistanceMeters: 8f,
            zoomStepMeters: 1f,
            thirdPersonMinPitchDegrees: 5f,
            thirdPersonMaxPitchDegrees: 70f,
            firstPersonMinPitchDegrees: -60f,
            firstPersonMaxPitchDegrees: 70f,
            rotateDegreesPerPixel: 0.2f,
            followBehindSharpness: 3f
        );

        private CameraOrbit orbit;

        [SetUp]
        public void CreateOrbit()
        {
            orbit = new CameraOrbit(Limits, startDistanceMeters: 5f, startPitchDegrees: 20f);
        }

        [Test]
        public void FollowBehind_TurnsPartwayTowardHeadingWithoutSnapping()
        {
            orbit.FollowBehind(headingDegrees: 90f, deltaTimeSeconds: 0.1f);

            Assert.That(orbit.YawDegrees, Is.GreaterThan(0f));
            Assert.That(orbit.YawDegrees, Is.LessThan(45f));
        }

        [Test]
        public void FollowBehind_KeptUp_EndsBehindHeadingWithoutOvershoot()
        {
            for (int frame = 0; frame < 120; frame++)
            {
                orbit.FollowBehind(headingDegrees: 90f, deltaTimeSeconds: 1f / 30f);
                Assert.That(orbit.YawDegrees, Is.LessThanOrEqualTo(90f + Tolerance));
            }

            Assert.That(orbit.YawDegrees, Is.EqualTo(90f).Within(0.5f));
        }

        [Test]
        public void FollowBehind_TakesTheShortWayAround()
        {
            orbit.SetYaw(350f);

            orbit.FollowBehind(headingDegrees: 10f, deltaTimeSeconds: 0.1f);

            float turned = Mathf.DeltaAngle(350f, orbit.YawDegrees);
            Assert.That(turned, Is.GreaterThan(0f));
            Assert.That(turned, Is.LessThan(20f));
        }

        [Test]
        public void FollowBehind_InFirstPersonOrDialogue_LeavesViewToThePlayer()
        {
            orbit.ToggleFirstPerson();
            orbit.FollowBehind(headingDegrees: 90f, deltaTimeSeconds: 0.5f);
            Assert.That(orbit.YawDegrees, Is.EqualTo(0f).Within(Tolerance));

            orbit.ToggleFirstPerson();
            orbit.EnterDialogueView();
            orbit.FollowBehind(headingDegrees: 90f, deltaTimeSeconds: 0.5f);
            Assert.That(orbit.YawDegrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void FollowBehind_WithoutSharpness_KeepsTheOldFixedCamera()
        {
            var fixedOrbit = new CameraOrbit(
                new CameraOrbitLimits(2f, 8f, 1f, 5f, 70f, -60f, 70f, 0.2f),
                startDistanceMeters: 5f,
                startPitchDegrees: 20f
            );

            fixedOrbit.FollowBehind(headingDegrees: 90f, deltaTimeSeconds: 0.5f);

            Assert.That(fixedOrbit.YawDegrees, Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
