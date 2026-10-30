using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 택시는 인도에서 손님이 세우면 그 옆 길가에 서서 기다리고, 대화를 마치거나 손님이 떠나면 다시 달린다.
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

        [Test]
        public void SomeoneOnTheCurbsideSidewalkAhead_CanHail()
        {
            Assert.That(TaxiFare.CanHail(route, 10f, new Vector3(4f, 0f, SouthZ + 20f)), Is.True);
        }

        [Test]
        public void SomeoneBehindTheTaxi_CannotHail()
        {
            Assert.That(TaxiFare.CanHail(route, 10f, new Vector3(4f, 0f, SouthZ + 5f)), Is.False);
        }

        [Test]
        public void SomeoneAcrossTheRoad_CannotHail()
        {
            Assert.That(TaxiFare.CanHail(route, 10f, new Vector3(-4f, 0f, SouthZ + 20f)), Is.False);
        }

        [Test]
        public void SomeoneTooFarAhead_CannotHailYet()
        {
            Assert.That(TaxiFare.CanHail(route, 10f, new Vector3(4f, 0f, SouthZ + 40f)), Is.False);
        }

        [Test]
        public void SomeoneBeyondTheUTurn_CannotHail()
        {
            float nearNorthEnd = route.NorthboundDistance(NorthZ - 3f);

            Assert.That(
                TaxiFare.CanHail(route, nearNorthEnd, new Vector3(4f, 0f, NorthZ + 8f)),
                Is.False
            );
        }

        [Test]
        public void PullOver_StopsBesideTheCustomer()
        {
            float stop = TaxiFare.PullOverDistance(route, 10f, new Vector3(4f, 0f, SouthZ + 20f));

            Assert.That(stop, Is.EqualTo(20f).Within(.001f));
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
