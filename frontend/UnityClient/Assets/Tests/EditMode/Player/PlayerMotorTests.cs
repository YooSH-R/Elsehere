using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    public class PlayerMotorTests
    {
        private const float WalkSpeed = 2f;
        private const float SprintSpeed = 5f;
        private const float JumpHeight = 1.25f;
        private const float Gravity = 20f;
        private const float DeltaTime = 0.1f;
        private const float Tolerance = 0.0001f;

        private PlayerMotor motor;

        [SetUp]
        public void CreateMotor()
        {
            motor = new PlayerMotor(WalkSpeed, SprintSpeed, JumpHeight, Gravity);
        }

        [Test]
        public void ComputeVelocity_NoMoveInput_DoesNotMoveHorizontally()
        {
            Vector3 velocity = motor.ComputeVelocity(Walk(Vector2.zero), true, DeltaTime);

            Assert.That(Horizontal(velocity).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ComputeVelocity_ForwardWithCameraFacingNorth_MovesAlongPositiveZ()
        {
            Vector3 velocity = motor.ComputeVelocity(Walk(Vector2.up), true, DeltaTime);

            AssertHorizontal(new Vector3(0f, 0f, WalkSpeed), velocity);
        }

        [Test]
        public void ComputeVelocity_ForwardWithCameraTurnedRight_MovesAlongPositiveX()
        {
            Vector3 velocity = motor.ComputeVelocity(Walk(Vector2.up, 90f), true, DeltaTime);

            AssertHorizontal(new Vector3(WalkSpeed, 0f, 0f), velocity);
        }

        [Test]
        public void ComputeVelocity_DiagonalKeys_MovesNoFasterThanWalkSpeed()
        {
            Vector3 velocity = motor.ComputeVelocity(Walk(new Vector2(1f, 1f)), true, DeltaTime);

            Assert.That(Horizontal(velocity).magnitude, Is.EqualTo(WalkSpeed).Within(Tolerance));
        }

        [Test]
        public void ComputeVelocity_HalfTiltedStick_MovesAtHalfWalkSpeed()
        {
            Vector3 velocity = motor.ComputeVelocity(Walk(new Vector2(0f, 0.5f)), true, DeltaTime);

            Assert.That(
                Horizontal(velocity).magnitude,
                Is.EqualTo(WalkSpeed * 0.5f).Within(Tolerance)
            );
        }

        [Test]
        public void ComputeVelocity_Sprinting_MovesAtSprintSpeed()
        {
            var input = new PlayerMoveInput(Vector2.up, 0f, true, false);

            Vector3 velocity = motor.ComputeVelocity(input, true, DeltaTime);

            AssertHorizontal(new Vector3(0f, 0f, SprintSpeed), velocity);
        }

        [Test]
        public void ComputeVelocity_JumpOnGround_StartsAtSpeedThatReachesJumpHeight()
        {
            Vector3 velocity = motor.ComputeVelocity(Jump(), true, DeltaTime);

            float expectedSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
            Assert.That(velocity.y, Is.EqualTo(expectedSpeed).Within(Tolerance));
        }

        [Test]
        public void ComputeVelocity_JumpOnGround_ReportsThatThePlayerJumped_OnlyForThatStep()
        {
            // 튜토리얼 걷기 단계가 [Space] 점프를 해냈는지 셀 수 있게 뛰어오른 순간을 알린다.
            motor.ComputeVelocity(Jump(), true, DeltaTime);
            Assert.That(motor.JustJumped, Is.True);

            motor.ComputeVelocity(Jump(), false, DeltaTime);
            Assert.That(motor.JustJumped, Is.False, "공중에서 누른 점프는 뛰어오른 것이 아닙니다.");
        }

        [Test]
        public void ComputeVelocity_JumpInAir_IsIgnoredAndGravityApplies()
        {
            Vector3 velocity = motor.ComputeVelocity(Jump(), false, DeltaTime);

            Assert.That(velocity.y, Is.EqualTo(-Gravity * DeltaTime).Within(Tolerance));
        }

        [Test]
        public void ComputeVelocity_FallingForTwoSteps_AccumulatesGravity()
        {
            motor.ComputeVelocity(Walk(Vector2.zero), false, DeltaTime);
            Vector3 velocity = motor.ComputeVelocity(Walk(Vector2.zero), false, DeltaTime);

            Assert.That(velocity.y, Is.EqualTo(-2f * Gravity * DeltaTime).Within(Tolerance));
        }

        [Test]
        public void ComputeVelocity_StandingOnGround_KeepsSameSmallDownwardSpeed()
        {
            Vector3 firstVelocity = motor.ComputeVelocity(Walk(Vector2.zero), true, DeltaTime);
            Vector3 secondVelocity = motor.ComputeVelocity(Walk(Vector2.zero), true, DeltaTime);

            Assert.That(firstVelocity.y, Is.LessThan(0f));
            Assert.That(secondVelocity.y, Is.EqualTo(firstVelocity.y).Within(Tolerance));
        }

        [Test]
        public void ComputeVelocity_AfterLanding_DropsFallSpeedToStandingSpeed()
        {
            var standingMotor = new PlayerMotor(WalkSpeed, SprintSpeed, JumpHeight, Gravity);
            Vector3 standingVelocity = standingMotor.ComputeVelocity(
                Walk(Vector2.zero),
                true,
                DeltaTime
            );

            motor.ComputeVelocity(Walk(Vector2.zero), false, 1f);
            Vector3 landedVelocity = motor.ComputeVelocity(Walk(Vector2.zero), true, DeltaTime);

            Assert.That(landedVelocity.y, Is.EqualTo(standingVelocity.y).Within(Tolerance));
        }

        [Test]
        public void ComputeVelocity_MovementLocked_IgnoresMoveAndJump()
        {
            motor.SetMovementLocked(true);
            var input = new PlayerMoveInput(Vector2.up, 0f, true, true);

            Vector3 velocity = motor.ComputeVelocity(input, true, DeltaTime);

            Assert.That(Horizontal(velocity).magnitude, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(velocity.y, Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void ComputeVelocity_MovementUnlocked_MovesAgain()
        {
            motor.SetMovementLocked(true);
            motor.SetMovementLocked(false);

            Vector3 velocity = motor.ComputeVelocity(Walk(Vector2.up), true, DeltaTime);

            AssertHorizontal(new Vector3(0f, 0f, WalkSpeed), velocity);
        }

        private static PlayerMoveInput Walk(Vector2 move, float cameraYawDegrees = 0f)
        {
            return new PlayerMoveInput(move, cameraYawDegrees, false, false);
        }

        private static PlayerMoveInput Jump()
        {
            return new PlayerMoveInput(Vector2.zero, 0f, false, true);
        }

        private static Vector3 Horizontal(Vector3 velocity)
        {
            return new Vector3(velocity.x, 0f, velocity.z);
        }

        private static void AssertHorizontal(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
