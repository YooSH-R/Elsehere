using System;
using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Npc;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>배치된 직원의 외형을 재사용해 출퇴근 모습과 근무 중 출현을 연결한다.</summary>
    public static class NpcPopulationBuilder
    {
        private const string SceneFolder = "Assets/Scenes/Production/";
        private const string Folder = "Assets/Prefabs/Npc/Commuters";
        private const string LifePath = "Assets/Data/Npc/NpcLife.json";
        private static readonly string[] Scenes =
        {
            "CafeScene",
            "HotelScene",
            "ShopScene",
            "TownScene",
            "PoliceStationScene",
        };
        private static readonly Vector3 Dorm = new Vector3(-18, .1f, 21);
        private static readonly Vector3 Park = new Vector3(11, .1f, 21);

        [MenuItem("Tools/A305/Npc/Build Population And Housing")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuilderTools.EnsureFolder(Folder);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(LifePath);
            var data = NpcLifeData.Parse(asset.text);
            var commuters = new Dictionary<string, string>();
            foreach (string sceneName in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(SceneFolder + sceneName + ".unity");
                // 이 도구가 이전에 배치한 출퇴근 본보기는 런타임 생성으로 옮긴다.
                foreach (var preview in BuilderTools.FindAllInScene<NpcTownWalker>(scene))
                    Object.DestroyImmediate(preview.gameObject);
                var workers = BuilderTools
                    .FindAllInScene<NpcIdentity>(scene)
                    .Where(npc =>
                        data.FindNpc(npc.Code) != null
                        && npc.Code != "TAXI_DRIVER"
                        && npc.GetComponent<NpcTownWalker>() == null
                    )
                    .ToArray();
                foreach (NpcIdentity npc in workers)
                {
                    var presence = BuilderTools.GetOrAddComponent<NpcWorkPresence>(npc.gameObject);
                    BuilderTools.SetReference(presence, "lifeData", asset);
                    string path = Folder + "/" + npc.Code + ".prefab";
                    // 재실행해도 기존 외형은 그대로 두고 기능 연결만 맞춘다.
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    {
                        GameObject copy = Object.Instantiate(npc.gameObject);
                        copy.name = npc.Code + "Commuter";
                        copy.SetActive(false);
                        if (copy.TryGetComponent(out NpcRoutine routine))
                            Object.DestroyImmediate(routine);
                        Object.DestroyImmediate(copy.GetComponent<NpcWorkPresence>());
                        if (copy.TryGetComponent(out PoliceReportDesk desk))
                            Object.DestroyImmediate(desk);
                        copy.AddComponent<NpcTownWalker>();
                        PrefabUtility.SaveAsPrefabAsset(copy, path);
                        Object.DestroyImmediate(copy);
                    }
                    GameObject contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (contents.TryGetComponent(out PoliceReportDesk offDutyDesk))
                        {
                            Object.DestroyImmediate(offDutyDesk);
                            PrefabUtility.SaveAsPrefabAsset(contents, path);
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                    commuters[npc.Code] = path;
                }
                if (sceneName != "TownScene")
                    ConnectPopulation(
                        scene,
                        asset,
                        workers
                            .Select(npc => new NpcPopulation.Resident { Worker = npc.gameObject })
                            .ToArray()
                    );
                BuilderTools.SaveScene(scene, scene.path);
            }

            Scene town = EditorSceneManager.OpenScene(SceneFolder + "TownScene.unity");
            var groups = BuilderTools.EnsureGroups(town);
            PlaceDorm(groups.Environment);
            var townNpcs = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .Where(npc => npc.GetComponent<NpcTownWalker>() == null)
                .ToDictionary(npc => npc.Code);
            var residents = new List<NpcPopulation.Resident>();
            foreach (NpcLifeEntry entry in data.Npcs)
            {
                if (!commuters.TryGetValue(entry.NpcId, out string path))
                    continue;
                GameObject worker = townNpcs.TryGetValue(entry.NpcId, out NpcIdentity original)
                    ? original.gameObject
                    : null;
                var location = Workplace(town, entry, worker);
                residents.Add(
                    new NpcPopulation.Resident
                    {
                        Worker = worker,
                        TownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                        WorkplaceDoor = location.Door,
                        InteriorScene = location.Interior,
                    }
                );
            }
            ConnectPopulation(town, asset, residents.ToArray());
            NpcBuilder.ConnectToScene(
                groups,
                BuilderTools.FindSingleInScene<PlayerMover>(town),
                BuilderTools.FindSingleInScene<DialogueController>(town)
            );
            NpcLifeBuilder.RebakeWalkArea(town);
            BuilderTools.SaveScene(town, town.path);
            NpcCastBuilder.RefreshCommuterVisuals();
            AssetDatabase.SaveAssets();
            Debug.Log("NPC 숙소·근무 중 출현·타운 출퇴근 연결을 완료했습니다.");
        }

        private static void ConnectPopulation(
            Scene scene,
            TextAsset life,
            NpcPopulation.Resident[] residents
        )
        {
            var groups = BuilderTools.EnsureGroups(scene);
            var population = BuilderTools.FindSingleInScene<NpcPopulation>(scene);
            if (population == null)
            {
                var root = new GameObject("NpcPopulation");
                root.transform.SetParent(groups.Systems, false);
                population = root.AddComponent<NpcPopulation>();
            }
            var fields = new SerializedObject(population);
            fields.FindProperty("lifeData").objectReferenceValue = life;
            fields.FindProperty("dormDoor").vector3Value = Dorm;
            fields.FindProperty("park").vector3Value = Park;
            fields.FindProperty("townCharacters").objectReferenceValue = groups.Characters;
            SerializedProperty list = fields.FindProperty("residents");
            list.arraySize = residents.Length;
            for (int i = 0; i < residents.Length; i++)
            {
                SerializedProperty row = list.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("Worker").objectReferenceValue = residents[i].Worker;
                row.FindPropertyRelative("TownPrefab").objectReferenceValue = residents[
                    i
                ].TownPrefab;
                row.FindPropertyRelative("WorkplaceDoor").vector3Value = residents[i].WorkplaceDoor;
                row.FindPropertyRelative("InteriorScene").stringValue =
                    residents[i].InteriorScene ?? "";
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (Vector3 Door, string Interior) Workplace(
            Scene town,
            NpcLifeEntry npc,
            GameObject worker
        )
        {
            string place = npc.Workplace;
            string doorId =
                place == "CAFE" ? "CafeDoor"
                : place == "HOTEL" ? "HotelDoor"
                : "ShopDoor";
            if (place == "CAFE" || place == "HOTEL" || place == "SHOP")
            {
                var door = BuilderTools
                    .FindAllInScene<SpaceDoorway>(town)
                    .Single(d => d.DoorwayId == doorId);
                return (door.ArrivalPoint.position, null);
            }
            if (place == "TOWN")
                return (worker.transform.position, null);
            string name = place switch
            {
                "BAKERY" => "MapleBakery",
                "ARCADE" => "LittleArcade",
                "HOME_STORE" => "HomeLiving",
                _ => TownStorefrontBuilder.Storefronts.Single(shop => shop.PlaceCode == place).Name,
            };
            Transform building = BuildingStaffBuilder.Building(town, name);
            return (building.TransformPoint(new Vector3(0, 0, 1)), name + "Interior");
        }

        private static void PlaceDorm(Transform environment)
        {
            if (environment.Find("NpcResidence") != null)
                return;
            var root = new GameObject("NpcResidence");
            root.transform.SetParent(environment, false);
            var exterior = AssetDatabase.LoadAssetAtPath<GameObject>(
                TownBackgroundKit.Folder + "/BrickResidence3F.prefab"
            );
            GameObject building = (GameObject)
                PrefabUtility.InstantiatePrefab(exterior, root.transform);
            building.transform.SetPositionAndRotation(
                new Vector3(-19, .1f, 21),
                Quaternion.Euler(0, 90, 0)
            );
            // 외관 안쪽 전체를 막아 플레이어가 들어가지 못하게 한다.
            var closed = building.AddComponent<BoxCollider>();
            closed.center = new Vector3(0, 4.5f, -4);
            closed.size = new Vector3(8, 9, 8);
            MainstreetArtKit.Block(
                "ResidencePaving",
                root.transform,
                new Vector3(-22, -.05f, 21),
                new Vector3(17, .2f, 14),
                MainstreetArtKit.Cream
            );
            MainstreetArtKit.Text(
                "ResidenceSign",
                "STAFF RESIDENCE",
                building.transform,
                new Vector3(0, 2.8f, .2f),
                .3f,
                MainstreetArtKit.Navy
            );
            var entrance = new GameObject("NpcOnlyEntrance");
            entrance.transform.SetParent(root.transform, false);
            entrance.transform.position = Dorm;
        }

        [MenuItem("Tools/A305/Npc/Render Housing Preview")]
        public static void RenderHousingPreview()
        {
            EditorSceneManager.OpenScene(SceneFolder + "TownScene.unity");
            var root = new GameObject("HousingPreviewCamera");
            var camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(-36, 24, 2);
            camera.transform.LookAt(new Vector3(-14, 2, 22));
            camera.orthographic = true;
            camera.orthographicSize = 17;
            var target = new RenderTexture(1400, 1000, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            string output = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "NpcResidencePreview.png"
            );
            System.IO.File.WriteAllBytes(output, image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(root);
            Debug.Log("NPC 숙소 배치 확인 이미지: " + output);
        }
    }
}
