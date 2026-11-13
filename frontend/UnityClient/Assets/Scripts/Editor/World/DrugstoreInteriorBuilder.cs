using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
using A305.ObjectHover;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.ThirdPartyModelKit;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 우산을 사는 상점을 미국 소도시의 드럭스토어처럼 꾸민다. 미국에서는 갑자기 비가 오면 CVS·Walgreens 같은
    /// 드럭스토어에서 우산을 사는 일이 많아, 입구 우산 통과 음료 냉장고 벽·약 코너·커피 코너를 둔다.
    /// 조각 프리팹과 실내 프리팹은 없을 때만 만들고, 이미 있으면 디자인을 고쳤을 수 있으므로 그대로 둔다.
    /// </summary>
    public static class DrugstoreInteriorBuilder
    {
        private const string MenuPath = "Tools/A305/World/Apply Drugstore Interior";
        internal const string ScenePath = "Assets/Scenes/Production/ShopScene.unity";
        private const string InteriorName = "DrugstoreInterior";
        private const string PrefabFolder = "Assets/Prefabs/World/Drugstore";
        private const string ArtFolder = "Assets/Art/World/Drugstore";
        private const string MaterialFolder = ArtFolder + "/Materials";
        private const string FloorTexturePath = ArtFolder + "/DrugstoreFloorTiles.png";
        private const string Food = "Assets/ThirdParty/KenneyFoodKit/";
        private const string Furniture = "Assets/ThirdParty/KenneyFurnitureKit/";
        private const string CrateModel = "Assets/ThirdParty/KenneySurvivalKit/boxOpen.obj";
        private const string FridgeModel = "Assets/ThirdParty/QuaterniusShopProps/CanFridge.obj";
        private const string ShelfModel =
            "Assets/ThirdParty/QuaterniusShopProps/Props_Shelf_Tall.fbx";
        private const string CoolerModel =
            "Assets/ThirdParty/MilkAndBananaCoolerBox/appliance_cooler_box.obj";
        private const string TinyTreats = "Assets/Prefabs/World/TinyTreats/";
        private static readonly Vector3 RainyDaySignPosition = new Vector3(1.55f, 0, -6.5f);
        private static readonly Vector3[] UmbrellaBinPositions =
        {
            new Vector3(2.45f, 0, -6.4f),
            new Vector3(3.25f, 0, -6.4f),
        };

        // 매장 바닥(ShopFloor) 윗면과 계산대(ShopCounter) 상판 높이
        private const float CounterTop = .8f;
        private const float CoffeeCounterHeight = 1.08f;
        private const float CoffeeCountertopThickness = .08f;

        // 진열품이 게임 화면에서도 알아보이도록 실제보다 조금 크게 둔다.
        private const float GoodsScale = 1.4f;

        // 납작한 물건은 길이로, 세워 두는 물건은 높이로 크기를 맞춘다.
        private static readonly Dictionary<string, (Fit Fit, float Size)> Goods = new Dictionary<
            string,
            (Fit, float)
        >
        {
            ["can"] = (Fit.Height, .15f),
            ["canSmall"] = (Fit.Height, .11f),
            ["carton"] = (Fit.Height, .24f),
            ["cartonSmall"] = (Fit.Height, .14f),
            ["candyBarWrapper"] = (Fit.Length, .17f),
            ["chocolateWrapper"] = (Fit.Length, .15f),
            ["bag"] = (Fit.Height, .28f),
            ["bagFlat"] = (Fit.Length, .24f),
            ["bottleKetchup"] = (Fit.Height, .22f),
            ["peanutButter"] = (Fit.Height, .12f),
            ["honey"] = (Fit.Height, .13f),
            ["styrofoam"] = (Fit.Height, .12f),
            ["cookie"] = (Fit.Length, .08f),
            ["lollypop"] = (Fit.Height, .18f),
            ["iceCream"] = (Fit.Height, .18f),
            ["cup"] = (Fit.Height, .12f),
            ["hotDog"] = (Fit.Length, .2f),
            ["apple"] = (Fit.Length, .08f),
            ["orange"] = (Fit.Length, .08f),
            ["banana"] = (Fit.Length, .18f),
            ["carrot"] = (Fit.Length, .2f),
            ["cabbage"] = (Fit.Length, .18f),
            ["corn"] = (Fit.Length, .22f),
            ["broccoli"] = (Fit.Length, .16f),
        };

        private static readonly string[] Snacks =
        {
            "candyBarWrapper",
            "chocolateWrapper",
            "bag",
            "bagFlat",
            "cookie",
            "lollypop",
        };

        private static readonly string[] Pantry =
        {
            "can",
            "canSmall",
            "carton",
            "cartonSmall",
            "bottleKetchup",
            "peanutButter",
            "honey",
            "styrofoam",
        };

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Apply(scene);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        [MenuItem("Tools/A305/World/Apply Drugstore Entrance Display")]
        public static void ApplyEntranceDisplay()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuilderTools.UpdatePrefabIfExists(
                PrefabFolder + "/RainyDaySign.prefab",
                FitRainyDaySignLabel
            );
            BuilderTools.UpdatePrefabIfExists(
                PrefabFolder + "/" + InteriorName + ".prefab",
                ArrangeEntranceDisplay
            );
            AssetDatabase.SaveAssets();
        }

        private static void ArrangeEntranceDisplay(GameObject interior)
        {
            Transform root = interior.transform;
            root.Find("RainyDaySign").localPosition = RainyDaySignPosition;
            Transform openUmbrella = root.Find("OpenUmbrella");
            if (openUmbrella != null)
                Object.DestroyImmediate(openUmbrella.gameObject);

            // 두 통을 창가에 나란히 붙여 진열대 앞 통로를 확보한다.
            Transform[] bins = root.Cast<Transform>()
                .Where(child => child.name == "UmbrellaBin")
                .OrderBy(child => child.localPosition.x)
                .ToArray();
            for (int i = 0; i < bins.Length; i++)
                bins[i].localPosition = UmbrellaBinPositions[i];
        }

        /// <summary>상점 씬에 드럭스토어 실내가 없으면 놓고, 예전 초록 우산 선반은 입구 우산 통으로 대신한다.</summary>
        public static void Apply(Scene scene)
        {
            Transform environment = BuilderTools.EnsureGroups(scene).Environment;
            RemoveBlockoutUmbrellas(environment);
            string prefabPath = PrefabFolder + "/" + InteriorName + ".prefab";
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(prefabPath, BuildInterior);
            // 천장 충돌과 정문을 넣기 전에 만든 프리팹도 디자인은 그대로 두고 빠진 부분만 더한다.
            BuilderTools.UpdatePrefabIfExists(prefabPath, AddMissingParts);
            DressEntranceDoor(scene);
            if (environment.Find(InteriorName) != null)
                return;
            BuilderTools.InstantiatePrefab(prefab, environment, Vector3.zero, Quaternion.identity);
        }

        private static void AddMissingParts(GameObject interior)
        {
            BlockCameraAtCeiling(interior);
            if (interior.transform.Find("DrugstoreEntrance") == null)
                Piece("DrugstoreEntrance", BuildEntrance, interior.transform, Vector3.zero, 0);
        }

        [MenuItem("Tools/A305/World/Apply Drugstore Displays")]
        public static void ApplyDisplays()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuilderTools.UpdatePrefabIfExists(
                PrefabFolder + "/CoffeeCounter.prefab",
                station =>
                {
                    FitCoffeeStation(station);
                    ObjectWordRegistration.ApplyToPrefab(
                        station,
                        PrefabFolder + "/CoffeeCounter.prefab"
                    );
                }
            );
            float checkoutTop;
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                Transform environment = scene
                    .GetRootGameObjects()
                    .Single(root => root.name == "Environment")
                    .transform;
                checkoutTop =
                    BoundsOf(environment.Find("TownShopInterior/ShopCounter").gameObject).max.y
                    - environment.Find(InteriorName + "/Checkout").position.y;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            string interiorPath = PrefabFolder + "/" + InteriorName + ".prefab";
            BuilderTools.UpdatePrefabIfExists(
                interiorPath,
                interior =>
                {
                    Transform pharmacy = interior.transform.Find("PharmacyCounter");
                    if (pharmacy != null)
                        Object.DestroyImmediate(pharmacy.gameObject);

                    Transform terminal = PlaceCardReader(
                        interior.transform.Find("Checkout"),
                        checkoutTop
                    );
                    ObjectWordRegistration.ApplyToPrefab(terminal.gameObject, interiorPath);
                    foreach (
                        Transform sign in interior
                            .transform.Cast<Transform>()
                            .Where(child => child.name == "AisleSign")
                    )
                        AddAisleSignBackLabel(sign);
                }
            );
            AssetDatabase.SaveAssets();
        }

        // 천장이 카메라를 막지 않으면 내려다볼 때 카메라가 천장 위로 빠져, 해를 받은 천장 윗면이 화면을 하얗게 덮는다.
        private static void BlockCameraAtCeiling(GameObject interior)
        {
            GameObject ceiling = interior.transform.Find("DrugstoreShell/Ceiling").gameObject;
            BuilderTools.GetOrAddComponent<BoxCollider>(ceiling);
        }

        // 마을로 나가는 문은 공용 출입구 프리팹의 갈색 문판이다. 처음 한 번만 정문 틀에 맞는 크기의 바깥 햇빛이 비치는
        // 유리문으로 바꾸고, 디자인에서 다시 칠했으면 그대로 둔다.
        internal static void DressEntranceDoor(Scene scene)
        {
            SpaceDoorway door = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(doorway => doorway.DoorwayId == "TownDoor");
            Transform visual = door.transform.Find("Visual");
            // 이 실내는 PUSH 표지와 밀대를 따로 제작했으므로 공용 당김 손잡이가 겹치지 않게 한다.
            Transform sharedFrame = visual.Find("ShopEntrance");
            if (sharedFrame != null && sharedFrame.gameObject.activeSelf)
            {
                sharedFrame.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sharedFrame.gameObject);
            }
            Renderer panel = visual.Find("DoorPanel").GetComponent<Renderer>();
            if (panel.sharedMaterial.name != "Door")
                return;
            visual.localScale = new Vector3(1.25f, 1.1f, 1);
            panel.sharedMaterial = DaylightGlass();
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        }

        private static void RemoveBlockoutUmbrellas(Transform environment)
        {
            Transform blockout = environment.Find("TownShopInterior");
            if (blockout == null)
                return;
            foreach (
                string name in new[] { "UmbrellaShelf", "Umbrella0", "Umbrella1", "Umbrella2" }
            )
            {
                Transform old = blockout.Find(name);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
        }

        // 좌표는 상점 씬 기준이다. 입구는 -Z(z=-7), 계산대는 z=2.4, 직원 뒤 벽은 z=7, 양옆 벽은 x=±6이다.
        private static GameObject BuildInterior()
        {
            var root = new GameObject(InteriorName);
            Transform t = root.transform;
            Shell(t);

            for (int i = 0; i < 5; i++)
                Piece(
                    "DrinkCooler",
                    BuildDrinkCooler,
                    t,
                    new Vector3(5.45f, 0, -4.6f + i * 1.15f),
                    -90
                );
            Piece("IceCreamFreezer", BuildFreezer, t, new Vector3(4.6f, 0, -6.15f), 0);

            Piece(
                "SnackShelf",
                () => BuildShelf("SnackShelf", Snacks),
                t,
                new Vector3(-1.2f, 0, 6.65f),
                0
            );
            Piece(
                "SnackShelf",
                () => BuildShelf("SnackShelf", Snacks),
                t,
                new Vector3(1.2f, 0, 6.65f),
                0
            );
            Piece(
                "PantryShelf",
                () => BuildShelf("PantryShelf", Pantry),
                t,
                new Vector3(3.7f, 0, 6.65f),
                0
            );
            Piece(
                "HealthShelf",
                () => BuildShelf("HealthShelf", null),
                t,
                new Vector3(-3.7f, 0, 6.65f),
                0
            );
            Piece(
                "HealthShelf",
                () => BuildShelf("HealthShelf", null),
                t,
                new Vector3(-5.55f, 0, 5.3f),
                -90
            );
            foreach (float z in new[] { -3.45f, -2.35f })
            {
                Piece(
                    "SnackShelf",
                    () => BuildShelf("SnackShelf", Snacks),
                    t,
                    new Vector3(-3.25f, 0, z),
                    90
                );
                Piece(
                    "SnackShelf",
                    () => BuildShelf("SnackShelf", Snacks),
                    t,
                    new Vector3(-2.55f, 0, z),
                    -90
                );
                Piece(
                    "HealthShelf",
                    () => BuildShelf("HealthShelf", null),
                    t,
                    new Vector3(2.55f, 0, z),
                    90
                );
                Piece(
                    "PantryShelf",
                    () => BuildShelf("PantryShelf", Pantry),
                    t,
                    new Vector3(3.25f, 0, z),
                    -90
                );
            }
            AisleSign("SNACKS & CANDY", t, new Vector3(-2.9f, 3.05f, -4.15f));
            AisleSign("HEALTH & GROCERY", t, new Vector3(2.9f, 3.05f, -4.15f));

            Piece("CoffeeCounter", BuildCoffeeCounter, t, new Vector3(-5.5f, 0, 1.9f), -90);
            Piece("ProduceStand", BuildProduceStand, t, new Vector3(-4.15f, 0, -5.45f), 0);

            Piece("UmbrellaBin", () => BuildUmbrellaBin(7), t, UmbrellaBinPositions[0], 0);
            Piece("UmbrellaBin", () => BuildUmbrellaBin(7), t, UmbrellaBinPositions[1], 140);
            Piece("RainyDaySign", BuildRainyDaySign, t, RainyDaySignPosition, 0);

            Model(
                Furniture + "rugDoormat.obj",
                "DoorMat",
                t,
                new Vector3(0, .01f, -6.1f),
                0,
                1.6f,
                Fit.Length
            );
            Model(
                Furniture + "pottedPlant.obj",
                "PottedPlant",
                t,
                new Vector3(5.4f, 0, 5.3f),
                40,
                1.1f,
                Fit.Height
            );
            Model(
                Furniture + "pottedPlant.obj",
                "PottedPlant",
                t,
                new Vector3(-5.45f, 0, -6.35f),
                0,
                1.1f,
                Fit.Height
            );
            Model(
                Furniture + "cardboardBoxClosed.obj",
                "StockBox",
                t,
                new Vector3(5.2f, 0, 3.9f),
                20,
                .6f,
                Fit.Length
            );
            Model(
                Furniture + "cardboardBoxOpen.obj",
                "StockBox",
                t,
                new Vector3(4.6f, 0, 4.3f),
                -15,
                .55f,
                Fit.Length
            );
            Model(
                Furniture + "trashcan.obj",
                "TrashCan",
                t,
                new Vector3(-1.7f, 0, -6.35f),
                0,
                .7f,
                Fit.Height
            );

            Checkout(t);
            Piece("DrugstoreEntrance", BuildEntrance, t, Vector3.zero, 0);
            return root;
        }

        // ---------- 매장 껍데기 ----------

        // 체크무늬 비닐 타일 바닥, 낮은 천장과 형광등, 벽 위 초록 띠와 코너 이름
        private static void Shell(Transform root)
        {
            Transform shell = BuilderTools.CreateEmpty(
                "DrugstoreShell",
                root,
                Vector3.zero,
                Quaternion.identity
            );
            Prim(
                PrimitiveType.Quad,
                "Floor",
                shell,
                new Vector3(0, .005f, 0),
                new Vector3(11.8f, 13.8f, 1),
                FloorMaterial(),
                Quaternion.Euler(90, 0, 0)
            );

            // 천장은 그림자를 드리우지 않아 기존 해가 실내를 계속 비춘다. 카메라를 막는 충돌은 Apply에서 더한다.
            GameObject ceiling = Prim(
                PrimitiveType.Cube,
                "Ceiling",
                shell,
                new Vector3(0, 3.95f, 0),
                new Vector3(12f, .1f, 14f),
                Paint("DrugstoreCeiling", "#ECE8E0")
            );
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            foreach (float x in new[] { -2.6f, 2.6f })
            foreach (float z in new[] { -3.6f, .4f, 4.4f })
                Prim(
                        PrimitiveType.Cube,
                        "LightPanel",
                        shell,
                        new Vector3(x, 3.88f, z),
                        new Vector3(1.4f, .04f, .55f),
                        Glow("DrugstoreLightPanel", "#FFF8E8")
                    )
                    .GetComponent<Renderer>()
                    .shadowCastingMode = ShadowCastingMode.Off;

            Material band = Paint("DrugstoreBand", "#2E6B57");
            Material baseboard = Paint("DrugstoreBaseboard", "#D9573F");
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(
                    PrimitiveType.Cube,
                    "WallBand",
                    shell,
                    new Vector3(side * 5.87f, 3.2f, 0),
                    new Vector3(.04f, .55f, 13.6f),
                    band
                );
                Prim(
                    PrimitiveType.Cube,
                    "Skirting",
                    shell,
                    new Vector3(side * 5.87f, .14f, 0),
                    new Vector3(.04f, .28f, 13.6f),
                    baseboard
                );
                Prim(
                    PrimitiveType.Cube,
                    "WallBand",
                    shell,
                    new Vector3(side * 4.5f, 3.2f, 6.87f),
                    new Vector3(2.8f, .55f, .04f),
                    band
                );
            }
            Prim(
                PrimitiveType.Cube,
                "Skirting",
                shell,
                new Vector3(0, .14f, 6.87f),
                new Vector3(11.7f, .28f, .04f),
                baseboard
            );

            Color cream = Hex("#FFF6E3");
            SectionSign("COLD DRINKS", shell, new Vector3(5.83f, 3.2f, -2.3f), 90, .3f, cream);
            SectionSign(
                "HOT COFFEE · HOT DOGS",
                shell,
                new Vector3(-5.83f, 3.2f, 1.9f),
                -90,
                .27f,
                cream
            );
            SectionSign("PHARMACY", shell, new Vector3(-4.5f, 3.2f, 6.83f), 0, .3f, cream);
            SectionSign("GROCERY", shell, new Vector3(4.5f, 3.2f, 6.83f), 0, .3f, cream);
        }

        private static Material FloorMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath) == null)
                WriteFloorTexture();
            Material material = Paint("DrugstoreFloorTiles", "#FFFFFF");
            if (material.GetTexture("_BaseMap") == null)
            {
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath)
                );
                material.SetTextureScale("_BaseMap", new Vector2(2.7f, 3.2f));
                material.SetFloat("_Smoothness", .45f);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static void WriteFloorTexture()
        {
            const int size = 512,
                tiles = 8;
            Color light = Hex("#F4F1EA"),
                dark = Hex("#D6D1C8"),
                grout = Hex("#BEBAB2");
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            int cell = size / tiles;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool line = x % cell < 2 || y % cell < 2;
                bool even = (x / cell + y / cell) % 2 == 0;
                texture.SetPixel(
                    x,
                    y,
                    line ? grout
                        : even ? light
                        : dark
                );
            }
            BuilderTools.EnsureFolder(ArtFolder);
            File.WriteAllBytes(FloorTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FloorTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FloorTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        // 글자 재질은 뒷면이 비쳐 보여서 매장 안에서 보는 면에만 쓴다.
        private static void SectionSign(
            string value,
            Transform parent,
            Vector3 position,
            float yaw,
            float size,
            Color color
        )
        {
            TownBlockoutGeometry
                .Label("SectionSign", value, parent, position, new Vector3(0, yaw, 0), size)
                .color = color;
        }

        private static void AisleSign(string value, Transform parent, Vector3 position)
        {
            Transform sign = BuilderTools.CreateEmpty(
                "AisleSign",
                parent,
                position,
                Quaternion.identity
            );
            Prim(
                    PrimitiveType.Cube,
                    "Board",
                    sign,
                    Vector3.zero,
                    new Vector3(1.7f, .42f, .05f),
                    Paint("DrugstoreAisleBlue", "#2F5D8A")
                )
                .GetComponent<Renderer>()
                .shadowCastingMode = ShadowCastingMode.Off;
            foreach (float x in new[] { -.7f, .7f })
                Prim(
                    PrimitiveType.Cylinder,
                    "Rod",
                    sign,
                    new Vector3(x, .45f, 0),
                    new Vector3(.015f, .45f, .015f),
                    Paint("DrugstoreSteel", "#9AA3A8")
                );
            TownBlockoutGeometry
                .Label("Label", value, sign, new Vector3(0, 0, -.035f), Vector3.zero, .17f)
                .color = Color.white;
            AddAisleSignBackLabel(sign);
        }

        private static void AddAisleSignBackLabel(Transform sign)
        {
            TMP_Text front = sign.Find("Label").GetComponent<TMP_Text>();
            if (sign.Find("BackLabel") != null)
                return;

            TMP_Text back = Object.Instantiate(front, sign);
            back.name = "BackLabel";
            back.transform.SetLocalPositionAndRotation(
                new Vector3(0, 0, .035f),
                Quaternion.Euler(0, 180, 0)
            );
        }

        // ---------- 조각 프리팹 ----------

        private static GameObject BuildDrinkCooler()
        {
            var root = new GameObject("DrinkCooler");
            Model(FridgeModel, "Cooler", root.transform, Vector3.zero, 0, 2.05f, Fit.Height);
            AddBlocker(root);
            return root;
        }

        private static GameObject BuildFreezer()
        {
            var root = new GameObject("IceCreamFreezer");
            GameObject chest = Model(
                CoolerModel,
                "Chest",
                root.transform,
                Vector3.zero,
                0,
                1.4f,
                Fit.Length
            );
            float top = BoundsOf(chest).max.y;
            var random = new Random(11);
            for (int i = 0; i < 6; i++)
            {
                var spot = new Vector3(
                    (float)(random.NextDouble() - .5) * 1.0f,
                    top,
                    (float)(random.NextDouble() - .5) * .35f
                );
                Good(i % 2 == 0 ? "iceCream" : "lollypop", root.transform, spot, random.Next(360));
            }
            AddBlocker(root);
            return root;
        }

        // 앞면은 로컬 -Z다. items가 없으면 미국 약국 선반처럼 주황 약병과 상자를 채운다.
        private static GameObject BuildShelf(string name, string[] items)
        {
            var root = new GameObject(name);
            GameObject frame = Model(
                ShelfModel,
                "Frame",
                root.transform,
                Vector3.zero,
                0,
                1.6f,
                Fit.Height
            );
            Bounds bounds = BoundsOf(frame);
            List<float> levels = ShelfLevels(frame, bounds);
            var random = new Random(name.Length * 31 + name[0]);
            for (int level = 0; level < levels.Count; level++)
            {
                float gap =
                    (level + 1 < levels.Count ? levels[level + 1] : bounds.max.y + .4f)
                    - levels[level];
                float x = -bounds.extents.x + .1f;
                string item =
                    items != null
                        ? items[(level + random.Next(items.Length)) % items.Length]
                        : null;
                while (x < bounds.extents.x - .1f)
                {
                    var spot = new Vector3(
                        bounds.center.x + x,
                        levels[level],
                        bounds.center.z - bounds.extents.z * .2f
                    );
                    float width;
                    if (items == null)
                        width = PharmacyItem(root.transform, spot, gap, random);
                    else
                    {
                        GameObject good = Good(
                            item,
                            root.transform,
                            spot,
                            180 + random.Next(-8, 8)
                        );
                        if (BoundsOf(good).size.y > gap * .8f)
                            Place(good, spot, gap * .8f, Fit.Height);
                        Bounds placed = BoundsOf(good);
                        width = Mathf.Max(placed.size.x, .08f);
                        if (random.NextDouble() < .25)
                            item = items[random.Next(items.Length)];
                    }
                    x += width + .03f;
                }
            }
            AddBlocker(root);
            return root;
        }

        // 선반 판 윗면 높이를 위에서 아래로 차례로 찾는다.
        private static List<float> ShelfLevels(GameObject frame, Bounds bounds)
        {
            var colliders = frame
                .GetComponentsInChildren<MeshFilter>()
                .Select(filter => filter.gameObject.AddComponent<MeshCollider>())
                .ToList();
            Physics.SyncTransforms();
            var levels = new List<float>();
            foreach (
                Vector3 offset in new[]
                {
                    Vector3.zero,
                    new Vector3(.15f, 0, .05f),
                    new Vector3(-.15f, 0, -.05f),
                }
            )
            {
                float y = bounds.max.y + .05f;
                while (y > bounds.min.y + .03f)
                {
                    var ray = new Ray(
                        new Vector3(bounds.center.x, y, bounds.center.z) + offset,
                        Vector3.down
                    );
                    float nearest = float.MaxValue;
                    foreach (MeshCollider collider in colliders)
                        if (collider.Raycast(ray, out RaycastHit hit, y - bounds.min.y + .1f))
                            nearest = Mathf.Min(nearest, hit.distance);
                    if (nearest == float.MaxValue)
                        break;
                    float top = y - nearest;
                    if (
                        top < bounds.max.y - .1f
                        && top > bounds.min.y + .02f
                        && !levels.Any(l => Mathf.Abs(l - top) < .08f)
                    )
                        levels.Add(top);
                    y = top - .03f;
                }
            }
            foreach (MeshCollider collider in colliders)
                Object.DestroyImmediate(collider);
            levels.Sort();
            return levels;
        }

        private static float PharmacyItem(Transform parent, Vector3 spot, float gap, Random random)
        {
            if (random.NextDouble() < .6)
            {
                float h = Mathf.Min(.14f, gap * .7f);
                Transform bottle = BuilderTools.CreateEmpty(
                    "PillBottle",
                    parent,
                    spot,
                    Quaternion.identity
                );
                Prim(
                    PrimitiveType.Cylinder,
                    "Body",
                    bottle,
                    new Vector3(0, h / 2, 0),
                    new Vector3(.075f, h / 2, .075f),
                    Paint("DrugstorePillAmber", "#E8892B")
                );
                Prim(
                    PrimitiveType.Cylinder,
                    "Cap",
                    bottle,
                    new Vector3(0, h + .012f, 0),
                    new Vector3(.08f, .012f, .08f),
                    Paint("DrugstoreWhite", "#F4F6F8")
                );
                return .1f;
            }
            float boxHeight = Mathf.Min(.2f, gap * .75f);
            Material box =
                random.NextDouble() < .5
                    ? Paint("DrugstoreWhite", "#F4F6F8")
                    : Paint("DrugstoreMedicineBlue", "#9CC8E8");
            Prim(
                PrimitiveType.Cube,
                "MedicineBox",
                parent,
                spot + new Vector3(0, boxHeight / 2, 0),
                new Vector3(.11f, boxHeight, .07f),
                box
            );
            return .11f;
        }

        // 서쪽 벽의 커피 머신 두 대, 컵, 핫도그 그릴
        private static GameObject BuildCoffeeCounter()
        {
            var root = new GameObject("CoffeeCounter");
            foreach (float x in new[] { -.8f, .8f })
                Model(
                    Furniture + "kitchenCabinet.obj",
                    "Cabinet",
                    root.transform,
                    new Vector3(x, 0, 0),
                    0,
                    1.6f,
                    Fit.Length
                );
            foreach (float x in new[] { -1.05f, -.45f })
                Model(
                    Furniture + "kitchenCoffeeMachine.obj",
                    "CoffeeMachine",
                    root.transform,
                    new Vector3(x, 0, .1f),
                    0,
                    .5f,
                    Fit.Height
                );
            for (int i = 0; i < 6; i++)
                Good("cup", root.transform, Vector3.zero, 0);
            Prim(
                PrimitiveType.Cube,
                "HotDogRoller",
                root.transform,
                new Vector3(.95f, 0, 0),
                new Vector3(.6f, .12f, .38f),
                Paint("DrugstoreSteel", "#9AA3A8")
            );
            for (int i = 0; i < 4; i++)
                Good("hotDog", root.transform, Vector3.zero, 0);
            FitCoffeeStation(root);
            AddBlocker(root);
            return root;
        }

        private static void FitCoffeeStation(GameObject station)
        {
            Transform root = station.transform;
            Transform[] props = root.Cast<Transform>().ToArray();
            float cabinetHeight = CoffeeCounterHeight - CoffeeCountertopThickness;
            foreach (Transform cabinet in props.Where(child => child.name == "Cabinet"))
            {
                Vector3 scale = cabinet.localScale;
                scale.y *= cabinetHeight / BoundsOf(cabinet.gameObject).size.y;
                cabinet.localScale = scale;
                SetModelBottom(cabinet, 0f);
            }

            // 이어진 상판을 두어 수납장 사이 틈 위에도 컵과 기계를 올릴 수 있게 한다.
            if (root.Find("CoffeeCountertop") == null)
                Prim(
                    PrimitiveType.Cube,
                    "CoffeeCountertop",
                    root,
                    new Vector3(0, CoffeeCounterHeight - CoffeeCountertopThickness / 2f, 0),
                    new Vector3(3.2f, CoffeeCountertopThickness, 1.6f),
                    Paint("DrugstoreCoffeeCountertop", "#9C775D")
                );

            foreach (Transform machine in props.Where(child => child.name == "CoffeeMachine"))
                SetModelBottom(machine, CoffeeCounterHeight);

            int cupIndex = 0;
            foreach (Transform cup in props.Where(child => child.name == "cup"))
            {
                SetModelBottom(
                    cup,
                    new Vector3(
                        .06f + (cupIndex / 2) * .20f,
                        CoffeeCounterHeight,
                        -.13f + (cupIndex % 2) * .24f
                    )
                );
                cupIndex++;
            }

            Transform roller = root.Find("HotDogRoller");
            SetModelBottom(roller, CoffeeCounterHeight);
            float rollerTop = BoundsOf(roller.gameObject).max.y - root.position.y;
            int hotDogIndex = 0;
            foreach (Transform hotDog in props.Where(child => child.name == "hotDog"))
            {
                hotDog.localRotation = Quaternion.Euler(0, 90, 0);
                SetModelBottom(hotDog, new Vector3(.74f + hotDogIndex * .14f, rollerTop, 0));
                hotDogIndex++;
            }

            NorthBlockAssets.Bind(station, "coffee_station");
        }

        private static void SetModelBottom(Transform model, float height)
        {
            float bottom = BoundsOf(model.gameObject).min.y - model.parent.position.y;
            model.localPosition += Vector3.up * (height - bottom);
        }

        private static void SetModelBottom(Transform model, Vector3 bottomCenter)
        {
            Bounds bounds = BoundsOf(model.gameObject);
            Vector3 bottom = model.parent.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
            );
            model.localPosition += bottomCenter - bottom;
        }

        // 입구 쪽 과일·채소 상자 두 단
        private static GameObject BuildProduceStand()
        {
            var root = new GameObject("ProduceStand");
            string[][] fillings =
            {
                new[] { "apple", "orange" },
                new[] { "banana" },
                new[] { "carrot", "corn" },
                new[] { "cabbage", "broccoli" },
                new[] { "orange", "apple" },
            };
            var random = new Random(5);
            for (int i = 0; i < fillings.Length; i++)
            {
                bool lower = i < 3;
                var spot = new Vector3(
                    lower ? -.62f + i * .62f : -.31f + (i - 3) * .62f,
                    lower ? 0 : .3f,
                    lower ? 0 : .22f
                );
                GameObject crate = Model(
                    CrateModel,
                    "ProduceCrate",
                    root.transform,
                    spot,
                    0,
                    .58f,
                    Fit.Length
                );
                Bounds box = BoundsOf(crate);
                for (int k = 0; k < 6; k++)
                {
                    var inside = new Vector3(
                        box.center.x + (float)(random.NextDouble() - .5) * box.size.x * .6f,
                        box.max.y - .08f,
                        box.center.z + (float)(random.NextDouble() - .5) * box.size.z * .6f
                    );
                    Good(
                        fillings[i][k % fillings[i].Length],
                        crate.transform,
                        crate.transform.InverseTransformPoint(inside),
                        random.Next(360)
                    );
                }
            }
            AddBlocker(root);
            return root;
        }

        // 손잡이가 위로 오게 꽂아 둔 우산 통. 미국 드럭스토어는 비 오는 날 입구에 이렇게 둔다.
        private static GameObject BuildUmbrellaBin(int count)
        {
            var root = new GameObject("UmbrellaBin");
            Transform t = root.transform;
            Prim(
                PrimitiveType.Cylinder,
                "Bin",
                t,
                new Vector3(0, .28f, 0),
                new Vector3(.5f, .28f, .5f),
                Paint("DrugstoreGalvanized", "#8C959C")
            );
            string[] colors =
            {
                "#1F3A5F",
                "#C8102E",
                "#2E6B57",
                "#F2C230",
                "#2A2A2A",
                "#6A4C93",
                "#E07A5F",
            };
            Material handle = Paint("DrugstoreUmbrellaHandle", "#3B2A20");
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                var foot = new Vector3(Mathf.Cos(angle) * .12f, 0, Mathf.Sin(angle) * .12f);
                Quaternion tilt = Quaternion.Euler(
                    Mathf.Sin(angle) * 8f,
                    0,
                    -Mathf.Cos(angle) * 8f
                );
                Material canopy = Paint(
                    "DrugstoreUmbrella" + colors[i % colors.Length].TrimStart('#'),
                    colors[i % colors.Length]
                );
                Transform umbrella = BuilderTools.CreateEmpty("Umbrella", t, foot, tilt);
                Prim(
                    PrimitiveType.Capsule,
                    "Canopy",
                    umbrella,
                    new Vector3(0, .66f, 0),
                    new Vector3(.11f, .4f, .11f),
                    canopy
                );
                Prim(
                    PrimitiveType.Cylinder,
                    "Shaft",
                    umbrella,
                    new Vector3(0, 1.1f, 0),
                    new Vector3(.022f, .08f, .022f),
                    handle
                );
                Quaternion hook = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
                Prim(
                    PrimitiveType.Cylinder,
                    "Hook",
                    umbrella,
                    new Vector3(0, 1.2f, 0) + hook * new Vector3(.05f, 0, 0),
                    new Vector3(.022f, .05f, .022f),
                    handle,
                    hook * Quaternion.Euler(0, 0, 90)
                );
                Prim(
                    PrimitiveType.Cylinder,
                    "HookEnd",
                    umbrella,
                    new Vector3(0, 1.15f, 0) + hook * new Vector3(.1f, 0, 0),
                    new Vector3(.022f, .05f, .022f),
                    handle,
                    hook
                );
            }
            AddBlocker(root);
            ConnectUmbrellaWord(root);
            return root;
        }

        // 우산 단어는 미션 상점 Builder가 만든 공용 단어를 그대로 쓴다.
        private static void ConnectUmbrellaWord(GameObject root)
        {
            var word = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                ObjectWordAuthoring.WordsFolder + "/umbrella.asset"
            );
            if (word == null)
                throw new InvalidOperationException(
                    "우산 단어가 없습니다. 먼저 Tools/A305/Mission/Build First Day Journey를 실행합니다."
                );
            BuilderTools.SetReference(root.AddComponent<HoverableObject>(), "word", word);
        }

        private static GameObject BuildRainyDaySign()
        {
            var root = new GameObject("RainyDaySign");
            Transform t = root.transform;
            Prim(
                PrimitiveType.Cylinder,
                "Pole",
                t,
                new Vector3(0, .8f, 0),
                new Vector3(.04f, .8f, .04f),
                Paint("DrugstoreDarkSteel", "#3A3F44")
            );
            Prim(
                PrimitiveType.Cube,
                "Board",
                t,
                new Vector3(0, 1.75f, 0),
                new Vector3(.78f, .5f, .04f),
                Paint("DrugstoreSignYellow", "#F2C230")
            );
            TownBlockoutGeometry
                .Label(
                    "Label",
                    "RAINY DAY?\nUMBRELLAS",
                    t,
                    new Vector3(0, 1.75f, .04f),
                    new Vector3(0, 180, 0),
                    .15f
                )
                .color = Hex("#2A2A2A");
            FitRainyDaySignLabel(root);
            return root;
        }

        private static void FitRainyDaySignLabel(GameObject sign)
        {
            var label = sign.transform.Find("Label").GetComponent<TextMeshPro>();
            Vector3 boardSize = sign.transform.Find("Board").localScale;
            // 공용 Label의 넓은 기본 영역 대신 실제 판의 안쪽 여백 안에서 두 줄을 맞춘다.
            label.rectTransform.sizeDelta = new Vector2(boardSize.x - .12f, boardSize.y - .12f);
            label.enableAutoSizing = true;
            label.fontSizeMin = .5f;
            label.fontSizeMax = 1.5f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // 앞벽(z=-7)의 문 구멍(x=-1~1, 높이 4m)을 미국 드럭스토어 정면처럼 알루미늄 문틀과 문 위 벽으로 채운다.
        // 양옆에는 바깥 햇빛이 비치는 진열창을 두고, 옆·뒷벽처럼 초록 띠와 걸레받이를 두른다.
        // 문틀과 문 위 벽은 카메라가 틈으로 빠지지 않게 충돌을 둔다.
        private static GameObject BuildEntrance()
        {
            var root = new GameObject("DrugstoreEntrance");
            Transform t = root.transform;
            Material frame = Paint("DrugstoreDarkSteel", "#3A3F44");
            Material daylight = DaylightGlass();

            Solid("WallOverDoor", t, new Vector3(0, 3.225f, -7), new Vector3(2, 1.55f, .2f));
            foreach (float side in new[] { -1f, 1f })
                Solid(
                    "DoorFrame",
                    t,
                    new Vector3(side * .875f, 1.225f, -6.85f),
                    new Vector3(.25f, 2.45f, .5f),
                    frame
                );
            Solid("DoorFrame", t, new Vector3(0, 2.45f, -6.85f), new Vector3(2, .14f, .5f), frame);

            // 매장 안쪽은 +Z다. 유리문 안쪽 면(z=-6.7) 바로 앞에 문짝 테두리·손잡이·발판을 붙인다.
            const float leafZ = -6.67f;
            // 단어 연결 규칙에서 DoorPanel로 시작하는 이름은 "door"로 묶인다.
            Transform trim = BuilderTools.CreateEmpty(
                "DoorPanelTrim",
                t,
                Vector3.zero,
                Quaternion.identity
            );
            Prim(
                PrimitiveType.Cube,
                "MeetingStile",
                trim,
                new Vector3(0, 1.2f, leafZ),
                new Vector3(.08f, 2.4f, .04f),
                frame
            );
            Prim(
                PrimitiveType.Cube,
                "KickPlate",
                trim,
                new Vector3(0, .13f, leafZ),
                new Vector3(1.5f, .26f, .04f),
                frame
            );
            Prim(
                PrimitiveType.Cube,
                "TopRail",
                trim,
                new Vector3(0, 2.34f, leafZ),
                new Vector3(1.5f, .08f, .04f),
                frame
            );
            Material steel = Paint("DrugstoreSteel", "#9AA3A8");
            foreach (float side in new[] { -1f, 1f })
                Prim(
                    PrimitiveType.Cube,
                    "PushBar",
                    trim,
                    new Vector3(side * .37f, 1.05f, leafZ + .04f),
                    new Vector3(.5f, .05f, .05f),
                    steel
                );
            // 매장 안에서 -Z를 보며 읽으므로 글자를 뒤로 돌린다.
            foreach (float side in new[] { -1f, 1f })
                TownBlockoutGeometry
                    .Label(
                        "Label",
                        "PUSH",
                        trim,
                        new Vector3(side * .37f, 1.4f, leafZ + .03f),
                        new Vector3(0, 180, 0),
                        .1f
                    )
                    .color = Hex("#2A2A2A");

            // 앞벽 안쪽 면은 z=-6.9다.
            foreach (float side in new[] { -1f, 1f })
                ShopWindow(t, new Vector3(side * 3.5f, 1.75f, -6.89f), frame, daylight);
            Prim(
                PrimitiveType.Cube,
                "WallBand",
                t,
                new Vector3(0, 3.2f, -6.87f),
                new Vector3(11.7f, .55f, .04f),
                Paint("DrugstoreBand", "#2E6B57")
            );
            SectionSign(
                "THANK YOU · COME AGAIN",
                t,
                new Vector3(0, 3.2f, -6.83f),
                180,
                .27f,
                Hex("#FFF6E3")
            );
            Material baseboard = Paint("DrugstoreBaseboard", "#D9573F");
            foreach (float side in new[] { -1f, 1f })
                Prim(
                    PrimitiveType.Cube,
                    "Skirting",
                    t,
                    new Vector3(side * 3.43f, .14f, -6.87f),
                    new Vector3(4.85f, .28f, .04f),
                    baseboard
                );
            return root;
        }

        // 벽에 붙인 진열창. 유리 너머가 비어 있으므로 바깥 햇빛 색으로 밝게 칠한다.
        private static void ShopWindow(
            Transform parent,
            Vector3 center,
            Material frame,
            Material daylight
        )
        {
            Transform window = BuilderTools.CreateEmpty(
                "ShopWindow",
                parent,
                center,
                Quaternion.identity
            );
            const float width = 3.2f,
                height = 1.7f;
            Prim(
                PrimitiveType.Cube,
                "Glass",
                window,
                Vector3.zero,
                new Vector3(width, height, .02f),
                daylight
            );
            foreach (float y in new[] { -height / 2, height / 2 })
                Prim(
                    PrimitiveType.Cube,
                    "Rail",
                    window,
                    new Vector3(0, y, .02f),
                    new Vector3(width + .1f, .1f, .05f),
                    frame
                );
            foreach (float x in new[] { -width / 2, 0, width / 2 })
                Prim(
                    PrimitiveType.Cube,
                    "Mullion",
                    window,
                    new Vector3(x, 0, .02f),
                    new Vector3(.1f, height, .05f),
                    frame
                );
        }

        // 보이는 겉모양과 같은 크기의 충돌을 가진 상자. 기본 재질은 매장 벽과 같은 크림색이다.
        private static Transform Solid(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material = null
        )
        {
            GameObject shape = Prim(
                PrimitiveType.Cube,
                name,
                parent,
                position,
                scale,
                material != null ? material : MainstreetArtKit.Cream
            );
            shape.AddComponent<BoxCollider>();
            return shape.transform;
        }

        // 계산대 위: 금전등록기, 카드 결제기, 빨간 과자 진열대, 화분
        private static void Checkout(Transform root)
        {
            Transform checkout = BuilderTools.CreateEmpty(
                "Checkout",
                root,
                Vector3.zero,
                Quaternion.identity
            );
            var register = AssetDatabase.LoadAssetAtPath<GameObject>(
                TinyTreats + "CashRegister.prefab"
            );
            if (register != null)
                Place(
                    BuilderTools.InstantiatePrefab(
                        register,
                        checkout,
                        Vector3.zero,
                        Quaternion.Euler(0, 180, 0)
                    ),
                    new Vector3(1.2f, CounterTop, 2.35f),
                    .42f,
                    Fit.Height
                );
            PlaceCardReader(checkout, CounterTop);
            Transform rack = BuilderTools.CreateEmpty(
                "CandyRack",
                checkout,
                new Vector3(-1.0f, CounterTop, 2.05f),
                Quaternion.identity
            );
            Prim(
                PrimitiveType.Cube,
                "Rack",
                rack,
                new Vector3(0, .18f, 0),
                new Vector3(.9f, .36f, .22f),
                Paint("DrugstoreRed", "#C8102E")
            );
            for (int row = 0; row < 2; row++)
            for (int i = 0; i < 5; i++)
                Good(
                    i % 2 == 0 ? "candyBarWrapper" : "chocolateWrapper",
                    rack,
                    new Vector3(-.36f + i * .18f, .18f + row * .17f, -.13f),
                    180
                );
            var succulent = AssetDatabase.LoadAssetAtPath<GameObject>(
                TinyTreats + "Succulent.prefab"
            );
            if (succulent != null)
                Place(
                    BuilderTools.InstantiatePrefab(
                        succulent,
                        checkout,
                        Vector3.zero,
                        Quaternion.Euler(0, 160, 0)
                    ),
                    new Vector3(2.1f, CounterTop, 2.35f),
                    .4f,
                    Fit.Height
                );
        }

        private static Transform PlaceCardReader(Transform checkout, float counterTop)
        {
            var position = new Vector3(.55f, counterTop, 2.15f);
            Transform reader = checkout.Find("CardReader");
            if (reader != null)
            {
                reader.localPosition = position;
                return reader;
            }

            reader = BuilderTools.CreateEmpty(
                "CardReader",
                checkout,
                position,
                Quaternion.identity
            );
            Prim(
                PrimitiveType.Cube,
                "Body",
                reader,
                new Vector3(0, .045f, 0),
                new Vector3(.21f, .09f, .31f),
                Paint("DrugstoreTerminal", "#2A2E33")
            );
            Prim(
                PrimitiveType.Cube,
                "Screen",
                reader,
                new Vector3(0, .09f, .065f),
                new Vector3(.15f, .012f, .11f),
                Glow("DrugstoreScreen", "#7CC6F2")
            );
            Material keypad = Paint("DrugstoreSteel", "#9AA3A8");
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
                Prim(
                    PrimitiveType.Cube,
                    "Key",
                    reader,
                    new Vector3((column - 1) * .048f, .09f, -.025f - row * .038f),
                    new Vector3(.033f, .012f, .024f),
                    keypad
                );

            NorthBlockAssets.Bind(reader.gameObject, "card_reader");
            return reader;
        }

        // ---------- 도우미 ----------

        private static GameObject Piece(
            string name,
            Func<GameObject> build,
            Transform parent,
            Vector3 position,
            float yaw
        )
        {
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(
                $"{PrefabFolder}/{name}.prefab",
                build
            );
            return BuilderTools.InstantiatePrefab(
                prefab,
                parent,
                position,
                Quaternion.Euler(0, yaw, 0)
            );
        }

        private static GameObject Good(
            string key,
            Transform parent,
            Vector3 bottomCenter,
            float yaw
        )
        {
            (Fit fit, float size) = Goods[key];
            return Model(
                Food + key + ".obj",
                key,
                parent,
                bottomCenter,
                yaw,
                size * GoodsScale,
                fit
            );
        }

        private static GameObject Model(
            string path,
            string name,
            Transform parent,
            Vector3 bottomCenter,
            float yaw,
            float size,
            Fit fit
        ) =>
            ThirdPartyModelKit.Model(
                path,
                name,
                parent,
                bottomCenter,
                yaw,
                size,
                fit,
                MaterialFolder
            );

        // 조각은 원점에서 만들므로 겉모양 범위가 곧 로컬 범위다. 플레이어가 통과하지 않게 막는다.
        private static void AddBlocker(GameObject root)
        {
            Bounds bounds = BoundsOf(root);
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = bounds.center - root.transform.position;
            blocker.size = bounds.size;
        }

        private static Material Paint(string name, string hex) =>
            BuilderTools.GetOrCreateMaterial(MaterialFolder, name, Hex(hex));

        private static Material Glow(string name, string hex)
        {
            Material material = Paint(name, hex);
            if (!material.IsKeywordEnabled("_EMISSION"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Hex(hex) * 1.4f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        // 유리 너머가 비어 있어 투명하게 둘 수 없으므로, 바깥 햇빛이 비치는 옅은 하늘색으로 은은하게 빛나게 한다.
        // 형광등 패널처럼 세게 빛나면 흰 판처럼 보인다.
        private static Material DaylightGlass()
        {
            Material material = Paint("DrugstoreDaylightGlass", "#B4D3E1");
            if (!material.IsKeywordEnabled("_EMISSION"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Hex("#B4D3E1") * .35f);
                material.SetFloat("_Smoothness", .85f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static GameObject Prim(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion? rotation = null
        )
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.SetLocalPositionAndRotation(position, rotation ?? Quaternion.identity);
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
