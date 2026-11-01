using System.Collections.Generic;
using A305.EditorTools.Common;
using A305.Npc;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 생활 데이터(Data/Npc/NpcLife.json)가 있는 NPC가 놓인 장면마다 그 NPC에 생활(<see cref="NpcRoutine"/>)을 붙이고, NPC가 걸을
    /// 바닥(NavMesh)을 구워 <see cref="NpcWalkArea"/>로 연결한다. 가구 배치를 바꾸면 다시 실행해 바닥을 새로 굽는다.
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
        private const float AgentClimbMeters = .3f;
        private const float AgentSlopeDegrees = 45f;

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Production/CafeScene.unity",
            "Assets/Scenes/Production/HotelScene.unity",
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
            foreach (string scenePath in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (ConnectRoutines(scene, data, lifeData) == 0)
                {
                    continue;
                }

                ConnectWalkArea(scene, BakeWalkArea(scene));
                BuilderTools.SaveScene(scene, scenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("NPC 생활과 걸을 바닥을 준비했습니다.");
        }

        // 생활 데이터가 있는 NPC에만 붙인다. 튜토리얼 행인처럼 정해진 흐름대로 움직이는 NPC는 그대로 둔다.
        private static int ConnectRoutines(Scene scene, NpcLifeData data, TextAsset lifeData)
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
                new List<NavMeshBuildMarkup>(),
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

            EditorUtility.CopySerialized(baked, existing);
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
