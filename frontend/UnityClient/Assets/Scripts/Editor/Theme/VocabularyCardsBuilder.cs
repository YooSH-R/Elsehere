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
        private const string SolidCardPath = "Assets/Art/UI/Theme/Jelly/Card.png";
        private const string BodyFontPath = "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset";
        private const string BoldFontPath = "Assets/Art/UI/Fonts/Pretendard-Bold SDF.asset";

        // 단어장 글은 피드백 화면처럼 Pretendard로 쓴다. 테마 본문 글꼴(나눔스퀘어라운드)은 영어 글자 사이가 넓고 획이 가늘어 흐려 보인다.
        // 흐린 글자는 유리 판 위에서도 대비가 4.5:1을 넘도록 테마의 흐린 색보다 조금 진하게 둔다.
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(107, 93, 77, 255);
        private static readonly Color Accent = new Color32(210, 79, 47, 255);
        private const string HeadingFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";

        // 목록 줄의 옅은 흰 카드와, 상세에서 저장한 문장을 담는 따뜻한 색 칸
        private static readonly Color RowCardColor = new Color(1, 1, 1, .62f);
        private static readonly Color SentenceBoxColor = new Color32(255, 244, 230, 255);

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
            Text(panel, "Title", "단어장", 28, 36, 26, 280, 44, UiThemeRole.Title);
            foreach (string old in new[] { "Hint", "List", "Buttons", "MemoRow" })
            {
                Transform child = panel.Find(old);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            Button close = IconButton(panel, "CloseButton", UiIcon.Close, 920, 24);
            UiCloseButton.Corner(close.transform, UiCloseButton.WindowSize);
            // 외운 단어는 목록에서 정리하고, 머리줄 닫기 왼쪽의 [외운 표현도 보기]로 다시 본다(기능 명세 F-35).
            Button showLearned = TextButton(
                panel,
                "ShowLearnedButton",
                "외운 표현도 보기",
                UiIcon.None,
                UiTone.Neutral,
                718,
                24,
                190,
                44
            );
            RectTransform content = Rect(panel, "Content");
            Position(content, 0, 96, 1000, 476);
            // 목록을 최근 저장순과 알파벳순으로 오가고, 알파벳순일 때 목록 맨 왼쪽의 A~Z로 그 글자 묶음에 간다.
            Button sort = TextButton(
                panel,
                "SortButton",
                "알파벳순으로 보기",
                UiIcon.None,
                UiTone.Neutral,
                516,
                24,
                190,
                44
            );
            RectTransform letters = LetterIndex(content);
            // 왼쪽 단어 목록. 단어가 많으면 마우스 휠·끌기로 스크롤한다. 고른 줄의 그림자가 잘리지 않게 보이는 곳을 줄보다 조금 넓게 둔다.
            RectTransform entries = Rect(content, "Entries");
            Position(entries, 40, 0, 300, 360);
            foreach (string old in new[] { "Entry0", "Entry1", "Entry2", "Entry3" })
            {
                Transform child = entries.Find(old);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            // 줄 사이 빈 곳에서도 휠이 먹도록 보이지 않는 바탕을 깐다.
            Image listArea = BuilderTools.GetOrAddComponent<Image>(entries.gameObject);
            listArea.color = Color.clear;
            BuilderTools.GetOrAddComponent<RectMask2D>(entries.gameObject);
            RectTransform rows = Rect(entries, "Rows");
            rows.anchorMin = new Vector2(0, 1);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(.5f, 1);
            rows.anchoredPosition = Vector2.zero;
            rows.sizeDelta = new Vector2(0, rows.sizeDelta.y);
            var column = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(rows.gameObject);
            column.padding = new RectOffset(10, 10, 6, 14);
            column.spacing = 14;
            column.childControlWidth = column.childForceExpandWidth = true;
            column.childControlHeight = column.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(rows.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            var list = BuilderTools.GetOrAddComponent<ScrollRect>(entries.gameObject);
            list.content = rows;
            list.viewport = entries;
            list.horizontal = false;
            list.vertical = true;
            list.movementType = ScrollRect.MovementType.Clamped;
            list.scrollSensitivity = 30;
            list.verticalScrollbar = ListScrollbar(content);
            list.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            // 줄 원본. 숨겨 두고 단어마다 복사해 쓴다.
            Button template = TextButton(
                rows,
                "EntryTemplate",
                "",
                UiIcon.None,
                UiTone.Neutral,
                0,
                0,
                280,
                76
            );
            Object.DestroyImmediate(template.GetComponent<ThemedElement>());
            template.GetComponent<Image>().color = Color.clear;
            Object.DestroyImmediate(template.transform.Find("Outline").gameObject);
            Object.DestroyImmediate(template.transform.Find("Icon").gameObject);
            // 줄마다 옅은 흰 카드를 깔아 단어 사이 경계를 보이고, 고른 줄은 그 위에 강조색 카드를 덮는다.
            RectTransform mark = Rect(template.transform, "Selection");
            Stretch(mark);
            mark.SetAsFirstSibling();
            Skin(mark, UiThemeRole.PillButton, UiTone.Primary);
            SetThemeFlag(mark, "selection");
            mark.GetComponent<Image>().raycastTarget = false;
            Image rowCard = SolidCard(Rect(template.transform, "Card"), RowCardColor);
            Stretch(rowCard.rectTransform);
            rowCard.transform.SetAsFirstSibling();
            // 영어 단어는 굵게, 뜻은 그 아래 흐린 글자로 나눠 무엇이 단어이고 무엇이 뜻인지 보이게 한다.
            TMP_Text label = template.transform.Find("Label").GetComponent<TMP_Text>();
            label.gameObject.SetActive(true);
            Position(label.rectTransform, 18, 12, 244, 28);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.fontSize = 20;
            label.lineSpacing = 0;
            label.margin = Vector4.zero;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Letter(label, UiThemeRole.Body, bold: true);
            Text(template.transform, "Meaning", "", 17, 18, 42, 244, 24, UiThemeRole.Muted);
            template.gameObject.SetActive(false);
            // 목록 넘기기는 목록에서 고른 단어를 옮기므로 목록 바로 아래 가운데에 둔다.
            Button previous = IconButton(content, "PreviousButton", UiIcon.Previous, 98, 372);
            TMP_Text page = Text(content, "Page", "", 18, 152, 372, 76, 44, UiThemeRole.Body);
            page.alignment = TextAlignmentOptions.Midline;
            Button next = IconButton(content, "NextButton", UiIcon.Next, 238, 372);
            // 고른 단어의 상세(단어·뜻·[외웠어요]·[삭제]·저장한 문장·메모)를 한 장의 카드 위에 모아 목록과 구분한다.
            RectTransform detailsCard = Rect(content, "DetailsCard");
            Position(detailsCard, 344, -16, 644, 410);
            detailsCard.SetAsFirstSibling();
            SolidCard(detailsCard, Color.white);
            RectTransform details = Rect(content, "Details");
            Position(details, 366, 0, 600, 400);
            TMP_Text word = Text(details, "Word", "", 34, 0, 0, 380, 46, UiThemeRole.Title);
            TMP_Text meaning = Text(details, "Meaning", "", 22, 0, 52, 600, 34, UiThemeRole.Accent);
            // 저장한 문장·메모는 피드백 화면처럼 코랄 막대 제목으로 구역을 나누고, 저장한 문장은 따뜻한 색 칸에 담는다.
            RectTransform context = Rect(details, "Context");
            Position(context, 0, 104, 600, 166);
            TMP_Text contextTitle = Text(
                context,
                "Title",
                "저장한 문장",
                18,
                0,
                0,
                600,
                26,
                UiThemeRole.Accent
            );
            SectionHeading(contextTitle);
            Image sentenceBox = SolidCard(Rect(context, "SentenceBox"), SentenceBoxColor);
            Position(sentenceBox.rectTransform, 0, 34, 600, 128);
            sentenceBox.transform.SetAsFirstSibling();
            // 문장 아래에 문장 뜻을 둔다(화면 18). 긴 대사는 두 줄 안에 들도록 글자를 줄인다.
            TMP_Text sentence = Text(
                context,
                "Sentence",
                "",
                21,
                18,
                48,
                564,
                56,
                UiThemeRole.Body
            );
            sentence.enableAutoSizing = true;
            sentence.fontSizeMin = 16;
            sentence.fontSizeMax = 21;
            TMP_Text sentenceMeaning = Text(
                context,
                "SentenceMeaning",
                "",
                18,
                18,
                108,
                564,
                36,
                UiThemeRole.Muted
            );
            TMP_Text memoTitle = Text(
                details,
                "MemoTitle",
                "메모",
                18,
                0,
                282,
                600,
                26,
                UiThemeRole.Accent
            );
            SectionHeading(memoTitle);
            TMP_InputField memo = UiBuilderTools.FindOrCreateInputField(
                details,
                "MemoInput",
                UiBuilderTools.RegularFont(),
                "기억하고 싶은 내용을 남겨 보세요"
            );
            Position((RectTransform)memo.transform, 0, 316, 492, 70);
            memo.lineType = TMP_InputField.LineType.MultiLineNewline;
            memo.textComponent.fontSize = 19;
            memo.textComponent.margin = new Vector4(12, 8, 12, 8);
            Skin(memo.transform, UiThemeRole.Input);
            Letter(memo.textComponent, UiThemeRole.Body);
            var placeholder = (TMP_Text)memo.placeholder;
            placeholder.fontSize = 19;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.margin = memo.textComponent.margin;
            Letter(placeholder, UiThemeRole.Muted);
            // [저장]은 메모 입력칸과 같은 줄 오른쪽에 둬 무엇을 저장하는지 바로 보이게 한다.
            Button save = TextButton(
                details,
                "MemoSaveButton",
                "저장",
                UiIcon.None,
                UiTone.Primary,
                508,
                330,
                92,
                42
            );
            // [외웠어요]·[삭제]는 고른 단어에 하는 일이라 넘기기 줄이 아니라 단어 제목 줄 오른쪽에 둔다.
            Button learned = TextButton(
                content,
                "LearnedButton",
                "외웠어요",
                UiIcon.Check,
                UiTone.Positive,
                758,
                0,
                152,
                44
            );
            Button delete = IconButton(content, "DeleteButton", UiIcon.Delete, 922, 0);
            TMP_Text status = Text(panel, "Status", "", 18, 36, 574, 918, 30, UiThemeRole.Muted);
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
            BuilderTools.SetReference(view, "emptyTitleText", emptyTitle);
            BuilderTools.SetReference(view, "emptyHintText", emptyHint);
            BuilderTools.SetReference(view, "showLearnedButton", showLearned);
            BuilderTools.SetReference(view, "sortButton", sort);
            BuilderTools.SetReference(
                view,
                "sortButtonLabel",
                sort.transform.Find("Label").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(view, "letterIndex", letters);
            BuilderTools.SetReference(
                view,
                "showLearnedButtonLabel",
                showLearned.transform.Find("Label").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(view, "loadState", load.gameObject);
            BuilderTools.SetReference(view, "loadStateTitle", loadTitle);
            BuilderTools.SetReference(view, "retryButton", retry);
            BuilderTools.SetReference(view, "wordText", word);
            BuilderTools.SetReference(view, "meaningText", meaning);
            BuilderTools.SetReference(view, "contextText", sentence);
            BuilderTools.SetReference(view, "contextTitleText", contextTitle);
            BuilderTools.SetReference(view, "contextMeaningText", sentenceMeaning);
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
            BuilderTools.SetReference(view, "entryList", list);
            BuilderTools.SetReference(view, "entryTemplate", template);
            var fields = new SerializedObject(view);
            SetArray(
                fields.FindProperty("memoParts"),
                new[]
                {
                    memoTitle.rectTransform,
                    (RectTransform)memo.transform,
                    (RectTransform)save.transform,
                }
            );
            fields.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        // 단어 카드 버튼 묶음: 카드 안쪽 너비(440 - 좌우 여백 32×2), 버튼 높이, 버튼 사이
        private const float ActionWidth = 376;
        private const float ActionHeight = 44;
        private const float ActionGap = 12;

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
            // 버튼은 카드 안쪽 너비를 한 덩어리로 채운다. [저장]·[발음]이 한 줄을 반씩, 그 아래 [알아요]가 같은 양 끝으로 한 줄.
            Transform pinHint = panel.Find("PinHint");
            if (pinHint != null)
                Object.DestroyImmediate(pinHint.gameObject);
            Transform row = panel.Find("WordActions");
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = ActionGap;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            BuilderTools.GetOrAddComponent<LayoutElement>(row.gameObject).preferredWidth =
                ActionWidth;
            float half = (ActionWidth - ActionGap) / 2;
            ConfigureButton(
                row.Find("SaveButton"),
                "저장",
                UiIcon.Save,
                UiTone.Primary,
                half,
                ActionHeight
            );
            ConfigureButton(
                row.Find("PronunciationButton"),
                "발음",
                UiIcon.Replay,
                UiTone.Neutral,
                half,
                ActionHeight
            );
            Transform actions = panel.Find("Actions");
            var actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
            actionLayout.childControlWidth = true;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            BuilderTools.GetOrAddComponent<LayoutElement>(actions.gameObject).preferredWidth =
                ActionWidth;
            Transform hide = actions.Find("HideButton");
            ConfigureButton(
                hide,
                "숨기기",
                UiIcon.Check,
                UiTone.Neutral,
                ActionWidth,
                ActionHeight
            );
            SetThemeFlag(hide, "quiet", false);
            hide.Find("Outline").gameObject.SetActive(true);
            Transform closeSource = panel.Find("CloseButton");
            if (closeSource == null)
                closeSource = actions.Find("CloseButton");
            Transform duplicate = actions.Find("CloseButton");
            if (duplicate != null && duplicate != closeSource)
                Object.DestroyImmediate(duplicate.gameObject);
            Button close = closeSource.GetComponent<Button>();
            close.transform.SetParent(panel, false);
            BuilderTools.GetOrAddComponent<LayoutElement>(close.gameObject).ignoreLayout = true;
            UiCloseButton.Corner(close.transform, UiCloseButton.CardSize);
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

        private static void SetThemeFlag(Component target, string propertyName, bool value = true)
        {
            var element = target.GetComponent<ThemedElement>();
            var fields = new SerializedObject(element);
            fields.FindProperty(propertyName).boolValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
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
            Letter(label, role);
            return label;
        }

        // 제목은 테마의 둥근 제목 글꼴을 그대로 쓰고, 나머지 글은 Pretendard와 정한 색으로 고정한다. 뜻(강조)은 굵게 쓴다.
        private static void Letter(TMP_Text label, UiThemeRole role, bool bold = false)
        {
            if (role == UiThemeRole.Title)
            {
                Skin(label.transform, role);
                return;
            }
            RemoveTheme(label);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                bold || role == UiThemeRole.Accent ? BoldFontPath : BodyFontPath
            );
            label.fontStyle = FontStyles.Normal;
            label.color =
                role == UiThemeRole.Muted ? Muted
                : role == UiThemeRole.Accent ? Accent
                : Ink;
        }

        // 알파벳순일 때 목록 맨 왼쪽의 A~Z. 글자마다 버튼이고, 그 글자로 시작하는 단어가 없으면 흐리게 보인다.
        private static RectTransform LetterIndex(RectTransform parent)
        {
            RectTransform column = Rect(parent, "Letters");
            Position(column, 8, 0, 28, 416);
            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                string name = letter.ToString();
                TMP_Text text = Text(
                    column,
                    name,
                    name,
                    14,
                    0,
                    (letter - 'A') * 16,
                    28,
                    16,
                    UiThemeRole.Muted
                );
                Letter(text, UiThemeRole.Muted, bold: true);
                text.alignment = TextAlignmentOptions.Center;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = true;
                var button = BuilderTools.GetOrAddComponent<Button>(text.gameObject);
                button.targetGraphic = text;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock colors = button.colors;
                colors.disabledColor = new Color(1, 1, 1, .3f);
                button.colors = colors;
            }
            return column;
        }

        // 목록과 상세 카드 사이의 가는 스크롤 막대. 목록이 넘칠 때만 보인다(ScrollRect의 AutoHide).
        private static Scrollbar ListScrollbar(RectTransform parent)
        {
            RectTransform bar = Rect(parent, "EntriesScrollbar");
            Position(bar, 333, 6, 5, 340);
            Image track = BuilderTools.GetOrAddComponent<Image>(bar.gameObject);
            track.color = new Color(Muted.r, Muted.g, Muted.b, .12f);
            RectTransform area = Rect(bar, "SlidingArea");
            Stretch(area);
            RectTransform handle = Rect(area, "Handle");
            Stretch(handle);
            Image thumb = BuilderTools.GetOrAddComponent<Image>(handle.gameObject);
            thumb.color = new Color(Muted.r, Muted.g, Muted.b, .55f);
            var scrollbar = BuilderTools.GetOrAddComponent<Scrollbar>(bar.gameObject);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = thumb;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        // 구역 제목: 피드백 화면처럼 왼쪽 코랄 막대와 둥근 제목 글꼴의 코랄 글자
        private static void SectionHeading(TMP_Text heading)
        {
            heading.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadingFontPath);
            heading.color = Accent;
            heading.margin = new Vector4(14, 0, 0, 0);
            Image bar = BuilderTools.GetOrAddComponent<Image>(
                Rect(heading.transform, "Bar").gameObject
            );
            Position(bar.rectTransform, 0, 3, 5, 18);
            // 작은 막대라 둥근 그림을 쓰면 모서리가 찌그러지므로 그림 없이 반듯하게 칠한다.
            bar.sprite = null;
            bar.type = Image.Type.Simple;
            bar.color = Accent;
            bar.raycastTarget = false;
        }

        // 테마의 유리 그림은 뒤 장면이 비쳐 글이 흐려 보이므로, 튜토리얼 말풍선과 같은 불투명한 카드 그림을 깐다.
        private static Image SolidCard(RectTransform card, Color color)
        {
            RemoveTheme(card);
            Image image = BuilderTools.GetOrAddComponent<Image>(card.gameObject);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SolidCardPath);
            image.type = Image.Type.Sliced;
            image.material = null;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void RemoveTheme(Component target)
        {
            foreach (
                Component themed in new Component[]
                {
                    target.GetComponent<ThemedElement>(),
                    target.GetComponent<SurfaceBleed>(),
                }
            )
            {
                if (themed != null)
                    Object.DestroyImmediate(themed);
            }
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
            // 이름 없는 버튼은 숨긴 이름의 글자 크기로 아이콘 크기를 정한다(PillContentLayout).
            label.fontSize = caption.Length == 0 ? Mathf.Round(height * .6f) : 18;
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
