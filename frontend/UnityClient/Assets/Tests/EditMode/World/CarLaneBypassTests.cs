using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    public class CarLaneBypassTests
    {
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
