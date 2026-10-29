using System;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.EditorTools.Player;
using A305.Npc;
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
using static A305.EditorTools.World.HotelInteriorKit;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>기존 호텔 출입 연결을 유지하며 Maple House 로비를 최초 적용한다.</summary>
    public static class HotelInteriorBuilder
    {
        internal const string ScenePath = "Assets/Scenes/Production/HotelScene.unity";
        internal const string SandboxPath = "Assets/Scenes/Sandbox/HotelInteriorSandbox.unity";
        private const string DataFolder = "Assets/Data/Hotel";
        internal const string StaffCode = "HOTEL_STAFF";

        // 사용자가 외관을 승인해 Tripo로 최종 등록한 호텔 직원(Jake)이다. 마스터 골격과 이동 애니메이션을 쓴다.
        private const string HotelierPath =
            "Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab";

        [MenuItem("Tools/A305/Hotel/Apply Maple House Design")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Transform environment = HomeInteriorBuilder.Root(scene, "Environment");
            if (environment.Find("MapleHouse") != null)
            {
                Debug.Log("MapleHouse already exists. Edit saved prefabs and placement directly.");
                return;
            }
            // 새 단어 키가 추가된 호텔에 기존 도형 교체를 무조건 적용하지 않는다.
            if (BuilderTools.FindAllInScene<HoverableObject>(scene).Length != 0)
                throw new InvalidOperationException(
                    "Hotel word objects changed; inspect before applying design."
                );
            SpaceDoorway door = BuilderTools.FindSingleInScene<SpaceDoorway>(scene);
            foreach (Transform part in door.transform.parent)
            {
                if (part.GetComponent<SpaceDoorway>() != null)
                    continue;
                part.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }
            Transform interior = Group("MapleHouse", environment, Vector3.zero);
            Put(Save("LobbyShell", Shell), interior, Vector3.zero);
            Put(Save("LobbyDecoration", Decoration), interior, Vector3.zero);
            Transform doorVisual = door.transform.Find("Visual");
            doorVisual.localScale = V(1.55f, 1.3f, 1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorVisual);
            BuilderTools.EnsureFolder(DataFolder);
            PlaceStaff(scene);
            SpaceSceneSetup.Apply(scene);
            ConfigurePeople(scene);
            ConfigureLighting(scene);
            AssetDatabase.SaveAssets();
            BuilderTools.SaveScene(scene, ScenePath);
            EditorSceneManager.SaveScene(scene, SandboxPath, true);
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log("Maple House saved with HOTEL_STAFF and original town doorway.");
        }

        private static void PlaceStaff(Scene scene)
        {
            if (BuilderTools.FindAllInScene<NpcIdentity>(scene).Any(npc => npc.Code == StaffCode))
                return;
            GameObject source = BuilderTools.LoadRequiredAsset<GameObject>(
                NpcBuilder.PasserbyPrefabPath,
                NpcBuilder.MenuPath
            );
            GameObject staff = BuilderTools.InstantiatePrefab(
                source,
                HomeInteriorBuilder.Root(scene, "Characters"),
                V(0, .05f, 5.7f),
                Quaternion.Euler(0, 180, 0)
            );
            staff.name = "HotelStaff";
            BuilderTools.SetText(staff.GetComponent<NpcIdentity>(), "code", StaffCode);
            string settingsPath = DataFolder + "/HotelStaffSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PasserbySettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PasserbySettings>();
                var fields = new SerializedObject(settings);
                fields.FindProperty("detectRadiusMeters").floatValue = 3.5f;
                fields.FindProperty("offerDistanceMeters").floatValue = 2.5f;
                fields.FindProperty("restSeconds").floatValue = 20;
                fields.FindProperty("canDecline").boolValue = true;
                fields.FindProperty("walkSpeedMetersPerSecond").floatValue = 0;
                fields.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            BuilderTools.SetReference(staff.GetComponent<Passerby>(), "settings", settings);
            string linesPath = DataFolder + "/MockHotelLines.asset";
            var lines = AssetDatabase.LoadAssetAtPath<MockNpcLineSource>(linesPath);
            if (lines == null)
            {
                lines = ScriptableObject.CreateInstance<MockNpcLineSource>();
                var fields = new SerializedObject(lines);
                SerializedProperty entries = fields.FindProperty("entries");
                entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("npcCode").stringValue =
                    StaffCode;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("greeting").stringValue =
                    "Welcome to Maple House! Are you checking in?";
                fields.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(lines, linesPath);
            }
            BuilderTools.SetReference(staff.GetComponent<Passerby>(), "lineSource", lines);
        }

        private static void ConfigurePeople(Scene scene)
        {
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            PlayerCharacterBuilder.ConfigurePlayer(player.gameObject, rig);
            player.transform.SetPositionAndRotation(V(0, .05f, -4.3f), Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);

            NpcIdentity staff = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == StaffCode);
            Transform oldStaff = staff.transform.Find("Visual");
            oldStaff.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldStaff.gameObject);
            ConnectHotelier(staff);
            SizeController(staff.GetComponent<CharacterController>());
            staff.LookTarget.localPosition = V(0, 1.12f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(staff.LookTarget);

            string path = DataFolder + "/HotelCameraSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
                var data = new SerializedObject(settings);
                data.FindProperty("startDistanceMeters").floatValue = 3.8f;
                data.FindProperty("minDistanceMeters").floatValue = 1.5f;
                data.FindProperty("maxDistanceMeters").floatValue = 5.2f;
                data.FindProperty("startPitchDegrees").floatValue = 15;
                data.FindProperty("pivotHeightMeters").floatValue = 1.5f;
                data.FindProperty("eyeHeightMeters").floatValue = 1.074f;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(settings, path);
            }
            BuilderTools.SetReference(rig, "cameraSettings", settings);
            Camera camera = rig.GetComponent<Camera>();
            camera.fieldOfView = 65;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 90;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }

        [MenuItem("Tools/A305/Hotel/Connect Hotelier Character")]
        public static void ConnectHotelierInScenes()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (string path in new[] { ScenePath, SandboxPath })
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                ConnectHotelier(
                    BuilderTools
                        .FindAllInScene<NpcIdentity>(scene)
                        .Single(npc => npc.Code == StaffCode)
                );
                BuilderTools.SaveScene(scene, path);
            }
            Debug.Log("Hotel staff now uses the Tripo hotelier character.");
        }

        /// <summary>호텔 직원에 Tripo 호텔리어 캐릭터를 연결한다. 위치·대화·시선 대상·캡슐은 유지한다.</summary>
        public static void ConnectHotelier(NpcIdentity staff)
        {
            // 리틀 멍 인형으로 만든 이전 임시 외형은 지우고 호텔리어는 한 번만 붙인다.
            Transform placeholder = staff.transform.Find("ConciergeVisual");
            if (placeholder != null)
                Object.DestroyImmediate(placeholder.gameObject);
            Transform hotelier = staff.transform.Find("Hotelier");
            if (hotelier == null)
            {
                hotelier = Put(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        HotelierPath,
                        "Tripo hotelier character"
                    ),
                    staff.transform,
                    Vector3.zero
                );
                hotelier.name = "Hotelier";
            }
            BuilderTools.SetReference(
                staff.GetComponent<CharacterAnimationDriver>(),
                "animator",
                hotelier.GetComponentInChildren<Animator>()
            );
        }

        private static void SizeController(CharacterController controller)
        {
            controller.height = 1.6f;
            controller.center = V(0, .8f, 0);
            controller.radius = .36f;
            controller.stepOffset = .15f;
            controller.skinWidth = .02f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        private static void ConfigureLighting(Scene scene)
        {
            Transform lighting = HomeInteriorBuilder.Root(scene, "Lighting");
            Light sun = lighting.GetComponentInChildren<Light>();
            sun.transform.rotation = Quaternion.Euler(48, 130, 0);
            sun.color = new UnityEngine.Color(1, .97f, .91f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .6f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new UnityEngine.Color(.8f, .85f, .89f);
            RenderSettings.ambientEquatorColor = new UnityEngine.Color(.76f, .75f, .7f);
            RenderSettings.ambientGroundColor = new UnityEngine.Color(.48f, .45f, .4f);
            RenderSettings.fog = false;
            Light fill = Group("LobbyBounce", lighting, V(0, 4.15f, 0))
                .gameObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new UnityEngine.Color(1, .95f, .85f);
            fill.intensity = 2.1f;
            fill.range = 15;
            fill.shadows = LightShadows.None;
            foreach (float z in new[] { -2.7f, 2.2f })
                Put(Save("LobbyChandelier", Chandelier), lighting, V(0, 4.6f, z));
            foreach (float x in new[] { -4.15f, 4.15f })
                Put(Save("GlobeSconce", Sconce), lighting, V(x, 2.15f, 6.8f));

            string path = DataFolder + "/HotelColor.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(.2f);
                color.contrast.Override(6);
                color.saturation.Override(6);
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(.12f);
                bloom.threshold.Override(1.1f);
                foreach (VolumeComponent component in profile.components)
                    AssetDatabase.AddObjectToAsset(component, profile);
            }
            Volume volume = Group("HotelColor", lighting, Vector3.zero)
                .gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cameraData);
        }
    }
}
