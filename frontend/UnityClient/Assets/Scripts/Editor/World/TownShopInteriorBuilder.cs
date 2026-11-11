using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
using A305.ObjectHover;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 마을 안에서 걸어 들어가는 매장(거리 매장 6곳, 북쪽 상가 3동의 1·2층)의 실내 소품을 공간별 묶음 프리팹으로 채운다.
    /// 공간 프리팹(<see cref="Folder"/>/&lt;공간&gt;Interior)은 원점이 건물 앞면 바닥 가운데라, 나중에 공간을 따로 씬으로 옮길 때 그대로 놓으면 된다.
    /// 매장 건물 프리팹의 Interior 아래에 그 층 높이로 끼우고, 매장 Builder 코드와 기존 가구는 고치지 않는다.
    /// </summary>
    public static class TownShopInteriorBuilder
    {
        internal const string Folder = "Assets/Prefabs/World/TownInteriors";
        private const string LaundromatRoom = "LaundromatInterior";
        private const string CaptureFolder = "../../../artifacts/unity/town-interiors";

        /// <summary>
        /// 공간 프리팹과 소품을 없을 때만 만들고, 아직 끼우지 않은 매장 건물 프리팹에 끼운다.
        /// 큰 가구가 늘면 매장 직원이 다닐 바닥을 다시 구워야 하므로 이어서 Npc → Build Building Staff를 실행한다.
        /// </summary>
        [MenuItem("Tools/A305/World/Build Town Shop Interiors")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            string sharedMeshes = MainstreetArtKit.ArtFolder + "/Meshes";
            var existingMeshes = new HashSet<string>(
                AssetDatabase.FindAssets("t:Mesh", new[] { sharedMeshes })
            );
            foreach (TownShopInteriorLayouts.Space space in TownShopInteriorLayouts.Spaces)
            {
                GameObject prefab = TownShopInteriorProps.CreateOrOverwrite(
                    Folder + "/" + space.Name + ".prefab",
                    () =>
                    {
                        var root = new GameObject(space.Name);
                        space.Furnish(root.transform);
                        return root;
                    }
                );
                if (
                    !BuilderTools.UpdatePrefabIfExists(
                        space.BuildingPrefab,
                        building => Install(building, space, prefab)
                    )
                )
                    throw new InvalidOperationException(
                        "매장 건물 프리팹이 없습니다: " + space.BuildingPrefab
                    );
            }
            // 도형 조각은 메시를 합쳐 저장하므로, 합치기 전에 공용 메시 폴더에 새로 생긴 둥근 상자 메시는 남기지 않는다.
            foreach (
                string guid in AssetDatabase
                    .FindAssets("t:Mesh", new[] { sharedMeshes })
                    .Where(guid => !existingMeshes.Contains(guid))
            )
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.SaveAssets();
        }

        /// <summary>코드로 바꾼 배치를 반영하려고 공간·소품 프리팹을 모두 새로 저장한다. 프리팹에서 손으로 고친 내용은 사라진다.</summary>
        [MenuItem("Tools/A305/World/Rebuild Town Shop Interiors (Overwrite)")]
        public static void Rebuild()
        {
            if (!BuilderTools.CanBuild())
                return;
            TownShopInteriorProps.BeginOverwrite();
            try
            {
                Build();
            }
            finally
            {
                TownShopInteriorProps.EndOverwrite();
            }
        }

        /// <summary>
        /// 계단실 칸막이 없이 건물 폭 전체를 쓰도록 바꾼 오락실·서점 배치를 반영한다.
        /// 두 공간 프리팹만 코드대로 다시 저장하고, 소품 프리팹은 없을 때만 만들며 다른 매장은 그대로 둔다.
        /// 이어서 Build Town Shop Scenes로 매장 장면을 다시 맞춘다.
        /// </summary>
        [MenuItem("Tools/A305/World/Rebuild Arcade And Bookshop Interiors")]
        public static void RebuildArcadeShops()
        {
            if (!BuilderTools.CanBuild())
                return;
            string sharedMeshes = MainstreetArtKit.ArtFolder + "/Meshes";
            var existingMeshes = new HashSet<string>(
                AssetDatabase.FindAssets("t:Mesh", new[] { sharedMeshes })
            );
            foreach (
                TownShopInteriorLayouts.Space space in TownShopInteriorLayouts.Spaces.Where(
                    candidate => candidate.Name is "ArcadeInterior" or "BookshopInterior"
                )
            )
            {
                var root = new GameObject(space.Name);
                space.Furnish(root.transform);
                GameObject prefab = BuilderTools.SavePrefab(
                    root,
                    Folder + "/" + space.Name + ".prefab"
                );
                BuilderTools.UpdatePrefabIfExists(
                    space.BuildingPrefab,
                    building => Install(building, space, prefab)
                );
            }
            foreach (
                string guid in AssetDatabase
                    .FindAssets("t:Mesh", new[] { sharedMeshes })
                    .Where(guid => !existingMeshes.Contains(guid))
            )
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/World/Update Laundromat Props")]
        public static void UpdateLaundromatProps()
        {
            if (!BuilderTools.CanBuild())
                return;

            TownShopInteriorLayouts.Space space = TownShopInteriorLayouts.Spaces.Single(candidate =>
                candidate.Name == LaundromatRoom
            );
            string roomPath = $"{Folder}/{LaundromatRoom}.prefab";
            if (
                !BuilderTools.UpdatePrefabIfExists(
                    roomPath,
                    room => RemovePlant(room.transform.Find("WasherRow/SnakePlant"))
                )
            )
                throw new InvalidOperationException($"빨래방 실내 프리팹이 없습니다: {roomPath}");

            GameObject roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(roomPath);
            if (
                !BuilderTools.UpdatePrefabIfExists(
                    space.BuildingPrefab,
                    building =>
                    {
                        RemovePlant(building.transform.Find("Interior/Plant"));
                        Install(building, space, roomPrefab);
                    }
                )
            )
                throw new InvalidOperationException(
                    $"빨래방 건물 프리팹이 없습니다: {space.BuildingPrefab}"
                );
            AssetDatabase.SaveAssets();
        }

        private static void RemovePlant(Transform plant)
        {
            if (plant != null)
                Object.DestroyImmediate(plant.gameObject);
        }

        // 건물 Interior 아래 그 층 높이에 공간 프리팹을 한 번만 둔다.
        private static void Install(
            GameObject building,
            TownShopInteriorLayouts.Space space,
            GameObject prefab
        )
        {
            Transform interior = building.transform.Find("Interior");
            if (interior == null)
                throw new InvalidOperationException(
                    "매장 건물에 Interior가 없습니다: " + building.name
                );
            Transform room = interior.Find(space.Name);
            if (room == null)
            {
                GameObject placed = BuilderTools.InstantiatePrefab(
                    prefab,
                    interior,
                    new Vector3(0, space.Floor, 0),
                    Quaternion.identity
                );
                placed.name = space.Name;
                room = placed.transform;
            }
            if (space.Name == LaundromatRoom)
                RegisterLaundromatWords(room);
            // 단어를 정하지 않은 소품은 기존 매장 소품처럼 건물 단어(shop)를 따른다.
            // 그 호버 영역을 건물 프리팹에 만들어 두지 않으면 마을 씬을 저장할 때마다 씬 쪽 덮어쓰기로 쌓인다.
            ObjectWordRegistration.ApplyToPrefab(building, space.BuildingPrefab);
        }

        private static readonly (
            string objectName,
            string file,
            string text,
            string meaning,
            string example,
            string exampleKo
        )[] LaundromatWords =
        {
            (
                "LaundryBasket",
                "laundry_basket",
                "laundry basket",
                "빨래 바구니",
                "Put your clean clothes in the laundry basket.",
                "깨끗한 옷을 빨래 바구니에 넣으세요."
            ),
            (
                "DetergentBottles",
                "laundry_detergent",
                "laundry detergent",
                "세탁 세제",
                "Add a little laundry detergent before you start the washer.",
                "세탁기를 돌리기 전에 세탁 세제를 조금 넣으세요."
            ),
            (
                "TowelStack",
                "folded_towels",
                "folded towels",
                "접어 놓은 수건",
                "The folded towels are on the shelf.",
                "접어 놓은 수건은 선반에 있어요."
            ),
            (
                "LaundryCart",
                "laundry_cart",
                "laundry cart",
                "빨래 수레",
                "Use the laundry cart to take your clothes to the dryer.",
                "빨래 수레를 써서 옷을 건조기로 옮기세요."
            ),
            (
                "DetergentVending",
                "detergent_vending_machine",
                "detergent vending machine",
                "세제 자판기",
                "You can buy detergent from the detergent vending machine.",
                "세제 자판기에서 세제를 살 수 있어요."
            ),
            (
                "ClothesRack",
                "clothes_rack",
                "clothes rack",
                "옷걸이 행거",
                "Hang your shirt on the clothes rack.",
                "셔츠를 행거에 걸어 두세요."
            ),
            (
                "ChangeMachine",
                "change_machine",
                "change machine",
                "동전 교환기",
                "The change machine gives you quarters for the washers.",
                "동전 교환기에서 세탁기에 쓸 25센트 동전을 받을 수 있어요."
            ),
        };

        private static void RegisterLaundromatWords(Transform room)
        {
            // 수건·세제·동전 교환기는 다른 매장에서도 쓰므로 빨래방 인스턴스에만 연결한다.
            var props = room.GetComponentsInChildren<Transform>(true).ToLookup(prop => prop.name);
            foreach (var word in LaundromatWords)
            {
                ObjectWordDefinition definition = ObjectWordAuthoring.FindOrCreateWord(
                    word.file,
                    word.text,
                    word.meaning,
                    word.example,
                    word.exampleKo
                );
                foreach (Transform prop in props[word.objectName])
                {
                    var hover = BuilderTools.GetOrAddComponent<HoverableObject>(prop.gameObject);
                    BuilderTools.SetReference(hover, "word", definition);
                }
            }
        }

        // 거리 매장은 문 안쪽에서 뒷벽을 본다. 북쪽 상가는 계단을 비켜 매장 쪽으로 들어선 자리에서 본다.
        private static readonly Vector3 DoorEye = new Vector3(0, 2.2f, -.5f);
        private static readonly Vector3 DoorTarget = new Vector3(0, .7f, -5.6f);

        // 건물 기준 좌표의 1층(y=0)과 북쪽 상가 2층(y=4) 실내. 위에서는 천장을 걷어 내려다본다.
        private static readonly (
            string building,
            string label,
            float floor,
            Vector3 eye,
            Vector3 target
        )[] Views =
        {
            ("PostOffice", "PostOffice", 0, DoorEye, DoorTarget),
            ("MapleDiner", "MapleDiner", 0, DoorEye, DoorTarget),
            ("TownBank", "TownBank", 0, DoorEye, DoorTarget),
            ("SudsLaundromat", "SudsLaundromat", 0, DoorEye, DoorTarget),
            ("BloomFlowers", "BloomFlowers", 0, DoorEye, DoorTarget),
            ("MainStreetBarber", "MainStreetBarber", 0, DoorEye, DoorTarget),
            ("PoliceStation", "PoliceStation", 0, DoorEye, DoorTarget),
            (
                "MapleBakery",
                "MapleBakery",
                0,
                new Vector3(-.5f, 2.2f, -.4f),
                new Vector3(-2f, .7f, -6f)
            ),
            (
                "MapleBakery",
                "MapleBakery2F",
                4,
                new Vector3(1f, 1.9f, -.6f),
                new Vector3(-2.2f, .5f, -5.5f)
            ),
            (
                "LittleArcade",
                "LittleArcade",
                0,
                new Vector3(-.4f, 2.2f, -.9f),
                new Vector3(-2.6f, .7f, -5.8f)
            ),
            (
                "LittleArcade",
                "PaperAndLeaf2F",
                4,
                new Vector3(-.4f, 2.2f, -.9f),
                new Vector3(-2.6f, .7f, -5.8f)
            ),
            (
                "HomeLiving",
                "HomeLiving",
                0,
                new Vector3(2.7f, 2.2f, -.5f),
                new Vector3(-.8f, .7f, -5.6f)
            ),
            (
                "HomeLiving",
                "LeafTeaRoom2F",
                4,
                new Vector3(2.7f, 2.2f, -.5f),
                new Vector3(-.8f, .7f, -5.6f)
            ),
        };

        /// <summary>
        /// 매장마다 문 안쪽 시점과 천장을 걷은 내려다보기, 비교용 카페·호텔 실내를 찍어 artifacts에 저장한다.
        /// 한눈에 비교하도록 시점별로 모은 한 장(entry-sheet·overview-sheet)도 함께 저장한다.
        /// </summary>
        [MenuItem("Tools/A305/World/Capture Town Shop Interiors")]
        public static void Capture()
        {
            int rows = (Views.Length + 2 + SheetColumns - 1) / SheetColumns;
            // 비교용 씬을 열 때 쓰지 않는 에셋 정리로 지워지지 않게 한다.
            var entrySheet = new RenderTexture(SheetColumns * TileWidth, rows * TileHeight, 24)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var overviewSheet = new RenderTexture(SheetColumns * TileWidth, rows * TileHeight, 24)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            int tile = 0;

            Scene town = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Camera camera = ReviewCamera();
            Transform buildings = TownProductionBuilder
                .Root(town, "Environment")
                .Find("TownMainstreet/BackgroundBuildings");
            foreach (
                (string building, string label, float floor, Vector3 eye, Vector3 target) in Views
            )
            {
                Transform root = buildings.Find(building);
                if (root == null)
                {
                    Debug.LogWarning("마을에 매장이 없습니다: " + building);
                    continue;
                }
                camera.fieldOfView = 78;
                camera.transform.position = root.TransformPoint(eye + Vector3.up * floor);
                camera.transform.LookAt(root.TransformPoint(target + Vector3.up * floor));
                SaveImage(camera, label + "-entry.png");
                DrawTile(camera, entrySheet, tile);

                // 문 안쪽 시점과 좌우가 같도록 뒷벽을 화면 위에 둔다. 2층 천장은 바닥에서 3m 아래에 있다.
                float ceiling = floor > 0 ? 2.9f : 3.6f;
                List<Renderer> hidden = HideAbove(root, root.position.y + floor + ceiling);
                camera.fieldOfView = 55;
                camera.transform.position = root.TransformPoint(
                    new Vector3(0, floor + 10.5f, -.6f)
                );
                camera.transform.LookAt(
                    root.TransformPoint(new Vector3(0, floor, -4.3f)),
                    -root.forward
                );
                SaveImage(camera, label + "-overview.png");
                DrawTile(camera, overviewSheet, tile);
                foreach (Renderer renderer in hidden)
                    renderer.enabled = true;
                tile++;
            }

            foreach (
                (string scenePath, string label, float entryZ, float lookZ) in new[]
                {
                    (CafeInteriorBuilder.ScenePath, "Cafe", -5.4f, 2.5f),
                    (HotelInteriorBuilder.ScenePath, "Hotel", -6.4f, 3.5f),
                }
            )
            {
                EditorSceneManager.OpenScene(scenePath);
                camera = ReviewCamera();
                camera.fieldOfView = 78;
                camera.transform.position = new Vector3(0, 2.2f, entryZ);
                camera.transform.LookAt(new Vector3(0, .7f, lookZ));
                SaveImage(camera, "Reference-" + label + ".png");
                DrawTile(camera, entrySheet, tile++);
            }

            SaveTexture(entrySheet, "entry-sheet.png");
            SaveTexture(overviewSheet, "overview-sheet.png");
            Object.DestroyImmediate(entrySheet);
            Object.DestroyImmediate(overviewSheet);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private const int SheetColumns = 4;
        private const int TileWidth = 480;
        private const int TileHeight = 300;

        // 모음 한 장의 칸에 그린다. 화면 비율은 낱장(1600×1000)과 같다.
        private static void DrawTile(Camera camera, RenderTexture sheet, int index)
        {
            int rows = sheet.height / TileHeight;
            int column = index % SheetColumns;
            int row = rows - 1 - index / SheetColumns;
            camera.targetTexture = sheet;
            camera.pixelRect = new Rect(
                column * TileWidth,
                row * TileHeight,
                TileWidth,
                TileHeight
            );
            camera.Render();
            camera.targetTexture = null;
            camera.rect = new Rect(0, 0, 1, 1);
        }

        private static Camera ReviewCamera()
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
            foreach (Camera existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;
            var camera = new GameObject("InteriorReviewCamera").AddComponent<Camera>();
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 160;
            camera.backgroundColor = new Color(.76f, .85f, .86f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            return camera;
        }

        // 내려다볼 층 위의 천장·윗층·지붕과 그 위로 드리운 나무를 잠시 끈다. 씬은 저장하지 않는다.
        private static List<Renderer> HideAbove(Transform building, float height)
        {
            var footprint = new Bounds(
                building.TransformPoint(new Vector3(0, 0, -4)),
                new Vector3(12, 200, 12)
            );
            List<Renderer> hidden = Object
                .FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(renderer =>
                    renderer.enabled
                    && renderer.bounds.min.y > height
                    && footprint.Intersects(renderer.bounds)
                )
                .ToList();
            foreach (Renderer renderer in hidden)
                renderer.enabled = false;
            return hidden;
        }

        private static void SaveImage(Camera camera, string filename)
        {
            var target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            SaveTexture(target, filename);
            Object.DestroyImmediate(target);
        }

        private static void SaveTexture(RenderTexture source, string filename)
        {
            RenderTexture.active = source;
            var pixels = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = null;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, CaptureFolder));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, filename), pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
        }
    }
}
