using System;
using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static A305.EditorTools.World.HomeInteriorKit;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>Maple House 로비의 마감·가구·조명. 최초 생성 후에는 저장된 프리팹을 편집한다.</summary>
    internal static class HotelInteriorKit
    {
        internal const string Folder = "Assets/Prefabs/Hotel/MapleHouse";

        internal static GameObject Save(string name, Func<GameObject> build, bool combine = false)
        {
            return BuilderTools.CreatePrefabIfMissing(
                Folder + "/" + name + ".prefab",
                () =>
                {
                    GameObject root = build();
                    root.name = name;
                    if (combine)
                        CombineParts(root, "Hotel_" + name);
                    return root;
                }
            );
        }

        internal static Material Finish(string name, string hex)
        {
            const string folder = "Assets/Art/Hotel/MapleHouse";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                folder + "/" + name + ".mat"
            );
            if (material != null)
                return material;
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            material = BuilderTools.GetOrCreateMaterial(folder, name, color);
            material.SetFloat("_Smoothness", .18f);
            material.enableInstancing = true;
            return material;
        }

        private static TextMeshPro Label(
            string name,
            string value,
            Transform parent,
            Vector3 position,
            float size
        )
        {
            TextMeshPro text = Text(name, value, parent, position, size);
            text.transform.localRotation = Quaternion.identity;
            return text;
        }

        internal static GameObject Shell()
        {
            var root = new GameObject("LobbyShell");
            Transform floor = Group("Floor", root.transform, Vector3.zero);
            Material stone = Finish("Limestone", "#E6DDCB");
            Material alternate = Finish("LimestoneLight", "#F0E6D2");
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 7; z++)
                Block(
                    "StoneTile",
                    floor,
                    V(x * 2 - 7, -.04f, z * 2 - 6),
                    V(1.985f, .08f, 1.985f),
                    (x + z) % 2 == 0 ? stone : alternate,
                    .01f
                );
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "FloorBorder",
                    floor,
                    V(side * 7.4f, .008f, 0),
                    V(.14f, .012f, 12.8f),
                    Navy,
                    .003f
                );
                Block(
                    "FloorBorder",
                    floor,
                    V(0, .008f, side * 6.4f),
                    V(14.8f, .012f, .14f),
                    Navy,
                    .003f
                );
            }
            CombineParts(floor.gameObject, "Hotel_StoneFloor");
            Solid(floor, V(0, -.1f, 0), V(16, .2f, 14));
            Wall(root.transform, "NorthWall", V(0, 0, 7), 0, 16);
            Wall(root.transform, "WestWall", V(-8, 0, 0), -90, 14);
            Wall(root.transform, "EastWall", V(8, 0, 0), 90, 14);
            Transform south = Group("SouthWall", root.transform, V(0, 0, -7));
            foreach (int side in new[] { -1, 1 })
            {
                Block("Plaster", south, V(side * 4.55f, 2.3f, 0), V(6.9f, 4.6f, .25f), Ivory);
                Solid(south, V(side * 4.55f, 2.3f, 0), V(6.9f, 4.6f, .25f));
            }
            Block("EntranceHeader", south, V(0, 3.8f, 0), V(2.2f, 1.6f, .25f), Ivory);
            Solid(south, V(0, 3.8f, 0), V(2.2f, 1.6f, .25f));
            foreach (int side in new[] { -1, 1 })
            foreach (float z in new[] { -3.7f, .1f })
            {
                Transform window = Put(
                    Save("LobbyWindow", LobbyWindow),
                    root.transform,
                    V(side * 7.83f, 2.6f, z),
                    side < 0 ? 90 : -90
                );
                window.SetParent(root.transform.Find(side < 0 ? "WestWall" : "EastWall"), true);
            }
            Transform ceiling = Group("Ceiling", root.transform, V(0, 4.65f, 0));
            Block("Plaster", ceiling, Vector3.zero, V(16, .1f, 14), Ivory)
                .GetComponent<Renderer>()
                .shadowCastingMode = ShadowCastingMode.Off;
            Solid(ceiling, Vector3.zero, V(16, .1f, 14));
            foreach (float x in new[] { -4.2f, 4.2f })
                Block("CeilingMoulding", ceiling, V(x, -.1f, 0), V(.14f, .1f, 14), LightOak);
            return root;
        }

        private static void Wall(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            float width
        )
        {
            Transform wall = Group(name, parent, position);
            wall.localRotation = Quaternion.Euler(0, yaw, 0);
            Block("Plaster", wall, V(0, 2.3f, 0), V(width, 4.6f, .25f), Ivory);
            Block("NavyWainscot", wall, V(0, .57f, -.16f), V(width, 1.14f, .08f), Navy);
            Block("ChairRail", wall, V(0, 1.17f, -.2f), V(width, .08f, .12f), LightOak);
            Block("Skirting", wall, V(0, .1f, -.2f), V(width, .2f, .12f), LightOak);
            Block("Cornice", wall, V(0, 4.39f, -.14f), V(width, .22f, .3f), Cream);
            for (float x = -width / 2 + .8f; x < width / 2; x += 1.6f)
                Block("PanelStile", wall, V(x, .62f, -.22f), V(.055f, .86f, .04f), Brass, .01f);
            CombineParts(wall.gameObject, "Hotel_" + name);
            Solid(wall, V(0, 2.3f, 0), V(width, 4.6f, .25f));
        }

        private static GameObject LobbyWindow()
        {
            GameObject root = HomeInteriorKit.Window();
            root.transform.localScale = V(1.15f, 1.15f, 1);
            return root;
        }

        internal static GameObject ReceptionDesk()
        {
            var root = new GameObject("ReceptionDesk");
            Transform p = root.transform;
            Block("Cabinet", p, V(0, .35f, 0), V(5.6f, .66f, 1.1f), Navy, .16f);
            Block("StoneTop", p, V(0, .71f, 0), V(5.85f, .12f, 1.22f), Ivory, .055f);
            Block("Plinth", p, V(0, .06f, -.55f), V(5.4f, .1f, .06f), Brass);
            for (int i = -12; i <= 12; i++)
                Block(
                    "FlutedFront",
                    p,
                    V(i * .21f, .37f, -.556f),
                    V(.035f, .46f, .035f),
                    LightOak,
                    .008f
                );
            Solid(p, V(0, .38f, 0), V(5.85f, .76f, 1.22f));
            return root;
        }

        internal static GameObject ReceptionBackdrop()
        {
            var root = new GameObject("ReceptionBackdrop");
            Transform p = root.transform;
            Block("Panel", p, V(0, 2.1f, 0), V(7.8f, 3.9f, .2f), Navy, .11f);
            foreach (int side in new[] { -1, 1 })
            {
                Post("ArchLeg", p, V(side * 2.1f, 1.63f, -.15f), .032f, 2.3f, Brass);
                Block("PanelEdge", p, V(side * 3.82f, 2.1f, -.13f), V(.06f, 3.7f, .05f), Brass);
            }
            for (int i = 0; i < 24; i++)
            {
                float angle = (i + .5f) * Mathf.PI / 24;
                Transform segment = Block(
                    "ArchCurve",
                    p,
                    V(Mathf.Cos(angle) * 2.1f, 2.78f + Mathf.Sin(angle) * .9f, -.15f),
                    V(.3f, .055f, .06f),
                    Brass,
                    .025f
                );
                segment.localRotation = Quaternion.Euler(
                    0,
                    0,
                    Mathf.Atan2(.9f * Mathf.Cos(angle), -2.1f * Mathf.Sin(angle)) * Mathf.Rad2Deg
                );
            }
            Label("HotelName", "MAPLE HOUSE", p, V(0, 2.85f, -.18f), .48f);
            Label("Welcome", "WELCOME TO ELSEHERE", p, V(0, 2.38f, -.18f), .2f);
            Label("Reception", "R E C E P T I O N", p, V(0, 1.94f, -.18f), .22f);
            Put(Save("KeyCabinet", KeyCabinet), p, V(-2.95f, 1.8f, -.25f));
            Put(Save("LobbyClock", Clock), p, V(2.95f, 2.25f, -.28f));
            return root;
        }

        private static GameObject KeyCabinet()
        {
            var root = new GameObject("KeyCabinet");
            Transform p = root.transform;
            Block("Frame", p, Vector3.zero, V(1.3f, 1.4f, .12f), LightOak);
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
            {
                Vector3 at = V((x - 1) * .4f, (y - 1) * .42f, -.08f);
                Block("KeySlot", p, at, V(.34f, .35f, .04f), Oak);
                SmoothBall("Hook", p, at + V(0, .08f, -.04f), V(.055f, .055f, .04f), Brass);
                Block("KeyTag", p, at + V(0, -.02f, -.07f), V(.12f, .16f, .035f), Ivory);
            }
            return root;
        }

        private static GameObject Clock()
        {
            var root = new GameObject("LobbyClock");
            Transform p = root.transform;
            SmoothBall("Frame", p, Vector3.zero, V(1.12f, 1.12f, .12f), Brass);
            SmoothBall("Face", p, V(0, 0, -.065f), V(.98f, .98f, .04f), Ivory);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                Transform mark = Block(
                    "HourMark",
                    p,
                    V(Mathf.Sin(angle) * .39f, Mathf.Cos(angle) * .39f, -.093f),
                    V(.035f, .085f, .018f),
                    Navy,
                    .008f
                );
                mark.localRotation = Quaternion.Euler(0, 0, -i * 30);
            }
            Block("MinuteHand", p, V(0, .14f, -.112f), V(.035f, .29f, .018f), Navy);
            Transform hand = Block(
                "HourHand",
                p,
                V(.075f, .04f, -.13f),
                V(.19f, .045f, .018f),
                Navy
            );
            hand.localRotation = Quaternion.Euler(0, 0, 30);
            return root;
        }

        internal static GameObject LoungeSet()
        {
            var root = new GameObject("LoungeSet");
            Transform p = root.transform;
            Put(HomeInteriorKit.Asset("SageSofa", Sofa), p, V(0, 0, -.8f));
            Put(HomeInteriorKit.Asset("CoffeeTable", CoffeeTable), p, V(0, 0, .7f));
            Block("RugBorder", p, V(0, .015f, .1f), V(3.8f, .03f, 3.3f), Coral, .014f);
            Block(
                "RugCenter",
                p,
                V(0, .033f, .1f),
                V(3.55f, .012f, 3.05f),
                Finish("Linen", "#D8CAB1"),
                .005f
            );
            Put(HomeInteriorKit.Asset("SideTable", SideTable), p, V(1.8f, 0, -.8f));
            Put(
                HomeInteriorKit.Asset("TableLamp", HomeInteriorKit.Lamp, false),
                p,
                V(1.8f, .62f, -.8f)
            );
            return root;
        }

        internal static GameObject LuggageCart()
        {
            var root = new GameObject("LuggageCart");
            Transform p = root.transform;
            Block("Platform", p, V(0, .24f, 0), V(1.5f, .15f, 1), Coral, .07f);
            foreach (int x in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
                SmoothBall("Caster", p, V(x * .58f, .11f, z * .36f), V(.19f, .22f, .16f), Ink);
            foreach (int x in new[] { -1, 1 })
                Post("Upright", p, V(x * .66f, 1.05f, 0), .045f, 1.65f, Brass);
            Block("TopRail", p, V(0, 1.9f, 0), V(1.4f, .09f, .1f), Brass);
            Put(Save("Suitcase", Suitcase, true), p, V(-.3f, .32f, 0));
            Transform second = Put(Save("Suitcase", Suitcase, true), p, V(.36f, .32f, .1f), 90);
            second.localScale = Vector3.one * .78f;
            Solid(p, V(0, .9f, 0), V(1.5f, 1.8f, 1));
            return root;
        }

        private static GameObject Suitcase()
        {
            var root = new GameObject("Suitcase");
            Transform p = root.transform;
            Block("Case", p, V(0, .36f, 0), V(.62f, .72f, .4f), DarkSage, .09f);
            foreach (int side in new[] { -1, 1 })
                Block("Strap", p, V(side * .19f, .36f, -.207f), V(.055f, .61f, .018f), LightOak);
            Block("Handle", p, V(0, .78f, 0), V(.27f, .065f, .075f), Brass);
            Block("Tag", p, V(.12f, .62f, -.23f), V(.12f, .18f, .02f), Ivory);
            return root;
        }

        internal static GameObject LiftFacade()
        {
            var root = new GameObject("LiftFacade");
            Transform p = root.transform;
            Block("Frame", p, V(0, 1.45f, 0), V(2.25f, 2.9f, .24f), LightOak, .1f);
            foreach (int side in new[] { -1, 1 })
                Block(
                    "DoorLeaf",
                    p,
                    V(side * .47f, 1.36f, -.14f),
                    V(.92f, 2.55f, .06f),
                    Finish("LiftMetal", "#A9AAA2")
                );
            Label("FloorIndicator", "1", p, V(0, 3.07f, -.15f), .2f);
            Block("Indicator", p, V(0, 3.07f, -.06f), V(.55f, .36f, .12f), Navy);
            Block("CallPanel", p, V(1.33f, 1.03f, -.12f), V(.18f, .31f, .05f), Brass);
            SmoothBall("CallButton", p, V(1.33f, 1.03f, -.16f), V(.08f, .08f, .03f), Ivory);
            Solid(p, V(0, 1.45f, 0), V(2.25f, 2.9f, .3f));
            return root;
        }

        internal static GameObject Sconce()
        {
            var root = new GameObject("GlobeSconce");
            Transform p = root.transform;
            Block("Backplate", p, Vector3.zero, V(.17f, .42f, .08f), Brass);
            Block("Arm", p, V(0, -.08f, -.16f), V(.06f, .06f, .3f), Brass);
            SmoothBall("OpalGlobe", p, V(0, .1f, -.31f), V(.42f, .42f, .42f), Bulb());
            PointLight(p, V(0, .12f, -.48f), 1.2f, 4.2f);
            return root;
        }

        internal static GameObject Chandelier()
        {
            var root = new GameObject("LobbyChandelier");
            Transform p = root.transform;
            Post("Stem", p, V(0, -.4f, 0), .035f, .8f, Brass);
            Block("Crossbar", p, V(0, -.82f, 0), V(2.5f, .08f, .08f), Brass);
            foreach (float x in new[] { -1.15f, 0, 1.15f })
                SmoothBall("OpalGlobe", p, V(x, -1.02f, 0), V(.48f, .48f, .48f), Bulb());
            PointLight(p, V(0, -1.25f, 0), 2.1f, 7);
            return root;
        }

        private static Material Bulb()
        {
            Material material = Finish("OpalLight", "#FFF0C9");
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new UnityEngine.Color(1, .8f, .45f) * 1.4f);
            return material;
        }

        private static void PointLight(
            Transform parent,
            Vector3 position,
            float intensity,
            float range
        )
        {
            Light light = Group("WarmLight", parent, position).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new UnityEngine.Color(1, .87f, .66f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        internal static GameObject Decoration()
        {
            var root = new GameObject("LobbyDecoration");
            Transform p = root.transform;
            Put(Save("ReceptionDesk", ReceptionDesk, true), p, V(0, 0, 4.6f));
            Put(Save("ReceptionBackdrop", ReceptionBackdrop), p, V(0, 0, 6.78f));
            Put(Save("LoungeSet", LoungeSet), p, V(-5.5f, 0, -.9f), 90);
            Put(Save("LoungeSet", LoungeSet), p, V(5.5f, 0, -.9f), -90);
            Put(Save("LuggageCart", LuggageCart), p, V(-6, 0, 3.9f), -15);
            foreach (int side in new[] { -1, 1 })
            {
                Put(Save("LiftFacade", LiftFacade), p, V(side * 5.9f, 0, 6.78f));
                var plant = BuilderTools.LoadRequiredAsset<GameObject>(
                    TinyTreatsPropsBuilder.Prefabs + "HousePlants/Monstera.prefab",
                    "Tiny Treats props"
                );
                Put(plant, p, V(side * 6.5f, 0, -5.2f)).localScale = Vector3.one * 1.8f;
            }
            Block("RunnerBorder", p, V(0, .018f, -1.9f), V(3.15f, .035f, 6.4f), Brass, .015f);
            Block(
                "Runner",
                p,
                V(0, .04f, -1.9f),
                V(2.97f, .012f, 6.2f),
                Finish("RunnerClay", "#BA7967"),
                .005f
            );
            for (int i = -2; i <= 2; i++)
                Block(
                    "RunnerStripe",
                    p,
                    V(0, .049f, -1.9f + i * 1.15f),
                    V(2.75f, .006f, .025f),
                    Cream,
                    .002f
                );
            Post("DeskBellBase", p, V(-1.8f, .795f, 4.3f), .13f, .05f, Ink);
            SmoothBall("DeskBell", p, V(-1.8f, .87f, 4.3f), V(.2f, .15f, .2f), Brass);
            Block("GuestBook", p, V(1.8f, .8f, 4.35f), V(.48f, .06f, .32f), Ivory);
            Block("GuestBookCover", p, V(1.8f, .772f, 4.35f), V(.5f, .015f, .34f), Coral);
            return root;
        }
    }
}
