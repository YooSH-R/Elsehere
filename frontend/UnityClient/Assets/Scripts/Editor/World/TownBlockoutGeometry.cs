using A305.EditorTools.Common;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.World
{
    /// <summary>Sandbox의 크기 비교용 단순 형상. 단위 프리팹은 유지하고 씬 인스턴스의 치수만 지정한다.</summary>
    internal static class TownBlockoutGeometry
    {
        internal const string ArtFolder = "Assets/Art/World/MainstreetBlockout";
        internal const string PrefabFolder = "Assets/Prefabs/World/MainstreetBlockout";

        internal static Material Material(string name, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return BuilderTools.GetOrCreateMaterial(ArtFolder, name, color);
        }

        internal static Transform Group(string name, Transform parent, Vector3 position)
        {
            return BuilderTools.CreateEmpty(name, parent, position, Quaternion.identity);
        }

        internal static Transform Box(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            bool solid = true
        )
        {
            return Shape(PrimitiveType.Cube, name, parent, position, size, material, solid);
        }

        internal static Transform Shape(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            bool solid = false
        )
        {
            string path = $"{PrefabFolder}/Unit{type}.prefab";
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(
                path,
                () =>
                {
                    GameObject primitive = GameObject.CreatePrimitive(type);
                    primitive.name = $"Unit{type}";
                    return primitive;
                }
            );
            GameObject instance = BuilderTools.InstantiatePrefab(
                prefab,
                parent,
                position,
                Quaternion.identity
            );
            instance.name = name;
            instance.transform.localScale = size;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            instance.GetComponent<Collider>().enabled = solid;
            return instance.transform;
        }

        internal static TextMeshPro Label(
            string name,
            string value,
            Transform parent,
            Vector3 position,
            Vector3 angles,
            float size = 0.45f
        )
        {
            Transform label = Group(name, parent, position);
            label.localEulerAngles = angles;
            var text = label.gameObject.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Pretendard-Bold SDF.asset"
            );
            text.text = value;
            text.fontSize = size * 10f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.17f, 0.24f, 0.3f);
            text.rectTransform.sizeDelta = new Vector2(8f, 3f);
            return text;
        }

        internal static void Building(
            string name,
            Transform parent,
            Vector3 origin,
            float frontage,
            float depth,
            float yaw,
            TownBlockoutSettings settings,
            bool house = false,
            bool enterable = true
        )
        {
            // Local +Z faces the road; the origin is the centre of the front facade at sidewalk level.
            Transform building = Group(name, parent, origin);
            building.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float height = house ? settings.GroundFloorHeightMeters : settings.ShopHeightMeters;
            Material wall = Material(
                name == "GeneralStore" ? "Cream" : "Coral",
                name == "GeneralStore" ? "#F2D6A7" : "#D97656"
            );
            Material cream = Material("Cream", "#F2D6A7");
            Material glass = Material("Window", "#6D9CB3");
            float doorWidth = settings.DoorwayWidthMeters;
            float doorHeight = settings.DoorwayHeightMeters;
            // The doorway occupies the southern four-metre facade bay in the site plan.
            float doorX = (yaw < 180f ? 1f : -1f) * (frontage / 2f - 2f);
            float leftWidth = frontage / 2f + doorX - doorWidth / 2f;
            float rightWidth = frontage - leftWidth - doorWidth;

            Box(
                "Floor",
                building,
                new Vector3(0f, -0.05f, -depth / 2f),
                new Vector3(frontage, 0.1f, depth),
                cream
            );
            Box(
                "BackWall",
                building,
                new Vector3(0f, height / 2f, -depth + 0.1f),
                new Vector3(frontage, height, 0.2f),
                wall
            );
            Box(
                "LeftWall",
                building,
                new Vector3(-frontage / 2f + 0.1f, height / 2f, -depth / 2f),
                new Vector3(0.2f, height, depth),
                wall
            );
            Box(
                "RightWall",
                building,
                new Vector3(frontage / 2f - 0.1f, height / 2f, -depth / 2f),
                new Vector3(0.2f, height, depth),
                wall
            );
            Box(
                "FacadeLeft",
                building,
                new Vector3(-frontage / 2f + leftWidth / 2f, height / 2f, -0.1f),
                new Vector3(leftWidth, height, 0.2f),
                wall
            );
            Box(
                "FacadeRight",
                building,
                new Vector3(frontage / 2f - rightWidth / 2f, height / 2f, -0.1f),
                new Vector3(rightWidth, height, 0.2f),
                wall
            );
            Box(
                "DoorHeader",
                building,
                new Vector3(doorX, (height + doorHeight) / 2f, -0.1f),
                new Vector3(doorWidth, height - doorHeight, 0.2f),
                wall
            );
            Box(
                "DoorFrameLeft",
                building,
                new Vector3(doorX - doorWidth / 2f - 0.05f, doorHeight / 2f, 0.03f),
                new Vector3(0.1f, doorHeight, 0.12f),
                cream
            );
            Box(
                "DoorFrameRight",
                building,
                new Vector3(doorX + doorWidth / 2f + 0.05f, doorHeight / 2f, 0.03f),
                new Vector3(0.1f, doorHeight, 0.12f),
                cream
            );
            Box(
                "DoorFrameTop",
                building,
                new Vector3(doorX, doorHeight + 0.05f, 0.03f),
                new Vector3(doorWidth + 0.2f, 0.1f, 0.12f),
                cream
            );
            Group("DoorCenter", building, new Vector3(doorX, 0f, 0f));
            Box(
                "DoorApproach",
                building,
                new Vector3(doorX, 0.012f, 1.2f),
                new Vector3(3f, 0.02f, 2.4f),
                Material("EntryZone", "#B0CED0"),
                false
            );
            if (!enterable)
            {
                Box(
                    "ClosedDisplay",
                    building,
                    new Vector3(doorX, doorHeight / 2f, -0.12f),
                    new Vector3(doorWidth, doorHeight, 0.1f),
                    glass
                );
            }

            Box(
                "FloorBand",
                building,
                new Vector3(0f, settings.GroundFloorHeightMeters, 0.06f),
                new Vector3(frontage + 0.1f, 0.16f, 0.15f),
                cream
            );
            if (house)
            {
                // Two stepped roof masses distinguish the house while keeping the blockout simple.
                Transform roof = Shape(
                    PrimitiveType.Cube,
                    "Roof",
                    building,
                    new Vector3(0f, height + 0.4f, -depth / 2f),
                    new Vector3(frontage + 0.3f, 0.8f, depth + 0.3f),
                    Material("Roof", "#536780"),
                    true
                );
                Box(
                    "RoofRidge",
                    building,
                    new Vector3(0f, height + 1f, -depth / 2f),
                    new Vector3(frontage * 0.55f, 0.4f, depth + 0.3f),
                    roof.GetComponent<Renderer>().sharedMaterial
                );
            }
            else
            {
                Box(
                    "Roof",
                    building,
                    new Vector3(0f, height - 0.1f, -depth / 2f),
                    new Vector3(frontage, 0.2f, depth),
                    cream
                );
                Box(
                    "Parapet",
                    building,
                    new Vector3(0f, height - settings.ParapetHeightMeters / 2f, 0f),
                    new Vector3(frontage + 0.1f, settings.ParapetHeightMeters, 0.3f),
                    cream
                );
                for (float x = -frontage / 2f + 1f; x < frontage / 2f; x += 2f)
                {
                    Box(
                        "UpperWindow",
                        building,
                        new Vector3(x, settings.GroundFloorHeightMeters + 1.1f, 0.025f),
                        new Vector3(1.15f, 1.35f, 0.08f),
                        glass,
                        false
                    );
                }
            }
            if (name == "Cafe" || name == "Hotel")
            {
                Box(
                    "Awning",
                    building,
                    new Vector3(doorX, 2.5f, 0.5f),
                    new Vector3(3.4f, 0.2f, 1f),
                    Material("Awning", "#326B57")
                );
            }
            Label(
                "BuildingSign",
                name.ToUpperInvariant(),
                building,
                new Vector3(0f, 2.85f, 0.2f),
                new Vector3(0f, 180f, 0f),
                0.45f
            );
            if (name == "Hotel")
            {
                Box(
                    "VerticalSign",
                    building,
                    new Vector3(frontage / 2f - 0.6f, 4.2f, 0.3f),
                    new Vector3(0.7f, 2f, 0.25f),
                    Material("Navy", "#293E57"),
                    false
                );
                TextMeshPro sign = Label(
                    "HotelLandmark",
                    "H\nO\nT\nE\nL",
                    building,
                    new Vector3(frontage / 2f - 0.6f, 4.2f, 0.45f),
                    new Vector3(0f, 180f, 0f),
                    0.3f
                );
                sign.color = Color.white;
            }
        }

        internal static GameObject CharacterVisual()
        {
            var root = new GameObject("LittleMungV7Proxy");
            Transform parent = root.transform;
            Material skin = Material("ProxySkin", "#F5C59D");
            Material hair = Material("ProxyHair", "#634B40");
            Material shirt = Material("ProxyJacket", "#7797D9");
            Material shorts = Material("ProxyShorts", "#65BEC6");
            Material shoes = Material("ProxyShoes", "#F5EBD4");
            // Head envelope is Y=0.56..1.40; body and shoes stay below Y=0.56.
            Shape(
                PrimitiveType.Sphere,
                "HeadHairEnvelope",
                parent,
                new Vector3(0f, 0.98f, -0.04f),
                new Vector3(1.24f, 0.84f, 0.78f),
                hair
            );
            Shape(
                PrimitiveType.Sphere,
                "Face",
                parent,
                new Vector3(0f, 0.95f, 0.16f),
                new Vector3(1.1f, 0.71f, 0.52f),
                skin
            );
            Box(
                "Jacket",
                parent,
                new Vector3(0f, 0.425f, 0f),
                new Vector3(0.4f, 0.25f, 0.25f),
                shirt,
                false
            );
            Box(
                "Shorts",
                parent,
                new Vector3(0f, 0.25f, 0f),
                new Vector3(0.36f, 0.12f, 0.24f),
                shorts,
                false
            );
            for (int side = -1; side <= 1; side += 2)
            {
                Shape(
                    PrimitiveType.Sphere,
                    "Sleeve",
                    parent,
                    new Vector3(side * 0.24f, 0.39f, 0f),
                    new Vector3(0.15f, 0.25f, 0.18f),
                    shirt
                );
                Shape(
                    PrimitiveType.Sphere,
                    "Hand",
                    parent,
                    new Vector3(side * 0.27f, 0.275f, 0f),
                    new Vector3(0.12f, 0.14f, 0.12f),
                    skin
                );
                Shape(
                    PrimitiveType.Sphere,
                    "Leg",
                    parent,
                    new Vector3(side * 0.1f, 0.16f, 0f),
                    new Vector3(0.13f, 0.19f, 0.15f),
                    skin
                );
                Box(
                    "Shoe",
                    parent,
                    new Vector3(side * 0.1f, 0.05f, 0.04f),
                    new Vector3(0.17f, 0.1f, 0.25f),
                    shoes,
                    false
                );
                Shape(
                    PrimitiveType.Sphere,
                    "EyeWhite",
                    parent,
                    new Vector3(side * 0.18f, 0.94f, 0.407f),
                    new Vector3(0.12f, 0.12f, 0.035f),
                    Material("ProxyWhite", "#FFFFFF")
                );
                Shape(
                    PrimitiveType.Sphere,
                    "Pupil",
                    parent,
                    new Vector3(side * 0.165f, 0.94f, 0.426f),
                    new Vector3(0.064f, 0.064f, 0.016f),
                    Material("ProxyInk", "#332C28")
                );
            }
            Box(
                "ThinNoseMarker",
                parent,
                new Vector3(0f, 0.84f, 0.421f),
                new Vector3(0.055f, 0.02f, 0.035f),
                skin,
                false
            );
            Box(
                "MouthMarker",
                parent,
                new Vector3(0f, 0.79f, 0.392f),
                new Vector3(0.07f, 0.015f, 0.02f),
                Material("ProxyInk", "#332C28"),
                false
            );
            return root;
        }
    }
}
