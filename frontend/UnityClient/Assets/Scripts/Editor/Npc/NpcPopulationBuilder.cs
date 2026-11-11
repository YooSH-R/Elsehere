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

        // 마을 매장 장면. 매장 직원은 근무 중에 이 장면 안에 있고 마을에는 출퇴근하는 모습만 나온다.
        private static string[] ShopScenePaths() =>
            A305.EditorTools.World.TownShopSceneBuilder.ScenePaths();

        private static readonly Vector3 Dorm = new Vector3(-20, .1f, 16.5f);
        private static readonly Vector3 Park = new Vector3(11, .1f, 21);

        [MenuItem("Tools/A305/Npc/Build Population And Housing")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            NpcHurryBuilder.Apply();
            BuilderTools.EnsureFolder(Folder);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(LifePath);
            var data = NpcLifeData.Parse(asset.text);
            var commuters = new Dictionary<string, string>();
            foreach (
                string scenePath in Scenes
                    .Select(name => SceneFolder + name + ".unity")
                    .Concat(ShopScenePaths())
            )
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath);
                string sceneName = scene.name;
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
                    string path = Folder + "/" + npc.Code + ".prefab";
                    // 재실행해도 기존 외형은 그대로 두고 기능 연결만 맞춘다.
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    {
                        GameObject copy = Object.Instantiate(npc.gameObject);
                        copy.name = npc.Code + "Commuter";
                        copy.SetActive(false);
                        if (copy.TryGetComponent(out NpcRoutine routine))
                            Object.DestroyImmediate(routine);
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
                // 2층 가게처럼 직원이 없는 매장 장면에는 출퇴근 관리를 두지 않는다.
                if (sceneName != "TownScene" && workers.Length > 0)
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
                residents.Add(
                    new NpcPopulation.Resident
                    {
                        Worker = worker,
                        TownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                        WorkplaceDoor = WorkplaceDoor(town, entry, worker),
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

        /// <summary>일터 장면의 직원을 그 장면의 출퇴근 관리에 이어 일과에 맞춰 켜고 끄게 한다.</summary>
        internal static void ConnectWorkers(Scene scene, NpcIdentity[] workers)
        {
            ConnectPopulation(
                scene,
                AssetDatabase.LoadAssetAtPath<TextAsset>(LifePath),
                workers
                    .Select(npc => new NpcPopulation.Resident { Worker = npc.gameObject })
                    .ToArray()
            );
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
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 출퇴근하는 모습이 들어가고 나오는 일터 문 앞(마을 문의 도착 지점). 마을 매장도 그 매장 문 앞이다.
        private static Vector3 WorkplaceDoor(Scene town, NpcLifeEntry npc, GameObject worker)
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
                return door.ArrivalPoint.position;
            }
            if (place == "TOWN")
                return worker.transform.position;
            string shopScene = PlaceScenes.SceneOf(place);
            return BuilderTools
                .FindAllInScene<SpaceDoorway>(town)
                .Single(door => door.DestinationScene == shopScene)
                .ArrivalPoint.position;
        }

        private static void PlaceDorm(Transform environment)
        {
            if (environment.Find("NpcResidence") != null)
            {
                FitDormToRoad(environment);
                return;
            }
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
            FitDormToRoad(environment);
        }

        [MenuItem("Tools/A305/Npc/Apply Road Facing Housing")]
        public static void ApplyRoadFacingHousing()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(SceneFolder + "TownScene.unity");
            PlaceDorm(BuilderTools.EnsureGroups(scene).Environment);
            var population = BuilderTools.FindSingleInScene<NpcPopulation>(scene);
            var fields = new SerializedObject(population);
            fields.FindProperty("dormDoor").vector3Value = Dorm;
            fields.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            NpcLifeBuilder.RebakeWalkArea(scene);
            BuilderTools.SaveScene(scene, scene.path);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "숙소 입구를 남쪽 도로로 돌리고 포장 충돌면·출입 위치·보행 경로를 맞췄습니다."
            );
            RenderHousingPreview();
        }

        private static void FitDormToRoad(Transform environment)
        {
            Transform residence = environment.Find("NpcResidence");
            Transform building = residence.Find("BrickResidence3F");
            building.SetPositionAndRotation(new Vector3(-20, .1f, 18), Quaternion.Euler(0, 180, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(building);
            residence.Find("NpcOnlyEntrance").position = Dorm;

            // 포장 메시는 장식만 생성하므로, 보이는 높이와 같은 실제 지지면을 별도로 만든다.
            var paving = residence.Find("ResidencePaving");
            Bounds mesh = paving.GetComponent<MeshFilter>().sharedMesh.bounds;
            var support = BuilderTools.GetOrAddComponent<BoxCollider>(paving.gameObject);
            support.center = mesh.center;
            support.size = mesh.size;

            // 기존 서쪽 경계가 숙소 앞마당을 가로지르지 않도록 포장의 바깥 둘레로 잇는다.
            Transform street = environment.Find("TownMainstreet");
            // 도로 끝 생울타리의 북쪽 끝도 앞마당 경계(14m)까지만 둔다.
            // 14m 폭 그대로면 숙소 입구까지 가려 NavMesh가 문 앞에 닿지 못한다.
            Transform streetEnd = street
                .Cast<Transform>()
                .First(child => child.name == "CrossStreetEnd14m" && child.position.x < 0);
            streetEnd.localScale = new Vector3(8f / 14f, 1, 1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(streetEnd);
            Transform boundary = street
                .Cast<Transform>()
                .First(child => child.name == "SideBoundary" && child.position.x < 0);
            FitBoundary(boundary, new Vector3(-19.6f, 3, -9), new Vector3(.5f, 6, 46));
            HousingBoundary(
                residence,
                "NorthBoundary",
                new Vector3(-19.6f, 3, 49),
                new Vector3(.5f, 6, 42)
            );
            HousingBoundary(
                residence,
                "WestBoundary",
                new Vector3(-30, 3, 21),
                new Vector3(.5f, 6, 14)
            );
            HousingBoundary(
                residence,
                "SouthBoundaryJoin",
                new Vector3(-24.8f, 3, 14),
                new Vector3(10.9f, 6, .5f)
            );
            HousingBoundary(
                residence,
                "NorthBoundaryJoin",
                new Vector3(-24.8f, 3, 28),
                new Vector3(10.9f, 6, .5f)
            );

            // 구름은 개발 예정 지역을 계속 가리되 숙소와 앞마당의 사용 영역 밖으로 물린다.
            Transform clouds = environment.Find("FutureDistrictClouds");
            if (clouds == null)
                return;
            Transform notice = clouds.Find("AirportBoundary/FloatingNotice");
            if (notice != null)
            {
                notice.position = new Vector3(-32, notice.position.y, notice.position.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(notice);
            }
            var courtyard = new Bounds(new Vector3(-22, 6, 21), new Vector3(18, 14, 18));
            foreach (Transform cloud in clouds.GetComponentsInChildren<Transform>())
            {
                if (
                    (cloud.name != "CloudBank" && cloud.name != "DistrictCloudTile")
                    || cloud.position.x >= 0
                )
                    continue;
                var renderers = cloud.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    continue;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);
                if (!bounds.Intersects(courtyard))
                    continue;
                cloud.position += Vector3.left * (bounds.max.x - courtyard.min.x + .5f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cloud);
            }
        }

        private static void HousingBoundary(
            Transform parent,
            string name,
            Vector3 center,
            Vector3 size
        )
        {
            Transform wall = parent.Find(name);
            if (wall == null)
            {
                wall = new GameObject(name).transform;
                wall.SetParent(parent, false);
            }
            FitBoundary(wall, center, size);
        }

        private static void FitBoundary(Transform wall, Vector3 center, Vector3 size)
        {
            wall.position = center;
            var collider = BuilderTools.GetOrAddComponent<BoxCollider>(wall.gameObject);
            collider.center = Vector3.zero;
            collider.size = size;
        }

        [MenuItem("Tools/A305/Npc/Render Housing Preview")]
        public static void RenderHousingPreview()
        {
            EditorSceneManager.OpenScene(SceneFolder + "TownScene.unity");
            var root = new GameObject("HousingPreviewCamera");
            var camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(-12, 16, 1);
            camera.transform.LookAt(new Vector3(-20, 4, 20));
            camera.orthographic = true;
            camera.orthographicSize = 11;
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
