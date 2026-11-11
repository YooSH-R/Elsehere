using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Npc;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Npc
{
    public static class NpcOutingBuilder
    {
        [MenuItem("Tools/A305/Npc/Apply Leisure Outings")]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            var town = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            var venues = new List<NpcOutingVenue>();
            // 들어갈 수 있는 시설(마을 매장·카페·드럭스토어)은 마을 문 앞(씬 전환 도착 지점)에서 들어간다.
            // 한 건물의 2층 가게는 1층 매장과 코드를 함께 쓰므로 그 코드의 장면인 1층 매장 문만 목적지로 둔다.
            foreach (SpaceDoorway door in BuilderTools.FindAllInScene<SpaceDoorway>(town))
            {
                string code = PlaceScenes.CodeOf(door.DestinationScene);
                bool facility =
                    PlaceScenes.IsTownShop(door.DestinationScene) || code is "CAFE" or "SHOP";
                if (!facility || PlaceScenes.SceneOf(code) != door.DestinationScene)
                    continue;
                venues.Add(
                    new NpcOutingVenue
                    {
                        Code = code,
                        Scene = door.DestinationScene,
                        Entrance = door.ArrivalPoint.position,
                        Capacity = 2,
                    }
                );
            }
            // 건물 사이 틈 대신 양쪽 큰길 보도에 산책 구간을 둔다.
            foreach (float x in new[] { -4.4f, 4.4f })
            foreach (float z in new[] { -12f, 24f, 42f })
                venues.Add(
                    new NpcOutingVenue
                    {
                        Code = $"WALK_{x}_{z}",
                        Scene = "TownScene",
                        IsOutdoor = true,
                        Entrance = new Vector3(x, .1f, z),
                        WalkTo = new Vector3(x, .1f, z + 6),
                        Capacity = 2,
                    }
                );
            RuntimeAnimatorController animator = null;
            // 시설 안에서는 마을 문 안쪽 도착 지점에서 나타나고 그곳으로 걸어 나간다.
            foreach (NpcOutingVenue venue in venues.Where(item => !item.IsOutdoor))
            {
                var scene = EditorSceneManager.OpenScene(
                    EditorBuildSettings
                        .scenes.Single(entry =>
                            System.IO.Path.GetFileNameWithoutExtension(entry.path) == venue.Scene
                        )
                        .path
                );
                var exit = BuilderTools
                    .FindAllInScene<SpaceDoorway>(scene)
                    .Single(item => item.DestinationScene == "TownScene");
                venue.Inside = exit.ArrivalPoint.position;
                var routine = BuilderTools.FindAllInScene<NpcRoutine>(scene).FirstOrDefault();
                if (routine != null)
                    animator = (RuntimeAnimatorController)
                        new SerializedObject(routine)
                            .FindProperty("lifeAnimator")
                            .objectReferenceValue;
            }
            BuilderTools.EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/NpcOutings.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NpcOutingCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<NpcOutingCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            catalog.LifeData = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Data/Npc/NpcLife.json"
            );
            catalog.LifeAnimator = animator;
            catalog.Venues = venues.ToArray();
            catalog.Visitors = AssetDatabase
                .FindAssets("t:Prefab", new[] { "Assets/Prefabs/Npc/Commuters" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"여가 목적지 {venues.Count}곳과 방문 외형 {catalog.Visitors.Length}개를 연결했습니다."
            );
        }
    }
}
