using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.EditorTools.ObjectHover;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 마을 남서쪽 끝 1층 배경 건물을 경찰서로 바꾸고(<see cref="TownStorefrontBuilder"/>의 경찰서 매장),
    /// 그 남쪽 잔디에 주차장을 깔아 순찰차를 세운다. 창구 뒤에는 경찰관 Dan(POLICE_OFFICER)을 세우고,
    /// 신고를 받으면 순찰차가 출동하도록 창구(<see cref="PoliceReportDesk"/>)와 순찰차(<see cref="PoliceCar"/>)를 잇는다.
    /// 주차장·순찰차 프리팹·경찰관 외형은 없을 때만 만들고, 이미 있으면 디자인을 두고 기능 연결만 맞춘다.
    /// </summary>
    public static class PoliceStationBuilder
    {
        private const string MenuPath = "Tools/A305/World/Apply Police Station";
        private const string Folder = "Assets/Prefabs/World/PoliceStation";
        private const string ArtFolder = "Assets/Art/World/PoliceStation";
        private const string CarPrefabPath = Folder + "/PoliceCar.prefab";
        private const string CarModelPath = "Assets/ThirdParty/KayKitCityBits/car_police.obj";
        private const string CarMaterialFolder = "Assets/Art/World/StreetLife/Materials";
        private const string OfficerSettingsPath = "Assets/Data/Npc/PoliceOfficerSettings.asset";
        private const string MockLinesPath = "Assets/Data/Npc/MockNpcLines.asset";
        private const string OfficerCode = "POLICE_OFFICER";
        private const string HotelierPath =
            "Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab";
        private const string HotelierFolder =
            "Assets/Art/Characters/Tripo/LittleMung_HotelStaff_male";
        private const string UniformTexturePath = ArtFolder + "/PoliceOfficerUniform.png";
        private const string UniformMaterialPath = ArtFolder + "/PoliceOfficerUniform.mat";

        // 순찰차가 도는 본길. 남쪽 끝은 택시와 같이 주차 트럭 우회가 끝나는 z=-25다.
        private const float SouthEnd = -25f;
        private const float NorthEnd = 66f;
        private const float LaneOffset = 1.5f;
        private const float NorthboundStopZ = 3.2f;
        private const float SouthboundStopZ = 16.8f;

        // 주차장은 경찰서 남쪽 잔디(x -14.8~-4.8, z -24.4~-29.9), 진입 포장은 본길 끝과 주차장 사이(x -4.8~3, z -24~-27.4)다.
        // 동쪽 끝 정원수(-3.7, -27.8)를 피해 진입로는 그 북쪽 z≈-26으로 지난다.
        private static readonly Vector3 LotCenter = new Vector3(-9.8f, 0, -27.15f);
        private static readonly Vector2 LotSize = new Vector2(10f, 5.5f);
        private static readonly Vector3 ApronCenter = new Vector3(-.9f, 0, -25.7f);
        private static readonly Vector2 ApronSize = new Vector2(7.8f, 3.4f);

        // 순찰차는 동쪽을 보고 주차 칸에 선다.
        private static readonly Vector3 Bay = new Vector3(-9f, 0, -28.8f);

        // 주차 칸 → 동쪽으로 나와 북쪽으로 비켜 진입 포장을 지나 → 북행 차로 시작점(1.5, -25)
        private static readonly Vector3[] ExitCurve =
        {
            Bay,
            new Vector3(-7.2f, 0, -28.8f),
            new Vector3(-7f, 0, -26.4f),
            new Vector3(-5.2f, 0, -26.4f),
            new Vector3(-1f, 0, -26.4f),
            new Vector3(1.5f, 0, -27.2f),
            new Vector3(1.5f, 0, SouthEnd),
        };

        // 남행 차로 끝(-1.5, -25) → 서쪽으로 꺾어 주차장 북쪽을 지나 → 서쪽 끝에서 유턴해 → 동쪽을 보고 주차 칸
        private static readonly Vector3[] EntryCurve =
        {
            new Vector3(-1.5f, 0, SouthEnd),
            new Vector3(-1.5f, 0, -25.8f),
            new Vector3(-2.3f, 0, -26f),
            new Vector3(-4f, 0, -26f),
            new Vector3(-6.3f, 0, -26f),
            new Vector3(-8.7f, 0, -26f),
            new Vector3(-11f, 0, -26f),
            new Vector3(-12.87f, 0, -26f),
            new Vector3(-12.87f, 0, -28.8f),
            new Vector3(-11f, 0, -28.8f),
            new Vector3(-10.33f, 0, -28.8f),
            new Vector3(-9.67f, 0, -28.8f),
            Bay,
        };

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuilderTools.EnsureFolder(Folder);
            BuilderTools.EnsureFolder(ArtFolder);
            TownStorefrontBuilder.BuildPrefabs();
            NpcBuilder.PrepareMockLines();
            ObjectWordAuthoring.FindOrCreateWord(
                "police_car",
                "police car",
                "경찰차",
                "A police car is parked next to the station.",
                "경찰서 옆에 경찰차가 서 있어요."
            );
            ObjectWordAuthoring.FindOrCreateWord(
                "police_officer",
                "police officer",
                "경찰관",
                "The police officer asked me what happened.",
                "경찰관이 무슨 일이 있었는지 물었어요."
            );
            ObjectWordAuthoring.FindOrCreateWord(
                "parking_lot",
                "parking lot",
                "주차장",
                "The police car is in the parking lot.",
                "경찰차가 주차장에 있어요."
            );
            ObjectWordRegistration.PrepareSource();
            GameObject carPrefab = BuilderTools.CreatePrefabIfMissing(CarPrefabPath, BuildCar);

            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            TownStorefrontBuilder.Install(scene);
            Transform mainstreet = TownProductionBuilder
                .Root(scene, "Environment")
                .Find("TownMainstreet");
            Transform station = mainstreet.Find("BackgroundBuildings/PoliceStation");
            float bayHeight = Parking(mainstreet);
            PoliceCar car = PatrolCar(
                scene,
                mainstreet.Find("PoliceParking"),
                carPrefab,
                bayHeight
            );
            NpcIdentity officer = Officer(scene, station);
            Desk(scene, officer, car);
            TaxiCabinBuilder.Configure(scene, BuilderTools.FindSingleInScene<TaxiRide>(scene));
            BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("경찰서와 순찰차를 TownScene에 적용했습니다.");
        }

        // ---------- 주차장 ----------

        // 잔디 높이에서 진입 포장 높이까지 채운 판을 깐다. 이미 있으면 배치는 그대로 두고 단어만 맞춘 뒤 높이를 읽는다.
        private static float Parking(Transform mainstreet)
        {
            Transform parking = mainstreet.Find("PoliceParking");
            if (parking != null)
            {
                BindParkingWords(parking);
                return parking.Find("Lot").position.y + parking.Find("Lot").localScale.y / 2;
            }

            float lawn = GroundAt(LotCenter);
            float top = Mathf.Max(lawn, 0) + .02f;
            parking = Group("PoliceParking", mainstreet, Vector3.zero);
            Material asphalt = Color("PoliceLotAsphalt", "#5E6670");
            Material paint = Color("PoliceLotPaint", "#F2F0E8");
            Pad("Lot", parking, LotCenter, LotSize, lawn, top, asphalt);
            Pad("Apron", parking, ApronCenter, ApronSize, lawn, top, asphalt);
            Pad(
                "BayLine",
                parking,
                new Vector3(-9f, 0, -27.6f),
                new Vector2(4.8f, .1f),
                top,
                top + .005f,
                paint
            );
            Pad(
                "BayEnd",
                parking,
                new Vector3(-11.4f, 0, -28.75f),
                new Vector2(.1f, 2.3f),
                top,
                top + .005f,
                paint
            );

            Transform sign = Group("ParkingSign", parking, new Vector3(-13.9f, top, -25.2f));
            sign.localRotation = Quaternion.Euler(0, 90, 0);
            NorthBlockAssets.Bind(sign.gameObject, "sign");
            Post("Pole", sign, Vector3.up * .9f, .03f, 1.8f, Color("PoliceSignPole", "#8A939B"));
            Block("Plate", sign, new Vector3(0, 1.75f, .04f), new Vector3(.9f, .5f, .03f), Navy);
            Text("Label", "POLICE\nVEHICLES ONLY", sign, new Vector3(0, 1.75f, .065f), .12f);
            BindParkingWords(parking);
            return top;
        }

        private static void BindParkingWords(Transform parking)
        {
            foreach (string name in new[] { "Lot", "BayLine", "BayEnd" })
                NorthBlockAssets.Bind(parking.Find(name).gameObject, "parking_lot");
            NorthBlockAssets.Bind(parking.Find("Apron").gameObject, "road");
        }

        private static void Pad(
            string name,
            Transform parent,
            Vector3 center,
            Vector2 size,
            float bottom,
            float top,
            Material material
        )
        {
            float height = Mathf.Max(top - bottom, .005f);
            Transform pad = BuilderTools.CreateShape(
                PrimitiveType.Cube,
                name,
                parent,
                new Vector3(center.x, top - height / 2, center.z),
                new Vector3(size.x, height, size.y),
                material
            );
            pad.gameObject.isStatic = true;
        }

        private static float GroundAt(Vector3 spot)
        {
            Physics.SyncTransforms();
            bool hit = Physics.Raycast(
                spot + Vector3.up * 5f,
                Vector3.down,
                out RaycastHit ground,
                10f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            return hit ? ground.point.y : 0f;
        }

        // ---------- 순찰차 ----------

        // KayKit 경찰차에 지붕 경광등 위로 빨강·파랑 발광 조각과 불빛을 얹는다. 둘 다 꺼 두고 PoliceCar가 번갈아 켠다.
        private static GameObject BuildCar()
        {
            var root = new GameObject("PoliceCar");
            ThirdPartyModelKit.Model(
                CarModelPath,
                "Model",
                root.transform,
                Vector3.zero,
                0,
                4.1f,
                ThirdPartyModelKit.Fit.Length,
                CarMaterialFolder
            );
            NorthBlockAssets.Bind(root, "police_car");
            Bounds body = ThirdPartyModelKit.BoundsOf(root);
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = body.center;
            blocker.size = body.size;

            // 지붕 윗면은 빼고 그 위에 솟은 경광등만 잡는다.
            Bounds bar = TopBand(root, body.max.y - .08f);
            Transform lights = Group(
                "LightBar",
                root.transform,
                new Vector3(bar.center.x, bar.center.y, bar.center.z)
            );
            float half = bar.size.x / 2 + .02f;
            LightHalf(
                lights,
                "RedLights",
                -half / 2,
                half,
                bar,
                new UnityEngine.Color(1f, .12f, .1f)
            );
            LightHalf(
                lights,
                "BlueLights",
                half / 2,
                half,
                bar,
                new UnityEngine.Color(.15f, .35f, 1f)
            );
            return root;
        }

        private static void LightHalf(
            Transform bar,
            string name,
            float x,
            float width,
            Bounds band,
            UnityEngine.Color color
        )
        {
            Transform half = Group(name, bar, new Vector3(x, 0, 0));
            Material glow = BuilderTools.GetOrCreateMaterial(ArtFolder, "Police" + name, color);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", color * 4f);
            EditorUtility.SetDirty(glow);
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "Glow",
                half,
                Vector3.zero,
                new Vector3(width, band.size.y + .03f, band.size.z + .03f),
                glow,
                false
            );
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(half, false);
            light.transform.localPosition = Vector3.up * .25f;
            light.type = LightType.Point;
            light.color = color;
            light.range = 6;
            light.intensity = 3;
            light.shadows = LightShadows.None;
            half.gameObject.SetActive(false);
        }

        // 모델에서 높이가 bottom보다 높은 꼭짓점(지붕 경광등)만 모은 경계
        private static Bounds TopBand(GameObject root, float bottom)
        {
            Bounds? band = null;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = filter.transform.TransformPoint(vertex);
                if (point.y < bottom)
                    continue;
                if (band == null)
                    band = new Bounds(point, Vector3.zero);
                else
                {
                    Bounds grown = band.Value;
                    grown.Encapsulate(point);
                    band = grown;
                }
            }
            return band ?? new Bounds(new Vector3(0, bottom, 0), new Vector3(.8f, .1f, .25f));
        }

        private static PoliceCar PatrolCar(
            Scene scene,
            Transform parking,
            GameObject prefab,
            float height
        )
        {
            PoliceCar car = BuilderTools.FindAllInScene<PoliceCar>(scene).SingleOrDefault();
            if (car == null)
            {
                GameObject instance = BuilderTools.InstantiatePrefab(
                    prefab,
                    parking,
                    Vector3.zero,
                    Quaternion.identity
                );
                instance.name = "PoliceCar";
                instance.transform.SetPositionAndRotation(
                    new Vector3(Bay.x, height, Bay.z),
                    Quaternion.Euler(0, 90, 0)
                );
                car = instance.AddComponent<PoliceCar>();
            }

            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            var route = new CarLoopRoute(SouthEnd, NorthEnd, LaneOffset);
            var loop = new SerializedObject(car.GetComponent<LoopingCar>());
            loop.FindProperty("southZ").floatValue = SouthEnd;
            loop.FindProperty("northZ").floatValue = NorthEnd;
            loop.FindProperty("laneOffset").floatValue = LaneOffset;
            loop.FindProperty("startDistance").floatValue = 0;
            loop.FindProperty("speedMetersPerSecond").floatValue = 6;
            // 남행 차로의 주차 트럭을 택시처럼 비켜 간다.
            loop.FindProperty("parkedTruckBypassOffset").floatValue = 1.9f;
            loop.FindProperty("lights").objectReferenceValue =
                BuilderTools.FindSingleInScene<TrafficLights>(scene);
            loop.FindProperty("player").objectReferenceValue = player;
            SerializedProperty stops = loop.FindProperty("signalStops");
            stops.arraySize = 2;
            stops.GetArrayElementAtIndex(0).floatValue = route.NorthboundDistance(NorthboundStopZ);
            stops.GetArrayElementAtIndex(1).floatValue = route.SouthboundDistance(SouthboundStopZ);
            loop.ApplyModifiedPropertiesWithoutUndo();

            var fields = new SerializedObject(car);
            SetPoints(fields.FindProperty("exitCurve"), ExitCurve);
            SetPoints(fields.FindProperty("entryCurve"), EntryCurve);
            TaxiCabinBuilder.FillDestinations(scene, fields.FindProperty("destinations"), false);
            fields.FindProperty("redLights").objectReferenceValue = car
                .transform.Find("LightBar/RedLights")
                .gameObject;
            fields.FindProperty("blueLights").objectReferenceValue = car
                .transform.Find("LightBar/BlueLights")
                .gameObject;
            fields.FindProperty("player").objectReferenceValue = player;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return car;
        }

        private static void SetPoints(SerializedProperty list, Vector3[] points)
        {
            list.arraySize = points.Length;
            for (int index = 0; index < points.Length; index++)
                list.GetArrayElementAtIndex(index).vector3Value = points[index];
        }

        // ---------- 경찰관 ----------

        // 창구 뒤(건물 기준 z=-6.4)에 앞문을 보고 선다. 호텔리어 모델에 갈색 바지를 남색으로 바꾼 제복과 경찰 모자를 씌운다.
        private static NpcIdentity Officer(Scene scene, Transform station)
        {
            NpcIdentity officer = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(npc => npc.Code == OfficerCode);
            if (officer == null)
            {
                SceneGroups groups = BuilderTools.EnsureGroups(scene);
                GameObject staff = BuilderTools.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        NpcBuilder.PasserbyPrefabPath,
                        MenuPath
                    ),
                    groups.Characters,
                    station.TransformPoint(new Vector3(0, 0, -6.4f)) + Vector3.up * .05f,
                    station.rotation
                );
                staff.name = "PoliceOfficer";
                officer = staff.GetComponent<NpcIdentity>();
                BuilderTools.SetText(officer, "code", OfficerCode);
                officer.LookTarget.localPosition = new Vector3(0, 1.12f, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(officer.LookTarget);
                Transform placeholder = staff.transform.Find("Visual");
                placeholder.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder.gameObject);
                Dress(staff.transform);
                var controller = staff.GetComponent<CharacterController>();
                controller.height = 1.6f;
                controller.center = new Vector3(0, .8f, 0);
                controller.radius = .36f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                BuilderTools.SetReference(
                    BuilderTools.GetOrAddComponent<HoverableObject>(staff),
                    "word",
                    AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                        ObjectWordAuthoring.WordsFolder + "/police_officer.asset"
                    )
                );
            }

            Passerby passerby = officer.GetComponent<Passerby>();
            BuilderTools.SetReference(passerby, "settings", OfficerSettings());
            BuilderTools.SetReference(
                passerby,
                "lineSource",
                BuilderTools.LoadRequiredAsset<MockNpcLineSource>(MockLinesPath, MenuPath)
            );
            BuilderTools.SetReference(
                officer.GetComponent<CharacterAnimationDriver>(),
                "animator",
                officer.transform.Find("OfficerVisual").GetComponentInChildren<Animator>()
            );
            NpcBuilder.ConnectToScene(
                BuilderTools.EnsureGroups(scene),
                BuilderTools.FindSingleInScene<PlayerMover>(scene),
                BuilderTools.FindSingleInScene<DialogueController>(scene)
            );
            return officer;
        }

        // 창구 직원처럼 제자리에서 손님을 맞는다. 가까이 오면 먼저 인사하고, 거절해도 잠시 뒤 다시 말을 걸 수 있다.
        private static PasserbySettings OfficerSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PasserbySettings>(OfficerSettingsPath);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<PasserbySettings>();
            var fields = new SerializedObject(settings);
            fields.FindProperty("detectRadiusMeters").floatValue = 4;
            fields.FindProperty("offerDistanceMeters").floatValue = 3;
            fields.FindProperty("walkSpeedMetersPerSecond").floatValue = 0;
            fields.FindProperty("restSeconds").floatValue = 5;
            fields.FindProperty("canDecline").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, OfficerSettingsPath);
            return settings;
        }

        private static void Dress(Transform staff)
        {
            GameObject visual = BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(
                    HotelierPath,
                    "Tripo hotelier character"
                ),
                staff,
                Vector3.zero,
                Quaternion.identity
            );
            visual.name = "OfficerVisual";
            Material uniform = UniformMaterial();
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (
                    renderer.sharedMaterials.Any(material =>
                        material != null && material.name == "Material_0"
                    )
                )
                {
                    renderer.sharedMaterials = renderer
                        .sharedMaterials.Select(material =>
                            material != null && material.name == "Material_0" ? uniform : material
                        )
                        .ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
            Cap(visual);
        }

        // 호텔리어 텍스처에서 어두운 갈색(바지·구두)만 남색으로 바꾼 사본. 피부·머리털처럼 밝은 갈색과 눈동자처럼 거의 검은 색은 두다.
        private static Material UniformMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(UniformMaterialPath);
            if (material != null)
                return material;
            if (!File.Exists(UniformTexturePath))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                texture.LoadImage(
                    File.ReadAllBytes(HotelierFolder + "/Textures/Material_0_color.png")
                );
                Color32[] pixels = texture.GetPixels32();
                for (int index = 0; index < pixels.Length; index++)
                {
                    UnityEngine.Color.RGBToHSV(
                        pixels[index],
                        out float hue,
                        out float saturation,
                        out float value
                    );
                    bool darkBrown =
                        (hue < .11f || hue > .97f)
                        && saturation > .3f
                        && value > .12f
                        && value < .45f;
                    if (darkBrown)
                        pixels[index] = UnityEngine.Color.HSVToRGB(.62f, .5f, value * .95f);
                }
                texture.SetPixels32(pixels);
                File.WriteAllBytes(UniformTexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(UniformTexturePath);
            }
            material = new Material(
                AssetDatabase.LoadAssetAtPath<Material>(
                    HotelierFolder + "/Materials/Material_0.mat"
                )
            );
            material.name = "PoliceOfficerUniform";
            material.SetTexture(
                "_BaseMap",
                AssetDatabase.LoadAssetAtPath<Texture2D>(UniformTexturePath)
            );
            AssetDatabase.CreateAsset(material, UniformMaterialPath);
            return material;
        }

        // 머리 꼭대기에 남색 모자, 앞쪽에 검은 챙과 금색 휘장을 얹고 머리 뼈를 따라 움직이게 한다.
        // 편집 화면에 저장된 자세는 고개가 돌아가 있을 수 있어 모델의 원래 자세(바인드 포즈)로 자리를 정하고,
        // 원래 자세의 머리 뼈 기준으로 붙인다. 플레이에서 서 있기 동작으로 고개가 돌아와도 모자가 머리 앞을 본다.
        private static void Cap(GameObject visual)
        {
            Transform head = visual
                .GetComponentInChildren<Animator>()
                .GetBoneTransform(HumanBodyBones.Head);
            SkinnedMeshRenderer skin = visual
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .First(renderer => Array.IndexOf(renderer.bones, head) >= 0);
            Animator animator = visual.GetComponentInChildren<Animator>();
            Matrix4x4 headAtBind = AtBind(skin, head);
            Bounds skull = BindHeadBounds(skin, headAtBind.GetColumn(3).y);
            // 원래 자세에서 얼굴이 보는 쪽: 왼쪽 위팔에서 오른쪽 위팔로 가는 방향에 수직인 앞
            Vector3 right =
                AtBind(skin, animator.GetBoneTransform(HumanBodyBones.RightUpperArm)).GetColumn(3)
                - AtBind(skin, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm)).GetColumn(3);
            right.y = 0;
            Transform cap = BuilderTools.CreateEmpty(
                "PoliceCap",
                visual.transform,
                Vector3.zero,
                Quaternion.identity
            );
            cap.SetPositionAndRotation(
                new Vector3(skull.center.x, skull.max.y - skull.size.y * .28f, skull.center.z),
                Quaternion.LookRotation(Vector3.Cross(right.normalized, Vector3.up))
            );
            float width = Mathf.Min(skull.size.x, skull.size.z);
            Material navy = Color("PoliceCapNavy", "#22314A");
            Material brim = Color("PoliceCapBrim", "#15181C");
            Post("Crown", cap, Vector3.up * width * .13f, width * .36f, width * .26f, navy);
            Post("Top", cap, Vector3.up * width * .28f, width * .39f, width * .05f, navy);
            Post("Band", cap, Vector3.up * width * .03f, width * .365f, width * .06f, brim);
            Transform visor = Post(
                "Visor",
                cap,
                new Vector3(0, 0, width * .3f),
                width * .24f,
                width * .02f,
                brim
            );
            visor.localScale = new Vector3(
                visor.localScale.x * 1.3f,
                visor.localScale.y,
                visor.localScale.z * .8f
            );
            Ball(
                "Badge",
                cap,
                new Vector3(0, width * .15f, width * .36f),
                new Vector3(width * .12f, width * .12f, width * .03f),
                Color("PoliceCapBadge", "#D8B24A")
            );
            Matrix4x4 onHead = headAtBind.inverse * cap.localToWorldMatrix;
            cap.SetParent(head, false);
            cap.SetLocalPositionAndRotation(onHead.GetColumn(3), onHead.rotation);
            cap.localScale = onHead.lossyScale;
        }

        // 바인드 포즈에서 뼈의 월드 행렬. 메시를 만들 때의 렌더러 기준 자세다.
        private static Matrix4x4 AtBind(SkinnedMeshRenderer skin, Transform bone) =>
            skin.transform.localToWorldMatrix
            * skin.sharedMesh.bindposes[Array.IndexOf(skin.bones, bone)].inverse;

        // 원래 자세에서 머리 뼈보다 높은 꼭짓점의 경계
        private static Bounds BindHeadBounds(SkinnedMeshRenderer skin, float neck)
        {
            Bounds? bounds = null;
            foreach (Vector3 vertex in skin.sharedMesh.vertices)
            {
                Vector3 point = skin.transform.TransformPoint(vertex);
                if (point.y < neck)
                    continue;
                if (bounds == null)
                    bounds = new Bounds(point, Vector3.zero);
                else
                {
                    Bounds grown = bounds.Value;
                    grown.Encapsulate(point);
                    bounds = grown;
                }
            }
            if (bounds == null)
                throw new InvalidOperationException("경찰관 머리 모양을 찾지 못했습니다.");
            return bounds.Value;
        }

        // ---------- 창구 ----------

        private static void Desk(Scene scene, NpcIdentity officer, PoliceCar car)
        {
            var desk = BuilderTools.GetOrAddComponent<PoliceReportDesk>(officer.gameObject);
            BuilderTools.SetReference(desk, "officer", officer);
            BuilderTools.SetReference(desk, "patrolCar", car);
            BuilderTools.SetReference(
                desk,
                "dialogue",
                BuilderTools.FindSingleInScene<DialogueController>(scene)
            );
            BuilderTools.SetReference(
                desk,
                "panel",
                BuilderTools.FindSingleInScene<DialoguePanelView>(scene)
            );
        }

        // ---------- 검토용 화면 ----------

        [MenuItem("Tools/A305/World/Capture Police Station")]
        public static void Capture()
        {
            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
            foreach (Camera existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;
            Transform station = TownProductionBuilder
                .Root(scene, "Environment")
                .Find("TownMainstreet/BackgroundBuildings/PoliceStation");
            PoliceCar car = BuilderTools.FindSingleInScene<PoliceCar>(scene);
            NpcIdentity officer = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == OfficerCode);
            // 편집 화면에 저장된 자세 대신 플레이와 같은 서 있기 동작으로 찍는다.
            Animator officerAnimator = officer.GetComponentInChildren<Animator>();
            AnimationClip idle = officerAnimator.runtimeAnimatorController.animationClips.First(
                clip => clip.name.Contains("Idle")
            );
            AnimationMode.StartAnimationMode();
            AnimationMode.SampleAnimationClip(officerAnimator.gameObject, idle, 0);
            var camera = new GameObject("PoliceReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 60;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 160;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new UnityEngine.Color(.76f, .85f, .86f);

            Shot(camera, new Vector3(6, 9, -38), new Vector3(-7, 1, -23), "street.png");
            Shot(camera, new Vector3(-9, 22, -27), new Vector3(-9, 0, -26.9f), "parking-top.png");
            car.transform.Find("LightBar/RedLights").gameObject.SetActive(true);
            Shot(
                camera,
                car.transform.position + new Vector3(4.5f, 2.4f, -3.5f),
                car.transform.position + Vector3.up * .8f,
                "car-lights.png"
            );
            Shot(
                camera,
                station.TransformPoint(new Vector3(-1.5f, 2.2f, -.8f)),
                station.TransformPoint(new Vector3(.5f, 1.2f, -6.4f)),
                "interior.png"
            );
            Shot(
                camera,
                officer.transform.position + officer.transform.forward * 1.9f + Vector3.up * 1.3f,
                officer.transform.position + Vector3.up * 1.0f,
                "officer.png"
            );
            AnimationMode.StopAnimationMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void Shot(Camera camera, Vector3 position, Vector3 target, string file)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            var texture = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            string folder = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "..",
                "..",
                "artifacts",
                "unity",
                "police-station"
            );
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
        }
    }
}
