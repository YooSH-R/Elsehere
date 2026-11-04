using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 외형 표(<see cref="NpcCast"/>)의 배색을 장면의 NPC에 입힌다. 외형의 몸·눈 색 텍스처에서 머리·옷·신발·눈동자 범위만 색을 바꾼
    /// 사본을 NPC마다 <c>Art/Characters/NpcCast/&lt;NPC 키&gt;/</c>에 두고, 그 사본을 쓰는 재질로 바꿔 입힌다.
    /// 배색이 같으면 텍스처를 다시 쓰지 않는다(가져오기 설정의 userData에 배색을 적어 둔다). 재질 사본은 실행할 때마다
    /// 외형의 원래 재질 설정(셰이더·그림자 받기 등)에 맞춘다. 배색이 없는 NPC는 외형의 원래 재질로 되돌린다.
    /// </summary>
    public static class NpcCastDresser
    {
        public const string ColorFolder = "Assets/Art/Characters/NpcCast";

        // 경찰 제복처럼 외형 재질을 한 번 바꿔 입힌 바탕 재질은 경찰서 Builder가 만든다.
        private const string BaseMaterialMenu = "Tools/A305/World/Apply Police Station";

        // 몸 색 사본은 원본(2048)의 절반으로 저장한다. 화면에서 작게 보이는 NPC라 차이가 없고 저장소를 덜 차지한다.
        private const int BodyTextureSize = 1024;

        // 텍스처 조각 가장자리의 여백까지 높이를 넓혀, 색을 바꾼 부분 둘레에 원래 색 테두리가 남지 않게 한다.
        private const int HeightPaddingPixels = 6;

        // 바꾼 머리카락 사이에서 메울 틈의 반 폭(1024 텍스처 기준). 가닥 사이 선은 1~3픽셀이다.
        private const int GapRadiusPixels = 4;

        private static readonly Dictionary<string, float[]> HeightsByLook =
            new Dictionary<string, float[]>();

        /// <summary>
        /// NPC(또는 외형 사본) 아래 외형의 몸·눈 재질을 배색 사본으로 바꾼다. 위치·연결은 그대로 둔다.
        /// 외형 프리팹에서 온 렌더러만 바꾸며, 프리팹의 원래 재질을 기준으로 다시 맞춰 이미 입힌 사본도 배색이 바뀌면 따라간다.
        /// </summary>
        public static void Dress(GameObject npc, NpcCastMember member)
        {
            Material baseMaterial =
                member.Look.BaseMaterialPath != null
                    ? BuilderTools.LoadRequiredAsset<Material>(
                        member.Look.BaseMaterialPath,
                        BaseMaterialMenu
                    )
                    : null;
            foreach (Renderer renderer in npc.GetComponentsInChildren<Renderer>(true))
            {
                Renderer original = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                if (original == null)
                {
                    continue;
                }

                Material[] sources = original.sharedMaterials;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length && index < sources.Length; index++)
                {
                    Material wanted = Wanted(member, sources[index], baseMaterial);
                    if (wanted != null && wanted != materials[index])
                    {
                        materials[index] = wanted;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
        }

        /// <summary>
        /// 외형 텍스처의 픽셀마다 그 자리가 그려진 몸 높이(발바닥 0 ~ 머리 꼭대기 1). 서 있는 자세의 몸 메시 삼각형을 텍스처 좌표에 칠해 구한다.
        /// 몸에 쓰이지 않는 자리는 -1이다.
        /// </summary>
        internal static float[] BodyHeights(NpcLook look, int size)
        {
            string key = look.Name + "/" + size;
            if (HeightsByLook.TryGetValue(key, out float[] cached))
            {
                return cached;
            }

            var copy = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        look.PrefabPath,
                        NpcVisualBuilder.MenuPath
                    )
                );
            try
            {
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = copy.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Update(0);
                SkinnedMeshRenderer body = copy.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .First(skin => UsesTexture(skin.sharedMaterials, look.BodyTexturePath));
                float[] heights = PaintHeights(body, look.BodyTexturePath, size);
                HeightsByLook[key] = heights;
                return heights;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        // 몸 색 텍스처와 눈 색 텍스처를 쓰는 재질만 바꾼다. 입·이·눈꺼풀 재질은 그대로 둔다.
        private static Material Wanted(NpcCastMember member, Material source, Material baseMaterial)
        {
            if (source == null)
            {
                return null;
            }

            string texturePath = AssetDatabase.GetAssetPath(BaseTexture(source));
            bool body = texturePath == member.Look.BodyTexturePath;
            bool eye =
                !member.Look.EyesInBody
                && Path.GetFileNameWithoutExtension(texturePath).EndsWith("EyeColor");
            if (!body && !eye)
            {
                return null;
            }

            Material plain = body && baseMaterial != null ? baseMaterial : source;
            IReadOnlyList<NpcColorSwap> swaps = body ? member.BodySwaps() : member.EyeSwaps();
            if (swaps.Count == 0)
            {
                return plain;
            }

            string name = body ? "Body" : "Eye_" + BaseTexture(source).name;
            return body
                ? Variant(member, plain, name, swaps, member.HairSwaps(), BodyTextureSize)
                : Variant(member, plain, name, swaps, new NpcColorSwap[0], 0);
        }

        private static Material Variant(
            NpcCastMember member,
            Material plain,
            string name,
            IReadOnlyList<NpcColorSwap> swaps,
            IReadOnlyList<NpcColorSwap> gapFilledSwaps,
            int size
        )
        {
            string folder = ColorFolder + "/" + member.Code;
            BuilderTools.EnsureFolder(folder);
            string texturePath = folder + "/" + name + ".png";
            string materialPath = folder + "/" + name + ".mat";
            string recipe = member.Recipe(name);
            var importer = AssetImporter.GetAtPath(texturePath);
            if (importer == null || importer.userData != recipe)
            {
                float[] heights = size > 0 ? BodyHeights(member.Look, size) : null;
                WriteRecolored(
                    AssetDatabase.GetAssetPath(BaseTexture(plain)),
                    texturePath,
                    swaps,
                    gapFilledSwaps,
                    size,
                    heights
                );
                importer = AssetImporter.GetAtPath(texturePath);
                importer.userData = recipe;
                importer.SaveAndReimport();
            }

            // 텍스처만 색 사본이고 셰이더·그림자 받기 같은 설정은 원래 재질을 따른다. 이미 있는 사본도 다시 맞춘다.
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(plain);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = plain.shader;
                material.CopyPropertiesFromMaterial(plain);
            }

            var recolored = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", recolored);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", recolored);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void WriteRecolored(
            string sourcePath,
            string targetPath,
            IReadOnlyList<NpcColorSwap> swaps,
            IReadOnlyList<NpcColorSwap> gapFilledSwaps,
            int size,
            float[] heights
        )
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(sourcePath));
            if (size > 0 && texture.width != size)
            {
                Texture2D resized = Resize(texture, size);
                Object.DestroyImmediate(texture);
                texture = resized;
            }

            Color32[] source = texture.GetPixels32();
            Color32[] recolored = NpcColorSwap.Recolor(source, swaps, heights);
            foreach (NpcColorSwap swap in gapFilledSwaps)
            {
                NpcColorSwap.FillGaps(
                    source,
                    recolored,
                    texture.width,
                    swap,
                    heights,
                    GapRadiusPixels
                );
            }

            texture.SetPixels32(recolored);
            File.WriteAllBytes(targetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(targetPath);
        }

        private static Texture2D Resize(Texture2D source, int size)
        {
            RenderTexture previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var resized = new Texture2D(size, size, TextureFormat.RGBA32, false);
            resized.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            resized.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return resized;
        }

        private static float[] PaintHeights(SkinnedMeshRenderer body, string texturePath, int size)
        {
            var baked = new Mesh();
            try
            {
                body.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                var heights = new float[vertices.Length];
                float bottom = float.PositiveInfinity;
                float top = float.NegativeInfinity;
                for (int index = 0; index < vertices.Length; index++)
                {
                    heights[index] = body.transform.TransformPoint(vertices[index]).y;
                    bottom = Mathf.Min(bottom, heights[index]);
                    top = Mathf.Max(top, heights[index]);
                }

                var map = Enumerable.Repeat(-1f, size * size).ToArray();
                Mesh mesh = body.sharedMesh;
                Vector2[] uv = mesh.uv;
                Material[] materials = body.sharedMaterials;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (
                        submesh >= materials.Length
                        || !UsesTexture(new[] { materials[submesh] }, texturePath)
                    )
                    {
                        continue;
                    }

                    int[] triangles = mesh.GetTriangles(submesh);
                    for (int corner = 0; corner < triangles.Length; corner += 3)
                    {
                        PaintTriangle(
                            map,
                            size,
                            uv,
                            heights,
                            triangles[corner],
                            triangles[corner + 1],
                            triangles[corner + 2],
                            bottom,
                            top - bottom
                        );
                    }
                }

                Pad(map, size);
                return map;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        private static void PaintTriangle(
            float[] map,
            int size,
            Vector2[] uv,
            float[] heights,
            int a,
            int b,
            int c,
            float bottom,
            float span
        )
        {
            Vector2 p0 = uv[a] * size;
            Vector2 p1 = uv[b] * size;
            Vector2 p2 = uv[c] * size;
            float area = (p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y);
            if (Mathf.Abs(area) < 1e-6f)
            {
                return;
            }

            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x + .5f, y + .5f);
                    float w1 =
                        ((point.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (point.y - p0.y))
                        / area;
                    float w2 =
                        ((p1.x - p0.x) * (point.y - p0.y) - (point.x - p0.x) * (p1.y - p0.y))
                        / area;
                    float w0 = 1f - w1 - w2;
                    if (w0 < -.01f || w1 < -.01f || w2 < -.01f)
                    {
                        continue;
                    }

                    float height = w0 * heights[a] + w1 * heights[b] + w2 * heights[c];
                    map[y * size + x] = (height - bottom) / span;
                }
            }
        }

        // 칠하지 않은 자리를 이웃한 칠한 자리의 높이로 몇 픽셀 넓힌다.
        private static void Pad(float[] map, int size)
        {
            for (int pass = 0; pass < HeightPaddingPixels; pass++)
            {
                float[] before = (float[])map.Clone();
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int index = y * size + x;
                        if (before[index] >= 0f)
                        {
                            continue;
                        }

                        if (x > 0 && before[index - 1] >= 0f)
                            map[index] = before[index - 1];
                        else if (x < size - 1 && before[index + 1] >= 0f)
                            map[index] = before[index + 1];
                        else if (y > 0 && before[index - size] >= 0f)
                            map[index] = before[index - size];
                        else if (y < size - 1 && before[index + size] >= 0f)
                            map[index] = before[index + size];
                    }
                }
            }
        }

        private static bool UsesTexture(IEnumerable<Material> materials, string texturePath)
        {
            return materials.Any(material =>
                material != null && AssetDatabase.GetAssetPath(BaseTexture(material)) == texturePath
            );
        }

        private static Texture BaseTexture(Material material)
        {
            return material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.mainTexture;
        }
    }
}
