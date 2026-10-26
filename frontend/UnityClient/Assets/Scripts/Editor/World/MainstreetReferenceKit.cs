using System.Collections.Generic;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>참고 이미지의 넓은 벽면·따뜻한 창·천 차양을 만드는 재사용 부품.</summary>
    internal static class MainstreetReferenceKit
    {
        internal static Material Brick => Surface("CoralMasonry", "#E58B63", true);
        internal static Material Foliage => Surface("GardenGreen", "#79A331", false);
        internal static Material Glow => Emissive("WindowAmberGlow", "#FFCE76", 0.65f);
        internal static Material Frame => Color("WindowWoodV2", "#5F5038");

        internal static GameObject StreetLamp()
        {
            GameObject root = Lamp();
            MainstreetStreetlightBuilder.AddLightIfMissing(root);
            root.transform.Find("Glass").GetComponent<Renderer>().sharedMaterial = Emissive(
                "LanternAmberV2",
                "#FFE4A1",
                2f
            );
            MainstreetStreetlightBuilder.EnableBulbEmission(
                root.transform.Find("Glass").GetComponent<Renderer>().sharedMaterial
            );
            return root;
        }

        internal static Material Surface(string name, string hex, bool brick)
        {
            string path = $"{ArtFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            material = new Material(Shader.Find("Elsehere/SoftMasonry")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_BrickPattern", brick ? 1f : 0f);
            material.SetFloat("_Grain", brick ? 0.015f : 0.05f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Material Emissive(string name, string hex, float strength)
        {
            string path = $"{ArtFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            Material material = Color(name, hex);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", material.GetColor("_BaseColor") * strength);
            return material;
        }

        internal static GameObject TallWindow()
        {
            var root = new GameObject("TallWindow");
            Transform p = root.transform;
            Block("Recess", p, Vector3.zero, new Vector3(1.35f, 2.06f, 0.1f), Frame);
            Block(
                "Glass",
                p,
                new Vector3(0f, 0f, 0.065f),
                new Vector3(1.1f, 1.84f, 0.025f),
                Color("SkyGlassV2", "#4F9EB4")
            );
            Block(
                "WarmLowerPane",
                p,
                new Vector3(0f, -0.57f, 0.085f),
                new Vector3(1.08f, 0.65f, 0.025f),
                Color("WarmGlassV2", "#B3AA77")
            );
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "Jamb",
                    p,
                    new Vector3(side * 0.63f, 0f, 0.11f),
                    new Vector3(0.12f, 2.05f, 0.18f),
                    Cream
                );
                Block(
                    "Lintel",
                    p,
                    new Vector3(0f, side * 1.02f, 0.15f),
                    new Vector3(1.62f, 0.22f, 0.28f),
                    Cream
                );
            }
            Post("LampStem", p, new Vector3(0.12f, -0.63f, 0.13f), 0.025f, 0.43f, Frame);
            SmoothBall(
                "LampShade",
                p,
                new Vector3(0.12f, -0.38f, 0.13f),
                new Vector3(0.38f, 0.32f, 0.06f),
                Glow
            );
            Block(
                "Reflection",
                p,
                new Vector3(-0.33f, 0.23f, 0.1f),
                new Vector3(0.12f, 1.22f, 0.02f),
                Color("ReflectionV2", "#7ABAC7")
            );
            return root;
        }

        internal static GameObject WarmShopWindow()
        {
            var root = new GameObject("WarmShopWindow");
            Transform p = root.transform;
            Block("Recess", p, Vector3.zero, new Vector3(1.85f, 2.5f, 0.16f), Frame);
            Block(
                "InteriorGlow",
                p,
                new Vector3(0f, 0f, 0.1f),
                new Vector3(1.65f, 2.28f, 0.03f),
                Glow
            );
            Block(
                "TopPane",
                p,
                new Vector3(0f, 0.84f, 0.13f),
                new Vector3(1.63f, 0.52f, 0.02f),
                Color("WarmTransom", "#CB984F")
            );
            Block(
                "Transom",
                p,
                new Vector3(0f, 0.55f, 0.15f),
                new Vector3(1.72f, 0.07f, 0.1f),
                Frame
            );
            Block(
                "Mullion",
                p,
                new Vector3(0.32f, 0f, 0.16f),
                new Vector3(0.07f, 2.3f, 0.1f),
                Frame
            );
            Block("Shelf", p, new Vector3(0f, -0.65f, 0.17f), new Vector3(1.65f, 0.1f, 0.2f), Wood);
            Post("PendantCable", p, new Vector3(-0.36f, 0.67f, 0.14f), 0.012f, 0.55f, Frame);
            SmoothBall(
                "Pendant",
                p,
                new Vector3(-0.36f, 0.34f, 0.17f),
                new Vector3(0.28f, 0.3f, 0.1f),
                Emissive("PendantLight", "#FFE5A0", 1.4f)
            );
            for (int index = 0; index < 4; index++)
                Block(
                    "Display",
                    p,
                    new Vector3(-0.58f + index * 0.33f, -0.42f, 0.19f),
                    new Vector3(0.2f, 0.34f + index % 2 * 0.08f, 0.15f),
                    index % 2 == 0 ? Color("TealGoods", "#81AF99") : Cream
                );
            return root;
        }

        internal static void Awning(
            Transform parent,
            float x,
            float width,
            bool striped,
            string title
        )
        {
            int count = Mathf.RoundToInt(width / 0.55f);
            float stripWidth = width / count;
            Material red = Color("FabricRedV2", "#D76E54");
            Material green = Color("FabricGreenV2", "#235F55");
            for (int index = 0; index < count; index++)
            {
                Transform strip = Place(
                    striped ? "StripedAwningStripV2" + index % 2 : "GreenAwningStripV2",
                    parent,
                    new Vector3(x - width / 2f + stripWidth * (index + 0.5f), 0f, 0f),
                    () =>
                    {
                        var root = new GameObject("AwningStrip");
                        Material material = striped ? (index % 2 == 0 ? Cream : red) : green;
                        Transform top = Block(
                            "FabricSlope",
                            root.transform,
                            new Vector3(0f, 2.96f, 0.7f),
                            new Vector3(0.55f, 0.09f, 1.58f),
                            material,
                            0.03f
                        );
                        top.localEulerAngles = new Vector3(30f, 0f, 0f);
                        Scallop(root.transform, material);
                        return root;
                    }
                );
                strip.localScale = new Vector3(stripWidth / 0.55f, 1f, 1f);
            }
            if (!striped)
            {
                Transform letters = Text(
                    "AwningName",
                    title,
                    parent,
                    new Vector3(x, 3.03f, 0.67f),
                    0.52f
                ).transform;
                letters.localEulerAngles = new Vector3(60f, 180f, 0f);
            }
        }

        private static void Scallop(Transform parent, Material material)
        {
            // The valance is a flat piece of cloth with a semicircular lower edge.
            Mesh mesh = LoadMesh(
                "ScallopV2",
                () =>
                {
                    var points = new List<Vector3>
                    {
                        new Vector3(-0.275f, 0.15f, 0f),
                        new Vector3(0.275f, 0.15f, 0f),
                    };
                    for (int index = 0; index <= 12; index++)
                    {
                        float angle = index * Mathf.PI / 12f;
                        points.Add(
                            new Vector3(Mathf.Cos(angle) * 0.275f, -Mathf.Sin(angle) * 0.17f, 0f)
                        );
                    }
                    var triangles = new List<int>();
                    for (int index = 1; index < points.Count - 1; index++)
                    {
                        triangles.AddRange(new[] { 0, index + 1, index, 0, index, index + 1 });
                    }
                    var result = new Mesh();
                    result.SetVertices(points);
                    result.SetTriangles(triangles, 0);
                    var normals = new List<Vector3>();
                    foreach (Vector3 point in points)
                        normals.Add(Vector3.forward);
                    result.SetNormals(normals);
                    result.RecalculateBounds();
                    return result;
                }
            );
            MeshObject(
                "ScallopedValance",
                parent,
                new Vector3(0f, 2.47f, 1.38f),
                Vector3.one,
                mesh,
                material
            );
        }

        internal static void HotelCanopy(Transform parent, float doorX)
        {
            Transform canopy = Place(
                "HotelArchedCanopyV2",
                parent,
                new Vector3(doorX, 2.48f, 0.7f),
                () =>
                {
                    var root = new GameObject("HotelArchedCanopy");
                    Material green = Color("FabricGreenV2", "#235F55");
                    Mesh mesh = LoadMesh(
                        "HotelArchV2",
                        () =>
                        {
                            var vertices = new List<Vector3>();
                            var triangles = new List<int>();
                            for (int index = 0; index <= 20; index++)
                            {
                                float x = -1.8f + index * 3.6f / 20f;
                                float y =
                                    0.2f + 0.65f * Mathf.Sqrt(Mathf.Max(0f, 1f - x * x / 3.24f));
                                vertices.Add(new Vector3(x, y, 0.75f));
                                vertices.Add(new Vector3(x, y, -0.75f));
                                if (index > 0)
                                {
                                    int a = index * 2;
                                    triangles.AddRange(
                                        new[] { a - 2, a, a + 1, a - 2, a + 1, a - 1 }
                                    );
                                }
                            }
                            int centre = vertices.Count;
                            vertices.Add(new Vector3(0f, 0f, 0.75f));
                            for (int index = 0; index < 20; index++)
                                triangles.AddRange(new[] { centre, (index + 1) * 2, index * 2 });
                            vertices.Add(new Vector3(-1.8f, 0f, 0.75f));
                            vertices.Add(new Vector3(1.8f, 0f, 0.75f));
                            triangles.AddRange(
                                new[] { centre, centre + 1, 0, centre, 40, centre + 2 }
                            );
                            var result = new Mesh();
                            result.SetVertices(vertices);
                            result.SetTriangles(triangles, 0);
                            result.RecalculateNormals();
                            result.RecalculateBounds();
                            return result;
                        }
                    );
                    MeshObject("Fabric", root.transform, Vector3.zero, Vector3.one, mesh, green);
                    return root;
                }
            );
            Text("HotelEntranceName", "HOTEL", parent, new Vector3(doorX, 2.9f, 1.47f), 0.42f);
        }

        internal static GameObject GardenTree()
        {
            var root = new GameObject("GardenTree");
            Transform p = root.transform;
            Post("Trunk", p, new Vector3(0f, 1.15f, 0f), 0.16f, 2.3f, Wood);
            foreach (int side in new[] { -1, 1 })
            {
                Transform branch = Post(
                    "Branch",
                    p,
                    new Vector3(side * .25f, 1.9f, 0f),
                    .085f,
                    1.2f,
                    Wood
                );
                branch.localEulerAngles = new Vector3(0f, 0f, -side * 33f);
            }
            SmoothBall(
                "Crown",
                p,
                new Vector3(0f, 3.25f, 0f),
                new Vector3(1.8f, 2.2f, 1.8f),
                Foliage
            );
            SmoothBall(
                "Left",
                p,
                new Vector3(-.75f, 2.7f, .05f),
                new Vector3(1.6f, 1.7f, 1.7f),
                Foliage
            );
            SmoothBall(
                "Right",
                p,
                new Vector3(.7f, 2.95f, -.1f),
                new Vector3(1.6f, 1.8f, 1.65f),
                Foliage
            );
            SmoothBall(
                "Front",
                p,
                new Vector3(.12f, 2.7f, .5f),
                new Vector3(1.5f, 1.5f, 1.5f),
                Foliage
            );
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 1.1f, 0);
            collider.height = 2.2f;
            collider.radius = .2f;
            return root;
        }

        internal static GameObject MarketCrate()
        {
            var root = new GameObject("MarketCrate");
            Block(
                "Crate",
                root.transform,
                new Vector3(0, .22f, 0),
                new Vector3(1.1f, .44f, .7f),
                Wood,
                .05f
            );
            Block(
                "CrateRim",
                root.transform,
                new Vector3(0, .46f, 0),
                new Vector3(1.18f, .09f, .78f),
                Color("CrateRimV2", "#BA8C51")
            );
            for (int row = 0; row < 2; row++)
            for (int index = 0; index < 5; index++)
                Ball(
                    "Produce",
                    root.transform,
                    new Vector3(-.42f + index * .21f, .52f, -.18f + row * .32f),
                    Vector3.one * .25f,
                    Color("OrangesV2", "#F3B34A")
                );
            return root;
        }

        internal static GameObject Chalkboard()
        {
            var root = new GameObject("Chalkboard");
            Transform board = Group("TiltedBoard", root.transform, new Vector3(0, .5f, 0));
            board.localEulerAngles = new Vector3(-12f, 0f, 0f);
            Block("Frame", board, Vector3.zero, new Vector3(.62f, .8f, .1f), Wood);
            Block("Chalk", board, new Vector3(0, 0, .06f), new Vector3(.49f, .66f, .02f), Navy);
            for (int index = 0; index < 3; index++)
                Block(
                    "MenuLine",
                    board,
                    new Vector3(0, .15f - index * .15f, .08f),
                    new Vector3(.3f, .017f, .01f),
                    Cream,
                    .002f
                );
            foreach (int side in new[] { -1, 1 })
                Block(
                    "Leg",
                    root.transform,
                    new Vector3(side * .25f, .25f, -.18f),
                    new Vector3(.06f, .5f, .4f),
                    Wood
                );
            return root;
        }
    }
}
