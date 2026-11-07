using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    public class CarLaneBypassTests
    {
        [Test]
        public void CurvedApproach_YieldsOnItsPathButNotToPeopleOnTheDistantSidewalk()
        {
            var route = new CarLoopRoute(-25, 66, 1.5f);
            float distance = route.SouthboundDistance(-13.52f);
            Vector3 sidewalk = new Vector3(4.87f, .12f, -16.51f);
            Assert.That(CarLaneBypass.ShouldYield(route, distance, 1.9f, sidewalk), Is.False);
            Vector3 onPath = CarLaneBypass.PoseAt(route, distance + 3, 1.9f).Position;
            Assert.That(CarLaneBypass.ShouldYield(route, distance, 1.9f, onPath), Is.True);
            Vector3 behind = CarLaneBypass.PoseAt(route, distance - 3, 1.9f).Position;
            Assert.That(CarLaneBypass.ShouldYield(route, distance, 1.9f, behind), Is.False);
        }

        [Test]
        public void SouthboundTaxi_ClearsTheParkedTruck_AndReturnsBeforeTheUTurn()
        {
            var route = new CarLoopRoute(-25, 66, 1.5f);
            var truck = new Bounds(new Vector3(-1.7f, 1, -18.8f), new Vector3(1.9f, 2, 4.1f));
            for (float z = -12; z >= -25; z -= .1f)
            {
                var pose = CarLaneBypass.PoseAt(route, route.SouthboundDistance(z), 1.9f);
                var body = new Bounds(pose.Position + Vector3.up, new Vector3(1.84f, 1.9f, 4.1f));
                Assert.That(body.Intersects(truck), Is.False, $"Truck overlap at z={z}");
                Assert.That(
                    Mathf.Abs(pose.Position.x) + .92f,
                    Is.LessThan(3),
                    "도로 밖으로 나가면 안 된다."
                );
            }
            var end = CarLaneBypass.PoseAt(route, route.SouthboundDistance(-25), 1.9f);
            Assert.That(end.Position.x, Is.EqualTo(-1.5f).Within(.001));
            Assert.That(end.Forward.z, Is.LessThan(-.99));
        }

        // 비켜 가는 차는 맞은편 차로까지 넘어가 마주 오는 차와 겹치므로, 비켜 가는 곳은 모두 한 대씩 구간이어야 한다.
        [Test]
        public void EverySpotWhereTheTaxiSwervesAroundTheTruck_IsInsideTheSingleLaneSection()
        {
            var route = new CarLoopRoute(-25, 66, 1.5f);
            for (float distance = 0; distance < route.Length; distance += .1f)
            {
                Vector3 swerved = CarLaneBypass.PoseAt(route, distance, 1.9f).Position;
                if (Vector3.Distance(swerved, route.PoseAt(distance).Position) < .001f)
                    continue;
                Assert.That(
                    CarLaneBypass.IsInSingleLane(route, distance),
                    Is.True,
                    $"z={swerved.z}에서 비켜 가는데 한 대씩 구간이 아닙니다."
                );
            }
        }

        [Test]
        public void SingleLaneSection_CoversBothLanesBesideTheTruckAndTheSouthUTurn()
        {
            var route = new CarLoopRoute(-25, 66, 1.5f);

            Assert.That(
                CarLaneBypass.IsInSingleLane(route, route.SouthboundDistance(-13)),
                Is.True
            );
            Assert.That(
                CarLaneBypass.IsInSingleLane(route, route.Length - 1),
                Is.True,
                "남쪽 유턴"
            );
            Assert.That(
                CarLaneBypass.IsInSingleLane(route, route.NorthboundDistance(-19)),
                Is.True,
                "트럭 맞은편 북행 차로"
            );
            Assert.That(
                CarLaneBypass.IsInSingleLane(route, route.SouthboundDistance(-11)),
                Is.False
            );
            Assert.That(
                CarLaneBypass.IsInSingleLane(route, route.NorthboundDistance(-11)),
                Is.False
            );
            Assert.That(
                CarLaneBypass.SingleLaneEntry(route),
                Is.EqualTo(route.SouthboundDistance(-12)).Within(.001)
            );
        }

        [Test]
        public void NorthboundTraffic_IsUnchanged()
        {
            var route = new CarLoopRoute(-25, 66, 1.5f);
            float distance = route.NorthboundDistance(-19);
            Assert.That(
                CarLaneBypass.PoseAt(route, distance, 1.9f).Position,
                Is.EqualTo(route.PoseAt(distance).Position)
            );
        }
    }
}
