using System;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Npc;
using A305.Player;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>승인된 택시 좌석·뒷좌석 대화 시점만 적용한다. 다른 거리·대화창 디자인은 재생성하지 않는다.</summary>
    public static class TaxiCabinBuilder
    {
        private const string TownPath = "Assets/Scenes/Production/TownScene.unity";
        private const float SouthEnd = -25;
        private const float NorthEnd = 66;

        [MenuItem("Tools/A305/World/Apply Taxi Cabin")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            Configure(scene, BuilderTools.FindSingleInScene<TaxiRide>(scene));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        internal static void Configure(Scene scene, TaxiRide taxi)
        {
            Transform root = taxi.transform;
            var fields = new SerializedObject(taxi);
            var driver = (NpcIdentity)fields.FindProperty("driver").objectReferenceValue;
            PoseDriver(root, driver);
            Transform cabin = root.Find("PassengerCabin");
            if (cabin == null)
                cabin = BuilderTools.CreateEmpty(
                    "PassengerCabin",
                    root,
                    Vector3.zero,
                    Quaternion.identity
                );
            Material upholstery = MainstreetArtKit.Color("TaxiUpholstery", "#B9A17C");
            Material trim = MainstreetArtKit.Color("TaxiTrim", "#504C46");
            Piece(cabin, "Floor", new Vector3(0, .40f, -.25f), new Vector3(1.5f, .06f, 2.0f), trim);
            foreach (float x in new[] { -.38f, .38f })
            {
                string side = x < 0 ? "Driver" : "FrontPassenger";
                Piece(
                    cabin,
                    side + "Seat",
                    new Vector3(x, .65f, .0f),
                    new Vector3(.62f, .16f, .62f),
                    upholstery
                );
                Transform back = Piece(
                    cabin,
                    side + "Back",
                    new Vector3(x, .82f, -.27f),
                    new Vector3(.61f, .38f, .12f),
                    upholstery
                );
                back.localRotation = Quaternion.Euler(-8, 0, 0);
                Piece(
                    cabin,
                    side + "Headrest",
                    new Vector3(x, 1.08f, -.31f),
                    new Vector3(.29f, .17f, .13f),
                    upholstery
                );
            }
            Piece(
                cabin,
                "DriverFootrest",
                new Vector3(-.38f, .58f, .15f),
                new Vector3(.45f, .08f, .38f),
                trim
            );
            Piece(
                cabin,
                "RearSeat",
                new Vector3(0, .65f, -.97f),
                new Vector3(1.42f, .16f, .56f),
                upholstery
            );
            Piece(
                cabin,
                "RearBack",
                new Vector3(0, .95f, -1.24f),
                new Vector3(1.42f, .60f, .12f),
                upholstery
            );
            Piece(
                cabin,
                "Dashboard",
                new Vector3(0, .85f, .61f),
                new Vector3(1.50f, .18f, .33f),
                trim
            );
            Piece(
                cabin,
                "DashboardTop",
                new Vector3(0, .95f, .63f),
                new Vector3(1.45f, .025f, .28f),
                upholstery
            );
            Piece(
                cabin,
                "RoofLiner",
                new Vector3(0, 1.54f, -.22f),
                new Vector3(1.50f, .025f, 1.97f),
                upholstery
            );
            foreach (float x in new[] { -.76f, .76f })
                Piece(
                    cabin,
                    x < 0 ? "LeftDoorTrim" : "RightDoorTrim",
                    new Vector3(x, .75f, -.33f),
                    new Vector3(.04f, .30f, 1.75f),
                    trim
                );
            Transform camera = cabin.Find("PassengerView");
            if (camera == null)
                camera = BuilderTools.CreateEmpty(
                    "PassengerView",
                    cabin,
                    Vector3.zero,
                    Quaternion.identity
                );
            camera.localPosition = new Vector3(.40f, 1.27f, -.96f);
            camera.localRotation = Quaternion.LookRotation(
                new Vector3(-.28f, 1.10f, .8f) - camera.localPosition
            );
            fields.FindProperty("passengerView").objectReferenceValue = camera;
            DialoguePanelView panel = BuilderTools.FindSingleInScene<DialoguePanelView>(scene);
            fields.FindProperty("panel").objectReferenceValue = panel;
            SpaceDoorway[] doors = BuilderTools.FindAllInScene<SpaceDoorway>(scene);
            SerializedProperty destinations = fields.FindProperty("destinations");
            destinations.arraySize = doors.Length + TownStorefrontBuilder.Storefronts.Length;
            int destinationIndex = 0;
            foreach (SpaceDoorway door in doors)
            {
                string code = door.DestinationScene.Replace("Scene", "").ToUpperInvariant();
                string label =
                    code == "HOME" ? "집"
                    : code == "HOTEL" ? "호텔"
                    : code == "CAFE" ? "카페"
                    : "드럭스토어";
                Vector3 stop = new Vector3(
                    door.transform.position.x > 0 ? 1.5f : -1.5f,
                    0,
                    door.transform.position.z
                );
                Vector3 exit = new Vector3(stop.x > 0 ? 4.15f : -4.15f, .1f, stop.z);
                SetDestination(
                    destinations.GetArrayElementAtIndex(destinationIndex++),
                    code,
                    label,
                    stop,
                    exit
                );
            }
            foreach (var storefront in TownStorefrontBuilder.Storefronts)
            {
                string code = storefront.Name switch
                {
                    "PostOffice" => "POST_OFFICE",
                    "MapleDiner" => "DINER",
                    "TownBank" => "BANK",
                    "SudsLaundromat" => "LAUNDROMAT",
                    "BloomFlowers" => "FLOWER_SHOP",
                    "MainStreetBarber" => "BARBER",
                    _ => throw new InvalidOperationException(
                        "택시 장소 코드가 없습니다: " + storefront.Name
                    ),
                };
                SetDestination(
                    destinations.GetArrayElementAtIndex(destinationIndex++),
                    code,
                    storefront.KoreanName,
                    storefront.TaxiStop,
                    storefront.TaxiAlighting
                );
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            ConfigureRoute(taxi.GetComponent<LoopingCar>());
            ConfigureStatus(panel);
        }

        private static void SetDestination(
            SerializedProperty target,
            string code,
            string name,
            Vector3 stop,
            Vector3 exit
        )
        {
            target.FindPropertyRelative("code").stringValue = code;
            target.FindPropertyRelative("displayName").stringValue = name;
            target.FindPropertyRelative("stop").vector3Value = stop;
            target.FindPropertyRelative("alighting").vector3Value = exit;
        }

        private static void ConfigureRoute(LoopingCar taxi)
        {
            var route = new CarLoopRoute(SouthEnd, NorthEnd, 1.5f);
            var fields = new SerializedObject(taxi);
            fields.FindProperty("southZ").floatValue = SouthEnd;
            fields.FindProperty("northZ").floatValue = NorthEnd;
            fields.FindProperty("startDistance").floatValue = route.NorthboundDistance(-1.2f);
            fields.FindProperty("speedMetersPerSecond").floatValue = 6;
            fields.FindProperty("parkedTruckBypassOffset").floatValue = 1.9f;
            var stops = fields.FindProperty("signalStops");
            stops.arraySize = 2;
            stops.GetArrayElementAtIndex(0).floatValue = route.NorthboundDistance(3.2f);
            stops.GetArrayElementAtIndex(1).floatValue = route.SouthboundDistance(16.8f);
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PoseDriver(Transform taxi, NpcIdentity identity)
        {
            Transform driver = identity.transform;
            Animator animator = driver.GetComponentInChildren<Animator>(true);
            driver.localScale = Vector3.one * 1.05f;
            driver.localRotation = Quaternion.Euler(0, 90, 0);
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                Array.Clear(pose.muscles, 0, pose.muscles.Length);
                handler.SetHumanPose(ref pose);
            }
            animator.enabled = false;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            driver.position += taxi.TransformPoint(new Vector3(-.38f, .75f, .01f)) - hips.position;
            SolveLimb(
                animator,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                taxi.TransformPoint(new Vector3(-.46f, .62f, .13f)),
                taxi.forward
            );
            SolveLimb(
                animator,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                taxi.TransformPoint(new Vector3(-.29f, .62f, .13f)),
                taxi.forward
            );
            SolveLimb(
                animator,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                taxi.TransformPoint(new Vector3(-.49f, .90f, .25f)),
                -taxi.right
            );
            SolveLimb(
                animator,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                taxi.TransformPoint(new Vector3(-.27f, .90f, .25f)),
                taxi.right
            );
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            identity.LookTarget.position = head.position;
            foreach (Transform bone in driver.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            Transform wheel = taxi.Find("SteeringWheel");
            Transform column = taxi.Find("SteeringColumn");
            if (wheel != null)
            {
                wheel.localPosition = new Vector3(-.38f, .90f, .26f);
                wheel.localRotation = Quaternion.Euler(65, 0, 0);
            }
            if (column != null)
            {
                column.localPosition = new Vector3(-.38f, .79f, .42f);
                column.localRotation = Quaternion.Euler(65, 0, 0);
            }
        }

        private static void SolveLimb(
            Animator animator,
            HumanBodyBones rootBone,
            HumanBodyBones middleBone,
            HumanBodyBones endBone,
            Vector3 target,
            Vector3 bend
        )
        {
            Transform root = animator.GetBoneTransform(rootBone);
            Transform middle = animator.GetBoneTransform(middleBone);
            Transform end = animator.GetBoneTransform(endBone);
            Vector3 start = root.position;
            float upper = Vector3.Distance(start, middle.position);
            float lower = Vector3.Distance(middle.position, end.position);
            Vector3 direction = (target - start).normalized;
            float distance = Mathf.Clamp(
                Vector3.Distance(start, target),
                Mathf.Abs(upper - lower) + .001f,
                upper + lower - .001f
            );
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            Vector3 perpendicular = Vector3.ProjectOnPlane(bend, direction).normalized;
            Vector3 elbow =
                start
                + direction * along
                + perpendicular * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            root.rotation =
                Quaternion.FromToRotation(middle.position - start, elbow - start) * root.rotation;
            middle.rotation =
                Quaternion.FromToRotation(
                    end.position - middle.position,
                    start + direction * distance - middle.position
                ) * middle.rotation;
        }

        private static Transform Piece(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 size,
            Material material
        )
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.localPosition = position;
                Vector3 meshSize = existing.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                existing.localScale = new Vector3(
                    size.x / meshSize.x,
                    size.y / meshSize.y,
                    size.z / meshSize.z
                );
                return existing;
            }
            return MainstreetArtKit.Block(name, parent, position, size, material);
        }

        private static void ConfigureStatus(DialoguePanelView panel)
        {
            Transform canvas = panel.GetComponentInParent<Canvas>().transform;
            Transform existing = canvas.Find("TaxiTripStatus");
            if (existing != null && existing.GetComponent<TMP_Text>() != null)
                Object.DestroyImmediate(existing.gameObject);
            Transform plate = canvas.Find("TaxiTripStatus");
            if (plate == null)
            {
                plate = new GameObject(
                    "TaxiTripStatus",
                    typeof(RectTransform),
                    typeof(Image)
                ).transform;
                plate.SetParent(canvas, false);
            }
            var rect = (RectTransform)plate;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -40);
            rect.sizeDelta = new Vector2(620, 68);
            plate.GetComponent<Image>().raycastTarget = false;
            TMP_Text text = UiBuilderTools.FindOrCreateText(
                plate,
                "Label",
                UiBuilderTools.BoldFont(),
                26,
                new Color(.27f, .24f, .20f)
            );
            var labelRect = (RectTransform)text.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24, 12);
            labelRect.offsetMax = new Vector2(-24, -12);
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            var theme = BuilderTools.GetOrAddComponent<ThemedElement>(plate.gameObject);
            BuilderTools.SetReference(
                theme,
                "catalog",
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(
                    "Assets/Data/Theme/UiThemeCatalog.asset"
                )
            );
            var themeFields = new SerializedObject(theme);
            themeFields.FindProperty("role").enumValueIndex = (int)UiThemeRole.Panel;
            themeFields.ApplyModifiedPropertiesWithoutUndo();
            theme.ApplyCurrent();
            BuilderTools.SetReference(panel, "vehicleStatusText", text);
            plate.gameObject.SetActive(false);
        }

        public static void Capture()
        {
            ShaderUtil.allowAsyncCompilation = false;
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            TaxiRide taxi = BuilderTools.FindSingleInScene<TaxiRide>(scene);
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                canvas.gameObject.SetActive(false);
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            Camera camera =
                player.CameraRig != null
                    ? player.CameraRig.GetComponent<Camera>()
                    : BuilderTools.FindSingleInScene<PlayerCameraRig>(scene).GetComponent<Camera>();
            camera.nearClipPlane = .025f;
            string output = Environment.GetEnvironmentVariable("TAXI_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Set TAXI_QA_OUTPUT.");
            Directory.CreateDirectory(output);
            File.WriteAllLines(
                Path.Combine(output, "south-road-objects.txt"),
                BuilderTools
                    .FindAllInScene<Transform>(scene)
                    .Where(t =>
                        Mathf.Abs(t.position.x) < 2.5f && t.position.z < -15 && t.position.y < .3f
                    )
                    .Select(t => $"{t.name} {t.position} parent={t.parent}")
            );
            foreach (
                var shot in new[]
                {
                    (
                        "taxi-seat-side",
                        new Vector3(-3.4f, 1.45f, .7f),
                        new Vector3(1.1f, 1.0f, -1.2f),
                        45f
                    ),
                    (
                        "taxi-seat-front",
                        new Vector3(.2f, 1.55f, 3.6f),
                        new Vector3(1.1f, 1.0f, -1.2f),
                        44f
                    ),
                }
            )
            {
                camera.transform.SetPositionAndRotation(
                    shot.Item2,
                    Quaternion.LookRotation(shot.Item3 - shot.Item2)
                );
                camera.fieldOfView = shot.Item4;
                SaveRender(camera, output, shot.Item1);
            }
            Transform anchor = taxi.transform.Find("PassengerCabin/PassengerView");
            camera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            camera.fieldOfView = 72;
            var head = taxi.GetComponentInChildren<Animator>(true)
                .GetBoneTransform(HumanBodyBones.Head);
            head.rotation = Quaternion.AngleAxis(80, taxi.transform.up) * head.rotation;
            SaveRender(camera, output, "taxi-cabin");
        }

        private static void SaveRender(Camera camera, string output, string name)
        {
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = 1600f / 900;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }
    }
}
