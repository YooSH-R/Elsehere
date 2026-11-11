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
            Transform buildings = town.GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownMainstreet/BackgroundBuildings");
            foreach (Transform building in buildings)
            {
                string sceneName = building.name + "Interior";
                if (
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        "Assets/Scenes/ShopInteriors/" + sceneName + ".unity"
                    ) == null
                )
                    continue;
                venues.Add(
                    new NpcOutingVenue
                    {
                        Code = PlaceScenes.CodeOf(sceneName),
                        Scene = sceneName,
                        Entrance = building.TransformPoint(new Vector3(0, 0, 1)),
                        Inside = building.TransformPoint(new Vector3(0, 0, -1.4f)),
                        Capacity = 2,
                    }
                );
            }
            var outside = BuilderTools.FindAllInScene<SpaceDoorway>(town).ToArray();
            foreach (string code in new[] { "CAFE", "SHOP" })
            {
                string sceneName = code == "CAFE" ? "CafeScene" : "ShopScene";
                SpaceDoorway door = outside.Single(item => item.DestinationScene == sceneName);
                venues.Add(
                    new NpcOutingVenue
                    {
                        Code = code,
                        Scene = sceneName,
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
            foreach (string code in new[] { "CAFE", "SHOP" })
            {
                var venue = venues.Single(item => item.Code == code);
                var scene = EditorSceneManager.OpenScene(
                    "Assets/Scenes/Production/" + venue.Scene + ".unity"
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
