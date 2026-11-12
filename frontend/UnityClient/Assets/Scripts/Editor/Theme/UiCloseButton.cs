using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Mission;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 닫기 버튼의 공용 모양(S15P31A305-525). 창 오른쪽 위 X는 흰 원 바탕 가운데에 진회색 X 아이콘을 두고,
    /// 글자로 된 [닫기]는 강조색 대신 흰 보조 알약에 진회색 글자로 둔다. 흰 창 위에서도 보이도록 옅은 테두리와 그림자를 단다.
    /// 여러 Builder가 닫기 버튼을 각자 만들어 모양이 갈렸으므로, 닫기 버튼은 모두 이 함수로 칠한다.
    /// </summary>
    public static class UiCloseButton
    {
        public const string MenuPath = "Tools/A305/UI/Unify Close Buttons";

        /// <summary>창의 X 지름</summary>
        public const float WindowSize = 52f;

        /// <summary>단어 카드처럼 작은 카드의 X 지름</summary>
        public const float CardSize = 44f;

        /// <summary>창 오른쪽 위 가장자리와 X 사이</summary>
        public const float Inset = 20f;

        // X 그림 칸은 지름의 60%다(52 → 31, 44 → 26). 그림에 여백이 있어 X 자체는 가늘고 작게 보이며,
        // 이름 없는 아이콘 버튼은 아이콘 칸이 버튼 높이의 절반 이상이어야 한다는 기준(S15P31A305-515)도 지킨다.
        private const float IconRatio = .6f;

        // PillContentLayout이 아이콘을 글자 크기의 95%로 그린다.
        private const float PillIconScale = .95f;

        // 테마의 알약 그림(GlassPop/Pill)은 전용 셰이더가 읽는 색 채널 그림이라 일반 이미지로 쓰면 분홍으로 보인다.
        // 닫기 버튼은 흰 원 그림을 따로 만들어, 9-slice로 정사각형이면 원·가로로 길면 알약이 되게 한다.
        private const string FaceSpritePath = "Assets/Art/UI/Theme/CloseButtonFace.png";
        private const int FaceTextureSize = 128;
        private const int FaceBorder = FaceTextureSize / 2 - 1;

        // 다른 화면의 글자 [닫기](대화창 단어 카드·음성 동의 창)와 같은 글꼴
        private const string LabelFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";

        // 진회색(#4A4A5A)으로 그린 공용 X 아이콘. 색을 곱하지 않고 그대로 쓴다.
        private const string IconSpritePath = "Assets/Art/UI/Theme/Icons/Close.png";
        private const string FeedbackPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";

        /// <summary>[닫기] 글자 색. X 아이콘 그림의 색과 맞춘다.</summary>
        public static readonly Color Ink = new Color32(0x3A, 0x40, 0x50, 0xFF);

        private static readonly Color Edge = new Color(.16f, .18f, .24f, .14f);
        private static readonly Color Drop = new Color(.16f, .18f, .24f, .18f);

        // 프리팹마다 창 오른쪽 위 X와 지름. 각 화면 Builder도 다시 만들 때 같은 함수로 칠한다.
        private static readonly (string Prefab, string Path, float Size)[] CornerButtons =
        {
            ("Assets/Prefabs/Learning/LearningHud.prefab", "ControlsGuide/CloseButton", WindowSize),
            ("Assets/Prefabs/Learning/LearningHud.prefab", "MyPage/Window/CloseButton", WindowSize),
            ("Assets/Prefabs/Learning/LearningHud.prefab", "History/CloseButton", WindowSize),
            (FeedbackPath, "Panel/TopCloseButton", WindowSize),
            ("Assets/Prefabs/Learning/VocabularyScreen.prefab", "Panel/CloseButton", WindowSize),
            (
                "Assets/Prefabs/Mission/FirstDayMission.prefab",
                "Content/Journal/CloseButton",
                WindowSize
            ),
            (
                "Assets/Prefabs/Mission/FirstDayMission.prefab",
                "Content/MapOverlay/Panel/CloseButton",
                WindowSize
            ),
            ("Assets/Prefabs/ObjectHover/WordTooltip.prefab", "Panel/CloseButton", CardSize),
        };

        /// <summary>
        /// 이미 만든 화면 프리팹의 닫기 버튼만 공용 모양으로 다시 칠한다. 화면 전체를 다시 만들지 않으므로
        /// 다른 Builder가 마무리한 화면 디자인은 그대로 둔다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ApplyToPrefabs()
        {
            BuilderTools.ThrowIfPlaying();
            foreach (var prefab in CornerButtons.GroupBy(button => button.Prefab))
            {
                BuilderTools.UpdatePrefabIfExists(
                    prefab.Key,
                    root =>
                    {
                        foreach (var button in prefab)
                            Corner(root.transform.Find(button.Path), button.Size);
                    }
                );
            }
            // 지도는 공용 X 모양을 쓰되, 길 안내 버튼과 제목 줄에 맞춘 자리를 유지한다.
            FirstDayMissionBuilder.ApplyRouteToggle();
            BuilderTools.UpdatePrefabIfExists(
                FeedbackPath,
                root => Text(root.transform.Find("Panel/Buttons/CloseButton"), "닫기", 22f)
            );
            AssetDatabase.SaveAssets();
            Debug.Log("닫기 버튼을 공용 모양으로 맞췄습니다.");
        }

        /// <summary>창 오른쪽 위 X. 있던 그림·글자를 걷어 내고 흰 원과 X 아이콘만 두어 오른쪽 위 안쪽에 붙인다.</summary>
        public static void Corner(Transform button, float size)
        {
            ClearLook(button);
            var rect = (RectTransform)button;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-Inset, -Inset);
            rect.sizeDelta = new Vector2(size, size);
            var layout = button.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }

            Face(button);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)icon.transform;
            iconRect.SetParent(button, false);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f, .5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = Vector2.one * Mathf.Round(size * IconRatio);
            Image iconImage = icon.GetComponent<Image>();
            iconImage.sprite = BuilderTools.LoadRequiredAsset<Sprite>(IconSpritePath, MenuPath);
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // 알약 버튼과 같은 배치 규칙(PillContentLayout)을 쓴다. 이름을 숨긴 버튼은 아이콘만 가운데에 두고
            // 아이콘 크기를 숨긴 이름의 글자 크기로 정하므로, 빈 이름을 숨겨 두고 글자 크기를 아이콘 크기에 맞춘다.
            var hiddenName = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            hiddenName.transform.SetParent(button, false);
            var name = hiddenName.GetComponent<TextMeshProUGUI>();
            name.text = "";
            name.fontSize = iconRect.sizeDelta.y / PillIconScale;
            name.raycastTarget = false;
            hiddenName.SetActive(false);
            BuilderTools.GetOrAddComponent<PillContentLayout>(button.gameObject).Arrange();
        }

        /// <summary>글자로 된 [닫기]. 크기와 자리는 두고, 흰 보조 알약 바탕에 진회색 글자만 남긴다.</summary>
        public static void Text(Transform button, string caption, float fontSize)
        {
            ClearLook(button);
            Face(button);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(button, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = BuilderTools.LoadRequiredAsset<TMP_FontAsset>(LabelFontPath, MenuPath);
            text.text = caption;
            text.fontSize = fontSize;
            text.color = Ink;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        // 버튼마다 다른 방식(테마 역할·알약 배치·그린 그림·글자 X)으로 붙여 둔 모양을 걷어 낸다. 버튼과 크기 지정은 남긴다.
        private static void ClearLook(Transform button)
        {
            foreach (Transform child in button.Cast<Transform>().ToArray())
                Object.DestroyImmediate(child.gameObject);
            foreach (ThemedElement themed in button.GetComponents<ThemedElement>())
                Object.DestroyImmediate(themed);
            // 알약 그림의 그림자 여백만큼 면을 넓히는 효과(SurfaceBleed)·그림자·테두리 같은 모양 효과도 걷어 낸다.
            foreach (BaseMeshEffect effect in button.GetComponents<BaseMeshEffect>())
                Object.DestroyImmediate(effect);
        }

        // 흰 원을 9-slice로 쓰는 바탕 그림. 없을 때만 만든다.
        private static Sprite FaceSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(FaceSpritePath);
            if (existing != null)
            {
                return existing;
            }

            float radius = FaceTextureSize / 2f;
            var texture = new Texture2D(
                FaceTextureSize,
                FaceTextureSize,
                TextureFormat.RGBA32,
                false
            );
            var pixels = new Color32[FaceTextureSize * FaceTextureSize];
            for (int y = 0; y < FaceTextureSize; y++)
            {
                for (int x = 0; x < FaceTextureSize; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x + .5f, y + .5f),
                        new Vector2(radius, radius)
                    );
                    byte alpha = (byte)
                        Mathf.RoundToInt(Mathf.Clamp01(radius - distance + .5f) * 255);
                    pixels[y * FaceTextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(FaceSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FaceSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FaceSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = new Vector4(FaceBorder, FaceBorder, FaceBorder, FaceBorder);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(FaceSpritePath);
        }

        // 흰 원(알약) 바탕과 흰 창 위에서도 보이는 옅은 테두리·그림자. 버튼이 이 바탕을 눌린 색으로 칠한다.
        private static void Face(Transform button)
        {
            Image face = BuilderTools.GetOrAddComponent<Image>(button.gameObject);
            face.sprite = FaceSprite();
            face.type = Image.Type.Sliced;
            // 알약 버튼이 쓰던 전용 셰이더 재질이 남으면 원이 네모로 그려진다.
            face.material = null;
            // 둥근 끝을 짧은 변의 절반에 맞춰, 정사각형은 원·가로로 긴 버튼은 알약이 되게 한다.
            Vector2 size = ((RectTransform)button).rect.size;
            float shortSide = Mathf.Min(size.x, size.y);
            face.pixelsPerUnitMultiplier = shortSide > 0 ? FaceBorder / (shortSide / 2f) : 1f;
            face.color = Color.white;
            face.raycastTarget = true;
            var edge = button.gameObject.AddComponent<Outline>();
            edge.effectColor = Edge;
            edge.effectDistance = new Vector2(1, -1);
            var drop = button.gameObject.AddComponent<Shadow>();
            drop.effectColor = Drop;
            drop.effectDistance = new Vector2(0, -2);
            Button clickable = button.GetComponent<Button>();
            if (clickable != null)
            {
                clickable.targetGraphic = face;
            }
        }
    }
}
