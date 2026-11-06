using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 택시는 가까우면 어느 방향에서도 탈 수 있고, 대화를 마치거나 손님이 떠나면 다시 달린다.
    /// </summary>
    public class TaxiFareTests
    {
        private const float SouthZ = -6f;
        private const float NorthZ = 66f;
        private const float LaneOffset = 1.5f;

        private CarLoopRoute route;
        private TaxiFare fare;

        [SetUp]
        public void Create()
        {
            route = new CarLoopRoute(SouthZ, NorthZ, LaneOffset);
            fare = new TaxiFare();
        }

        [TestCase(0f, -5f)]
        [TestCase(-5f, 0f)]
        [TestCase(5f, 0f)]
        [TestCase(0f, 5f)]
        public void CustomerWithinRadius_CanHailFromAnyDirection(float x, float z)
        {
            Vector3 taxi = route.PoseAt(10f).Position;

            Assert.That(TaxiFare.CanHail(taxi, taxi + new Vector3(x, 0f, z)), Is.True);
        }

        [Test]
        public void SomeoneOnTheCurbsideSidewalkAhead_CanHail()
        {
            Assert.That(
                TaxiFare.CanHail(route.PoseAt(10f).Position, new Vector3(4f, 0f, SouthZ + 15f)),
                Is.True
            );
        }

        [Test]
        public void SomeoneBehindTheTaxi_CanHail()
        {
            Assert.That(
                TaxiFare.CanHail(route.PoseAt(10f).Position, new Vector3(4f, 0f, SouthZ + 5f)),
                Is.True
            );
        }

        [Test]
        public void SomeoneAcrossTheRoad_CanHail()
        {
            Assert.That(
                TaxiFare.CanHail(route.PoseAt(10f).Position, new Vector3(-4f, 0f, SouthZ + 10f)),
                Is.True
            );
        }

        [Test]
        public void SomeoneTooFarAhead_CannotHailYet()
        {
            Assert.That(
                TaxiFare.CanHail(route.PoseAt(10f).Position, new Vector3(4f, 0f, SouthZ + 40f)),
                Is.False
            );
        }

        [Test]
        public void SomeoneBeyondTheUTurnWithinRadius_CanHail()
        {
            float nearNorthEnd = route.NorthboundDistance(NorthZ - 3f);

            Assert.That(
                TaxiFare.CanHail(
                    route.PoseAt(nearNorthEnd).Position,
                    new Vector3(4f, 0f, NorthZ + 2f)
                ),
                Is.True
            );
        }

        [TestCase(8f, 0f, true)]
        [TestCase(-8f, 0f, true)]
        [TestCase(0f, 8f, true)]
        [TestCase(0f, -8f, true)]
        [TestCase(8.01f, 0f, false)]
        [TestCase(-8.01f, 0f, false)]
        [TestCase(0f, 8.01f, false)]
        [TestCase(0f, -8.01f, false)]
        [TestCase(6f, 6f, false)]
        [TestCase(0f, 0f, true)]
        public void BoardingRadius_UsesCircularHorizontalDistance(float x, float z, bool expected)
        {
            Vector3 taxi = new Vector3(-3f, .2f, 12f);
            Vector3 customer = taxi + new Vector3(x, .5f, z);
            Assert.That(TaxiFare.CanHail(taxi, customer), Is.EqualTo(expected));
        }

        [Test]
        public void NewFare_IsCruising()
        {
            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Cruising));
        }

        [Test]
        public void Hail_WhileCruising_PullsOver()
        {
            Assert.That(fare.TryHail(), Is.True);
            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.PullingOver));
        }

        [Test]
        public void Hail_WhileAlreadyPullingOver_IsIgnored()
        {
            fare.TryHail();

            Assert.That(fare.TryHail(), Is.False);
            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.PullingOver));
        }

        [Test]
        public void ArrivingBesideTheCustomer_Waits()
        {
            fare.TryHail();
            fare.Arrive();

            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Waiting));
        }

        [Test]
        public void Arrive_WithoutBeingHailed_KeepsCruising()
        {
            fare.Arrive();

            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Cruising));
        }

        [Test]
        public void CustomerWalksAway_TaxiDrivesOn()
        {
            fare.TryHail();
            fare.Arrive();

            fare.Tick(TaxiFare.LeaveDistanceMeters + 1f, isTalking: false);

            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Cruising));
        }

        [Test]
        public void CustomerNearby_TaxiKeepsWaiting()
        {
            fare.TryHail();
            fare.Arrive();

            fare.Tick(2f, isTalking: false);

            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Waiting));
        }

        [Test]
        public void WhileTalking_TaxiKeepsWaiting()
        {
            fare.TryHail();
            fare.Arrive();

            fare.Tick(TaxiFare.LeaveDistanceMeters + 1f, isTalking: true);

            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Waiting));
        }

        [Test]
        public void ConversationEnds_TaxiDrivesOn()
        {
            fare.TryHail();
            fare.Arrive();

            fare.EndRide();

            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Cruising));
        }

        [Test]
        public void CustomerWalksAwayBeforeTheTaxiArrives_TaxiDrivesOn()
        {
            fare.TryHail();

            fare.Tick(TaxiFare.LeaveDistanceMeters + 20f, isTalking: false);

            Assert.That(fare.Phase, Is.EqualTo(TaxiPhase.Cruising));
        }
    }
}
