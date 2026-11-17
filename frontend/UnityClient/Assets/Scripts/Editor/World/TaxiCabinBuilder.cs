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

        // 운전석 골반 자리(택시 기준)와 기사 배율. 머리 꼭대기는 지붕 안감(택시 기준 약 1.53m)보다 3cm 아래까지만 둔다.
        private static readonly Vector3 DriverSeat = new Vector3(-.38f, .75f, .01f);
        private const float DriverScale = 1.05f;
        private const float HeadTopLimit = 1.5f;

        [MenuItem("Tools/A305/World/Apply Taxi Cabin")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            Configure(scene, BuilderTools.FindSingleInScene<TaxiRide>(scene));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/World/Align Burger Taxi Stop")]
        public static void AlignBurgerTaxiStop()
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            var burger = TownStorefrontBuilder.Storefronts.Single(storefront =>
                storefront.PlaceCode == "DINER"
            );
            foreach (TaxiRide taxi in BuilderTools.FindAllInScene<TaxiRide>(scene))
            {
                var fields = new SerializedObject(taxi);
                SerializedProperty destinations = fields.FindProperty("destinations");
                for (int index = 0; index < destinations.arraySize; index++)
                {
                    SerializedProperty destination = destinations.GetArrayElementAtIndex(index);
                    if (destination.FindPropertyRelative("code").stringValue != burger.PlaceCode)
                        continue;
                    SetDestination(
                        destination,
                        burger.PlaceCode,
                        burger.KoreanName,
                        burger.TaxiStop,
                        burger.TaxiAlighting
                    );
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
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
            ConfigureRearSeats(cabin);
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
            FillDestinations(scene, fields.FindProperty("destinations"), true);
            fields.ApplyModifiedPropertiesWithoutUndo();
            ConfigureRoute(taxi.GetComponent<LoopingCar>());
            ConfigureStatus(panel);
        }

        internal static void ConfigureRearSeats(Transform cabin)
        {
            Material upholstery = MainstreetArtKit.Color("TaxiUpholstery", "#B9A17C");
            // 차체가 위로 갈수록 좁아지고 뒷유리가 앞으로 기울어져 있어 등받이를 더 좁고 앞에 둔다.
            Piece(
                cabin,
                "RearSeat",
                new Vector3(0, .65f, -.77f),
                new Vector3(1.28f, .16f, .50f),
                upholstery
            );
            Piece(
                cabin,
                "RearBack",
                new Vector3(0, .94f, -.95f),
                new Vector3(1.18f, .52f, .12f),
                upholstery
            );
        }

        /// <summary>
        /// 마을 장소 목록(기존 공간 4곳과 거리 매장)을 정차·하차 지점과 함께 채운다. 택시와 순찰차가 같은 목록을 쓴다.
        /// 순찰차는 경찰서 자신으로 출동하지 않으므로 includePoliceStation을 끈다.
        /// </summary>
        internal static void FillDestinations(
            Scene scene,
            SerializedProperty destinations,
            bool includePoliceStation
        )
        {
            // 거리 매장(경찰서 포함)과 북쪽 상가처럼 마을 건물 안의 장소는 거리 매장의 정차 지점만 쓰고 문마다 목적지를 만들지 않는다.
            SpaceDoorway[] doors = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Where(door => !PlaceScenes.IsTownShop(door.DestinationScene))
                .Where(door =>
                    TownStorefrontBuilder.Storefronts.All(storefront =>
                        storefront.PlaceCode != PlaceScenes.CodeOf(door.DestinationScene)
                    )
                )
                .ToArray();
            TownStorefrontBuilder.Storefront[] storefronts = TownStorefrontBuilder
                .Storefronts.Where(storefront =>
                    includePoliceStation || storefront.Name != "PoliceStation"
                )
                .ToArray();
            destinations.arraySize = doors.Length + storefronts.Length;
            int destinationIndex = 0;
            foreach (SpaceDoorway door in doors)
            {
                string code = PlaceScenes.CodeOf(door.DestinationScene);
                string label = PlaceScenes.NameOf(code);
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
            foreach (var storefront in storefronts)
            {
                SetDestination(
                    destinations.GetArrayElementAtIndex(destinationIndex++),
                    storefront.PlaceCode,
                    storefront.KoreanName,
                    storefront.TaxiStop,
                    storefront.TaxiAlighting
                );
            }
        }

        internal static void SetDestination(
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
            SeatAtTheWheel(taxi, driver);
            Transform head = driver
                .GetComponentInChildren<Animator>(true)
                .GetBoneTransform(HumanBodyBones.Head);
            identity.LookTarget.position = head.position;
            foreach (Transform bone in driver.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        }

        /// <summary>
        /// KayKit 도시 차(택시·승용차·순찰차는 같은 차체)의 운전석에 운전자를 앉히고 발과 손을 페달·핸들에 맞춘다.
        /// driver는 차의 바로 아래 자식이고, 그 아래에 Animator가 있는 모델이 있다. 차에는 SteeringWheel·SteeringColumn이 있어야 한다.
        /// </summary>
        internal static void SeatAtTheWheel(Transform taxi, Transform driver)
        {
            Animator animator = driver.GetComponentInChildren<Animator>(true);
            driver.localRotation = Quaternion.Euler(0, 90, 0);
            SeatDriver(taxi, driver, animator, DriverScale);
            // 머리가 큰 기사는 머리 꼭대기가 지붕 안감 아래에 오도록 줄여 앉힌다. 좌석에서 머리 꼭대기까지는 배율에 비례한다.
            float rise = HighestPoint(taxi, driver) - DriverSeat.y;
            float allowedRise = HeadTopLimit - DriverSeat.y;
            if (rise > allowedRise)
                SeatDriver(taxi, driver, animator, DriverScale * allowedRise / rise);
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
            VehicleDriverBuilder.FitSteering(taxi, driver);
        }

        // 기본 자세(근육 0)로 펴고 골반을 운전석에 맞춘다.
        private static void SeatDriver(
            Transform taxi,
            Transform driver,
            Animator animator,
            float scale
        )
        {
            driver.localScale = Vector3.one * scale;
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                Array.Clear(pose.muscles, 0, pose.muscles.Length);
                handler.SetHumanPose(ref pose);
            }
            animator.enabled = false;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            driver.position += taxi.TransformPoint(DriverSeat) - hips.position;
        }

        // 기사 스킨 메시와 모자 같은 장신구 메시의 가장 높은 점(택시 기준 높이)
        private static float HighestPoint(Transform taxi, Transform driver)
        {
            float highest = float.NegativeInfinity;
            var baked = new Mesh();
            try
            {
                foreach (var skin in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.BakeMesh(baked, true);
                    foreach (Vector3 vertex in baked.vertices)
                        highest = Mathf.Max(
                            highest,
                            taxi.InverseTransformPoint(skin.transform.TransformPoint(vertex)).y
                        );
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
            foreach (var filter in driver.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    highest = Mathf.Max(
                        highest,
                        taxi.InverseTransformPoint(filter.transform.TransformPoint(vertex)).y
                    );
            }
            return highest;
        }

        internal static void SolveLimb(
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

        internal static void ConfigureStatus(DialoguePanelView panel)
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
