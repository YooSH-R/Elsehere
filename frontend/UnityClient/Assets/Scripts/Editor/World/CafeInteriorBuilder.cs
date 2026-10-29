using System;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Characters;
using A305.EditorTools.Common;
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
using static A305.EditorTools.World.CafeInteriorKit;
using static A305.EditorTools.World.HomeInteriorKit;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>기존 카페의 기능 객체를 유지하며 Corner Cafe 외형을 최초 적용한다.</summary>
    public static class CafeInteriorBuilder
    {
        internal const string ScenePath = "Assets/Scenes/Production/CafeScene.unity";
        internal const string SandboxPath = "Assets/Scenes/Sandbox/CafeInteriorSandbox.unity";
        private const string DataFolder = "Assets/Data/Cafe";

        // Tripo 생성 메시를 마스터 골격에 연결한 Female Variant다. 골격·애니메이션·배율은 Female을 따른다.
        private const string BaristaPath = "Assets/Prefabs/Npc/Barista.prefab";

        [MenuItem("Tools/A305/Cafe/Apply Corner Cafe Design")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Transform environment = HomeInteriorBuilder.Root(scene, "Environment");
            if (environment.Find("CornerCafe") != null)
            {
                Debug.Log("CornerCafe already exists. Edit saved prefabs and placement directly.");
                return;
            }
            SpaceDoorway door = BuilderTools.FindSingleInScene<SpaceDoorway>(scene);
            HoverableObject[] words = BuilderTools.FindAllInScene<HoverableObject>(scene).ToArray();
            if (
                !words
                    .Select(item => item.UnityKey)
                    .OrderBy(key => key)
                    .SequenceEqual(new[] { "counter", "menu" })
            )
                throw new InvalidOperationException(
                    "Cafe word objects changed; inspect before applying design."
                );
            Transform oldRoom = door.transform.parent;
            foreach (Transform part in oldRoom)
            {
                if (
                    part.GetComponent<SpaceDoorway>() != null
                    || part.GetComponent<HoverableObject>() != null
                )
                    continue;
                part.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }
            Transform interior = Group("CornerCafe", environment, Vector3.zero);
            Put(Save("CafeShell", Shell), interior, Vector3.zero);
            Put(Save("CafeDecoration", Decoration), interior, Vector3.zero);
            Replace(
                words.Single(item => item.UnityKey == "counter"),
                Save("ServiceCounter", Counter, true),
                V(0, 0, 3.7f)
            );
            Replace(
                words.Single(item => item.UnityKey == "menu"),
                Save("MenuBoard", Menu),
                V(0, 2.9f, 5.77f)
            );
            Transform doorVisual = door.transform.Find("Visual");
            doorVisual.localScale = V(1.45f, 1.2f, 1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorVisual);
            ConfigurePeople(scene);
            ConfigureLighting(scene);
            AssetDatabase.SaveAssets();
            BuilderTools.SaveScene(scene, ScenePath);
            EditorSceneManager.SaveScene(scene, SandboxPath, true);
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log(
                "Corner Cafe saved with existing staff, hover, mission and doorway references."
            );
        }

        private static void Replace(HoverableObject target, GameObject prefab, Vector3 position)
        {
            // 기존 기능 루트와 단어 키는 유지하고 도형·글자 외형만 비활성화한다.
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (Collider collider in target.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            target.transform.localScale = Vector3.one;
            target.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.transform);
            Put(prefab, target.transform, Vector3.zero);
        }

        private static void ConfigurePeople(Scene scene)
        {
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            PlayerCharacterBuilder.ConfigurePlayer(player.gameObject, rig);
            player.transform.SetPositionAndRotation(V(0, .05f, -3f), Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);

            NpcIdentity staff = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == "CAFE_STAFF");
            Transform oldStaff = staff.transform.Find("Visual");
            oldStaff.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldStaff.gameObject);
            ConnectBarista(staff);
            SizeController(staff.GetComponent<CharacterController>());
            staff.transform.SetPositionAndRotation(V(0, .05f, 4.72f), Quaternion.Euler(0, 180, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(staff.transform);
            staff.LookTarget.localPosition = V(0, 1.12f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(staff.LookTarget);
            // 카운터와 직원의 기존 접근 거리·대화 설정은 그대로 둔다.
            BuilderTools.EnsureFolder(DataFolder);
            string path = DataFolder + "/CafeCameraSettings.asset";
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

        [MenuItem("Tools/A305/Cafe/Connect Barista Character")]
        public static void ConnectBaristaInScenes()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (string path in new[] { ScenePath, SandboxPath })
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                ConnectBarista(
                    BuilderTools
                        .FindAllInScene<NpcIdentity>(scene)
                        .Single(npc => npc.Code == "CAFE_STAFF")
                );
                BuilderTools.SaveScene(scene, path);
            }
            Debug.Log("Cafe staff now uses the Tripo barista character.");
        }

        /// <summary>카페 직원에 Tripo 바리스타 캐릭터를 연결한다. 위치·대화·시선 대상·캡슐은 유지한다.</summary>
        public static void ConnectBarista(NpcIdentity staff)
        {
            // 도형으로 만든 이전 임시 외형은 지우고 바리스타는 한 번만 붙인다.
            Transform placeholder = staff.transform.Find("BaristaVisual");
            if (placeholder != null)
                Object.DestroyImmediate(placeholder.gameObject);
            Transform barista = staff.transform.Find("Barista");
            if (barista == null)
            {
                barista = Put(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        BaristaPath,
                        "Cafe barista character"
                    ),
                    staff.transform,
                    Vector3.zero
                );
                barista.name = "Barista";
            }
            BuilderTools.SetReference(
                staff.GetComponent<CharacterAnimationDriver>(),
                "animator",
                barista.GetComponentInChildren<Animator>()
            );
            CafeDialogueBuilder.Connect(staff);
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
            sun.transform.rotation = Quaternion.Euler(45, 125, 0);
            sun.color = new UnityEngine.Color(1, .97f, .9f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .6f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new UnityEngine.Color(.8f, .85f, .88f);
            RenderSettings.ambientEquatorColor = new UnityEngine.Color(.77f, .75f, .69f);
            RenderSettings.ambientGroundColor = new UnityEngine.Color(.48f, .45f, .39f);
            RenderSettings.fog = false;
            Light fill = Group("CafeBounce", lighting, V(0, 3.7f, 0))
                .gameObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new UnityEngine.Color(1, .96f, .87f);
            fill.intensity = 2.1f;
            fill.range = 13;
            fill.shadows = LightShadows.None;
            GameObject pendant = Save("CafePendant", Pendant);
            foreach (
                Vector3 position in new[]
                {
                    V(-2.4f, 4.2f, 3.3f),
                    V(2.4f, 4.2f, 3.3f),
                    V(-4.8f, 4.2f, -2.9f),
                    V(4.5f, 4.2f, -2.9f),
                }
            )
                Put(pendant, lighting, position);

            string path = DataFolder + "/CafeColor.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(.2f);
                color.contrast.Override(6);
                color.saturation.Override(8);
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(.1f);
                bloom.threshold.Override(1.1f);
                foreach (VolumeComponent component in profile.components)
                    AssetDatabase.AddObjectToAsset(component, profile);
            }
            Volume volume = Group("CafeColor", lighting, Vector3.zero)
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
