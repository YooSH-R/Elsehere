using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class ObjectHoverRulesTests
    {
        [Test]
        public void StillAndNear_CanHover()
        {
            Assert.That(
                ObjectHoverRules.CanHover(
                    isPointerOverUi: false,
                    isMoving: false,
                    isMovementLocked: false,
                    isWithinReach: true,
                    isTouchDevice: false
                ),
                Is.True
            );
        }

        [Test]
        public void PointerOverUi_DoesNotHoverObjectBehindIt()
        {
            // HUD 메뉴·창 뒤의 벽·천장을 가리킨 것으로 보면 카드가 메뉴를 덮는다.
            Assert.That(
                ObjectHoverRules.CanHover(
                    isPointerOverUi: true,
                    isMoving: false,
                    isMovementLocked: false,
                    isWithinReach: true,
                    isTouchDevice: false
                ),
                Is.False
            );
        }

        [Test]
        public void Moving_DoesNotHover()
        {
            Assert.That(
                ObjectHoverRules.CanHover(
                    isPointerOverUi: false,
                    isMoving: true,
                    isMovementLocked: false,
                    isWithinReach: true,
                    isTouchDevice: false
                ),
                Is.False
            );
        }

        [Test]
        public void MovementLocked_DoesNotHover()
        {
            // 대화나 단어장·피드백·내 정보 창이 열려 있으면 이동이 잠긴다.
            Assert.That(
                ObjectHoverRules.CanHover(
                    isPointerOverUi: false,
                    isMoving: false,
                    isMovementLocked: true,
                    isWithinReach: true,
                    isTouchDevice: false
                ),
                Is.False
            );
        }

        [Test]
        public void OutOfReach_DoesNotHover()
        {
            Assert.That(
                ObjectHoverRules.CanHover(
                    isPointerOverUi: false,
                    isMoving: false,
                    isMovementLocked: false,
                    isWithinReach: false,
                    isTouchDevice: false
                ),
                Is.False
            );
        }

        [Test]
        public void TouchDevice_DoesNotHover()
        {
            // 터치 화면에는 호버가 없어, 마지막으로 터치한 자리에 카드가 남아 뜬다. 탭으로만 카드를 고정한다.
            Assert.That(
                ObjectHoverRules.CanHover(
                    isPointerOverUi: false,
                    isMoving: false,
                    isMovementLocked: false,
                    isWithinReach: true,
                    isTouchDevice: true
                ),
                Is.False
            );
        }
    }
}
