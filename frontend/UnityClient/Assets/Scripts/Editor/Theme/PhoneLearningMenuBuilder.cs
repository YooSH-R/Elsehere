using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Theme
{
    /// <summary>기존 휴대폰 기능 연결을 보존하고 학습 앱 여섯 개와 작은 보조 버튼 둘로 정리한다.</summary>
    public static class PhoneLearningMenuBuilder
    {
        private const string Folder = "Assets/Art/UI/Phone";
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const float Px = 1.2f;
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Ivory = new Color32(255, 255, 247, 255);
        private static readonly string[] Names =
        {
            "VocabularyButton",
            "HistoryButton",
            "ReviewButton",
            "MapButton",
            "MyPageButton",
            "NeighborsButton",
        };
        private static readonly string[] Labels =
        {
            "단어장",
            "피드백 목록",
            "복습장",
            "마을 지도",
            "내 정보",
            "이웃",
        };
        private static readonly Color32[] Tones =
        {
            new Color32(66, 184, 111, 255),
            new Color32(245, 118, 85, 255),
            new Color32(239, 179, 53, 255),
            new Color32(61, 157, 219, 255),
            new Color32(157, 121, 222, 255),
            new Color32(222, 119, 156, 255),
        };

        [MenuItem("Tools/A305/UI/Apply Phone Learning Icons")]
        public static void ApplyToPrefab()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(HudPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject hud)
        {
            Transform phone = hud.transform.Find("Panel/Menu");
            // 최초 HUD 생성 때는 아직 복습장·이웃이 없다. 각 기능 Builder가 연결을 마친 뒤 다시 호출한다.
            if (phone == null || Names.Any(name => phone.Find(name) == null))
                return;
            WriteArtIfMissing();
            RectTransform backdrop = Rect(phone, "LearningAppBackdrop", 14, 325, 402, 380);
            Picture(backdrop, "PhoneLearningBackdrop");
            backdrop.SetAsLastSibling();
            for (int i = 0; i < Names.Length; i++)
            {
                Transform app = phone.Find(Names[i]);
                ResetLook(app);
                Place((RectTransform)app, 38 + i % 3 * 122, 338 + i / 3 * 148, 110, 128);
                Image hit = app.GetComponent<Image>();
                hit.sprite = null;
                hit.color = Color.clear;
                app.GetComponent<Button>().targetGraphic = hit;
                Picture(Rect(app, "Icon", -7, -12, 124, 124), "PhoneLearningApp" + i);
                Label(app, Labels[i], 0, 110, 110, 24, 18);
                app.Find("CountBadge")?.SetAsLastSibling();
                app.SetAsLastSibling();
            }
            Utility(phone.Find("HomeButton"), "집으로", "PhoneUtilityHome", 48);
            Utility(phone.Find("ControlsButton"), "조작법", "PhoneUtilityControls", 228);
        }

        private static void Utility(Transform button, string caption, string icon, float left)
        {
            ResetLook(button);
            Place((RectTransform)button, left, 657, 154, 40);
            Image face = button.GetComponent<Image>();
            face.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Theme/CloseButtonFace.png"
            );
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 63f / (40 * Px / 2);
            face.color = new Color(1, .98f, .94f, .88f);
            var shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.25f, .17f, .1f, .13f);
            shadow.effectDistance = new Vector2(0, -2);
            button.GetComponent<Button>().targetGraphic = face;
            Picture(Rect(button, "Icon", 26, 7, 26, 26), icon);
            TMP_Text label = Label(button, caption, 61, 0, 74, 40, 18);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            button.SetAsLastSibling();
        }

        private static void ResetLook(Transform button)
        {
            foreach (Component component in button.GetComponents<Component>())
                if (
                    component is LayoutGroup
                    || component is ContentSizeFitter
                    || component is ThemedElement
                    || component is PillContentLayout
                    || component is SurfaceBleed
                    || component is ReviewButtonLabel
                    || component is Shadow
                )
                    Object.DestroyImmediate(component);
            foreach (Transform child in button.Cast<Transform>().ToArray())
                if (child.name != "Label" && child.name != "CountBadge")
                    Object.DestroyImmediate(child.gameObject);
            var layout = button.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;
            Button clickable = button.GetComponent<Button>();
            clickable.transition = Selectable.Transition.None;
        }

        private static TMP_Text Label(
            Transform button,
            string caption,
            float x,
            float y,
            float width,
            float height,
            float size
        )
        {
            var rect = Rect(button, "Label", x, y, width, height);
            TMP_Text text = BuilderTools.GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Jua-Regular SDF.asset"
            );
            text.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                Folder + "/Jua Phone.mat"
            );
            text.text = caption;
            text.fontSize = size * Px;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Midline;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.margin = Vector4.zero;
            text.color = Ink;
            text.raycastTarget = false;
            rect.gameObject.SetActive(true);
            return text;
        }

        private static RectTransform Rect(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            Place(rect, x, y, width, height);
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y) * Px;
            rect.sizeDelta = new Vector2(width, height) * Px;
        }

        private static void Picture(RectTransform rect, string art)
        {
            Image image = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{art}.png");
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.material = null;
            image.raycastTarget = false;
        }

        private static void WriteArtIfMissing()
        {
            if (!File.Exists(Folder + "/PhoneLearningBackdrop.png"))
                WriteBackdrop();
            for (int i = 0; i < Names.Length; i++)
            {
                string name = "PhoneLearningApp" + i;
                if (File.Exists($"{Folder}/{name}.png"))
                    continue;
                var art = new UiArtCanvas(256, 256);
                art.SoftShadow(24, 24, 232, 232, 62, 18, new Color(.3f, .2f, .1f, .14f));
                Color bottom = Tones[i];
                for (int y = 24; y < 232; y++)
                    art.Custom(
                        new Rect(24, y, 208, 1),
                        Color.Lerp(bottom, Color.Lerp(bottom, Color.white, .47f), (y - 24f) / 208f),
                        InsidePlate
                    );
                art.Custom(new Rect(24, 228, 208, 4), new Color(1, 1, 1, .28f), InsidePlate);
                Glyph(art, i);
                Save(name, art.ToTexture());
            }
            if (!File.Exists(Folder + "/PhoneUtilityHome.png"))
            {
                var home = new UiArtCanvas(128, 128);
                Color mint = new Color32(102, 196, 155, 255);
                home.Polygon(mint, new Vector2(15, 65), new Vector2(64, 108), new Vector2(113, 65));
                home.RoundRect(30, 20, 98, 74, 8, mint);
                home.RoundRect(54, 20, 74, 57, 7, Ivory);
                Save("PhoneUtilityHome", home.ToTexture());
            }
            if (!File.Exists(Folder + "/PhoneUtilityControls.png"))
            {
                var pad = new UiArtCanvas(128, 128);
                Color brown = new Color32(169, 132, 80, 255);
                pad.RoundRect(18, 40, 110, 88, 22, brown);
                pad.Circle(34, 43, 23, brown);
                pad.Circle(94, 43, 23, brown);
                pad.Line(6, Ivory, new Vector2(33, 56), new Vector2(53, 56));
                pad.Line(6, Ivory, new Vector2(43, 46), new Vector2(43, 66));
                pad.Circle(83, 53, 5, Ivory);
                pad.Circle(97, 67, 5, Ivory);
                Save("PhoneUtilityControls", pad.ToTexture());
            }
        }

        private static bool InsidePlate(Vector2 point)
        {
            var nearest = new Vector2(Mathf.Clamp(point.x, 86, 170), Mathf.Clamp(point.y, 86, 170));
            return (point - nearest).sqrMagnitude <= 62 * 62;
        }

        private static void Glyph(UiArtCanvas art, int index)
        {
            Color gold = new Color32(255, 214, 107, 255);
            if (index == 0)
            {
                art.RoundRect(92, 87, 164, 174, 12, Ivory);
                art.Line(6, new Color(.82f, .85f, .8f), new Vector2(104, 95), new Vector2(154, 95));
                art.Polygon(
                    gold,
                    new Vector2(135, 174),
                    new Vector2(151, 174),
                    new Vector2(151, 145),
                    new Vector2(143, 152),
                    new Vector2(135, 145)
                );
            }
            else if (index == 1)
            {
                art.RoundRect(85, 108, 171, 169, 18, Ivory);
                art.Polygon(
                    Ivory,
                    new Vector2(103, 115),
                    new Vector2(103, 90),
                    new Vector2(128, 115)
                );
                foreach (float x in new[] { 110f, 128f, 146f })
                    art.Circle(x, 139, 5, gold);
            }
            else if (index == 2)
            {
                art.RoundRect(74, 104, 127, 173, 10, Ivory);
                art.RoundRect(129, 104, 182, 173, 10, Ivory);
                art.Polygon(
                    Ivory,
                    new Vector2(75, 112),
                    new Vector2(128, 94),
                    new Vector2(181, 112)
                );
                art.Line(
                    3,
                    new Color(.89f, .84f, .68f),
                    new Vector2(128, 107),
                    new Vector2(128, 169)
                );
                art.Arc(175, 98, 17, 30, 325, 6, Ivory);
                art.Line(
                    6,
                    Ivory,
                    new Vector2(190, 117),
                    new Vector2(191, 104),
                    new Vector2(178, 105)
                );
            }
            else if (index == 3)
            {
                art.Polygon(
                    Ivory,
                    new Vector2(83, 94),
                    new Vector2(111, 105),
                    new Vector2(111, 173),
                    new Vector2(83, 162)
                );
                art.Polygon(
                    new Color32(220, 239, 250, 255),
                    new Vector2(111, 105),
                    new Vector2(144, 94),
                    new Vector2(144, 162),
                    new Vector2(111, 173)
                );
                art.Polygon(
                    Ivory,
                    new Vector2(144, 94),
                    new Vector2(173, 105),
                    new Vector2(173, 173),
                    new Vector2(144, 162)
                );
                art.Circle(151, 156, 14, gold);
                art.Polygon(
                    gold,
                    new Vector2(139, 149),
                    new Vector2(151, 130),
                    new Vector2(163, 149)
                );
                art.Circle(151, 157, 5, Ivory);
            }
            else
            {
                if (index == 4)
                {
                    art.Circle(128, 155, 22, Ivory);
                    art.RoundRect(87, 92, 169, 128, 22, Ivory);
                    art.Circle(160, 105, 15, gold);
                    art.Circle(160, 105, 7, Ivory);
                }
                else
                {
                    art.Circle(106, 153, 19, Ivory);
                    art.Circle(152, 153, 19, Ivory);
                    art.RoundRect(76, 94, 132, 128, 18, Ivory);
                    art.RoundRect(136, 94, 184, 128, 18, Ivory);
                }
            }
        }

        // 폰 원본에 구워진 예전 아이콘을 덮을 새 화면 바탕. 양쪽 빈 배경의 같은 높이를 보간해 기존 그라데이션과 잇는다.
        private static void WriteBackdrop()
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(Folder + "/PhoneBase.png"));
            var texture = new Texture2D(804, 760, TextureFormat.RGBA32, false);
            float scale = source.width / 610f;
            for (int y = 0; y < texture.height; y++)
            {
                float top = 325 + (texture.height - y - .5f) / 2;
                int sourceY = Mathf.Clamp(
                    Mathf.RoundToInt(source.height - (60 + top) * scale),
                    0,
                    source.height - 1
                );
                Color left = source.GetPixel(Mathf.RoundToInt((90 + 20) * scale), sourceY);
                Color right = source.GetPixel(Mathf.RoundToInt((90 + 410) * scale), sourceY);
                for (int x = 0; x < texture.width; x++)
                    texture.SetPixel(
                        x,
                        y,
                        Color.Lerp(left, right, Mathf.Clamp01((14 + x / 2f - 20) / 390))
                    );
            }
            texture.Apply();
            Object.DestroyImmediate(source);
            Save("PhoneLearningBackdrop", texture);
        }

        private static void Save(string name, Texture2D texture)
        {
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
