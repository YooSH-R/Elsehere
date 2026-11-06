using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>
    /// 경찰서는 마을과 다른 장면이라, 창구에서 받은 신고를 기억했다가 마을이 열리면 순찰차가 가져가 출동한다.
    /// </summary>
    public class PoliceDispatchOrderTests
    {
        [SetUp]
        public void Clear()
        {
            PoliceDispatchOrder.Clear();
        }

        [Test]
        public void RequestedOrder_IsTakenOnceWithItsPlace()
        {
            Assert.That(PoliceDispatchOrder.TryRequest("DINER"), Is.True);

            Assert.That(PoliceDispatchOrder.TryTake(out string place), Is.True);
            Assert.That(place, Is.EqualTo("DINER"));
            Assert.That(PoliceDispatchOrder.TryTake(out _), Is.False, "한 번 가져가면 없어진다.");
        }

        // 장소를 말하지 않은 신고는 본길을 순찰한다.
        [Test]
        public void OrderWithoutAPlace_IsTakenAsAPatrol()
        {
            PoliceDispatchOrder.TryRequest(null);

            Assert.That(PoliceDispatchOrder.TryTake(out string place), Is.True);
            Assert.That(place, Is.Null);
        }

        [Test]
        public void NoOrder_IsNotTaken()
        {
            Assert.That(PoliceDispatchOrder.TryTake(out _), Is.False);
            Assert.That(PoliceDispatchOrder.IsPending, Is.False);
        }

        // 순찰차는 한 대라, 아직 출발하지 않은 신고가 있으면 새 신고는 받지 않고 먼저 받은 장소로 간다.
        [Test]
        public void SecondRequestWhilePending_KeepsTheFirstPlace()
        {
            PoliceDispatchOrder.TryRequest("DINER");

            Assert.That(PoliceDispatchOrder.TryRequest("BANK"), Is.False);
            Assert.That(PoliceDispatchOrder.IsPending, Is.True);
            PoliceDispatchOrder.TryTake(out string place);
            Assert.That(place, Is.EqualTo("DINER"));
        }
    }
}
