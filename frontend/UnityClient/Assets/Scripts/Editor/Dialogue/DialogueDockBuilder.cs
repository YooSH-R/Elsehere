using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 상자 없는 그라데이션 대화창과 오른쪽 위 도움 카드를 실제 기능 프리팹에 적용한다.
    /// 화면 아래 전체 폭에 어두운 그라데이션을 깔고, 코랄 이름표·반투명 도구 버튼·흰 대사·반투명 입력칸·코랄 [말하기]를 둔다.
    /// 자리는 DialoguePanelView가 화면 크기에 맞춰 다시 정하고, 여기서는 기준 화면(1920×1080)의 자리를 넣어 둔다.
    /// </summary>
    public static class DialogueDockBuilder
    {
        private const string MenuPath = "Tools/A305/Dialogue/Apply Approved Layout";
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";
        private static readonly Vector2 ReferenceCanvas = new Vector2(1920, 1080);

        // 이름 줄 버튼 높이와 글자 크기. 64px보다 낮아 아이콘 없이 이름만 둔다.
        private const float ToolHeight = DialogueCinemaLayout.NameHeight;
        private const float ToolFontSize = 20;

        // 72px 높이의 입력칸·[보내기]를 양 끝이 둥근 알약·원으로 그리는 9칸 나누기 배율
        private const float RoundEnds = .75f;

        /// <summary>대화창 프리팹에 승인된 배치만 다시 적용한다. 대화창의 기능 구성은 건드리지 않는다.</summary>
        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PanelPrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                ReferenceCanvas,
                DialogueCinemaLayout.MinLinesHeight,
                160
            );
            Transform panel = root.transform.Find("Panel");
            FreeLayout(panel);
            var frame = (RectTransform)panel;
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = new Vector2(1, 0);
            frame.pivot = new Vector2(.5f, 0);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(0, layout.Height);
            Theme(panel, UiThemeRole.Scrim);
            Transform conversation = panel.Find("Conversation");
            FreeLayout(conversation);
            Stretch((RectTransform)conversation);

            NameRow(panel, layout);
            Lines(conversation, layout);
            InputRow(conversation.Find("InputRow"), layout);
            ToolRow(conversation.Find("ToolRow"));
            WordCard(conversation.Find("WordCard"));
            Waiting(conversation.Find("History/Wait"));
            HelpCards(root.transform.Find("HelpPanel"));
        }

        // 코랄 이름표 위의 NPC 이름, 그 옆 호감도, 오른쪽 끝의 [다시 시도]·[대화 종료]
        private static void NameRow(Transform panel, DialogueCinemaLayout layout)
        {
            Transform title = panel.Find("Title");
            RectTransform chip = NameChip(panel, title);
            Place(chip, layout.Name);
            Place(title, layout.Name);
            var name = title.GetComponent<TMP_Text>();
            name.fontSize = 26;
            // 줄 높이 기준(Center)이면 Jua 글자가 이름표 위로 떠 보여, 글자 모양 기준(Midline)으로 가운데에 둔다.
            name.alignment = TextAlignmentOptions.Midline;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.margin = Vector4.zero;
            Theme(title, UiThemeRole.TitleOnDark);

            Transform standing = panel.Find("Standing");
            standing.GetComponent<TMP_Text>().fontSize = 20;
            Theme(standing, UiThemeRole.BodyOnDark);

            Transform buttons = panel.Find("Buttons");
            Row(buttons);
            Place(buttons, new Rect(layout.Tools.xMax - 280, layout.Tools.y, 280, ToolHeight));
            Ghost(buttons.Find("RetryButton"), "다시 시도", UiIcon.Retry, 132);
            Ghost(buttons.Find("EndButton"), "대화 종료", UiIcon.Exit, 132);
        }

        // 이름 글자 뒤에 깔리는 코랄 입체 이름표. 이름 길이에 맞춘 폭은 DialoguePanelView가 정한다.
        private static RectTransform NameChip(Transform panel, Transform title)
        {
            Transform existing = panel.Find("NameChip");
            var chip =
                existing != null
                    ? (RectTransform)existing
                    : new GameObject(
                        "NameChip",
                        typeof(RectTransform),
                        typeof(Image)
                    ).GetComponent<RectTransform>();
            chip.SetParent(panel, false);
            // 이름 글자가 이름표 위에 그려지도록 이름표를 이름 바로 앞에 둔다.
            if (chip.GetSiblingIndex() > title.GetSiblingIndex())
                chip.SetSiblingIndex(title.GetSiblingIndex());
            var image = chip.GetComponent<Image>();
            image.raycastTarget = false;
            // 알약 그림의 둥근 끝이 48px 높이 안에 들어가도록 9칸 나누기 모서리를 줄여 그린다.
            image.pixelsPerUnitMultiplier = 2f;
            Theme(chip, UiThemeRole.Badge);
            var view = panel.GetComponentInParent<DialoguePanelView>(true);
            BuilderTools.SetReference(view, "nameChip", chip);
            return chip;
        }

        // 지난 대사까지 스크롤해 보는 대사 칸. 장면 위에서 읽히도록 크고 흰 둥근 글자로 쓴다.
        private static void Lines(Transform conversation, DialogueCinemaLayout layout)
        {
            var history = (RectTransform)conversation.Find("History");
            Place(history, layout.Lines);
            // 스크롤을 받는 바탕은 투명하게 둔다. 옅은 흰색도 어두운 그라데이션 위에서는 상자 테두리처럼 보였다.
            history.Find("Viewport").GetComponent<Image>().color = Color.clear;
            var lines = history.GetComponentInChildren<TMP_Text>(true);
            lines.fontSize = 34;
            lines.lineSpacing = 6;
            // 긴 대사가 오른쪽 끝에서 잘리지 않게 줄을 바꾼다. 대사 칸 높이는 DialoguePanelView가 대사 길이에 맞춘다.
            lines.textWrappingMode = TextWrappingModes.Normal;
            Theme(lines.transform, UiThemeRole.TitleOnDark);
            LinesScrollbar(history);

            Transform status = conversation.Find("Status");
            Place(status, layout.Status);
            status.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            Theme(status, UiThemeRole.AccentOnDark);
        }

        // 대사 칸 오른쪽 끝 세로 스크롤바. 지난 대사가 있어 위로 스크롤할 수 있을 때만 보인다.
        // 손가락으로도 잡을 수 있게 누르는 자리는 넓게 두고, 보이는 막대는 가늘게 그린다.
        private const float ScrollbarHitWidth = 36;
        private const float ScrollbarWidth = 10;

        private static void LinesScrollbar(RectTransform history)
        {
            Transform old = history.Find("Scrollbar");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            RectTransform bar = NewRect("Scrollbar", history);
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1, .5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(ScrollbarHitWidth, 0);
            Image hitArea = bar.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            ThinBar(NewRect("Track", bar), new Color(1, 1, 1, .18f));
            RectTransform slidingArea = NewRect("SlidingArea", bar);
            Stretch(slidingArea);
            RectTransform handle = NewRect("Handle", slidingArea);
            Stretch(handle);
            Image handleHitArea = handle.gameObject.AddComponent<Image>();
            handleHitArea.color = Color.clear;
            ThinBar(NewRect("Thumb", handle), new Color(1, 1, 1, .78f));

            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleHitArea;
            scrollbar.transition = Selectable.Transition.None;
            // 키보드 이동으로 스크롤바가 골라지면 입력칸의 글자 입력을 가로챈다.
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            var scroll = history.GetComponent<ScrollRect>();
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            // 긴 대사가 스크롤바 밑으로 들어가지 않게 대사 칸 오른쪽을 스크롤바 자리만큼 비운다.
            var viewport = (RectTransform)history.Find("Viewport");
            viewport.offsetMax = new Vector2(-ScrollbarHitWidth, viewport.offsetMax.y);
        }

        // 누르는 자리 가운데에 세로로 선 둥근 막대
        private static void ThinBar(RectTransform rect, Color color)
        {
            rect.anchorMin = new Vector2(.5f, 0);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ScrollbarWidth, 0);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            // 기본 둥근 판 그림의 모서리(10)를 막대 폭의 절반으로 줄여 양 끝을 둥글게 한다.
            image.pixelsPerUnitMultiplier = 10f / (ScrollbarWidth / 2f);
            image.color = color;
            image.raycastTarget = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        // 반투명 입력칸, 둥근 [보내기], 코랄 [말하기]. 세 조작은 높이를 맞춘다.
        private static void InputRow(Transform inputRow, DialogueCinemaLayout layout)
        {
            FreeLayout(inputRow);
            Stretch((RectTransform)inputRow);
            Transform inputBox = inputRow.Find("Input");
            Place(inputBox, layout.Input);
            var input = inputBox.GetComponent<TMP_InputField>();
            input.textComponent.fontSize = 26;
            input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport.offsetMin = new Vector2(28, 12);
            input.textViewport.offsetMax = new Vector2(-28, -12);
            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.text = "영어로 답하기…";
                placeholder.fontSize = 24;
                placeholder.fontStyle = FontStyles.Normal;
                placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            }
            // 반투명 그림의 모서리를 높이의 절반까지 키워 양 끝을 둥글게 한다.
            inputBox.GetComponent<Image>().pixelsPerUnitMultiplier = RoundEnds;
            Theme(inputBox, UiThemeRole.GhostInput);

            Transform send = inputRow.Find("SendButton");
            Place(send, layout.Send);
            // 이름을 숨긴 둥근 버튼이라 아이콘 크기만 이름 글자 크기로 정한다.
            Ghost(send, "", UiIcon.Send, layout.Send.width, 30);
            send.GetComponent<Image>().pixelsPerUnitMultiplier = RoundEnds;

            Transform talk = inputRow.Find("TalkButton");
            Place(talk, layout.Talk);
            Pill(talk, "말하기", UiIcon.Mic, UiTone.Secondary, 1.4f, 26);
        }

        /// <summary>
        /// 대화 보조 버튼을 [자막]·[듣기]·[뜻]·[힌트]·[예시 문장] 순서로 이름 줄 오른쪽에 붙인다.
        /// [자막]은 [듣기]와 짝이라 바로 앞에 둔다.
        /// 대화 종류에 따라 숨는 버튼이 있어 가로 정렬로 빈자리 없이 오른쪽에 모은다.
        /// </summary>
        internal static void ToolRow(Transform tools)
        {
            Row(tools);
            Ghost(tools.Find("SubtitleButton"), "자막 끔", UiIcon.None, 108);
            Ghost(tools.Find("ReplayButton"), "듣기", UiIcon.Replay, 92);
            Ghost(tools.Find("MeaningButton"), "뜻", UiIcon.Meaning, 80);
            Ghost(tools.Find("HintButton"), "힌트", UiIcon.Hint, 92);
            Ghost(tools.Find("ExampleButton"), "예시 문장", UiIcon.Example, 128);
            foreach (
                string name in new[]
                {
                    "SubtitleButton",
                    "ReplayButton",
                    "MeaningButton",
                    "HintButton",
                    "ExampleButton",
                }
            )
                tools.Find(name).SetAsLastSibling();
            Transform more = tools.Find("MoreButton");
            if (more != null)
                Object.DestroyImmediate(more.gameObject);
        }

        /// <summary>
        /// 대사에서 누른 단어 근처에 뜨는 호버 카드. 오른쪽 위 도움 카드·사물 단어 카드처럼 카드 자체를 밝은 테마 카드로 칠해 어두운 대화창 위에서 떠 보이게 하고,
        /// 단어는 제목, 뜻은 강조색, 저장 안내는 흐린 글자로 둔다. 카드 바탕이 클릭을 받아 아래 대사가 눌리지 않는다.
        /// </summary>
        private static void WordCard(Transform card)
        {
            BuilderTools.GetOrAddComponent<Image>(card.gameObject).raycastTarget = true;
            Theme(card, UiThemeRole.Card);
            Theme(card.Find("Title"), UiThemeRole.Title);
            Theme(card.Find("Meaning"), UiThemeRole.Accent);
            Theme(card.Find("Note"), UiThemeRole.Muted);
            // 64px보다 낮은 버튼이라 아이콘 없이 이름만 둔다. 밝은 카드 위 버튼은 문장 카드의 [넣기]처럼 입체 알약으로 칠한다.
            Pill(card.Find("WordSaveButton"), "단어장에 저장", UiIcon.Save, UiTone.Primary, 2f, 20);
            Pill(card.Find("CloseButton"), "닫기", UiIcon.Close, UiTone.Neutral, 2f, 18);
        }

        // NPC 대답을 기다리는 동안 대사 자리에 바탕 없이 도는 고리와 흰 글씨를 왼쪽부터 둔다.
        private static void Waiting(Transform wait)
        {
            if (wait == null)
                return;
            Object.DestroyImmediate(wait.GetComponent<ThemedElement>());
            wait.GetComponent<Image>().enabled = false;
            var row = wait.Find("Row").GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            var label = wait.Find("Row/Label").GetComponent<TMP_Text>();
            label.fontSize = 30;
            Theme(label.transform, UiThemeRole.TitleOnDark);
        }

        // 단어·문장 도움 카드. 자리(오른쪽 위)는 DialoguePanelView가 정한다.
        private static void HelpCards(Transform help)
        {
            FreeLayout(help);
            help.GetComponent<Image>().enabled = false;
            help.Find("Title").gameObject.SetActive(false);
            Transform viewport = help.Find("Viewport");
            Stretch((RectTransform)viewport);
            Transform content = viewport.Find("Content");
            var helpLayout = content.GetComponent<VerticalLayoutGroup>();
            helpLayout.padding = new RectOffset(0, 0, 0, 0);
            helpLayout.spacing = 16;
            HelpCard(content.Find("WordsCard"), "단어", 150);
            HelpCard(content.Find("HintCard"), "문장", 240);
            Transform hint = content.Find("HintCard/UseHintButton");
            Pill(hint, "넣기", UiIcon.Example, UiTone.Primary, 1.7f, 22);
            var hintSize = BuilderTools.GetOrAddComponent<LayoutElement>(hint.gameObject);
            hintSize.minHeight = hintSize.preferredHeight = 58;
            hintSize.minWidth = hintSize.preferredWidth = 120;
            hintSize.flexibleWidth = 0;
        }

        private static void HelpCard(Transform card, string title, float minimumHeight)
        {
            Theme(card, UiThemeRole.Card);
            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(44, 44, 36, 36);
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.UpperLeft;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(card.gameObject);
            size.minHeight = minimumHeight;
            var heading = card.Find("Title").GetComponent<TMP_Text>();
            heading.text = title;
            heading.fontSize = 26;
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
                text.enableAutoSizing = false;
        }

        // 어두운 바탕 위의 반투명 버튼. 이름이 없으면 아이콘만 가운데에 두는 둥근 버튼이다.
        private static void Ghost(
            Transform button,
            string caption,
            UiIcon icon,
            float width,
            float fontSize = ToolFontSize
        )
        {
            TMP_Text label = Content(button, caption, fontSize);
            label.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            var image = button.GetComponent<Image>();
            image.pixelsPerUnitMultiplier = 1f;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.ignoreLayout = false;
            size.minWidth = size.preferredWidth = width;
            size.minHeight = size.preferredHeight = ToolHeight;
            Theme(button, UiThemeRole.GhostControl, UiTone.Neutral, icon);
        }

        // 색이 칠해진 입체 알약 버튼. 64px 이상이면 아이콘과 이름을 한 묶음으로 가운데에 둔다.
        private static void Pill(
            Transform button,
            string caption,
            UiIcon icon,
            UiTone tone,
            float pixelsPerUnit,
            float fontSize
        )
        {
            Content(button, caption, fontSize);
            button.GetComponent<Image>().pixelsPerUnitMultiplier = pixelsPerUnit;
            Theme(button, UiThemeRole.PillButton, tone, icon);
        }

        // 버튼 안을 아이콘(Icon)과 이름(Label)만 남기고 정리한다. 색과 글꼴은 버튼의 테마 역할이 칠한다.
        private static TMP_Text Content(Transform button, string caption, float fontSize)
        {
            foreach (string decoration in new[] { "Face", "Outline", "LineIcon" })
            {
                Transform old = button.Find(decoration);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
            var image = button.GetComponent<Image>();
            image.raycastTarget = true;
            var selectable = button.GetComponent<Selectable>();
            if (selectable != null)
                selectable.targetGraphic = image;
            Transform iconBox = button.Find("Icon");
            if (iconBox == null)
            {
                iconBox = new GameObject("Icon", typeof(RectTransform), typeof(Image)).transform;
                iconBox.SetParent(button, false);
            }
            iconBox.GetComponent<Image>().raycastTarget = false;
            var label = button.Find("Label").GetComponent<TMP_Text>();
            Object.DestroyImmediate(label.GetComponent<ThemedElement>());
            label.text = caption;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static void Theme(
            Transform target,
            UiThemeRole role,
            UiTone tone = UiTone.Neutral,
            UiIcon icon = UiIcon.None
        )
        {
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.FindProperty("tone").enumValueIndex = (int)tone;
            fields.FindProperty("icon").enumValueIndex = (int)icon;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
        }

        // 보이는 버튼만 오른쪽 끝에 붙여 세우는 가로 줄
        private static void Row(Transform row)
        {
            var fitter = row.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);
            var group = row.GetComponent<HorizontalLayoutGroup>();
            group.enabled = true;
            group.spacing = 10;
            group.padding = new RectOffset(0, 0, 0, 0);
            group.childAlignment = TextAnchor.MiddleRight;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
        }

        private static void FreeLayout(Transform target)
        {
            var group = target.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (group != null)
                group.enabled = false;
            var fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Place(Transform target, Rect area)
        {
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = area.position;
            rect.sizeDelta = area.size;
            var size = target.GetComponent<LayoutElement>();
            if (size != null)
                size.ignoreLayout = true;
        }
    }
}
