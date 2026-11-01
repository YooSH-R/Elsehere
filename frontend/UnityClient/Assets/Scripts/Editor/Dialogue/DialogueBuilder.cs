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
            AddMissingMockScripts(
                BuilderTools.LoadOrCreateAsset<MockDialogueScriptSource>(MockScriptsPath)
            );
            BuilderTools.UpdateOrCreatePrefab(PanelPrefabPath, "DialoguePanel", SetUpPanel);
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
                    )
                    {
                        existing.FindPropertyRelative("roleLabel").stringValue = roleLabel;
                    }
                }
                if (existing != null)
                    ReplaceRetiredTexts(existing);
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(examples);
        }

        // 상점을 편의점에서 드럭스토어로 바꿨다. 예전 기본값 그대로인 칸만 새 이름으로 고치고 직접 고친 대사는 둔다.
        private static readonly (string Old, string Current)[] RetiredTexts =
        {
            ("편의점 직원", "드럭스토어 직원"),
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
            TMP_Text guide = UiBuilderTools.FindOrCreateText(
                help != null ? help : conversation,
                "Guide",
                regular,
                20f,
                SubTextColor
            );
            RectTransform inputRow = UiBuilderTools.FindOrCreateButtonRow(conversation, "InputRow");
            RectTransform toolRow = UiBuilderTools.FindOrCreateButtonRow(conversation, "ToolRow");
            // [힌트]를 바로 누를 수 있게 [...] 메뉴(MoreTools)에 넣었던 버튼을 도구 줄로 꺼낸다.
            Transform moreTools = toolRow.Find("MoreTools");
            if (moreTools != null)
            {
                MoveLegacy(moreTools, "ExampleButton", toolRow);
                MoveLegacy(moreTools, "SaveButton", toolRow);
                MoveLegacy(moreTools, "HintButton", toolRow);
                Object.DestroyImmediate(moreTools.gameObject);
            }
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
            Button saveButton = UiBuilderTools.FindOrCreateButton(
                toolRow,
                "SaveButton",
                "표현 저장",
                bold,
                160f
            );

            // 서버 AI 대화: 상태 문구, 추천 단어, 꾹 눌러 말하기, 다시 듣기, 힌트
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
                "꾹 눌러 말하기",
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
            BuilderTools.SetReference(view, "guideText", guide);
            BuilderTools.SetReference(view, "inputField", input);
            BuilderTools.SetReference(view, "sendButton", sendButton);
            BuilderTools.SetReference(view, "exampleButton", exampleButton);
            BuilderTools.SetReference(view, "meaningButton", meaningButton);
            BuilderTools.SetReference(view, "saveButton", saveButton);
            BuilderTools.SetReference(view, "statusText", status);
            BuilderTools.SetReference(view, "suggestText", suggest);
            BuilderTools.SetReference(
                view,
                "talkButton",
                BuilderTools.GetOrAddComponent<HoldButton>(talkButton.gameObject)
            );
            BuilderTools.SetReference(view, "replayButton", replayButton);
            BuilderTools.SetReference(view, "hintButton", hintButton);
            BuilderTools.SetReference(view, "retryButton", retryButton);
            DialogueLayoutBuilder.SetUp(root, view, lines, guide, suggest);
            // 택시 안내 문구는 택시 장면에서만 쓰며 TaxiCabinBuilder가 씬의 대화 화면에 연결한다.
            BuilderTools.RequireReferences(view, "vehicleStatusText");

            // 대화 중에만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }

        private static void MoveLegacy(Transform parent, string name, Transform destination)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                existing.SetParent(destination, false);
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
