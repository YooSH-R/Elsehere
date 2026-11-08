using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc.Tests
{
    public class TownNpcWalkAreaTests
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
        public void BuildingGaps_AreNotPedestrianShortcuts()
        {
            foreach (
                Vector3 gap in new[] { new Vector3(-11, .1f, 25.5f), new Vector3(11, .1f, 43.5f) }
            )
                Assert.That(
                    NavMesh.SamplePosition(gap, out _, .4f, NavMesh.AllAreas),
                    Is.False,
                    $"건물 사이 좁은 틈 {gap}을 보행 경로로 사용합니다."
                );
        }

        [Test]
        public void Residence_ReachesSidewalkEntrancesAndPark()
        {
            int areas = NavMesh.AllAreas & ~(1 << NavMesh.GetAreaFromName("Road"));
            Assert.That(
                NavMesh.SamplePosition(new Vector3(-20, .1f, 16.5f), out var start, 2, areas),
                Is.True
            );
            foreach (
                Vector3 destination in new[]
                {
                    new Vector3(-6, .1f, 21), // 베이커리
                    new Vector3(-6, .1f, 48), // 은행
                    new Vector3(6, .1f, 57), // 맞은편 이발소
                    new Vector3(12, .1f, 22), // 공원
                    new Vector3(5.95f, .1f, -5), // 호텔
                }
            )
            {
                Assert.That(
                    NavMesh.SamplePosition(destination, out var end, 2, areas),
                    Is.True,
                    destination.ToString()
                );
                var path = new NavMeshPath();
                Assert.That(
                    NavMesh.CalculatePath(start.position, end.position, areas, path),
                    Is.True
                );
                Assert.That(
                    path.status,
                    Is.EqualTo(NavMeshPathStatus.PathComplete),
                    destination.ToString()
                );
            }
        }
    }
}
