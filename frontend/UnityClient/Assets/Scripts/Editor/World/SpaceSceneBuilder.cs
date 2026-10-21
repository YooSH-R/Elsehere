using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 방(HomeScene)·마을(TownScene)·카페(CafeScene)·숙소(HotelScene) 배포용 씬과 공간·출입구 프리팹을 만들고
    /// Scene List를 방부터 이 순서로 정리한다. 도형으로 만든 대체 공간이며, 디자인 에셋이 정해지면 공간 프리팹 안을 교체한다.
    /// 씬·프리팹·머티리얼은 덮어쓴다. 플레이어 프리팹은 Player Builder로 먼저 만든다.
    /// </summary>
    public static class SpaceSceneBuilder
    {
        // 씬 이름은 ERD scenes.unity_key 값으로 쓴다.
        private const string HomeScene = "HomeScene";
        private const string TownScene = "TownScene";
        private const string CafeScene = "CafeScene";
        private const string HotelScene = "HotelScene";

        // 출입구 이름은 이어지는 공간 + Door로 짓는다.
        private const string HomeDoor = "HomeDoor";
        private const string TownDoor = "TownDoor";
        private const string CafeDoor = "CafeDoor";
        private const string HotelDoor = "HotelDoor";

        private const string SceneFolder = "Assets/Scenes/Production";
        private const string DoorwayPrefabPath = "Assets/Prefabs/World/SpaceDoorway.prefab";

        // 마을 도로. 남북 큰길은 x -4~4, 동서 길은 z 6~14
        private const float RoadHalfWidth = 4f;
        private const float SidewalkWidth = 1.5f;
        private const float CrossRoadCenterZ = 10f;
        private const float TownHalfSize = 32f;

        private static readonly Color DoorColor = new Color(0.45f, 0.3f, 0.2f);
        private static readonly Color SignBoardColor = new Color(0.15f, 0.17f, 0.2f);

        [MenuItem("Tools/A305/World/Build Spaces")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            var playerPrefab = BuilderTools.LoadRequiredAsset<GameObject>(
                PlayerSandboxBuilder.PlayerPrefabPath,
                PlayerSandboxBuilder.MenuPath
            );
            var cameraPrefab = BuilderTools.LoadRequiredAsset<GameObject>(
                PlayerSandboxBuilder.CameraPrefabPath,
                PlayerSandboxBuilder.MenuPath
            );
            var players = new PlayerPrefabs(playerPrefab, cameraPrefab);
            GameObject doorwayPrefab = BuildDoorwayPrefab();

            string[] scenePaths =
            {
                BuildSpaceScene(
                    HomeScene,
                    players,
                    new Pose(new Vector3(0f, 0f, -1.5f), Quaternion.identity),
                    new PrefabPlacement(BuildHomeRoom(doorwayPrefab), Vector3.zero, 0f)
                ),
                BuildSpaceScene(
                    TownScene,
                    players,
                    new Pose(new Vector3(0f, 0f, -24f), Quaternion.identity),
                    BuildTownPlacements(doorwayPrefab)
                ),
                BuildSpaceScene(
                    CafeScene,
                    players,
                    new Pose(new Vector3(0f, 0f, -3f), Quaternion.identity),
                    new PrefabPlacement(BuildCafeInterior(doorwayPrefab), Vector3.zero, 0f)
                ),
                BuildSpaceScene(
                    HotelScene,
                    players,
                    new Pose(new Vector3(0f, 0f, -4f), Quaternion.identity),
                    new PrefabPlacement(BuildHotelLobby(doorwayPrefab), Vector3.zero, 0f)
                ),
            };

            // 첫 번째 씬(방)에서 시작한다. Sandbox 씬은 넣지 않는다.
            EditorBuildSettings.scenes = scenePaths
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log($"공간 씬을 만들었습니다: {string.Join(", ", scenePaths)}");
        }

        /// <summary>
        /// 출입구 프리팹. 앞쪽(+z)이 플레이어가 서는 쪽이다. 문짝 앞 감지 영역에 닿으면 이동하고,
        /// 다른 공간에서 들어오면 감지 영역 밖 ArrivalPoint에 문을 등지고 선다.
        /// </summary>
        private static GameObject BuildDoorwayPrefab()
        {
            var root = new GameObject("SpaceDoorway");
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.2f, 0.35f);
            trigger.size = new Vector3(1.4f, 2.4f, 0.7f);
            var doorway = root.AddComponent<SpaceDoorway>();

            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "DoorPanel",
                root.transform,
                new Vector3(0f, 1.1f, 0.05f),
                new Vector3(1.2f, 2.2f, 0.1f),
                ArtMaterial("World", "Door", DoorColor)
            );
            Transform arrivalPoint = BuilderTools.CreateEmpty(
                "ArrivalPoint",
                root.transform,
                new Vector3(0f, 0f, 2f),
                Quaternion.identity
            );
            BuilderTools.SetReference(doorway, "arrivalPoint", arrivalPoint);
            BuilderTools.RequireReferences(doorway);

            return BuilderTools.SavePrefab(root, DoorwayPrefabPath);
        }

        /// <summary>튜토리얼을 시작하는 내 방. 호버 학습용 가구(TV·냉장고·에어컨·소파·책장)를 둔다.</summary>
        private static GameObject BuildHomeRoom(GameObject doorwayPrefab)
        {
            var root = new GameObject("HomeRoom");
            AddRoomShell(
                root.transform,
                new Vector3(10f, 3f, 10f),
                ArtMaterial("Home", "HomeFloor", new Color(0.62f, 0.48f, 0.34f)),
                ArtMaterial("Home", "HomeWall", new Color(0.93f, 0.9f, 0.82f))
            );
            AddDoorway(
                doorwayPrefab,
                root.transform,
                new Vector3(0f, 0f, -5f),
                new DoorwayTarget(TownDoor, TownScene, HomeDoor)
            );

            Material wood = ArtMaterial("Home", "HomeWood", new Color(0.5f, 0.33f, 0.2f));
            Material fabric = ArtMaterial("Home", "HomeFabric", new Color(0.35f, 0.45f, 0.6f));
            Material appliance = ArtMaterial("Home", "HomeAppliance", new Color(0.88f, 0.9f, 0.92f));
            Material screen = ArtMaterial("Home", "HomeScreen", new Color(0.08f, 0.08f, 0.1f));

            AddBox(root.transform, "TvStand", new Vector3(-1.5f, 0.25f, 4.7f), new Vector3(2f, 0.5f, 0.45f), wood);
            AddBox(root.transform, "Tv", new Vector3(-1.5f, 1.15f, 4.85f), new Vector3(1.6f, 0.9f, 0.08f), screen);
            AddBox(root.transform, "Sofa", new Vector3(-1.5f, 0.4f, 1f), new Vector3(2.4f, 0.8f, 0.9f), fabric);
            AddBox(root.transform, "Bookshelf", new Vector3(-4.75f, 1f, -2.5f), new Vector3(0.5f, 2f, 1.8f), wood);
            AddBox(root.transform, "Fridge", new Vector3(4.5f, 0.95f, 4.5f), new Vector3(0.8f, 1.9f, 0.75f), appliance);
            AddBox(root.transform, "AirConditioner", new Vector3(2f, 2.4f, 4.85f), new Vector3(1.2f, 0.35f, 0.3f), appliance);
            AddBox(root.transform, "Bed", new Vector3(3.8f, 0.25f, -1.5f), new Vector3(2f, 0.5f, 2.8f), fabric);

            return BuilderTools.SavePrefab(root, "Assets/Prefabs/Home/HomeRoom.prefab");
        }

        /// <summary>
        /// 집 밖 밀집형 마을. 남북 큰길과 동서 길을 따라 집·카페·숙소와 준비 중인 식당·공항을 둔다.
        /// 건물 프리팹은 앞면(+z)을 길 쪽으로 돌려 놓는다.
        /// </summary>
        private static PrefabPlacement[] BuildTownPlacements(GameObject doorwayPrefab)
        {
            GameObject streets = BuildTownStreets();
            GameObject home = BuildBuilding(
                "HomeBuilding",
                new Vector3(7f, 5f, 7f),
                new Color(0.55f, 0.7f, 0.85f),
                "HOME",
                doorwayPrefab,
                new DoorwayTarget(HomeDoor, HomeScene, TownDoor)
            );
            GameObject cafe = BuildBuilding(
                "CafeBuilding",
                new Vector3(9f, 5f, 8f),
                new Color(0.9f, 0.62f, 0.35f),
                "CAFE",
                doorwayPrefab,
                new DoorwayTarget(CafeDoor, CafeScene, TownDoor)
            );
            GameObject hotel = BuildBuilding(
                "HotelBuilding",
                new Vector3(12f, 10f, 9f),
                new Color(0.65f, 0.58f, 0.8f),
                "HOTEL",
                doorwayPrefab,
                new DoorwayTarget(HotelDoor, HotelScene, TownDoor)
            );
            GameObject restaurant = BuildBuilding(
                "RestaurantBuilding",
                new Vector3(9f, 5f, 8f),
                new Color(0.8f, 0.4f, 0.38f),
                "RESTAURANT\nCOMING SOON",
                doorwayPrefab,
                entrance: null
            );
            GameObject airport = BuildBuilding(
                "AirportBuilding",
                new Vector3(20f, 7f, 10f),
                new Color(0.78f, 0.8f, 0.82f),
                "AIRPORT\nCOMING SOON",
                doorwayPrefab,
                entrance: null
            );
            GameObject house = BuildBuilding(
                "TownHouse",
                new Vector3(6f, 6f, 6f),
                new Color(0.86f, 0.8f, 0.68f),
                signText: null,
                doorwayPrefab,
                entrance: null
            );

            const float FacingEast = 90f;
            const float FacingWest = -90f;
            const float FacingNorth = 0f;
            const float FacingSouth = 180f;
            float westRowX = -(RoadHalfWidth + SidewalkWidth);
            float eastRowX = RoadHalfWidth + SidewalkWidth;
            float crossRoadNorthZ = CrossRoadCenterZ + RoadHalfWidth + SidewalkWidth;
            float crossRoadSouthZ = CrossRoadCenterZ - RoadHalfWidth - SidewalkWidth;

            return new[]
            {
                new PrefabPlacement(streets, Vector3.zero, 0f),
                // 큰길 서쪽: 앞면이 동쪽을 본다.
                new PrefabPlacement(house, new Vector3(westRowX - 3f, 0f, -25f), FacingEast),
                new PrefabPlacement(home, new Vector3(westRowX - 3.5f, 0f, -16f), FacingEast),
                new PrefabPlacement(house, new Vector3(westRowX - 3f, 0f, -7f), FacingEast),
                new PrefabPlacement(house, new Vector3(westRowX - 3f, 0f, 0f), FacingEast),
                new PrefabPlacement(hotel, new Vector3(westRowX - 4.5f, 0f, 22f), FacingEast),
                // 큰길 동쪽: 앞면이 서쪽을 본다.
                new PrefabPlacement(house, new Vector3(eastRowX + 3f, 0f, -25f), FacingWest),
                new PrefabPlacement(house, new Vector3(eastRowX + 3f, 0f, -16f), FacingWest),
                new PrefabPlacement(cafe, new Vector3(eastRowX + 4f, 0f, -4f), FacingWest),
                new PrefabPlacement(restaurant, new Vector3(eastRowX + 4f, 0f, 22f), FacingWest),
                // 동서 길 양쪽과 동쪽 끝 공항
                new PrefabPlacement(house, new Vector3(-28f, 0f, crossRoadNorthZ + 3f), FacingSouth),
                new PrefabPlacement(house, new Vector3(-22f, 0f, crossRoadNorthZ + 3f), FacingSouth),
                new PrefabPlacement(house, new Vector3(17f, 0f, crossRoadNorthZ + 3f), FacingSouth),
                new PrefabPlacement(house, new Vector3(-22f, 0f, crossRoadSouthZ - 3f), FacingNorth),
                new PrefabPlacement(house, new Vector3(-15f, 0f, crossRoadSouthZ - 3f), FacingNorth),
                new PrefabPlacement(house, new Vector3(17f, 0f, crossRoadSouthZ - 3f), FacingNorth),
                new PrefabPlacement(airport, new Vector3(26f, 0f, CrossRoadCenterZ), FacingWest),
            };
        }

        /// <summary>마을 바닥·도로와 밖으로 나가지 못하게 막는 경계 벽.</summary>
        private static GameObject BuildTownStreets()
        {
            var root = new GameObject("TownStreets");
            Material ground = ArtMaterial("Town", "TownGround", new Color(0.5f, 0.63f, 0.42f));
            Material road = ArtMaterial("Town", "TownRoad", new Color(0.3f, 0.3f, 0.32f));
            Material boundary = ArtMaterial("Town", "TownBoundary", new Color(0.6f, 0.55f, 0.5f));
            float size = TownHalfSize * 2f;

            AddBox(root.transform, "Ground", new Vector3(0f, -0.1f, 0f), new Vector3(size, 0.2f, size), ground);
            // 도로는 바닥 위에 얇게 덮어 색만 구분한다. 충돌은 바닥이 맡는다.
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "MainRoad",
                root.transform,
                new Vector3(0f, 0.01f, 0f),
                new Vector3(RoadHalfWidth * 2f, 0.02f, size),
                road,
                keepCollider: false
            );
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "CrossRoad",
                root.transform,
                // 교차로에서 큰길과 면이 겹쳐 깜박이지 않게 조금 높인다.
                new Vector3(-5f, 0.011f, CrossRoadCenterZ),
                new Vector3(50f, 0.02f, RoadHalfWidth * 2f),
                road,
                keepCollider: false
            );

            float wallOffset = TownHalfSize + 0.25f;
            AddBox(root.transform, "BoundaryNorth", new Vector3(0f, 1.25f, wallOffset), new Vector3(size + 1f, 2.5f, 0.5f), boundary);
            AddBox(root.transform, "BoundarySouth", new Vector3(0f, 1.25f, -wallOffset), new Vector3(size + 1f, 2.5f, 0.5f), boundary);
            AddBox(root.transform, "BoundaryEast", new Vector3(wallOffset, 1.25f, 0f), new Vector3(0.5f, 2.5f, size), boundary);
            AddBox(root.transform, "BoundaryWest", new Vector3(-wallOffset, 1.25f, 0f), new Vector3(0.5f, 2.5f, size), boundary);

            return BuilderTools.SavePrefab(root, "Assets/Prefabs/Town/TownStreets.prefab");
        }

        /// <summary>
        /// 앞면(+z)에 출입구와 간판이 있는 건물. 들어갈 수 없는 건물은 문짝만 두고 간판에 준비 중임을 적는다.
        /// </summary>
        private static GameObject BuildBuilding(
            string buildingName,
            Vector3 size,
            Color wallColor,
            string signText,
            GameObject doorwayPrefab,
            DoorwayTarget? entrance
        )
        {
            var root = new GameObject(buildingName);
            float front = size.z / 2f;

            AddBox(
                root.transform,
                "Body",
                new Vector3(0f, size.y / 2f, 0f),
                size,
                ArtMaterial("Town", $"{buildingName}Wall", wallColor)
            );
            AddBox(
                root.transform,
                "Roof",
                new Vector3(0f, size.y + 0.15f, 0f),
                new Vector3(size.x + 0.6f, 0.3f, size.z + 0.6f),
                ArtMaterial("Town", "TownRoof", new Color(0.45f, 0.32f, 0.3f))
            );

            if (entrance.HasValue)
            {
                AddDoorway(doorwayPrefab, root.transform, new Vector3(0f, 0f, front), entrance.Value);
            }
            else
            {
                AddBox(
                    root.transform,
                    "ClosedDoor",
                    new Vector3(0f, 1.1f, front + 0.05f),
                    new Vector3(1.2f, 2.2f, 0.1f),
                    ArtMaterial("World", "Door", DoorColor)
                );
            }

            if (signText != null)
            {
                bool hasTwoLines = signText.Contains('\n');
                AddSign(
                    root.transform,
                    signText,
                    new Vector3(0f, hasTwoLines ? 3.3f : 3.1f, front),
                    facingYawDegrees: 0f,
                    new Vector2(Mathf.Min(size.x * 0.7f, 6f), hasTwoLines ? 1.4f : 0.9f)
                );
            }

            return BuilderTools.SavePrefab(root, $"Assets/Prefabs/Town/{buildingName}.prefab");
        }

        /// <summary>카페 내부. 카운터와 메뉴판, 테이블을 둔다. 카페 NPC는 NPC 작업에서 배치한다.</summary>
        private static GameObject BuildCafeInterior(GameObject doorwayPrefab)
        {
            var root = new GameObject("CafeInterior");
            AddRoomShell(
                root.transform,
                new Vector3(14f, 3.5f, 12f),
                ArtMaterial("Cafe", "CafeFloor", new Color(0.55f, 0.4f, 0.3f)),
                ArtMaterial("Cafe", "CafeWall", new Color(0.95f, 0.88f, 0.78f))
            );
            AddDoorway(
                doorwayPrefab,
                root.transform,
                new Vector3(0f, 0f, -6f),
                new DoorwayTarget(TownDoor, TownScene, CafeDoor)
            );

            Material wood = ArtMaterial("Cafe", "CafeWood", new Color(0.42f, 0.28f, 0.18f));
            Material chair = ArtMaterial("Cafe", "CafeChair", new Color(0.3f, 0.45f, 0.35f));

            AddBox(root.transform, "Counter", new Vector3(0f, 0.55f, 3.8f), new Vector3(6f, 1.1f, 1f), wood);
            AddSign(root.transform, "MENU", new Vector3(0f, 2.4f, 6f), facingYawDegrees: 180f, new Vector2(3f, 1f));

            Vector3[] tablePositions =
            {
                new Vector3(-4f, 0f, -2f),
                new Vector3(4f, 0f, -2f),
                new Vector3(-4f, 0f, 1.5f),
                new Vector3(4f, 0f, 1.5f),
            };
            for (int index = 0; index < tablePositions.Length; index++)
            {
                Vector3 table = tablePositions[index];
                BuilderTools.CreateShape(
                    PrimitiveType.Cylinder,
                    $"Table{index + 1}",
                    root.transform,
                    table + new Vector3(0f, 0.375f, 0f),
                    new Vector3(1f, 0.375f, 1f),
                    wood
                );
                AddBox(root.transform, $"Table{index + 1}ChairLeft", table + new Vector3(-0.95f, 0.225f, 0f), new Vector3(0.45f, 0.45f, 0.45f), chair);
                AddBox(root.transform, $"Table{index + 1}ChairRight", table + new Vector3(0.95f, 0.225f, 0f), new Vector3(0.45f, 0.45f, 0.45f), chair);
            }

            return BuilderTools.SavePrefab(root, "Assets/Prefabs/Cafe/CafeInterior.prefab");
        }

        /// <summary>숙소 로비. 프런트 데스크와 소파, 장식용 엘리베이터 문을 둔다.</summary>
        private static GameObject BuildHotelLobby(GameObject doorwayPrefab)
        {
            var root = new GameObject("HotelLobby");
            AddRoomShell(
                root.transform,
                new Vector3(16f, 4f, 14f),
                ArtMaterial("Hotel", "HotelFloor", new Color(0.75f, 0.72f, 0.68f)),
                ArtMaterial("Hotel", "HotelWall", new Color(0.88f, 0.86f, 0.92f))
            );
            AddDoorway(
                doorwayPrefab,
                root.transform,
                new Vector3(0f, 0f, -7f),
                new DoorwayTarget(TownDoor, TownScene, HotelDoor)
            );

            Material wood = ArtMaterial("Hotel", "HotelWood", new Color(0.4f, 0.27f, 0.2f));
            Material fabric = ArtMaterial("Hotel", "HotelFabric", new Color(0.55f, 0.25f, 0.3f));
            Material metal = ArtMaterial("Hotel", "HotelMetal", new Color(0.7f, 0.72f, 0.75f));

            AddBox(root.transform, "FrontDesk", new Vector3(0f, 0.55f, 4.6f), new Vector3(6f, 1.1f, 1.2f), wood);
            AddSign(root.transform, "RECEPTION", new Vector3(0f, 2.8f, 7f), facingYawDegrees: 180f, new Vector2(4f, 0.9f));
            AddBox(root.transform, "SofaLeft", new Vector3(-5.5f, 0.4f, -1f), new Vector3(1f, 0.8f, 2.6f), fabric);
            AddBox(root.transform, "SofaRight", new Vector3(5.5f, 0.4f, -1f), new Vector3(1f, 0.8f, 2.6f), fabric);
            AddBox(root.transform, "ElevatorDoorLeft", new Vector3(-5.5f, 1.2f, 6.95f), new Vector3(1.6f, 2.4f, 0.1f), metal);
            AddBox(root.transform, "ElevatorDoorRight", new Vector3(5.5f, 1.2f, 6.95f), new Vector3(1.6f, 2.4f, 0.1f), metal);

            return BuilderTools.SavePrefab(root, "Assets/Prefabs/Hotel/HotelLobby.prefab");
        }

        /// <summary>
        /// 공간 프리팹을 배치하고 플레이어·카메라·도착 처리를 연결한 배포용 씬을 저장한다.
        /// 이동 없이 이 씬을 열고 Play하면 <paramref name="playerStart"/>에서 시작한다.
        /// </summary>
        private static string BuildSpaceScene(
            string sceneName,
            PlayerPrefabs players,
            Pose playerStart,
            params PrefabPlacement[] environment
        )
        {
            SceneGroups groups = BuilderTools.NewSceneWithGroups();

            foreach (PrefabPlacement placement in environment)
            {
                BuilderTools.InstantiatePrefab(
                    placement.Prefab,
                    groups.Environment,
                    placement.Position,
                    Quaternion.Euler(0f, placement.YawDegrees, 0f)
                );
            }

            GameObject player = BuilderTools.InstantiatePrefab(
                players.Player,
                groups.Characters,
                playerStart.position,
                playerStart.rotation
            );
            // Editor에서 씬을 열었을 때도 플레이어 뒤에서 보이도록 놓는다. Play하면 카메라가 다시 맞춘다.
            GameObject playerCamera = BuilderTools.InstantiatePrefab(
                players.Camera,
                groups.Systems,
                playerStart.position + playerStart.rotation * new Vector3(0f, 2.5f, -5f),
                playerStart.rotation * Quaternion.Euler(20f, 0f, 0f)
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, playerCamera);

            var arrival = new GameObject("SpaceArrival").AddComponent<SpaceArrival>();
            arrival.transform.SetParent(groups.Systems, false);
            BuilderTools.SetReference(arrival, "player", player.GetComponent<PlayerMover>());
            BuilderTools.SetReference(arrival, "cameraRig", playerCamera.GetComponent<PlayerCameraRig>());
            BuilderTools.RequireReferences(arrival);

            string scenePath = $"{SceneFolder}/{sceneName}.unity";
            BuilderTools.SaveScene(groups.Scene, scenePath);
            return scenePath;
        }

        /// <summary>바닥과 네 벽. 카메라가 안을 볼 수 있게 천장은 두지 않는다. 안쪽 면이 원점 기준 ±size/2에 온다.</summary>
        private static void AddRoomShell(Transform parent, Vector3 size, Material floor, Material wall)
        {
            const float Thickness = 0.3f;
            float halfWidth = size.x / 2f;
            float halfDepth = size.z / 2f;
            float wallCenterY = size.y / 2f;

            AddBox(parent, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(size.x, 0.2f, size.z), floor);
            AddBox(parent, "WallNorth", new Vector3(0f, wallCenterY, halfDepth + Thickness / 2f), new Vector3(size.x + Thickness * 2f, size.y, Thickness), wall);
            AddBox(parent, "WallSouth", new Vector3(0f, wallCenterY, -halfDepth - Thickness / 2f), new Vector3(size.x + Thickness * 2f, size.y, Thickness), wall);
            AddBox(parent, "WallEast", new Vector3(halfWidth + Thickness / 2f, wallCenterY, 0f), new Vector3(Thickness, size.y, size.z), wall);
            AddBox(parent, "WallWest", new Vector3(-halfWidth - Thickness / 2f, wallCenterY, 0f), new Vector3(Thickness, size.y, size.z), wall);
        }

        /// <summary>출입구 프리팹을 놓고 이름과 이어지는 공간을 정한다. 앞쪽(+z)이 플레이어가 서는 쪽이다.</summary>
        private static void AddDoorway(
            GameObject doorwayPrefab,
            Transform parent,
            Vector3 localPosition,
            DoorwayTarget target
        )
        {
            GameObject instance = BuilderTools.InstantiatePrefab(
                doorwayPrefab,
                parent,
                localPosition,
                Quaternion.identity
            );
            instance.name = target.DoorwayId;

            var doorway = instance.GetComponent<SpaceDoorway>();
            BuilderTools.SetText(doorway, "doorwayId", target.DoorwayId);
            BuilderTools.SetText(doorway, "destinationScene", target.DestinationScene);
            BuilderTools.SetText(doorway, "destinationDoorwayId", target.DestinationDoorwayId);
        }

        /// <summary>
        /// 공간 이름을 영어로 적은 간판. 마을이 영어권이라 간판도 영어로 둔다.
        /// <paramref name="facingYawDegrees"/>는 간판 앞면이 보는 방향이며, 그 반대쪽 면에 붙인다.
        /// </summary>
        private static void AddSign(
            Transform parent,
            string text,
            Vector3 mountPosition,
            float facingYawDegrees,
            Vector2 boardSize
        )
        {
            Quaternion facing = Quaternion.Euler(0f, facingYawDegrees, 0f);
            Transform sign = BuilderTools.CreateEmpty($"Sign{FirstWord(text)}", parent, mountPosition, facing);

            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "Board",
                sign,
                new Vector3(0f, 0f, 0.05f),
                new Vector3(boardSize.x, boardSize.y, 0.1f),
                ArtMaterial("World", "SignBoard", SignBoardColor),
                keepCollider: false
            );

            // TextMesh는 뒤쪽(-z)에서 볼 때 바르게 읽히므로, 앞면 쪽에서 읽히도록 반 바퀴 돌린다.
            Transform label = BuilderTools.CreateEmpty(
                "Label",
                sign,
                new Vector3(0f, 0f, 0.11f),
                Quaternion.Euler(0f, 180f, 0f)
            );
            label.gameObject.AddComponent<MeshRenderer>();
            var textMesh = label.gameObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.fontSize = 80;
            textMesh.characterSize = 0.05f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = SignTextMaterial(textMesh.font);
        }

        /// <summary>
        /// 간판 글자 머티리얼. 글꼴 기본 머티리얼은 깊이 검사를 하지 않아 건물 너머의 간판 글자가 비쳐 보이므로,
        /// 깊이 검사를 하는 Art/World/SignText 셰이더를 쓴다.
        /// </summary>
        private static Material SignTextMaterial(Font font)
        {
            const string MaterialPath = "Assets/Art/World/SignText.mat";
            Shader shader = Shader.Find("A305/World/SignText");
            if (shader == null)
            {
                throw new InvalidOperationException("Assets/Art/World/SignText.shader를 찾지 못했습니다.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;
            material.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddBox(
            Transform parent,
            string boxName,
            Vector3 localPosition,
            Vector3 size,
            Material material
        )
        {
            BuilderTools.CreateShape(PrimitiveType.Cube, boxName, parent, localPosition, size, material);
        }

        private static Material ArtMaterial(string feature, string materialName, Color color)
        {
            return BuilderTools.GetOrCreateMaterial($"Assets/Art/{feature}", materialName, color);
        }

        private static string FirstWord(string text)
        {
            string firstWord = text.Split('\n', ' ')[0].ToLowerInvariant();
            return char.ToUpperInvariant(firstWord[0]) + firstWord.Substring(1);
        }

        private readonly struct PlayerPrefabs
        {
            public PlayerPrefabs(GameObject player, GameObject camera)
            {
                Player = player;
                Camera = camera;
            }

            public GameObject Player { get; }

            public GameObject Camera { get; }
        }

        private readonly struct PrefabPlacement
        {
            public PrefabPlacement(GameObject prefab, Vector3 position, float yawDegrees)
            {
                Prefab = prefab;
                Position = position;
                YawDegrees = yawDegrees;
            }

            public GameObject Prefab { get; }

            public Vector3 Position { get; }

            public float YawDegrees { get; }
        }

        private readonly struct DoorwayTarget
        {
            public DoorwayTarget(string doorwayId, string destinationScene, string destinationDoorwayId)
            {
                DoorwayId = doorwayId;
                DestinationScene = destinationScene;
                DestinationDoorwayId = destinationDoorwayId;
            }

            public string DoorwayId { get; }

            public string DestinationScene { get; }

            public string DestinationDoorwayId { get; }
        }
    }
}
