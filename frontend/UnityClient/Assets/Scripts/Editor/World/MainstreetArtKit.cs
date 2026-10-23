using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>크림색 창틀과 둥근 가로 시설을 공유하는 메인스트리트 아트 모듈.</summary>
    internal static class MainstreetArtKit
    {
        internal const string ArtFolder = "Assets/Art/World/MainstreetArt";
        internal const string PrefabFolder = "Assets/Prefabs/World/MainstreetArt";

        internal static Material Color(string name, string hex)
        {
            string path = $"{ArtFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            Material material = BuilderTools.GetOrCreateMaterial(ArtFolder, name, color);
            material.SetFloat("_Smoothness", 0.18f);
            material.enableInstancing = true;
            return material;
        }

        internal static Material Cream => Color("WarmCream", "#F2DFBA");
        internal static Material Green => Color("DeepGreen", "#326B57");
        internal static Material Navy => Color("SignNavy", "#293E57");
        internal static Material Wood => Color("HoneyWood", "#A77C4C");
        internal static Material Leaf => Color("LeafGreen", "#7FAD50");
        internal static Material Glass => Color("WindowBlue", "#739FAF");

        internal static Transform Group(string name, Transform parent, Vector3 position)
        {
            return BuilderTools.CreateEmpty(name, parent, position, Quaternion.identity);
        }

        internal static Transform Block(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            float radius = 0.04f
        )
        {
            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.48f);
            string key = string.Format(
                CultureInfo.InvariantCulture,
                "Round_{0:0.###}_{1:0.###}_{2:0.###}_{3:0.###}",
                size.x,
                size.y,
                size.z,
                radius
            );
            Mesh mesh = LoadMesh(key, () => RoundedBox(size, radius));
            return MeshObject(name, parent, position, Vector3.one, mesh, material);
        }

        internal static Transform Ball(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material
        )
        {
            return MeshObject(
                name,
                parent,
                position,
                size,
                LoadMesh("SoftSphere", () => Sphere(12, 8)),
                material
            );
        }

        internal static Transform Post(
            string name,
            Transform parent,
            Vector3 position,
            float radius,
            float height,
            Material material
        )
        {
            return MeshObject(
                name,
                parent,
                position,
                new Vector3(radius * 2f, height, radius * 2f),
                LoadMesh("Cylinder12", Cylinder),
                material
            );
        }

        internal static Transform Place(
            string name,
            Transform parent,
            Vector3 position,
            Func<GameObject> build,
            float yaw = 0f
        )
        {
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(
                $"{PrefabFolder}/{name}.prefab",
                () =>
                {
                    GameObject root = build();
                    root.name = name;
                    CombineParts(root, name);
                    return root;
                }
            );
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0f, yaw, 0f))
                .transform;
        }

        internal static TextMeshPro Text(
            string name,
            string value,
            Transform parent,
            Vector3 position,
            float size,
            Material background = null
        )
        {
            TextMeshPro text = TownBlockoutGeometry.Label(
                name,
                value,
                parent,
                position,
                new Vector3(0f, 180f, 0f),
                size
            );
            text.color = background == null ? Cream.color : Navy.color;
            return text;
        }

        internal static GameObject Window()
        {
            var root = new GameObject("Window");
            Transform p = root.transform;
            Block("Recess", p, Vector3.zero, new Vector3(1.55f, 1.86f, 0.08f), Navy);
            Block(
                "Glass",
                p,
                new Vector3(0f, 0f, 0.06f),
                new Vector3(1.3f, 1.58f, 0.04f),
                Glass,
                0.015f
            );
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "Jamb",
                    p,
                    new Vector3(side * 0.73f, 0f, 0.09f),
                    new Vector3(0.12f, 1.83f, 0.15f),
                    Cream
                );
                Block(
                    "Lintel",
                    p,
                    new Vector3(0f, side * 0.91f, 0.11f),
                    new Vector3(1.7f, 0.18f, 0.23f),
                    Cream
                );
            }
            Block(
                "Mullion",
                p,
                new Vector3(0f, 0f, 0.105f),
                new Vector3(0.06f, 1.6f, 0.05f),
                Cream,
                0.01f
            );
            Block(
                "Transom",
                p,
                new Vector3(0f, -0.12f, 0.105f),
                new Vector3(1.35f, 0.06f, 0.05f),
                Cream,
                0.01f
            );
            Block(
                "Reflection",
                p,
                new Vector3(-0.28f, 0.4f, 0.09f),
                new Vector3(0.38f, 0.55f, 0.02f),
                Color("GlassLight", "#A3C7CF"),
                0.01f
            );
            return root;
        }

        internal static GameObject ShopWindow()
        {
            var root = new GameObject("ShopWindow");
            Transform p = root.transform;
            Block("Frame", p, Vector3.zero, new Vector3(1.7f, 2.45f, 0.16f), Green);
            Block(
                "WarmInterior",
                p,
                new Vector3(0f, 0f, 0.1f),
                new Vector3(1.46f, 2.2f, 0.035f),
                Color("WindowWarm", "#CFAD73"),
                0.01f
            );
            Block(
                "UpperReflection",
                p,
                new Vector3(0f, 0.68f, 0.13f),
                new Vector3(1.44f, 0.75f, 0.025f),
                Glass,
                0.01f
            );
            Block(
                "Mullion",
                p,
                new Vector3(0f, 0f, 0.15f),
                new Vector3(0.07f, 2.24f, 0.06f),
                Green,
                0.01f
            );
            Block(
                "Transom",
                p,
                new Vector3(0f, 0.25f, 0.15f),
                new Vector3(1.5f, 0.08f, 0.06f),
                Green,
                0.01f
            );
            Block(
                "DisplayShelf",
                p,
                new Vector3(0f, -0.6f, 0.15f),
                new Vector3(1.5f, 0.07f, 0.1f),
                Wood
            );
            for (int index = 0; index < 3; index++)
                Block(
                    "DisplayBook",
                    p,
                    new Vector3(-0.48f + index * 0.36f, -0.38f, 0.17f),
                    new Vector3(0.22f, 0.35f, 0.12f),
                    index % 2 == 0 ? Cream : Color("Terracotta", "#CE795B")
                );
            return root;
        }

        internal static GameObject Planter(bool windowBox = false)
        {
            var root = new GameObject("Planter");
            Transform p = root.transform;
            float width = windowBox ? 1.55f : 0.85f;
            float height = windowBox ? 0.25f : 0.48f;
            Block(
                "Pot",
                p,
                new Vector3(0f, height / 2f, 0f),
                new Vector3(width, height, 0.6f),
                windowBox ? Cream : Wood,
                0.06f
            );
            Block(
                "Rim",
                p,
                new Vector3(0f, height, 0f),
                new Vector3(width + 0.08f, 0.1f, 0.68f),
                Cream
            );
            for (int index = 0; index < 3; index++)
            {
                float x = (index - 1) * width * 0.28f;
                Ball(
                    "Foliage",
                    p,
                    new Vector3(x, height + 0.19f, 0f),
                    new Vector3(width * 0.55f, 0.48f, 0.62f),
                    Leaf
                );
                Vector3 flower = new Vector3(x, height + 0.31f, 0.25f);
                for (int petal = 0; petal < 5; petal++)
                {
                    float angle = petal * Mathf.PI * 2f / 5f;
                    Ball(
                        "Petal",
                        p,
                        flower
                            + new Vector3(Mathf.Cos(angle) * 0.075f, Mathf.Sin(angle) * 0.075f, 0f),
                        Vector3.one * 0.1f,
                        Cream
                    );
                }
                Ball(
                    "Pollen",
                    p,
                    flower + Vector3.forward * 0.04f,
                    Vector3.one * 0.07f,
                    Color("Gold", "#E0B456")
                );
            }
            return root;
        }

        internal static GameObject Tree()
        {
            var root = new GameObject("Tree");
            Transform p = root.transform;
            Post("Trunk", p, new Vector3(0f, 1f, 0f), 0.15f, 2f, Wood);
            Transform branch = Post("Branch", p, new Vector3(0.25f, 1.8f, 0f), 0.09f, 1.1f, Wood);
            branch.localEulerAngles = new Vector3(0f, 0f, -28f);
            Ball("Crown", p, new Vector3(0f, 2.9f, 0f), new Vector3(1.7f, 1.9f, 1.6f), Leaf);
            Ball(
                "CrownLeft",
                p,
                new Vector3(-0.65f, 2.5f, 0.1f),
                new Vector3(1.45f, 1.5f, 1.4f),
                Leaf
            );
            Ball(
                "CrownRight",
                p,
                new Vector3(0.65f, 2.6f, -0.12f),
                new Vector3(1.45f, 1.7f, 1.4f),
                Color("LeafLight", "#91B65C")
            );
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.radius = 0.2f;
            collider.height = 2f;
            return root;
        }

        internal static GameObject Lamp()
        {
            var root = new GameObject("Lamp");
            Transform p = root.transform;
            Post("Foot", p, new Vector3(0f, 0.09f, 0f), 0.23f, 0.18f, Navy);
            Post("Base", p, new Vector3(0f, 0.35f, 0f), 0.14f, 0.5f, Navy);
            Post("Post", p, new Vector3(0f, 1.65f, 0f), 0.065f, 2.2f, Navy);
            Ball(
                "Glass",
                p,
                new Vector3(0f, 2.88f, 0f),
                new Vector3(0.48f, 0.6f, 0.48f),
                Color("LampWarm", "#FFE4A1")
            );
            Ball("Cap", p, new Vector3(0f, 3.17f, 0f), new Vector3(0.6f, 0.17f, 0.6f), Navy);
            Ball("Finial", p, new Vector3(0f, 3.3f, 0f), Vector3.one * 0.16f, Navy);
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.5f, 0f);
            collider.radius = 0.15f;
            collider.height = 3f;
            return root;
        }

        internal static GameObject Bench()
        {
            var root = new GameObject("Bench");
            Transform p = root.transform;
            for (int index = 0; index < 3; index++)
            {
                Block(
                    "SeatSlat",
                    p,
                    new Vector3(0f, 0.29f, -0.18f + index * 0.16f),
                    new Vector3(1.6f, 0.07f, 0.13f),
                    Wood
                );
                Block(
                    "BackSlat",
                    p,
                    new Vector3(0f, 0.45f + index * 0.15f, -0.25f),
                    new Vector3(1.6f, 0.11f, 0.07f),
                    Wood
                );
            }
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "Leg",
                    p,
                    new Vector3(side * 0.65f, 0.15f, 0f),
                    new Vector3(0.08f, 0.3f, 0.46f),
                    Navy
                );
                Block(
                    "BackPost",
                    p,
                    new Vector3(side * 0.65f, 0.52f, -0.27f),
                    new Vector3(0.07f, 0.56f, 0.08f),
                    Navy
                );
                Block(
                    "Arm",
                    p,
                    new Vector3(side * 0.72f, 0.49f, 0f),
                    new Vector3(0.09f, 0.09f, 0.48f),
                    Navy
                );
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.4f, 0f);
            collider.size = new Vector3(1.6f, 0.8f, 0.55f);
            return root;
        }

        internal static GameObject CafeTable()
        {
            var root = new GameObject("CafeTable");
            Transform p = root.transform;
            Post("Top", p, new Vector3(0f, 0.58f, 0f), 0.38f, 0.07f, Wood);
            Post("Pedestal", p, new Vector3(0f, 0.28f, 0f), 0.055f, 0.55f, Navy);
            Post("Foot", p, new Vector3(0f, 0.04f, 0f), 0.22f, 0.07f, Navy);
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "ChairSeat",
                    p,
                    new Vector3(side * 0.68f, 0.27f, 0f),
                    new Vector3(0.38f, 0.07f, 0.4f),
                    Wood
                );
                Block(
                    "ChairBack",
                    p,
                    new Vector3(side * 0.87f, 0.5f, 0f),
                    new Vector3(0.06f, 0.44f, 0.4f),
                    Wood
                );
                foreach (int front in new[] { -1, 1 })
                    Block(
                        "ChairLeg",
                        p,
                        new Vector3(side * 0.68f, 0.12f, front * 0.14f),
                        new Vector3(0.28f, 0.24f, 0.045f),
                        Navy
                    );
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.size = new Vector3(1.8f, 0.7f, 0.8f);
            return root;
        }

        internal static GameObject Hydrant()
        {
            var root = new GameObject("Hydrant");
            Transform p = root.transform;
            Material red = Color("HydrantRed", "#B94D3E");
            Post("Base", p, new Vector3(0f, 0.07f, 0f), 0.2f, 0.14f, red);
            Post("Body", p, new Vector3(0f, 0.36f, 0f), 0.13f, 0.62f, red);
            Ball("Cap", p, new Vector3(0f, 0.7f, 0f), new Vector3(0.3f, 0.2f, 0.3f), red);
            Transform nozzle = Post("Nozzle", p, new Vector3(0f, 0.46f, 0f), 0.11f, 0.5f, red);
            nozzle.localEulerAngles = new Vector3(0f, 0f, 90f);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.4f, 0f);
            collider.size = new Vector3(0.5f, 0.8f, 0.4f);
            return root;
        }

        internal static void GableRoof(
            Transform parent,
            float width,
            float depth,
            float floorHeight
        )
        {
            string key = string.Format(CultureInfo.InvariantCulture, "Gable_{0}_{1}", width, depth);
            Mesh mesh = LoadMesh(
                key,
                () =>
                {
                    var result = new Mesh();
                    result.vertices = new[]
                    {
                        new Vector3(-width / 2f, 0f, 0.25f),
                        new Vector3(width / 2f, 0f, 0.25f),
                        new Vector3(0f, 1.2f, 0.25f),
                        new Vector3(-width / 2f, 0f, -depth - 0.25f),
                        new Vector3(width / 2f, 0f, -depth - 0.25f),
                        new Vector3(0f, 1.2f, -depth - 0.25f),
                    };
                    result.triangles = new[]
                    {
                        0,
                        1,
                        2,
                        5,
                        4,
                        3,
                        0,
                        2,
                        5,
                        0,
                        5,
                        3,
                        2,
                        1,
                        4,
                        2,
                        4,
                        5,
                        1,
                        0,
                        3,
                        1,
                        3,
                        4,
                    };
                    result.RecalculateNormals();
                    result.RecalculateBounds();
                    return result;
                }
            );
            MeshObject(
                "GableRoof",
                parent,
                new Vector3(0f, floorHeight, 0f),
                Vector3.one,
                mesh,
                Navy
            );
            float slope = Mathf.Atan2(1.2f, width / 2f) * Mathf.Rad2Deg;
            foreach (int side in new[] { -1, 1 })
            {
                Transform trim = Block(
                    "GableTrim",
                    parent,
                    new Vector3(side * width / 4f, floorHeight + 0.6f, 0.3f),
                    new Vector3(Mathf.Sqrt(width * width / 4f + 1.44f) + 0.1f, 0.14f, 0.2f),
                    Cream
                );
                trim.localEulerAngles = new Vector3(0f, 0f, -side * slope);
            }
        }

        private static Transform MeshObject(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Mesh mesh,
            Material material
        )
        {
            Transform transform = Group(name, parent, position);
            transform.localScale = scale;
            transform.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            transform.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return transform;
        }

        private static Mesh LoadMesh(string name, Func<Mesh> create)
        {
            string path = $"{ArtFolder}/Meshes/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            BuilderTools.EnsureFolder($"{ArtFolder}/Meshes");
            mesh = create();
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh RoundedBox(Vector3 size, float radius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 half = size / 2f;
            Vector3 core = half - Vector3.one * radius;
            Vector3[] axes =
            {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back,
            };
            foreach (Vector3 normal in axes)
            {
                Vector3 u = normal.y == 0f ? Vector3.up : Vector3.forward;
                Vector3 v = Vector3.Cross(normal, u);
                float hu = Vector3.Dot(
                    half,
                    new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z))
                );
                float hv = Vector3.Dot(
                    half,
                    new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z))
                );
                float hn = Vector3.Dot(
                    half,
                    new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z))
                );
                float[] us = { -hu, -hu + radius, hu - radius, hu };
                float[] vs = { -hv, -hv + radius, hv - radius, hv };
                int start = vertices.Count;
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    Vector3 point = normal * hn + u * us[x] + v * vs[y];
                    Vector3 inner = new Vector3(
                        Mathf.Clamp(point.x, -core.x, core.x),
                        Mathf.Clamp(point.y, -core.y, core.y),
                        Mathf.Clamp(point.z, -core.z, core.z)
                    );
                    Vector3 outward = (point - inner).normalized;
                    vertices.Add(inner + outward * radius);
                    normals.Add(outward);
                }
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int a = start + y * 4 + x;
                    triangles.AddRange(new[] { a, a + 1, a + 5, a, a + 5, a + 4 });
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Sphere(int segments, int rings)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int y = 0; y <= rings; y++)
            {
                float latitude = Mathf.PI * y / rings;
                for (int x = 0; x <= segments; x++)
                {
                    float longitude = Mathf.PI * 2f * x / segments;
                    vertices.Add(
                        new Vector3(
                            Mathf.Sin(latitude) * Mathf.Cos(longitude),
                            Mathf.Cos(latitude),
                            Mathf.Sin(latitude) * Mathf.Sin(longitude)
                        ) * 0.5f
                    );
                }
            }
            for (int y = 0; y < rings; y++)
            for (int x = 0; x < segments; x++)
            {
                int a = y * (segments + 1) + x;
                triangles.AddRange(
                    new[] { a, a + 1, a + segments + 2, a, a + segments + 2, a + segments + 1 }
                );
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(vertices.Select(vertex => vertex.normalized).ToList());
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Cylinder()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int index = 0; index < 12; index++)
            {
                float angle = index * Mathf.PI * 2f / 12f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, -0.5f, Mathf.Sin(angle) * 0.5f));
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, 0.5f, Mathf.Sin(angle) * 0.5f));
            }
            vertices.Add(Vector3.down * 0.5f);
            vertices.Add(Vector3.up * 0.5f);
            for (int index = 0; index < 12; index++)
            {
                int a = index * 2;
                int b = (a + 2) % 24;
                triangles.AddRange(
                    new[] { a, a + 1, b + 1, a, b + 1, b, 24, a, b, 25, b + 1, a + 1 }
                );
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CombineParts(GameObject root, string name)
        {
            MeshFilter[] parts = root.GetComponentsInChildren<MeshFilter>();
            foreach (
                var group in parts.GroupBy(part => part.GetComponent<MeshRenderer>().sharedMaterial)
            )
            {
                CombineInstance[] instances = group
                    .Select(part => new CombineInstance
                    {
                        mesh = part.sharedMesh,
                        transform =
                            root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix,
                    })
                    .ToArray();
                Mesh mesh = LoadMesh(
                    $"{name}_{group.Key.name}",
                    () =>
                    {
                        var combined = new Mesh();
                        combined.CombineMeshes(instances);
                        return combined;
                    }
                );
                MeshObject(
                    group.Key.name,
                    root.transform,
                    Vector3.zero,
                    Vector3.one,
                    mesh,
                    group.Key
                );
            }
            foreach (MeshFilter part in parts)
                Object.DestroyImmediate(part.gameObject);
        }
    }
}
