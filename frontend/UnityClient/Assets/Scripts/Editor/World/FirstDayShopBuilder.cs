using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.EditorTools.ObjectHover;
using A305.Npc;
using A305.ObjectHover;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>기존 마을의 빈 건물 앞에 상점 출입구를 연결하고 없는 실내만 새로 만든다.</summary>
    public static class FirstDayShopBuilder
    {
        private const string ScenePath = "Assets/Scenes/Production/ShopScene.unity";
        private const string MenuPath = "Tools/A305/Mission/Build First Day Journey";

        [MenuItem("Tools/A305/Mission/Adjust Shop Counter Sightline")]
        public static void AdjustSightline()
        {
            BuilderTools.ThrowIfPlaying();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            NpcIdentity staff = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == "SHOP_STAFF");
            staff.LookTarget.localPosition = new Vector3(0, 1.12f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(staff.LookTarget);
            Transform counter = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownShopInterior/ShopCounter");
            float meshHeight = counter.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
            counter.localScale = new Vector3(1, .8f / meshHeight, 1);
            counter.localPosition = new Vector3(0, .4f, 2.4f);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        public static void BuildShop()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Scene existing = EditorSceneManager.OpenScene(ScenePath);
                SpaceSceneSetup.Apply(existing);
                BuilderTools.SaveScene(existing, ScenePath);
                return;
            }
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single
            );
            SceneGroups groups = BuilderTools.EnsureGroups(scene);
            Transform art = Group("TownShopInterior", groups.Environment, Vector3.zero);
            Solid(
                "ShopFloor",
                art,
                new Vector3(0, -.12f, 0),
                new Vector3(12, .2f, 14),
                Cream,
                "floor"
            );
            Solid("BackWall", art, new Vector3(0, 2, 7), new Vector3(12, 4, .2f), Cream, "wall");
            Solid("WestWall", art, new Vector3(-6, 2, 0), new Vector3(.2f, 4, 14), Cream, "wall");
            Solid("EastWall", art, new Vector3(6, 2, 0), new Vector3(.2f, 4, 14), Cream, "wall");
            Solid(
                "EntranceLeft",
                art,
                new Vector3(-3.5f, 2, -7),
                new Vector3(5, 4, .2f),
                Cream,
                "wall"
            );
            Solid(
                "EntranceRight",
                art,
                new Vector3(3.5f, 2, -7),
                new Vector3(5, 4, .2f),
                Cream,
                "wall"
            );
            Solid(
                "ShopCounter",
                art,
                new Vector3(0, .4f, 2.4f),
                new Vector3(5, .8f, .85f),
                Wood,
                "counter"
            );
            Transform shelf = Solid(
                "UmbrellaShelf",
                art,
                new Vector3(-3.8f, 1, 3.7f),
                new Vector3(1.6f, 2, .4f),
                Green,
                "shelf"
            );
            ObjectWordDefinition umbrella = UmbrellaWord();
            for (int index = 0; index < 3; index++)
            {
                Transform display = Group(
                    "Umbrella" + index,
                    art,
                    new Vector3(-3.8f, 1.9f, 1.8f - index * 1.2f)
                );
                Transform canopy = Ball(
                    "Canopy",
                    display,
                    new Vector3(0, .4f, 0),
                    new Vector3(1.2f, .35f, 1.2f),
                    index % 2 == 0 ? Green : Navy
                );
                Block(
                    "Handle",
                    display,
                    new Vector3(0, -.2f, 0),
                    new Vector3(.05f, 1.15f, .05f),
                    Wood
                );
                var hit = display.gameObject.AddComponent<BoxCollider>();
                hit.size = new Vector3(1.2f, 1.5f, 1.2f);
                var hover = display.gameObject.AddComponent<HoverableObject>();
                BuilderTools.SetReference(hover, "word", umbrella);
            }
            Transform sign = Solid(
                "ShopPriceSign",
                art,
                new Vector3(0, 2.8f, 6.7f),
                new Vector3(6, 1.3f, .12f),
                Green,
                "sign"
            );
            TownBlockoutGeometry
                .Label(
                    "PriceLabel",
                    "TOWN SHOP\nUMBRELLAS · 8 COINS",
                    sign,
                    new Vector3(0, 0, -.1f),
                    Vector3.zero,
                    .35f
                )
                .color = Cream.color;
            Door(
                groups.Environment,
                "TownDoor",
                new Vector3(0, 0, -6.8f),
                0,
                "TownScene",
                "ShopDoor"
            );
            Staff(groups);
            var sun = new GameObject("ShopSun").AddComponent<Light>();
            sun.transform.SetParent(groups.Lighting, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.ambientLight = new Color(.65f, .65f, .6f);
            BuilderTools.SaveScene(scene, ScenePath);
            SpaceSceneSetup.Apply(scene);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        public static void ConnectTownEntrance(SceneGroups groups)
        {
            if (
                BuilderTools
                    .FindAllInScene<SpaceDoorway>(groups.Scene)
                    .Any(door => door.DoorwayId == "ShopDoor")
            )
                return;
            Door(
                groups.Environment,
                "ShopDoor",
                new Vector3(-5.1f, .1f, -1),
                90,
                "ShopScene",
                "TownDoor"
            );
            Transform sign = Solid(
                "TownShopSign",
                groups.Environment,
                new Vector3(-4.9f, 2.75f, -1),
                new Vector3(.12f, .8f, 2.6f),
                Green,
                "sign"
            );
            TownBlockoutGeometry
                .Label(
                    "ShopLabel",
                    "TOWN SHOP",
                    sign,
                    new Vector3(.08f, 0, 0),
                    new Vector3(0, -90, 0),
                    .28f
                )
                .color = Cream.color;
        }

        private static void Door(
            Transform parent,
            string id,
            Vector3 position,
            float yaw,
            string destination,
            string arrival
        )
        {
            GameObject root = BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(
                    SpaceSceneBuilder.DoorwayPrefabPath,
                    MenuPath
                ),
                parent,
                position,
                Quaternion.Euler(0, yaw, 0)
            );
            root.name = id;
            var doorway = root.GetComponent<SpaceDoorway>();
            BuilderTools.SetText(doorway, "doorwayId", id);
            BuilderTools.SetText(doorway, "destinationScene", destination);
            BuilderTools.SetText(doorway, "destinationDoorwayId", arrival);
            var trigger = root.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0, 1.3f, .45f);
            trigger.size = new Vector3(1.8f, 2.6f, 1);
            doorway.ArrivalPoint.localPosition = new Vector3(0, .1f, 2);
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorway.ArrivalPoint);
        }

        private static void Staff(SceneGroups groups)
        {
            GameObject staff = BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(NpcBuilder.PasserbyPrefabPath, MenuPath),
                groups.Characters,
                new Vector3(0, .05f, 3.6f),
                Quaternion.Euler(0, 180, 0)
            );
            staff.name = "ShopStaff";
            BuilderTools.SetText(staff.GetComponent<NpcIdentity>(), "code", "SHOP_STAFF");
            staff.GetComponent<NpcIdentity>().LookTarget.localPosition = new Vector3(0, 1.12f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                staff.GetComponent<NpcIdentity>().LookTarget
            );
            BuilderTools.EnsureFolder("Assets/Data/Mission");
            string settingsPath = "Assets/Data/Mission/ShopStaffSettings.asset";
            var settings = BuilderTools.LoadOrCreateAsset<PasserbySettings>(settingsPath);
            var fields = new SerializedObject(settings);
            fields.FindProperty("detectRadiusMeters").floatValue = 4;
            fields.FindProperty("offerDistanceMeters").floatValue = 3;
            fields.FindProperty("walkSpeedMetersPerSecond").floatValue = 0;
            fields.FindProperty("restSeconds").floatValue = 5;
            fields.FindProperty("canDecline").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.SetReference(staff.GetComponent<Passerby>(), "settings", settings);
            BuilderTools.SetReference(
                staff.GetComponent<Passerby>(),
                "lineSource",
                BuilderTools.LoadRequiredAsset<MockNpcLineSource>(
                    "Assets/Data/Npc/MockNpcLines.asset",
                    MenuPath
                )
            );
            GameObject visual = BuilderTools.LoadRequiredAsset<GameObject>(
                "Assets/Prefabs/World/TownProduction/LittleMungV7NpcVisual.prefab",
                MenuPath
            );
            staff.transform.Find("Visual").gameObject.SetActive(false);
            BuilderTools
                .InstantiatePrefab(visual, staff.transform, Vector3.zero, Quaternion.identity)
                .name = "ShopStaffVisual";
        }

        private static Transform Solid(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            string word
        )
        {
            Transform shape = Block(name, parent, position, size, material);
            var collider = shape.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            var hover = shape.gameObject.AddComponent<HoverableObject>();
            BuilderTools.SetReference(
                hover,
                "word",
                BuilderTools.LoadRequiredAsset<ObjectWordDefinition>(
                    ObjectWordAuthoring.WordsFolder + "/" + word + ".asset",
                    MenuPath
                )
            );
            return shape;
        }

        private static ObjectWordDefinition UmbrellaWord()
        {
            string path = ObjectWordAuthoring.WordsFolder + "/umbrella.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(path);
            if (definition != null)
                return definition;
            definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            var fields = new SerializedObject(definition);
            fields.FindProperty("details.text").stringValue = "umbrella";
            fields.FindProperty("details.meaningKo").stringValue = "우산";
            fields.FindProperty("details.example").stringValue = "Where can I buy an umbrella?";
            fields.FindProperty("details.exampleKo").stringValue = "우산은 어디에서 살 수 있나요?";
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, path);
            ObjectWordAuthoring.EnsureIdentity(definition);
            return definition;
        }
    }
}
