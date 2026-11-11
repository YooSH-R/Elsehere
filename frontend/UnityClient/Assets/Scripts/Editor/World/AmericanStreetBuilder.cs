using System;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
using A305.ObjectHover;
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
    /// 마을 거리를 미국 소도시처럼 보이게 한다. 차선은 도로 전체에 이어 그리고, 교차로에는 횡단보도·정지선·신호등을 둔다.
    /// 길가에는 속도 제한 표지판, 버스 정류장, 신문 판매함, 파란 우체통, 보행자 횡단 표지판을 두고,
    /// 호텔에 성조기 하나, 드럭스토어 앞에 ICE 냉동고와 OPEN 네온, 카페 앞에 입간판을 둔다.
    /// 소품 프리팹은 다른 거리 소품처럼 MainstreetArt에 없을 때만 만들고, 이미 놓았으면 배치를 다시 바꾸지 않는다.
    /// </summary>
    public static class AmericanStreetBuilder
    {
        private const string MenuPath = "Tools/A305/World/Apply American Street Details";
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string GroupName = "AmericanStreetDetails";
        private const string FlagTexturePath = ArtFolder + "/UsFlag.png";

        // 기존 도로의 중앙선·횡단보도와 같은 재질을 쓴다.
        private const string CentreLinePath = "Assets/Art/World/MainstreetBlockout/Yellow.mat";
        private const string StripePath = "Assets/Art/World/MainstreetBlockout/Stripe.mat";

        // 기존 도로: 본길은 x=-3~3, z=-24~70이고 교차로 가로길은 z=7~13, x=±3~±20이다.
        // 카페 앞 횡단보도는 z=-12.5~-9.5다.
        private const float CrossRoadZ = 10f;
        private const float CrossRoadEndX = 20f;

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
            Transform group = Group(GroupName, street, Vector3.zero);
            PaintRoadMarkings(Group("RoadMarkings", group, Vector3.zero));

            // 좌표는 마을 씬 기준이다. 차도는 x=-3~3, 보도는 그 바깥 4m, 건물 정면은 x=±7이다.
            // 오른쪽 통행이므로 북쪽으로 가는 차는 동쪽 차로, 남쪽으로 가는 차는 서쪽 차로를 쓴다.
            Transform northEast = Prop(
                "TrafficSignal",
                BuildTrafficSignal,
                group,
                new Vector3(5.7f, .1f, 13.45f),
                180
            );
            Transform southWest = Prop(
                "TrafficSignal",
                BuildTrafficSignal,
                group,
                new Vector3(-5.7f, .1f, 6.55f),
                0
            );
            ConnectTrafficLights(group, northEast, southWest);
            Prop("SpeedLimitSign", BuildSpeedLimit, group, new Vector3(3.45f, .1f, -20.5f), 180);
            Prop("SpeedLimitSign", BuildSpeedLimit, group, new Vector3(-3.45f, .1f, 23.5f), 0);
            Prop("BusStopSign", BuildBusStop, group, new Vector3(-3.45f, .1f, 18f), 0);
            Place("ParkBench", group, new Vector3(-4.8f, .1f, 18f), Bench, 90);
            Prop("UsFlag", BuildFlag, group, new Vector3(7.02f, 4.0f, 2.3f), -90);

            string[] boxes = { "NewspaperBoxBlue", "NewspaperBoxRed", "NewspaperBoxYellow" };
            for (int i = 0; i < boxes.Length; i++)
            {
                string name = boxes[i];
                Prop(
                    name,
                    () => BuildNewspaperBox(name),
                    group,
                    new Vector3(-3.6f, .1f, -15f - i * .55f),
                    -90
                );
            }
            Prop("BlueMailbox", BuildMailbox, group, new Vector3(-3.75f, .1f, -20.5f), 90);
            Prop("PedXingSign", BuildPedXing, group, new Vector3(3.4f, .1f, -13.6f), 180);
            Prop("PedXingSign", BuildPedXing, group, new Vector3(-3.4f, .1f, -8.5f), 0);
            Prop("IceChest", BuildIceChest, group, new Vector3(-6.4f, .1f, -3.6f), 90);
            // 진열창 안 조명 아래, 선반 위에 건다.
            Prop("OpenSign", BuildOpenSign, group, new Vector3(-6.88f, 1.5f, 2f), 90).localScale =
                Vector3.one * .8f;
            Prop(
                "CoffeeSandwichBoard",
                BuildSandwichBoard,
                group,
                new Vector3(-4.35f, .1f, -13.4f),
                90
            );
            // 이번에 만든 단어도 씬 저장 때 등록되도록 단어 목록을 다시 읽는다.
            ObjectWordRegistration.PrepareSource();
        }

        // 두 신호등이 한 주기로 바뀌도록 묶는다. 본길을 도는 차도 이 신호를 본다.
        private static void ConnectTrafficLights(Transform parent, params Transform[] signals)
        {
            var lights = BuilderTools
                .CreateEmpty("TrafficLights", parent, Vector3.zero, Quaternion.identity)
                .gameObject.AddComponent<TrafficLights>();
            var fields = new SerializedObject(lights);
            SerializedProperty heads = fields.FindProperty("heads");
            heads.arraySize = signals.Length;
            for (int i = 0; i < signals.Length; i++)
                heads.GetArrayElementAtIndex(i).objectReferenceValue = signals[i]
                    .GetComponent<TrafficSignalHead>();
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 도로 표시 ----------

        // 기존 중앙선이 끊긴 곳을 잇고, 교차로 네 방향에 횡단보도와 정지선을 그린다. 가로길에도 중앙선을 긋는다.
        private static void PaintRoadMarkings(Transform parent)
        {
            Material yellow = AssetDatabase.LoadAssetAtPath<Material>(CentreLinePath);
            Material white = AssetDatabase.LoadAssetAtPath<Material>(StripePath);

            // 카페 앞 횡단보도: 정지선까지 중앙선을 잇는다.
            CentreLineAlongZ(parent, yellow, -14f, -13.15f);
            CentreLineAlongZ(parent, yellow, -8.55f, -8f);
            Paint(
                "StopLine",
                parent,
                white,
                new Vector3(1.575f, 0, -13.3f),
                new Vector2(2.65f, .3f)
            );
            Paint(
                "StopLine",
                parent,
                white,
                new Vector3(-1.575f, 0, -8.7f),
                new Vector2(2.65f, .3f)
            );

            // 교차로의 본길 쪽 횡단보도와 정지선
            CrosswalkAcrossMain(parent, white, 6.85f);
            CrosswalkAcrossMain(parent, white, 13.15f);
            Paint(
                "StopLine",
                parent,
                white,
                new Vector3(1.575f, 0, 5.65f),
                new Vector2(2.65f, .3f)
            );
            Paint(
                "StopLine",
                parent,
                white,
                new Vector3(-1.575f, 0, 14.35f),
                new Vector2(2.65f, .3f)
            );

            // 가로길: 횡단보도, 정지선, 끝까지 이어지는 중앙선
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float offset in new[] { -2.2f, -1.1f, 0f, 1.1f, 2.2f })
                    Paint(
                        "CrosswalkStripe",
                        parent,
                        white,
                        new Vector3(side * 4.1f, 0, CrossRoadZ + offset),
                        new Vector2(1.6f, .65f)
                    );
                Paint(
                    "StopLine",
                    parent,
                    white,
                    new Vector3(side * 5.45f, 0, CrossRoadZ + side * 1.575f),
                    new Vector2(.3f, 2.65f)
                );
                foreach (float line in new[] { -.12f, .12f })
                    Paint(
                        "CentreLine",
                        parent,
                        yellow,
                        new Vector3(side * (5.3f + CrossRoadEndX - .2f) / 2f, 0, CrossRoadZ + line),
                        new Vector2(CrossRoadEndX - .2f - 5.3f, .08f)
                    );
            }
        }

        private static void CentreLineAlongZ(
            Transform parent,
            Material yellow,
            float fromZ,
            float toZ
        )
        {
            foreach (float x in new[] { -.12f, .12f })
                Paint(
                    "CentreLine",
                    parent,
                    yellow,
                    new Vector3(x, 0, (fromZ + toZ) / 2f),
                    new Vector2(.08f, toZ - fromZ)
                );
        }

        private static void CrosswalkAcrossMain(Transform parent, Material white, float z)
        {
            foreach (float x in new[] { -2.2f, -1.1f, 0f, 1.1f, 2.2f })
                Paint(
                    "CrosswalkStripe",
                    parent,
                    white,
                    new Vector3(x, 0, z),
                    new Vector2(.65f, 1.6f)
                );
        }

        // 노면 표시는 기존 표시와 같은 높이에 그림자 없이 얇게 깐다. size는 (x 너비, z 길이)다.
        private static void Paint(
            string name,
            Transform parent,
            Material material,
            Vector3 center,
            Vector2 size
        )
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = name;
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.transform.SetParent(parent, false);
            mark.transform.localPosition = new Vector3(center.x, .008f, center.z);
            mark.transform.localScale = new Vector3(size.x, .01f, size.y);
            var renderer = mark.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------- 소품 ----------
        // 소품의 앞은 로컬 +Z다. 글자는 앞에서 읽히도록 로컬 yaw 180으로 둔다.

        // 벽이나 가로등에 비스듬히 꽂은 깃대. 깃발의 짧은 변이 깃대를 따라 붙고 나머지는 아래로 늘어진다.
        private static (GameObject Root, Action<Transform> Decorate) BuildFlag()
        {
            var root = new GameObject("UsFlag");
            Transform t = root.transform;
            const float length = 1.8f;
            Vector3 pole = new Vector3(
                0,
                Mathf.Sin(35 * Mathf.Deg2Rad),
                Mathf.Cos(35 * Mathf.Deg2Rad)
            );
            Block(
                "Bracket",
                t,
                Vector3.zero,
                new Vector3(.12f, .16f, .12f),
                Color("FlagBracket", "#3A3F44")
            );
            Transform staff = Post(
                "Staff",
                t,
                pole * (length / 2),
                .035f,
                length,
                Color("FlagStaff", "#E9E4D8")
            );
            staff.localRotation = Quaternion.FromToRotation(Vector3.up, pole);
            Ball(
                "Finial",
                t,
                pole * (length + .04f),
                Vector3.one * .09f,
                Color("FlagFinial", "#D4A944")
            );
            MeshObject(
                "Cloth",
                t,
                Vector3.zero,
                Vector3.one,
                LoadMesh("UsFlagCloth", () => FlagCloth(pole, length)),
                FlagMaterial()
            );
            Hover(
                root,
                Word(
                    "flag",
                    "flag",
                    "깃발",
                    "The flag is waving in the wind.",
                    "깃발이 바람에 펄럭이고 있어요."
                )
            );
            return (root, null);
        }

        // 깃발 천을 앞뒤 두 면으로 만든다. 왼쪽 위(파란 칸)가 깃대 끝 쪽에 온다.
        private static Mesh FlagCloth(Vector3 pole, float length)
        {
            const float height = .78f,
                width = 1.4f;
            Vector3 fly = Vector3.ProjectOnPlane(Vector3.down, pole).normalized;
            Vector3 hoistTop = pole * (length - .1f);
            Vector3 hoistBottom = hoistTop - pole * height;
            Vector3[] corners =
            {
                hoistBottom,
                hoistTop,
                hoistTop + fly * width,
                hoistBottom + fly * width,
            };
            var uvs = new[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0),
            };
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    corners[0],
                    corners[1],
                    corners[2],
                    corners[3],
                    corners[0],
                    corners[1],
                    corners[2],
                    corners[3],
                },
                uv = new[] { uvs[0], uvs[1], uvs[2], uvs[3], uvs[0], uvs[1], uvs[2], uvs[3] },
                triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material FlagMaterial()
        {
            Material material = Color("UsFlagCloth", "#FFFFFF");
            if (material.GetTexture("_BaseMap") == null)
            {
                material.SetTexture("_BaseMap", FlagTexture());
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        // 13줄 줄무늬와 파란 칸의 별 50개. 작은 화면 크기에 맞춰 별은 점으로 그린다.
        private static Texture2D FlagTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(FlagTexturePath);
            if (existing != null)
                return existing;
            const int width = 380,
                height = 200;
            UnityEngine.Color red = Hex("#B22234"),
                blue = Hex("#3C3B6E");
            float stripe = height / 13f;
            float cantonWidth = width * .4f,
                cantonHeight = stripe * 7;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float fromTop = height - 1 - y;
                UnityEngine.Color color =
                    (int)(fromTop / stripe) % 2 == 0 ? red : UnityEngine.Color.white;
                if (x < cantonWidth && fromTop < cantonHeight)
                    color = IsStar(x, fromTop, cantonWidth, cantonHeight)
                        ? UnityEngine.Color.white
                        : blue;
                texture.SetPixel(x, y, color);
            }
            BuilderTools.EnsureFolder(ArtFolder);
            System.IO.File.WriteAllBytes(FlagTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FlagTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FlagTexturePath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FlagTexturePath);
        }

        // 별은 9줄로 6개·5개가 번갈아 놓인다.
        private static bool IsStar(float x, float y, float width, float height)
        {
            for (int row = 0; row < 9; row++)
            {
                int count = row % 2 == 0 ? 6 : 5;
                for (int column = 0; column < count; column++)
                {
                    float starX = width / 12f * (2 * column + (row % 2 == 0 ? 1 : 2));
                    float starY = height / 10f * (row + 1);
                    if ((x - starX) * (x - starX) + (y - starY) * (y - starY) < 6.5f)
                        return true;
                }
            }
            return false;
        }

        // 모퉁이 기둥에서 차도 위로 팔을 뻗은 미국식 신호등. 팔 끝 신호등은 로컬 +Z(본길), 기둥의 작은 신호등은 로컬 -X(가로길)를 본다.
        // 팔에는 건너는 길 이름판을 단다. 렌즈는 신호에 따라 켜고 끄도록 합치지 않고 따로 둔다.
        private static (GameObject Root, Action<Transform> Decorate) BuildTrafficSignal()
        {
            var root = new GameObject("TrafficSignal");
            Transform t = root.transform;
            Material metal = Color("SignalPole", "#4A5056");
            Material housing = Color("SignalHousing", "#E3B23C");
            Post("Pole", t, new Vector3(0, 2.45f, 0), .09f, 4.9f, metal);
            Transform arm = Post("Arm", t, new Vector3(2.5f, 4.75f, 0), .06f, 5f, metal);
            arm.localRotation = Quaternion.Euler(0, 0, 90);
            Block(
                "NameSign",
                t,
                new Vector3(2f, 4.42f, 0),
                new Vector3(1.5f, .32f, .05f),
                Green,
                .02f
            );
            Block(
                "Head",
                t,
                new Vector3(4.4f, 4.12f, 0),
                new Vector3(.38f, 1f, .3f),
                housing,
                .04f
            );
            Post("Hanger", t, new Vector3(4.4f, 4.68f, 0), .025f, .14f, metal);
            Block(
                "SideHead",
                t,
                new Vector3(-.26f, 3.1f, 0),
                new Vector3(.3f, 1f, .38f),
                housing,
                .04f
            );
            Hover(
                root,
                Word(
                    "traffic_light",
                    "traffic light",
                    "신호등",
                    "Turn right at the traffic light.",
                    "신호등에서 오른쪽으로 도세요."
                )
            );
            return (
                root,
                top =>
                {
                    Label(
                        top,
                        "MAPLE AVE",
                        new Vector3(2f, 4.42f, .03f),
                        .14f,
                        UnityEngine.Color.white
                    );
                    AddLenses(top);
                    // 차와 사람은 기둥에만 부딪히고 머리 위 팔 아래로는 지나간다.
                    var pole = top.gameObject.AddComponent<BoxCollider>();
                    pole.center = new Vector3(0, 2.45f, 0);
                    pole.size = new Vector3(.3f, 4.9f, .3f);
                }
            );
        }

        // 처음에는 본길 초록, 가로길 빨강을 켜 둔다. Play에서는 TrafficLights가 주기에 맞춰 바꾼다.
        private static void AddLenses(Transform signal)
        {
            Material off = Color("SignalLensOff", "#3E3A30");
            Material red = Glow("SignalRedOn", "#FF453A");
            Material yellow = Glow("SignalYellowOn", "#FFC83D");
            Material green = Glow("SignalGreenOn", "#33D17A");
            var main = new Vector3(.2f, .2f, .05f);
            var cross = new Vector3(.05f, .2f, .2f);
            var head = signal.gameObject.AddComponent<TrafficSignalHead>();
            BuilderTools.SetReference(
                head,
                "mainRed",
                Lens("MainRed", signal, new Vector3(4.4f, 4.42f, .16f), main, off)
            );
            BuilderTools.SetReference(
                head,
                "mainYellow",
                Lens("MainYellow", signal, new Vector3(4.4f, 4.12f, .16f), main, off)
            );
            BuilderTools.SetReference(
                head,
                "mainGreen",
                Lens("MainGreen", signal, new Vector3(4.4f, 3.82f, .16f), main, green)
            );
            BuilderTools.SetReference(
                head,
                "crossRed",
                Lens("CrossRed", signal, new Vector3(-.42f, 3.4f, 0), cross, red)
            );
            BuilderTools.SetReference(
                head,
                "crossYellow",
                Lens("CrossYellow", signal, new Vector3(-.42f, 3.1f, 0), cross, off)
            );
            BuilderTools.SetReference(
                head,
                "crossGreen",
                Lens("CrossGreen", signal, new Vector3(-.42f, 2.8f, 0), cross, off)
            );
            BuilderTools.SetReference(head, "redOn", red);
            BuilderTools.SetReference(head, "yellowOn", yellow);
            BuilderTools.SetReference(head, "greenOn", green);
            BuilderTools.SetReference(head, "off", off);
        }

        private static Renderer Lens(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material
        )
        {
            return Ball(name, parent, position, size, material).GetComponent<Renderer>();
        }

        // 켜진 신호 렌즈는 낮에도 보이게 스스로 빛나게 한다.
        private static Material Glow(string name, string hex)
        {
            Material material = Color(name, hex);
            if (!material.IsKeywordEnabled("_EMISSION"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Hex(hex) * .8f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static (GameObject Root, Action<Transform> Decorate) BuildSpeedLimit()
        {
            var root = new GameObject("SpeedLimitSign");
            Transform t = root.transform;
            Post("Pole", t, new Vector3(0, 1.1f, 0), .04f, 2.2f, Color("SignPole", "#9AA3A8"));
            Block(
                "Border",
                t,
                new Vector3(0, 1.85f, .03f),
                new Vector3(.54f, .7f, .025f),
                Color("SignBlack", "#1A1A1A"),
                .02f
            );
            Block(
                "Face",
                t,
                new Vector3(0, 1.85f, .045f),
                new Vector3(.48f, .64f, .02f),
                Color("SignWhite", "#F7F7F2"),
                .015f
            );
            Hover(root, Word("sign"));
            return (
                root,
                top =>
                {
                    Label(top, "SPEED\nLIMIT", new Vector3(0, 2.0f, .06f), .095f, Hex("#1A1A1A"));
                    Label(top, "25", new Vector3(0, 1.72f, .06f), .3f, Hex("#1A1A1A"));
                }
            );
        }

        private static (GameObject Root, Action<Transform> Decorate) BuildBusStop()
        {
            var root = new GameObject("BusStopSign");
            Transform t = root.transform;
            Post("Pole", t, new Vector3(0, 1.25f, 0), .04f, 2.5f, Color("SignPole", "#9AA3A8"));
            Block(
                "Board",
                t,
                new Vector3(0, 2.2f, .03f),
                new Vector3(.5f, .6f, .03f),
                Color("BusStopBlue", "#1F5FAD"),
                .02f
            );
            Hover(
                root,
                Word(
                    "bus_stop",
                    "bus stop",
                    "버스 정류장",
                    "The bus stop is across the street.",
                    "버스 정류장은 길 건너편에 있어요."
                )
            );
            return (
                root,
                top =>
                    Label(
                        top,
                        "BUS\nSTOP",
                        new Vector3(0, 2.2f, .05f),
                        .13f,
                        UnityEngine.Color.white
                    )
            );
        }

        private static (GameObject Root, Action<Transform> Decorate) BuildNewspaperBox(string name)
        {
            var root = new GameObject(name);
            Transform t = root.transform;
            string hex =
                name.EndsWith("Blue") ? "#1F5FAD"
                : name.EndsWith("Red") ? "#C8102E"
                : "#F2C230";
            Block(
                "Stand",
                t,
                new Vector3(0, .16f, 0),
                new Vector3(.34f, .32f, .3f),
                Color("NewsStand", "#3A3F44")
            );
            Block(
                "Body",
                t,
                new Vector3(0, .63f, 0),
                new Vector3(.46f, .62f, .4f),
                Color(name, hex),
                .03f
            );
            Block(
                "Lid",
                t,
                new Vector3(0, .96f, 0),
                new Vector3(.48f, .06f, .42f),
                Color("NewsLid", "#2A2A2A")
            );
            Block(
                "Window",
                t,
                new Vector3(0, .74f, .2f),
                new Vector3(.36f, .26f, .02f),
                Color("NewsWindow", "#DDE6EA"),
                .01f
            );
            Hover(
                root,
                Word(
                    "newspaper_box",
                    "newspaper box",
                    "신문 판매함",
                    "You can buy a newspaper from the newspaper box.",
                    "신문 판매함에서 신문을 살 수 있어요."
                )
            );
            return (
                root,
                top =>
                    Label(top, "NEWS", new Vector3(0, .52f, .215f), .09f, UnityEngine.Color.white)
            );
        }

        // 미국 거리의 파란 공용 우체통. 기관 표시는 넣지 않는다.
        private static (GameObject Root, Action<Transform> Decorate) BuildMailbox()
        {
            var root = new GameObject("BlueMailbox");
            Transform t = root.transform;
            Material blue = Color("MailboxBlue", "#1F4E9C");
            foreach (float x in new[] { -.17f, .17f })
            foreach (float z in new[] { -.15f, .15f })
                Post(
                    "Leg",
                    t,
                    new Vector3(x, .14f, z),
                    .025f,
                    .28f,
                    Color("MailboxLeg", "#2A2A2A")
                );
            Block("Body", t, new Vector3(0, .62f, 0), new Vector3(.46f, .7f, .4f), blue, .04f);
            Transform roof = Post("Roof", t, new Vector3(0, .97f, 0), .23f, .46f, blue);
            roof.localRotation = Quaternion.Euler(0, 0, 90);
            Block(
                "Slot",
                t,
                new Vector3(0, .85f, .205f),
                new Vector3(.26f, .05f, .02f),
                Color("MailboxSlot", "#16233A"),
                .01f
            );
            Hover(
                root,
                Word(
                    "mailbox",
                    "mailbox",
                    "우체통",
                    "Where is the nearest mailbox?",
                    "가장 가까운 우체통이 어디예요?"
                )
            );
            return (
                root,
                top => Label(top, "MAIL", new Vector3(0, .5f, .205f), .1f, UnityEngine.Color.white)
            );
        }

        // 횡단보도 앞 노란 마름모 표지판
        private static (GameObject Root, Action<Transform> Decorate) BuildPedXing()
        {
            var root = new GameObject("PedXingSign");
            Transform t = root.transform;
            Post("Pole", t, new Vector3(0, 1.15f, 0), .05f, 2.3f, Color("SignPole", "#9AA3A8"));
            Transform board = Block(
                "Board",
                t,
                new Vector3(0, 2.15f, .03f),
                new Vector3(.52f, .52f, .03f),
                Color("PedXingYellow", "#F2C230"),
                .02f
            );
            board.localRotation = Quaternion.Euler(0, 0, 45);
            Hover(root, Word("sign"));
            return (
                root,
                top => Label(top, "PED\nXING", new Vector3(0, 2.15f, .05f), .12f, Hex("#1A1A1A"))
            );
        }

        // 미국 편의점·드럭스토어 앞에 흔히 있는 얼음 냉동고
        private static (GameObject Root, Action<Transform> Decorate) BuildIceChest()
        {
            var root = new GameObject("IceChest");
            Transform t = root.transform;
            Block(
                "Body",
                t,
                new Vector3(0, .5f, 0),
                new Vector3(1.3f, 1f, .7f),
                Color("IceChestWhite", "#F4F6F8"),
                .05f
            );
            Block(
                "Lid",
                t,
                new Vector3(0, 1.03f, 0),
                new Vector3(1.34f, .06f, .74f),
                Color("IceChestTrim", "#9CC8E8"),
                .02f
            );
            Hover(root, Word("freezer"));
            return (
                root,
                top => Label(top, "ICE", new Vector3(0, .55f, .36f), .32f, Hex("#1E5AA8"))
            );
        }

        // 진열창 안쪽에 건 OPEN 네온
        private static (GameObject Root, Action<Transform> Decorate) BuildOpenSign()
        {
            var root = new GameObject("OpenSign");
            Block(
                "Backing",
                root.transform,
                Vector3.zero,
                new Vector3(1.05f, .44f, .03f),
                Color("OpenSignBacking", "#1D2433"),
                .04f
            );
            Hover(root, Word("sign"));
            return (root, top => Label(top, "OPEN", new Vector3(0, 0, .02f), .3f, Hex("#FF4D6D")));
        }

        private static (GameObject Root, Action<Transform> Decorate) BuildSandwichBoard()
        {
            var root = new GameObject("CoffeeSandwichBoard");
            Transform t = root.transform;
            Material board = Color("SandwichBoardGreen", "#2E3B33");
            foreach (float side in new[] { -1f, 1f })
            {
                Transform face = Block(
                    "Face",
                    t,
                    new Vector3(0, .5f, side * .13f),
                    new Vector3(.58f, .85f, .03f),
                    board,
                    .02f
                );
                face.localRotation = Quaternion.Euler(side * 12f, 0, 0);
            }
            Hover(root, Word("sidewalk_sign"));
            return (
                root,
                top =>
                    Label(
                        top,
                        "FRESH\nCOFFEE",
                        new Vector3(0, .6f, .17f),
                        .13f,
                        UnityEngine.Color.white
                    )
            );
        }

        // ---------- 도우미 ----------

        // 부품을 재질별로 합친 뒤 글자를 붙여 프리팹으로 저장한다. 글자는 합치면 지워지므로 합친 다음에 단다.
        private static Transform Prop(
            string name,
            Func<(GameObject Root, Action<Transform> Decorate)> build,
            Transform parent,
            Vector3 position,
            float yaw
        )
        {
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(
                $"{PrefabFolder}/{name}.prefab",
                () =>
                {
                    (GameObject root, Action<Transform> decorate) = build();
                    root.name = name;
                    CombineParts(root, name);
                    decorate?.Invoke(root.transform);
                    // 머리 위 깃발과 창에 붙인 간판은 길을 막거나 카메라를 당기지 않게 호버만 받는다.
                    if (root.GetComponent<Collider>() == null)
                        AddCollider(root, isTrigger: name == "UsFlag" || name == "OpenSign");
                    return root;
                }
            );
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0f, yaw, 0f))
                .transform;
        }

        private static void Label(
            Transform parent,
            string value,
            Vector3 position,
            float size,
            UnityEngine.Color color
        )
        {
            TownBlockoutGeometry
                .Label("Label", value, parent, position, new Vector3(0f, 180f, 0f), size)
                .color = color;
        }

        private static void AddCollider(GameObject root, bool isTrigger)
        {
            Renderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center - root.transform.position;
            collider.size = bounds.size;
            collider.isTrigger = isTrigger;
        }

        private static void Hover(GameObject root, ObjectWordDefinition word)
        {
            BuilderTools.SetReference(root.AddComponent<HoverableObject>(), "word", word);
        }

        private static ObjectWordDefinition Word(string file) => ObjectWordAuthoring.LoadWord(file);

        private static ObjectWordDefinition Word(
            string file,
            string text,
            string meaningKo,
            string example,
            string exampleKo
        ) => ObjectWordAuthoring.FindOrCreateWord(file, text, meaningKo, example, exampleKo);

        private static UnityEngine.Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            return color;
        }
    }
}
