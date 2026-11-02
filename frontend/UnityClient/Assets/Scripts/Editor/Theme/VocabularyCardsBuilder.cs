using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Theme
{
    /// <summary>승인한 사물 카드·단어장 배치를 기존 세 테마에 연결한다. 공간 씬은 수정하지 않는다.</summary>
    public static class VocabularyCardsBuilder
    {
        private const string BookPath = "Assets/Prefabs/Learning/VocabularyScreen.prefab";
        private const string TooltipPath = "Assets/Prefabs/ObjectHover/WordTooltip.prefab";

        [MenuItem("Tools/A305/UI/Apply Vocabulary Cards")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdateOrCreatePrefab(BookPath, "VocabularyScreen", ApplyVocabulary);
            BuilderTools.UpdatePrefabIfExists(TooltipPath, root => ApplyTooltip(root.transform));
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }

        public static void ApplyVocabulary(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, 135, true);
            var view = BuilderTools.GetOrAddComponent<VocabularyView>(root);
            RectTransform panel = Rect(root.transform, "Panel");
            RemoveLayout(panel);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1000, 620);
            Skin(panel, UiThemeRole.Panel);
            Text(panel, "Title", "단어장", 28, 36, 26, 500, 44, UiThemeRole.Title);
            foreach (string old in new[] { "Hint", "List", "Buttons", "MemoRow" })
            {
                Transform child = panel.Find(old);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            Button close = IconButton(panel, "CloseButton", UiIcon.Close, 920, 24);
            RectTransform content = Rect(panel, "Content");
            Position(content, 0, 96, 1000, 476);
            RectTransform entries = Rect(content, "Entries");
            Position(entries, 36, 0, 280, 376);
            var buttons = new Button[4];
            var labels = new TMP_Text[4];
            var marks = new GameObject[4];
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = TextButton(
                    entries,
                    "Entry" + index,
                    "",
                    UiIcon.None,
                    UiTone.Neutral,
                    0,
                    index * 90,
                    280,
                    76
                );
                // 목록 바탕은 선택한 줄에만 둔다. 나머지는 텍스트와 여백으로 구분한다.
                Object.DestroyImmediate(button.GetComponent<ThemedElement>());
                button.GetComponent<Image>().color = Color.clear;
                Object.DestroyImmediate(button.transform.Find("Outline").gameObject);
                Object.DestroyImmediate(button.transform.Find("Icon").gameObject);
                RectTransform mark = Rect(button.transform, "Selection");
                Stretch(mark);
                mark.SetAsFirstSibling();
                Skin(mark, UiThemeRole.PillButton, UiTone.Primary);
                SetThemeFlag(mark, "selection");
                mark.GetComponent<Image>().raycastTarget = false;
                TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();
                label.gameObject.SetActive(true);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSize = 20;
                label.lineSpacing = 6;
                label.margin = new Vector4(18, 4, 18, 4);
                Skin(label.transform, UiThemeRole.Body);
                buttons[index] = button;
                labels[index] = label;
                marks[index] = mark.gameObject;
            }
            RectTransform details = Rect(content, "Details");
            Position(details, 354, 0, 600, 400);
            TMP_Text word = Text(details, "Word", "", 28, 0, 0, 600, 42, UiThemeRole.Title);
            TMP_Text meaning = Text(details, "Meaning", "", 22, 0, 48, 600, 48, UiThemeRole.Accent);
            RectTransform context = Rect(details, "Context");
            Position(context, 0, 112, 600, 142);
            TMP_Text contextTitle = Text(
                context,
                "Title",
                "저장한 문장",
                18,
                0,
                0,
                600,
                28,
                UiThemeRole.Muted
            );
            TMP_Text sentence = Text(
                context,
                "Sentence",
                "",
                21,
                0,
                38,
                600,
                100,
                UiThemeRole.Body
            );
            Text(details, "MemoTitle", "메모", 18, 0, 266, 600, 28, UiThemeRole.Muted);
            TMP_InputField memo = UiBuilderTools.FindOrCreateInputField(
                details,
                "MemoInput",
                UiBuilderTools.RegularFont(),
                "기억하고 싶은 내용을 남겨 보세요"
            );
            Position((RectTransform)memo.transform, 0, 302, 600, 76);
            memo.lineType = TMP_InputField.LineType.MultiLineNewline;
            memo.textComponent.fontSize = 19;
            memo.textComponent.margin = new Vector4(12, 8, 12, 8);
            Skin(memo.transform, UiThemeRole.Input);
            Skin(memo.textComponent.transform, UiThemeRole.Body);
            var placeholder = (TMP_Text)memo.placeholder;
            placeholder.fontSize = 19;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.margin = memo.textComponent.margin;
            Skin(placeholder.transform, UiThemeRole.Muted);
            Button save = TextButton(
                details,
                "MemoSaveButton",
                "저장",
                UiIcon.None,
                UiTone.Primary,
                508,
                390,
                92,
                42
            );
            Button previous = IconButton(content, "PreviousButton", UiIcon.Previous, 354, 434);
            TMP_Text page = Text(content, "Page", "", 18, 408, 434, 76, 44, UiThemeRole.Body);
            page.alignment = TextAlignmentOptions.Midline;
            Button next = IconButton(content, "NextButton", UiIcon.Next, 494, 434);
            Button learned = TextButton(
                content,
                "LearnedButton",
                "외웠어요",
                UiIcon.Check,
                UiTone.Positive,
                746,
                434,
                152,
                44
            );
            Button delete = IconButton(content, "DeleteButton", UiIcon.Delete, 910, 434);
            TMP_Text status = Text(panel, "Status", "", 18, 36, 574, 918, 30, UiThemeRole.Accent);
            RectTransform empty = Rect(panel, "EmptyState");
            Position(empty, 100, 188, 800, 270);
            RectTransform bookIcon = Rect(empty, "Book");
            Position(bookIcon, 368, 12, 64, 64);
            Skin(bookIcon, UiThemeRole.PillButton, UiTone.Neutral, UiIcon.Book);
            SetThemeFlag(bookIcon, "quiet");
            RectTransform icon = Rect(bookIcon, "Icon");
            Stretch(icon);
            BuilderTools.GetOrAddComponent<Image>(icon.gameObject).raycastTarget = false;
            TMP_Text emptyTitle = Text(
                empty,
                "Title",
                "아직 저장한 단어가 없어요",
                24,
                0,
                112,
                800,
                40,
                UiThemeRole.Title
            );
            emptyTitle.alignment = TextAlignmentOptions.Midline;
            TMP_Text emptyHint = Text(
                empty,
                "Hint",
                "사물 카드에서 마음에 드는 단어를 저장해 보세요.",
                19,
                0,
                170,
                800,
                64,
                UiThemeRole.Muted
            );
            emptyHint.alignment = TextAlignmentOptions.Top;
            // 불러오는 중이거나 불러오지 못했을 때 빈 상태와 같은 자리에 보이는 안내와 [다시 시도](S15P31A305-419)
            RectTransform load = Rect(panel, "LoadState");
            Position(load, 100, 188, 800, 270);
            TMP_Text loadTitle = Text(
                load,
                "Title",
                "단어장을 불러오는 중이에요…",
                24,
                0,
                112,
                800,
                40,
                UiThemeRole.Title
            );
            loadTitle.alignment = TextAlignmentOptions.Midline;
            Button retry = TextButton(
                load,
                "RetryButton",
                "다시 시도",
                UiIcon.Retry,
                UiTone.Primary,
                310,
                172,
                180,
                48
            );
            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "content", content.gameObject);
            BuilderTools.SetReference(view, "emptyState", empty.gameObject);
            BuilderTools.SetReference(view, "loadState", load.gameObject);
            BuilderTools.SetReference(view, "loadStateTitle", loadTitle);
            BuilderTools.SetReference(view, "retryButton", retry);
            BuilderTools.SetReference(view, "wordText", word);
            BuilderTools.SetReference(view, "meaningText", meaning);
            BuilderTools.SetReference(view, "contextText", sentence);
            BuilderTools.SetReference(view, "contextTitleText", contextTitle);
            BuilderTools.SetReference(view, "contextSection", context.gameObject);
            BuilderTools.SetReference(view, "pageText", page);
            BuilderTools.SetReference(view, "previousButton", previous);
            BuilderTools.SetReference(view, "nextButton", next);
            BuilderTools.SetReference(view, "learnedButton", learned);
            BuilderTools.SetReference(
                view,
                "learnedButtonLabel",
                learned.transform.Find("Label").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(view, "deleteButton", delete);
            BuilderTools.SetReference(view, "closeButton", close);
            BuilderTools.SetReference(view, "memoInput", memo);
            BuilderTools.SetReference(view, "memoSaveButton", save);
            BuilderTools.SetReference(view, "statusText", status);
            var fields = new SerializedObject(view);
            SetArray(fields.FindProperty("entryButtons"), buttons);
            SetArray(fields.FindProperty("entryTexts"), labels);
            SetArray(fields.FindProperty("selectedMarks"), marks);
            fields.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        public static void ApplyTooltip(Transform root)
        {
            RectTransform panel = (RectTransform)root.Find("Panel");
            Skin(panel, UiThemeRole.Card);
            panel.sizeDelta = new Vector2(440, panel.sizeDelta.y);
            var column = panel.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(32, 32, 28, 28);
            column.spacing = 12;
            column.childForceExpandWidth = false;
            foreach (
                var spec in new[]
                {
                    ("Word", 28f, UiThemeRole.Title),
                    ("Meaning", 22f, UiThemeRole.Accent),
                    ("Example", 18f, UiThemeRole.Muted),
                    ("PinHint", 16f, UiThemeRole.Muted),
                    ("Status", 16f, UiThemeRole.Accent),
                }
            )
            {
                TMP_Text label = panel.Find(spec.Item1).GetComponent<TMP_Text>();
                label.fontSize = spec.Item2;
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.alignment = TextAlignmentOptions.TopLeft;
                label.margin = new Vector4(0, 0, spec.Item1 == "Word" ? 34 : 0, 0);
                BuilderTools.GetOrAddComponent<LayoutElement>(label.gameObject).preferredWidth =
                    376;
                Skin(label.transform, spec.Item3);
            }
            Transform row = panel.Find("WordActions");
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            ConfigureButton(row.Find("SaveButton"), "저장", UiIcon.Save, UiTone.Primary, 134, 44);
            ConfigureButton(
                row.Find("PronunciationButton"),
                "발음",
                UiIcon.Replay,
                UiTone.Neutral,
                134,
                44
            );
            Transform actions = panel.Find("Actions");
            var actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
            actionLayout.childForceExpandWidth = false;
            ConfigureButton(
                actions.Find("HideButton"),
                "알아요 · 호버에서 숨기기",
                UiIcon.Check,
                UiTone.Neutral,
                366,
                40
            );
            Transform hide = actions.Find("HideButton");
            Skin(hide, UiThemeRole.PillButton, UiTone.Neutral, UiIcon.Check);
            SetThemeFlag(hide, "quiet");
            hide.GetComponent<Image>().color = Color.clear;
            hide.Find("Outline").gameObject.SetActive(false);
            TMP_Text hideText = hide.Find("Label").GetComponent<TMP_Text>();
            hideText.alignment = TextAlignmentOptions.MidlineLeft;
            hideText.margin = new Vector4(42, 0, 4, 0);
            Transform closeSource = panel.Find("CloseButton");
            if (closeSource == null)
                closeSource = actions.Find("CloseButton");
            Transform duplicate = actions.Find("CloseButton");
            if (duplicate != null && duplicate != closeSource)
                Object.DestroyImmediate(duplicate.gameObject);
            Button close = closeSource.GetComponent<Button>();
            close.transform.SetParent(panel, false);
            BuilderTools.GetOrAddComponent<LayoutElement>(close.gameObject).ignoreLayout = true;
            ConfigureButton(close.transform, "", UiIcon.Close, UiTone.Neutral, 36, 36);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-20, -20);
            closeRect.sizeDelta = new Vector2(36, 36);
            // X는 별도 배치지만 고정/호버 상태는 기존 Actions와 동일하게 제어한다.
            BuilderTools.SetReference(
                root.GetComponent<A305.ObjectHover.WordTooltipView>(),
                "closeButton",
                close
            );
        }

        private static void SetArray<T>(SerializedProperty property, T[] values)
            where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetThemeFlag(Component target, string propertyName)
        {
            var fields = new SerializedObject(target.GetComponent<ThemedElement>());
            fields.FindProperty(propertyName).boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text Text(
            Transform parent,
            string name,
            string caption,
            float size,
            float x,
            float y,
            float width,
            float height,
            UiThemeRole role
        )
        {
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                parent,
                name,
                UiBuilderTools.RegularFont(),
                size,
                Color.black,
                caption
            );
            Position((RectTransform)label.transform, x, y, width, height);
            label.text = caption;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            Skin(label.transform, role);
            return label;
        }

        private static Button TextButton(
            Transform parent,
            string name,
            string caption,
            UiIcon icon,
            UiTone tone,
            float x,
            float y,
            float width,
            float height
        )
        {
            Button button = UiBuilderTools.FindOrCreateButton(
                parent,
                name,
                caption,
                UiBuilderTools.BoldFont(),
                width
            );
            Position((RectTransform)button.transform, x, y, width, height);
            ConfigureButton(button.transform, caption, icon, tone, width, height);
            return button;
        }

        private static Button IconButton(
            Transform parent,
            string name,
            UiIcon icon,
            float x,
            float y
        ) => TextButton(parent, name, "", icon, UiTone.Neutral, x, y, 44, 44);

        private static void ConfigureButton(
            Transform button,
            string caption,
            UiIcon icon,
            UiTone tone,
            float width,
            float height
        )
        {
            foreach (Shadow shadow in button.GetComponents<Shadow>())
                Object.DestroyImmediate(shadow);
            Transform face = button.Find("Face");
            if (face != null)
                Object.DestroyImmediate(face.gameObject);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.preferredWidth = size.minWidth = width;
            size.preferredHeight = size.minHeight = height;
            button.GetComponent<Image>().pixelsPerUnitMultiplier = 2;
            Skin(button, UiThemeRole.PillButton, tone, icon);
            RectTransform outline = Rect(button, "Outline");
            Stretch(outline);
            outline.SetAsFirstSibling();
            BuilderTools.GetOrAddComponent<Image>(outline.gameObject).raycastTarget = false;
            outline.GetComponent<Image>().pixelsPerUnitMultiplier = 2;
            RectTransform pictogram = Rect(button, "Icon");
            Position(
                pictogram,
                caption.Length == 0 ? (width - 20) / 2 : 12,
                (height - 20) / 2,
                20,
                20
            );
            BuilderTools.GetOrAddComponent<Image>(pictogram.gameObject).raycastTarget = false;
            TMP_Text label = button.Find("Label").GetComponent<TMP_Text>();
            Stretch((RectTransform)label.transform);
            label.text = caption;
            label.gameObject.SetActive(caption.Length > 0);
            label.fontSize = 18;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Midline;
            float textInset = icon == UiIcon.None ? 12 : 32;
            label.margin = new Vector4(textInset, 6, textInset, 6);
            label.transform.SetAsLastSibling();
        }

        private static void Skin(
            Component target,
            UiThemeRole role,
            UiTone tone = UiTone.Neutral,
            UiIcon icon = UiIcon.None
        )
        {
            if (target.GetComponent<TMP_Text>() == null)
                BuilderTools.GetOrAddComponent<Image>(target.gameObject);
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(
                    "Assets/Data/Theme/UiThemeCatalog.asset"
                );
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.FindProperty("tone").enumValueIndex = (int)tone;
            fields.FindProperty("icon").enumValueIndex = (int)icon;
            fields.FindProperty("fixedTheme").boolValue = false;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return (RectTransform)found;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Position(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height
        )
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void RemoveLayout(Transform target)
        {
            foreach (var layout in target.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            foreach (var fitter in target.GetComponents<ContentSizeFitter>())
                Object.DestroyImmediate(fitter);
        }
    }
}
