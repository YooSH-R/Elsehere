using System;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>마을과 같은 둥근 모서리를 사용하는 집의 재사용 가구와 실내 마감.</summary>
    internal static class HomeInteriorKit
    {
        internal const string Folder = "Assets/Prefabs/Home/CozyCottage";
        internal const string Materials = "Assets/Art/Home/CozyCottage";

        internal static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        internal static Material Paint(string name, string hex)
        {
            string path = Materials + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            Material material = BuilderTools.GetOrCreateMaterial(Materials, name, color);
            material.SetFloat("_Smoothness", .16f);
            material.enableInstancing = true;
            return material;
        }

        internal static Material Ivory => Paint("Ivory", "#F4E5CD");
        internal static Material Oak => Paint("Oak", "#BD8D5D");
        internal static Material LightOak => Paint("LightOak", "#D6AC79");
        internal static Material Sage => Paint("Sage", "#91AB83");
        internal static Material DarkSage => Paint("DarkSage", "#607D68");
        internal static Material Coral => Paint("Terracotta", "#CB846A");
        internal static Material Ink => Paint("Charcoal", "#384A50");
        internal static Material Brass => Paint("Brass", "#CDA769");

        internal static GameObject Asset(string name, Func<GameObject> build, bool combine = true)
        {
            return BuilderTools.CreatePrefabIfMissing(
                Folder + "/" + name + ".prefab",
                () =>
                {
                    GameObject root = build();
                    root.name = name;
                    if (combine)
                        CombineParts(root, "Home_" + name);
                    return root;
                }
            );
        }

        internal static Transform Put(
            GameObject asset,
            Transform parent,
            Vector3 position,
            float yaw = 0
        )
        {
            return BuilderTools
                .InstantiatePrefab(asset, parent, position, Quaternion.Euler(0, yaw, 0))
                .transform;
        }

        internal static void Solid(Transform root, Vector3 center, Vector3 size)
        {
            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        internal static GameObject Sofa()
        {
            var root = new GameObject("Sofa");
            Transform p = root.transform;
            Block("Base", p, V(0, .31f, 0), V(2.6f, .35f, 1.05f), DarkSage, .14f);
            Block("Back", p, V(0, .88f, -.43f), V(2.65f, .95f, .32f), Sage, .15f);
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "SeatCushion",
                    p,
                    V(side * .57f, .58f, .06f),
                    V(1.1f, .25f, .85f),
                    Sage,
                    .12f
                );
                Block("Arm", p, V(side * 1.23f, .68f, .03f), V(.3f, .65f, 1.03f), Sage, .14f);
                Transform pillow = Block(
                    "Pillow",
                    p,
                    V(side * .85f, .96f, -.12f),
                    V(.47f, .48f, .2f),
                    side < 0 ? Ivory : Coral,
                    .09f
                );
                pillow.localRotation = Quaternion.Euler(-14, 0, side * 12);
                foreach (int back in new[] { -1, 1 })
                    Post("Leg", p, V(side * 1.02f, .13f, back * .35f), .065f, .26f, Oak);
            }
            Solid(p, V(0, .6f, 0), V(2.7f, 1.2f, 1.1f));
            return root;
        }

        internal static GameObject Television()
        {
            var root = new GameObject("Television");
            Transform p = root.transform;
            Block("Frame", p, V(0, 1.12f, 0), V(1.6f, .95f, .16f), Ink, .075f);
            Block(
                "Screen",
                p,
                V(0, 1.13f, .087f),
                V(1.43f, .78f, .018f),
                Paint("ScreenBlue", "#73979F"),
                .035f
            );
            // 큰 색면으로만 화면 그림을 표현해 실사 화면과 글자가 시선을 빼앗지 않게 한다.
            SmoothBall("ScreenSun", p, V(.38f, 1.31f, .101f), V(.25f, .25f, .012f), Ivory);
            Block("ScreenLandscape", p, V(-.2f, .87f, .1f), V(.94f, .23f, .012f), DarkSage, .005f);
            foreach (int side in new[] { -1, 1 })
                Block("Foot", p, V(side * .5f, .6f, .06f), V(.09f, .15f, .32f), Ink);
            Solid(p, V(0, 1.11f, 0), V(1.6f, 1.05f, .22f));
            return root;
        }

        internal static GameObject Console()
        {
            var root = new GameObject("MediaConsole");
            Transform p = root.transform;
            Block("Case", p, V(0, .3f, 0), V(2.5f, .49f, .62f), Oak, .07f);
            Block("Top", p, V(0, .56f, 0), V(2.6f, .1f, .7f), LightOak);
            foreach (int side in new[] { -1, 1 })
            {
                Block("Door", p, V(side * .64f, .32f, .32f), V(1.14f, .35f, .05f), LightOak);
                Post("Handle", p, V(side * .19f, .35f, .36f), .025f, .14f, Brass);
            }
            Solid(p, V(0, .3f, 0), V(2.6f, .6f, .7f));
            return root;
        }

        internal static GameObject Refrigerator()
        {
            var root = new GameObject("Refrigerator");
            Transform p = root.transform;
            Block("Body", p, V(0, 1, 0), V(.98f, 2, .82f), Sage, .12f);
            Block("Freezer", p, V(0, 1.62f, .435f), V(.88f, .6f, .1f), Ivory, .05f);
            Block("Door", p, V(0, .64f, .435f), V(.88f, 1.29f, .1f), Ivory, .06f);
            Block("Handle", p, V(-.31f, 1.06f, .53f), V(.05f, .3f, .08f), Brass);
            Block("FreezerHandle", p, V(-.31f, 1.57f, .53f), V(.05f, .18f, .08f), Brass);
            Block(
                "Note",
                p,
                V(.12f, 1.64f, .494f),
                V(.23f, .26f, .012f),
                Paint("Paper", "#FFF2BD"),
                .004f
            );
            SmoothBall("Magnet", p, V(.12f, 1.74f, .51f), V(.055f, .055f, .02f), Coral);
            Solid(p, V(0, 1, .03f), V(1, 2, .95f));
            return root;
        }

        internal static GameObject AirConditioner()
        {
            var root = new GameObject("AirConditioner");
            Transform p = root.transform;
            Block("Body", p, Vector3.zero, V(1.3f, .37f, .3f), Ivory, .12f);
            Block("Vent", p, V(0, -.105f, .145f), V(1.08f, .055f, .025f), Ink, .012f);
            Block("Flap", p, V(0, -.13f, .18f), V(1.08f, .035f, .07f), Ivory);
            SmoothBall("Indicator", p, V(.48f, .025f, .155f), V(.035f, .035f, .01f), Sage);
            Solid(p, Vector3.zero, V(1.3f, .37f, .35f));
            return root;
        }

        internal static GameObject Bookcase()
        {
            var root = new GameObject("Bookcase");
            Transform p = root.transform;
            Block("Back", p, V(0, 1.05f, -.23f), V(1.55f, 2.1f, .09f), Oak);
            foreach (int side in new[] { -1, 1 })
                Block("Side", p, V(side * .74f, 1.05f, 0), V(.1f, 2.1f, .55f), LightOak);
            for (int row = 0; row < 4; row++)
            {
                float y = .12f + row * .64f;
                Block("Shelf", p, V(0, y, 0), V(1.55f, .1f, .57f), LightOak);
                if (row == 3)
                    continue;
                for (int book = 0; book < 6; book++)
                {
                    float height = .33f + (book % 3) * .055f;
                    Material material =
                        book % 3 == 0 ? Coral
                        : book % 3 == 1 ? Sage
                        : Ivory;
                    Block(
                        "Book",
                        p,
                        V(-.55f + book * .2f, y + .05f + height / 2, .07f),
                        V(.14f, height, .32f),
                        material,
                        .015f
                    );
                    Block(
                        "SpineBand",
                        p,
                        V(-.55f + book * .2f, y + .16f, .235f),
                        V(.11f, .025f, .012f),
                        Brass,
                        .003f
                    );
                }
            }
            Solid(p, V(0, 1.05f, 0), V(1.55f, 2.1f, .57f));
            return root;
        }

        internal static GameObject Bed()
        {
            var root = new GameObject("Bed");
            Transform p = root.transform;
            Block("Frame", p, V(0, .27f, 0), V(1.95f, .38f, 2.7f), LightOak, .09f);
            Block("Headboard", p, V(0, .79f, -1.27f), V(2.04f, 1.35f, .17f), Oak, .08f);
            Block("HeadboardInset", p, V(0, .95f, -1.16f), V(1.77f, .63f, .08f), Sage, .035f);
            Block("Mattress", p, V(0, .54f, 0), V(1.83f, .31f, 2.54f), Ivory, .14f);
            Block(
                "Duvet",
                p,
                V(0, .7f, .28f),
                V(1.89f, .21f, 1.98f),
                Paint("Duvet", "#B2C3A2"),
                .1f
            );
            Block("FoldedBlanket", p, V(0, .82f, .9f), V(1.92f, .09f, .48f), Coral, .04f);
            foreach (int side in new[] { -1, 1 })
                Block("Pillow", p, V(side * .44f, .78f, -.9f), V(.73f, .2f, .48f), Ivory, .09f);
            Solid(p, V(0, .5f, 0), V(2.04f, 1, 2.78f));
            return root;
        }

        internal static GameObject KitchenCounter()
        {
            var root = new GameObject("KitchenCounter");
            Transform p = root.transform;
            Block("Cabinet", p, V(0, .49f, 0), V(2.8f, .9f, .72f), DarkSage);
            Block("Worktop", p, V(0, .97f, .01f), V(2.88f, .13f, .83f), Ivory, .055f);
            for (int i = 0; i < 4; i++)
            {
                float x = -1.05f + i * .7f;
                Block("Door", p, V(x, .52f, .38f), V(.63f, .72f, .05f), Sage);
                Block("Handle", p, V(x, .78f, .43f), V(.2f, .035f, .035f), Brass);
            }
            Block("SinkRim", p, V(-.57f, 1.05f, .04f), V(.86f, .05f, .54f), Brass, .02f);
            Block("Sink", p, V(-.57f, 1.077f, .04f), V(.72f, .012f, .42f), Ink, .005f);
            Post("Faucet", p, V(-.57f, 1.25f, -.25f), .035f, .4f, Brass);
            Block("Spout", p, V(-.57f, 1.43f, -.1f), V(.07f, .07f, .33f), Brass);
            Block("Hob", p, V(.78f, 1.05f, 0), V(.83f, .035f, .59f), Ink);
            foreach (int side in new[] { -1, 1 })
                Post("Burner", p, V(.78f + side * .21f, 1.075f, 0), .14f, .018f, Brass);
            Solid(p, V(0, .52f, 0), V(2.9f, 1.04f, .85f));
            return root;
        }

        internal static GameObject CoffeeTable()
        {
            var root = new GameObject("CoffeeTable");
            Transform p = root.transform;
            Block("Top", p, V(0, .53f, 0), V(1.25f, .14f, .75f), LightOak, .065f);
            foreach (int side in new[] { -1, 1 })
            foreach (int back in new[] { -1, 1 })
                Post("Leg", p, V(side * .43f, .25f, back * .22f), .055f, .5f, Oak);
            Block("Book", p, V(.23f, .63f, 0), V(.37f, .055f, .27f), Coral, .013f);
            Solid(p, V(0, .3f, 0), V(1.25f, .6f, .75f));
            return root;
        }

        internal static GameObject SideTable()
        {
            var root = new GameObject("SideTable");
            Transform p = root.transform;
            Block("Body", p, V(0, .32f, 0), V(.6f, .56f, .55f), LightOak);
            Block("Drawer", p, V(0, .4f, .29f), V(.5f, .27f, .035f), Ivory);
            SmoothBall("Knob", p, V(0, .41f, .33f), V(.065f, .065f, .065f), Brass);
            Solid(p, V(0, .32f, 0), V(.6f, .64f, .62f));
            return root;
        }

        internal static GameObject Lamp()
        {
            var root = new GameObject("TableLamp");
            Transform p = root.transform;
            Post("Base", p, V(0, .045f, 0), .17f, .09f, Coral);
            Post("Stem", p, V(0, .26f, 0), .035f, .44f, Brass);
            SmoothBall("Shade", p, V(0, .48f, 0), V(.55f, .44f, .55f), Ivory);
            Light light = Group("WarmLight", p, V(0, .42f, 0)).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new UnityEngine.Color(1, .78f, .48f);
            light.range = 3.6f;
            light.intensity = 1.8f;
            light.shadows = LightShadows.None;
            return root;
        }

        internal static GameObject Window()
        {
            var root = new GameObject("CurtainedWindow");
            Transform p = root.transform;
            Material sky = Paint("WindowDaylight", "#A6CCD2");
            Block("Glass", p, V(0, 0, -.02f), V(1.7f, 1.55f, .055f), sky)
                .GetComponent<Renderer>()
                .shadowCastingMode = ShadowCastingMode.Off;
            foreach (int side in new[] { -1, 1 })
            {
                Block("SideFrame", p, V(side * .88f, 0, .04f), V(.13f, 1.78f, .18f), Ivory);
                Block("CrossFrame", p, V(0, side * .82f, .04f), V(1.9f, .13f, .18f), Ivory);
                for (int fold = 0; fold < 3; fold++)
                    Block(
                        "CurtainFold",
                        p,
                        V(side * (1.01f + fold * .12f), -.05f, .16f + fold % 2 * .03f),
                        V(.18f, 2.04f, .14f),
                        Ivory,
                        .065f
                    );
                Block("Tie", p, V(side * 1.12f, -.35f, .26f), V(.43f, .09f, .07f), Coral);
            }
            Block("Mullion", p, V(0, 0, .06f), V(.065f, 1.6f, .1f), Ivory);
            Block("Transom", p, V(0, 0, .06f), V(1.8f, .065f, .1f), Ivory);
            Block("Sill", p, V(0, -.91f, .15f), V(2.05f, .13f, .42f), LightOak);
            Block("CurtainRail", p, V(0, 1.08f, .17f), V(2.8f, .06f, .06f), Brass);
            return root;
        }

        internal static GameObject Rug()
        {
            var root = new GameObject("LivingRug");
            Transform p = root.transform;
            Block("Border", p, V(0, .018f, 0), V(3.4f, .035f, 3.1f), Coral, .016f);
            Block("Weave", p, V(0, .04f, 0), V(3.16f, .018f, 2.86f), Ivory, .008f);
            for (int i = -3; i <= 3; i++)
                Block(
                    "Stripe",
                    p,
                    V(i * .4f, .052f, 0),
                    V(.025f, .006f, 2.76f),
                    Paint("RugWeave", "#D9CBB0"),
                    .002f
                );
            return root;
        }
    }
}
