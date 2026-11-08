using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 마을 본길의 차는 오른쪽 차로로 북쪽 끝까지 가서 유턴하고, 반대 차로로 남쪽 끝까지 내려와 다시 유턴한다.
    /// </summary>
    public class CarLoopRouteTests
    {
        private const float SouthZ = -21f;
        private const float NorthZ = 66f;
        private const float LaneOffset = 1.5f;
        private const float Straight = NorthZ - SouthZ;
        private const float Turn = Mathf.PI * LaneOffset;

        private CarLoopRoute route;

        [SetUp]
        public void CreateRoute()
        {
            route = new CarLoopRoute(SouthZ, NorthZ, LaneOffset);
        }

        [Test]
        public void Start_IsNorthboundInTheEastLane()
        {
            CarLoopRoute.Pose pose = route.PoseAt(0f);

            AssertNear(pose.Position, new Vector3(LaneOffset, 0f, SouthZ));
            AssertNear(pose.Forward, Vector3.forward);
        }

        [TestCase(-11f)]
        [TestCase(6.85f)]
        [TestCase(13.15f)]
        public void PedestrianCrossing_StopsCarsBeforeTheCrosswalkInBothLanes(float crossingZ)
        {
            Assert.That(route.TryCrossingStops(crossingZ, 3.65f,
                out float north, out float south), Is.True);
            Assert.That(route.PoseAt(north).Position.z, Is.EqualTo(crossingZ - 3.65f).Within(.001f));
            Assert.That(route.PoseAt(south).Position.z, Is.EqualTo(crossingZ + 3.65f).Within(.001f));
            Assert.That(route.Advance(north - 1, 5, new[] { north, south }), Is.EqualTo(north).Within(.001f));
            Assert.That(route.Advance(south - 1, 5, new[] { north, south }), Is.EqualTo(south).Within(.001f));
        }

        [TestCase(-24f)]
        [TestCase(66f)]
        public void CrossingOutsideTheStraightRoad_DoesNotCreateAWrappedStop(float crossingZ)
        {
            Assert.That(route.TryCrossingStops(crossingZ, 3.65f, out _, out _), Is.False);
        }

        [Test]
        public void EastLane_GoesNorth()
        {
            CarLoopRoute.Pose pose = route.PoseAt(10f);

            AssertNear(pose.Position, new Vector3(LaneOffset, 0f, SouthZ + 10f));
            AssertNear(pose.Forward, Vector3.forward);
        }

        [Test]
        public void NorthEnd_TurnsAroundAcrossTheRoad()
        {
            CarLoopRoute.Pose pose = route.PoseAt(Straight + Turn / 2f);

            AssertNear(pose.Position, new Vector3(0f, 0f, NorthZ + LaneOffset));
            AssertNear(pose.Forward, Vector3.left);
        }

        [Test]
        public void WestLane_ComesBackSouth()
        {
            CarLoopRoute.Pose pose = route.PoseAt(Straight + Turn + 10f);

            AssertNear(pose.Position, new Vector3(-LaneOffset, 0f, NorthZ - 10f));
            AssertNear(pose.Forward, Vector3.back);
        }

        [Test]
        public void SouthEnd_TurnsBackIntoTheEastLane()
        {
            CarLoopRoute.Pose pose = route.PoseAt(2f * Straight + Turn + Turn / 2f);

            AssertNear(pose.Position, new Vector3(0f, 0f, SouthZ - LaneOffset));
            AssertNear(pose.Forward, Vector3.right);
        }

        [Test]
        public void FullLoop_ReturnsToTheStart()
        {
            Assert.That(route.Length, Is.EqualTo(2f * Straight + 2f * Turn).Within(.001f));

            CarLoopRoute.Pose pose = route.PoseAt(route.Length + 10f);

            AssertNear(pose.Position, new Vector3(LaneOffset, 0f, SouthZ + 10f));
            AssertNear(pose.Forward, Vector3.forward);
        }

        [Test]
        public void LaneDistance_FindsWhereEachLanePassesAStreetPoint()
        {
            Assert.That(route.NorthboundDistance(SouthZ + 10f), Is.EqualTo(10f).Within(.001f));
            Assert.That(
                route.SouthboundDistance(NorthZ - 10f),
                Is.EqualTo(Straight + Turn + 10f).Within(.001f)
            );
        }

        [Test]
        public void Advance_WithoutStops_MovesByTheStep()
        {
            Assert.That(route.Advance(10f, 2f, new float[0]), Is.EqualTo(12f).Within(.001f));
        }

        [Test]
        public void Advance_StopsAtAStopPointAhead()
        {
            Assert.That(route.Advance(10f, 2f, new[] { 11f }), Is.EqualTo(11f).Within(.001f));
        }

        [Test]
        public void Advance_WaitsWhileAtAStopPoint()
        {
            Assert.That(route.Advance(11f, 2f, new[] { 11f }), Is.EqualTo(11f).Within(.001f));
        }

        [Test]
        public void Advance_IgnoresStopPointsBeyondTheStepOrBehind()
        {
            Assert.That(route.Advance(10f, 2f, new[] { 15f, 5f }), Is.EqualTo(12f).Within(.001f));
        }

        [Test]
        public void Advance_WrapsAroundTheLoop()
        {
            Assert.That(
                route.Advance(route.Length - 1f, 2f, new float[0]),
                Is.EqualTo(1f).Within(.001f)
            );
            Assert.That(
                route.Advance(route.Length - 1f, 2f, new[] { .5f }),
                Is.EqualTo(.5f).Within(.001f)
            );
        }

        [Test]
        public void SomeoneStandingAheadInTheLane_StopsTheCar()
        {
            bool stops = CarLoopRoute.ShouldYield(
                new Vector3(LaneOffset, 0f, 0f),
                Vector3.forward,
                new Vector3(LaneOffset + .5f, 0f, 3f)
            );

            Assert.That(stops, Is.True);
        }

        [Test]
        public void SomeoneBehindTheCar_DoesNotStopIt()
        {
            bool stops = CarLoopRoute.ShouldYield(
                new Vector3(LaneOffset, 0f, 0f),
                Vector3.forward,
                new Vector3(LaneOffset, 0f, -3f)
            );

            Assert.That(stops, Is.False);
        }

        [Test]
        public void SomeoneOnTheSidewalk_DoesNotStopTheCar()
        {
            bool stops = CarLoopRoute.ShouldYield(
                new Vector3(LaneOffset, 0f, 0f),
                Vector3.forward,
                new Vector3(4.5f, 0f, 3f)
            );

            Assert.That(stops, Is.False);
        }

        [Test]
        public void SomeoneFarAhead_DoesNotStopTheCarYet()
        {
            bool stops = CarLoopRoute.ShouldYield(
                new Vector3(LaneOffset, 0f, 0f),
                Vector3.forward,
                new Vector3(LaneOffset, 0f, 20f)
            );

            Assert.That(stops, Is.False);
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(
                Vector3.Distance(actual, expected),
                Is.LessThan(.001f),
                $"{actual} != {expected}"
            );
        }
    }
}
