using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Onboarding;
using A305.EditorTools.UI;
using A305.EditorTools.Voice;
using A305.Player;
using A305.Voice;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using BrowserImeInput = WebGLSupport.WebGLInput;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 대화 화면 프리팹과 확인용 대화 대본을 준비하고, 공간 씬에 대화 처리를 붙인다.
    /// 화면은 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class DialogueBuilder
    {
        public const string MenuPath = "Tools/A305/Dialogue/Build";

        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string MockScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";

        // 대화 내용 화면이 생기기 전의 임시 안내 문구. 이 문구가 그대로면 숨기고, 디자인에서 바꾼 문구는 둔다.
        private const string PlaceholderNotice =
            "대화 화면은 준비 중입니다. [대화 종료]를 누르면 대화를 끝내고 다시 움직일 수 있습니다.";

        private const string EndButtonLabel = "대화 종료 (Esc)";
        private const string OldEndButtonLabel = "대화 종료";

        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";
        private const string VoiceRecorderSettingsPath =
            "Assets/Data/Voice/VoiceRecorderSettings.asset";

        // 단어 카드의 [단어장에 저장]. 글자를 줄이지 않고 한 줄에 쓰는 폭이다.
        private const float WordSaveButtonWidth = 220f;

        // 단어 카드의 [닫기]. 64px보다 낮은 버튼은 아이콘 없이 이름만 두므로 '닫기' 글자가 들어가는 폭이다.
        private const float WordCloseButtonWidth = 76f;

        // 단어 카드 크기. 넓은 화면 대화창의 대사 위쪽 공간에 들어가는 높이다.
        private static readonly Vector2 WordCardSize = new Vector2(420f, 156f);

        private static readonly Color SubTextColor = new Color(0.8f, 0.82f, 0.86f);
        private static readonly Color StatusColor = new Color(1f, 0.85f, 0.45f);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            AssetDatabase.SaveAssets();
            Debug.Log($"대화 화면을 준비했습니다: {PanelPrefabPath}");
        }

        /// <summary>확인용 대본(없을 때만)과 대화 화면 프리팹의 기능 구성을 맞춘다. 디자인은 유지한다.</summary>
        public static void PrepareAssets()
        {
            RefreshMockScripts();
            BuilderTools.UpdateOrCreatePrefab(PanelPrefabPath, "DialoguePanel", SetUpPanel);
        }

        /// <summary>대화 UI를 재생성하지 않고 확인용 대본의 이름과 이전 기본 문구만 맞춘다.</summary>
        public static void RefreshMockScripts()
        {
            var scripts = BuilderTools.LoadOrCreateAsset<MockDialogueScriptSource>(MockScriptsPath);
            AddMissingMockScripts(scripts);
            AssetDatabase.SaveAssetIfDirty(scripts);
        }

        /// <summary>
        /// 새로 놓은 NPC가 기본 대본("Neighbor")으로 빠지지 않도록, 코드의 예시 대본 중 에셋에 없는 NPC만 더한다.
        /// 이미 있는 대본은 고친 대사를 그대로 두고, 비어 있는 역할만 채운다.
        /// 이름은 서버 시드와 같아야 하므로 이미 있는 대본도 코드의 예시 이름으로 맞춘다.
        /// </summary>
        private static void AddMissingMockScripts(MockDialogueScriptSource scripts)
        {
            var examples = ScriptableObject.CreateInstance<MockDialogueScriptSource>();
            var fields = new SerializedObject(scripts);
            SerializedProperty entries = fields.FindProperty("entries");
            SerializedProperty exampleEntries = new SerializedObject(examples).FindProperty(
                "entries"
            );
            for (int index = 0; index < exampleEntries.arraySize; index++)
            {
                SerializedProperty example = exampleEntries.GetArrayElementAtIndex(index);
                string roleLabel = example.FindPropertyRelative("roleLabel").stringValue;
                SerializedProperty existing = FindEntry(
                    entries,
                    example.FindPropertyRelative("npcCode").stringValue
                );
                if (existing == null)
                {
                    entries.InsertArrayElementAtIndex(entries.arraySize);
                    entries.GetArrayElementAtIndex(entries.arraySize - 1).boxedValue =
                        example.boxedValue;
                }
                else
                {
                    existing.FindPropertyRelative("npcName").stringValue = example
                        .FindPropertyRelative("npcName")
                        .stringValue;
                    if (
                        string.IsNullOrEmpty(existing.FindPropertyRelative("roleLabel").stringValue)
                        || (
                            example.FindPropertyRelative("npcCode").stringValue == "POLICE_ROOKIE"
                            && existing.FindPropertyRelative("roleLabel").stringValue == "경찰관"
                        )
                    )
                    {
                        existing.FindPropertyRelative("roleLabel").stringValue = roleLabel;
                    }
                }
                if (existing != null)
                {
                    ReplaceRetiredTexts(existing);
                    FillMissingSuggestWords(existing, example);
                }
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(examples);
        }

        // 인물·가게 이름이 바뀐 기본 문구만 갱신한다. 직접 고친 대사는 유지한다.
        private static readonly (string Old, string Current)[] RetiredTexts =
        {
            ("편의점 직원", "드럭스토어 직원"),
            (
                "Hello! I'm Tyler. I just started here. How can I help?",
                "Hello! I'm Taylor. I just started here. How can I help?"
            ),
            (
                "안녕하세요! 저는 Tyler예요. 여기 막 왔어요. 무엇을 도와드릴까요?",
                "안녕하세요! 저는 Taylor예요. 여기 막 왔어요. 무엇을 도와드릴까요?"
            ),
            (
                "Hi! Welcome to the convenience store. Can I help you find something?",
                "Hi! Welcome to the drugstore. Can I help you find something?"
            ),
            (
                "안녕하세요! 편의점에 오신 걸 환영해요. 찾으시는 물건이 있나요?",
                "안녕하세요! 드럭스토어에 오신 걸 환영해요. 찾으시는 물건이 있나요?"
            ),
        };

        private static void ReplaceRetiredTexts(SerializedProperty entry)
        {
            SerializedProperty field = entry.Copy();
            SerializedProperty end = entry.GetEndProperty();
            while (field.Next(true) && !SerializedProperty.EqualContents(field, end))
            {
                if (field.propertyType != SerializedPropertyType.String)
                    continue;
                foreach ((string old, string current) in RetiredTexts)
                    if (field.stringValue == old)
                        field.stringValue = current;
            }
        }

        // 추천 단어를 나중에 더했다. 이미 있는 대본은 같은 대사인 정해진 턴에서 비어 있는 추천 단어만 코드의 예시로 채운다.
        private static void FillMissingSuggestWords(
            SerializedProperty entry,
            SerializedProperty example
        )
        {
            SerializedProperty turns = entry.FindPropertyRelative("guidedTurns");
            SerializedProperty exampleTurns = example.FindPropertyRelative("guidedTurns");
            for (int index = 0; index < turns.arraySize; index++)
            {
                SerializedProperty turn = turns.GetArrayElementAtIndex(index);
                SerializedProperty words = turn.FindPropertyRelative("suggestWords");
                if (words.arraySize > 0)
                    continue;
                string line = turn.FindPropertyRelative("suggestedEnglish").stringValue;
                for (int exampleIndex = 0; exampleIndex < exampleTurns.arraySize; exampleIndex++)
                {
                    SerializedProperty exampleTurn = exampleTurns.GetArrayElementAtIndex(
                        exampleIndex
                    );
                    if (exampleTurn.FindPropertyRelative("suggestedEnglish").stringValue != line)
                        continue;
                    SerializedProperty exampleWords = exampleTurn.FindPropertyRelative(
                        "suggestWords"
                    );
                    words.arraySize = exampleWords.arraySize;
                    for (int word = 0; word < exampleWords.arraySize; word++)
                        words.GetArrayElementAtIndex(word).stringValue = exampleWords
                            .GetArrayElementAtIndex(word)
                            .stringValue;
                }
            }
        }

        private static SerializedProperty FindEntry(SerializedProperty entries, string npcCode)
        {
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("npcCode").stringValue == npcCode)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// 씬에 UI 입력, 대화 화면, 대화 처리가 없으면 붙이고 플레이어·카메라·대본과 연결한다.
        /// </summary>
        public static DialogueController ConnectToScene(
            SceneGroups groups,
            PlayerMover player,
            PlayerCameraRig cameraRig
        )
        {
            BuilderTools.EnsureEventSystem(groups);

            var panel = BuilderTools.FindSingleInScene<DialoguePanelView>(groups.Scene);
            if (panel == null)
            {
                panel = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(PanelPrefabPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<DialoguePanelView>();
            }

            var dialogue = BuilderTools.FindSingleInScene<DialogueController>(groups.Scene);
            if (dialogue == null)
            {
                dialogue = BuilderTools
                    .CreateEmpty("Dialogue", groups.Systems, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<DialogueController>();
            }

            BuilderTools.SetReference(dialogue, "player", player);
            BuilderTools.SetReference(dialogue, "cameraRig", cameraRig);
            BuilderTools.SetReference(dialogue, "panel", panel);
            BuilderTools.SetReference(
                dialogue,
                "scriptSource",
                BuilderTools.LoadRequiredAsset<MockDialogueScriptSource>(MockScriptsPath, MenuPath)
            );
            BuilderTools.RequireReferences(dialogue);
            return dialogue;
        }

        /// <summary>
        /// NPC 이름 옆의 호감도·무례 경고 칸 한 줄. 자리와 색은 화면이 이름에 맞춰 정하므로 레이아웃에서 빼고, 처음 만들 때만 숨긴다.
        /// </summary>
        private static TMP_Text FindOrCreateStanding(RectTransform panel, TMP_FontAsset font)
        {
            bool isNew = panel.Find("Standing") == null;
            TMP_Text standing = UiBuilderTools.FindOrCreateText(
                panel,
                "Standing",
                font,
                24f,
                Color.white
            );
            if (isNew)
            {
                BuilderTools.GetOrAddComponent<LayoutElement>(standing.gameObject).ignoreLayout =
                    true;
                var rect = (RectTransform)standing.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(600f, 50f);
                standing.alignment = TextAlignmentOptions.MidlineLeft;
                standing.textWrappingMode = TextWrappingModes.NoWrap;
                standing.overflowMode = TextOverflowModes.Ellipsis;
                standing.gameObject.SetActive(false);
            }

            return standing;
        }

        private static void SetUpPanel(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, sortingOrder: 120, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<DialoguePanelView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateBottomPanel(
                root.transform,
                "Panel",
                900f,
                60f
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            UiBuilderTools.FindOrCreateText(panel, "Title", bold, 30f, Color.white, "대화 중");
            TMP_Text standing = FindOrCreateStanding(panel, bold);
            TMP_Text notice = UiBuilderTools.FindOrCreateText(
                panel,
                "Notice",
                regular,
                22f,
                SubTextColor,
                PlaceholderNotice
            );
            if (notice.text == PlaceholderNotice)
            {
                notice.gameObject.SetActive(false);
            }

            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            Button endButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "EndButton",
                EndButtonLabel,
                bold
            );
            // Esc로도 끝낼 수 있음을 알린다(기능 명세 F-23). 예전 기본 문구일 때만 바꾸고 디자인에서 바꾼 문구는 둔다.
            TMP_Text endLabel = endButton.GetComponentInChildren<TMP_Text>(true);
            if (endLabel.text == OldEndButtonLabel)
            {
                endLabel.text = EndButtonLabel;
            }

            // 일시적인 장애로 시작이나 발화가 실패했을 때만 보인다. 새로 붙이면 [대화 종료] 앞에 둔다.
            bool isRetryNew = buttons.Find("RetryButton") == null;
            Button retryButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "RetryButton",
                "다시 시도",
                bold
            );
            if (isRetryNew)
            {
                retryButton.transform.SetSiblingIndex(endButton.transform.GetSiblingIndex());
                retryButton.gameObject.SetActive(false);
            }

            // 대화 내용은 [대화 종료] 줄 위에 둔다. 처음 만들 때만 자리를 정하고, 이후에는 디자인한 순서를 유지한다.
            bool isConversationNew = panel.Find("Conversation") == null;
            RectTransform conversation = UiBuilderTools.FindOrCreateColumn(panel, "Conversation");
            if (isConversationNew)
            {
                conversation.SetSiblingIndex(buttons.GetSiblingIndex());
            }

            Transform history = conversation.Find("History/Viewport");
            Transform help = root.transform.Find("HelpPanel/Viewport/Content");
            Transform words = root.transform.Find("HelpPanel/Viewport/Content/WordsCard");
            TMP_Text lines = UiBuilderTools.FindOrCreateText(
                history != null ? history : conversation,
                "Lines",
                regular,
                24f,
                Color.white
            );
            // 도움 칸은 배경 없이 장면 위에 놓여 카드 밖 안내 글씨는 읽을 수 없으므로 두지 않는다. 튜토리얼 할 일은 코치 마크 설명이 알린다.
            Transform oldGuide = help != null ? help.Find("Guide") : null;
            if (oldGuide != null)
            {
                Object.DestroyImmediate(oldGuide.gameObject);
            }

            RectTransform inputRow = UiBuilderTools.FindOrCreateButtonRow(conversation, "InputRow");
            RectTransform toolRow = UiBuilderTools.FindOrCreateButtonRow(conversation, "ToolRow");
            // [힌트]를 바로 누를 수 있게 [...] 메뉴(MoreTools)에 넣었던 버튼을 도구 줄로 꺼낸다.
            Transform moreTools = toolRow.Find("MoreTools");
            if (moreTools != null)
            {
                MoveLegacy(moreTools, "ExampleButton", toolRow);
                MoveLegacy(moreTools, "HintButton", toolRow);
                Object.DestroyImmediate(moreTools.gameObject);
            }
            // 대사 전체를 담던 [담기]는 서버가 문장을 저장하지 않아 지우고, 대사의 풀이 단어를 눌러 저장한다(단어 카드).
            Transform sentenceSave = toolRow.Find("SaveButton");
            if (sentenceSave != null)
                Object.DestroyImmediate(sentenceSave.gameObject);
            MoveLegacy(inputRow, "ExampleButton", toolRow);
            MoveLegacy(toolRow, "TalkButton", inputRow);
            TMP_InputField input = UiBuilderTools.FindOrCreateInputField(
                inputRow,
                "Input",
                regular,
                "영어로 입력해 보세요 (Enter로 보내기)"
            );
            Button sendButton = UiBuilderTools.FindOrCreateButton(
                inputRow,
                "SendButton",
                "보내기",
                bold,
                160f
            );
            Button exampleButton = UiBuilderTools.FindOrCreateButton(
                toolRow,
                "ExampleButton",
                "예시 문장",
                bold,
                160f
            );
            Button meaningButton = UiBuilderTools.FindOrCreateButton(
                toolRow,
                "MeaningButton",
                "뜻 보기",
                bold,
                160f
            );
            // 대사의 밑줄 친 풀이 단어를 누를 수 있게 대사 글자가 클릭을 받는다.
            lines.raycastTarget = true;
            var lineLinks = BuilderTools.GetOrAddComponent<TextLinkClicks>(lines.gameObject);
            RectTransform wordCard = FindOrCreateWordCard(conversation, regular, bold);

            // 서버 AI 대화: 상태 문구, 추천 단어, 말하기, 다시 듣기, 힌트
            TMP_Text status = UiBuilderTools.FindOrCreateText(
                conversation,
                "Status",
                regular,
                20f,
                StatusColor
            );
            TMP_Text suggest = UiBuilderTools.FindOrCreateText(
                words != null ? words : conversation,
                "SuggestWords",
                regular,
                20f,
                SubTextColor
            );
            Button talkButton = UiBuilderTools.FindOrCreateButton(
                inputRow,
                "TalkButton",
                "말하기",
                bold,
                200f
            );
            Button replayButton = UiBuilderTools.FindOrCreateButton(
                toolRow,
                "ReplayButton",
                "다시 듣기",
                bold,
                160f
            );
            Button hintButton = UiBuilderTools.FindOrCreateButton(
                toolRow,
                "HintButton",
                "힌트",
                bold,
                160f
            );

            // Web 빌드의 Unity 입력창은 한글 조합을 받지 못해 브라우저 입력창을 겹쳐 받는다(닉네임 입력창과 같은 방식).
            BuilderTools.GetOrAddComponent<BrowserImeInput>(input.gameObject);
            SetUpServerLink(root);

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(
                view,
                "npcNameText",
                panel.Find("Title").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(view, "standingText", standing);
            BuilderTools.SetReference(view, "endButton", endButton);
            BuilderTools.SetReference(view, "linesText", lines);
            BuilderTools.SetReference(view, "inputField", input);
            BuilderTools.SetReference(view, "sendButton", sendButton);
            BuilderTools.SetReference(view, "exampleButton", exampleButton);
            BuilderTools.SetReference(view, "meaningButton", meaningButton);
            BuilderTools.SetReference(view, "lineLinks", lineLinks);
            BuilderTools.SetReference(view, "wordCard", wordCard);
            BuilderTools.SetReference(view, "wordTitleText", CardPart<TMP_Text>(wordCard, "Title"));
            BuilderTools.SetReference(
                view,
                "wordMeaningText",
                CardPart<TMP_Text>(wordCard, "Meaning")
            );
            BuilderTools.SetReference(view, "wordNoteText", CardPart<TMP_Text>(wordCard, "Note"));
            BuilderTools.SetReference(
                view,
                "wordSaveButton",
                CardPart<Button>(wordCard, "WordSaveButton")
            );
            BuilderTools.SetReference(
                view,
                "wordCloseButton",
                CardPart<Button>(wordCard, "CloseButton")
            );
            BuilderTools.SetReference(view, "statusText", status);
            BuilderTools.SetReference(view, "suggestText", suggest);
            SetUpTalkButton(view, talkButton);
            BuilderTools.SetReference(view, "replayButton", replayButton);
            BuilderTools.SetReference(view, "hintButton", hintButton);
            BuilderTools.SetReference(view, "retryButton", retryButton);
            DialogueLayoutBuilder.SetUp(root, view, lines, suggest);
            DialogueWaitBuilder.SetUp(root, view);
            DialogueSubtitleBuilder.SetUp(root, view);
            VoiceConsentDialogBuilder.SetUp(root, view);
            // 위 기능 구성은 카드 여백·글자 크기를 기본값으로 다시 정하므로, 마지막에 승인된 배치를 다시 적용해
            // 대화창 Builder만 실행해도 단어·문장 카드 글자가 테두리에 붙지 않게 한다.
            DialogueDockBuilder.Apply(root);
            // 택시 안내 문구는 택시 장면에서만 쓰며 TaxiCabinBuilder가 씬의 대화 화면에 연결한다.
            BuilderTools.RequireReferences(view, "vehicleStatusText");

            // 대화 중에만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 대사에서 누른 풀이 단어 근처에 뜨는 호버 카드. 단어·뜻·저장 안내와 [단어장에 저장]·닫기(X)가 있다.
        /// 자리는 <see cref="DialoguePanelView"/>가 누른 단어에 맞춰 정하므로 레이아웃에서 빼고, 대사 위에 겹쳐 보이도록 맨 앞에 둔다.
        /// 단어를 누르기 전에는 숨긴다. 모양은 <see cref="DialogueDockBuilder"/>가 정한다.
        /// </summary>
        private static RectTransform FindOrCreateWordCard(
            RectTransform conversation,
            TMP_FontAsset regular,
            TMP_FontAsset bold
        )
        {
            // 대사 칸 아래에 붙여 두던 단어 줄은 호버 카드로 바꿨다.
            Transform oldRow = conversation.Find("WordRow");
            if (oldRow != null)
                Object.DestroyImmediate(oldRow.gameObject);
            Transform existing = conversation.Find("WordCard");
            RectTransform card =
                existing != null
                    ? (RectTransform)existing
                    : new GameObject(
                        "WordCard",
                        typeof(RectTransform)
                    ).GetComponent<RectTransform>();
            card.SetParent(conversation, false);
            card.SetAsLastSibling();
            BuilderTools.GetOrAddComponent<LayoutElement>(card.gameObject).ignoreLayout = true;
            card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
            card.sizeDelta = WordCardSize;
            // 카드 바탕은 카드 자신이 칠한다. 예전 테마에서 반투명한 카드 아래에 깔던 받침·카드 면·색 층은 지운다.
            foreach (string layer in new[] { "Backing", "Face", "Tint" })
            {
                Transform old = card.Find(layer);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }

            // 단어가 길어도 오른쪽 위 [닫기]에 걸리지 않는 폭이다.
            CardText(
                card,
                "Title",
                bold,
                30f,
                new Vector2(0, 1),
                new Vector2(24, -16),
                WordCardSize.x - WordCloseButtonWidth - 48
            );
            CardText(card, "Meaning", regular, 22f, new Vector2(0, 1), new Vector2(24, -58), 372);
            TMP_Text note = UiBuilderTools.FindOrCreateText(
                card,
                "Note",
                regular,
                18f,
                Color.white
            );
            var noteRect = (RectTransform)note.transform;
            noteRect.anchorMin = noteRect.pivot = Vector2.zero;
            noteRect.anchorMax = new Vector2(1, 0);
            noteRect.offsetMin = new Vector2(24, 16);
            noteRect.offsetMax = new Vector2(-(WordSaveButtonWidth + 28), 60);
            note.alignment = TextAlignmentOptions.MidlineLeft;
            note.textWrappingMode = TextWrappingModes.Normal;
            note.enableAutoSizing = true;
            note.fontSizeMin = 14;
            note.fontSizeMax = 18;

            Button save = UiBuilderTools.FindOrCreateButton(
                card,
                "WordSaveButton",
                "단어장에 저장",
                bold,
                WordSaveButtonWidth
            );
            Corner(
                save.transform,
                new Vector2(1, 0),
                new Vector2(-16, 16),
                WordSaveButtonWidth,
                44
            );
            Button close = UiBuilderTools.FindOrCreateButton(
                card,
                "CloseButton",
                "닫기",
                bold,
                WordCloseButtonWidth
            );
            Corner(close.transform, Vector2.one, new Vector2(-12, -12), WordCloseButtonWidth, 44);

            card.gameObject.SetActive(false);
            return card;
        }

        private static void CardText(
            RectTransform card,
            string name,
            TMP_FontAsset font,
            float size,
            Vector2 corner,
            Vector2 position,
            float width
        )
        {
            TMP_Text text = UiBuilderTools.FindOrCreateText(card, name, font, size, Color.white);
            Corner(text.transform, corner, position, width, size + 10);
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 카드 모서리(corner)를 기준으로 붙인다.
        private static void Corner(
            Transform target,
            Vector2 corner,
            Vector2 position,
            float width,
            float height
        )
        {
            BuilderTools.GetOrAddComponent<LayoutElement>(target.gameObject).ignoreLayout = true;
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static T CardPart<T>(RectTransform card, string name)
            where T : Component => card.Find(name).GetComponent<T>();

        private static void MoveLegacy(Transform parent, string name, Transform destination)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                existing.SetParent(destination, false);
        }

        /// <summary>[말하기]를 한 번 눌러 녹음을 시작하고 다시 눌러 끝내는 버튼과 녹음 중 켜는 불을 연결한다.</summary>
        public static void SetUpTalkButton(DialoguePanelView view, Button talkButton)
        {
            // 누르고 있는 동안만 녹음하던 HoldButton을 지웠으므로, 버튼에 남은 빈 스크립트 칸을 없앤다.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(talkButton.gameObject);
            TMP_Text caption = talkButton.transform.Find("Label").GetComponent<TMP_Text>();
            caption.text = "말하기";
            BuilderTools.SetReference(view, "talkButton", talkButton);
            BuilderTools.SetReference(view, "talkCaption", caption);
            BuilderTools.SetReference(view, "voiceLight", FindOrCreateVoiceLight(talkButton));
        }

        // 녹음 중 [종료] 오른쪽에 켜는 동그란 불. 켜기와 색은 DialoguePanelView가 정하고 평소에는 꺼 둔다.
        private static Image FindOrCreateVoiceLight(Button talkButton)
        {
            Transform existing = talkButton.transform.Find("VoiceLight");
            Image light =
                existing != null
                    ? existing.GetComponent<Image>()
                    : new GameObject(
                        "VoiceLight",
                        typeof(RectTransform),
                        typeof(Image)
                    ).GetComponent<Image>();
            light.transform.SetParent(talkButton.transform, false);
            light.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            light.raycastTarget = false;
            var rect = (RectTransform)light.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(26f, 26f);
            rect.anchoredPosition = new Vector2(-32f, 0f);
            // 주황 버튼 위에서도 불이 또렷하게 보이도록 흰 테두리를 두른다.
            var ring = BuilderTools.GetOrAddComponent<Outline>(light.gameObject);
            ring.effectColor = Color.white;
            ring.effectDistance = new Vector2(2f, -2f);
            light.gameObject.SetActive(false);
            return light;
        }

        // 서버 AI 대화 연결(API 주소, NPC 음성, 녹음)은 화면 프리팹에 두어 공간 씬을 다시 만들지 않아도 모든 씬에 붙는다.
        private static void SetUpServerLink(GameObject root)
        {
            var link = BuilderTools.GetOrAddComponent<DialogueServerLink>(root);
            var speech = BuilderTools.GetOrAddComponent<AudioSource>(root);
            speech.playOnAwake = false;
            var recorder = BuilderTools.GetOrAddComponent<VoiceRecorder>(root);
            BuilderTools.SetReference(
                recorder,
                "settings",
                BuilderTools.LoadRequiredAsset<VoiceRecorderSettings>(
                    VoiceRecorderSettingsPath,
                    VoiceBuilder.MenuPath
                )
            );
            BuilderTools.SetReference(
                link,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(
                    ApiSettingsPath,
                    OnboardingBuilder.MenuPath
                )
            );
            BuilderTools.SetReference(link, "speechSource", speech);
            BuilderTools.SetReference(link, "voiceRecorder", recorder);
            BuilderTools.RequireReferences(link);
        }
    }
}
