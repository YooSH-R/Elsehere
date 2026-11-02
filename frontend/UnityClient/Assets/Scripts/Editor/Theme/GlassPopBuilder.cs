using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Onboarding;
using A305.EditorTools.UI;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 화면 디자인(글래스 팝)을 준비한다. 창·카드·입력칸은 뒤 화면을 흐리게 비추는 유리, 버튼·배지는 입체 버튼으로 그린다.
    /// 그림은 셰이더가 읽는 채널로 그려 저장하고(없을 때만), 재질·디자인 정의·테마 목록(디자인 하나)을 맞추고,
    /// 흐린 뒤 화면을 만드는 렌더러 기능을 모바일·PC 렌더러에 붙인 뒤 테마 역할이 있는 프리팹을 다시 칠한다.
    /// </summary>
    public static class GlassPopBuilder
    {
        public const string MenuPath = "Tools/A305/UI/Build Glass Pop Design";
        public const string DefinitionPath = DataFolder + "/GlassPop.asset";
        private const string DataFolder = "Assets/Data/Theme";
        private const string CatalogPath = DataFolder + "/UiThemeCatalog.asset";
        private const string ArtFolder = "Assets/Art/UI/Theme/GlassPop";
        private const string DisplayFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";
        private const string IconsPath = DataFolder + "/Icons.asset";
        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/Mobile_Renderer.asset",
            "Assets/Settings/PC_Renderer.asset",
        };

        // 버튼 쓰임 순서(UiTone): 기본 흰색, 주 노랑, 보조 코랄, 강조 하늘, 완료 초록
        private static readonly string[] Tones =
        {
            "#FFFFFF",
            "#FFC23D",
            "#FF7759",
            "#45A8F0",
            "#4CB96B",
        };
        private static readonly string[] ToneInks =
        {
            "#2F251B",
            "#2F251B",
            "#FFFFFF",
            "#FFFFFF",
            "#FFFFFF",
        };

        // 지도 색: 바탕·인도·도로·점선·이름·갈 곳, 그리고 집·카페·호텔·드럭스토어 칸
        private static readonly string[] MapColors =
        {
            "#FBF3E2",
            "#F1E6D1",
            "#C9D6E6",
            "#FFFFFF",
            "#7D6E5B",
            "#FF7759",
            "#FFC23D",
            "#4CB96B",
            "#45A8F0",
            "#FF7759",
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            PrepareDesign();
            AddBlurToRenderers();
            RepaintThemedPrefabs();
            OnboardingBuilder.PaintCamera();
            AssetDatabase.SaveAssets();
            Debug.Log("글래스 팝 디자인을 준비하고 화면 프리팹을 다시 칠했습니다.");
        }

        /// <summary>그림·재질·디자인 정의와 디자인 하나뿐인 테마 목록을 맞춘다.</summary>
        public static UiThemeCatalog PrepareDesign()
        {
            BuilderTools.EnsureFolder(ArtFolder);
            BuilderTools.EnsureFolder(DataFolder);
            UiThemeDefinition definition = Definition();
            var catalog = BuilderTools.LoadOrCreateAsset<UiThemeCatalog>(CatalogPath);
            var fields = new SerializedObject(catalog);
            SerializedProperty themes = fields.FindProperty("themes");
            themes.arraySize = 1;
            themes.GetArrayElementAtIndex(0).objectReferenceValue = definition;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static UiThemeDefinition Definition()
        {
            var definition = BuilderTools.LoadOrCreateAsset<UiThemeDefinition>(DefinitionPath);
            var fields = new SerializedObject(definition);
            // 색은 처음 만들 때만 넣고, 이후에는 Inspector에서 고친 값을 유지한다.
            if (string.IsNullOrEmpty(definition.DisplayName))
            {
                fields.FindProperty("id").enumValueIndex = (int)UiThemeId.Jelly;
                fields.FindProperty("displayName").stringValue = "글래스 팝";
                fields.FindProperty("description").stringValue =
                    "뒤 화면이 비치는 유리 창과 동글동글한 입체 버튼";
                fields.FindProperty("ink").colorValue = Hex("#2F251B");
                fields.FindProperty("mutedInk").colorValue = Hex("#7D6E5B");
                fields.FindProperty("accentInk").colorValue = Hex("#D24F2F");
                fields.FindProperty("panelTint").colorValue = Hex("#FFF8EE");
                fields.FindProperty("cardTint").colorValue = Hex("#FFFFFF");
                fields.FindProperty("badgeColor").colorValue = Hex("#FF5A45");
                SetColors(fields.FindProperty("toneColors"), Tones);
                SetColors(fields.FindProperty("toneInks"), ToneInks);
                SetMap(fields.FindProperty("townMap"));
            }

            fields.FindProperty("titleFont").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ServiceFontBuilder.FontPath);
            fields.FindProperty("displayFont").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            fields.FindProperty("glassMaterial").objectReferenceValue = MaterialIfMissing(
                "Glass",
                "A305/UI/Glass"
            );
            fields.FindProperty("popMaterial").objectReferenceValue = MaterialIfMissing(
                "Pop",
                "A305/UI/Pop"
            );
            fields.FindProperty("panelSprite").objectReferenceValue = SpriteIfMissing(
                "Panel",
                () => Glass(radius: 40, margin: 30, shadowOffset: 10, shadowBlur: 18, band: 34),
                70
            );
            fields.FindProperty("cardSprite").objectReferenceValue = SpriteIfMissing(
                "Card",
                () => Glass(radius: 24, margin: 18, shadowOffset: 6, shadowBlur: 10, band: 18),
                42
            );
            fields.FindProperty("inputSprite").objectReferenceValue = SpriteIfMissing(
                "Input",
                () => Glass(radius: 26, margin: 8, shadowOffset: 2, shadowBlur: 4, band: 0),
                34
            );
            fields.FindProperty("buttonSprite").objectReferenceValue = SpriteIfMissing(
                "Button",
                () => Pop(faceWidth: 46, faceHeight: 46, radius: 22),
                new Vector4(30, 32, 30, 32)
            );
            Sprite pill = SpriteIfMissing(
                "Pill",
                () => Pop(faceWidth: 66, faceHeight: 66, radius: 32),
                new Vector4(40, 42, 40, 42)
            );
            fields.FindProperty("pillSprite").objectReferenceValue = pill;
            fields.FindProperty("badgeSprite").objectReferenceValue = pill;
            fields.FindProperty("scrimSprite").objectReferenceValue = SpriteIfMissing(
                "Scrim",
                Scrim,
                0
            );
            fields.FindProperty("ghostSprite").objectReferenceValue = SpriteIfMissing(
                "Ghost",
                Ghost,
                30
            );
            fields.FindProperty("buttonOutlineSprite").objectReferenceValue = null;
            fields.FindProperty("pillOutlineSprite").objectReferenceValue = null;
            // 그림을 그린 값과 같은 바깥 여백. 이만큼 넓혀 그려 유리·버튼 모양이 칸에 맞는다.
            fields.FindProperty("panelBleed").vector2Value = new Vector2(30, 30);
            fields.FindProperty("cardBleed").vector2Value = new Vector2(18, 18);
            fields.FindProperty("inputBleed").vector2Value = new Vector2(8, 8);
            fields.FindProperty("buttonBleed").vector2Value = new Vector2(PopSide, PopVertical);
            fields.FindProperty("pillBleed").vector2Value = new Vector2(PopSide, PopVertical);
            fields.FindProperty("icons").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiIconSet>(IconsPath);
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>흐린 뒤 화면을 만드는 렌더러 기능을 붙인다. 웹은 모바일 렌더러를 쓴다.</summary>
        public static void AddBlurToRenderers()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                ArtFolder + "/UiBackdropBlur.shader"
            );
            foreach (string path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (
                    data == null
                    || data.rendererFeatures.Any(feature => feature is UiBackdropBlurFeature)
                )
                    continue;
                var blur = ScriptableObject.CreateInstance<UiBackdropBlurFeature>();
                blur.name = "UiBackdropBlur";
                var settings = new SerializedObject(blur);
                settings.FindProperty("shader").objectReferenceValue = shader;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(blur, data);
                data.rendererFeatures.Add(blur);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    blur,
                    out string _,
                    out long localId
                );
                var fields = new SerializedObject(data);
                SerializedProperty map = fields.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                fields.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>테마 역할이 있는 화면 프리팹을 새 디자인으로 다시 칠해 저장한다. 다른 프리팹 안에 넣은 것은 그 프리팹에서 칠한다.</summary>
        public static void RepaintThemedPrefabs()
        {
            foreach (
                string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            )
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponentInChildren<ThemedElement>(true) == null)
                    continue;
                BuilderTools.UpdatePrefabIfExists(
                    path,
                    root =>
                    {
                        foreach (
                            ThemedElement element in root.GetComponentsInChildren<ThemedElement>(
                                true
                            )
                        )
                        {
                            if (!PrefabUtility.IsPartOfPrefabInstance(element))
                                element.ApplyCurrent();
                        }
                    }
                );
            }
        }

        // 유리 창 그림. R = 유리 모양, G = 위쪽 하이라이트와 가장자리 빛(위가 밝다), A = 모양 + 아래로 번진 그림자.
        private static Texture2D Glass(
            int radius,
            int margin,
            float shadowOffset,
            float shadowBlur,
            float band
        )
        {
            int size = 2 * (margin + radius) + 4;
            float half = size / 2f - margin;
            float top = size - margin;
            return Paint(
                size,
                size,
                (x, y) =>
                {
                    Vector2 point = new Vector2(x - size / 2f, y - size / 2f);
                    float distance = RoundedBox(point, half, half, radius);
                    float glass = Mathf.Clamp01(.5f - distance);
                    float edge = 1f - Smooth(0f, 2.4f, -distance);
                    float topness = Mathf.Clamp01((y - margin) / (size - 2f * margin));
                    float highlight = band > 0 ? (1f - Smooth(0f, band, top - y)) * .42f : 0f;
                    float sheen =
                        Mathf.Clamp01(edge * Mathf.Lerp(.35f, 1f, topness) * .9f + highlight)
                        * glass;
                    float shade = RoundedBox(
                        point + new Vector2(0, shadowOffset),
                        half,
                        half,
                        radius
                    );
                    float shadow = (1f - Smooth(-shadowBlur * .6f, shadowBlur, shade)) * .85f;
                    return new Color(glass, sheen, 0f, Mathf.Max(glass, shadow));
                }
            );
        }

        // 입체 버튼 그림. R = 앞면, G = 앞면 위아래 위치(위 1), B = 앞면을 아래로 민 두께, A = 모양 + 바닥 그림자.
        // 위아래 여백을 같게 두어 버튼 이름이 가운데에 오게 한다.
        private const int PopSide = 8;
        private const int PopVertical = 10;

        private static Texture2D Pop(int faceWidth, int faceHeight, int radius)
        {
            const float Thickness = 6f;
            int width = faceWidth + 2 * PopSide;
            int height = faceHeight + 2 * PopVertical;
            float halfWidth = faceWidth / 2f;
            float halfHeight = faceHeight / 2f;
            return Paint(
                width,
                height,
                (x, y) =>
                {
                    Vector2 point = new Vector2(x - width / 2f, y - height / 2f);
                    float face = Mathf.Clamp01(
                        .5f - RoundedBox(point, halfWidth, halfHeight, radius)
                    );
                    float baseCover = Mathf.Clamp01(
                        .5f
                            - RoundedBox(
                                point + new Vector2(0, Thickness),
                                halfWidth,
                                halfHeight,
                                radius
                            )
                    );
                    float shade = RoundedBox(
                        point + new Vector2(0, Thickness + 3f),
                        halfWidth,
                        halfHeight,
                        radius
                    );
                    float shadow = (1f - Smooth(-3f, 5f, shade)) * .8f;
                    float level = Mathf.Clamp01((point.y + halfHeight) / faceHeight) * face;
                    return new Color(
                        face,
                        level,
                        baseCover,
                        Mathf.Max(face, Mathf.Max(baseCover, shadow))
                    );
                }
            );
        }

        // 대화창 바탕 그라데이션. 흰 그림의 투명도만 쓰고 색은 디자인 정의의 어두운 색을 곱한다.
        // 아래 40%는 거의 불투명하게 둬 흰 글자가 읽히고, 그 위로 장면 속으로 부드럽게 옅어진다.
        private static Texture2D Scrim()
        {
            const int Height = 256;
            return Paint(
                4,
                Height,
                (x, y) => new Color(1f, 1f, 1f, .94f * (1f - Smooth(.38f, 1f, y / Height)))
            );
        }

        // 어두운 바탕 위의 반투명 버튼·입력칸. 옅은 흰 면에 위가 밝은 테두리와 위쪽 하이라이트를 얹어 유리 조각처럼 보이게 한다.
        private static Texture2D Ghost()
        {
            const int Size = 64;
            const float Radius = 28;
            const float Half = Size / 2f - 1f;
            return Paint(
                Size,
                Size,
                (x, y) =>
                {
                    Vector2 point = new Vector2(x - Size / 2f, y - Size / 2f);
                    float distance = RoundedBox(point, Half, Half, Radius);
                    float inside = Mathf.Clamp01(.5f - distance);
                    float ring = 1f - Smooth(1.2f, 2.8f, -distance);
                    float topness = y / Size;
                    float highlight = (1f - Smooth(0f, 16f, Size - 1f - y)) * .12f;
                    float alpha = .13f + highlight + ring * Mathf.Lerp(.32f, .62f, topness);
                    return new Color(1f, 1f, 1f, inside * Mathf.Clamp01(alpha));
                }
            );
        }

        private static Texture2D Paint(
            int width,
            int height,
            System.Func<float, float, Color> pixel
        )
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var colors = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                colors[y * width + x] = pixel(x + .5f, y + .5f);
            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }

        private static float RoundedBox(
            Vector2 point,
            float halfWidth,
            float halfHeight,
            float radius
        )
        {
            Vector2 q = new Vector2(
                Mathf.Abs(point.x) - halfWidth + radius,
                Mathf.Abs(point.y) - halfHeight + radius
            );
            return Vector2.Max(q, Vector2.zero).magnitude
                + Mathf.Min(Mathf.Max(q.x, q.y), 0f)
                - radius;
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static Sprite SpriteIfMissing(
            string name,
            System.Func<Texture2D> paint,
            float border
        ) => SpriteIfMissing(name, paint, new Vector4(border, border, border, border));

        /// <summary>그림 파일이 없을 때만 그려 저장한다. 채널을 값으로 읽으므로 색 공간 변환과 압축 없이 가져온다.</summary>
        private static Sprite SpriteIfMissing(
            string name,
            System.Func<Texture2D> paint,
            Vector4 border
        )
        {
            string path = $"{ArtFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
                return existing;
            Texture2D texture = paint();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Material MaterialIfMissing(string name, string shaderName)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var material = new Material(Shader.Find(shaderName)) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetColors(SerializedProperty list, string[] hexes)
        {
            list.arraySize = hexes.Length;
            for (int i = 0; i < hexes.Length; i++)
                list.GetArrayElementAtIndex(i).colorValue = Hex(hexes[i]);
        }

        private static void SetMap(SerializedProperty map)
        {
            string[] names =
            {
                "ground",
                "pavement",
                "road",
                "roadLine",
                "label",
                "destination",
                "home",
                "cafe",
                "hotel",
                "shop",
            };
            for (int i = 0; i < names.Length; i++)
                map.FindPropertyRelative(names[i]).colorValue = Hex(MapColors[i]);
        }

        private static Color Hex(string value) =>
            ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
    }
}
