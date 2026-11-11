using System.IO;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    public static class DialogueSuggestionsBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string HideIconPath = "Assets/Art/UI/Theme/Icons/HideHint.png";

        [MenuItem("Tools/A305/Dialogue/Apply Fixed Suggestion Cards")]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, SetUp);
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root)
        {
            var view = root.GetComponent<DialoguePanelView>();
            Transform tools = root.transform.Find("Panel/Conversation/ToolRow");
            Transform existing = root.transform.Find("Suggestions");
            if (existing != null)
            {
                Transform redundant = tools.Find("HintButton");
                if (redundant != null)
                    Object.DestroyImmediate(redundant.gameObject);
                BuilderTools.SetReference(
                    view,
                    "suggestions",
                    existing.GetComponent<DialogueSuggestionsView>()
                );
                BuilderTools.SetReference(
                    view,
                    "hintButton",
                    existing.Find("HintCard/HintCover/HintButton").GetComponent<Button>()
                );
                SetTheme(
                    existing.Find("HintCard/HintCover/HintButton"),
                    UiThemeRole.PillButton,
                    UiTone.Primary
                );
                ReadingText(
                    existing.Find("WordsCard/Viewport/SuggestedWordsBody").GetComponent<TMP_Text>()
                );
                ReadingText(
                    existing.Find("HintCard/Viewport/HintSentence").GetComponent<TMP_Text>()
                );
                return;
            }
            RectTransform group = Rect(root.transform, "Suggestions");
            var cards = group.gameObject.AddComponent<DialogueSuggestionsView>();
            Transform source = root.transform.Find("HelpPanel/Viewport/Content/WordsCard");
            RectTransform words = Card(group, "WordsCard", source, "단어");
            RectTransform hint = Card(group, "HintCard", source, "추천 문장");
            Scrollbar scrollbar = root
                .transform.Find("Panel/Conversation/History")
                .GetComponent<ScrollRect>()
                .verticalScrollbar;
            TMP_Text wordsText = ScrollText(words, "SuggestedWordsBody", scrollbar, out _);
            TMP_Text sentence = ScrollText(
                hint,
                "HintSentence",
                scrollbar,
                out ScrollRect hintScroll
            );
            sentence.richText = true;
            // 글자와 버튼을 구분하고, 문장 영역 전체를 누를 수 있게 한다.
            RectTransform useTarget = Rect(sentence.transform.parent, "UseHintButton");
            Fill(useTarget, Vector2.zero, Vector2.zero);
            useTarget.gameObject.AddComponent<Image>().color = Color.clear;
            Button useButton = useTarget.gameObject.AddComponent<Button>();
            useButton.transition = Selectable.Transition.None;

            RectTransform cover = Rect(hint, "HintCover");
            Fill(cover, new Vector2(24, 20), new Vector2(-24, -58));
            for (int i = 0; i < 2; i++)
            {
                RectTransform line = Rect(cover, "MaskedLine" + i);
                line.anchorMin = new Vector2(.08f, .6f - i * .28f);
                line.anchorMax = new Vector2(i == 0 ? .92f : .72f, .6f - i * .28f);
                line.sizeDelta = new Vector2(0, 8);
                var mask = line.gameObject.AddComponent<Image>();
                mask.sprite = source.GetComponent<Image>().sprite;
                mask.type = Image.Type.Sliced;
                mask.color = new Color(.45f, .42f, .38f, .12f);
                mask.raycastTarget = false;
            }
            Transform hintButton = tools.Find("HintButton");
            hintButton.SetParent(cover, false);
            var buttonRect = (RectTransform)hintButton;
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(.5f, .5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(100, 44);
            var size = hintButton.GetComponent<LayoutElement>();
            size.ignoreLayout = true;
            hintButton.GetComponent<Image>().pixelsPerUnitMultiplier = 2;
            SetTheme(hintButton, UiThemeRole.PillButton, UiTone.Primary);
            TMP_Text caption = hintButton.Find("Label").GetComponent<TMP_Text>();
            caption.text = "힌트";
            caption.fontSize = 23;
            caption.alignment = TextAlignmentOptions.Midline;
            hintButton.Find("Icon")?.gameObject.SetActive(false);
            Button hide = HideButton(hint);
            BuilderTools.SetReference(cards, "wordsCard", words);
            BuilderTools.SetReference(cards, "hintCard", hint);
            BuilderTools.SetReference(cards, "wordsText", wordsText);
            BuilderTools.SetReference(cards, "hintText", sentence);
            BuilderTools.SetReference(cards, "hintCover", cover.gameObject);
            BuilderTools.SetReference(cards, "hintScroll", hintScroll);
            BuilderTools.SetReference(cards, "hideButton", hide);
            BuilderTools.SetReference(cards, "useButton", useButton);
            BuilderTools.SetReference(view, "suggestions", cards);
            cards.SetBounds(new Vector2(-40, -128), 390, 440);
            cards.Show("", true, false, null, false);
            group.gameObject.SetActive(false);
        }

        private static RectTransform Card(
            Transform parent,
            string name,
            Transform source,
            string title
        )
        {
            RectTransform card = Rect(parent, name);
            var image = card.gameObject.AddComponent<Image>();
            EditorUtility.CopySerialized(source.GetComponent<Image>(), image);
            var theme = card.gameObject.AddComponent<ThemedElement>();
            EditorUtility.CopySerialized(source.GetComponent<ThemedElement>(), theme);
            theme.ApplyCurrent();
            TMP_Text heading = Text(card, "Title", title, 26);
            heading.rectTransform.anchorMin = new Vector2(0, 1);
            heading.rectTransform.anchorMax = Vector2.one;
            heading.rectTransform.offsetMin = new Vector2(26, -54);
            heading.rectTransform.offsetMax = new Vector2(-26, -22);
            SetTheme(heading.transform, UiThemeRole.Title);
            return card;
        }

        private static TMP_Text ScrollText(
            RectTransform card,
            string name,
            Scrollbar template,
            out ScrollRect scroll
        )
        {
            RectTransform viewport = Rect(card, "Viewport");
            Fill(viewport, new Vector2(26, 22), new Vector2(-26, -64));
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text body = Text(viewport, name, "", 24);
            body.alignment = TextAlignmentOptions.TopLeft;
            ReadingText(body);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            var rect = body.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            SetTheme(body.transform, UiThemeRole.Body);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            if (template != null)
            {
                Scrollbar bar = Object.Instantiate(template, card);
                bar.name = "Scrollbar";
                var barRect = (RectTransform)bar.transform;
                barRect.anchorMin = new Vector2(1, 0);
                barRect.anchorMax = Vector2.one;
                barRect.offsetMin = new Vector2(-12, 22);
                barRect.offsetMax = new Vector2(-7, -64);
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            return body;
        }

        private static Button HideButton(Transform parent)
        {
            if (!File.Exists(HideIconPath))
            {
                var art = new UiArtCanvas(64, 64);
                var upper = new Vector2[25];
                var lower = new Vector2[25];
                for (int i = 0; i < upper.Length; i++)
                {
                    float x = 10 + i * 44f / 24;
                    float y = 12 * Mathf.Sin(Mathf.PI * i / 24);
                    upper[i] = new Vector2(x, 32 + y);
                    lower[i] = new Vector2(x, 32 - y);
                }
                art.Line(3, Color.white, upper);
                art.Line(3, Color.white, lower);
                art.Ring(32, 32, 7, 4, Color.white);
                art.Line(4, Color.white, new Vector2(11, 12), new Vector2(53, 52));
                Texture2D texture = art.ToTexture();
                File.WriteAllBytes(HideIconPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(HideIconPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(HideIconPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            RectTransform root = Rect(parent, "HideHintButton");
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-14, -14);
            root.sizeDelta = new Vector2(44, 44);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            RectTransform icon = Rect(root, "Icon");
            Fill(icon, new Vector2(8, 8), new Vector2(-8, -8));
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HideIconPath);
            image.color = new Color32(112, 106, 95, 255);
            image.raycastTarget = false;
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, string content, float size)
        {
            TMP_Text text = UiBuilderTools.FindOrCreateText(
                parent,
                name,
                UiBuilderTools.RegularFont(),
                size,
                Color.black,
                content
            );
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            return text;
        }

        private static void ReadingText(TMP_Text text)
        {
            text.richText = true;
            text.enableAutoSizing = false;
            text.fontSize = 24;
            text.lineSpacing = -6;
            text.paragraphSpacing = 0;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            return result;
        }

        private static void Fill(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static void SetTheme(
            Transform target,
            UiThemeRole role,
            UiTone tone = UiTone.Neutral
        )
        {
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(
                    "Assets/Data/Theme/UiThemeCatalog.asset"
                );
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.FindProperty("tone").enumValueIndex = (int)tone;
            fields.FindProperty("icon").enumValueIndex = (int)UiIcon.None;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
        }
    }
}
