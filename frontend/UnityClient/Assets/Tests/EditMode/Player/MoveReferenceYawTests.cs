using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>카메라가 뒤로 따라 도는 동안 이동 키를 어느 방향 기준으로 읽을지.</summary>
    public class MoveReferenceYawTests
    {
        private const float Tolerance = 0.001f;

        private MoveReferenceYaw reference;

        [SetUp]
        public void CreateReference()
        {
            reference = new MoveReferenceYaw();
        }

        [Test]
        public void Resolve_WithoutInput_FollowsTheCamera()
        {
            Assert.That(
                reference.Resolve(Vector2.zero, 40f, cameraTurnedByUser: false),
                Is.EqualTo(40f).Within(Tolerance)
            );
        }

        [Test]
        public void Resolve_WhileHoldingKeys_KeepsTheCameraDirectionFromWhenWalkingStarted()
        {
            // 뒤로 걷기(S)로 캐릭터가 돌아서면 카메라가 등 뒤로 따라 돈다. 기준이 같이 돌면 캐릭터가 계속 돌아서므로 고정한다.
            reference.Resolve(Vector2.down, 0f, cameraTurnedByUser: false);

            Assert.That(
                reference.Resolve(Vector2.down, 120f, cameraTurnedByUser: false),
                Is.EqualTo(0f).Within(Tolerance)
            );
        }

        [Test]
        public void Resolve_ChangingKeysWhileHolding_KeepsTheSameReference()
        {
            reference.Resolve(Vector2.down, 0f, cameraTurnedByUser: false);

            Assert.That(
                reference.Resolve(new Vector2(-1f, -1f), 150f, cameraTurnedByUser: false),
                Is.EqualTo(0f).Within(Tolerance)
            );
        }

        [Test]
        public void Resolve_AfterLettingGo_UsesTheCameraDirectionAgain()
        {
            reference.Resolve(Vector2.down, 0f, cameraTurnedByUser: false);
            reference.Resolve(Vector2.zero, 120f, cameraTurnedByUser: false);

            Assert.That(
                reference.Resolve(Vector2.up, 120f, cameraTurnedByUser: false),
                Is.EqualTo(120f).Within(Tolerance)
            );
        }

        [Test]
        public void Resolve_WhenThePlayerTurnsTheCamera_SteersByTheNewDirection()
        {
            reference.Resolve(Vector2.up, 0f, cameraTurnedByUser: false);

            Assert.That(
                reference.Resolve(Vector2.up, 30f, cameraTurnedByUser: true),
                Is.EqualTo(30f).Within(Tolerance)
            );
        }
    }
}
