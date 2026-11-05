using NUnit.Framework;
using UnityEngine.AI;

namespace A305.Wayfinding.Tests
{
    public sealed class TownGuideRouteTests
    {
        [Test]
        public void NpcsWalkRoadsAndCrosswalksAtTheUsualCost()
        {
            foreach (string name in new[] { GuideWalkAreas.Road, GuideWalkAreas.Crosswalk })
            {
                int area = NavMesh.GetAreaFromName(name);
                Assert.That(area, Is.GreaterThanOrEqualTo(0), $"{name} 구역 이름이 없습니다.");
                Assert.That(NavMesh.GetAreaCost(area), Is.EqualTo(1f), name);
            }
        }
    }
}
