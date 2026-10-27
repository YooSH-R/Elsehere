using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.HomeInteriorKit;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>기존 HomeScene의 기능 객체를 유지하며 실내 외형을 한 번 적용한다.</summary>
    public static class HomeInteriorBuilder
    {
        internal const string ScenePath = "Assets/Scenes/Production/HomeScene.unity";
        internal const string SandboxPath = "Assets/Scenes/Sandbox/HomeInteriorSandbox.unity";
        private const string DataFolder = "Assets/Data/Home";

        [MenuItem("Tools/A305/Home/Apply Soft Daylight")]
        public static void ApplySoftDaylight()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureLighting(scene);
            AssetDatabase.SaveAssets();
            BuilderTools.SaveScene(scene, ScenePath);
            EditorSceneManager.SaveScene(scene, SandboxPath, true);
        }

        [MenuItem("Tools/A305/Home/Apply Cozy Cottage Design")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Transform environment = Root(scene, "Environment");
            if (environment.Find("CozyInterior") != null)
            {
                Debug.Log(
                    "Home interior already applied. Edit the saved prefabs and scene placement directly."
                );
                return;
            }
            SpaceDoorway door = BuilderTools.FindSingleInScene<SpaceDoorway>(scene);
            Transform oldRoom = door.transform.parent;
            HoverableObject[] objects = oldRoom.GetComponentsInChildren<HoverableObject>();
            string[] expected =
            {
                "tv",
                "sofa",
                "bookshelf",
                "refrigerator",
                "air_conditioner",
                "bed",
            };
            if (
                !expected
                    .OrderBy(key => key)
                    .SequenceEqual(objects.Select(item => item.UnityKey).OrderBy(key => key))
            )
                throw new InvalidOperationException(
                    "Home hover objects changed; inspect the scene before applying art."
                );

            foreach (Transform child in oldRoom)
            {
                if (
                    child.GetComponent<SpaceDoorway>() != null
                    || child.GetComponent<HoverableObject>() != null
                )
                    continue;
                child.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }

            Transform interior = Group("CozyInterior", environment, Vector3.zero);
            Put(Asset("RoomShell", RoomShell, false), interior, Vector3.zero);
            Put(Asset("RoomDecoration", Decoration, false), interior, Vector3.zero);
            Replace(objects, "sofa", Asset("SageSofa", Sofa), V(-2.7f, 0, .65f));
            Replace(objects, "tv", Asset("Television", Television), V(-2.7f, 0, 4.35f), 180);
            Replace(
                objects,
                "refrigerator",
                Asset("RetroRefrigerator", Refrigerator),
                V(4.25f, 0, 4.4f),
                180
            );
            Replace(
                objects,
                "air_conditioner",
                Asset("AirConditioner", AirConditioner),
                V(.2f, 2.9f, 4.82f),
                180
            );
            Replace(objects, "bookshelf", Asset("OakBookcase", Bookcase), V(-4.6f, 0, -2.1f), 90);
            Replace(objects, "bed", Asset("SageBed", Bed), V(3.4f, 0, -1.85f), 270);

            Transform doorVisual = door.transform.Find("Visual");
            doorVisual.localScale = V(1.35f, 1.12f, 1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorVisual);
            ConfigurePlayer(scene);
            ConfigureLighting(scene);
            AssetDatabase.SaveAssets();
            BuilderTools.SaveScene(scene, ScenePath);
            EditorSceneManager.SaveScene(scene, SandboxPath, true);
            Debug.Log(
                "HomeScene and HomeInteriorSandbox saved. Existing furniture keys, tutorial and travel references preserved."
            );
        }

        private static void Replace(
            HoverableObject[] objects,
            string key,
            GameObject prefab,
            Vector3 position,
            float yaw = 0
        )
        {
            HoverableObject item = objects.Single(candidate => candidate.UnityKey == key);
            Transform target = item.transform;
            // 사물 키와 기능 참조가 달린 기존 루트를 보존하고 외형만 자식 프리팹으로 교체한다.
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (Collider collider in target.GetComponents<Collider>())
            {
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            target.localScale = Vector3.one;
            target.SetLocalPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            Put(prefab, target, Vector3.zero);
        }

        private static GameObject RoomShell()
        {
            var root = new GameObject("RoomShell");
            Transform floor = Group("Floor", root.transform, Vector3.zero);
            Material alternate = Paint("OakAlternate", "#CCA475");
            Material pale = Paint("OakPale", "#DDB788");
            for (int row = 0; row < 20; row++)
            {
                for (int plank = 0; plank < 5; plank++)
                {
                    Material material =
                        (row + plank) % 3 == 0 ? alternate
                        : (row + plank) % 3 == 1 ? LightOak
                        : pale;
                    Block(
                        "Plank",
                        floor,
                        V(-4 + plank * 2, -.04f, -4.75f + row * .5f),
                        V(1.985f, .08f, .487f),
                        material,
                        .012f
                    );
                }
            }
            CombineParts(floor.gameObject, "HomeFloor");
            Solid(floor, V(0, -.1f, 0), V(10, .2f, 10));
            Wall(root.transform, "NorthWall", V(0, 0, 5), 180, 2.7f);
            Wall(root.transform, "WestWall", V(-5, 0, 0), 90, -1.5f);
            Wall(root.transform, "EastWall", V(5, 0, 0), 270, 0);
            Transform south = Group("SouthWall", root.transform, V(0, 0, -5));
            foreach (int side in new[] { -1, 1 })
            {
                Transform wall = Block(
                    "Wall",
                    south,
                    V(side * 3, 1.8f, 0),
                    V(4, 3.6f, .22f),
                    Ivory
                );
                Solid(wall, Vector3.zero, V(4, 3.6f, .22f));
                Block("Skirting", south, V(side * 3, .12f, .15f), V(4, .24f, .08f), LightOak);
            }
            Transform lintel = Block("OverDoor", south, V(0, 3.1f, 0), V(2, 1, .22f), Ivory);
            Solid(lintel, Vector3.zero, V(2, 1, .22f));
            Transform ceiling = Group("Ceiling", root.transform, V(0, 3.62f, 0));
            Transform slab = Block("Plaster", ceiling, Vector3.zero, V(10, .14f, 10), Ivory);
            slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Solid(ceiling, Vector3.zero, V(10, .14f, 10));
            return root;
        }

        private static void Wall(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            float windowX
        )
        {
            Transform wall = Group(name, parent, position);
            wall.localRotation = Quaternion.Euler(0, yaw, 0);
            // 창 위치에 실제 개구부를 두어 바깥 햇빛이 실내에 들어오게 한다.
            WallPiece(wall, "LowerWall", V(0, .7375f, 0), V(10, 1.475f, .22f));
            WallPiece(wall, "UpperWall", V(0, 3.3625f, 0), V(10, .475f, .22f));
            float leftWidth = windowX + 4.05f;
            float rightWidth = 4.05f - windowX;
            WallPiece(wall, "LeftPier", V(-5 + leftWidth / 2, 2.3f, 0), V(leftWidth, 1.65f, .22f));
            WallPiece(
                wall,
                "RightPier",
                V(5 - rightWidth / 2, 2.3f, 0),
                V(rightWidth, 1.65f, .22f)
            );
            Solid(wall, V(windowX, 2.3f, 0), V(1.9f, 2, .15f));
            Block("Wainscot", wall, V(0, .45f, .13f), V(10, .86f, .06f), Sage);
            Block("ChairRail", wall, V(0, .92f, .15f), V(10, .1f, .1f), Ivory);
            Block("Skirting", wall, V(0, .12f, .16f), V(10, .24f, .1f), LightOak);
            Block("Cornice", wall, V(0, 3.46f, .12f), V(10, .14f, .18f), LightOak);
            for (int index = -6; index <= 6; index++)
                Block(
                    "PanelBatten",
                    wall,
                    V(index * .76f, .48f, .18f),
                    V(.055f, .62f, .035f),
                    Ivory,
                    .015f
                );
            Put(
                Asset("CurtainedWindow", HomeInteriorKit.Window, false),
                wall,
                V(windowX, 2.3f, .04f)
            );
        }

        private static void WallPiece(Transform parent, string name, Vector3 center, Vector3 size)
        {
            Transform part = Block(name, parent, center, size, Ivory);
            Solid(part, Vector3.zero, size);
        }

        private static GameObject Decoration()
        {
            var root = new GameObject("RoomDecoration");
            Transform p = root.transform;
            Put(Asset("MediaConsole", Console), p, V(-2.7f, 0, 4.3f), 180);
            Put(Asset("KitchenCounter", KitchenCounter), p, V(2.18f, 0, 4.45f), 180);
            Put(Asset("CoffeeTable", CoffeeTable), p, V(-2.7f, 0, 2.25f));
            Put(Asset("LivingRug", Rug), p, V(-2.7f, 0, 1.9f));
            Transform bedside = Put(Asset("SideTable", SideTable), p, V(4.45f, 0, -.4f), 270);
            Put(Asset("TableLamp", HomeInteriorKit.Lamp), bedside, V(0, .61f, 0));
            Put(Asset("SideTable", SideTable), p, V(-4.35f, 0, .8f));
            Put(Asset("TableLamp", HomeInteriorKit.Lamp), p, V(-4.35f, .61f, .8f));
            Put(Asset("MediaConsole", Console), p, V(-2.5f, 0, -4.45f));
            Transform bedRug = Put(Asset("LivingRug", Rug), p, V(2.8f, 0, -1.85f));
            bedRug.localScale = V(.9f, 1, .82f);
            Existing("HousePlants/Monstera", p, V(-4.35f, 0, 4.05f));
            Existing("HousePlants/SnakePlant", p, V(4.5f, 0, 1.15f));
            Existing("HousePlants/Succulent", p, V(-3.3f, .62f, -4.4f));
            Existing("BakeryInterior/CafeTable", p, V(2.65f, 0, 1.8f));
            Existing("BakeryInterior/CafeChair", p, V(2.65f, 0, .85f));
            Existing("BakeryInterior/CafeChair", p, V(2.65f, 0, 2.75f), 180);
            Existing("BakeryInterior/Mug", p, V(-3, .61f, 2.2f));
            Transform entryArt = Group("EntryArtwork", p, V(-2.5f, 2.03f, -4.83f));
            Block("Frame", entryArt, Vector3.zero, V(1.25f, 1.13f, .08f), Oak);
            Block("Paper", entryArt, V(0, 0, .05f), V(1.08f, .96f, .025f), Ivory);
            SmoothBall("Sun", entryArt, V(.22f, .21f, .073f), V(.3f, .3f, .015f), Coral);
            Block("Hill", entryArt, V(-.1f, -.2f, .07f), V(.85f, .31f, .02f), Sage);
            // 주방 선반은 벽에 붙여 중앙 통로와 사물 호버 시야를 비워 둔다.
            Block("KitchenShelf", p, V(2.1f, 2.28f, 4.64f), V(2.1f, .1f, .48f), LightOak);
            Existing("HousePlants/Succulent", p, V(2.65f, 2.34f, 4.55f));
            for (int i = 0; i < 3; i++)
                Post(
                    "Jar",
                    p,
                    V(1.4f + i * .28f, 2.5f, 4.55f),
                    .09f,
                    .32f,
                    i % 2 == 0 ? Ivory : Coral
                );
            return root;
        }

        private static void Existing(string name, Transform parent, Vector3 position, float yaw = 0)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/TinyTreats/" + name + ".prefab"
            );
            if (prefab == null)
                throw new InvalidOperationException("Missing licensed asset: " + name);
            Put(prefab, parent, position, yaw);
        }

        private static void ConfigurePlayer(Scene scene)
        {
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            PlayerCharacterBuilder.ConfigurePlayer(player.gameObject, rig);
            player.transform.SetPositionAndRotation(V(0, .05f, -1.5f), Quaternion.Euler(0, -12, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            BuilderTools.EnsureFolder(DataFolder);
            string settingsPath = DataFolder + "/HomeCameraSettings.asset";
            PlayerCameraSettings settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(
                settingsPath
            );
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
                var values = new SerializedObject(settings);
                values.FindProperty("startDistanceMeters").floatValue = 3.4f;
                values.FindProperty("minDistanceMeters").floatValue = 1.5f;
                values.FindProperty("maxDistanceMeters").floatValue = 4.2f;
                values.FindProperty("startPitchDegrees").floatValue = 16;
                values.FindProperty("pivotHeightMeters").floatValue = 1.5f;
                values.FindProperty("eyeHeightMeters").floatValue = 1.074f;
                values.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            BuilderTools.SetReference(rig, "cameraSettings", settings);
            Camera camera = rig.GetComponent<Camera>();
            camera.fieldOfView = 65;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 75;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }

        private static void ConfigureLighting(Scene scene)
        {
            Transform lighting = Root(scene, "Lighting");
            Light sun = lighting.GetComponentInChildren<Light>();
            sun.transform.rotation = Quaternion.Euler(44, 120, 0);
            sun.color = new UnityEngine.Color(1, .96f, .88f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .65f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new UnityEngine.Color(.82f, .85f, .88f);
            RenderSettings.ambientEquatorColor = new UnityEngine.Color(.78f, .73f, .64f);
            RenderSettings.ambientGroundColor = new UnityEngine.Color(.48f, .43f, .37f);
            RenderSettings.fog = false;
            Transform fillRoot = lighting.Find("RoomBounce");
            if (fillRoot == null)
                fillRoot = Group("RoomBounce", lighting, V(0, 2.9f, 0));
            Light fill = BuilderTools.GetOrAddComponent<Light>(fillRoot.gameObject);
            fill.type = LightType.Point;
            fill.color = new UnityEngine.Color(1, .94f, .84f);
            fill.intensity = 2.2f;
            fill.range = 10;
            fill.shadows = LightShadows.None;
            string profilePath = DataFolder + "/HomeColor.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                profile.Add<ColorAdjustments>(true);
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(.12f);
                bloom.threshold.Override(1.1f);
                foreach (VolumeComponent component in profile.components)
                    AssetDatabase.AddObjectToAsset(component, profile);
            }
            if (profile.TryGet(out ColorAdjustments adjustments))
            {
                adjustments.contrast.Override(5);
                adjustments.saturation.Override(5);
                adjustments.postExposure.Override(.25f);
                EditorUtility.SetDirty(adjustments);
            }
            Transform colorRoot = lighting.Find("HomeColor");
            if (colorRoot == null)
                colorRoot = Group("HomeColor", lighting, Vector3.zero);
            Volume volume = BuilderTools.GetOrAddComponent<Volume>(colorRoot.gameObject);
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            if (lighting.Find("CeilingPendant") == null)
                Put(Asset("CeilingPendant", CeilingPendant), lighting, V(0, 3.6f, 0));
        }

        private static GameObject CeilingPendant()
        {
            var root = new GameObject("CeilingPendant");
            Post("CeilingRose", root.transform, V(0, -.035f, 0), .14f, .07f, Brass);
            Post("Cord", root.transform, V(0, -.2f, 0), .018f, .35f, Ink);
            SmoothBall("Shade", root.transform, V(0, -.43f, 0), V(.96f, .32f, .96f), Ivory);
            SmoothBall(
                "WarmDiffuser",
                root.transform,
                V(0, -.52f, 0),
                V(.7f, .12f, .7f),
                Paint("Diffuser", "#FFF1C9")
            );
            return root;
        }

        internal static Transform Root(Scene scene, string name) =>
            scene.GetRootGameObjects().Single(root => root.name == name).transform;
    }
}
