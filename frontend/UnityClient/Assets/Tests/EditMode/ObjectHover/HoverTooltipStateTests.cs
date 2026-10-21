using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class HoverTooltipStateTests
    {
        private const float ShowDelaySeconds = 0.2f;

        private HoverTooltipState state;

        [SetUp]
        public void CreateState()
        {
            state = new HoverTooltipState(ShowDelaySeconds);
        }

        [Test]
        public void NewState_ShowsNothing()
        {
            Assert.That(state.VisibleKey, Is.Null);
        }

        [Test]
        public void Update_HoveringShorterThanDelay_ShowsNothing()
        {
            // 마우스가 사물 위를 스쳐 지나갈 때는 툴팁이 깜박이지 않아야 한다.
            state.Update("tv", canShow: true, 0.1f);

            Assert.That(state.VisibleKey, Is.Null);
        }

        [Test]
        public void Update_HoveringForDelay_ShowsHoveredObject()
        {
            state.Update("tv", canShow: true, 0.1f);
            state.Update("tv", canShow: true, 0.1f);

            Assert.That(state.VisibleKey, Is.EqualTo("tv"));
        }

        [Test]
        public void Update_PointerLeaves_HidesImmediately()
        {
            state.Update("tv", canShow: true, ShowDelaySeconds);

            state.Update(null, canShow: true, 0.01f);

            Assert.That(state.VisibleKey, Is.Null);
        }

        [Test]
        public void Update_PointerMovesToOtherObject_HidesAndShowsOtherOnlyAfterDelay()
        {
            // 툴팁은 하나만 보이고, 다른 사물로 옮기면 그 사물을 잠시 가리킨 뒤에 바뀐다.
            state.Update("tv", canShow: true, ShowDelaySeconds);

            state.Update("sofa", canShow: true, 0.1f);
            Assert.That(state.VisibleKey, Is.Null);

            state.Update("sofa", canShow: true, 0.1f);
            Assert.That(state.VisibleKey, Is.EqualTo("sofa"));
        }

        [Test]
        public void Update_WhenCannotShow_HidesImmediately()
        {
            // 이동 중이거나, 너무 멀거나, 대화 같은 상호작용 중이면 보이지 않는다.
            state.Update("tv", canShow: true, ShowDelaySeconds);

            state.Update("tv", canShow: false, 0.01f);

            Assert.That(state.VisibleKey, Is.Null);
        }

        [Test]
        public void Update_CanShowAgain_WaitsForDelayAgain()
        {
            state.Update("tv", canShow: true, ShowDelaySeconds);
            state.Update("tv", canShow: false, 0.5f);

            state.Update("tv", canShow: true, 0.1f);
            Assert.That(state.VisibleKey, Is.Null);

            state.Update("tv", canShow: true, 0.1f);
            Assert.That(state.VisibleKey, Is.EqualTo("tv"));
        }

        [Test]
        public void Update_WithZeroDelay_ShowsOnFirstFrame()
        {
            var immediate = new HoverTooltipState(0f);

            immediate.Update("tv", canShow: true, 0f);

            Assert.That(immediate.VisibleKey, Is.EqualTo("tv"));
        }
    }
}
