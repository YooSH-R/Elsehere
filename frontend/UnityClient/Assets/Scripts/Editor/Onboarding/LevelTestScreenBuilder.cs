using A305.Api;
using A305.EditorTools.Common;
using A305.Onboarding;
using A305.Theme;
using A305.Voice;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using BrowserImeInput = WebGLSupport.WebGLInput;
using F = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Onboarding
{
    /// <summary>
    /// 화면 04 레벨 테스트 창을 확정 시안(L1 안내·L2 듣기·L3 말하기·L4 결과)대로 만든다. 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다.
    /// </summary>
    /// <remarks>
    /// 창은 진입 화면과 같은 가운데 유리 창이고, 안내·문항 머리·듣기·말하기·문항 [다음]·결과 묶음을 세로로 쌓아 보이는 묶음만 켠다. 창 높이는
    /// 켜진 묶음에 맞춘다(ContentSizeFitter). 보기·상황 카드·피드백처럼 글 길이에 따라 높이가 바뀌는 칸은 세로 배치로 쌓고, 나머지는 시안 자리에
    /// 둔다. 칸·버튼·아이콘·점수 칸은 시안을 렌더링한 그림(Art/UI/Entry/LevelTest*)이고, 서버가 주거나 바뀌는 글자와 버튼 이름은 글자다.
    /// 글자 위치는 시안의 줄 높이에서 위아래로 남는 반쪽 간격만큼 내려 맞춘다.
    /// </remarks>
    public static class LevelTestScreenBuilder
    {
        private const string MenuPath = "Tools/A305/Onboarding/Apply Level Test Screens";
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";
        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";
        private const string VoiceSettingsPath = "Assets/Data/Voice/VoiceRecorderSettings.asset";
        private const string VoiceBuilderMenu = A305.EditorTools.Voice.VoiceBuilder.MenuPath;

        // 시안 창 폭과 안내 화면 높이, 내용의 왼쪽 자리·폭(테두리 포함)
        private const float CardWidth = 620;
        private const float IntroHeight = 640;
        private const float Left = 54;
        private const float Width = 512;

        // Pretendard는 글자 칸이 1.193em, Jua는 1em이고 줄 간격은 1.25em이다. TMP 줄 간격은 0.01em 단위다.
        private const float PretendardBox = 1.193f;
        private const float JuaBox = 1f;
        private const float JuaLine = 1.25f;

        // 칸 그림의 그림자 자리(왼, 위, 오른, 아래)
        private static readonly Vector4 TileShadow = new Vector4(20, 14, 20, 26);
        private static readonly Vector4 ButtonShadow = new Vector4(22, 12, 22, 40);

        // 시안의 반투명 글자색(잉크 62·50·70%)을 흰 창 위에 섞은 색. 선형 색 공간은 반투명 글자를 더 옅게 섞어 불투명 색으로 둔다.
        private static readonly Color Ink = F.Ink;
        private static readonly Color Muted = new Color32(126, 118, 110, 255);
        private static readonly Color Quiet = new Color32(151, 146, 141, 255);
        private static readonly Color GhostInk = new Color32(109, 102, 95, 255);
        private static readonly Color ChipInk = new Color32(154, 91, 20, 255);
        private static readonly Color LevelOrange = new Color32(255, 108, 79, 255);
        private static readonly Color Caret = new Color32(255, 108, 79, 255);

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(
                PrefabPath,
                root =>
                {
                    var screen = root.GetComponent<OnboardingScreen>();
                    BuilderTools.SetReference(
                        screen,
                        "levelTestPanel",
                        Apply((RectTransform)root.transform.Find("LevelTestPanel"))
                    );
                    BuilderTools.RequireReferences(screen);
                }
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>창을 시안대로 다시 맞추고 화면 연결을 채운다. 여러 번 실행해도 같은 결과다.</summary>
        public static LevelTestPanelView Apply(RectTransform panel)
        {
            RemoveOldParts(panel);
            Card(panel);
            Stack(panel.gameObject);
            var fitter = BuilderTools.GetOrAddComponent<ContentSizeFitter>(panel.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var view = BuilderTools.GetOrAddComponent<LevelTestPanelView>(panel.gameObject);
            Intro(panel, view);
            Header(panel, view);
            Listen(panel, view);
            Speak(panel, view);
            Footer(panel, view);
            Result(panel, view);
            Devices(panel, view);

            string[] order =
            {
                "Sheet",
                "IntroPage",
                "ItemHeader",
                "ListenBody",
                "SpeakBody",
                "ItemFooter",
                "ResultPage",
            };
            for (int index = 0; index < order.Length; index++)
            {
                Transform part = panel.Find(order[index]);
                part.SetSiblingIndex(index);
                // 처음에는 안내만 보인다. 나머지는 화면이 테스트 상태에 맞춰 켠다.
                part.gameObject.SetActive(index <= 1);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            BuilderTools.RequireReferences(view);
            return view;
        }

        // 준비 중 안내 시절의 제목·안내·칸·버튼처럼 지금 창에 없는 자식을 지운다.
        private static void RemoveOldParts(RectTransform panel)
        {
            string[] parts =
            {
                "Sheet",
                "IntroPage",
                "ItemHeader",
                "ListenBody",
                "SpeakBody",
                "ItemFooter",
                "ResultPage",
            };
            for (int index = panel.childCount - 1; index >= 0; index--)
            {
                Transform child = panel.GetChild(index);
                if (System.Array.IndexOf(parts, child.name) < 0)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        // 화면 가운데의 시안 폭 창. 테마 창 바탕을 지우고 진입 화면과 같은 불투명한 유리 그림(그림자 자리 왼·오른 72, 위 48, 아래 104)을 깐다.
        private static void Card(RectTransform panel)
        {
            var theme = panel.GetComponent<ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            foreach (var bleed in panel.GetComponents<SurfaceBleed>())
                Object.DestroyImmediate(bleed);
            Image area = BuilderTools.GetOrAddComponent<Image>(panel.gameObject);
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(CardWidth, IntroHeight) * F.Px;
            RectTransform sheet = F.Rect("Sheet", panel);
            F.Spread(sheet, 72, 48, 72, 104);
            BuilderTools.GetOrAddComponent<LayoutElement>(sheet.gameObject).ignoreLayout = true;
            F.Sliced(
                F.Picture(sheet.gameObject, null),
                Art("EntryModal", new Vector4(278, 355, 278, 221))
            );
        }

        // L1: 제목·안내, 듣기·말하기 칸, [시작하기]·[건너뛰기], 건너뛰기 안내
        private static void Intro(Transform panel, LevelTestPanelView view)
        {
            RectTransform page = Block(panel, "IntroPage", IntroHeight);
            Text(
                page,
                "Title",
                F.DisplayFontPath,
                40,
                Ink,
                TextAlignmentOptions.Left,
                "레벨 테스트"
            )
                .rectTransform.Place(Left, 45, Width, 46);
            TMP_Text sub = Paragraph(
                page,
                "Subtitle",
                F.BodyFontPath,
                17,
                1.65f,
                Muted,
                "듣기와 말하기 문제로 나에게 맞는 대화 난이도를 추천해요.\n3분 정도 걸려요."
            );
            sub.rectTransform.Place(Left, 103 + HalfLeading(17, 1.65f, PretendardBox), Width, 56);
            Picture(page, "Tiles", "LevelTestIntroTiles")
                .rectTransform.Place(
                    Left - TileShadow.x,
                    183 - TileShadow.y,
                    Width + TileShadow.x + TileShadow.z,
                    216 + TileShadow.y + TileShadow.w
                );
            Button start = ArtButton(page, "StartButton", "LevelTestPrimary", Left, 427, Width, 60);
            ButtonLabel(start, "시작하기", Color.white);
            Button skip = ArtButton(page, "SkipButton", "LevelTestGhost", Left, 501, Width, 60);
            ButtonLabel(skip, "건너뛰기", GhostInk);
            Text(
                page,
                "Note",
                F.BodyFontPath,
                15,
                Muted,
                TextAlignmentOptions.Center,
                "건너뛰면 초급으로 시작해요. 난이도는 다음 화면에서 바꿀 수 있어요."
            )
                .rectTransform.Place(Left, 575, Width, 18);

            BuilderTools.SetReference(view, "introPage", page.gameObject);
            BuilderTools.SetReference(view, "startButton", start);
            BuilderTools.SetReference(view, "skipButton", skip);
        }

        // L2·L3 위: "문항 n / N", [그만하기], 진행 막대
        private static void Header(Transform panel, LevelTestPanelView view)
        {
            RectTransform header = Block(panel, "ItemHeader", 110);
            TMP_Text progress = Text(
                header,
                "Progress",
                F.DisplayFontPath,
                19,
                Muted,
                TextAlignmentOptions.Left,
                "문항 2 / 6"
            );
            progress.rectTransform.Place(Left, 45, 200, 24);

            // 글자는 시안 자리(오른쪽 끝, 높이 23)에 두고 누르는 칸만 조금 넓힌다.
            RectTransform quitRect = F.Rect("QuitButton", header);
            quitRect.Place(Left + Width - 80, 37, 80, 40);
            Image quitArea = BuilderTools.GetOrAddComponent<Image>(quitRect.gameObject);
            quitArea.sprite = null;
            quitArea.color = Color.clear;
            TMP_Text quitLabel = Text(
                quitRect,
                "Label",
                F.DisplayFontPath,
                18,
                Quiet,
                TextAlignmentOptions.Right,
                "그만하기"
            );
            F.Fill(quitLabel.rectTransform);
            var quit = BuilderTools.GetOrAddComponent<Button>(quitRect.gameObject);
            quit.targetGraphic = quitLabel;

            Image track = Picture(header, "ProgressTrack", "LevelTestBarTrack");
            track.rectTransform.Place(Left, 81, Width, 9);
            RectTransform fill = F.Rect("Fill", track.transform);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(2f / 6, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            F.Sliced(
                F.Picture(fill.gameObject, null),
                Art("LevelTestBarFill", new Vector4(12, 0, 12, 0))
            );

            BuilderTools.SetReference(view, "itemHeader", header.gameObject);
            BuilderTools.SetReference(view, "progressText", progress);
            BuilderTools.SetReference(view, "progressFill", fill);
            BuilderTools.SetReference(view, "quitButton", quit);
        }

        // L2: 듣기·말한 사람 알약, [다시 듣기] 칸, 물음, 보기 4줄(글이 길면 줄이 높아진다)
        private static void Listen(Transform panel, LevelTestPanelView view)
        {
            RectTransform body = F.Rect("ListenBody", panel);
            Stack(body.gameObject);
            var listen = BuilderTools.GetOrAddComponent<LevelTestListenView>(body.gameObject);

            RectTransform top = Block(body, "Top", 219);
            Picture(top, "ListenPill", "LevelTestListenPill")
                .rectTransform.Place(Left, 0, 83.97f, 32);
            RectTransform speaker = SpeakerPill(top);
            Button play = ArtButton(
                top,
                "PlayButton",
                "LevelTestPlayer",
                Left,
                48,
                Width,
                106,
                TileShadow
            );
            TMP_Text question = Text(
                top,
                "Question",
                F.DisplayFontPath,
                26,
                Ink,
                TextAlignmentOptions.Left,
                "무슨 뜻일까요?"
            );
            question.rectTransform.Place(Left, 168, Width, 33);

            RectTransform choices = F.Rect("Choices", body);
            var column = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(choices.gameObject);
            Controls(column, expandWidth: true);
            column.spacing = 10 * F.Px;
            column.padding = new RectOffset(0, 0, 0, 0);
            ContentWidth(choices);
            string[] samples =
            {
                "왼쪽 기계는 고장이라 창가 기계를 쓰고, 25센트 동전만 된다",
                "창가 기계는 고장이라 왼쪽 기계를 쓰고, 동전은 안 받는다",
                "왼쪽 기계를 쓰되, 세탁물은 4분의 1만 넣으라고 한다",
                "창가 기계는 25분 뒤에 쓸 수 있다",
            };
            var rows = new LevelTestChoiceRow[samples.Length];
            for (int index = 0; index < samples.Length; index++)
                rows[index] = ChoiceRow(choices, index, samples[index]);

            BuilderTools.SetReference(listen, "speakerPill", speaker.gameObject);
            BuilderTools.SetReference(
                listen,
                "speakerText",
                speaker.Find("Label").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(listen, "playButton", play);
            BuilderTools.SetReference(listen, "questionText", question);
            var fields = new SerializedObject(listen);
            SerializedProperty rowsProperty = fields.FindProperty("choiceRows");
            rowsProperty.arraySize = rows.Length;
            for (int index = 0; index < rows.Length; index++)
                rowsProperty.GetArrayElementAtIndex(index).objectReferenceValue = rows[index];
            fields.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.RequireReferences(listen);
            BuilderTools.SetReference(view, "listenView", listen);
        }

        // 말한 사람 알약: 글자 폭에 맞춰 늘어나는 흰 알약(테두리 1.5 + 양옆 14)
        private static RectTransform SpeakerPill(Transform top)
        {
            RectTransform pill = F.Rect("SpeakerPill", top);
            pill.Place(145.97f, 0, 145.75f, 32);
            F.Sliced(
                F.Picture(pill.gameObject, null),
                Art("LevelTestSpeakerPill", new Vector4(40, 0, 40, 0))
            );
            var row = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(pill.gameObject);
            int side = Mathf.RoundToInt(15.5f * F.Px);
            row.padding = new RectOffset(side, side, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            var fit = BuilderTools.GetOrAddComponent<ContentSizeFitter>(pill.gameObject);
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            Text(
                pill,
                "Label",
                F.DisplayFontPath,
                17,
                Ink,
                TextAlignmentOptions.Center,
                "Gus · 빨래방 주인"
            );
            return pill;
        }

        // 보기 한 줄: 흰 줄(고르면 코랄 테두리), 왼쪽 번호 동그라미, 보기 글자(17, 줄 높이 1.4). 한 줄이면 58 높이다.
        private static LevelTestChoiceRow ChoiceRow(Transform choices, int index, string sample)
        {
            RectTransform rowRect = F.Rect("Choice" + (index + 1), choices);
            Image area = BuilderTools.GetOrAddComponent<Image>(rowRect.gameObject);
            area.sprite = null;
            area.color = Color.clear;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(rowRect.gameObject);
            size.minHeight = 58 * F.Px;
            var row = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(rowRect.gameObject);
            int padding = Mathf.RoundToInt((11.5f + HalfLeading(17, 1.4f, PretendardBox)) * F.Px);
            row.padding = new RectOffset(
                Mathf.RoundToInt(64 * F.Px),
                Mathf.RoundToInt(19.5f * F.Px),
                padding,
                padding
            );
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;

            RectTransform art = F.Rect("Art", rowRect);
            art.SetAsFirstSibling();
            F.Fill(art);
            BuilderTools.GetOrAddComponent<LayoutElement>(art.gameObject).ignoreLayout = true;
            var border = new Vector4(58, 58, 58, 58);
            Image face = F.Picture(art.gameObject, null);
            F.Sliced(face, Art("LevelTestChoiceOff", border));
            var rowArt = BuilderTools.GetOrAddComponent<ChoiceArt>(art.gameObject);
            rowArt.Set(face, Art("LevelTestChoiceOn", border), Art("LevelTestChoiceOff", border));
            EditorUtility.SetDirty(rowArt);

            RectTransform number = F.Rect("Number", rowRect);
            number.SetSiblingIndex(1);
            number.anchorMin = number.anchorMax = number.pivot = new Vector2(0, .5f);
            number.anchoredPosition = new Vector2(20 * F.Px, 0);
            number.sizeDelta = new Vector2(30, 30) * F.Px;
            BuilderTools.GetOrAddComponent<LayoutElement>(number.gameObject).ignoreLayout = true;
            Image circle = F.Picture(number.gameObject, Art("LevelTestNumberOff"));
            var numberArt = BuilderTools.GetOrAddComponent<ChoiceArt>(number.gameObject);
            numberArt.Set(circle, Art("LevelTestNumberOn"), Art("LevelTestNumberOff"));
            EditorUtility.SetDirty(numberArt);
            TMP_Text numberText = Text(
                number,
                "Text",
                F.DisplayFontPath,
                17,
                Color.white,
                TextAlignmentOptions.Center,
                (index + 1).ToString()
            );
            F.Fill(numberText.rectTransform);

            TMP_Text label = Paragraph(rowRect, "Label", F.BodyFontPath, 17, 1.4f, Ink, sample);
            label.alignment = TextAlignmentOptions.Left;
            label.transform.SetAsLastSibling();

            var button = BuilderTools.GetOrAddComponent<Button>(rowRect.gameObject);
            Tint(button, face);
            var choice = BuilderTools.GetOrAddComponent<LevelTestChoiceRow>(rowRect.gameObject);
            BuilderTools.SetReference(choice, "button", button);
            BuilderTools.SetReference(choice, "rowArt", rowArt);
            BuilderTools.SetReference(choice, "numberArt", numberArt);
            BuilderTools.SetReference(choice, "numberText", numberText);
            BuilderTools.SetReference(choice, "choiceText", label);
            choice.Show(sample, index == 0);
            return choice;
        }

        // L3: 말하기 알약, 상황 카드(문장·말할 정보), 왼쪽 답 칸과 오른쪽 마이크
        private static void Speak(Transform panel, LevelTestPanelView view)
        {
            RectTransform body = F.Rect("SpeakBody", panel);
            Stack(body.gameObject);
            var speak = BuilderTools.GetOrAddComponent<LevelTestSpeakView>(body.gameObject);

            RectTransform top = Block(body, "Top", 48);
            Picture(top, "SpeakPill", "LevelTestSpeakPill")
                .rectTransform.Place(Left, 0, 99.17f, 32);

            // 상황 카드: 테두리 1.5 + 안쪽 18·20, 문장(Jua 23, 줄 높이 1.35)과 칩 사이 12
            RectTransform situation = F.Rect("Situation", body);
            var card = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(situation.gameObject);
            Controls(card, expandWidth: true);
            float leading = HalfLeading(23, 1.35f, JuaBox);
            card.padding = new RectOffset(
                Mathf.RoundToInt(21 * F.Px),
                Mathf.RoundToInt(21 * F.Px),
                Mathf.RoundToInt((19 + leading) * F.Px),
                Mathf.RoundToInt(19 * F.Px)
            );
            card.spacing = (12 + leading) * F.Px;
            ContentWidth(situation);
            Backdrop(situation, "LevelTestSituation", TileShadow, new Vector4(115, 130, 115, 101));
            TMP_Text question = Paragraph(
                situation,
                "Question",
                F.DisplayFontPath,
                23,
                1.35f,
                Ink,
                "카페에 가방을 두고 왔어요.\n가방이 어떻게 생겼는지 말하고 찾아 달라고 하세요."
            );

            RectTransform cues = F.Rect("Cues", situation);
            var cueRow = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(cues.gameObject);
            cueRow.spacing = 8 * F.Px;
            cueRow.padding = new RectOffset(0, 0, 0, 0);
            cueRow.childAlignment = TextAnchor.MiddleLeft;
            cueRow.childControlWidth = cueRow.childControlHeight = true;
            cueRow.childForceExpandWidth = false;
            cueRow.childForceExpandHeight = true;
            var cueSize = BuilderTools.GetOrAddComponent<LayoutElement>(cues.gameObject);
            cueSize.minHeight = cueSize.preferredHeight = 32 * F.Px;
            RectTransform cue = Chip(cues, "CueTemplate", "파란 숄더백");

            RectTransform answer = Block(body, "Answer", 122.28f);
            RectTransform box = F.Rect("AnswerBox", answer);
            box.Place(Left, 18, 410, 104.28f);
            Image boxArt = F.Picture(box.gameObject, null);
            F.Sliced(boxArt, Art("LevelTestAnswerBox", new Vector4(58, 58, 58, 58)));
            boxArt.raycastTarget = true;
            TMP_Text note = Text(
                box,
                "Note",
                F.DisplayFontPath,
                15,
                Muted,
                TextAlignmentOptions.Left,
                "내 답 · 말하면 여기에 적혀요. 고치거나 직접 써도 돼요"
            );
            note.rectTransform.Place(19, 15, 372, 21);
            TMP_InputField input = AnswerInput(box);
            Button microphone = ArtButton(answer, "MicButton", "LevelTestMic", 482, 28.14f, 84, 84);

            BuilderTools.SetReference(speak, "situationText", question);
            BuilderTools.SetReference(speak, "cuesParent", cues);
            BuilderTools.SetReference(speak, "cueTemplate", cue);
            BuilderTools.SetReference(speak, "answerInput", input);
            BuilderTools.SetReference(speak, "answerNoteText", note);
            BuilderTools.SetReference(speak, "microphoneButton", microphone);
            BuilderTools.RequireReferences(speak);
            BuilderTools.SetReference(view, "speakView", speak);
        }

        // 말할 정보 칩: 글자 폭에 맞춰 늘어나는 연노랑 알약(테두리 1 + 양옆 13), 글자 15
        private static RectTransform Chip(Transform cues, string name, string sample)
        {
            RectTransform chip = F.Rect(name, cues);
            F.Sliced(
                F.Picture(chip.gameObject, null),
                Art("LevelTestChip", new Vector4(40, 0, 40, 0))
            );
            var row = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(chip.gameObject);
            int side = Mathf.RoundToInt(14 * F.Px);
            row.padding = new RectOffset(side, side, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            // 칩은 글자 폭만큼만 차지한다.
            BuilderTools.GetOrAddComponent<LayoutElement>(chip.gameObject).flexibleWidth = 0;
            Text(chip, "Label", F.BodyFontPath, 15, ChipInk, TextAlignmentOptions.Center, sample);
            return chip;
        }

        // 답 칸의 입력: 글자 17(줄 높이 1.45) 두 줄이 보이고, 더 길면 칸 안에서 넘긴다. 영어 답이라 Enter는 줄바꿈 대신 입력을 마친다.
        private static TMP_InputField AnswerInput(RectTransform box)
        {
            RectTransform area = F.Rect("TextArea", box);
            area.Place(19, 40 + HalfLeading(17, 1.45f, PretendardBox), 372, 49.28f);
            BuilderTools.GetOrAddComponent<RectMask2D>(area.gameObject);
            TMP_Text typed = Paragraph(
                area,
                "Text",
                F.BodyFontPath,
                17,
                1.45f,
                Ink,
                "I think I left my bag here. It's a blue shoulder bag with a red keyring."
            );
            F.Fill(typed.rectTransform);

            var input = BuilderTools.GetOrAddComponent<TMP_InputField>(box.gameObject);
            input.textViewport = area;
            input.textComponent = typed;
            input.placeholder = null;
            input.targetGraphic = box.GetComponent<Image>();
            input.lineType = TMP_InputField.LineType.MultiLineSubmit;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterLimit = 300;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = Caret;
            input.caretWidth = Mathf.RoundToInt(2 * F.Px);
            input.transition = Selectable.Transition.None;
            input.SetTextWithoutNotify(typed.text);
            // Web 빌드의 입력창은 휴대폰 자판·한글 조합을 받지 못해 브라우저 입력창을 겹쳐 받는다(닉네임 입력과 같다).
            BuilderTools.GetOrAddComponent<BrowserImeInput>(box.gameObject);
            return input;
        }

        // L2·L3 아래: [다음](마지막 문항이면 채점 중 이름으로 바뀐다)
        private static void Footer(Transform panel, LevelTestPanelView view)
        {
            RectTransform footer = Block(panel, "ItemFooter", 22 + 60 + 47);
            Button next = ArtButton(footer, "NextButton", "LevelTestPrimary", Left, 22, Width, 60);
            TMP_Text label = ButtonLabel(next, "다음", Color.white);

            BuilderTools.SetReference(view, "itemFooter", footer.gameObject);
            BuilderTools.SetReference(view, "nextButton", next);
            BuilderTools.SetReference(view, "nextLabel", label);
        }

        // L4: 별 아이콘, 추천 난이도, 듣기·말하기 점수 칸, 피드백(없으면 숨김), [다음], 안내
        private static void Result(Transform panel, LevelTestPanelView view)
        {
            RectTransform page = F.Rect("ResultPage", panel);
            Stack(page.gameObject);
            var result = BuilderTools.GetOrAddComponent<LevelTestResultView>(page.gameObject);

            RectTransform top = Block(page, "Top", 344.44f);
            Picture(top, "Star", "LevelTestStar").rectTransform.Place(272, 43, 76, 76);
            Text(
                top,
                "Caption",
                F.BodyFontPath,
                17,
                Muted,
                TextAlignmentOptions.Center,
                "추천 난이도"
            )
                .rectTransform.Place(Left, 133, Width, 28.05f);
            TMP_Text level = Text(
                top,
                "Level",
                F.DisplayFontPath,
                64,
                LevelOrange,
                TextAlignmentOptions.Center,
                "중급"
            );
            level.rectTransform.Place(Left, 165.05f, Width, 70.39f);
            Picture(top, "Scores", "LevelTestScores")
                .rectTransform.Place(
                    Left - TileShadow.x,
                    257.44f - TileShadow.y,
                    Width + TileShadow.x + TileShadow.z,
                    87 + TileShadow.y + TileShadow.w
                );
            TMP_Text listening = Text(
                top,
                "ListeningScore",
                F.DisplayFontPath,
                28,
                Ink,
                TextAlignmentOptions.Center,
                "3 / 4"
            );
            listening.rectTransform.Place(65, 294.44f, 228, 35);
            TMP_Text speaking = Text(
                top,
                "SpeakingScore",
                F.DisplayFontPath,
                28,
                Ink,
                TextAlignmentOptions.Center,
                "2 / 2 통과"
            );
            speaking.rectTransform.Place(327, 294.44f, 228, 35);

            // 피드백 칸: 점수 칸과 14 띄우고, 테두리 1 + 안쪽 16·20, 글자 17(줄 높이 1.5)
            RectTransform slot = F.Rect("FeedbackSlot", page);
            var slotColumn = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(slot.gameObject);
            Controls(slotColumn, expandWidth: true);
            slotColumn.padding = new RectOffset(0, 0, Mathf.RoundToInt(14 * F.Px), 0);
            ContentWidth(slot);
            RectTransform feedback = F.Rect("Feedback", slot);
            var box = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(feedback.gameObject);
            Controls(box, expandWidth: true);
            float leading = HalfLeading(17, 1.5f, PretendardBox);
            box.padding = new RectOffset(
                Mathf.RoundToInt(21 * F.Px),
                Mathf.RoundToInt(21 * F.Px),
                Mathf.CeilToInt((17 + leading) * F.Px),
                Mathf.FloorToInt((17 + leading) * F.Px)
            );
            Backdrop(feedback, "LevelTestFeedback", Vector4.zero, new Vector4(58, 58, 58, 58));
            TMP_Text feedbackText = Paragraph(
                feedback,
                "Text",
                F.BodyFontPath,
                17,
                1.5f,
                Ink,
                "설명은 잘했어요. 부탁할 때 Could you…? 같은 표현을 더 연습해 보세요."
            );

            RectTransform bottom = Block(page, "Bottom", 26 + 60 + 14 + 18 + 47);
            Button next = ArtButton(bottom, "NextButton", "LevelTestPrimary", Left, 26, Width, 60);
            ButtonLabel(next, "다음", Color.white);
            Text(
                bottom,
                "Note",
                F.BodyFontPath,
                15,
                Muted,
                TextAlignmentOptions.Center,
                "다음 화면에서 추천 난이도에 배지가 붙고, 직접 바꿀 수도 있어요."
            )
                .rectTransform.Place(Left, 100, Width, 18);

            BuilderTools.SetReference(result, "levelText", level);
            BuilderTools.SetReference(result, "listeningScoreText", listening);
            BuilderTools.SetReference(result, "speakingScoreText", speaking);
            BuilderTools.SetReference(result, "feedbackSlot", slot.gameObject);
            BuilderTools.SetReference(result, "feedbackText", feedbackText);
            BuilderTools.SetReference(result, "nextButton", next);
            BuilderTools.RequireReferences(result);
            BuilderTools.SetReference(view, "resultView", result);
        }

        // 듣기 음성 재생(ONB-10)과 말하기 녹음은 창에 붙인다.
        private static void Devices(Transform panel, LevelTestPanelView view)
        {
            var source = BuilderTools.GetOrAddComponent<AudioSource>(panel.gameObject);
            source.playOnAwake = false;
            source.loop = false;
            var speech = BuilderTools.GetOrAddComponent<LevelTestSpeechPlayer>(panel.gameObject);
            BuilderTools.SetReference(
                speech,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(ApiSettingsPath, MenuPath)
            );
            BuilderTools.SetReference(speech, "speechSource", source);
            var recorder = BuilderTools.GetOrAddComponent<VoiceRecorder>(panel.gameObject);
            BuilderTools.SetReference(
                recorder,
                "settings",
                BuilderTools.LoadRequiredAsset<VoiceRecorderSettings>(
                    VoiceSettingsPath,
                    VoiceBuilderMenu
                )
            );
            BuilderTools.SetReference(view, "speechPlayer", speech);
            BuilderTools.SetReference(view, "voiceRecorder", recorder);
        }

        // 세로로 쌓는 묶음. 묶음마다 정한 폭(창 폭 또는 내용 폭)으로 가운데에 둔다.
        private static void Stack(GameObject owner)
        {
            var column = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(owner);
            Controls(column, expandWidth: false);
            column.padding = new RectOffset(0, 0, 0, 0);
            column.spacing = 0;
            column.childAlignment = TextAnchor.UpperCenter;
        }

        private static void Controls(VerticalLayoutGroup column, bool expandWidth)
        {
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = expandWidth;
            column.childForceExpandHeight = false;
        }

        // 높이가 정해진 묶음. 안의 요소는 시안 자리(창 왼쪽, 묶음 위 기준)에 둔다.
        private static RectTransform Block(Transform parent, string name, float height)
        {
            RectTransform block = F.Rect(name, parent);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(block.gameObject);
            size.minHeight = size.preferredHeight = height * F.Px;
            size.preferredWidth = CardWidth * F.Px;
            size.flexibleWidth = 0;
            return block;
        }

        // 내용 폭(512)으로 가운데에 두는 세로 배치 칸. 안의 줄이 폭을 채우려 해도 창 폭까지 늘어나지 않게 막는다.
        private static void ContentWidth(Transform owner)
        {
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(owner.gameObject);
            size.preferredWidth = Width * F.Px;
            size.flexibleWidth = 0;
        }

        // 세로 배치 칸의 바탕 그림. 배치에서 빠지고 칸 바깥 그림자 자리만큼 넓혀 깐다.
        private static void Backdrop(Transform owner, string sprite, Vector4 shadow, Vector4 border)
        {
            RectTransform art = F.Rect("Art", owner);
            art.SetAsFirstSibling();
            F.Spread(art, shadow.x, shadow.y, shadow.z, shadow.w);
            BuilderTools.GetOrAddComponent<LayoutElement>(art.gameObject).ignoreLayout = true;
            F.Sliced(F.Picture(art.gameObject, null), Art(sprite, border));
        }

        private static Image Picture(Transform parent, string name, string sprite)
        {
            return F.Picture(F.Rect(name, parent).gameObject, Art(sprite));
        }

        // 렌더링한 그림 버튼: 버튼 칸은 시안 자리이고, 그림은 그림자 자리만큼 넓혀 깐다.
        private static Button ArtButton(
            Transform parent,
            string name,
            string sprite,
            float left,
            float top,
            float width,
            float height,
            Vector4? shadow = null
        )
        {
            Vector4 pad = shadow ?? ButtonShadow;
            RectTransform rect = F.Rect(name, parent);
            rect.Place(left, top, width, height);
            Image area = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = true;
            RectTransform art = F.Rect("Art", rect);
            art.SetAsFirstSibling();
            art.Place(-pad.x, -pad.y, width + pad.x + pad.z, height + pad.y + pad.w);
            Image face = F.Picture(art.gameObject, Art(sprite));
            var button = BuilderTools.GetOrAddComponent<Button>(rect.gameObject);
            Tint(button, face);
            return button;
        }

        // 버튼 그림 색으로 눌림·올림을 보이고, 누를 수 없으면 시안의 .off처럼 45%로 흐리게 한다.
        private static void Tint(Button button, Graphic face)
        {
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.96f, .96f, .96f);
            colors.pressedColor = new Color(.86f, .86f, .86f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1, 1, 1, .45f);
            button.colors = colors;
        }

        // 버튼 이름: Jua 22, 버튼 앞면 가운데
        private static TMP_Text ButtonLabel(Button button, string value, Color color)
        {
            TMP_Text label = Text(
                button.transform,
                "Label",
                F.DisplayFontPath,
                22,
                color,
                TextAlignmentOptions.Center,
                value
            );
            F.Fill(label.rectTransform);
            return label;
        }

        // 한 줄 글자. 칸 높이는 시안의 줄 높이이고, 글자 칸을 그 가운데에 둔다.
        private static TMP_Text Text(
            Transform parent,
            string name,
            string fontPath,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            string value
        )
        {
            RectTransform rect = F.Rect(name, parent);
            var text = BuilderTools.GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
            F.Style(text, fontPath, size, color);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            text.lineSpacing = 0;
            text.richText = false;
            text.text = value;
            return text;
        }

        // 여러 줄 글자. 시안 줄 높이(lineHeight)만큼 줄 사이를 벌리고, 칸 위는 글자 위에서 시작한다.
        private static TMP_Text Paragraph(
            Transform parent,
            string name,
            string fontPath,
            float size,
            float lineHeight,
            Color color,
            string value
        )
        {
            TMP_Text text = Text(
                parent,
                name,
                fontPath,
                size,
                color,
                TextAlignmentOptions.TopLeft,
                value
            );
            text.textWrappingMode = TextWrappingModes.Normal;
            float fontLine = fontPath == F.DisplayFontPath ? JuaLine : PretendardBox;
            text.lineSpacing = (lineHeight - fontLine) * 100;
            return text;
        }

        // 시안 줄 높이에서 글자 칸 위에 남는 간격(px)
        private static float HalfLeading(float size, float lineHeight, float fontBox)
        {
            return (lineHeight - fontBox) * size / 2;
        }

        private static Sprite Art(string name, Vector4 border = default)
        {
            return EntryScreensBuilder.Sprite(name, border);
        }

        // 창 왼쪽 위 기준 시안 자리
        private static void Place(
            this RectTransform rect,
            float x,
            float y,
            float width,
            float height
        )
        {
            F.TopLeft(rect, x, y, width, height);
        }
    }
}
