using System;
using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Mission;
using A305.EditorTools.Npc;
using A305.Player;
using A305.Wayfinding;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Wayfinding
{
    /// <summary>
    /// 길 안내를 마을에 붙인다. 바닥 구역 이름(Road·Crosswalk)을 프로젝트 설정에 두고, TownScene의 차도에 Road 표시를,
    /// 횡단보도 줄무늬 자리에 Crosswalk 상자를 둔 뒤 걸을 바닥을 다시 굽는다. 길 안내 프리팹을 마을 Systems에 두어 플레이어·대화와 잇고,
    /// 크게 본 지도에 도로 위 길 안내 켜기·끄기 버튼을 둔다. 설정 에셋과 빛 그림·재질은 없을 때만 만든다.
    /// 차도나 횡단보도를 옮기면 다시 실행한다.
    /// </summary>
    public static class WayfindingBuilder
    {
        public const string MenuPath = "Tools/A305/Wayfinding/Build";
        internal const string SettingsPath =
            "Assets/Data/Wayfinding/DestinationGuideSettings.asset";
        private const string PrefabPath = "Assets/Prefabs/Wayfinding/DestinationGuide.prefab";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string StreetsPath = "TownMainstreet/StreetBase";

        // 구역 상자 이름은 사물 단어 자동 연결(Crosswalk·Zebra로 시작하면 '횡단보도')에 걸리지 않게 짓는다.
        private const string CrossingGroupName = "PedestrianCrossings";
        private const string CrossingName = "PedestrianCrossing";

        // 프로젝트 설정의 비어 있는 구역 번호. 횡단보도 상자가 차도 위에서 이기도록 Crosswalk가 Road보다 큰 번호를 쓴다.
        private const int RoadAreaIndex = 3;
        private const int CrosswalkAreaIndex = 4;

        // 한 횡단보도의 줄무늬 사이(0.45m)는 이어 보고, 교차로의 다른 횡단보도와의 틈(0.75m)은 나눈다.
        private const float StripeGapMeters = .5f;

        // 줄무늬 끝에서 연석까지 남은 차도(약 0.5m)까지 횡단보도로 덮는다.
        private const float CrosswalkMarginMeters = .6f;
        private const float CrosswalkHeightMeters = 1f;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            int road = EnsureArea(GuideWalkAreas.Road, RoadAreaIndex);
            int crosswalk = EnsureArea(GuideWalkAreas.Crosswalk, CrosswalkAreaIndex);
            var settings = BuilderTools.LoadOrCreateAsset<DestinationGuideSettings>(SettingsPath);
            // 새로 더한 항목을 기본값으로 적어 둔다. 이미 있는 수치는 그대로다.
            EditorUtility.SetDirty(settings);
            GameObject prefab = BuilderTools.UpdateOrCreatePrefab(
                PrefabPath,
                "DestinationGuide",
                root => SetUpGuide(root, settings)
            );

            Scene scene = EditorSceneManager.OpenScene(TownScenePath);
            Transform environment = EnvironmentRoot(scene);
            MarkRoads(environment.Find(StreetsPath), road);
            MarkCrosswalks(scene, environment, crosswalk);
            PlaceGuide(scene, prefab);
            NpcLifeBuilder.RebakeWalkArea(scene);
            BuilderTools.SaveScene(scene, TownScenePath);
            FirstDayMissionBuilder.ApplyRouteToggle();
            AssetDatabase.SaveAssets();
            Debug.Log("길 안내를 마을에 붙였습니다.");
        }

        // 기능 구성(도로 위 길, 출입문 빛 묶음과 참조)만 맞추고, 빛 모양을 두는 Visual 아래는 없을 때만 만든다.
        private static void SetUpGuide(GameObject root, DestinationGuideSettings settings)
        {
            var guide = BuilderTools.GetOrAddComponent<DestinationGuide>(root);

            Transform routeObject = root.transform.Find("Route");
            if (routeObject == null)
                routeObject = BuilderTools.CreateEmpty(
                    "Route",
                    root.transform,
                    Vector3.zero,
                    Quaternion.Euler(90f, 0f, 0f)
                );
            var route = BuilderTools.GetOrAddComponent<LineRenderer>(routeObject.gameObject);
            // 길은 땅에 눕힌다: Transform의 Z가 아래를 보고 선은 그 면에 펼친다. 무늬는 길 1m마다 한 칸 되풀이한다.
            route.useWorldSpace = true;
            route.alignment = LineAlignment.TransformZ;
            route.textureMode = LineTextureMode.Tile;
            route.numCornerVertices = 4;
            route.numCapVertices = 4;
            route.positionCount = 0;
            route.widthMultiplier = settings.RouteWidthMeters;
            route.shadowCastingMode = ShadowCastingMode.Off;
            route.receiveShadows = false;
            route.sharedMaterial = DestinationGuideArt.RouteMaterial();

            Transform glow = root.transform.Find("EntranceGlow");
            if (glow == null)
                glow = BuilderTools.CreateEmpty(
                    "EntranceGlow",
                    root.transform,
                    Vector3.zero,
                    Quaternion.identity
                );
            Transform visual = glow.Find("Visual");
            if (visual == null)
                visual = BuildGlowVisual(glow);
            var pool = visual.Find("Pool").GetComponent<Renderer>();
            var beam = visual.Find("Beam").GetComponent<Renderer>();
            // 모양은 그대로 두고 재질이 빠졌을 때만 기본 빛 재질을 채운다.
            if (pool.sharedMaterial == null)
                pool.sharedMaterial = DestinationGuideArt.PoolMaterial();
            if (beam.sharedMaterial == null)
                beam.sharedMaterial = DestinationGuideArt.BeamMaterial();

            BuilderTools.SetReference(guide, "settings", settings);
            BuilderTools.SetReference(guide, "route", route);
            BuilderTools.SetReference(guide, "entranceGlow", glow);
            BuilderTools.SetReference(guide, "pool", pool);
            BuilderTools.SetReference(guide, "beam", beam);
        }

        // 출입문 앞 바닥에 눕힌 빛 원(지름 1.8m)과 멀리서도 보이는 빛기둥(폭 1.4m, 높이 6m). 충돌 없이 보이기만 한다.
        private static Transform BuildGlowVisual(Transform glow)
        {
            Transform visual = BuilderTools.CreateEmpty(
                "Visual",
                glow,
                Vector3.zero,
                Quaternion.identity
            );
            Transform pool = BuilderTools.CreateShape(
                PrimitiveType.Quad,
                "Pool",
                visual,
                Vector3.zero,
                new Vector3(1.8f, 1.8f, 1f),
                DestinationGuideArt.PoolMaterial(),
                keepCollider: false
            );
            pool.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Transform beam = BuilderTools.CreateShape(
                PrimitiveType.Quad,
                "Beam",
                visual,
                new Vector3(0f, 3f, 0f),
                new Vector3(1.4f, 6f, 1f),
                DestinationGuideArt.BeamMaterial(),
                keepCollider: false
            );
            foreach (Transform shape in new[] { pool, beam })
            {
                var renderer = shape.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return visual;
        }

        // 마을 Systems 아래에 하나만 두고 그 씬의 플레이어·대화와 잇는다.
        private static void PlaceGuide(Scene scene, GameObject prefab)
        {
            var guide = BuilderTools.FindSingleInScene<DestinationGuide>(scene);
            if (guide == null)
            {
                SceneGroups groups = BuilderTools.EnsureGroups(scene);
                guide = BuilderTools
                    .InstantiatePrefab(prefab, groups.Systems, Vector3.zero, Quaternion.identity)
                    .GetComponent<DestinationGuide>();
            }

            BuilderTools.SetReference(
                guide,
                "player",
                BuilderTools.FindSingleInScene<PlayerMover>(scene)
            );
            BuilderTools.SetReference(
                guide,
                "dialogue",
                BuilderTools.FindSingleInScene<DialogueController>(scene)
            );
            BuilderTools.RequireReferences(guide);
        }

        // 구역 이름을 프로젝트 설정(NavMeshAreas)에 둔다. 비용은 보통 바닥과 같은 1이라 NPC 걸음은 그대로다.
        private static int EnsureArea(string name, int index)
        {
            int existing = NavMesh.GetAreaFromName(name);
            if (existing >= 0)
                return existing;
            var settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]
            );
            SerializedProperty area = settings.FindProperty("areas").GetArrayElementAtIndex(index);
            SerializedProperty areaName = area.FindPropertyRelative("name");
            if (!string.IsNullOrEmpty(areaName.stringValue))
                throw new InvalidOperationException(
                    $"NavMesh 구역 {index}번을 이미 {areaName.stringValue}가 씁니다."
                );
            areaName.stringValue = name;
            area.FindPropertyRelative("cost").floatValue = 1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return index;
        }

        // 차가 다니는 바닥: 큰길(Road_*)과 교차로의 동서 길(CrossRoadWing)
        private static void MarkRoads(Transform streets, int road)
        {
            foreach (Transform child in streets)
            {
                if (!child.name.StartsWith("Road_") && child.name != "CrossRoadWing")
                    continue;
                var modifier = BuilderTools.GetOrAddComponent<NavMeshModifier>(child.gameObject);
                modifier.overrideArea = true;
                modifier.area = road;
                modifier.applyToChildren = true;
                EditorUtility.SetDirty(modifier);
            }
        }

        // 횡단보도마다 Crosswalk 상자를 새로 둔다. 상자는 차도 위 바닥만 바꾸며 충돌이나 화면 표시는 없다.
        // 다시 실행해도 씬이 바뀌지 않게 이미 둔 상자는 그대로 쓰고, 횡단보도가 줄었으면 남는 상자를 지운다.
        private static void MarkCrosswalks(Scene scene, Transform environment, int crosswalk)
        {
            Transform group = environment.Find(CrossingGroupName);
            if (group == null)
                group = BuilderTools.CreateEmpty(
                    CrossingGroupName,
                    environment,
                    Vector3.zero,
                    Quaternion.identity
                );
            List<Bounds> crossings = Crossings(scene);
            for (int i = 0; i < crossings.Count; i++)
            {
                string name = CrossingName + (i + 1);
                Transform box = group.Find(name);
                if (box == null)
                    box = BuilderTools.CreateEmpty(name, group, Vector3.zero, Quaternion.identity);
                Bounds crossing = crossings[i];
                box.localPosition = new Vector3(crossing.center.x, 0f, crossing.center.z);
                var volume = BuilderTools.GetOrAddComponent<NavMeshModifierVolume>(box.gameObject);
                volume.area = crosswalk;
                volume.center = Vector3.zero;
                volume.size = new Vector3(
                    crossing.size.x + CrosswalkMarginMeters * 2f,
                    CrosswalkHeightMeters,
                    crossing.size.z + CrosswalkMarginMeters * 2f
                );
                EditorUtility.SetDirty(volume);
            }

            for (int i = group.childCount - 1; i >= crossings.Count; i--)
                Object.DestroyImmediate(group.GetChild(i).gameObject);
            Debug.Log($"횡단보도 {crossings.Count}곳에 Crosswalk 구역을 두었습니다.");
        }

        // 가까운 횡단보도 줄무늬끼리 묶어 횡단보도 하나의 범위로 본다.
        private static List<Bounds> Crossings(Scene scene)
        {
            var crossings = new List<Bounds>();
            IEnumerable<Renderer> stripes = BuilderTools
                .FindAllInScene<Renderer>(scene)
                .Where(renderer =>
                    renderer.name == "CrosswalkStripe" && renderer.gameObject.activeInHierarchy
                );
            foreach (Renderer stripe in stripes)
            {
                Bounds merged = stripe.bounds;
                Bounds reach = stripe.bounds;
                reach.Expand(StripeGapMeters * 2f);
                for (int i = crossings.Count - 1; i >= 0; i--)
                {
                    if (!crossings[i].Intersects(reach))
                        continue;
                    merged.Encapsulate(crossings[i]);
                    crossings.RemoveAt(i);
                }

                crossings.Add(merged);
            }

            return crossings;
        }

        private static Transform EnvironmentRoot(Scene scene) =>
            scene.GetRootGameObjects().Single(root => root.name == "Environment").transform;
    }
}
