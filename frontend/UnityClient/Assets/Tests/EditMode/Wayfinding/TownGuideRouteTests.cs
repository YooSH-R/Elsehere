using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.World;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace A305.Wayfinding.Tests
{
    /// <summary>
    /// 마을 바닥(NavMesh)은 차도를 Road, 횡단보도를 Crosswalk 구역으로 나눠 굽는다. 길 안내는 차도에 큰 비용을 매겨
    /// 차도를 건너야 할 때 횡단보도로 돌아가고, NPC는 두 구역 모두 보통 바닥과 같은 비용으로 걷는다.
    /// </summary>
    public sealed class TownGuideRouteTests
    {
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string WalkAreaPath = "Assets/Data/Npc/WalkAreas/TownSceneWalkArea.asset";
        private const string SettingsPath = "Assets/Data/Wayfinding/DestinationGuideSettings.asset";

        // 길 위 점을 이 간격으로 짚어 그 자리 바닥 구역을 본다.
        private const float CheckSpacingMeters = .25f;

        // 문 앞 도착 지점 말고 플레이어가 서 있을 만한 먼 인도: 북쪽 양쪽 인도와 동서 길 서쪽 인도
        private static readonly Vector3[] FarSidewalks =
        {
            new Vector3(-5f, .1f, 45f),
            new Vector3(5f, .1f, 45f),
            new Vector3(-12f, .1f, 5f),
        };

        private NavMeshDataInstance walkArea;

        [TearDown]
        public void RemoveWalkArea()
        {
            if (walkArea.valid)
                walkArea.Remove();
        }

        [Test]
        public void EveryCarRoadIsMarkedAsRoad()
        {
            Scene scene = EditorSceneManager.OpenScene(TownScenePath);
            int road = NavMesh.GetAreaFromName(GuideWalkAreas.Road);
            Assert.That(road, Is.GreaterThanOrEqualTo(0), "Road 구역 이름이 없습니다.");

            Transform streets = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownMainstreet/StreetBase");
            Transform[] roads = streets
                .Cast<Transform>()
                .Where(child => child.name.StartsWith("Road_") || child.name == "CrossRoadWing")
                .ToArray();
            Assert.That(roads, Is.Not.Empty);
            foreach (Transform carRoad in roads)
            {
                var modifier = carRoad.GetComponent<NavMeshModifier>();
                Assert.That(modifier, Is.Not.Null, $"{carRoad.name}에 구역 표시가 없습니다.");
                Assert.That(modifier.overrideArea && modifier.area == road, Is.True, carRoad.name);
            }
        }

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

        [Test]
        public void GuideRouteCrossesCarRoadsOnlyAtCrosswalks()
        {
            Scene scene = EditorSceneManager.OpenScene(TownScenePath);
            var settings = AssetDatabase.LoadAssetAtPath<DestinationGuideSettings>(SettingsPath);
            Assert.That(settings, Is.Not.Null, "길 안내 설정 에셋이 없습니다.");
            int road = NavMesh.GetAreaFromName(GuideWalkAreas.Road);
            Assert.That(road, Is.GreaterThanOrEqualTo(0), "Road 구역 이름이 없습니다.");
            walkArea = NavMesh.AddNavMeshData(
                AssetDatabase.LoadAssetAtPath<NavMeshData>(WalkAreaPath)
            );
            NavMeshQueryFilter filter = GuideWalkAreas.Filter(settings.RoadCrossingCost);

            Vector3[] doors = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Select(door => door.ArrivalPoint.position)
                .ToArray();
            var starts = doors.Concat(FarSidewalks).ToArray();
            foreach (Vector3 from in starts)
            foreach (Vector3 to in doors)
            {
                if (from == to)
                    continue;
                List<Vector3> route = FindRoute(from, to, filter);
                foreach (Vector3 point in Along(route))
                    Assert.That(
                        IsInsideRoad(point, road),
                        Is.False,
                        $"{from}→{to} 길이 {point}에서 횡단보도 밖 차도를 건넙니다."
                    );
            }
        }

        private static List<Vector3> FindRoute(Vector3 from, Vector3 to, NavMeshQueryFilter filter)
        {
            Assert.That(NavMesh.SamplePosition(from, out NavMeshHit start, 1f, filter), Is.True);
            Assert.That(NavMesh.SamplePosition(to, out NavMeshHit end, 1f, filter), Is.True);
            var path = new NavMeshPath();
            NavMesh.CalculatePath(start.position, end.position, filter, path);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), $"{from}→{to}");
            return path.corners.ToList();
        }

        // 길은 구역 경계(연석 선)를 따라 붙어 지나갈 수 있다. 경계에서 차도 안쪽으로 EdgeMeters 넘게 들어갔을 때만 차도로 본다.
        private static bool IsInsideRoad(Vector3 point, int road)
        {
            foreach (Vector3 nudge in EdgeNudges)
            {
                if (!OnRoad(point + nudge, road))
                    return false;
            }

            return true;
        }

        private const float EdgeMeters = .1f;

        private static readonly Vector3[] EdgeNudges =
        {
            Vector3.zero,
            new Vector3(EdgeMeters, 0, 0),
            new Vector3(-EdgeMeters, 0, 0),
            new Vector3(0, 0, EdgeMeters),
            new Vector3(0, 0, -EdgeMeters),
        };

        private static bool OnRoad(Vector3 point, int road)
        {
            Assert.That(
                NavMesh.SamplePosition(point, out NavMeshHit hit, .3f, NavMesh.AllAreas),
                Is.True,
                $"길 위 {point}가 바닥 밖입니다."
            );
            return (hit.mask & (1 << road)) != 0;
        }

        private static IEnumerable<Vector3> Along(List<Vector3> route)
        {
            var points = new List<Vector3>();
            GuidePolyline.Resample(route, CheckSpacingMeters, points);
            return points;
        }
    }
}
