using A305.Mission;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Quest = A305.EditorTools.Mission.QuestBoardBuilder;
using Ui = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 퀘스트 창의 [이야기 | 장소 연습] 탭과 장소 연습 쪽(시안 mock.html #place)을 만든다. 자리는 시안 px × 1.2 캔버스 단위이고,
    /// 그림은 시안을 2.4배로 렌더링한 Art/UI/Quest/Practice* 조각이다. 글자(장소·상황·직원)는 Jua·Pretendard TMP로 쓴다.
    /// 판(1520×800)과 머리줄(아이콘·'퀘스트'·탭·닫기)은 이야기 쪽과 같고, 자리는 시안 판 기준 그대로다.
    /// </summary>
    internal static class PracticeBoardBuilder
    {
        private const float Px = Ui.Px;

        // 시안 판 자리에서 내용을 옮기는 만큼(머리줄이 시안과 같아 0)
        private const float BodyLift = 0;

        // 머리줄(시안 .head 높이 76): 아이콘 타일(76) 왼쪽 41·위 35, '퀘스트'(Jua 46) 왼쪽 139, 탭(.tabs 290×56) 289.31·45, 둥근 닫기(76) 1403
        private const float TabsLeft = 289.31f;
        private const float TabsTop = 45;
        private static readonly (float left, float top, float right, float bottom) HeadPad = (
            16,
            10,
            16,
            22
        );

        // 시안의 반투명 잉크(.58·.45·.75)를 밝은 바탕에 얹은 색. 선형 색 공간의 UI는 반투명 글자가 더 옅어 보여 불투명으로 쓴다.
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(134, 127, 119, 255);
        private static readonly Color CountInk = new Color32(160, 154, 147, 255);
        private static readonly Color ChipInk = new Color32(96, 88, 78, 255);

        private const string Note =
            "이 가게에서 한 가지 상황만 골라 연습해요. 이야기와 따로, 몇 번이든 할 수 있어요.";

        private static readonly (string Code, string Art)[] Places =
        {
            ("HOTEL", "Hotel"),
            ("CAFE", "Cafe"),
            ("DINER", "Diner"),
            ("LAUNDRY", "Laundry"),
            ("BANK", "Bank"),
            ("BARBER", "Barber"),
            ("FLORIST", "Florist"),
            ("BAKERY", "Bakery"),
            ("DRUGSTORE", "Drugstore"),
            ("HOME_STORE", "HomeStore"),
            ("ARCADE", "Arcade"),
            ("POST_OFFICE", "PostOffice"),
        };

        /// <summary>
        /// 두 탭 쪽의 머리줄(시안 .head): 아이콘 타일·'퀘스트'·[이야기 | 장소 연습] 탭·둥근 닫기. 탭은 쪽마다 지금 쪽이 켜진 그림을 두고
        /// 두 칸을 누르는 자리로 덮는다.
        /// </summary>
        internal static void Header(QuestBoardView view, RectTransform page, bool storyShown)
        {
            Quest.Picture(
                page,
                "HeaderIcon",
                41,
                35,
                76,
                76,
                HeadPad,
                Quest.Art("QuestHeaderTile")
            );
            // TMP의 Jua는 칸 가운데에 두면 브라우저보다 낮게 그려져, 캡처로 잰 만큼(시안 px) 올린다.
            TMP_Text title = Quest.Text(
                page,
                "Title",
                139,
                44 - 3.5f,
                300,
                58,
                Ui.DisplayFontPath,
                46,
                Ink
            );
            title.text = "퀘스트";
            Tabs(view, page, storyShown);
            Quest.Listen(
                Quest.ArtButton(
                    page,
                    "Close",
                    1403,
                    35,
                    76,
                    76,
                    HeadPad,
                    Quest.Art("QuestCloseRound")
                ),
                view.Close
            );
        }

        private static void Tabs(QuestBoardView view, RectTransform page, bool storyShown)
        {
            RectTransform tabs = Ui.Rect("Tabs", page);
            Ui.TopLeft(tabs, TabsLeft, TabsTop, 290, 56);
            RectTransform art = Ui.Rect("Art", tabs);
            Ui.Spread(art, 10, 6, 10, 14);
            Ui.Picture(
                art.gameObject,
                Quest.Art(storyShown ? "PracticeTabsStory" : "PracticeTabsPlace")
            );
            Quest.Listen(TabButton(tabs, "StoryTab", 5), view.ShowStoryTab);
            Quest.Listen(TabButton(tabs, "PlaceTab", 145), view.ShowPracticeTab);
        }

        /// <summary>장소 연습 쪽: 머리줄, 왼쪽 장소 목록과 스크롤 막대·아래 흐림, 오른쪽 판</summary>
        internal static void Build(
            QuestBoardView view,
            SerializedObject boardFields,
            RectTransform sheet
        )
        {
            RectTransform page = Ui.Rect("Practice", sheet);
            Ui.Fill(page);
            var practice = page.gameObject.AddComponent<PracticeBoardView>();
            var fields = new SerializedObject(practice);

            Header(view, page, storyShown: false);

            BuildList(fields, page);
            BuildPane(view, fields, page);
            fields.ApplyModifiedPropertiesWithoutUndo();
            boardFields.FindProperty("practicePage").objectReferenceValue = practice;
            page.gameObject.SetActive(false);
        }

        // 왼쪽 장소 목록(시안 .list 420×620, 안쪽 여백 위 2·왼 2·오른 6, 줄 412×82 사이 12). 줄 그림자는 목록 밖에서 잘린다.
        private static void BuildList(SerializedObject fields, RectTransform page)
        {
            RectTransform list = Ui.Rect("Places", page);
            Ui.TopLeft(list, 41, 141 - BodyLift, 420, 620);
            // 줄 사이 빈 곳에서도 휠이 먹도록 보이지 않는 바탕을 깐다.
            list.gameObject.AddComponent<Image>().color = Color.clear;
            list.gameObject.AddComponent<RectMask2D>();
            RectTransform rows = Ui.Rect("Rows", list);
            rows.anchorMin = new Vector2(0, 1);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(.5f, 1);
            rows.anchoredPosition = Vector2.zero;
            rows.sizeDelta = Vector2.zero;
            var column = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(Round(2), Round(6), Round(2), 0);
            column.spacing = 12 * Px;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = column.childControlHeight = false;
            column.childForceExpandWidth = column.childForceExpandHeight = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.content = rows;
            scroll.viewport = list;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            scroll.verticalScrollbar = Scrollbar(page);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            Button template = Row(rows);
            template.gameObject.SetActive(false);
            Image fade = Quest.Picture(
                page,
                "Fade",
                41,
                671 - BodyLift,
                420,
                90,
                (0, 0, 0, 0),
                Quest.Art("PracticeFade")
            );

            Set(fields, "placeList", scroll);
            Set(fields, "rowTemplate", template);
            Set(fields, "fade", fade.gameObject);
            Set(fields, "rowSprite", Quest.Art("PracticeRow"));
            Set(fields, "selectedRowSprite", Quest.Art("PracticeRowOn"));
            SerializedProperty icons = fields.FindProperty("icons");
            icons.arraySize = Places.Length;
            for (int i = 0; i < Places.Length; i++)
            {
                SerializedProperty icon = icons.GetArrayElementAtIndex(i);
                icon.FindPropertyRelative("PlaceCode").stringValue = Places[i].Code;
                icon.FindPropertyRelative("Small").objectReferenceValue = Quest.Art(
                    "PracticeIcon" + Places[i].Art
                );
                icon.FindPropertyRelative("Large").objectReferenceValue = Quest.Art(
                    "PracticeIcon" + Places[i].Art + "Large"
                );
            }
        }

        // 장소 줄(시안 .place 412×82): 아이콘(54) 왼쪽 19, 이름(Jua 24)·앞 두 상황(15), 오른쪽 19 안에 끝낸 수(Jua 17).
        // 고른 줄은 코랄 테두리 그림으로 바꾸고 안의 내용을 테두리만큼 들인다(PracticeBoardView).
        private static Button Row(RectTransform rows)
        {
            RectTransform row = Ui.Rect("PlaceRow", rows);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
            row.sizeDelta = new Vector2(412, 82) * Px;
            row.gameObject.AddComponent<Image>().color = Color.clear;
            Image background = Quest.Picture(
                row,
                "Background",
                0,
                0,
                412,
                82,
                (20, 14, 20, 30),
                Quest.Art("PracticeRow")
            );
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.97f, .97f, .97f);
            colors.pressedColor = new Color(.9f, .9f, .9f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            RectTransform content = Ui.Rect("Content", row);
            Ui.Fill(content);
            Quest.Picture(
                content,
                "Icon",
                19,
                14,
                54,
                54,
                (0, 0, 0, 0),
                Quest.Art("PracticeIconHotel")
            );
            TMP_Text name = Quest.Text(
                content,
                "Name",
                89,
                16.5f - 1,
                250,
                30,
                Ui.DisplayFontPath,
                24,
                Ink
            );
            name.overflowMode = TextOverflowModes.Ellipsis;
            // 말줄임은 줄 높이가 칸보다 크면 글자를 지우므로 시안 칸(47.5, 높이 18)의 가운데를 지키며 칸을 넓힌다.
            TMP_Text summary = Quest.Text(
                content,
                "Summary",
                89,
                45.5f,
                250,
                22,
                Ui.BodyFontPath,
                15,
                Muted
            );
            summary.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text count = Quest.Text(
                content,
                "Count",
                0,
                30.5f,
                60,
                21,
                Ui.DisplayFontPath,
                17,
                CountInk
            );
            // 끝낸 수는 줄 오른쪽 끝에 붙인다.
            RectTransform countRect = count.rectTransform;
            countRect.anchorMin = countRect.anchorMax = countRect.pivot = Vector2.one;
            countRect.anchoredPosition = new Vector2(-19, -30.5f) * Px;
            count.alignment = TextAlignmentOptions.MidlineRight;
            return button;
        }

        // 목록 오른쪽 스크롤 막대(시안 .scroll 6 폭, 목록 오른쪽 밖 8, 위 6). 막대 길이는 보이는 비율을 따른다.
        private static Scrollbar Scrollbar(RectTransform page)
        {
            RectTransform bar = Ui.Rect("Scrollbar", page);
            Ui.TopLeft(bar, 469, 147 - BodyLift, 6, 608);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scrollbar.transition = Selectable.Transition.None;
            RectTransform area = Ui.Rect("SlidingArea", bar);
            Ui.Fill(area);
            RectTransform handle = Ui.Rect("Handle", area);
            Ui.Fill(handle);
            Image thumb = handle.gameObject.AddComponent<Image>();
            Ui.Sliced(thumb, Quest.Art("PracticeScrollThumb", new Vector4(0, 7, 0, 7)));
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = thumb;
            // 처음에는 목록 맨 위를 보인다(막대 값 1). Editor는 화면을 다시 그릴 때 막대 값으로 목록을 옮긴다.
            scrollbar.value = 1;
            return scrollbar;
        }

        // 오른쪽 판(시안 .right 990×620, 안쪽 여백 36·40): 큰 아이콘(92)·장소 이름(Jua 44)·직원 칩·안내, 상황 줄 세 개(910×108, 사이 14)
        private static void BuildPane(
            QuestBoardView view,
            SerializedObject fields,
            RectTransform page
        )
        {
            RectTransform pane = Ui.Rect("Pane", page);
            Ui.TopLeft(pane, 489, 141 - BodyLift, 990, 620);
            RectTransform card = Ui.Rect("Card", pane);
            Ui.Spread(card, 30, 20, 30, 40);
            Image cardFace = card.gameObject.AddComponent<Image>();
            // 그림 2.4배: 모서리 36 + 그림자 여백(왼 30, 위 20, 오른 30, 아래 40)
            Ui.Sliced(cardFace, Quest.Art("PracticePane", new Vector4(159, 183, 159, 135)));
            cardFace.raycastTarget = false;

            RectTransform content = Ui.Rect("Content", pane);
            Ui.Fill(content);
            Image icon = Quest.Picture(
                content,
                "PlaceIcon",
                40,
                36,
                92,
                92,
                (0, 0, 0, 0),
                Quest.Art("PracticeIconHotelLarge")
            );
            TMP_Text name = Quest.Text(
                content,
                "PlaceName",
                154,
                44 - 3,
                400,
                44,
                Ui.DisplayFontPath,
                44,
                Ink
            );

            // 직원 칩(시안 .npc 높이 34, 양옆 14): 자리와 폭은 PracticeBoardView가 이름 폭에 맞춘다.
            RectTransform chip = Ui.Rect("NpcChip", content);
            Ui.TopLeft(chip, 248.81f, 52.5f, 138.38f, 34);
            Image chipFace = chip.gameObject.AddComponent<Image>();
            Ui.Sliced(chipFace, Quest.Art("PracticeNpcChip", new Vector4(41, 0, 41, 0)));
            chipFace.raycastTarget = false;
            TMP_Text chipText = Quest.Text(
                chip,
                "NpcChipText",
                0,
                0,
                138.38f,
                34,
                Ui.DisplayFontPath,
                17,
                ChipInk
            );
            Ui.Fill(chipText.rectTransform);
            chipText.alignment = TextAlignmentOptions.Midline;

            TMP_Text note = Quest.Text(
                content,
                "PlaceNote",
                154,
                100,
                760,
                20,
                Ui.BodyFontPath,
                17,
                Muted
            );
            note.text = Note;

            SerializedProperty slots = fields.FindProperty("scenarios");
            slots.arraySize = 3;
            for (int i = 0; i < 3; i++)
                BuildScenario(view, slots.GetArrayElementAtIndex(i), content, i);

            Set(fields, "paneContent", content.gameObject);
            Set(fields, "placeIcon", icon);
            Set(fields, "placeName", name);
            Set(fields, "npcChip", chip);
            Set(fields, "npcChipText", chipText);
            Set(fields, "numberSprite", Quest.Art("PracticeNumber"));
            Set(fields, "doneNumberSprite", Quest.Art("PracticeNumberDone"));
            content.gameObject.SetActive(false);
        }

        // 상황 줄(시안 .q 910×108): 번호 동그라미(46, 끝냈으면 초록 체크)·제목(Jua 27)·설명(16),
        // 오른쪽 27 안에 [연습하기] 또는 '완료' 칩과 [다시 하기]. 누르면 QuestBoardView.StartPractice(줄 번호)
        private static void BuildScenario(
            QuestBoardView view,
            SerializedProperty slot,
            RectTransform content,
            int index
        )
        {
            RectTransform root = Ui.Rect("Scenario" + index, content);
            root.name = "Scenario";
            Ui.TopLeft(root, 40, 158 + index * 122, 910, 108);
            Image background = root.gameObject.AddComponent<Image>();
            Ui.Sliced(background, Quest.Art("PracticeScenario", new Vector4(68, 0, 68, 0)));
            background.raycastTarget = false;

            Image number = Quest.Picture(
                root,
                "Number",
                27,
                31,
                46,
                46,
                (0, 0, 0, 0),
                Quest.Art("PracticeNumber")
            );
            TMP_Text numberText = Quest.Text(
                number.transform,
                "NumberText",
                0,
                0,
                46,
                46,
                Ui.DisplayFontPath,
                22,
                Color.white
            );
            Ui.Fill(numberText.rectTransform);
            numberText.alignment = TextAlignmentOptions.Midline;
            TMP_Text title = Quest.Text(
                root,
                "ScenarioTitle",
                93,
                24.5f - 1.5f,
                649.14f,
                34,
                Ui.DisplayFontPath,
                27,
                Ink
            );
            title.overflowMode = TextOverflowModes.Ellipsis;
            // 시안의 h4는 굵게 그린다(Jua에 굵은 글꼴이 없어 브라우저가 획만 두껍게 칠함).
            // TMP 굵게는 글자 사이를 0.07em 벌리므로 그만큼 좁혀 글자 폭을 시안과 맞춘다.
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = -7;
            // 시안 칸(64.5, 높이 19)의 가운데를 지키며 한 줄이 들어가게 넓힌다.
            TMP_Text description = Quest.Text(
                root,
                "ScenarioDescription",
                93,
                62.5f,
                649.14f,
                23,
                Ui.BodyFontPath,
                16,
                Muted
            );
            description.overflowMode = TextOverflowModes.Ellipsis;

            Image done = Quest.Picture(
                root,
                "DoneChip",
                684.23f,
                36,
                54.38f,
                36,
                (0, 0, 0, 0),
                Quest.Art("PracticeDoneChip")
            );
            Button again = Quest.ArtButton(
                root,
                "Again",
                752.61f,
                26,
                130.39f,
                56,
                (22, 12, 22, 40),
                Quest.Art("PracticeAgain")
            );
            Button start = Quest.ArtButton(
                root,
                "Start",
                762.14f,
                26,
                120.86f,
                56,
                (22, 12, 22, 40),
                Quest.Art("PracticeStart")
            );
            ListenSlot(again, view.StartPractice, index);
            ListenSlot(start, view.StartPractice, index);

            Set(slot, "Root", root);
            Set(slot, "Number", number);
            Set(slot, "NumberText", numberText);
            Set(slot, "Title", title);
            Set(slot, "Description", description);
            Set(slot, "DoneChip", done.gameObject);
            Set(slot, "Start", start);
            Set(slot, "Again", again);
        }

        private static Button TabButton(RectTransform tabs, string name, float x)
        {
            RectTransform rect = Ui.Rect(name, tabs);
            Ui.TopLeft(rect, x, 5, 140, 46);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        // EditMode 테스트와 Editor에서도 누르면 동작하도록 Editor·실행 모두에서 부른다.
        private static void ListenSlot(Button button, UnityAction<int> action, int slot)
        {
            UnityEventTools.AddIntPersistentListener(button.onClick, action, slot);
            button.onClick.SetPersistentListenerState(
                button.onClick.GetPersistentEventCount() - 1,
                UnityEventCallState.EditorAndRuntime
            );
        }

        private static void Set(SerializedObject fields, string name, Object value) =>
            fields.FindProperty(name).objectReferenceValue = value;

        private static void Set(SerializedProperty slot, string name, Object value) =>
            slot.FindPropertyRelative(name).objectReferenceValue = value;

        private static int Round(float px) => Mathf.RoundToInt(px * Px);
    }
}
