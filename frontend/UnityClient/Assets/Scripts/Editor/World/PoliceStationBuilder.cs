using System;
using System.Collections.Generic;
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
    /// 그 남쪽 잔디에 주차장을 깔아 순찰차를 세운다. 경찰서 안은 카페·호텔처럼 따로 장면(PoliceStationScene)이라,
    /// 마을 경찰서 문에는 그 장면으로 옮기는 출입구를 붙이고, 경찰서 장면에는 같은 건물과 마을로 나가는 문,
    /// 창구 뒤 경찰관 3명과 신고 창구(<see cref="PoliceReportDesk"/>)를 둔다. 창구는 순찰차와 다른 장면이라 신고를 남기고
    /// (<see cref="PoliceDispatchOrder"/>), 마을이 열리면 순찰차(<see cref="PoliceCar"/>)가 그 신고로 출동한다.
    /// 주차장·순찰차 프리팹·경찰관 외형은 없을 때만 만들고, 이미 있으면 디자인을 두고 기능 연결만 맞춘다.
    /// </summary>
    public static class PoliceStationBuilder
    {
        private const string MenuPath = "Tools/A305/World/Apply Police Station";
        private const string StationMenuPath = "Tools/A305/World/Build Police Station Scene";
        internal const string StationScenePath =
            "Assets/Scenes/Production/PoliceStationScene.unity";
        private const string OldInteriorScenePath =
            "Assets/Scenes/ShopInteriors/PoliceStationInterior.unity";
        private const string Folder = "Assets/Prefabs/World/PoliceStation";
        private const string ArtFolder = "Assets/Art/World/PoliceStation";
        private const string CarPrefabPath = Folder + "/PoliceCar.prefab";
        private const string CarModelPath = "Assets/ThirdParty/KayKitCityBits/car_police.obj";
        private const string CarMaterialFolder = "Assets/Art/World/StreetLife/Materials";
        private const string OfficerSettingsPath = "Assets/Data/Npc/PoliceOfficerSettings.asset";
        private const string MockLinesPath = "Assets/Data/Npc/MockNpcLines.asset";
        private const string OfficerCode = "POLICE_OFFICER";
        private const string DriversGroup = "Drivers";
        private const string HotelierFolder =
            "Assets/Art/Characters/Tripo/LittleMung_HotelStaff_male";

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

        // 경찰서 장면에서 마을 문으로 들어오면 문 안쪽 2m에서 창구를 보고 선다.
        private static readonly Vector3 InsideArrival = new Vector3(0, 0, 2f);

        // 순찰차는 동쪽을 보고 주차 칸에 선다.
        private static readonly Vector3 Bay = new Vector3(-9f, 0, -28.8f);

        // 택시 목적지에는 없고 순찰차만 출동하는 마을 장소(서버 PoliceDesk의 장소 코드). 장소 쪽 차로 가운데, 장소 앞과 같은 z에 선다.
        // 2층 매장(서점·찻집)은 같은 건물 앞이다. 아이스크림 광장(x 9~16, z -24~-30)은 북행 차로가 시작하는 z=-25 바로 북쪽에 선다.
        private static readonly (string Code, string Name, Vector3 Stop)[] PatrolOnlyPlaces =
        {
            ("BAKERY", "빵집", new Vector3(-LaneOffset, 0, 21)),
            ("PARK", "분수대 공원", new Vector3(LaneOffset, 0, 21)),
            ("ARCADE", "오락실", new Vector3(LaneOffset, 0, 30)),
            ("HOME_STORE", "생활용품점", new Vector3(-LaneOffset, 0, 39)),
            ("ICE_CREAM_PLAZA", "아이스크림 트럭", new Vector3(LaneOffset, 0, -23)),
        };

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

        // 경찰관이 설 자리(건물 기준). 창구는 z -5.8~-5.0, x -2.2~2.2이고 앞문을 보고 선다. 근무 중인 경찰관 중 창구에 가장 가까운
        // 사람이 응대하므로(NpcDuty) Dan이 창구 바로 뒤에 서고, 다른 경찰관은 Dan이 지나다닐 수 있게 조금 뒤나 창구 끝에 선다.
        private static readonly (string Code, string ObjectName, Vector3 Spot)[] Officers =
        {
            (OfficerCode, "PoliceOfficer", new Vector3(0, 0, -6.4f)),
            ("POLICE_SERGEANT", "PoliceSergeant", new Vector3(1.6f, 0, -7.2f)),
            ("POLICE_ROOKIE", "PoliceRookie", new Vector3(-1.6f, 0, -7.2f)),
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
            RemoveTownOfficers(scene);
            ConnectTownDoor(station);
            SeatOfficersInCar(scene, car);
            TaxiCabinBuilder.Configure(scene, BuilderTools.FindSingleInScene<TaxiRide>(scene));
            BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);
            BuildStationScene();
            AssetDatabase.SaveAssets();
            Debug.Log("경찰서 장면과 마을의 경찰서 문·순찰차를 적용했습니다.");
        }

        /// <summary>
        /// 이미 놓인 순찰차의 출동 장소만 마을 전체로 다시 채운다. 경찰서·순찰차의 다른 배치는 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/A305/World/Apply Police Dispatch Places")]
        public static void ApplyDispatchPlaces()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            var fields = new SerializedObject(BuilderTools.FindSingleInScene<PoliceCar>(scene));
            FillDispatchPlaces(scene, fields.FindProperty("destinations"));
            fields.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);
            Debug.Log("순찰차 출동 장소를 마을 전체로 채웠습니다.");
        }

        /// <summary>현재 장면 디자인을 유지하며 마을의 운전석·목적지와 경찰서의 신고 창구 연결을 함께 맞춘다.</summary>
        [MenuItem("Tools/A305/World/Connect Police Dispatch")]
        public static void ConnectDispatch()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene town = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            PoliceCar car = BuilderTools.FindSingleInScene<PoliceCar>(town);
            var fields = new SerializedObject(car);
            FillDispatchPlaces(town, fields.FindProperty("destinations"));
            fields.ApplyModifiedPropertiesWithoutUndo();
            SeatOfficersInCar(town, car);
            BuilderTools.SaveScene(town, TownProductionBuilder.ScenePath);
            var places = PatrolPlaces();
            Scene station = EditorSceneManager.OpenScene(StationScenePath);
            foreach (NpcIdentity officer in BuilderTools.FindAllInScene<NpcIdentity>(station))
                if (Officers.Any(entry => entry.Code == officer.Code))
                    Desk(station, officer, places);
            BuilderTools.SaveScene(station, StationScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("경찰 신고의 장면 간 연결과 운전석·목적지를 적용했습니다.");
        }

        // 택시와 같은 장소(경찰서 제외)에 순찰차만 가는 장소를 더한다.
        private static void FillDispatchPlaces(Scene scene, SerializedProperty destinations)
        {
            TaxiCabinBuilder.FillDestinations(scene, destinations, false);
            int first = destinations.arraySize;
            destinations.arraySize = first + PatrolOnlyPlaces.Length;
            for (int index = 0; index < PatrolOnlyPlaces.Length; index++)
            {
                var place = PatrolOnlyPlaces[index];
                TaxiCabinBuilder.SetDestination(
                    destinations.GetArrayElementAtIndex(first + index),
                    place.Code,
                    place.Name,
                    place.Stop,
                    new Vector3(place.Stop.x > 0 ? 3.6f : -3.6f, .1f, place.Stop.z)
                );
            }
        }

        // ---------- 운전석의 경찰관 ----------

        /// <summary>
        /// 경찰관마다 같은 외형을 순찰차 운전석에 앉혀 두고 순찰차에 잇는다. 신고를 받은 경찰관이 직접 운전해 나가는 모습에 쓴다.
        /// 이미 앉힌 경찰관은 Inspector에서 조절한 자세를 덮어쓰지 않게 그대로 둔다.
        /// </summary>
        [MenuItem("Tools/A305/World/Apply Patrol Car Drivers")]
        public static void ApplyPatrolCarDrivers()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            SeatOfficersInCar(scene, BuilderTools.FindSingleInScene<PoliceCar>(scene));
            BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("순찰차 운전석에 경찰관을 앉혔습니다.");
        }

        private static void SeatOfficersInCar(Scene scene, PoliceCar car)
        {
            OpenCarWindows(car.transform);
            AddSteering(scene, car.transform);
            Transform group = car.transform.Find(DriversGroup);
            if (group == null)
                group = Group(DriversGroup, car.transform, Vector3.zero);
            foreach (Transform seat in group.Cast<Transform>().ToArray())
                if (!Officers.Any(officer => officer.Code == seat.name))
                    Object.DestroyImmediate(seat.gameObject);
            var seats = new List<(string Code, GameObject Seat)>();
            foreach (var officer in Officers)
            {
                Transform seat = group.Find(officer.Code);
                if (seat == null)
                    seat = SeatOfficer(car.transform, group, officer.Code);
                seat.gameObject.SetActive(false);
                seats.Add((officer.Code, seat.gameObject));
            }
            var fields = new SerializedObject(car);
            SerializedProperty drivers = fields.FindProperty("drivers");
            drivers.arraySize = seats.Count;
            for (int index = 0; index < seats.Count; index++)
            {
                SerializedProperty driver = drivers.GetArrayElementAtIndex(index);
                driver.FindPropertyRelative("officerCode").stringValue = seats[index].Code;
                driver.FindPropertyRelative("seat").objectReferenceValue = seats[index].Seat;
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 창구의 경찰관과 같은 외형(외형 표의 경찰 프리팹, 모자·제복 포함 + 배색)을 입혀 운전석에 앉힌다.
        // 걷기용 뼈 보정(계단·발 접지·팔 간격)은 앉은 자세를 바꾸고, 꺼 둬도 켜질 때 몸(CharacterController)을 찾다 실패하므로 뺀다.
        private static Transform SeatOfficer(Transform car, Transform group, string code)
        {
            Transform seat = Group(code, group, Vector3.zero);
            NpcCastMember member = NpcCast.Find(code);
            BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(member.Look.PrefabPath, MenuPath),
                seat,
                Vector3.zero,
                Quaternion.identity
            );
            NpcCastDresser.Dress(seat.gameObject, member);
            foreach (MonoBehaviour behaviour in seat.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(behaviour);
            TaxiCabinBuilder.SeatAtTheWheel(car, seat);
            foreach (Transform bone in seat.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            return seat;
        }

        // 운전자가 보이도록 승용차·택시처럼 도시 차 텍스처의 유리 칸을 투명하게 한다. 경광등 재질은 두다.
        private static void OpenCarWindows(Transform car)
        {
            foreach (Renderer renderer in car.Find("Model").GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (
                    material != null
                    && material.HasProperty("_BaseMap")
                    && material.GetTexture("_BaseMap") != null
                )
                    TownStreetLifeBuilder.OpenWindows(material);
        }

        // 택시의 핸들을 그대로 복사한다. 자리는 운전자를 앉힐 때 손에 맞춰 정한다.
        private static void AddSteering(Scene scene, Transform car)
        {
            Transform taxi = BuilderTools.FindSingleInScene<TaxiRide>(scene).transform;
            foreach (string part in new[] { "SteeringWheel", "SteeringColumn" })
            {
                if (car.Find(part) != null)
                    continue;
                GameObject copy = Object.Instantiate(taxi.Find(part).gameObject, car, false);
                copy.name = part;
            }
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
            FillDispatchPlaces(scene, fields.FindProperty("destinations"));
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

        // ---------- 경찰서 장면 ----------

        /// <summary>
        /// 경찰서 실내를 카페·호텔처럼 따로 장면으로 만든다. 마을과 같은 경찰서 건물 프리팹을 두어 실내 모습은 그대로이고,
        /// 마을로 나가는 문·플레이어·실내 카메라·경찰관과 신고 창구를 붙인다. 이미 있으면 디자인은 두고 연결만 맞춘다.
        /// 같은 장면 안에서 실내를 따로 불러오던 예전 실내 씬(ShopInteriors/PoliceStationInterior)은 지운다.
        /// </summary>
        [MenuItem(StationMenuPath)]
        public static void BuildStationScene()
        {
            if (!BuilderTools.CanBuild())
                return;
            (string Code, string Name)[] places = PatrolPlaces();
            Scene scene = OpenOrCreateStationScene();
            Transform station = Station(scene);
            ConnectStationDoor(station);
            SpaceSceneSetup.Apply(scene);
            FirstDayShopBuilder.UseInteriorCamera(scene);
            SpaceDoorway townDoor = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(doorway => doorway.DoorwayId == "TownDoor");
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            player.SetPositionAndRotation(
                townDoor.ArrivalPoint.position,
                townDoor.ArrivalPoint.rotation
            );
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            PlaceOfficers(scene, station, places);
            // 신고 접수 문구는 택시와 같은 대화 화면 위쪽 상태 판에 보인다.
            TaxiCabinBuilder.ConfigureStatus(
                BuilderTools.FindSingleInScene<DialoguePanelView>(scene)
            );
            BuilderTools.SaveScene(scene, StationScenePath);
            BuilderTools.AddToSceneList(StationScenePath);
            // 경찰관에 생활을 붙이고 경찰서 바닥을 구운 뒤, 경찰서 1층 안에서만 지내게 한다.
            NpcLifeBuilder.BuildScene(StationScenePath);
            scene = EditorSceneManager.OpenScene(StationScenePath, OpenSceneMode.Single);
            KeepOfficersInStation(scene);
            BuilderTools.SaveScene(scene, StationScenePath);
            RemoveOldInteriorScene();
        }

        private static Scene OpenOrCreateStationScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StationScenePath) != null)
                return EditorSceneManager.OpenScene(StationScenePath, OpenSceneMode.Single);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single
            );
            SceneGroups groups = BuilderTools.EnsureGroups(scene);
            GameObject building = BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(
                    TownStorefrontBuilder.Folder + "/PoliceStation.prefab",
                    StationMenuPath
                ),
                groups.Environment,
                Vector3.zero,
                Quaternion.identity
            );
            building.name = "PoliceStation";
            var sun = new GameObject("StationSun").AddComponent<Light>();
            sun.transform.SetParent(groups.Lighting, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.ambientLight = new UnityEngine.Color(.65f, .65f, .6f);
            BuilderTools.SaveScene(scene, StationScenePath);
            return scene;
        }

        private static Transform Station(Scene scene) =>
            BuilderTools
                .FindAllInScene<Transform>(scene)
                .Single(part => part.name == "PoliceStation" && part.Find("Entrance") != null);

        // 마을 경찰서 문 밖 보도에 닿으면 경찰서 장면으로 옮긴다. 다른 마을 문처럼 문 앞 1m에서 문을 등지고 나온다.
        private static void ConnectTownDoor(Transform station)
        {
            SpaceDoorway door = StationDoorway(
                station,
                "PoliceStationDoorway",
                "PoliceStationDoor",
                "PoliceStationScene",
                "TownDoor",
                outward: true
            );
            TownProductionBuilder.FitDoorToSidewalk(door);
        }

        // 경찰서 장면의 문 안쪽에 닿으면 마을로 나간다. 마을에서 들어오면 문 안쪽 2m에서 창구를 보고 선다.
        private static void ConnectStationDoor(Transform station)
        {
            SpaceDoorway door = StationDoorway(
                station,
                "TownDoorway",
                "TownDoor",
                "TownScene",
                "PoliceStationDoor",
                outward: false
            );
            door.ArrivalPoint.localPosition = InsideArrival;
            door.ArrivalPoint.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(door.ArrivalPoint);
        }

        /// <summary>
        /// 경찰서 정면 출입문 자리에 장면을 옮기는 출입구를 붙인다. 건물에 그려진 문(PoliceStationDoor)이 그대로 보이도록
        /// 출입구 프리팹의 문 그림은 끄고, 지나가지 못하게 막는 문짝 충돌만 남긴다. outward면 길 쪽, 아니면 실내 쪽을 본다.
        /// </summary>
        private static SpaceDoorway StationDoorway(
            Transform station,
            string objectName,
            string doorwayId,
            string destinationScene,
            string destinationDoorwayId,
            bool outward
        )
        {
            Transform entrance = station.Find("Entrance");
            Transform existing = entrance.Find(objectName);
            GameObject root =
                existing != null
                    ? existing.gameObject
                    : BuilderTools.InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(
                            SpaceSceneBuilder.DoorwayPrefabPath,
                            MenuPath
                        ),
                        entrance,
                        new Vector3(0, 0, outward ? .07f : -.07f),
                        Quaternion.Euler(0, outward ? 0 : 180, 0)
                    );
            root.name = objectName;
            var doorway = root.GetComponent<SpaceDoorway>();
            BuilderTools.SetText(doorway, "doorwayId", doorwayId);
            BuilderTools.SetText(doorway, "destinationScene", destinationScene);
            BuilderTools.SetText(doorway, "destinationDoorwayId", destinationDoorwayId);
            foreach (
                Renderer picture in root
                    .transform.Find("Visual")
                    .GetComponentsInChildren<Renderer>(true)
            )
            {
                picture.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(picture);
            }
            return doorway;
        }

        // 마을 순찰차의 출동 장소(코드·화면 이름). 마을이 열려 있지 않으면 잠깐 열어 읽고 닫는다.
        private static (string Code, string Name)[] PatrolPlaces()
        {
            Scene town = SceneManager.GetSceneByPath(TownProductionBuilder.ScenePath);
            bool opened = !town.isLoaded;
            if (opened)
                town = EditorSceneManager.OpenScene(
                    TownProductionBuilder.ScenePath,
                    OpenSceneMode.Additive
                );
            try
            {
                SerializedProperty destinations = new SerializedObject(
                    BuilderTools.FindSingleInScene<PoliceCar>(town)
                ).FindProperty("destinations");
                return Enumerable
                    .Range(0, destinations.arraySize)
                    .Select(index => destinations.GetArrayElementAtIndex(index))
                    .Select(place =>
                        (
                            place.FindPropertyRelative("code").stringValue,
                            place.FindPropertyRelative("displayName").stringValue
                        )
                    )
                    .ToArray();
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(town, true);
            }
        }

        private static void RemoveOldInteriorScene()
        {
            EditorBuildSettings.scenes = EditorBuildSettings
                .scenes.Where(entry => entry.path != OldInteriorScenePath)
                .ToArray();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OldInteriorScenePath) != null)
                AssetDatabase.DeleteAsset(OldInteriorScenePath);
        }

        // ---------- 경찰관 ----------

        /// <summary>
        /// 경찰관 3명을 경찰서 장면의 창구 뒤에 두고, 누구에게 신고해도 접수되도록 경찰관마다 신고 창구를 잇는다.
        /// 이미 있는 경찰관은 자리를 바꾸지 않는다. 경찰서 장면에 쓰며, 장면 저장은 부르는 쪽이 한다.
        /// </summary>
        public static void PlaceOfficers(Scene scene)
        {
            PlaceOfficers(scene, Station(scene), PatrolPlaces());
        }

        /// <summary>
        /// 생활이 붙은 경찰관이 거리 매장 직원처럼 경찰서 1층 안의 사물·자리만 골라 지내게 한다.
        /// 생활(<see cref="NpcLifeBuilder"/>)을 붙인 뒤에 부르며, 장면 저장은 부르는 쪽이 한다.
        /// </summary>
        public static void KeepOfficersInStation(Scene scene)
        {
            foreach (var officer in Officers)
            {
                BuildingStaffBuilder.LimitToGroundFloor(scene, officer.Code, "PoliceStation");
            }
        }

        private static void PlaceOfficers(
            Scene scene,
            Transform station,
            (string Code, string Name)[] places
        )
        {
            foreach (var officer in Officers)
            {
                Desk(
                    scene,
                    Officer(scene, station, officer.Code, officer.ObjectName, officer.Spot),
                    places
                );
            }
        }

        // 경찰관은 경찰서 장면에서 일하므로, 예전에 마을 경찰서 안에 두었던 경찰관은 지운다.
        private static void RemoveTownOfficers(Scene town)
        {
            foreach (NpcIdentity npc in BuilderTools.FindAllInScene<NpcIdentity>(town))
            {
                if (Officers.Any(officer => officer.Code == npc.Code))
                    Object.DestroyImmediate(npc.gameObject);
            }
        }

        // 창구 뒤에 앞문을 보고 선다. 호텔리어 모델에 갈색 바지를 남색으로 바꾼 제복과 경찰 모자를 씌우고, 외형 표의 배색을 입힌다.
        private static NpcIdentity Officer(
            Scene scene,
            Transform station,
            string code,
            string objectName,
            Vector3 spot
        )
        {
            NpcIdentity officer = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(npc => npc.Code == code);
            if (officer == null)
            {
                SceneGroups groups = BuilderTools.EnsureGroups(scene);
                GameObject staff = BuilderTools.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        NpcBuilder.PasserbyPrefabPath,
                        MenuPath
                    ),
                    groups.Characters,
                    NpcPlacement.FreeSpot(station, spot, BuildingStaffBuilder.GroundFloor),
                    station.rotation
                );
                staff.name = objectName;
                officer = staff.GetComponent<NpcIdentity>();
                BuilderTools.SetText(officer, "code", code);
                officer.LookTarget.localPosition = new Vector3(0, 1.12f, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(officer.LookTarget);
                Transform placeholder = staff.transform.Find("Visual");
                placeholder.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder.gameObject);
                var controller = staff.GetComponent<CharacterController>();
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

            NpcVisualBuilder.Connect(officer, NpcCast.Find(code).Look.PrefabPath, true);
            BuilderTools.SetReference(
                BuilderTools.GetOrAddComponent<HoverableObject>(officer.gameObject),
                "word",
                AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                    ObjectWordAuthoring.WordsFolder + "/police_officer.asset"
                )
            );
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
                officer.GetComponentInChildren<Animator>()
            );
            NpcCastDresser.Dress(officer.gameObject, NpcCast.Find(code));
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

        // ---------- 창구 ----------

        // 순찰차는 마을에 있으므로, 접수 문구에 쓸 장소 이름은 순찰차의 출동 장소 목록을 그대로 옮겨 둔다.
        private static void Desk(
            Scene scene,
            NpcIdentity officer,
            (string Code, string Name)[] places
        )
        {
            var desk = BuilderTools.GetOrAddComponent<PoliceReportDesk>(officer.gameObject);
            BuilderTools.SetReference(desk, "officer", officer);
            var fields = new SerializedObject(desk);
            SerializedProperty destinations = fields.FindProperty("destinations");
            destinations.arraySize = places.Length;
            for (int index = 0; index < places.Length; index++)
            {
                SerializedProperty place = destinations.GetArrayElementAtIndex(index);
                place.FindPropertyRelative("code").stringValue = places[index].Code;
                place.FindPropertyRelative("displayName").stringValue = places[index].Name;
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
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
            Camera camera = ReviewCamera();

            Shot(camera, new Vector3(6, 9, -38), new Vector3(-7, 1, -23), "street.png");
            Shot(camera, new Vector3(-9, 22, -27), new Vector3(-9, 0, -26.9f), "parking-top.png");
            car.transform.Find("LightBar/RedLights").gameObject.SetActive(true);
            Shot(
                camera,
                car.transform.position + new Vector3(4.5f, 2.4f, -3.5f),
                car.transform.position + Vector3.up * .8f,
                "car-lights.png"
            );
            // 출동할 때처럼 Dan을 운전석에 앉혀 옆 유리와 앞 유리로 본다.
            car.transform.Find(DriversGroup + "/" + OfficerCode).gameObject.SetActive(true);
            Shot(
                camera,
                car.transform.TransformPoint(new Vector3(-3.2f, 1.5f, .3f)),
                car.transform.TransformPoint(new Vector3(-.3f, 1f, 0)),
                "driver-side.png"
            );
            Shot(
                camera,
                car.transform.TransformPoint(new Vector3(-1.2f, 1.7f, 3.6f)),
                car.transform.TransformPoint(new Vector3(-.3f, 1f, 0)),
                "driver-front.png"
            );
            Object.DestroyImmediate(camera.gameObject);

            // 실내와 경찰관은 경찰서 장면에서 찍는다.
            scene = EditorSceneManager.OpenScene(StationScenePath);
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
            foreach (Camera existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;
            station = Station(scene);
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
            camera = ReviewCamera();
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

        private static Camera ReviewCamera()
        {
            var camera = new GameObject("PoliceReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 60;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 160;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new UnityEngine.Color(.76f, .85f, .86f);
            return camera;
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
