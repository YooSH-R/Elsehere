using System.Linq;
using A305.EditorTools.Common;
using A305.World;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>치수와 충돌 영역은 블록아웃에 맡기고, 외관과 가로 시설을 조립한다.</summary>
    internal static class MainstreetArtBuilder
    {
        internal static void Apply(Transform environment, TownBlockoutSettings settings)
        {
            Transform buildings = environment.Find("BuildingFootprints");
            DecorateShop(buildings.Find("Cafe"), settings.ShopFootprintMeters, settings);
            DecorateShop(buildings.Find("GeneralStore"), settings.ShopFootprintMeters, settings);
            DecorateShop(buildings.Find("Hotel"), settings.HotelFootprintMeters, settings);
            DecorateHome(buildings.Find("Home"), settings);
            Transform streets = Group("MainstreetArt", environment, Vector3.zero);
            BuildPaving(streets);
            BuildStreetFurniture(streets);
            BuildTruck(streets);
            MainstreetStreetDressing.Apply(environment, settings);
            MainstreetCafeKitBuilder.Apply(environment, settings);
            // Scale guides remain available in the hierarchy without covering the art review.
            environment.Find("ScaleAndDistanceMarkers").gameObject.SetActive(false);
        }

        private static void PrepareFacade(Transform building, Material wall)
        {
            string[] hidden =
            {
                "UpperWindow",
                "FloorBand",
                "Parapet",
                "Awning",
                "BuildingSign",
                "VerticalSign",
                "HotelLandmark",
                "DoorApproach",
            };
            foreach (Transform child in building)
            {
                if (hidden.Contains(child.name))
                    child.gameObject.SetActive(false);
                else if (
                    child.name.Contains("Wall")
                    || child.name.StartsWith("Facade")
                    || child.name == "DoorHeader"
                )
                    child.GetComponent<Renderer>().sharedMaterial = wall;
                else if (child.name.StartsWith("DoorFrame"))
                    child.GetComponent<Renderer>().sharedMaterial = Cream;
            }
        }

        private static void DecorateShop(
            Transform building,
            float width,
            TownBlockoutSettings settings
        )
        {
            bool store = building.name == "GeneralStore";
            bool hotel = building.name == "Hotel";
            PrepareFacade(
                building,
                store ? Color("ButterStuccoV2", "#F0D29C") : MainstreetReferenceKit.Brick
            );
            Transform art = Group("FacadeArt", building, Vector3.zero);
            float height = settings.ShopHeightMeters;
            float upperY = settings.GroundFloorHeightMeters + settings.UpperFloorHeightMeters / 2f;
            float doorX = building.Find("DoorCenter").localPosition.x;
            foreach (float y in new[] { settings.GroundFloorHeightMeters - 0.18f, height - 0.14f })
            {
                Block(
                    "CorniceFront",
                    art,
                    new Vector3(0, y, .13f),
                    new Vector3(width + .25f, .25f, .38f),
                    Cream
                );
                foreach (int side in new[] { -1, 1 })
                    Block(
                        "CorniceSide",
                        art,
                        new Vector3(side * width / 2, y, -width / 2),
                        new Vector3(.3f, .25f, width),
                        Cream
                    );
            }
            float[] upperXs = hotel ? new[] { -1.7f, 1.5f } : new[] { -1.8f, 1.8f };
            foreach (float x in upperXs)
            {
                Place(
                    "TallWindowV2",
                    art,
                    new Vector3(x, upperY, .12f),
                    MainstreetReferenceKit.TallWindow
                );
                Place(
                    "WindowFlowerBox",
                    art,
                    new Vector3(x, upperY - 1.18f, .36f),
                    () => Planter(true)
                );
            }
            for (float x = -width / 2 + 1f; x < width / 2; x += 2f)
                if (Mathf.Abs(x - doorX) > 1.8f)
                    Place(
                        "WarmShopWindowV2",
                        art,
                        new Vector3(x, 1.42f, .12f),
                        MainstreetReferenceKit.WarmShopWindow
                    );
            foreach (int side in new[] { -1, 1 })
            {
                for (float z = -2f; z > -width; z -= 3.5f)
                {
                    Place(
                        "TallWindowV2",
                        art,
                        new Vector3(side * (width / 2 + .08f), upperY, z),
                        MainstreetReferenceKit.TallWindow,
                        side * 90f
                    );
                    Place(
                        "TallWindowV2",
                        art,
                        new Vector3(side * (width / 2 + .08f), 1.6f, z),
                        MainstreetReferenceKit.TallWindow,
                        side * 90f
                    );
                }
                Block(
                    "CornerPier",
                    art,
                    new Vector3(side * (width / 2 - .14f), height / 2, .035f),
                    new Vector3(.28f, height, .12f),
                    store ? Cream : MainstreetReferenceKit.Brick
                );
                Block(
                    "CornerCap",
                    art,
                    new Vector3(side * (width / 2 - .14f), height + .05f, .035f),
                    new Vector3(.55f, .16f, .5f),
                    Cream
                );
            }
            if (hotel)
            {
                MainstreetReferenceKit.HotelCanopy(art, doorX);
                Transform sign = Group(
                    "HotelCornerSign",
                    art,
                    new Vector3(-width / 2 + .5f, upperY, .5f)
                );
                sign.localEulerAngles = new Vector3(0, -30, 0);
                Block(
                    "GoldBorder",
                    sign,
                    Vector3.zero,
                    new Vector3(1.05f, 3.05f, .32f),
                    Color("Gold", "#E0B456"),
                    .12f
                );
                Block(
                    "SignFace",
                    sign,
                    Vector3.forward * .18f,
                    new Vector3(.92f, 2.92f, .08f),
                    Navy,
                    .1f
                );
                var letters = Text(
                    "HotelLetters",
                    "H\nO\nT\nE\nL",
                    sign,
                    Vector3.forward * .24f,
                    .31f
                );
                letters.rectTransform.sizeDelta = new Vector2(1f, 3.1f);
            }
            else
            {
                MainstreetReferenceKit.Awning(art, 0, width - .5f, store, "CAFE");
                if (store)
                    Text("ShopName", "GENERAL STORE", art, new Vector3(0, 3.55f, .2f), .39f, Cream);
                for (float x = -width / 2 + 1f; x < width / 2; x += 2.8f)
                    AddWallLamp(art, x, 3.6f);
            }
            foreach (float x in new[] { doorX - 1.8f, doorX + 1.8f })
                if (Mathf.Abs(x) < width / 2 - .4f)
                    Place("FlowerPlanter", art, new Vector3(x, 0, .55f), () => Planter());
            if (!hotel && !store)
            {
                Place("CafeTable", art, new Vector3(-2.4f, 0, 1.0f), CafeTable, 90);
                Place(
                    "ChalkboardV2",
                    art,
                    new Vector3(-.3f, 0, .9f),
                    MainstreetReferenceKit.Chalkboard
                );
            }
            if (store)
            {
                Place(
                    "MarketCrateV2",
                    art,
                    new Vector3(-2.9f, 0, .7f),
                    MainstreetReferenceKit.MarketCrate
                );
                Place(
                    "MarketCrateV2",
                    art,
                    new Vector3(-1.65f, 0, .7f),
                    MainstreetReferenceKit.MarketCrate
                );
            }
            if (hotel)
                foreach (float x in new[] { doorX - 1.5f, doorX + 1.5f })
                    AddWallLamp(art, x, 2.1f);
            AddInteriorBackdrop(
                art,
                width,
                hotel ? "WELCOME"
                    : store ? "LOCAL GOODS"
                    : "COFFEE & HELLO"
            );
        }

        private static void AddWallLamp(Transform parent, float x, float y)
        {
            Transform lamp = Group("WallLamp", parent, new Vector3(x, y, .15f));
            Block("Mount", lamp, Vector3.zero, new Vector3(.12f, .25f, .12f), Navy);
            Transform arm = Post("Arm", lamp, new Vector3(0, .1f, .15f), .025f, .35f, Navy);
            arm.localEulerAngles = new Vector3(65, 0, 0);
            SmoothBall(
                "Shade",
                lamp,
                new Vector3(0, .13f, .29f),
                new Vector3(.35f, .15f, .35f),
                Navy
            );
            SmoothBall(
                "Bulb",
                lamp,
                new Vector3(0, .04f, .29f),
                Vector3.one * .15f,
                MainstreetReferenceKit.Glow
            );
        }

        private static void AddInteriorBackdrop(Transform parent, float width, string sign)
        {
            Block(
                "BackWallPanel",
                parent,
                new Vector3(0f, 1.6f, -width + 0.3f),
                new Vector3(width - 0.6f, 2.8f, 0.1f),
                Cream
            );
            Text("InteriorName", sign, parent, new Vector3(0f, 2f, -width + 0.37f), 0.25f, Cream);
            Block(
                "Counter",
                parent,
                new Vector3(0f, 0.4f, -width + 1.8f),
                new Vector3(width - 2f, 0.8f, 0.7f),
                Green
            );
            Block(
                "Countertop",
                parent,
                new Vector3(0f, 0.84f, -width + 1.8f),
                new Vector3(width - 1.8f, 0.08f, 0.85f),
                Wood
            );
        }

        private static void DecorateHome(Transform building, TownBlockoutSettings settings)
        {
            PrepareFacade(building, Color("HomeCreamV2", "#E8DFBF"));
            building.Find("Roof").gameObject.SetActive(false);
            building.Find("RoofRidge").gameObject.SetActive(false);
            Transform art = Group("HomeArt", building, Vector3.zero);
            float width = settings.HomeFootprintMeters;
            float height = settings.GroundFloorHeightMeters;
            float doorX = building.Find("DoorCenter").localPosition.x;
            GableRoof(art, width + 0.4f, width, height);
            for (float y = 0.5f; y < height; y += 0.4f)
            {
                // Upper siding spans the doorway only above its clear opening.
                if (y > settings.DoorwayHeightMeters + 0.15f)
                    Block(
                        "Clapboard",
                        art,
                        new Vector3(0f, y, 0.025f),
                        new Vector3(width, 0.035f, 0.035f),
                        Color("HomeSidingV2", "#CDBF9E"),
                        0.01f
                    );
                foreach (int side in new[] { -1, 1 })
                    Block(
                        "SideClapboard",
                        art,
                        new Vector3(side * width / 2f, y, -width / 2f),
                        new Vector3(0.035f, 0.035f, width),
                        Color("HomeSidingV2", "#CDBF9E"),
                        0.01f
                    );
            }
            foreach (float x in new[] { 0.8f, 2.8f })
            {
                Place("UpperWindow", art, new Vector3(x, 1.8f, 0.1f), Window);
                Place("WindowFlowerBox", art, new Vector3(x, 0.76f, 0.38f), () => Planter(true));
            }
            foreach (int side in new[] { -1, 1 })
                for (float z = -1.5f; z > -width; z -= 2.5f)
                    Place(
                        "UpperWindow",
                        art,
                        new Vector3(side * (width / 2f + 0.08f), 1.8f, z),
                        Window,
                        side * 90f
                    );
            Block(
                "PorchRoof",
                art,
                new Vector3(doorX, 2.8f, 0.7f),
                new Vector3(3.1f, 0.18f, 1.8f),
                Navy
            );
            foreach (int side in new[] { -1, 1 })
                Block(
                    "PorchColumn",
                    art,
                    new Vector3(doorX + side * 1.45f, 1.35f, 1.4f),
                    new Vector3(0.18f, 2.7f, 0.18f),
                    Cream
                );
            Text("HouseNumber", "12", art, new Vector3(doorX, 2.55f, 0.1f), 0.2f, Cream);
        }

        private static void BuildPaving(Transform parent)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = -23f; z < 30f; z += 2f)
                {
                    if (z > 7f && z < 13f)
                        continue;
                    bool junctionEdge = z == 7f || z == 13f;
                    float centreZ =
                        z == 7f ? 6.5f
                        : z == 13f ? 13.5f
                        : z;
                    for (int lane = 0; lane < 2; lane++)
                    {
                        Transform tile = Place(
                            "SidewalkTile2m",
                            parent,
                            new Vector3(side * (4f + lane * 2f), 0.105f, centreZ),
                            PavingTile
                        );
                        if (junctionEdge)
                            tile.localScale = new Vector3(1f, 1f, 0.5f);
                    }
                    Block(
                        "Curb",
                        parent,
                        new Vector3(side * 3.04f, 0.06f, centreZ),
                        new Vector3(0.16f, 0.12f, junctionEdge ? 0.96f : 1.96f),
                        Cream
                    );
                }
            }
        }

        private static GameObject PavingTile()
        {
            var root = new GameObject("PavingTile");
            for (int x = 0; x < 2; x++)
            for (int z = 0; z < 2; z++)
                Block(
                    "Paver",
                    root.transform,
                    new Vector3(x - 0.5f, 0f, z - 0.5f),
                    new Vector3(0.985f, 0.02f, 0.985f),
                    Color("SandPaving", "#D8C9AD"),
                    0.008f
                );
            return root;
        }

        private static void BuildStreetFurniture(Transform parent)
        {
            MainstreetStreetlightBuilder.PreparePrefab();
            foreach (int side in new[] { -1, 1 })
            {
                foreach (float z in new[] { -28f, 2.5f, 20f, 30f })
                {
                    Place(
                        "GardenTreeV2",
                        parent,
                        new Vector3(side * 3.65f, 0.12f, z),
                        MainstreetReferenceKit.GardenTree
                    );
                    Block(
                        "TreeBed",
                        parent,
                        new Vector3(side * 3.65f, 0.115f, z),
                        new Vector3(1f, 0.03f, 1.1f),
                        Color("Soil", "#776951")
                    );
                }
                foreach (float z in new[] { -27f, -7.5f, 15f, 27f })
                    Place(
                        "StreetLampV2",
                        parent,
                        new Vector3(side * 3.45f, 0.12f, z),
                        MainstreetReferenceKit.StreetLamp
                    );
            }
            Place("ParkBench", parent, new Vector3(11f, 0.1f, -12.4f), Bench);
            Place("FlowerPlanter", parent, new Vector3(12.5f, 0.1f, -11.5f), () => Planter());
            Place("FlowerPlanter", parent, new Vector3(12.5f, 0.1f, -8.5f), () => Planter());
            Place("FireHydrant", parent, new Vector3(3.6f, 0.12f, -9f), Hydrant);
            for (float x = 7.3f; x < 13f; x += 1.9f)
                Place("PicketFence2m", parent, new Vector3(x + 0.9f, 0.1f, -7.6f), Fence);
            Transform sign = Group("StreetSign", parent, new Vector3(3.45f, 0.1f, 5.8f));
            Post("Post", sign, new Vector3(0f, 1.35f, 0f), 0.065f, 2.7f, Navy);
            foreach (var item in new[] { ("MAIN ST", 2.5f), ("MAPLE AVE", 2.18f) })
            {
                Block(
                    "Sign",
                    sign,
                    new Vector3(0f, item.Item2, 0f),
                    new Vector3(1.45f, 0.28f, 0.08f),
                    Green
                );
                Text("StreetName", item.Item1, sign, new Vector3(0f, item.Item2, 0.05f), 0.13f);
                Transform reverse = Text(
                    "StreetNameReverse",
                    item.Item1,
                    sign,
                    new Vector3(0f, item.Item2, -0.05f),
                    0.13f
                ).transform;
                reverse.localEulerAngles = Vector3.zero;
            }
        }

        private static GameObject Fence()
        {
            var root = new GameObject("PicketFence2m");
            for (int index = 0; index < 7; index++)
                Block(
                    "Picket",
                    root.transform,
                    new Vector3(-0.87f + index * 0.29f, 0.48f, 0f),
                    new Vector3(0.2f, 0.96f, 0.12f),
                    Cream,
                    0.055f
                );
            foreach (float y in new[] { 0.25f, 0.68f })
                Block(
                    "Rail",
                    root.transform,
                    new Vector3(0f, y, -0.08f),
                    new Vector3(1.92f, 0.11f, 0.1f),
                    Cream
                );
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.48f, 0f);
            collider.size = new Vector3(1.92f, 0.96f, 0.2f);
            return root;
        }

        private static void BuildTruck(Transform parent)
        {
            const string path = "Assets/ThirdParty/KenneyCarKit/truck-flat.fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
                throw new System.InvalidOperationException($"무료 트럭 모델이 없습니다: {path}");
            // Preserve the source FBX and palette; the Unity wrapper owns scale and collision.
            string prefabPath = $"{PrefabFolder}/KenneyDeliveryTruck.prefab";
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(
                prefabPath,
                () =>
                {
                    var root = new GameObject("KenneyDeliveryTruck");
                    GameObject visual = Object.Instantiate(source, root.transform);
                    visual.name = "Visual";
                    Material palette = Color("KenneyCarPalette", "#FFFFFF");
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Assets/ThirdParty/KenneyCarKit/Textures/colormap.png"
                    );
                    palette.SetTexture("_BaseMap", texture);
                    Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                        renderer.sharedMaterial = palette;
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                        bounds.Encapsulate(renderer.bounds);
                    float scale = 3.8f / Mathf.Max(bounds.size.x, bounds.size.z);
                    visual.transform.localScale *= scale;
                    visual.transform.localPosition -=
                        new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0f, bounds.size.y * scale / 2f, 0f);
                    collider.size = bounds.size * scale;
                    return root;
                }
            );
            BuilderTools.InstantiatePrefab(
                prefab,
                parent,
                new Vector3(-1.7f, 0.02f, -18.8f),
                Quaternion.identity
            );
        }
    }
}
