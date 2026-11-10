using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>
    /// [이웃]에서 [지도에서 보기]로 고른 NPC. 한 번에 한 명만 찾고, 다른 이웃을 고르면 바뀌며, 3m 안에 다가가면 찾기를 끝낸다.
    /// 장면을 옮겨도 남도록 정적으로 둔다.
    /// </summary>
    public class NpcTrackingTests
    {
        private NpcTracking tracking;

        [SetUp]
        public void CreateTracking()
        {
            tracking = new NpcTracking();
        }

        [Test]
        public void Track_RemembersWhoIsBeingLookedFor()
        {
            var life = new NpcLifeEntry { NpcId = "BAKERY_STAFF" };
            int changes = 0;
            tracking.Changed += () => changes++;

            tracking.Track("BAKERY_STAFF", "Mia", life);

            Assert.That(tracking.Code, Is.EqualTo("BAKERY_STAFF"));
            Assert.That(tracking.Name, Is.EqualTo("Mia"));
            Assert.That(tracking.Life, Is.SameAs(life));
            Assert.That(tracking.IsTracking("BAKERY_STAFF"), Is.True);
            Assert.That(tracking.IsTracking("CAFE_STAFF"), Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void TrackingAnotherNeighbor_ReplacesTheFirst()
        {
            tracking.Track("BAKERY_STAFF", "Mia", null);

            tracking.Track("CAFE_STAFF", "Ruby", null);

            Assert.That(tracking.Code, Is.EqualTo("CAFE_STAFF"));
            Assert.That(tracking.Name, Is.EqualTo("Ruby"));
            Assert.That(tracking.IsTracking("BAKERY_STAFF"), Is.False);
        }

        [Test]
        public void Clear_StopsLooking()
        {
            tracking.Track("BAKERY_STAFF", "Mia", null);
            int changes = 0;
            tracking.Changed += () => changes++;

            tracking.Clear();

            Assert.That(tracking.Code, Is.Null);
            Assert.That(tracking.Name, Is.Null);
            Assert.That(tracking.Life, Is.Null);
            Assert.That(tracking.IsTracking("BAKERY_STAFF"), Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Clear_WhenNotLooking_DoesNotAnnounceAChange()
        {
            int changes = 0;
            tracking.Changed += () => changes++;

            tracking.Clear();

            Assert.That(changes, Is.Zero);
        }

        [TestCase(2.9f, true)]
        [TestCase(3f, true)]
        [TestCase(3.1f, false)]
        public void HasArrived_WithinThreeMeters(float meters, bool arrived)
        {
            Assert.That(NpcTracking.HasArrived(meters), Is.EqualTo(arrived));
        }
    }
}
