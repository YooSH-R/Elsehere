using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
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
    /// 거리에 닫힌 외관으로만 서 있던 배경 건물을 1층 매장으로 바꾼다. 북쪽 거리의 여섯 동과 남서쪽 끝의 경찰서다.
    /// 외관과 위층(주거층)은 그대로 두고, 1층을 열어 간판·출입문과 용도를 알아볼 수 있는 실내를 넣는다.
    /// 건물 프리팹은 없을 때만 만들고, 검토는 TownScene을 복사한 Sandbox 씬에서 한다.
    /// 공용 TownScene에는 Apply 메뉴를 실행할 때만 놓는다.
    /// </summary>
    public static class TownStorefrontBuilder
    {
        public const string Folder = "Assets/Prefabs/World/TownStorefronts";
        public const string SandboxPath = "Assets/Scenes/Sandbox/TownStorefrontsSandbox.unity";
        private const string ArtFolder = "Assets/Art/World/TownStorefronts";
        private const string CheckerPath = ArtFolder + "/FloorChecker.png";
        private const string TilePath = ArtFolder + "/FloorTile.png";

        // 1층 문 열린 자리(가로 2.4m) 안쪽에 문짝을 단다. 문은 건물 앞면 가운데다.
        private static readonly Vector3 DoorHinge = new Vector3(-1.1f, 0, .02f);
        private const float DoorWidth = 2.2f;

        /// <summary>매장 하나. 위치는 마을 씬에서 배경 건물이 서 있는 앞면 가운데다.</summary>
        public sealed class Storefront
        {
            public Storefront(
                string name,
                string source,
                Vector3 position,
                float yaw,
                string koreanName,
                string sign,
                string tagline,
                string signHex,
                string wallHex,
                string floorHex,
                bool checkerFloor = false,
                float taxiOffsetZ = 0
            )
            {
                Name = name;
                Source = source;
                Position = position;
                Yaw = yaw;
                KoreanName = koreanName;
                Sign = sign;
                Tagline = tagline;
                SignHex = signHex;
                WallHex = wallHex;
                FloorHex = floorHex;
                CheckerFloor = checkerFloor;
                TaxiOffsetZ = taxiOffsetZ;
            }

            public string Name { get; }
            public string Source { get; }
            public Vector3 Position { get; }
            public float Yaw { get; }
            public string KoreanName { get; }
            public string Sign { get; }
            public string Tagline { get; }
            public string SignHex { get; }
            public string WallHex { get; }
            public string FloorHex { get; }
            public bool CheckerFloor { get; }

            /// <summary>문 앞 보도에 가로수가 있으면 택시가 그만큼 비켜 선다.</summary>
            public float TaxiOffsetZ { get; }

            private bool IsWestSide => Position.x < 0;

            /// <summary>출입문 앞 보도 위 한 점</summary>
            public Vector3 Entrance =>
                Position + Quaternion.Euler(0, Yaw, 0) * new Vector3(0, 0, .8f);

            /// <summary>택시가 서는 자리. 매장 쪽 차로(서쪽은 남행, 동쪽은 북행) 가운데, 문과 같은 z다.</summary>
            public Vector3 TaxiStop =>
                new Vector3(IsWestSide ? -1.5f : 1.5f, 0, Position.z + TaxiOffsetZ);

            /// <summary>손님이 내리는 보도 가장자리</summary>
            public Vector3 TaxiAlighting =>
                new Vector3(IsWestSide ? -3.6f : 3.6f, .1f, Position.z + TaxiOffsetZ);
        }

        public static readonly Storefront[] Storefronts =
        {
            new Storefront(
                "PostOffice",
                "ButterResidence2F",
                new Vector3(-7, .1f, 30),
                90,
                "우체국",
                "POST OFFICE",
                "STAMPS · SHIPPING · PO BOXES",
                "#293E57",
                "#DCE3E8",
                "#D9D6CF",
                // 문 앞 보도 (-3.65, 30)에 가로수가 있다.
                taxiOffsetZ: 1.5f
            ),
            new Storefront(
                "MapleDiner",
                "BrickResidence2F",
                new Vector3(7, .1f, 39),
                270,
                "메이플 다이너",
                "MAPLE DINER",
                "BREAKFAST ALL DAY",
                "#A8402F",
                "#F3E2C7",
                "#FFFFFF",
                checkerFloor: true
            ),
            new Storefront(
                "TownBank",
                "BrickResidence3F",
                new Vector3(-7, .1f, 48),
                90,
                "타운 은행",
                "TOWN BANK",
                "SAVINGS · LOANS · ATM",
                "#293E57",
                "#DDE5D6",
                "#E9E1D2"
            ),
            new Storefront(
                "SudsLaundromat",
                "ButterResidence3F",
                new Vector3(7, .1f, 48),
                270,
                "빨래방",
                "SUDS LAUNDROMAT",
                "WASH · DRY · FOLD",
                "#2F7C86",
                "#D6ECEE",
                "#EEF2F1"
            ),
            new Storefront(
                "BloomFlowers",
                "ButterResidence2F",
                new Vector3(-7, .1f, 57),
                90,
                "꽃집",
                "BLOOM FLOWERS",
                "FRESH CUT FLOWERS",
                "#326B57",
                "#F6E1E1",
                "#E3D3BF"
            ),
            new Storefront(
                "MainStreetBarber",
                "SageResidence2F",
                new Vector3(7, .1f, 57),
                270,
                "이발소",
                "MAIN ST. BARBER",
                "CUTS · SHAVES",
                "#293E57",
                "#EDE6D8",
                "#FFFFFF",
                checkerFloor: true
            ),
            new Storefront(
                "PoliceStation",
                "SageResidence1F",
                new Vector3(-7, .1f, -20),
                90,
                "경찰서",
                "POLICE",
                "MAPLE TOWN POLICE DEPARTMENT",
                "#293E57",
                "#DCE3E8",
                "#D9D6CF",
                // 문 앞 남행 차로 (-1.3, -17~-20.6)에 주차 트럭이 있어 택시 길이(4.1m)만큼 남쪽에 선다.
                taxiOffsetZ: -3f
            ),
        };

        [MenuItem("Tools/A305/World/Build Town Storefronts Sandbox")]
        public static void BuildSandbox()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuildPrefabs();
            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Install(scene);
            BuilderTools.SaveScene(scene, SandboxPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>검토한 매장을 공용 TownScene에 놓는다. 마을 씬 담당자가 실행한다.</summary>
        [MenuItem("Tools/A305/World/Apply Town Storefronts To Town")]
        public static void ApplyToTown()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuildPrefabs();
            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Install(scene);
            BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void BuildPrefabs()
        {
            Prepare();
            foreach (Storefront storefront in Storefronts)
            {
                string path = Folder + "/" + storefront.Name + ".prefab";
                BuilderTools.CreatePrefabIfMissing(path, () => Build(storefront));
                // 천장 충돌을 넣기 전에 만든 매장도 디자인은 그대로 두고 충돌만 더한다.
                BuilderTools.UpdatePrefabIfExists(path, BlockCameraAtCeiling);
                if (storefront.Name == "PoliceStation")
                    BuilderTools.UpdatePrefabIfExists(path, EnterThroughInteriorDoorways);
            }
            AssetDatabase.SaveAssets();
        }

        // 천장이 카메라를 막지 않으면 내려다볼 때 카메라가 천장 위로 빠져 천장 윗면이 화면을 덮는다(드럭스토어와 같은 문제).
        private static void BlockCameraAtCeiling(GameObject storefront)
        {
            GameObject ceiling = storefront.transform.Find("Interior/Ceiling").gameObject;
            BuilderTools.GetOrAddComponent<BoxCollider>(ceiling);
        }

        // 경찰서는 문을 여닫지 않고 다른 장소처럼 문에 닿으면 화면을 덮었다가 실내(또는 문 밖)에 세운다.
        // 문짝은 닫힌 채 통로를 막고, 문 앞뒤 감지 영역이 서로의 도착 지점을 가리킨다.
        private static void EnterThroughInteriorDoorways(GameObject storefront)
        {
            Transform entrance = storefront.transform.Find("Entrance");
            Transform door = entrance.Find(storefront.name + "Door");
            if (door.TryGetComponent(out ShopSwingDoor swing))
            {
                Object.DestroyImmediate(swing);
                Object.DestroyImmediate(door.GetComponent<BoxCollider>());
            }
            InteriorDoorway outside = Doorway(entrance, "OutsideDoorway", 1);
            InteriorDoorway inside = Doorway(entrance, "InsideDoorway", -1);
            BuilderTools.SetReference(outside, "destination", inside);
            BuilderTools.SetReference(inside, "destination", outside);
        }

        // side가 1이면 문 밖 보도, -1이면 실내다. 감지 영역은 문 앞 0.2~1m, 도착 지점은 문에서 2m 떨어져 문을 등진다.
        private static InteriorDoorway Doorway(Transform entrance, string name, int side)
        {
            Transform doorway = entrance.Find(name);
            if (doorway == null)
                doorway = Group(name, entrance, new Vector3(0, 0, side * .6f));
            var sensor = BuilderTools.GetOrAddComponent<BoxCollider>(doorway.gameObject);
            sensor.isTrigger = true;
            sensor.center = new Vector3(0, 1.3f, 0);
            sensor.size = new Vector3(1.8f, 2.6f, .8f);
            Transform arrival = doorway.Find("ArrivalPoint");
            if (arrival == null)
                arrival = Group("ArrivalPoint", doorway, Vector3.zero);
            arrival.localPosition = new Vector3(0, 0, side * 1.4f);
            arrival.localRotation = Quaternion.Euler(0, side > 0 ? 0 : 180, 0);
            var component = BuilderTools.GetOrAddComponent<InteriorDoorway>(doorway.gameObject);
            BuilderTools.SetReference(component, "arrivalPoint", arrival);
            return component;
        }

        private static void Prepare()
        {
            BuilderTools.EnsureFolder(Folder);
            BuilderTools.EnsureFolder(ArtFolder);
            NorthBlockAssets.Prepare();
            NorthBlockAssets.AddWord(
                "po_box",
                "PO box",
                "사서함",
                "I'd like to rent a PO box.",
                "사서함을 하나 빌리고 싶어요."
            );
            NorthBlockAssets.AddWord(
                "package",
                "package",
                "소포",
                "I want to send this package.",
                "이 소포를 보내고 싶어요."
            );
            NorthBlockAssets.AddWord(
                "stool",
                "stool",
                "스툴 의자",
                "Can I sit on this stool?",
                "이 의자에 앉아도 될까요?"
            );
            NorthBlockAssets.AddWord(
                "atm",
                "ATM",
                "현금 인출기",
                "Is there an ATM near here?",
                "이 근처에 현금 인출기가 있나요?"
            );
            NorthBlockAssets.AddWord(
                "washing_machine",
                "washing machine",
                "세탁기",
                "This washing machine is free.",
                "이 세탁기는 비어 있어요."
            );
            NorthBlockAssets.AddWord(
                "dryer",
                "dryer",
                "건조기",
                "How long does the dryer take?",
                "건조기는 얼마나 걸려요?"
            );
            NorthBlockAssets.AddWord(
                "flower",
                "flower",
                "꽃",
                "These flowers smell nice.",
                "이 꽃들은 향기가 좋아요."
            );
            NorthBlockAssets.AddWord(
                "police_station",
                "police station",
                "경찰서",
                "Where is the police station?",
                "경찰서가 어디에 있나요?"
            );
            NorthBlockAssets.AddWord(
                "barber_chair",
                "barber chair",
                "이발 의자",
                "Please have a seat in the barber chair.",
                "이발 의자에 앉으세요."
            );
            ObjectWordRegistration.PrepareSource();
        }

        // ---------- 매장 한 동 ----------

        private static GameObject Build(Storefront storefront)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                TownBackgroundKit.Folder + "/" + storefront.Source + ".prefab"
            );
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            root.name = storefront.Name;
            Transform groundFloor = root
                .transform.Cast<Transform>()
                .First(child => child.name.Contains("GroundFloor8m"));
            NorthBlockInteriorBuilder.OpenFloor(groundFloor, true);
            NorthBlockAssets.Bind(
                root,
                storefront.Name == "PoliceStation" ? "police_station" : "shop"
            );

            Transform interior = Group("Interior", root.transform, Vector3.zero);
            Shell(interior, storefront);
            Signboard(root.transform, storefront);
            NorthBlockTenantLayout.Door(
                Group("Entrance", root.transform, Vector3.zero),
                storefront.Name + "Door",
                DoorHinge,
                0,
                DoorWidth
            );
            switch (storefront.Name)
            {
                case "PostOffice":
                    PostOffice(interior);
                    break;
                case "MapleDiner":
                    Diner(interior);
                    break;
                case "TownBank":
                    Bank(interior);
                    break;
                case "SudsLaundromat":
                    Laundromat(interior);
                    break;
                case "BloomFlowers":
                    FlowerShop(interior);
                    break;
                case "MainStreetBarber":
                    Barber(root.transform, interior);
                    break;
                case "PoliceStation":
                    PoliceStation(interior);
                    break;
                default:
                    throw new InvalidOperationException("매장 실내가 없습니다: " + storefront.Name);
            }
            return root;
        }

        // 바닥·천장·실내 벽·걸레받이·조명. 1층 높이는 4m이고 위층 바닥 밑에 천장을 둔다.
        // 둥근 모서리 블록에는 UV가 없어, 바닥 무늬는 그 위에 얇은 면을 한 장 더 깐다.
        private static void Shell(Transform interior, Storefront storefront)
        {
            Solid(
                "Floor",
                interior,
                new Vector3(0, -.08f, -4),
                new Vector3(8, .16f, 8),
                Cream,
                "floor"
            );
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "FloorSurface";
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(interior, false);
            surface.transform.localPosition = new Vector3(0, .003f, -4);
            surface.transform.localRotation = Quaternion.Euler(90, 0, 0);
            surface.transform.localScale = new Vector3(7.6f, 7.6f, 1);
            surface.GetComponent<MeshRenderer>().sharedMaterial = FloorPattern(storefront);
            NorthBlockAssets.Bind(surface, "floor");
            Solid(
                "Ceiling",
                interior,
                new Vector3(0, 3.88f, -4),
                new Vector3(7.6f, .1f, 7.6f),
                Cream,
                "ceiling"
            );

            Material plaster = Paint(
                "StorefrontWall" + storefront.WallHex.TrimStart('#'),
                storefront.WallHex
            );
            foreach (int side in new[] { -1, 1 })
            {
                Solid(
                    "InteriorWall",
                    interior,
                    new Vector3(side * 3.785f, 1.95f, -4),
                    new Vector3(.025f, 3.9f, 7.6f),
                    plaster,
                    "wall",
                    false
                );
                Solid(
                    "Skirting",
                    interior,
                    new Vector3(side * 3.75f, .2f, -4),
                    new Vector3(.08f, .4f, 7.6f),
                    Green,
                    "wall",
                    false
                );
            }
            Solid(
                "InteriorWall",
                interior,
                new Vector3(0, 1.95f, -7.785f),
                new Vector3(7.6f, 3.9f, .025f),
                plaster,
                "wall",
                false
            );
            Solid(
                "Skirting",
                interior,
                new Vector3(0, .2f, -7.75f),
                new Vector3(7.6f, .4f, .08f),
                Green,
                "wall",
                false
            );

            Material panel = Paint("CeilingLightPanel", "#FFF6DE");
            if (!panel.IsKeywordEnabled("_EMISSION"))
            {
                panel.EnableKeyword("_EMISSION");
                panel.SetColor("_EmissionColor", new UnityEngine.Color(1, .96f, .85f) * .9f);
                EditorUtility.SetDirty(panel);
            }
            foreach (float z in new[] { -2.4f, -5.6f })
                Solid(
                    "CeilingLight",
                    interior,
                    new Vector3(0, 3.8f, z),
                    new Vector3(1.6f, .05f, .5f),
                    panel,
                    "lamp",
                    false
                );
            var lamp = new GameObject("InteriorLight").AddComponent<Light>();
            lamp.transform.SetParent(interior, false);
            lamp.transform.localPosition = new Vector3(0, 3.2f, -4);
            lamp.type = LightType.Point;
            lamp.range = 9;
            lamp.intensity = .9f;
            lamp.color = new UnityEngine.Color(1, .9f, .74f);
            lamp.shadows = LightShadows.None;
        }

        // 다른 상점과 같은 크기·글꼴의 간판과 그 아래 한 줄 안내
        private static void Signboard(Transform root, Storefront storefront)
        {
            Solid(
                "ShopSign",
                root,
                new Vector3(0, 3.2f, .25f),
                new Vector3(6.1f, .6f, .14f),
                Paint("StorefrontSign" + storefront.SignHex.TrimStart('#'), storefront.SignHex),
                "sign",
                false
            );
            Text("ShopName", storefront.Sign, root, new Vector3(0, 3.2f, .34f), .32f);
            Text("Tagline", storefront.Tagline, root, new Vector3(0, 2.78f, .34f), .12f);
        }

        // ---------- 용도별 실내 (건물 기준 좌표: 앞면 z=0, 뒷벽 z=-7.8, 문은 x=-1.2~1.2) ----------

        // 뒤쪽 창구 카운터, 왼쪽 벽 사서함, 오른쪽 벽 소포 선반
        private static void PostOffice(Transform interior)
        {
            Counter(interior, new Vector3(.4f, 0, -5.6f), new Vector3(5.4f, 1.0f, .7f));
            Treat("BakeryInterior", "CashRegister", interior, new Vector3(1.8f, 1.05f, -5.6f), 180);
            Solid(
                "Scale",
                interior,
                new Vector3(-.8f, 1.1f, -5.6f),
                new Vector3(.45f, .1f, .4f),
                Steel(),
                "counter",
                false
            );
            WallBoard(
                interior,
                "WINDOW 1        WINDOW 2",
                new Vector3(.4f, 2.5f, -7.7f),
                4.6f,
                Navy
            );

            Material brass = Paint("PoBoxBrass", "#C9A45C");
            Solid(
                "PoBoxWall",
                interior,
                new Vector3(-3.66f, 1.45f, -3.2f),
                new Vector3(.16f, 1.9f, 2.9f),
                Wood,
                "po_box",
                false
            );
            for (int row = 0; row < 5; row++)
            for (int column = 0; column < 6; column++)
                Solid(
                    "PoBox",
                    interior,
                    new Vector3(-3.56f, .7f + row * .34f, -2.0f - column * .46f),
                    new Vector3(.05f, .28f, .4f),
                    brass,
                    "po_box",
                    false
                );
            WallBoard(
                interior,
                "FIRST CLASS STAMPS",
                new Vector3(-2.2f, 1.7f, -7.7f),
                2.2f,
                Paint("StampRed", "#B23A3A")
            );
            WallText(interior, "PO BOXES", new Vector3(-3.55f, 2.65f, -3.2f), 90, .16f);

            Shelf(interior, new Vector3(3.35f, 0, -3.4f), -90, 3.2f, 3);
            Material cardboard = Paint("Cardboard", "#C49A63");
            for (int level = 0; level < 3; level++)
            for (int index = 0; index < 4; index++)
            {
                float size = .28f + (index + level) % 3 * .08f;
                Solid(
                    "Package",
                    interior,
                    new Vector3(3.35f, .2f + level * .7f + size / 2, -2.2f - index * .75f),
                    new Vector3(size * 1.2f, size, size),
                    cardboard,
                    "package",
                    false
                );
            }
            NorthBlockAssets.Place("Plant", interior, new Vector3(-3.1f, 0, -1.0f));
        }

        // 왼쪽 긴 카운터와 스툴, 오른쪽 부스 좌석 두 조, 뒷벽 메뉴판
        private static void Diner(Transform interior)
        {
            Material red = Paint("DinerRed", "#C8463B");
            Material chrome = Steel();
            Solid(
                "DinerCounter",
                interior,
                new Vector3(-3.0f, .5f, -4.3f),
                new Vector3(.9f, 1.0f, 5.2f),
                red,
                "counter"
            );
            Solid(
                "CounterTop",
                interior,
                new Vector3(-3.0f, 1.03f, -4.3f),
                new Vector3(1.05f, .06f, 5.3f),
                Cream,
                "counter",
                false
            );
            foreach (float z in new[] { -2.4f, -3.6f, -4.8f, -6.0f })
            {
                Transform stool = Solid(
                    "Stool",
                    interior,
                    new Vector3(-2.05f, .74f, z),
                    new Vector3(.42f, .1f, .42f),
                    red,
                    "stool"
                );
                Post("StoolPost", stool, new Vector3(0, -.37f, 0), .04f, .7f, chrome);
                Post("StoolFoot", stool, new Vector3(0, -.72f, 0), .18f, .03f, chrome);
            }
            Treat("BakeryInterior", "DisplayCase", interior, new Vector3(-3.0f, 1.06f, -5.6f), 90);
            Treat("BakedGoods", "ChocolateCake", interior, new Vector3(-3.0f, 1.2f, -5.6f));
            Treat(
                "BakeryInterior",
                "CoffeeMachine",
                interior,
                new Vector3(-3.1f, 1.06f, -6.7f),
                90
            );
            Treat("BakeryInterior", "Mug", interior, new Vector3(-2.75f, 1.06f, -3.0f));
            Treat("BakeryInterior", "Mug", interior, new Vector3(-2.75f, 1.06f, -4.2f));

            foreach (float z in new[] { -2.8f, -5.6f })
            {
                Solid(
                    "DinerTable",
                    interior,
                    new Vector3(2.9f, .74f, z),
                    new Vector3(1.3f, .06f, .8f),
                    Cream,
                    "table"
                );
                Post("TableLeg", interior, new Vector3(2.9f, .36f, z), .05f, .72f, chrome);
                foreach (int side in new[] { -1, 1 })
                {
                    Solid(
                        "BoothSeat",
                        interior,
                        new Vector3(2.9f, .24f, z + side * .85f),
                        new Vector3(1.4f, .48f, .5f),
                        red,
                        "sofa"
                    );
                    Solid(
                        "BoothBack",
                        interior,
                        new Vector3(2.9f, .8f, z + side * 1.08f),
                        new Vector3(1.4f, .7f, .14f),
                        red,
                        "sofa"
                    );
                }
                Treat("BakeryInterior", "Plate", interior, new Vector3(2.6f, .77f, z));
                Treat("BakeryInterior", "Mug", interior, new Vector3(3.2f, .77f, z));
            }
            WallBoard(
                interior,
                "MENU",
                new Vector3(.6f, 2.85f, -7.7f),
                2.6f,
                Paint("MenuBoard", "#2F3A33")
            );
            WallText(
                interior,
                "PANCAKES · BURGERS\nAPPLE PIE · COFFEE",
                new Vector3(.6f, 2.3f, -7.66f),
                0,
                .15f
            );
        }

        // 뒤쪽 창구와 유리 칸막이, 줄서기 기둥, 오른쪽 현금 인출기, 왼쪽 대기 의자
        private static void Bank(Transform interior)
        {
            Counter(interior, new Vector3(0, 0, -5.6f), new Vector3(6.6f, 1.1f, .7f));
            foreach (float x in new[] { -2.2f, 0f, 2.2f })
            {
                Solid(
                    "TellerGlass",
                    interior,
                    new Vector3(x, 1.7f, -5.6f),
                    new Vector3(1.9f, 1.1f, .03f),
                    ClearGlass(),
                    "window",
                    false
                );
                WallText(
                    interior,
                    x > 1 ? "1"
                        : x < -1 ? "3"
                        : "2",
                    new Vector3(x, 2.4f, -5.55f),
                    0,
                    .2f
                );
            }
            WallBoard(interior, "TELLERS", new Vector3(0, 2.95f, -7.7f), 2.6f, Navy);
            Material rope = Paint("BankRope", "#8C2F39");
            foreach (float x in new[] { -1.6f, 1.6f })
                Post("Stanchion", interior, new Vector3(x, .45f, -3.6f), .05f, .9f, Steel());
            Solid(
                "QueueRope",
                interior,
                new Vector3(0, .82f, -3.6f),
                new Vector3(3.2f, .05f, .05f),
                rope,
                "railing",
                false
            );

            Transform atm = Solid(
                "Atm",
                interior,
                new Vector3(3.35f, .85f, -1.9f),
                new Vector3(.6f, 1.7f, .8f),
                Steel(),
                "atm"
            );
            Solid(
                "AtmScreen",
                atm,
                new Vector3(-.31f, .35f, 0),
                new Vector3(.02f, .35f, .5f),
                Paint("AtmScreen", "#5FA8D3"),
                "atm",
                false
            );
            WallText(atm, "ATM", new Vector3(-.32f, .7f, 0), -90, .16f);
            for (int index = 0; index < 3; index++)
                NorthBlockAssets.Place(
                    "Chair",
                    interior,
                    new Vector3(-3.1f, 0, -1.6f - index * 1.0f),
                    90
                );
            NorthBlockAssets.Place("Plant", interior, new Vector3(-3.1f, 0, -4.6f));
        }

        // 왼쪽 벽 세탁기 다섯 대, 뒷벽 2단 건조기, 가운데 빨래 개는 테이블과 대기 의자
        private static void Laundromat(Transform interior)
        {
            Material white = Paint("ApplianceWhite", "#F2F2EE");
            Material glassDoor = Paint("ApplianceDoor", "#5E7C8C");
            for (int index = 0; index < 5; index++)
            {
                Transform washer = Solid(
                    "WashingMachine",
                    interior,
                    new Vector3(-3.3f, .45f, -1.7f - index * .95f),
                    new Vector3(.8f, .9f, .85f),
                    white,
                    "washing_machine"
                );
                Ball(
                    "Door",
                    washer,
                    new Vector3(.4f, .05f, 0),
                    new Vector3(.05f, .5f, .5f),
                    glassDoor
                );
            }
            for (int level = 0; level < 2; level++)
            for (int index = 0; index < 4; index++)
            {
                Transform dryer = Solid(
                    "Dryer",
                    interior,
                    new Vector3(-1.4f + index * 1.0f, .5f + level * .95f, -7.3f),
                    new Vector3(.9f, .9f, .8f),
                    Steel(),
                    "dryer"
                );
                Ball("Door", dryer, new Vector3(0, 0, .4f), new Vector3(.5f, .5f, .05f), glassDoor);
            }
            WallBoard(
                interior,
                "WASH · DRY · FOLD",
                new Vector3(1.6f, 2.9f, -7.7f),
                3.2f,
                Paint("LaundryTeal", "#2F7C86")
            );
            Transform table = NorthBlockAssets.Place(
                "Table",
                interior,
                new Vector3(1.8f, 0, -3.4f)
            );
            string[] towels = { "#E9A6A6", "#9CC3D5", "#F2E0A7" };
            for (int index = 0; index < 3; index++)
                Block(
                    "FoldedTowel",
                    table,
                    new Vector3(-.35f + index * .35f, .8f + index % 2 * .05f, 0),
                    new Vector3(.3f, .08f + index % 2 * .05f, .25f),
                    Paint("Towel" + index, towels[index])
                );
            NorthBlockAssets.Place("Chair", interior, new Vector3(3.2f, 0, -1.6f), -90);
            NorthBlockAssets.Place("Chair", interior, new Vector3(3.2f, 0, -2.6f), -90);
            NorthBlockAssets.Place("Plant", interior, new Vector3(3.2f, 0, -5.4f));
        }

        // 양쪽 꽃 계단 진열대, 가운데 화분 테이블, 뒤쪽 계산대
        private static void FlowerShop(Transform interior)
        {
            string[] petals = { "#E85D75", "#F4C430", "#F7F3EA", "#B07CC6", "#F08A4B" };
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 3.2f;
                Solid(
                    "FlowerStep",
                    interior,
                    new Vector3(x, .2f, -3.4f),
                    new Vector3(.9f, .4f, 3.6f),
                    Wood,
                    "shelf"
                );
                Solid(
                    "FlowerStep",
                    interior,
                    new Vector3(x + side * .3f, .6f, -3.4f),
                    new Vector3(.4f, .4f, 3.6f),
                    Wood,
                    "shelf"
                );
                for (int index = 0; index < 6; index++)
                {
                    bool upper = index % 2 == 1;
                    Vector3 bucket = new Vector3(
                        x + (upper ? side * .3f : -side * .15f),
                        upper ? .8f : .4f,
                        -1.9f - index * .55f
                    );
                    FlowerBucket(
                        interior,
                        bucket,
                        petals[(index + (side > 0 ? 2 : 0)) % petals.Length]
                    );
                }
            }
            NorthBlockAssets.Place("Table", interior, new Vector3(0, 0, -4.4f));
            Treat("HousePlants", "Succulent", interior, new Vector3(-.3f, .76f, -4.4f));
            FlowerBucket(interior, new Vector3(.3f, .76f, -4.4f), petals[0]);
            Treat("BakeryInterior", "Counter", interior, new Vector3(2.4f, 0, -6.7f), 180);
            Treat("BakeryInterior", "CashRegister", interior, new Vector3(2.4f, .9f, -6.7f), 180);
            Treat("HousePlants", "Monstera", interior, new Vector3(-3.0f, 0, -6.6f));
            Treat("HousePlants", "SnakePlant", interior, new Vector3(-2.2f, 0, -6.9f));
            WallBoard(interior, "FRESH CUT FLOWERS", new Vector3(0, 2.9f, -7.7f), 3.4f, Green);
        }

        // 왼쪽 벽 거울과 이발 의자 두 개, 오른쪽 대기 소파, 문 옆 바깥 이발소 기둥
        private static void Barber(Transform root, Transform interior)
        {
            Material red = Paint("BarberRed", "#B23A3A");
            foreach (float z in new[] { -2.8f, -5.4f })
            {
                Solid(
                    "Mirror",
                    interior,
                    new Vector3(-3.74f, 1.75f, z),
                    new Vector3(.04f, 1.2f, .9f),
                    Paint("MirrorSilver", "#D5E1E6"),
                    "mirror",
                    false
                );
                Solid(
                    "MirrorFrame",
                    interior,
                    new Vector3(-3.76f, 1.75f, z),
                    new Vector3(.03f, 1.32f, 1.02f),
                    Wood,
                    "mirror",
                    false
                );
                Solid(
                    "MirrorShelf",
                    interior,
                    new Vector3(-3.6f, 1.05f, z),
                    new Vector3(.3f, .05f, 1.0f),
                    Cream,
                    "shelf",
                    false
                );
                for (int index = 0; index < 3; index++)
                    Post(
                        "Bottle",
                        interior,
                        new Vector3(-3.6f, 1.16f, z - .3f + index * .3f),
                        .04f,
                        .18f,
                        Paint("BottleBlue", "#4E7FA6")
                    );
                Transform chair = Solid(
                    "BarberChair",
                    interior,
                    new Vector3(-2.5f, .55f, z),
                    new Vector3(.62f, .16f, .62f),
                    red,
                    "barber_chair"
                );
                Post("ChairBase", chair, new Vector3(0, -.36f, 0), .08f, .4f, Steel());
                Post("ChairFoot", chair, new Vector3(0, -.54f, 0), .3f, .04f, Steel());
                Block(
                    "ChairBack",
                    chair,
                    new Vector3(.3f, .38f, 0),
                    new Vector3(.12f, .7f, .62f),
                    red
                );
                foreach (int side in new[] { -1, 1 })
                    Block(
                        "Armrest",
                        chair,
                        new Vector3(0, .2f, side * .3f),
                        new Vector3(.5f, .06f, .06f),
                        Steel()
                    );
            }
            NorthBlockAssets.Place("Sofa", interior, new Vector3(3.15f, 0, -3.6f), -90);
            NorthBlockAssets.Place("CoffeeTable", interior, new Vector3(2.0f, 0, -3.6f));
            NorthBlockAssets.Place("Book", interior, new Vector3(2.0f, .5f, -3.6f));
            WallBoard(interior, "CUTS · SHAVES", new Vector3(.8f, 2.9f, -7.7f), 3.0f, Navy);

            // 미국 이발소의 빨강·흰색·파랑 줄무늬 기둥
            Transform pole = Group("BarberPole", root, new Vector3(1.75f, 1.2f, .32f));
            Material white = Paint("PoleWhite", "#F5F2EA");
            Material blue = Paint("PoleBlue", "#2F5DA8");
            for (int index = 0; index < 9; index++)
                Post(
                    "Stripe",
                    pole,
                    new Vector3(0, index * .13f, 0),
                    .09f,
                    .13f,
                    index % 3 == 0 ? red
                        : index % 3 == 1 ? white
                        : blue
                );
            Ball("PoleCap", pole, new Vector3(0, 1.14f, 0), Vector3.one * .2f, Steel());
            Ball("PoleCap", pole, new Vector3(0, -.12f, 0), Vector3.one * .2f, Steel());
            NorthBlockAssets.Bind(pole.gameObject, "sign");
        }

        // 뒤쪽 민원 창구, 왼쪽 벽 대기 의자, 오른쪽 벽 게시판, 뒤 모서리 서류함과 국기.
        // 경찰관은 창구 뒤(z=-6.4)에 선다(PoliceStationBuilder가 마을 씬에 놓는다). 3등신 경찰관 얼굴이 가리지 않게 창구는 0.7m다.
        private static void PoliceStation(Transform interior)
        {
            Solid(
                "FrontDesk",
                interior,
                new Vector3(0, .35f, -5.4f),
                new Vector3(4.4f, .7f, .8f),
                Navy,
                "reception_desk"
            );
            Solid(
                "FrontDeskTop",
                interior,
                new Vector3(0, .73f, -5.4f),
                new Vector3(4.5f, .06f, .9f),
                Cream,
                "reception_desk",
                false
            );
            Material screen = Paint("PoliceMonitor", "#2B3238");
            Solid(
                "Monitor",
                interior,
                new Vector3(-1.4f, .94f, -5.55f),
                new Vector3(.55f, .36f, .05f),
                screen,
                "reception_desk",
                false
            );
            WallBoard(interior, "MAPLE TOWN POLICE", new Vector3(0, 2.75f, -7.7f), 3.8f, Navy);
            Material gold = Paint("PoliceBadgeGold", "#D8B24A");
            Ball(
                "Badge",
                interior,
                new Vector3(0, 2.05f, -7.74f),
                new Vector3(.55f, .55f, .06f),
                gold
            );

            for (int index = 0; index < 3; index++)
                NorthBlockAssets.Place(
                    "Chair",
                    interior,
                    new Vector3(-3.1f, 0, -1.6f - index * 1.0f),
                    90
                );
            NorthBlockAssets.Place("Plant", interior, new Vector3(-3.1f, 0, -5.4f));

            Material cork = Paint("NoticeCork", "#B98A5B");
            Solid(
                "NoticeBoard",
                interior,
                new Vector3(3.72f, 1.6f, -3.2f),
                new Vector3(.05f, 1.1f, 1.9f),
                cork,
                "note",
                false
            );
            Material paper = Paint("NoticePaper", "#F7F3EA");
            for (int index = 0; index < 4; index++)
                Solid(
                    "Notice",
                    interior,
                    new Vector3(
                        3.69f,
                        1.4f + index % 2 * .45f,
                        -2.6f - index / 2 * .8f - index % 2 * .15f
                    ),
                    new Vector3(.01f, .32f, .26f),
                    paper,
                    "note",
                    false
                );
            WallText(interior, "LOST & FOUND", new Vector3(3.68f, 2.35f, -3.2f), -90, .14f);

            NorthBlockAssets.Place("Cabinet", interior, new Vector3(3.1f, 0, -7.2f));
            Transform flag = Group("Flag", interior, new Vector3(-3.2f, 0, -7.2f));
            NorthBlockAssets.Bind(flag.gameObject, "flag");
            Post("FlagPole", flag, Vector3.up * 1.1f, .025f, 2.2f, Steel());
            Block(
                "FlagCloth",
                flag,
                new Vector3(.38f, 1.85f, 0),
                new Vector3(.7f, .45f, .02f),
                Paint("FlagNavy", "#2F4B7C")
            );
        }

        // ---------- 공통 조각 ----------

        private static void Counter(Transform parent, Vector3 floorCenter, Vector3 size)
        {
            Solid(
                "ServiceCounter",
                parent,
                floorCenter + Vector3.up * size.y / 2,
                size,
                Wood,
                "counter"
            );
            Solid(
                "CounterTop",
                parent,
                floorCenter + Vector3.up * (size.y + .03f),
                new Vector3(size.x + .1f, .06f, size.z + .1f),
                Cream,
                "counter",
                false
            );
        }

        // 벽에 붙인 선반. yaw는 선반 앞이 바라보는 방향이다.
        private static void Shelf(
            Transform parent,
            Vector3 floorCenter,
            float yaw,
            float length,
            int levels
        )
        {
            Transform shelf = Group("WallShelf", parent, floorCenter);
            shelf.localRotation = Quaternion.Euler(0, yaw, 0);
            NorthBlockAssets.Bind(shelf.gameObject, "shelf");
            for (int level = 0; level < levels; level++)
                Block(
                    "Board",
                    shelf,
                    new Vector3(0, .18f + level * .7f, 0),
                    new Vector3(length, .05f, .5f),
                    Wood
                );
            foreach (int side in new[] { -1, 1 })
                Block(
                    "Upright",
                    shelf,
                    new Vector3(side * length / 2, (levels * .7f) / 2, 0),
                    new Vector3(.06f, levels * .7f, .5f),
                    Wood
                );
            var blocker = shelf.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, levels * .35f, 0);
            blocker.size = new Vector3(length, levels * .7f, .5f);
        }

        private static void FlowerBucket(Transform parent, Vector3 bottom, string petalHex)
        {
            Transform bucket = Group("FlowerBucket", parent, bottom);
            NorthBlockAssets.Bind(bucket.gameObject, "flower");
            Post("Bucket", bucket, Vector3.up * .15f, .14f, .3f, Steel());
            Material stem = Paint("FlowerStem", "#5E8C3A");
            Material petal = Paint("Petal" + petalHex.TrimStart('#'), petalHex);
            for (int index = 0; index < 5; index++)
            {
                float angle = index * 72f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * .07f, 0, Mathf.Sin(angle) * .07f);
                Post("Stem", bucket, offset + Vector3.up * .25f, .012f, .35f, stem);
                Ball("Bloom", bucket, offset + Vector3.up * .45f, Vector3.one * .12f, petal);
            }
        }

        private static void WallBoard(
            Transform parent,
            string value,
            Vector3 position,
            float width,
            Material color
        )
        {
            Solid(
                "WallSign",
                parent,
                position,
                new Vector3(width, .5f, .04f),
                color,
                "sign",
                false
            );
            WallText(parent, value, position + new Vector3(0, 0, .04f), 0, .17f);
        }

        // 글자는 로컬 +Z(매장 안쪽에서 앞쪽)로 읽힌다. yaw로 벽 방향에 맞춘다.
        private static void WallText(
            Transform parent,
            string value,
            Vector3 position,
            float yaw,
            float size
        )
        {
            Text("Label", value, parent, position, size).transform.localRotation = Quaternion.Euler(
                0,
                180 + yaw,
                0
            );
        }

        private static Transform Solid(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            string word,
            bool collision = true
        ) =>
            NorthBlockInteriorBuilder.Solid(
                name,
                parent,
                position,
                size,
                material,
                word,
                collision
            );

        private static Transform Treat(
            string category,
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        )
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TinyTreatsPropsBuilder.Prefabs + category + "/" + name + ".prefab"
            );
            if (prefab == null)
                throw new InvalidOperationException(
                    "Tiny Treats 프리팹이 없습니다: " + category + "/" + name
                );
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0, yaw, 0))
                .transform;
        }

        private static Material Paint(string name, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            return BuilderTools.GetOrCreateMaterial(ArtFolder, name, color);
        }

        private static Material Steel() => Paint("StorefrontSteel", "#A7B0B6");

        private static Material FloorPattern(Storefront storefront)
        {
            string name = storefront.CheckerFloor
                ? "FloorChecker"
                : "FloorTile" + storefront.FloorHex.TrimStart('#');
            Material material = Paint(name, storefront.FloorHex);
            if (material.GetTexture("_BaseMap") != null)
                return material;
            material.SetTexture(
                "_BaseMap",
                storefront.CheckerFloor ? CheckerTexture() : TileTexture()
            );
            material.SetTextureScale(
                "_BaseMap",
                storefront.CheckerFloor ? new Vector2(4, 4) : new Vector2(6, 6)
            );
            EditorUtility.SetDirty(material);
            return material;
        }

        // 검정·크림 바둑판. 미국 다이너와 이발소 바닥에 흔하다.
        private static Texture2D CheckerTexture() =>
            Pattern(
                CheckerPath,
                (x, y) =>
                    (x / 16 + y / 16) % 2 == 0
                        ? new UnityEngine.Color(.16f, .17f, .18f)
                        : new UnityEngine.Color(.95f, .92f, .85f)
            );

        // 흰 타일과 회색 줄눈. 재질 색으로 매장마다 다르게 물들인다.
        private static Texture2D TileTexture() =>
            Pattern(
                TilePath,
                (x, y) =>
                    x % 64 < 2 || y % 64 < 2
                        ? new UnityEngine.Color(.72f, .7f, .66f)
                        : UnityEngine.Color.white
            );

        private static Texture2D Pattern(string path, Func<int, int, UnityEngine.Color> pixel)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                return texture;
            const int size = 128;
            var pixels = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels.SetPixel(x, y, pixel(x, y));
            System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 은행 창구의 투명 유리
        private static Material ClearGlass()
        {
            Material material = Paint("TellerGlass", "#CFE7EF");
            if (material.GetFloat("_Surface") > .5f)
                return material;
            UnityEngine.Color color = material.color;
            color.a = .25f;
            material.color = color;
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- 마을 씬에 놓기 ----------

        // 위치와 원본 프리팹이 모두 맞는 배경 건물만 매장으로 바꾸고, 원래 건물은 꺼서 남긴다.
        internal static void Install(Scene scene)
        {
            Transform background = TownProductionBuilder
                .Root(scene, "Environment")
                .Find("TownMainstreet/BackgroundBuildings");
            if (background == null)
                throw new InvalidOperationException("마을 배경 건물 묶음이 없습니다.");
            foreach (Storefront storefront in Storefronts)
            {
                if (background.Find(storefront.Name) != null)
                    continue;
                Transform old = background
                    .Cast<Transform>()
                    .SingleOrDefault(child =>
                        child.gameObject.activeSelf
                        && Vector3.Distance(child.position, storefront.Position) < .05f
                        && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)
                            == TownBackgroundKit.Folder + "/" + storefront.Source + ".prefab"
                    );
                if (old == null)
                    throw new InvalidOperationException(
                        "예상한 배경 건물이 옮겨졌거나 바뀌었습니다: " + storefront.Name
                    );
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Folder + "/" + storefront.Name + ".prefab"
                );
                GameObject replacement = BuilderTools.InstantiatePrefab(
                    prefab,
                    background,
                    old.localPosition,
                    old.localRotation
                );
                replacement.name = storefront.Name;
                replacement.transform.localScale = old.localScale;
                old.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
            }
            // 닫힌 외관 안에 묻혀 있던 옛 정원수는 매장 안에 보이므로 건물 뒤 마당으로 옮긴다.
            Transform gardens = background.parent.Find("StreetGardens");
            foreach (
                Transform tree in gardens
                    .Cast<Transform>()
                    .Where(child => child.name == "GardenTreeV2")
            )
            foreach (Storefront storefront in Storefronts)
            {
                Vector3 local = background
                    .Find(storefront.Name)
                    .InverseTransformPoint(tree.position);
                if (Mathf.Abs(local.x) >= 4 || local.z >= 0 || local.z <= -8)
                    continue;
                tree.position = new Vector3(
                    Mathf.Sign(tree.position.x) * 17.5f,
                    tree.position.y,
                    tree.position.z
                );
                PrefabUtility.RecordPrefabInstancePropertyModifications(tree);
            }
        }
    }
}
