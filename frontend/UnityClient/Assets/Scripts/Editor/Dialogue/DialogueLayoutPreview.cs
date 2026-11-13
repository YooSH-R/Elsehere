using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Npc;
using A305.Player;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>실제 NPC와 고정 대본으로 대화 구도·도움말 배치를 확인한다. Play·모델 호출 없음.</summary>
    public static class DialogueLayoutPreview
    {
        public static void CaptureSuggestions()
        {
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            var content = new WordPreviewContent
            {
                PreviewLines = new[]
                {
                    DialogueLine.FromUser("I'd like an iced latte, please."),
                    DialogueLine.FromNpc("Sure! For here or to go?", "드시고 가세요, 포장하세요?"),
                    DialogueLine.FromUser("For here, please."),
                    DialogueLine.FromNpc(
                        "Of course. Would you like anything else?",
                        "더 필요한 건 있으세요?"
                    ),
                },
            };
            view.Show(content);
            string output = OutputFolder();
            SelectTheme(UiThemeId.Jelly);
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "suggestions-hidden.png"),
                1920,
                1080
            );
            Invoke(view, "PressHint");
            RenderThemes(camera, view, eye, face, output, "suggestions-visible");
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "suggestions-short.png"),
                844,
                390
            );
            view.SetReviewReference(
                "분실한 파란 가방을 카페 직원에게 물어보고, 가방의 특징을 설명해 보세요.\n\n이번에 써 볼 표현\nHas anyone turned in a blue bag?\n파란 가방 맡겨진 거 있나요?"
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "suggestions-review.png"),
                1920,
                1080
            );
        }

        /// <summary>[다시 연습] 대화 위 참고 카드. 고친 문장 구역과 상황 표현 구역, 말한 문장의 배지를 세 테마·낮은 화면으로 그린다.</summary>
        public static void CaptureRetryReference()
        {
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            view.Show(
                new WordPreviewContent
                {
                    PreviewLines = new[]
                    {
                        DialogueLine.FromNpc(
                            "Hi there! What can I get for you?",
                            "안녕하세요! 무엇을 드릴까요?"
                        ),
                        DialogueLine.FromUser("Can I get that to go?"),
                        DialogueLine.FromNpc("Sure! Anything to drink?", "그럼요! 음료는요?"),
                    },
                }
            );
            view.SetReviewReference(
                new[]
                {
                    new ReviewReferenceItem(
                        "Can I get that to go?",
                        "포장해 주세요",
                        true,
                        "포장해 주세요"
                    ),
                    new ReviewReferenceItem(
                        "I'd like an iced coffee, please.",
                        "주문할 때는 I'd like로 시작하면 더 부드러워요.",
                        false,
                        "I want drink iced coffee"
                    ),
                    new ReviewReferenceItem(
                        "Could I get a little less ice?",
                        "얼음 조금만 넣어 주실 수 있나요?",
                        false
                    ),
                },
                1,
                "<b>음료 주문하기</b>\n카페 · 손님\n\n카페에서 음료를 주문하고 포장을 부탁해요.\n",
                listFirst: true
            );
            string output = OutputFolder();
            RenderThemes(camera, view, eye, face, output, "retry-reference");
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "retry-reference-short.png"),
                844,
                390
            );
            // 상황 설명은 문장 목록 아래에 있어 카드를 끝까지 내린 모습도 찍는다.
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "retry-reference-list.png"),
                1920,
                1080,
                showLatest: true
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "retry-reference-short-list.png"),
                844,
                390,
                showLatest: true
            );
        }

        public static void CaptureFourLines()
        {
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            view.Show(
                new WordPreviewContent
                {
                    PreviewLines = new[]
                    {
                        DialogueLine.FromNpc(
                            "Good morning! What can I get for you?",
                            "안녕하세요! 무엇을 드릴까요?"
                        ),
                        DialogueLine.FromUser("I'd like an iced latte, please."),
                        DialogueLine.FromNpc(
                            "Sure! For here or to go?",
                            "좋아요! 드시고 가세요, 포장하세요?"
                        ),
                        DialogueLine.FromUser("For here, please."),
                        DialogueLine.FromNpc(
                            "Of course. Would you like anything else?",
                            "그럼요. 더 필요한 건 있으세요?"
                        ),
                    },
                }
            );
            typeof(DialoguePanelView)
                .GetMethod("ToggleMeaning", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            SelectTheme(UiThemeId.Jelly);
            string output = OutputFolder();
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "four-lines-desktop.png"),
                1920,
                1080,
                showLatest: true
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "four-lines-short.png"),
                844,
                390,
                showLatest: true
            );
        }

        public static void CaptureTranslation()
        {
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            var content = new WordPreviewContent { UserTranslation = "Where is the cafe?" };
            view.Show(content);
            SelectTheme(UiThemeId.Jelly);
            string output = OutputFolder();
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "translation-wide.png"),
                1920,
                1080
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "translation-portrait.png"),
                900,
                1400
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "translation-mobile.png"),
                844,
                390
            );
            // 길게 말한 표현은 카드가 문장에 맞춰 커져 두 번째 줄부터 잘리지 않는지 본다.
            content.UserTranslation =
                "You have no conscience, do you? I should've just left you to die. "
                + "What were you even thinking back there?";
            view.Refresh();
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "translation-long-wide.png"),
                1920,
                1080
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "translation-long-portrait.png"),
                900,
                1400
            );
            // 카드 상한보다 긴 표현은 오른쪽 막대가 보이고, 끝까지 내리면 마지막 줄이 보인다.
            content.UserTranslation =
                "You have no conscience, do you? I should've just left you to die. "
                + "What were you even thinking back there? I waited for you at the bus stop "
                + "for almost an hour in the rain, and you didn't even call me once. "
                + "Next time, please tell me before you change your plans, because I really "
                + "worried about you and I almost called the police.";
            view.Refresh();
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "translation-overflow-top.png"),
                1920,
                1080
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "translation-overflow-end.png"),
                1920,
                1080,
                showLatest: true
            );
            // 영어로 틀리게 한 말에는 교정문이 오고, 카드 제목이 다시 말해 보도록 바뀐다.
            content.PreviewLines = new[]
            {
                DialogueLine.FromNpc("For here or to go?", null),
                DialogueLine.FromUser("I eat here. No onion please, I don't like."),
                DialogueLine.FromNpc("Sure, no onions. Anything to drink?", null),
            };
            content.UserTranslation = "I'll eat here. No onions, please. I don't like them.";
            view.Refresh();
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "correction-wide.png"),
                1920,
                1080
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "correction-portrait.png"),
                900,
                1400
            );
        }

        /// <summary>
        /// 한국어로 길게 주고받은 대화. 영어 표현 카드 때문에 대사 칸이 좁아져 줄이 늘어도 단어·추천 문장 카드가 눌리지 않는지 본다.
        /// </summary>
        public static void CaptureKoreanLongLines()
        {
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            view.Show(
                new WordPreviewContent
                {
                    UserTranslation =
                        "I'm the police commissioner. I'm the one who can approve it.",
                    PreviewLines = new[]
                    {
                        DialogueLine.FromUser(
                            "폐지하겠네 다른 경찰서에서 인력 충원하는 거로 하겠어 다들 제 식구 감싸기에 바빠서 자기 할 일을 못하는구만"
                        ),
                        DialogueLine.FromNpc(
                            "Sir, closing this station needs official approval. I can record your complaint; what happened, and where?",
                            null
                        ),
                        DialogueLine.FromUser("내가 치안총감일세 내가 곧 공식 승인자야"),
                        DialogueLine.FromNpc(
                            "Right, by the book. I need to verify your credentials first, sir.",
                            "네, 규정대로 하겠습니다. 먼저 신원 확인을 해야 합니다, 선생님."
                        ),
                    },
                }
            );
            Invoke(view, "ToggleMeaning");
            SelectTheme(UiThemeId.Jelly);
            string output = OutputFolder();
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "korean-long-lines.png"),
                1920,
                1080,
                showLatest: true
            );
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "korean-long-lines-short.png"),
                1920,
                720,
                showLatest: true
            );
            // [힌트]를 눌러 펼친 추천 문장과 뜻이 눌린 카드 안에서도 읽히는지 본다.
            Invoke(view, "PressHint");
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "korean-long-lines-hint.png"),
                1920,
                1080
            );
        }

        // 영어 표현 카드(S15P31A305-862): 맞게 말함·틀리게 말함·한국어 섞음의 접힌 카드, 긴 발화의 펼친 카드와 끝까지 내린 카드,
        // 세로 화면의 펼친 카드. 카드는 내용만큼만 차지하고 대화창을 늘리지 않으며, [더 보기]·[접기]는 늘 카드 아래에 보인다.
        public static void CaptureOtherExpressions()
        {
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            SelectTheme(UiThemeId.Jelly);
            string output = OutputFolder();
            DialogueLine npc = DialogueLine.FromNpc("Hi there! What can I get for you?", null);
            var content = new WordPreviewContent();
            // 대화 중반: 앞선 대사가 쌓여 대사 칸이 높아진 장면
            DialogueLine[] earlier =
            {
                DialogueLine.FromNpc("Good morning! Are you new around here?", null),
                DialogueLine.FromUser("Yes, I moved here last week."),
                npc,
            };
            void Capture(
                string file,
                string said,
                string expression,
                string[] others,
                bool open,
                int width = 1920,
                int height = 1080,
                bool showLatest = false,
                bool midConversation = false,
                bool scrollToEnd = false
            )
            {
                content.PreviewLines = (midConversation ? earlier : new[] { npc })
                    .Append(DialogueLine.FromUser(said))
                    .ToArray();
                content.UserTranslation = expression;
                // 새 목록으로 넘겨 매번 접힌 카드에서 시작한다.
                content.UserBetterExpressions = others?.ToArray();
                view.Show(content);
                if (open)
                    Invoke(view, "ToggleOtherExpressions");
                if (scrollToEnd)
                {
                    Canvas.ForceUpdateCanvases();
                    view
                        .transform.Find("Panel/UserTranslation")
                        .GetComponent<ScrollRect>()
                        .verticalNormalizedPosition = 0;
                }
                Render(
                    camera,
                    view,
                    eye,
                    face,
                    Path.Combine(output, file),
                    width,
                    height,
                    showLatest
                );
            }

            Capture(
                "others-wellsaid-closed.png",
                "Can I get an iced americano, please?",
                null,
                new[] { "I'd like an iced americano, please.", "Can I have an iced americano?" },
                false
            );
            Capture(
                "others-correction-closed.png",
                "i want to speak korea",
                "I want to speak Korean.",
                new[] { "I'd like to speak Korean." },
                false
            );
            Capture(
                "others-korean-closed.png",
                "한국어로 대화하자. i want korea",
                "Let's talk in Korean. I want to speak Korean.",
                new[]
                {
                    "Can we talk in Korean? I'd like to speak Korean.",
                    "Could we use Korean instead?",
                },
                false
            );
            string[] longOthers =
            {
                "I tried to buy two loaves of bread at the bakery yesterday, but it was closed, so I went back home.",
                "Yesterday I went to get some bread at the bakery, but it was closed, so I just came home.",
                "I wanted to pick up two loaves at the bakery yesterday, but it was already closed, so I headed home.",
            };
            const string longSaid =
                "yesterday i go to the bakery and buy two bread, but it was close so i come back home";
            const string longFixed =
                "Yesterday I went to the bakery and bought two loaves of bread, but it was closed, so I came back home.";
            Capture("others-long-closed.png", longSaid, longFixed, longOthers, false);
            Capture("others-long-open.png", longSaid, longFixed, longOthers, true);
            Capture(
                "others-long-open-portrait.png",
                longSaid,
                longFixed,
                longOthers,
                true,
                900,
                1400
            );
            // 낮은 화면: 대화창 최대 높이에 걸려 다음 문장 칸이 반쯤 보인다.
            Capture(
                "others-long-open-short-screen.png",
                longSaid,
                longFixed,
                longOthers,
                true,
                1920,
                720
            );
            Capture(
                "others-long-open-end.png",
                longSaid,
                longFixed,
                longOthers,
                true,
                scrollToEnd: true
            );
            Capture(
                "others-long-open-mid.png",
                longSaid,
                longFixed,
                longOthers,
                true,
                midConversation: true
            );
            Capture(
                "others-long-open-mid-end.png",
                longSaid,
                longFixed,
                longOthers,
                true,
                midConversation: true,
                scrollToEnd: true
            );
        }

        public static void Capture()
        {
            string output = OutputFolder();
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            var source = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            if (!source.TryGetScript("CAFE_STAFF", out DialogueScript script))
                throw new System.InvalidOperationException("Missing cafe script.");
            var conversation = new DialogueConversation("CAFE_STAFF", script);
            conversation.Send("I'd like an iced latte, please.");
            view.Show(conversation);
            FillHelp(view);
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                SelectTheme(theme);
                Render(
                    camera,
                    view,
                    eye,
                    face,
                    Path.Combine(output, $"dialogue-{theme}.png"),
                    1920,
                    1080
                );
            }
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "dialogue-portrait.png"),
                900,
                1400
            );
        }

        /// <summary>
        /// [뜻]을 켠 대사의 풀이 단어를 눌렀을 때 단어 근처에 뜨는 호버 카드를 찍는다. 저장 전·저장 뒤·숙어,
        /// 숙어 안의 단어(단어와 숙어를 쌓은 카드, 아래로 스크롤한 카드)와 세로 화면이다.
        /// 서버 호출 없이 고정 대사와 풀이를 쓴다.
        /// </summary>
        public static void CaptureWords()
        {
            string output = OutputFolder();
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            var content = new WordPreviewContent();
            view.Show(content);
            FillHelp(view);
            Invoke(view, "ToggleMeaning");
            ShowWord(view, content, WordPreviewContent.LatteLink);
            Render(camera, view, eye, face, Path.Combine(output, "words.png"), 1920, 1080);
            view.ShowWordSaveResult(
                DialogueLinesText.FindWords(content.Lines, WordPreviewContent.LatteLink)[0],
                true,
                "단어장에 저장했어요."
            );
            Render(camera, view, eye, face, Path.Combine(output, "words-saved.png"), 1920, 1080);
            ShowWord(view, content, WordPreviewContent.ToGoLink);
            Render(camera, view, eye, face, Path.Combine(output, "words-idiom.png"), 1920, 1080);
            // 숙어(to go) 안의 단어(go)를 누르면 단어와 숙어가 함께 쌓이고, 아래로 스크롤해 숙어를 본다.
            ShowWord(view, content, WordPreviewContent.GoLink);
            Render(camera, view, eye, face, Path.Combine(output, "words-overlap.png"), 1920, 1080);
            Field<ScrollRect>(view, "wordEntries").verticalNormalizedPosition = 0;
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "words-overlap-scrolled.png"),
                1920,
                1080
            );
            ShowWord(view, content, WordPreviewContent.LatteLink);
            Render(camera, view, eye, face, Path.Combine(output, "words-portrait.png"), 900, 1400);
        }

        /// <summary>
        /// 자막을 켜고 껐을 때 테두리와 버튼 여백을 비교한다.
        /// 첫 대사 준비 중 [다시 듣기]와 다음 대사를 기다릴 때의 단어 막대도 확인한다.
        /// 이 기기의 자막 설정은 찍은 뒤 되돌린다.
        /// </summary>
        public static void CaptureSubtitles()
        {
            string output = OutputFolder();
            bool savedPreference = NpcSubtitlePreference.ShowsSubtitles;
            UiThemeId savedTheme = UiThemeSelection.Shared.Current;
            try
            {
                DialoguePanelView view = OpenCafe(
                    out Camera camera,
                    out Vector3 eye,
                    out Vector3 face
                );
                foreach (bool showsSubtitles in new[] { false, true })
                {
                    // 이전에 켜 둔 기록이 있어도 새 대화에서는 가려지는지 함께 확인한다.
                    NpcSubtitlePreference.ShowsSubtitles = true;
                    var content = new WordPreviewContent();
                    view.Show(content);
                    if (showsSubtitles)
                    {
                        NpcSubtitlePreference.ShowsSubtitles = true;
                        view.Refresh();
                    }
                    FillHelp(view);
                    string state = showsSubtitles ? "on" : "off";
                    RenderThemes(camera, view, eye, face, output, $"subtitles-{state}");
                    Render(
                        camera,
                        view,
                        eye,
                        face,
                        Path.Combine(output, $"subtitles-{state}-mobile.png"),
                        844,
                        390
                    );
                    if (showsSubtitles)
                        continue;
                    content.Waiting = DialogueWait.NpcReply;
                    view.Refresh();
                    RenderThemes(camera, view, eye, face, output, "subtitles-off-waiting");
                }
                view.Show(new WordPreviewContent { Waiting = DialogueWait.Opening });
                Render(
                    camera,
                    view,
                    eye,
                    face,
                    Path.Combine(output, "subtitles-opening.png"),
                    1920,
                    1080
                );
                Render(
                    camera,
                    view,
                    eye,
                    face,
                    Path.Combine(output, "subtitles-opening-mobile.png"),
                    844,
                    390
                );
            }
            finally
            {
                NpcSubtitlePreference.ShowsSubtitles = savedPreference;
                SelectTheme(savedTheme);
            }
        }

        private static void RenderThemes(
            Camera camera,
            DialoguePanelView view,
            Vector3 eye,
            Vector3 face,
            string output,
            string name
        )
        {
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                SelectTheme(theme);
                Render(
                    camera,
                    view,
                    eye,
                    face,
                    Path.Combine(output, $"{name}-{theme}.png"),
                    1920,
                    1080
                );
            }
        }

        private static string OutputFolder()
        {
            string output = System.Environment.GetEnvironmentVariable("DIALOGUE_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new System.InvalidOperationException("Set DIALOGUE_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            return output;
        }

        // 카페 씬의 대화 화면만 켜고 카메라 앞에 그린다. 플레이어 모델은 숨긴다.
        internal static DialoguePanelView OpenCafe(
            out Camera camera,
            out Vector3 eye,
            out Vector3 face
        )
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/CafeScene.unity");
            var view = BuilderTools.FindSingleInScene<DialoguePanelView>(scene);
            camera = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene).GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                canvas.gameObject.SetActive(canvas.gameObject == view.gameObject);
                if (canvas.gameObject != view.gameObject)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            foreach (
                Renderer renderer in BuilderTools
                    .FindSingleInScene<PlayerMover>(scene)
                    .GetComponentsInChildren<Renderer>()
            )
                renderer.enabled = false;
            var npc = BuilderTools.FindAllInScene<NpcIdentity>(scene).First();
            face = npc.LookTarget.position;
            eye = new Vector3(0, 1.1576f, 2.1f);
            foreach (
                Renderer renderer in BuilderTools
                    .FindSingleInScene<PlayerMover>(scene)
                    .GetComponentsInChildren<Renderer>()
            )
                renderer.forceRenderingOff = true;
            return view;
        }

        // 서버 대화처럼 추천 단어, 상태 줄과 [말하기]·[다시 듣기]·[힌트]를 채운다.
        private static void FillHelp(DialoguePanelView view)
        {
            Field<TMP_Text>(view, "suggestText").text =
                "• iced latte\n• oat milk\n• for here\n• to go";
            Field<TMP_Text>(view, "statusText").text = "Mia가 듣고 있어요";
            Field<MonoBehaviour>(view, "talkButton").gameObject.SetActive(true);
            Field<Button>(view, "replayButton").gameObject.SetActive(true);
            Field<Button>(view, "hintButton").gameObject.SetActive(true);
        }

        private static void SelectTheme(UiThemeId theme)
        {
            UiThemeSelection.Shared.Select(theme);
            foreach (
                ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(
                    SceneManager.GetActiveScene()
                )
            )
                element.ApplyCurrent();
        }

        // 대사 칸의 밑줄 링크(대사 번호/풀이 번호)를 누른 것처럼 단어를 고른다. 이미 고른 단어면 다시 눌러 닫지 않게 그대로 둔다.
        private static void ShowWord(
            DialoguePanelView view,
            IDialogueContent content,
            string linkId
        )
        {
            var card = (GlossaryWordCard)
                typeof(DialoguePanelView)
                    .GetField("glossaryCard", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view);
            if (card.Word != DialogueLinesText.FindWords(content.Lines, linkId).FirstOrDefault())
                Invoke(view, "SelectWord", linkId);
        }

        private static void Invoke(DialoguePanelView view, string method, params object[] arguments)
        {
            typeof(DialoguePanelView)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, arguments);
        }

        private static T Field<T>(Component owner, string field)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;

        private static void Render(
            Camera camera,
            DialoguePanelView view,
            Vector3 eye,
            Vector3 face,
            string path,
            int width,
            int height,
            bool showLatest = false
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(
                "Assets/Data/Cafe/CafeCameraSettings.asset"
            );
            eye = DialogueFraming.CameraPosition(
                face,
                eye,
                settings.DialogueDistanceMeters,
                settings.DialogueHeightOffsetMeters
            );
            Pose pose = DialogueFraming.Pose(
                eye,
                face,
                camera.fieldOfView,
                camera.aspect,
                settings.DialogueFacePosition
            );
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            camera.Render();
            Canvas.ForceUpdateCanvases();
            view.UpdateLayout();
            Canvas.ForceUpdateCanvases();
            // 스크롤 막대 손잡이 크기는 Play 중 매 프레임(LateUpdate)에만 맞춰지므로 찍기 전에 한 번 맞춘다.
            // Editor에서는 ScrollRect가 늘 스크롤할 것이 있다고 보므로, Play처럼 내용이 넘칠 때만 막대를 보인다.
            foreach (ScrollRect scroll in view.GetComponentsInChildren<ScrollRect>())
            {
                if (showLatest)
                    scroll.verticalNormalizedPosition = 0;
                // 옮긴 스크롤 위치에 막대 손잡이도 맞춘다.
                typeof(ScrollRect)
                    .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scroll, null);
                if (scroll.verticalScrollbar != null)
                    scroll.verticalScrollbar.gameObject.SetActive(
                        scroll.content.rect.height > scroll.viewport.rect.height + 0.01f
                    );
            }
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Vector3 screen = camera.WorldToViewportPoint(face);
            if (screen.y < .6f)
                throw new System.InvalidOperationException("NPC face is covered by dialogue area.");
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }

        /// <summary>
        /// [뜻 보기]로 뜻과 풀이를 받아 둔 카페 서버 대화. 마지막 Mia 대사에 한 단어(latte), 숙어(to go)와 숙어 안의 단어(go) 풀이가 있다.
        /// 서버 대화처럼 NPC 대사를 음성으로 들려주므로 [자막]을 끄면 대사를 가린다.
        /// </summary>
        private sealed class WordPreviewContent : IDialogueContent
        {
            public const string LatteLink = "2/0";

            // to go의 "to " 조각. 숙어만 덮는다.
            public const string ToGoLink = "2/1";

            // to go의 "go" 조각. 단어 go와 숙어 to go가 함께 덮는다.
            public const string GoLink = "2/2,1";

            private const long TurnId = 105;

            private static readonly DialogueLine Question = DialogueLine.FromNpc(
                "Sure! Would you like your iced latte for here or to go?",
                "그럼요! 아이스 라떼는 드시고 가세요, 포장하세요?",
                new[]
                {
                    new GlossaryWord(TurnId, "latte", "에스프레소에 우유를 넣은 커피(라테)"),
                    new GlossaryWord(TurnId, "to go", "포장해 가는"),
                    new GlossaryWord(TurnId, "go", "가다"),
                }
            );

            private readonly DialogueLine[] lines =
            {
                DialogueLine.FromNpc(
                    "Hi there! What can I get for you?",
                    "안녕하세요! 무엇을 드릴까요?"
                ),
                DialogueLine.FromUser("I'd like an iced latte, please."),
                Question,
            };

            public string UserTranslation { get; set; }
            public IReadOnlyList<string> UserBetterExpressions { get; set; }
            public IReadOnlyList<DialogueLine> PreviewLines { get; set; }
            public string NpcName => "Mia";
            public IReadOnlyList<DialogueLine> Lines =>
                Waiting == DialogueWait.Opening ? System.Array.Empty<DialogueLine>()
                : PreviewLines != null ? PreviewLines
                : UserTranslation == null ? lines
                : new[]
                {
                    DialogueLine.FromUser("카페 어디야?"),
                    DialogueLine.FromNpc("It is on your right.", null),
                };
            public DialogueLine LastNpcLine => Waiting == DialogueWait.Opening ? null : Question;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => "매장에서 먹을게요.";
            public string ExampleSentence => null;
            public bool IsBusy => Waiting == DialogueWait.Opening;
            public string StatusMessage => null;
            public DialogueWait Waiting { get; set; }
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords =>
                new[]
                {
                    "for here — 매장에서 먹을게요",
                    "to go — 포장해 주세요",
                    "oat milk — 귀리 우유",
                };
            public bool IsSuggestingWords => false;
            public bool CanSend => true;
            public bool HasMeaning => true;

            public IReadOnlyList<GlossaryData> LastNpcGlossary =>
                new[]
                {
                    new GlossaryData
                    {
                        Text = "latte",
                        MeaningKo = "에스프레소에 우유를 넣은 커피(라테)",
                    },
                    new GlossaryData { Text = "to go", MeaningKo = "포장해 가는" },
                    new GlossaryData { Text = "go", MeaningKo = "가다" },
                };

            public bool HasHint => true;
            public string HintText => "For here, please.";
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public bool CanTalk => true;
            public bool CanReplaySpeech => LastNpcLine != null;
            public bool SpeaksNpcLines => PreviewLines == null;
            public bool IsNpcSpeechReady => LastNpcLine != null;
        }
    }
}
