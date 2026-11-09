using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc.Tests
{
    /// <summary>실제 마을 보행 데이터에서 NPC가 횡단보도로 들어가는 때를 확인한다.</summary>
    public class NpcWalkRouteTests
    {
        private NavMeshDataInstance walkArea;

        [SetUp]
        public void LoadTownWalkArea()
        {
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(
                "Assets/Data/Npc/WalkAreas/TownSceneWalkArea.asset"
            );
            Assert.That(data, Is.Not.Null);
            walkArea = NavMesh.AddNavMeshData(data);
        }

        [TearDown]
        public void UnloadTownWalkArea() => walkArea.Remove();

        [Test]
        public void SidewalkWalk_PastTheCafeCrosswalkEnd_DoesNotApproachACrossing()
        {
            // 카페 앞 횡단보도 동쪽 끝은 인도 안쪽까지 들어와 있다. 인도를 따라 남쪽으로 걷는 산책은 길을 건너지 않는다.
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(4.3f, .12f, -8.55f), new Vector3(4.4f, .15f, -12f)),
                Is.True
            );

            Assert.That(
                route.TryFindCrossingAhead(.6f, out NpcWalkRoute.CrossingApproach approach),
                Is.False,
                approach.Entry.ToString()
            );
        }

        [Test]
        public void SidewalkWalk_BesideTheJunctionStreetLamp_DoesNotApproachACrossing()
        {
            // 교차로 북서쪽 가로등과 차도 사이 틈은 횡단보도 끝에 붙어 있다. 인도를 따라 북쪽으로 가는 길은 건너지 않는다.
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(-3.55f, .12f, 14.52f), new Vector3(-4.4f, .15f, 29.28f)),
                Is.True
            );

            Assert.That(
                route.TryFindCrossingAhead(.6f, out NpcWalkRoute.CrossingApproach approach),
                Is.False,
                approach.Entry.ToString()
            );
        }

        [Test]
        public void TargetAcrossTheMainRoad_ApproachesTheCrossingAcrossTheRoad()
        {
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(4.6f, .12f, -11f), new Vector3(-4.6f, .15f, -11f)),
                Is.True
            );

            Assert.That(
                route.TryFindCrossingAhead(.6f, out NpcWalkRoute.CrossingApproach approach),
                Is.True
            );
            Vector3 across = approach.Across;
            Assert.That(
                Mathf.Abs(across.x),
                Is.GreaterThan(Mathf.Abs(across.z)),
                across.ToString()
            );
        }

        [Test]
        public void WalkerInTheGapBesideTheStreetLamp_SeesTheCrossingAhead()
        {
            // 가로등과 차도 사이 틈은 차도에 붙어 있다. 여기서 횡단보도로 다가가는 NPC도 신호를 보고 횡단을 시작해야 한다.
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(-3f, .12f, 15.1f), new Vector3(3.6f, .15f, 14f)),
                Is.True
            );

            Assert.That(
                route.TryFindCrossingAhead(.6f, out NpcWalkRoute.CrossingApproach approach),
                Is.True
            );
            Vector3 across = approach.Across;
            Assert.That(
                Mathf.Abs(across.x),
                Is.GreaterThan(Mathf.Abs(across.z)),
                across.ToString()
            );
        }

        [TestCase(-3.3f, 13.18f)]
        [TestCase(-3.37f, 13.26f)]
        [TestCase(-3.4f, 13.28f)]
        [TestCase(-3.5f, 13.3f)]
        public void WalkerOnTheJunctionCornerSidewalk_SeesTheCrossingAhead(float x, float z)
        {
            // 교차로 북서쪽 모서리 인도는 횡단보도와 거의 맞닿아 있다. 인도 위에 서 있으면 아직 건너기 전이다.
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(x, .12f, z), new Vector3(3.12f, .15f, 2.05f)),
                Is.True
            );

            Assert.That(route.TryFindCrossingAhead(.6f, out _), Is.True);
        }

        [TestCase(3.66f, 6.54f)]
        [TestCase(3.4f, 6.45f)]
        [TestCase(3.2f, 6.42f)]
        [TestCase(3.49f, 6.46f)]
        [TestCase(3.3f, 6.6f)]
        [TestCase(3.15f, 6.4f)]
        public void ApproachAtTheSoutheastCorner_StepsOntoTheCrosswalk(float x, float z)
        {
            // 모서리에서는 건너는 방향이 연석과 거의 나란할 수 있다. 발을 디딜 지점은 길을 따라 횡단보도 안쪽이어야 한다.
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(x, .12f, z), new Vector3(-20, .1f, 16.5f)),
                Is.True
            );

            Assert.That(
                route.TryFindCrossingAhead(.6f, out NpcWalkRoute.CrossingApproach approach),
                Is.True
            );
            Assert.That(
                NpcWalkRoute.IsOnSidewalk(approach.StepIn),
                Is.False,
                approach.StepIn.ToString()
            );
        }

        [Test]
        public void PathAlongTheCrosswalkEdge_IsNotACrossingEntry()
        {
            // 남동쪽 모서리에서 숙소로 가는 길은 횡단보도 경계선을 따라 북쪽으로 붙어 가다가 1m쯤 앞에서 횡단보도로 들어간다.
            // 경계선 위를 지나는 구간을 횡단보도로 들어가는 것으로 보면 경계와 나란한 방향을 건너는 방향으로 잡는다.
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(3.13f, .12f, 6.07f), new Vector3(-20, .1f, 16.5f)),
                Is.True
            );

            Assert.That(
                route.TryFindCrossingAhead(.6f, out NpcWalkRoute.CrossingApproach approach),
                Is.False,
                $"{approach.Entry} → {approach.StepIn}"
            );
        }

        [Test]
        public void WalkerAlreadyOnTheCrosswalk_IsCrossingRatherThanApproaching()
        {
            var route = new NpcWalkRoute();
            Assert.That(
                route.TryPlan(new Vector3(0, .05f, 13.15f), new Vector3(3.6f, .15f, 14f)),
                Is.True
            );

            Assert.That(route.TryFindCrossingAhead(.6f, out _), Is.False);
        }

        [Test]
        public void CafeSidewalkStroll_StaysOffTheCrosswalk()
        {
            Vector3[] route = NpcWalkRoute.Between(
                new Vector3(4.4f, .1f, -12f),
                new Vector3(4.4f, .1f, -6f)
            );

            Assert.That(route, Is.Not.Null);
            int crosswalk = 1 << NavMesh.GetAreaFromName("Crosswalk");
            for (int i = 1; i < route.Length; i++)
            for (float t = 0; t <= 1; t += .05f)
            {
                Vector3 point = Vector3.Lerp(route[i - 1], route[i], t);
                Assert.That(
                    NavMesh.SamplePosition(point, out _, .1f, crosswalk),
                    Is.False,
                    $"산책 경로 {point}가 횡단보도를 밟습니다."
                );
            }
        }
    }
}
