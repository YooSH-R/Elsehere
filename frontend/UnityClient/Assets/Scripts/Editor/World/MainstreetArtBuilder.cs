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
            Material wall = store
                ? Color("ButterStucco", "#E6C998")
                : Color("CoralBrick", "#CC795E");
            PrepareFacade(building, wall);
            Transform art = Group("FacadeArt", building, Vector3.zero);
            float height = settings.ShopHeightMeters;
            float upperY = settings.GroundFloorHeightMeters + settings.UpperFloorHeightMeters / 2f;
            float doorX = building.Find("DoorCenter").localPosition.x;

            foreach (float y in new[] { settings.GroundFloorHeightMeters, height - 0.14f })
            {
                Block(
                    "CorniceFront",
                    art,
                    new Vector3(0f, y, 0.13f),
                    new Vector3(width + 0.25f, 0.24f, 0.38f),
                    Cream
                );
                foreach (int side in new[] { -1, 1 })
                    Block(
                        "CorniceSide",
                        art,
                        new Vector3(side * width / 2f, y, -width / 2f),
                        new Vector3(0.3f, 0.24f, width),
                        Cream
                    );
            }
            for (float x = -width / 2f + 1f; x < width / 2f; x += 2f)
            {
                if (!(hotel && x > width / 2f - 2f))
                {
                    Place("UpperWindow", art, new Vector3(x, upperY, 0.12f), Window);
                    Place(
                        "WindowFlowerBox",
                        art,
                        new Vector3(x, upperY - 1.04f, 0.36f),
                        () => Planter(true)
                    );
                }
                if (Mathf.Abs(x - doorX) > 1.8f)
                    Place("ShopWindow", art, new Vector3(x, 1.42f, 0.12f), ShopWindow);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = -1.4f; z > -width; z -= 2.6f)
                {
                    Place(
                        "UpperWindow",
                        art,
                        new Vector3(side * (width / 2f + 0.08f), upperY, z),
                        Window,
                        side * 90f
                    );
                    Place(
                        "UpperWindow",
                        art,
                        new Vector3(side * (width / 2f + 0.08f), 1.6f, z),
                        Window,
                        side * 90f
                    );
                }
                for (float y = 0.35f; y < height - 0.4f; y += 0.52f)
                    Block(
                        "CornerStone",
                        art,
                        new Vector3(side * (width / 2f - 0.18f), y, 0.04f),
                        new Vector3(0.42f, 0.32f, 0.16f),
                        store ? Cream : Color("BrickLight", "#D89073")
                    );
            }
            if (!store)
                AddBrickAccents(art, width, height);

            float canopyWidth = hotel ? 3.4f : width - 0.3f;
            float canopyX = hotel ? doorX : 0f;
            BuildAwning(art, canopyX, canopyWidth, store);
            string sign =
                hotel ? "MAPLE HOTEL"
                : store ? "GENERAL STORE"
                : "ELSEHERE CAFE";
            Block(
                "SignBoard",
                art,
                new Vector3(0f, 3.55f, 0.14f),
                new Vector3(width - 0.9f, 0.62f, 0.18f),
                Cream
            );
            Text("ShopName", sign, art, new Vector3(0f, 3.55f, 0.25f), 0.3f, Cream);
            if (hotel)
            {
                Vector3 signPosition = new Vector3(width / 2f - 1f, upperY - 0.05f, 0.35f);
                Block(
                    "HotelSignBorder",
                    art,
                    signPosition,
                    new Vector3(1.05f, 2.7f, 0.32f),
                    Color("Gold", "#E0B456"),
                    0.12f
                );
                Block(
                    "HotelSignFace",
                    art,
                    signPosition + Vector3.forward * 0.18f,
                    new Vector3(0.9f, 2.53f, 0.08f),
                    Navy,
                    0.1f
                );
                var letters = Text(
                    "HotelLetters",
                    "H\nO\nT\nE\nL",
                    art,
                    signPosition + Vector3.forward * 0.24f,
                    0.27f
                );
                letters.rectTransform.sizeDelta = new Vector2(1f, 3f);
            }
            foreach (float x in new[] { doorX - 1.75f, doorX + 1.75f })
            {
                if (Mathf.Abs(x) < width / 2f - 0.45f)
                    Place("FlowerPlanter", art, new Vector3(x, 0f, 0.55f), () => Planter());
            }
            if (!hotel && !store)
                Place("CafeTable", art, new Vector3(-1.8f, 0f, 1.15f), CafeTable, 90f);
            AddInteriorBackdrop(
                art,
                width,
                hotel ? "WELCOME"
                    : store ? "LOCAL GOODS"
                    : "COFFEE & HELLO"
            );
        }

        private static void AddBrickAccents(Transform parent, float width, float height)
        {
            // A shared sparse brick panel suggests masonry without a full brick mesh wall.
            for (float x = -width / 2f + 1f; x < width / 2f; x += 2f)
            {
                Transform panel = Place(
                    "BrickAccentPanel",
                    parent,
                    new Vector3(x, height - 0.8f, 0.015f),
                    () =>
                    {
                        var root = new GameObject("BrickAccentPanel");
                        Block(
                            "BrickA",
                            root.transform,
                            new Vector3(-0.52f, 0f, 0f),
                            new Vector3(0.42f, 0.2f, 0.025f),
                            Color("BrickLight", "#D89073"),
                            0.01f
                        );
                        Block(
                            "BrickB",
                            root.transform,
                            new Vector3(0.2f, -0.22f, 0f),
                            new Vector3(0.42f, 0.2f, 0.025f),
                            Color("BrickShade", "#BA6A52"),
                            0.01f
                        );
                        return root;
                    }
                );
                panel.name = "BrickAccents";
            }
        }

        private static void BuildAwning(Transform parent, float centreX, float width, bool striped)
        {
            int strips = Mathf.CeilToInt(width / 0.55f);
            float stripWidth = width / strips;
            for (int index = 0; index < strips; index++)
            {
                float x = centreX - width / 2f + stripWidth * (index + 0.5f);
                Material fabric = striped
                    ? (index % 2 == 0 ? Cream : Color("AwningRed", "#C46F57"))
                    : Green;
                Transform roof = Block(
                    "AwningSlope",
                    parent,
                    new Vector3(x, 2.94f, 0.66f),
                    new Vector3(stripWidth + 0.005f, 0.08f, 1.48f),
                    fabric,
                    0.02f
                );
                roof.localEulerAngles = new Vector3(22f, 0f, 0f);
                Block(
                    "AwningScallop",
                    parent,
                    new Vector3(x, 2.53f, 1.32f),
                    new Vector3(stripWidth, 0.32f, 0.1f),
                    fabric,
                    0.045f
                );
            }
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
            PrepareFacade(building, Color("HomeBlue", "#89A9B3"));
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
                        Color("HomeSiding", "#A6BEC3"),
                        0.01f
                    );
                foreach (int side in new[] { -1, 1 })
                    Block(
                        "SideClapboard",
                        art,
                        new Vector3(side * width / 2f, y, -width / 2f),
                        new Vector3(0.035f, 0.035f, width),
                        Color("HomeSiding", "#A6BEC3"),
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
            foreach (int side in new[] { -1, 1 })
            {
                foreach (float z in new[] { -21.5f, 1.5f, 19f, 27f })
                {
                    Place("RoundTree", parent, new Vector3(side * 3.65f, 0.12f, z), Tree);
                    Block(
                        "TreeBed",
                        parent,
                        new Vector3(side * 3.65f, 0.115f, z),
                        new Vector3(1f, 0.03f, 1.1f),
                        Color("Soil", "#776951")
                    );
                }
                foreach (float z in new[] { -18f, -8f, 5f, 16f, 24f })
                    Place("StreetLamp", parent, new Vector3(side * 3.45f, 0.12f, z), Lamp);
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
