using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.Npc;
using A305.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 생활 데이터(Data/Npc/NpcLife.json)가 있는 NPC가 놓인 장면마다 그 NPC에 생활(<see cref="NpcRoutine"/>)과 생활 동작
    /// 컨트롤러(<see cref="NpcAnimationBuilder"/>)를 붙이고, NPC가 걸을 바닥(NavMesh)을 구워 <see cref="NpcWalkArea"/>로 연결한다.
    /// 가구 배치를 바꾸면 다시 실행해 바닥을 새로 굽는다.
    /// 생활 데이터는 AI 규칙 원본에서 ai/tools/npc_life_export.py로 내보낸다.
    /// </summary>
    public static class NpcLifeBuilder
    {
        public const string MenuPath = "Tools/A305/Npc/Build Daily Life";

        private const string LifeDataPath = "Assets/Data/Npc/NpcLife.json";
        private const string WalkAreaFolder = "Assets/Data/Npc/WalkAreas";

        // NPC 몸(CharacterController 반지름 0.3m, 키 1.8m)과 같은 크기로 굽고, 보도 턱 정도(0.3m)는 오른다.
        private const float AgentRadiusMeters = .3f;
        private const float AgentHeightMeters = 1.8f;
        private const float AgentClimbMeters = Passerby.WalkableStepMeters;
        private const float AgentSlopeDegrees = 45f;

        // 본길 양옆 건물 사이 1m 틈은 통행로가 아니다. 정식 인도·출입구는 별도로 유지한다.
        private const float MinimumBuildingPassageMeters = 2f;

        // 매장 건물의 바닥 자리(건물 기준). 앞면 z=0, 뒷벽 z=-8, 폭 8m, 바닥 두께 0.2m
        private static readonly Bounds ShopBuildingFootprint = new Bounds(
            new Vector3(0, -.1f, -4),
            new Vector3(8, .2f, 8)
        );

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Production/CafeScene.unity",
            "Assets/Scenes/Production/HotelScene.unity",
            "Assets/Scenes/Production/ShopScene.unity",
            "Assets/Scenes/Production/PoliceStationScene.unity",
            "Assets/Scenes/Production/TownScene.unity",
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            var lifeData = BuilderTools.LoadRequiredAsset<TextAsset>(LifeDataPath, MenuPath);
            NpcLifeData data = NpcLifeData.Parse(lifeData.text);
            RuntimeAnimatorController animator = NpcAnimationBuilder.PrepareController();
            // 마을 매장 장면의 직원도 같은 생활을 쓴다.
            foreach (
                string scenePath in ScenePaths.Concat(
                    A305.EditorTools.World.TownShopSceneBuilder.ScenePaths()
                )
            )
            {
                BuildScene(scenePath, data, lifeData, animator);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("NPC 생활과 걸을 바닥을 준비했습니다.");
        }

        /// <summary>장면 하나의 NPC에만 생활을 붙이고 그 장면의 바닥을 굽는다. 새 장소 장면을 만든 Builder가 다른 장면은 두고 쓴다.</summary>
        public static void BuildScene(string scenePath)
        {
            var lifeData = BuilderTools.LoadRequiredAsset<TextAsset>(LifeDataPath, MenuPath);
            BuildScene(
                scenePath,
                NpcLifeData.Parse(lifeData.text),
                lifeData,
                NpcAnimationBuilder.PrepareController()
            );
            AssetDatabase.SaveAssets();
        }

        private static void BuildScene(
            string scenePath,
            NpcLifeData data,
            TextAsset lifeData,
            RuntimeAnimatorController animator
        )
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (ConnectRoutines(scene, data, lifeData, animator) == 0)
            {
                return;
            }

            RebakeWalkArea(scene);
            BuilderTools.SaveScene(scene, scenePath);
        }

        /// <summary>열린 장면의 걸을 바닥을 다시 굽고 연결한다. 바닥 구역 표시를 바꾼 뒤에도 쓴다. 장면 저장은 부르는 쪽이 한다.</summary>
        public static void RebakeWalkArea(Scene scene)
        {
            ConnectWalkArea(scene, BakeWalkArea(scene));
        }

        [MenuItem("Tools/A305/Npc/Rebake Town Walk Area")]
        public static void RebakeTownWalkArea()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            RebakeWalkArea(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("마을 NPC 보행 데이터를 다시 만들었습니다.");
        }

        // 생활 데이터가 있는 NPC에만 붙인다. 튜토리얼 행인처럼 정해진 흐름대로 움직이는 NPC는 그대로 둔다.
        private static int ConnectRoutines(
            Scene scene,
            NpcLifeData data,
            TextAsset lifeData,
            RuntimeAnimatorController animator
        )
        {
            int connected = 0;
            foreach (Passerby passerby in BuilderTools.FindAllInScene<Passerby>(scene))
            {
                if (data.FindNpc(passerby.GetComponent<NpcIdentity>().Code) == null)
                {
                    continue;
                }

                var routine = BuilderTools.GetOrAddComponent<NpcRoutine>(passerby.gameObject);
                BuilderTools.SetReference(routine, "lifeData", lifeData);
                NpcAnimationBuilder.Connect(routine, animator);
                connected++;
            }

            return connected;
        }

        private static NavMeshData BakeWalkArea(Scene scene)
        {
            var sources = new List<NavMeshBuildSource>();
            var everything = new Bounds(Vector3.zero, Vector3.one * 10000f);
            NavMeshBuilder.CollectSources(
                everything,
                ~0,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                AreaMarkups(scene),
                sources
            );
            sources.RemoveAll(source => !IsStandingGround(source.component as Collider));

            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (NavMeshBuildSource source in sources)
            {
                Bounds sourceBounds = ((Collider)source.component).bounds;
                if (hasBounds)
                {
                    bounds.Encapsulate(sourceBounds);
                }
                else
                {
                    bounds = sourceBounds;
                    hasBounds = true;
                }
            }

            sources.AddRange(AreaVolumes(scene));
            sources.AddRange(BlockedTownBuildingGaps(scene));

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = AgentRadiusMeters;
            settings.agentHeight = AgentHeightMeters;
            settings.agentClimb = AgentClimbMeters;
            settings.agentSlope = AgentSlopeDegrees;
            bounds.Expand(1f);
            NavMeshData baked = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                bounds,
                Vector3.zero,
                Quaternion.identity
            );
            return SaveWalkArea(baked, $"{WalkAreaFolder}/{scene.name}WalkArea.asset");
        }

        // 건물 자체나 물리 충돌체는 바꾸지 않고, 건물 사이 흙바닥만 길 찾기에서 제외한다.
        // 실제 바닥의 범위를 사용하므로 간판·처마가 인도 쪽으로 돌출해도 인도를 막지 않는다.
        private static IEnumerable<NavMeshBuildSource> BlockedTownBuildingGaps(Scene scene)
        {
            if (scene.name != "TownScene")
                yield break;
            Transform buildings = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Environment")
                    buildings = root.transform.Find("TownMainstreet/BackgroundBuildings");
            }
            if (buildings == null)
                yield break;

            // 매장 건물(매장 장면으로 들어가는 문이 있는 건물)은 8m 모듈이다. 실내 바닥은 매장 장면에만 있으므로
            // 건물 기준 바닥 자리(앞면 z=0~뒷벽 z=-8, 폭 8m)를 발자국으로 쓴다.
            var footprints = new List<Bounds>();
            foreach (Transform building in buildings)
            {
                if (
                    building.gameObject.activeInHierarchy
                    && building.GetComponentInChildren<SpaceDoorway>(true) != null
                )
                    footprints.Add(WorldBounds(building, ShopBuildingFootprint));
            }
            footprints.Sort((first, second) => first.min.z.CompareTo(second.min.z));
            for (int i = 0; i < footprints.Count; i++)
            for (int j = i + 1; j < footprints.Count; j++)
            {
                Bounds south = footprints[i];
                Bounds north = footprints[j];
                float gap = north.min.z - south.max.z;
                float left = Mathf.Max(south.min.x, north.min.x);
                float right = Mathf.Min(south.max.x, north.max.x);
                if (gap <= 0 || gap >= MinimumBuildingPassageMeters || right <= left)
                    continue;

                yield return new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(
                        new Vector3(
                            (left + right) * .5f,
                            south.max.y + 1f,
                            south.max.z + gap * .5f
                        ),
                        Quaternion.identity,
                        Vector3.one
                    ),
                    size = new Vector3(right - left, 4f, gap),
                    area = NavMesh.GetAreaFromName("Not Walkable"),
                };
            }
        }

        // 건물은 90도 단위로 돌아 있어 모서리 8개를 감싼 축 정렬 상자가 건물 자리와 같다.
        private static Bounds WorldBounds(Transform building, Bounds local)
        {
            var world = new Bounds(building.TransformPoint(local.center), Vector3.zero);
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                world.Encapsulate(
                    building.TransformPoint(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))
                    )
                );
            return world;
        }

        // 바닥 구역 표시(NavMeshModifier). 마을 차도처럼 붙은 사물과 그 아래 바닥을 다른 구역으로 굽는다.
        private static List<NavMeshBuildMarkup> AreaMarkups(Scene scene)
        {
            var markups = new List<NavMeshBuildMarkup>();
            foreach (
                NavMeshModifier modifier in BuilderTools.FindAllInScene<NavMeshModifier>(scene)
            )
            {
                markups.Add(
                    new NavMeshBuildMarkup
                    {
                        root = modifier.transform,
                        overrideArea = modifier.overrideArea,
                        area = modifier.area,
                        ignoreFromBuild = modifier.ignoreFromBuild,
                        applyToChildren = modifier.applyToChildren,
                    }
                );
            }

            return markups;
        }

        // 상자 안 바닥을 다른 구역으로 바꾸는 표시(NavMeshModifierVolume). 차도 위 횡단보도처럼 바닥 일부만 바꾼다.
        private static IEnumerable<NavMeshBuildSource> AreaVolumes(Scene scene)
        {
            foreach (
                NavMeshModifierVolume volume in BuilderTools.FindAllInScene<NavMeshModifierVolume>(
                    scene
                )
            )
            {
                Vector3 scale = volume.transform.lossyScale;
                yield return new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(
                        volume.transform.TransformPoint(volume.center),
                        volume.transform.rotation,
                        Vector3.one
                    ),
                    size = new Vector3(
                        volume.size.x * Mathf.Abs(scale.x),
                        volume.size.y * Mathf.Abs(scale.y),
                        volume.size.z * Mathf.Abs(scale.z)
                    ),
                    area = volume.area,
                };
            }
        }

        // 움직이는 것(사람·차)과 통과하는 영역(사물 호버·문 트리거)은 바닥이나 장애물로 굽지 않는다.
        private static bool IsStandingGround(Collider collider)
        {
            if (collider == null || collider.isTrigger)
            {
                return false;
            }

            bool moves =
                collider.GetComponentInParent<CharacterController>() != null
                || collider.GetComponentInParent<LoopingCar>() != null
                || (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic);
            return !moves;
        }

        // 다시 구울 때 같은 에셋을 덮어써 장면 참조와 GUID가 바뀌지 않게 한다.
        private static NavMeshData SaveWalkArea(NavMeshData baked, string path)
        {
            BuilderTools.EnsureFolder(WalkAreaFolder);
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(baked, path);
                return baked;
            }

            // 덮어쓰면 에셋 이름까지 바뀌므로 원래 이름을 지킨다.
            string assetName = existing.name;
            EditorUtility.CopySerialized(baked, existing);
            existing.name = assetName;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(baked);
            return existing;
        }

        private static void ConnectWalkArea(Scene scene, NavMeshData data)
        {
            var walkArea = BuilderTools.FindSingleInScene<NpcWalkArea>(scene);
            if (walkArea == null)
            {
                Transform root = BuilderTools.CreateEmpty(
                    "NpcWalkArea",
                    null,
                    Vector3.zero,
                    Quaternion.identity
                );
                walkArea = root.gameObject.AddComponent<NpcWalkArea>();
            }

            BuilderTools.SetReference(walkArea, "data", data);
        }
    }
}
