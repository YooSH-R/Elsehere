using System;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.ThirdPartyModelKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 마을이 삭막하지 않게 사람 사는 동네의 모습을 더한다. 집 남쪽 잔디에 아이스크림 트럭 광장을 두고,
    /// 카페 앞 파라솔 테라스, 본길을 도는 택시와 승용차, 자전거 거치대, 가로수 밑 꽃, 공원과 집 앞의 동물을 놓는다.
    /// 택시와 승용차는 교차로 신호를 지키며 달리고, 택시는 인도에서 세우면 서서 운전석의 기사와 대화할 수 있다.
    /// 기존 픽업트럭은 길가에 세워 둔다.
    /// 외부 모델은 Assets/ThirdParty 원본을 쓰고, 단어와 충돌을 붙인 조각 프리팹은 StreetLife 폴더에 없을 때만 만든다.
    /// </summary>
    public static class TownStreetLifeBuilder
    {
        private const string MenuPath = "Tools/A305/World/Apply Town Street Life";
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string GroupName = "StreetLife";
        private const string PrefabFolder = "Assets/Prefabs/World/StreetLife";
        private const string MaterialFolder = "Assets/Art/World/StreetLife/Materials";
        private const string TinyTreats = "Assets/Prefabs/World/TinyTreats/";
        private const string Scenario = "Assets/ThirdParty/ScenarioCollection/";
        private const string KayKit = "Assets/ThirdParty/KayKitCityBits/";
        private const string StreetProps = "Assets/ThirdParty/PolyPizzaStreetProps/";
        private const string Nature = "Assets/ThirdParty/QuaterniusNature/";
        private const string Food = "Assets/ThirdParty/KenneyFoodKit/";
        private const string Furniture = "Assets/ThirdParty/KenneyFurnitureKit/";
        private const string PavingPath = "Assets/Art/World/MainstreetArt/SandPaving.mat";

        // 택시 기사 Joe는 중년 남성 리틀 멍 외형을 쓴다.
        private const string DriverModelPath =
            "Assets/Prefabs/Npc/Review/LittleMung_MiddleAge_Male.prefab";
        private const string ReplaceDriverMenuPath = "Tools/A305/World/Replace Taxi Driver Model";
        private const string OpenWindowsTexturePath =
            "Assets/Art/World/StreetLife/CityCarsOpenWindows.png";
        private const string TaxiDriverCode = "TAXI_DRIVER";

        // 운전석 자리(택시 기준)와 기사 크기. 머리는 앞문 창(z=-0.1~0.3, 지면에서 0.87~1.54m) 가운데에 두어
        // 앞뒤 창 사이 기둥(z≈-0.2)에 가리지 않게 한다.
        private const float DriverSeatX = -.4f;
        private const float DriverSeatZ = .08f;
        private const float DriverScale = .95f;
        private const float DriverHeadHeight = 1.13f;

        // 사람형 근육 값(-1~1). 상체를 조금 숙이고, 팔을 앞으로 뻗어 팔꿈치를 굽히고, 다리는 앉은 자세로 접는다.
        private static readonly (string Muscle, float Value)[] DrivingPose =
        {
            ("Spine Front-Back", .15f),
            ("Left Upper Leg Front-Back", .9f),
            ("Right Upper Leg Front-Back", .9f),
            ("Left Lower Leg Stretch", -.6f),
            ("Right Lower Leg Stretch", -.6f),
            ("Left Arm Down-Up", .1f),
            ("Right Arm Down-Up", .1f),
            ("Left Arm Front-Back", .6f),
            ("Right Arm Front-Back", .6f),
            ("Left Forearm Stretch", .3f),
            ("Right Forearm Stretch", .3f),
        };

        // 차가 도는 길. 남쪽은 집과 튜토리얼 행인 앞이고 픽업트럭(z≈-19)이 서 있어, 카페 앞 횡단보도 북쪽에서 유턴한다.
        private const float LoopSouthZ = -6f;
        private const float LoopNorthZ = 66f;
        private const float LaneOffset = 1.5f;
        private const float RoadHeight = .02f;

        // 교차로 정지선(북쪽 방향 z=5.65, 남쪽 방향 z=14.35) 바로 앞에 차 가운데가 오는 자리
        private const float NorthboundStopZ = 3.2f;
        private const float SouthboundStopZ = 16.8f;

        // 집 남쪽 잔디의 광장 가운데
        private static readonly Vector3 PlazaCenter = new Vector3(12.3f, 0f, -26.8f);

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Apply(scene);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        public static void Apply(Scene scene)
        {
            Transform street = BuilderTools.EnsureGroups(scene).Environment.Find("TownMainstreet");
            if (street == null)
                throw new InvalidOperationException(
                    "TownMainstreet가 없습니다. 먼저 Tools/A305/World/Apply Production Town Design을 실행합니다."
                );
            if (street.Find(GroupName) != null)
                return;
            Transform group = MainstreetArtKit.Group(GroupName, street, Vector3.zero);

            IceCreamPlaza(MainstreetArtKit.Group("IceCreamPlaza", group, Vector3.zero));
            CafeTerrace(MainstreetArtKit.Group("CafeTerrace", group, Vector3.zero));

            Traffic(scene, group);

            // 자전거 거치대는 신문 판매함과 우체통 사이 길가에 둔다.
            Put("BikeRack", group, new Vector3(-3.85f, .1f, -18f), 90);
            Put("Bicycle", group, new Vector3(-4.05f, .1f, -18.5f), 90);
            Put("Bicycle", group, new Vector3(-4.05f, .1f, -17.5f), 90);

            Greenery(MainstreetArtKit.Group("StreetFlowers", group, Vector3.zero));

            Put("SleepingFox", group, Ground(new Vector3(7.4f, 0, 22.6f)), 120);
            Put("Cat", group, new Vector3(8.2f, .1f, -20.3f), 100);
            foreach (
                Vector3 spot in new[]
                {
                    new Vector3(12.6f, 0, 19.6f),
                    new Vector3(12.9f, 0, 20.2f),
                    new Vector3(9.4f, 0, 19.3f),
                }
            )
                Put("Pigeon", group, Ground(spot), spot.x * 40f);

            // 이번에 만든 단어도 씬 저장 때 등록되도록 단어 목록을 다시 읽는다.
            ObjectWordRegistration.PrepareSource();
        }

        // 택시는 호텔 앞 동쪽 차로, 승용차는 드럭스토어 앞 서쪽 차로에서 출발한다. 신호는 교차로 신호등(AmericanStreetBuilder)을 따른다.
        private static void Traffic(Scene scene, Transform parent)
        {
            TrafficLights lights = BuilderTools.FindSingleInScene<TrafficLights>(scene);
            if (lights == null)
                throw new InvalidOperationException(
                    "교차로 신호등이 없습니다. 먼저 Tools/A305/World/Apply American Street Details를 실행합니다."
                );
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            var route = new CarLoopRoute(LoopSouthZ, LoopNorthZ, LaneOffset);
            GameObject taxi = Drive(
                "Taxi",
                parent,
                route,
                route.NorthboundDistance(-1.2f),
                lights,
                player
            );
            Drive("Sedan", parent, route, route.SouthboundDistance(2.3f), lights, player);
            SeatTaxiDriver(scene, taxi, player);
            TaxiCabinBuilder.Configure(scene, taxi.GetComponent<TaxiRide>());
        }

        private static GameObject Drive(
            string piece,
            Transform parent,
            CarLoopRoute route,
            float startDistance,
            TrafficLights lights,
            PlayerMover player
        )
        {
            CarLoopRoute.Pose pose = route.PoseAt(startDistance);
            GameObject car = BuilderTools.InstantiatePrefab(
                Piece(piece),
                parent,
                pose.Position + Vector3.up * RoadHeight,
                Quaternion.LookRotation(pose.Forward)
            );
            var fields = new SerializedObject(car.AddComponent<LoopingCar>());
            fields.FindProperty("southZ").floatValue = LoopSouthZ;
            fields.FindProperty("northZ").floatValue = LoopNorthZ;
            fields.FindProperty("laneOffset").floatValue = LaneOffset;
            fields.FindProperty("startDistance").floatValue = startDistance;
            fields.FindProperty("lights").objectReferenceValue = lights;
            fields.FindProperty("player").objectReferenceValue = player;
            SerializedProperty stops = fields.FindProperty("signalStops");
            stops.arraySize = 2;
            stops.GetArrayElementAtIndex(0).floatValue = route.NorthboundDistance(NorthboundStopZ);
            stops.GetArrayElementAtIndex(1).floatValue = route.SouthboundDistance(SouthboundStopZ);
            fields.ApplyModifiedPropertiesWithoutUndo();
            return car;
        }

        // 미국 택시는 왼쪽(-X)이 운전석이다. 앉아서 운전대를 잡은 자세로 두고, 머리가 열린 창 높이에 오게 맞춘다.
        /// <summary>
        /// 이미 배치된 택시 기사의 외형만 기사 모델로 바꾼다. NPC 키·사물 단어·택시 대화 연결은 그대로 두고
        /// 운전석 자세와 운전대는 <see cref="TaxiCabinBuilder"/>가 다시 맞춘다. 이미 기사 모델이면 바꾸지 않는다.
        /// </summary>
        [MenuItem(ReplaceDriverMenuPath)]
        public static void ReplaceTaxiDriver()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            TaxiRide ride = BuilderTools.FindSingleInScene<TaxiRide>(scene);
            var fields = new SerializedObject(ride);
            var current = (NpcIdentity)fields.FindProperty("driver").objectReferenceValue;
            if (
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject)
                == DriverModelPath
            )
                return;

            GameObject driver = SeatDriverModel(ride.transform);
            NpcIdentity identity = IdentifyDriver(driver, driver.transform.position);
            BuilderTools.SetReference(ride, "driver", identity);
            Object.DestroyImmediate(current.gameObject);
            TaxiCabinBuilder.Configure(scene, ride);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        private static void SeatTaxiDriver(Scene scene, GameObject taxi, PlayerMover player)
        {
            GameObject driver = SeatDriverModel(taxi.transform);
            Animator animator = driver.GetComponentInChildren<Animator>();

            // 머리 높이를 창 가운데에 맞추고, 두 손 사이에 운전대를 둔다.
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            driver.transform.position +=
                Vector3.up * (taxi.transform.position.y + DriverHeadHeight - head.position.y);
            Vector3 hands =
                (
                    animator.GetBoneTransform(HumanBodyBones.LeftHand).position
                    + animator.GetBoneTransform(HumanBodyBones.RightHand).position
                ) / 2f;
            AddSteeringWheel(taxi.transform, taxi.transform.InverseTransformPoint(hands));
            foreach (Transform bone in driver.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);

            NpcIdentity identity = IdentifyDriver(driver, head.position);

            var ride = taxi.AddComponent<TaxiRide>();
            BuilderTools.SetReference(ride, "driver", identity);
            BuilderTools.SetReference(ride, "player", player);
            BuilderTools.SetReference(
                ride,
                "dialogue",
                BuilderTools.FindSingleInScene<DialogueController>(scene)
            );
            BuilderTools.SetReference(
                ride,
                "offerView",
                BuilderTools.FindSingleInScene<PasserbyOfferView>(scene)
            );
        }

        // 운전석에 기사 모델을 앉힌다.
        private static GameObject SeatDriverModel(Transform taxi)
        {
            GameObject model = BuilderTools.LoadRequiredAsset<GameObject>(
                DriverModelPath,
                ReplaceDriverMenuPath
            );
            GameObject driver = BuilderTools.InstantiatePrefab(
                model,
                taxi,
                new Vector3(DriverSeatX, 0f, DriverSeatZ),
                Quaternion.identity
            );
            driver.name = "TaxiDriver";
            driver.transform.localScale = Vector3.one * DriverScale;
            PoseDriving(driver, driver.GetComponentInChildren<Animator>());
            return driver;
        }

        // 대화 상대 NPC 키와 머리 위치의 시선, 사물 단어(taxi driver)를 붙인다.
        private static NpcIdentity IdentifyDriver(GameObject driver, Vector3 headPosition)
        {
            Transform lookTarget = BuilderTools.CreateEmpty(
                "LookTarget",
                driver.transform,
                driver.transform.InverseTransformPoint(headPosition),
                Quaternion.identity
            );
            var identity = new SerializedObject(driver.AddComponent<NpcIdentity>());
            identity.FindProperty("code").stringValue = TaxiDriverCode;
            identity.FindProperty("lookTarget").objectReferenceValue = lookTarget;
            identity.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.SetReference(
                driver.AddComponent<HoverableObject>(),
                "word",
                Word(
                    "taxi_driver",
                    "taxi driver",
                    "택시 기사",
                    "The taxi driver asked me where I was going.",
                    "택시 기사가 어디로 가는지 물었어요."
                )
            );
            return driver.GetComponent<NpcIdentity>();
        }

        // 앉아서 운전하는 애니메이션이 없어 사람형 근육 값으로 자세를 만든다. 다리는 차 몸체에 가려 보이지 않는다.
        // 걷기용 애니메이터는 자세를 덮어쓰므로 끈다. 플레이어용 발·팔·계단 보정 컴포넌트는 꺼 두어도 Awake에서
        // 플레이어 이동 장치를 찾다 실패하므로 기사에서 지운다.
        private static void PoseDriving(GameObject driver, Animator animator)
        {
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            foreach ((string muscle, float value) in DrivingPose)
                pose.muscles[Array.IndexOf(HumanTrait.MuscleName, muscle)] = value;
            handler.SetHumanPose(ref pose);
            handler.Dispose();
            foreach (MonoBehaviour behaviour in driver.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(behaviour);
            animator.enabled = false;
        }

        // 손이 모인 자리에 기사 쪽으로 기운 운전대를 단다.
        private static void AddSteeringWheel(Transform taxi, Vector3 handsLocal)
        {
            Material dark = MainstreetArtKit.Color("SteeringWheel", "#2A2A2A");
            Transform wheel = MainstreetArtKit.Post(
                "SteeringWheel",
                taxi,
                handsLocal + new Vector3(0f, 0f, .04f),
                .19f,
                .03f,
                dark
            );
            wheel.localRotation = Quaternion.Euler(65f, 0f, 0f);
            Transform column = MainstreetArtKit.Post(
                "SteeringColumn",
                taxi,
                handsLocal + new Vector3(0f, -.08f, .22f),
                .025f,
                .32f,
                dark
            );
            column.localRotation = Quaternion.Euler(65f, 0f, 0f);
        }

        // 잔디에 모래색 포장을 깔고, 아이스크림 트럭과 파라솔 테이블 두 조를 둔다.
        private static void IceCreamPlaza(Transform parent)
        {
            float ground = Ground(PlazaCenter).y;
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Paving";
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(PlazaCenter.x, ground + .05f, PlazaCenter.z);
            pad.transform.localScale = new Vector3(7.6f, .1f, 6.4f);
            pad.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(PavingPath);
            float top = ground + .1f;

            Put("IceCreamTruck", parent, new Vector3(14.7f, top, -26.8f), 90);
            foreach (float z in new[] { -24.9f, -28.6f })
            {
                float tableTop = Table(parent, new Vector3(10.4f, top, z), Vector3.right);
                Put("IceCreamCone", parent, new Vector3(10.25f, tableTop, z + .1f), 0);
                Put("IceCreamCone", parent, new Vector3(10.55f, tableTop, z - .1f), 40);
            }
            Existing("PrettyPark/TrashCan", parent, new Vector3(8.95f, top, -23.95f), 0);
            foreach (
                Vector3 spot in new[]
                {
                    new Vector3(8.9f, top, -29.7f),
                    new Vector3(15.8f, top, -23.9f),
                    new Vector3(8.9f, top, -26.8f),
                }
            )
                Put("PottedPlant", parent, spot, spot.z * 37f);
            foreach (float x in new[] { 9.6f, 11.6f, 13.6f, 15.6f })
                Put("FlowerBush", parent, new Vector3(x, ground, -30.6f), x * 53f);
            Put("Pigeon", parent, new Vector3(12.6f, top, -29.3f), 70);
        }

        // 카페 앞 보도에 파라솔 테이블 두 조를 문 양옆으로 둔다.
        private static void CafeTerrace(Transform parent)
        {
            foreach (float z in new[] { -13.7f, -7.7f })
            {
                float tableTop = Table(parent, new Vector3(-5.5f, .1f, z), Vector3.forward);
                Existing(
                    "BakeryInterior/TakeawayCup",
                    parent,
                    new Vector3(-5.35f, tableTop, z + .1f),
                    0
                );
            }
        }

        // 테이블과 마주 보는 의자 두 개, 파라솔. across는 의자가 놓이는 방향이다. 테이블 윗면 높이를 돌려준다.
        private static float Table(Transform parent, Vector3 center, Vector3 across)
        {
            GameObject table = Existing("BakeryInterior/CafeTable", parent, center, 0);
            float chairYaw = Quaternion.LookRotation(across).eulerAngles.y;
            Existing("BakeryInterior/CafeChair", parent, center - across * .8f, chairYaw);
            Existing("BakeryInterior/CafeChair", parent, center + across * .8f, chairYaw + 180);
            Put("Parasol", parent, center, 0);
            return BoundsOf(table).max.y;
        }

        // 가로수 밑에 꽃을 심고, 건물 앞 빈자리에 꽃덤불을 둔다.
        private static void Greenery(Transform parent)
        {
            string[] patches = { "FlowerPatch", "FlowerPatchWhite", "CloverPatch" };
            int next = 0;
            foreach (float z in new[] { -28f, 2.5f, 20f, 30f })
            foreach (float side in new[] { -1f, 1f })
            {
                if (side > 0 && z >= 30f)
                    continue;
                Vector3 tree = new Vector3(side * 3.65f, .12f, z);
                foreach (
                    Vector3 offset in new[]
                    {
                        new Vector3(.32f, 0, -.38f),
                        new Vector3(-.3f, 0, .4f),
                        new Vector3(.3f, 0, .42f),
                    }
                )
                    Put(patches[next++ % patches.Length], parent, tree + offset, next * 47f);
            }
            foreach (
                Vector3 spot in new[]
                {
                    new Vector3(-6.55f, .1f, -23f),
                    new Vector3(-6.55f, .1f, -17f),
                    new Vector3(-6.55f, .1f, 27f),
                    new Vector3(-6.55f, .1f, 33f),
                    new Vector3(6.55f, .1f, 27f),
                    new Vector3(6.55f, .1f, 33f),
                }
            )
                Put("FlowerBush", parent, spot, spot.z * 31f);
        }

        // ---------- 조각 프리팹 ----------

        private static GameObject Piece(string name)
        {
            return BuilderTools.CreatePrefabIfMissing(
                $"{PrefabFolder}/{name}.prefab",
                () =>
                    name switch
                    {
                        "IceCreamTruck" => Build(
                            name,
                            Scenario + "IceCreamTruck.obj",
                            5f,
                            Fit.Length,
                            IceCreamTruckWord(),
                            blocks: true
                        ),
                        "SleepingFox" => Build(
                            name,
                            Scenario + "SleepingFox.obj",
                            .95f,
                            Fit.Length,
                            Word(
                                "fox",
                                "fox",
                                "여우",
                                "The fox is curled up asleep.",
                                "여우가 몸을 웅크리고 자고 있어요."
                            )
                        ),
                        "Taxi" => Car(
                            name,
                            KayKit + "car_taxi.obj",
                            Word(
                                "taxi",
                                "taxi",
                                "택시",
                                "Can you call a taxi for me?",
                                "택시 좀 불러 주실래요?"
                            )
                        ),
                        "Sedan" => Car(
                            name,
                            KayKit + "car_sedan.obj",
                            Word("car", "car", "자동차", "Is this your car?", "이거 당신 차예요?")
                        ),
                        "Parasol" => Build(
                            name,
                            StreetProps + "Parasol.obj",
                            2.5f,
                            Fit.Height,
                            Word(
                                "parasol",
                                "parasol",
                                "파라솔",
                                "Let's sit under the parasol.",
                                "파라솔 아래에 앉아요."
                            )
                        ),
                        "Bicycle" => Build(
                            name,
                            StreetProps + "Bicycle.obj",
                            1.7f,
                            Fit.Length,
                            Word(
                                "bicycle",
                                "bicycle",
                                "자전거",
                                "I came here by bicycle.",
                                "저는 자전거를 타고 왔어요."
                            ),
                            blocks: true
                        ),
                        "BikeRack" => Build(
                            name,
                            StreetProps + "BikeRack.obj",
                            1.8f,
                            Fit.Length,
                            Word(
                                "bike_rack",
                                "bike rack",
                                "자전거 거치대",
                                "You can park your bike at the bike rack.",
                                "자전거 거치대에 자전거를 세울 수 있어요."
                            ),
                            blocks: true
                        ),
                        "Cat" => Build(
                            name,
                            StreetProps + "Cat.obj",
                            .6f,
                            Fit.Length,
                            Word(
                                "cat",
                                "cat",
                                "고양이",
                                "The cat is sleeping in the sun.",
                                "고양이가 햇볕 아래에서 자고 있어요."
                            )
                        ),
                        "Pigeon" => Build(
                            name,
                            StreetProps + "Dove.obj",
                            .3f,
                            Fit.Height,
                            Word(
                                "pigeon",
                                "pigeon",
                                "비둘기",
                                "The pigeons are looking for food.",
                                "비둘기들이 먹이를 찾고 있어요."
                            )
                        ),
                        "FlowerBush" => Build(
                            name,
                            Nature + "BushWithFlowers.obj",
                            .85f,
                            Fit.Height,
                            ObjectWordAuthoring.LoadWord("plant")
                        ),
                        "FlowerPatch" => Build(
                            name,
                            Nature + "FlowerGroup0.obj",
                            .42f,
                            Fit.Height,
                            ObjectWordAuthoring.LoadWord("flowerbed")
                        ),
                        "FlowerPatchWhite" => Build(
                            name,
                            Nature + "FlowerGroup1.obj",
                            .42f,
                            Fit.Height,
                            ObjectWordAuthoring.LoadWord("flowerbed")
                        ),
                        "CloverPatch" => Build(
                            name,
                            Nature + "Clover.obj",
                            .3f,
                            Fit.Height,
                            ObjectWordAuthoring.LoadWord("flowerbed")
                        ),
                        "IceCreamCone" => Build(
                            name,
                            Food + "iceCream.obj",
                            .22f,
                            Fit.Height,
                            Word(
                                "ice_cream",
                                "ice cream",
                                "아이스크림",
                                "Can I have a vanilla ice cream?",
                                "바닐라 아이스크림 하나 주세요."
                            )
                        ),
                        "PottedPlant" => Build(
                            name,
                            Furniture + "pottedPlant.obj",
                            1.05f,
                            Fit.Height,
                            ObjectWordAuthoring.LoadWord("plant")
                        ),
                        _ => throw new ArgumentException("알 수 없는 거리 조각: " + name),
                    }
            );
        }

        // 택시 기사가 보이도록 유리 대신 창을 연 텍스처를 쓴다.
        private static GameObject Car(string name, string modelPath, ObjectWordDefinition word)
        {
            GameObject car = Build(name, modelPath, 4.1f, Fit.Length, word, blocks: true);
            foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>())
                OpenWindows(renderer.sharedMaterial);
            return car;
        }

        // 도시 차 atlas의 왼쪽 아래 칸(256×256)이 유리다. 원본은 두고 그 칸만 투명한 복사본을 만들어 잘라 낸다.
        private static void OpenWindows(Material material)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(OpenWindowsTexturePath);
            if (texture == null)
            {
                var source = (Texture2D)material.GetTexture("_BaseMap");
                var copy = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                copy.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
                for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    copy.SetPixel(x, y, new UnityEngine.Color(0f, 0f, 0f, 0f));
                BuilderTools.EnsureFolder(System.IO.Path.GetDirectoryName(OpenWindowsTexturePath));
                System.IO.File.WriteAllBytes(OpenWindowsTexturePath, copy.EncodeToPNG());
                Object.DestroyImmediate(copy);
                AssetDatabase.ImportAsset(OpenWindowsTexturePath);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(OpenWindowsTexturePath);
            }
            if (material.GetTexture("_BaseMap") == texture)
                return;
            material.SetTexture("_BaseMap", texture);
            MakeCutout(material);
            EditorUtility.SetDirty(material);
        }

        private static ObjectWordDefinition IceCreamTruckWord() =>
            Word(
                "ice_cream_truck",
                "ice cream truck",
                "아이스크림 트럭",
                "Let's get ice cream from the ice cream truck.",
                "아이스크림 트럭에서 아이스크림을 사 먹자."
            );

        // 원점에 바닥 가운데를 맞춘 모델 하나와 단어를 가진 조각. 차·자전거처럼 길을 막는 것만 충돌을 단다.
        private static GameObject Build(
            string name,
            string modelPath,
            float size,
            Fit fit,
            ObjectWordDefinition word,
            bool blocks = false
        )
        {
            var root = new GameObject(name);
            Model(modelPath, "Model", root.transform, Vector3.zero, 0, size, fit, MaterialFolder);
            BuilderTools.SetReference(root.AddComponent<HoverableObject>(), "word", word);
            if (blocks)
            {
                Bounds bounds = BoundsOf(root);
                var blocker = root.AddComponent<BoxCollider>();
                blocker.center = bounds.center;
                blocker.size = bounds.size;
            }
            return root;
        }

        private static ObjectWordDefinition Word(
            string file,
            string text,
            string meaningKo,
            string example,
            string exampleKo
        ) => ObjectWordAuthoring.FindOrCreateWord(file, text, meaningKo, example, exampleKo);

        // ---------- 배치 ----------

        private static GameObject Put(
            string piece,
            Transform parent,
            Vector3 position,
            float yaw
        ) =>
            BuilderTools.InstantiatePrefab(
                Piece(piece),
                parent,
                position,
                Quaternion.Euler(0f, yaw, 0f)
            );

        // 카페·공원에 이미 쓰는 Tiny Treats 프리팹을 그대로 놓는다.
        private static GameObject Existing(
            string path,
            Transform parent,
            Vector3 position,
            float yaw
        )
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TinyTreats}{path}.prefab");
            if (prefab == null)
                throw new InvalidOperationException("Tiny Treats 프리팹이 없습니다: " + path);
            return BuilderTools.InstantiatePrefab(
                prefab,
                parent,
                position,
                Quaternion.Euler(0f, yaw, 0f)
            );
        }

        // 잔디처럼 높이가 다른 바닥은 위에서 내려 쏴 높이를 잰다.
        private static Vector3 Ground(Vector3 spot)
        {
            bool hit = Physics.Raycast(
                spot + Vector3.up * 5f,
                Vector3.down,
                out RaycastHit ground,
                10f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            return new Vector3(spot.x, hit ? ground.point.y : 0f, spot.z);
        }
    }
}
