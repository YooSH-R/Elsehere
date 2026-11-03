using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.Wayfinding;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Wayfinding
{
    /// <summary>
    /// 길 안내를 마을에 붙인다. 바닥 구역 이름(Road·Crosswalk)을 프로젝트 설정에 두고, TownScene의 차도에 Road 표시를,
    /// 횡단보도 줄무늬 자리에 Crosswalk 상자를 둔 뒤 걸을 바닥을 다시 굽는다. 길 안내 설정 에셋은 없을 때만 만든다.
    /// 차도나 횡단보도를 옮기면 다시 실행한다.
    /// </summary>
    public static class WayfindingBuilder
    {
        public const string MenuPath = "Tools/A305/Wayfinding/Build";
        internal const string SettingsPath =
            "Assets/Data/Wayfinding/DestinationGuideSettings.asset";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string StreetsPath = "TownMainstreet/StreetBase";
        private const string CrosswalkGroupName = "CrosswalkAreas";

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
            BuilderTools.LoadOrCreateAsset<DestinationGuideSettings>(SettingsPath);

            Scene scene = EditorSceneManager.OpenScene(TownScenePath);
            Transform environment = EnvironmentRoot(scene);
            MarkRoads(environment.Find(StreetsPath), road);
            MarkCrosswalks(scene, environment, crosswalk);
            NpcLifeBuilder.RebakeWalkArea(scene);
            BuilderTools.SaveScene(scene, TownScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("길 안내를 마을에 붙였습니다.");
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
        private static void MarkCrosswalks(Scene scene, Transform environment, int crosswalk)
        {
            Transform old = environment.Find(CrosswalkGroupName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            Transform group = BuilderTools.CreateEmpty(
                CrosswalkGroupName,
                environment,
                Vector3.zero,
                Quaternion.identity
            );
            List<Bounds> crossings = Crossings(scene);
            for (int i = 0; i < crossings.Count; i++)
            {
                Bounds crossing = crossings[i];
                Transform box = BuilderTools.CreateEmpty(
                    $"Crosswalk{i + 1}",
                    group,
                    new Vector3(crossing.center.x, 0f, crossing.center.z),
                    Quaternion.identity
                );
                var volume = box.gameObject.AddComponent<NavMeshModifierVolume>();
                volume.area = crosswalk;
                volume.center = Vector3.zero;
                volume.size = new Vector3(
                    crossing.size.x + CrosswalkMarginMeters * 2f,
                    CrosswalkHeightMeters,
                    crossing.size.z + CrosswalkMarginMeters * 2f
                );
            }

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
