using System;
using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Learning;
using A305.Mission;
using A305.Player;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Learning
{
    /// <summary>설문 진입점·메인 화면 스피커와 기존 휴대폰을 같은 상단 배치에 연결한다.</summary>
    public static class SurveyHudBuilder
    {
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string ArtPath = "Assets/Art/UI/Hud/";
        private const int EntrySortingOrder = 109;
        private static readonly Color Ink = new Color32(62, 46, 32, 255);

        [MenuItem("Tools/A305/Learning/Apply Survey HUD Entry")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            Sprite icon = SaveArt("SurveyEntry", Clipboard(), Vector4.zero);
            Sprite panel = SaveArt(
                "SurveyInvitationPanel",
                InvitationPanel(),
                new Vector4(64, 64, 64, 64)
            );
            Sprite button = SaveArt(
                "SurveyParticipate",
                Participate(),
                new Vector4(36, 36, 36, 36)
            );
            BuilderTools.UpdatePrefabIfExists(HudPath, root => Apply(root, icon, button, panel));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/Learning/Apply Survey Entry Layer")]
        public static void ApplyEntryLayerToPrefab()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                HudPath,
                root =>
                    PlaceEntryOnHudLayer(root.transform.Find("SurveyPrompt/EntryButton").gameObject)
            );
            AssetDatabase.SaveAssets();
        }

        // HudTopBarLayout 값을 바꾼 뒤에는 이 메뉴로 저장된 설문 아이콘 자리를 다시 맞춘다.
        [MenuItem("Tools/A305/Learning/Apply Survey Entry Position")]
        public static void ApplyEntryPositionToPrefab()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                HudPath,
                root => PlaceEntry((RectTransform)root.transform.Find("SurveyPrompt/EntryButton"))
            );
            AssetDatabase.SaveAssets();
        }

        // 휴대폰 바로 왼쪽 칸. 시계·코인 칩은 FirstDayMissionView가 이 칸만큼 왼쪽으로 비킨다.
        private static void PlaceEntry(RectTransform entry)
        {
            entry.anchorMin = entry.anchorMax = entry.pivot = Vector2.one;
            entry.anchoredPosition = new Vector2(
                -HudTopBarLayout.SurveyRight,
                -HudTopBarLayout.Margin
            );
            entry.sizeDelta = new Vector2(HudTopBarLayout.SurveyWidth, HudTopBarLayout.Size);
        }

        // 접힌 버튼은 HUD에 남기고, 설문 안내 카드만 상위 레이어를 사용한다.
        private static void PlaceEntryOnHudLayer(GameObject entry)
        {
            // 활성 상태에서 캔버스를 연결해야 Unity가 부모 캔버스와 그리기 순서를 구분한다.
            bool wasActive = entry.activeSelf;
            entry.SetActive(true);
            Canvas canvas = BuilderTools.GetOrAddComponent<Canvas>(entry);
            canvas.overrideSorting = true;
            canvas.sortingOrder = EntrySortingOrder;
            BuilderTools.GetOrAddComponent<GraphicRaycaster>(entry);
            entry.SetActive(wasActive);
        }

        private static void Apply(GameObject root, Sprite icon, Sprite action, Sprite panelSprite)
        {
            var phone = (RectTransform)root.transform.Find("Panel/MenuBar");
            BuilderTools.GetOrAddComponent<LayoutElement>(phone.gameObject).ignoreLayout = true;
            phone.anchorMin = phone.anchorMax = phone.pivot = Vector2.one;
            phone.sizeDelta = new Vector2(HudTopBarLayout.PhoneWidth, HudTopBarLayout.Size);
            foreach (string name in new[] { "MenuButton", "OpenMenuButton" })
            {
                Transform button = phone.Find(name);
                var size = button.GetComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = HudTopBarLayout.PhoneWidth;
                size.minHeight = size.preferredHeight = HudTopBarLayout.Size;
                if (button.Find("Art") != null)
                    OpticalIconPlacement.Apply(
                        button.Find("Art").GetComponent<Image>(),
                        HudTopBarLayout.Size
                    );
            }
            BuilderTools.GetOrAddComponent<HudTopBarView>(root).Configure(phone);
            var rootRect = (RectTransform)root.transform;
            phone.position = rootRect.TransformPoint(
                new Vector3(
                    rootRect.rect.xMax - HudTopBarLayout.PhoneRight,
                    rootRect.rect.yMax - HudTopBarLayout.Margin,
                    0
                )
            );

            Transform layer = root.transform.Find("SurveyPrompt");
            var card = (RectTransform)layer.Find("Card");
            // 위치·크기는 확정한 작은 안내창을 직접 배치하고 기존 HUD는 재생성하지 않는다.
            foreach (LayoutGroup group in card.GetComponentsInChildren<LayoutGroup>(true))
                Object.DestroyImmediate(group);
            foreach (ContentSizeFitter fit in card.GetComponentsInChildren<ContentSizeFitter>(true))
                Object.DestroyImmediate(fit);
            foreach (ThemedElement theme in card.GetComponentsInChildren<ThemedElement>(true))
                Object.DestroyImmediate(theme);
            foreach (SurfaceBleed bleed in card.GetComponentsInChildren<SurfaceBleed>(true))
                Object.DestroyImmediate(bleed);
            card.anchorMin = card.anchorMax = new Vector2(.5f, 1);
            card.pivot = new Vector2(.5f, 1);
            card.anchoredPosition = new Vector2(0, -128);
            card.sizeDelta = new Vector2(760, 198);
            card.localScale = Vector3.one;
            BuilderTools.GetOrAddComponent<CanvasGroup>(card.gameObject);
            var panel = card.GetComponent<Image>();
            panel.sprite = panelSprite;
            panel.material = null;
            panel.type = Image.Type.Sliced;
            panel.color = new Color(1, 1, 1, .95f);

            TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Jua-Regular SDF.asset"
            );
            TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset"
            );
            if (regular == null)
                regular = bold;
            Text(
                card.Find("Title").GetComponent<TMP_Text>(),
                bold,
                SurveyPromptView.Title,
                32,
                132,
                26,
                540,
                44
            );
            Text(
                card.Find("Body").GetComponent<TMP_Text>(),
                regular,
                SurveyPromptView.Body,
                23,
                132,
                74,
                580,
                36
            );
            var emblem = ImageChild(card, "SurveyIcon", icon);
            TopLeft(emblem.rectTransform, 24, 44, 92, 92);
            var buttons = (RectTransform)card.Find("Buttons");
            buttons.anchorMin = Vector2.zero;
            buttons.anchorMax = Vector2.one;
            buttons.offsetMin = buttons.offsetMax = Vector2.zero;
            var open = buttons.Find("OpenButton").GetComponent<Button>();
            for (int i = open.transform.childCount - 1; i >= 0; i--)
            {
                Transform part = open.transform.GetChild(i);
                if (part.name != "Label")
                    Object.DestroyImmediate(part.gameObject);
            }
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = Vector2.one;
            openRect.anchoredPosition = new Vector2(-38, -120);
            openRect.sizeDelta = new Vector2(180, 58);
            var face = open.GetComponent<Image>();
            face.material = null;
            face.sprite = action;
            face.type = Image.Type.Sliced;
            face.color = Color.white;
            open.targetGraphic = face;
            open.transition = Selectable.Transition.ColorTint;
            TMP_Text label = open.GetComponentInChildren<TMP_Text>(true);
            label.font = bold;
            label.text = SurveyPromptView.OpenLabel;
            label.fontSize = 28;
            label.color = new Color32(255, 252, 241, 255);
            label.alignment = TextAlignmentOptions.Midline;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(14, 7);
            label.rectTransform.offsetMax = new Vector2(-14, -3);
            label.raycastTarget = false;
            Transform duplicateClose = buttons.Find("CloseButton");
            if (duplicateClose != null)
                Object.DestroyImmediate(duplicateClose.gameObject);
            Transform close = card.Find("CloseButton");
            UiCloseButton.Corner(close, 46);
            BuilderTools.SetReference(
                layer.GetComponent<SurveyPromptView>(),
                "closeButton",
                close.GetComponent<Button>()
            );

            var entry = new GameObject(
                "EntryButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            Transform old = layer.Find("EntryButton");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            entry.transform.SetParent(layer, false);
            PlaceEntry((RectTransform)entry.transform);
            Image hit = entry.GetComponent<Image>();
            hit.color = Color.clear;
            Button click = entry.GetComponent<Button>();
            click.targetGraphic = hit;
            click.transition = Selectable.Transition.None;
            Image picture = ImageChild(entry.transform, "Clipboard", icon);
            picture.rectTransform.anchorMin =
                picture.rectTransform.anchorMax =
                picture.rectTransform.pivot =
                    Vector2.one * .5f;
            picture.rectTransform.anchoredPosition = Vector2.zero;
            OpticalIconPlacement.Apply(picture, HudTopBarLayout.Size);
            PlaceEntryOnHudLayer(entry);
            entry.SetActive(false);
            BuilderTools.SetReference(layer.GetComponent<SurveyPromptView>(), "entryButton", click);
            card.gameObject.SetActive(false);
        }

        private static Image ImageChild(Transform parent, string name, Sprite sprite)
        {
            Transform old = parent.Find(name);
            var image =
                old != null
                    ? old.GetComponent<Image>()
                    : new GameObject(
                        name,
                        typeof(RectTransform),
                        typeof(Image)
                    ).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Text(
            TMP_Text label,
            TMP_FontAsset font,
            string value,
            float size,
            float x,
            float y,
            float width,
            float height
        )
        {
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.color = Ink;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            TopLeft(label.rectTransform, x, y, width, height);
        }

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Texture2D Clipboard()
        {
            var a = new UiArtCanvas(128, 128);
            a.SoftShadow(29, 13, 100, 111, 13, 6, new Color(0.25f, .12f, .04f, .22f));
            a.RoundRect(28, 19, 99, 115, 13, new Color32(215, 104, 68, 255));
            a.RoundRect(28, 25, 99, 118, 13, new Color32(244, 147, 102, 255));
            a.RoundRect(37, 32, 90, 106, 7, new Color32(255, 244, 214, 255));
            a.RoundRect(46, 103, 82, 123, 8, new Color32(247, 163, 118, 255));
            a.Circle(64, 119, 6, new Color32(255, 244, 214, 255));
            Color line = new Color32(233, 145, 96, 255);
            a.Line(4, line, new Vector2(47, 88), new Vector2(80, 88));
            a.Line(4, line, new Vector2(47, 76), new Vector2(76, 76));
            a.Line(5, line, new Vector2(49, 52), new Vector2(60, 42), new Vector2(79, 62));
            return a.ToTexture();
        }

        private static Texture2D InvitationPanel()
        {
            var a = new UiArtCanvas(256, 256);
            a.SoftShadow(12, 8, 244, 240, 32, 8, new Color(.28f, .19f, .1f, .2f));
            a.RoundRect(12, 16, 244, 248, 32, new Color32(255, 255, 251, 235));
            a.RoundRect(14, 20, 242, 246, 30, new Color32(255, 247, 231, 222));
            return a.ToTexture();
        }

        public static void BuildAndCapture()
        {
            Build();
            SurveyHudPreview.Capture();
        }

        private static Texture2D Participate()
        {
            var a = new UiArtCanvas(220, 80);
            a.SoftShadow(8, 6, 212, 70, 32, 6, new Color(.3f, .16f, .06f, .22f));
            a.RoundRect(8, 7, 212, 70, 31, new Color32(204, 106, 73, 255));
            a.RoundRect(8, 13, 212, 75, 31, new Color32(241, 149, 111, 255));
            return a.ToTexture();
        }

        private static Sprite SaveArt(string name, Texture2D texture, Vector4 border)
        {
            string path = ArtPath + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
