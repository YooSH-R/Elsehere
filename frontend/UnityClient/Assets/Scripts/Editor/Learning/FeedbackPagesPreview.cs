using System.Collections.Generic;
using System.IO;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Learning;
using A305.Player;
using A305.Theme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace A305.EditorTools.Learning
{
    /// <summary>외부 호출 없이 고정 피드백으로 페이지와 테마를 확인하는 Editor 미리 보기.</summary>
    public static class FeedbackPagesPreview
    {
        [MenuItem("Tools/A305/Learning/Build Feedback Pages")]
        public static void Build()
        {
            LearningBuilder.Build();
            UiThemeBuilder.StyleFeaturePrefabs();
            BindTranscriptWordCard();
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }

        /// <summary>현재 피드백 프리팹 구조를 보존하면서 대화 NPC 표시 그림만 연결한다.</summary>
        public static void BindTranscriptAvatar()
        {
            const string path = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                FeedbackView view = root.GetComponent<FeedbackView>();
                BuilderTools.SetReference(
                    view,
                    "transcriptAvatarSprite",
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/Art/UI/Feedback/HistoryAvatarCoral.png"
                    )
                );
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/A305/Learning/Bind Transcript Word Card")]
        public static void BindTranscriptWordCard()
        {
            const string path = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                GameObject tooltip = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/ObjectHover/WordTooltip.prefab"
                );
                BuilderTools.SetReference(
                    root.GetComponent<FeedbackView>(),
                    "transcriptWordCardTemplate",
                    tooltip.transform.Find("Panel")
                );
                BuilderTools.SetReference(
                    root.GetComponent<FeedbackView>(),
                    "transcriptWordCardBackground",
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Theme/Jelly/Card.png")
                );
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void Capture() => Capture(naturalOnly: false);

        public static void CaptureNaturalSentence() => Capture(naturalOnly: true);

        public static void CaptureExpanded() => Capture(naturalOnly: false, expanded: true);

        public static void CaptureExpressionGuidance() =>
            Capture(naturalOnly: false, expressionGuidance: true);

        private static void Capture(
            bool naturalOnly,
            bool expanded = false,
            bool expressionGuidance = false
        )
        {
            string output = System.Environment.GetEnvironmentVariable("FEEDBACK_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new System.InvalidOperationException("Set FEEDBACK_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            var view = BuilderTools.FindSingleInScene<FeedbackView>(scene);
            Camera camera = FrameCanvas(scene, view.gameObject);
            // 완성된 서버 피드백이라 아래 줄에 [다시 연습]이 보인다.
            view.SetPracticeAvailable(true);
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                2,
                new ServerFeedback(
                    new FeedbackData
                    {
                        Status = "DONE",
                        Summary = "카페에서 아이스커피를 주문하고 카드로 결제했어요.",
                        Reviews = new FeedbackReviews
                        {
                            Good = new List<string>
                            {
                                "원하는 음료와 결제 방법을 분명하게 전달했어요.",
                            },
                            Improve = new List<string>
                            {
                                "상황에 따라 뜻이 달라지는 표현을 한 번 더 살펴봐요.",
                            },
                        },
                        Suggestion = new FeedbackSuggestion
                        {
                            UserText = "I want drink iced coffee",
                            BetterText = "I'd like an iced coffee, please.",
                        },
                        SituationExpressions = new List<SituationExpressionData>
                        {
                            new SituationExpressionData
                            {
                                Expression = "Can I get that to go?",
                                MeaningKo = "포장해 주실 수 있나요?",
                                UsageKo = "음료를 가지고 나가고 싶을 때 써요.",
                                ExampleEn = "Can I get that to go? I'm in a bit of a hurry.",
                                ExampleKo = "포장해 주실 수 있나요? 좀 급해서요.",
                            },
                            new SituationExpressionData
                            {
                                Expression = "Could I get a little less ice?",
                                MeaningKo = "얼음 조금만 넣어 주실 수 있나요?",
                                UsageKo = "차가운 음료의 얼음 양을 부탁할 때 써요.",
                                ExampleEn = "Could I get a little less ice in my iced coffee?",
                                ExampleKo = "아이스커피에 얼음 조금만 넣어 주실 수 있나요?",
                            },
                            new SituationExpressionData
                            {
                                Expression = "Have a good one!",
                                MeaningKo = "좋은 하루 보내세요!",
                                UsageKo = "주문을 마친 뒤 직원에게 가볍게 인사할 때 써요.",
                                ExampleEn = "Thanks for the coffee. Have a good one!",
                                ExampleKo = "커피 고마워요. 좋은 하루 보내세요!",
                            },
                        },
                    },
                    new List<DetailedFeedbackItem>
                    {
                        new DetailedFeedbackItem
                        {
                            UserText = "I want drink iced coffee",
                            BetterText = "I'd like an iced coffee, please.",
                            Reason =
                                "want 뒤에 동작을 말할 때는 to를 넣어요. 주문할 때는 I'd like로 시작하면 더 부드러워요.",
                        },
                        new DetailedFeedbackItem
                        {
                            UserText = "I'm good!",
                            Reason = "음료가 마음에 든다는 뜻이라면 It's good!이 더 분명해요.",
                            BetterText = "It's good!",
                            Idiom = new IdiomData
                            {
                                Text = "I'm good, thanks.",
                                MeaningKo =
                                    "뭔가 더 필요한지 물었을 때 ‘괜찮아요, 됐어요’라고 정중히 거절할 때도 써요.",
                            },
                        },
                    },
                    null
                )
            );
            view.SetSaveAvailable(true);
            view.Show(
                feedback,
                new[]
                {
                    DialogueLine.FromNpc(
                        "Hi there! What can I get started for you?",
                        "안녕하세요! 무엇을 드릴까요?"
                    ),
                    DialogueLine.FromUser("I want drink iced coffee"),
                    DialogueLine.FromNpc(
                        "Sure. Would you like anything else?",
                        "네. 더 필요한 건 있으세요?"
                    ),
                    DialogueLine.FromUser("I'm good!"),
                    DialogueLine.FromNpc("Great, that'll be four dollars.", "좋아요, 4달러입니다."),
                    DialogueLine.FromUser("Can I pay by card?"),
                    DialogueLine.FromNpc(
                        "Of course. Have a good one!",
                        "물론이죠. 좋은 하루 보내세요!"
                    ),
                }
            );
            if (expanded)
                view.ToggleExpanded();
            if (expressionGuidance)
            {
                feedback = ExpressionGuidanceFeedback();
                view.Show(feedback, new DialogueLine[0]);
                foreach (
                    UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
                )
                {
                    UiThemeSelection.Shared.Select(theme);
                    foreach (
                        ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene)
                    )
                        element.ApplyCurrent();
                    view.SelectSection(FeedbackSection.Corrections);
                    Render(
                        camera,
                        view,
                        Path.Combine(output, $"expressions-{theme}.png"),
                        1600,
                        1000
                    );
                    Render(
                        camera,
                        view,
                        Path.Combine(output, $"expressions-mobile-{theme}.png"),
                        540,
                        960
                    );
                    view.SelectSection(FeedbackSection.Conversation);
                    view.SelectReview(0);
                    Render(
                        camera,
                        view,
                        Path.Combine(output, $"ambiguous-{theme}.png"),
                        1600,
                        1000
                    );
                    Render(
                        camera,
                        view,
                        Path.Combine(output, $"ambiguous-mobile-{theme}.png"),
                        540,
                        960
                    );
                    Render(
                        camera,
                        view,
                        Path.Combine(output, $"ambiguous-examples-{theme}.png"),
                        1600,
                        1000,
                        true
                    );
                    Render(
                        camera,
                        view,
                        Path.Combine(output, $"ambiguous-examples-mobile-{theme}.png"),
                        540,
                        960,
                        true
                    );
                }
                view.SelectReview(1);
                Render(camera, view, Path.Combine(output, "word-list-desktop.png"), 1600, 1000);
                Render(camera, view, Path.Combine(output, "word-list-mobile.png"), 540, 960);
                view.SelectReview(2);
                Render(camera, view, Path.Combine(output, "natural-short-mobile.png"), 540, 960);
                return;
            }
            if (naturalOnly)
            {
                const string sentence = "I have a reservation under Kim.";
                feedback = ConversationFeedback.FromServer(
                    "Jake",
                    1,
                    new ServerFeedback(
                        new FeedbackData { Status = "DONE", Summary = "예약을 확인했어요." },
                        new List<DetailedFeedbackItem>
                        {
                            new DetailedFeedbackItem
                            {
                                UserText = sentence,
                                BetterText = sentence,
                                Reason = "예약자 이름을 자연스럽게 전달해 예약 확인이 쉬웠어요.",
                            },
                        },
                        null
                    )
                );
                view.Show(
                    feedback,
                    new[]
                    {
                        DialogueLine.FromNpc("Do you have a reservation?", "예약하셨나요?"),
                        DialogueLine.FromUser(sentence),
                    }
                );
                foreach (
                    UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
                )
                {
                    UiThemeSelection.Shared.Select(theme);
                    foreach (
                        ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene)
                    )
                        element.ApplyCurrent();
                    view.SelectSection(FeedbackSection.Conversation);
                    view.SelectReview(0);
                    Render(camera, view, Path.Combine(output, $"natural-{theme}.png"), 1600, 1000);
                }
                Render(camera, view, Path.Combine(output, "natural-mobile.png"), 540, 960);
                return;
            }

            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                UiThemeSelection.Shared.Select(theme);
                foreach (ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene))
                    element.ApplyCurrent();
                foreach (FeedbackSection section in System.Enum.GetValues(typeof(FeedbackSection)))
                {
                    view.SelectSection(section);
                    Render(
                        camera,
                        view,
                        Path.Combine(output, $"feedback-{theme}-{section}.png"),
                        1600,
                        1000
                    );
                }
                view.SelectSection(FeedbackSection.Conversation);
                view.SelectReview(1);
                Render(
                    camera,
                    view,
                    Path.Combine(output, $"feedback-{theme}-SelectedReview.png"),
                    1600,
                    1000
                );
            }
            UiThemeSelection.Shared.Select(UiThemeId.Jelly);
            Render(camera, view, Path.Combine(output, "feedback-portrait.png"), 900, 1400);
            view.SelectSection(FeedbackSection.Summary);
            view.SelectSection(FeedbackSection.Conversation);
            Render(
                camera,
                view,
                Path.Combine(output, "feedback-conversation-portrait.png"),
                900,
                1400
            );
            var longFeedback = ConversationFeedback.FromServer(
                "Mia",
                1,
                new ServerFeedback(
                    new FeedbackData { Status = "DONE", Summary = "긴 설명 페이지 확인" },
                    new List<DetailedFeedbackItem>
                    {
                        new DetailedFeedbackItem
                        {
                            UserText = "I want go.",
                            BetterText = "I want to go.",
                            Reason = string.Concat(
                                System.Linq.Enumerable.Repeat(
                                    "문법 교정 설명을 한 페이지에 다 담지 못해도 다음 페이지에서 이어서 읽을 수 있어요. ",
                                    60
                                )
                            ),
                        },
                    },
                    null
                )
            );
            view.Show(longFeedback, new DialogueLine[0]);
            view.SelectSection(FeedbackSection.Corrections);
            Render(camera, view, Path.Combine(output, "feedback-long-first.png"), 1600, 1000);
            var body = (TMPro.TMP_Text)
                new SerializedObject(view).FindProperty("bodyText").objectReferenceValue;
            var scroll = view.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            if (scroll.content.rect.height <= scroll.viewport.rect.height)
                throw new System.InvalidOperationException(
                    "Long feedback did not expand for scrolling."
                );
            scroll.verticalNormalizedPosition = 0;
            Render(camera, view, Path.Combine(output, "feedback-long-bottom.png"), 1600, 1000);
            view.SelectSection(FeedbackSection.Conversation);
            view.SelectReview(0);
            Render(camera, view, Path.Combine(output, "feedback-long-review.png"), 1600, 1000);
            File.WriteAllText(
                Path.Combine(output, "scroll-check.txt"),
                $"Content height: {scroll.content.rect.height}; viewport: {scroll.viewport.rect.height}; external model calls: 0"
            );
        }

        private static ConversationFeedback ExpressionGuidanceFeedback() =>
            ConversationFeedback.FromServer(
                "Mia",
                2,
                new ServerFeedback(
                    new FeedbackData { Status = "DONE", Summary = "표현 연습용 가상 대화예요." },
                    new List<DetailedFeedbackItem>
                    {
                        new DetailedFeedbackItem
                        {
                            UserText = "umbrella please",
                            Verdict = "needs_clarification",
                            Reason = "우산을 빌리려는지 판매처를 묻는지 분명하게 말해 보세요.",
                            ExpressionExamples = new List<FeedbackExpressionExample>
                            {
                                new FeedbackExpressionExample
                                {
                                    ConditionKo = "우산을 빌리고 싶을 때",
                                    Expression = "Could I borrow an umbrella, please?",
                                    MeaningKo = "우산을 빌릴 수 있을까요?",
                                },
                                new FeedbackExpressionExample
                                {
                                    ConditionKo = "우산 파는 곳을 물을 때",
                                    Expression = "Where can I buy an umbrella?",
                                    MeaningKo = "우산을 어디에서 살 수 있나요?",
                                },
                            },
                        },
                        new DetailedFeedbackItem
                        {
                            UserText = "Coffee. Large. Cold.",
                            BetterText = "I'd like a large cold coffee, please.",
                            Verdict = "improvement_suggested",
                            SuggestionKind = "context_fit",
                            Reason =
                                "음료와 크기·온도는 전달했어요. 원하는 주문을 한 문장으로 연결해 봐요.",
                            ExpressionExamples = new List<FeedbackExpressionExample>
                            {
                                new FeedbackExpressionExample
                                {
                                    ConditionKo = "원하는 음료를 주문할 때",
                                    Expression = "I'd like a large cold coffee, please.",
                                    MeaningKo = "차가운 커피 큰 것으로 주세요.",
                                },
                                new FeedbackExpressionExample
                                {
                                    ConditionKo = "같은 주문을 다른 말로 할 때",
                                    Expression = "Could I have a large cold coffee?",
                                    MeaningKo = "차가운 커피 큰 것으로 받을 수 있을까요?",
                                },
                            },
                        },
                        new DetailedFeedbackItem
                        {
                            UserText = "For here, please.",
                            BetterText = "I'd like to eat here, please.",
                            Verdict = "appropriate",
                            SuggestionKind = "natural_alternative",
                            Reason =
                                "짧아도 자연스러운 답이에요. 문장으로 말하는 방법도 연습할 수 있어요.",
                            ExpressionExamples = new List<FeedbackExpressionExample>
                            {
                                new FeedbackExpressionExample
                                {
                                    ConditionKo = "매장에서 먹겠다고 문장으로 말할 때",
                                    Expression = "I'd like to eat here, please.",
                                    MeaningKo = "여기서 먹고 갈게요.",
                                },
                                new FeedbackExpressionExample
                                {
                                    ConditionKo = "다른 말로 답할 때",
                                    Expression = "I'll eat here, thanks.",
                                    MeaningKo = "여기서 먹을게요, 고마워요.",
                                },
                            },
                        },
                    },
                    null
                )
            );

        /// <summary>
        /// [다시 연습]을 마친 뒤의 [연습 돌아보기] 창. 고친 문장의 지난번·이번 비교와 상황 표현을 세 테마·세로 화면으로 그린다. Play·서버 호출 없음.
        /// </summary>
        public static void CaptureReviewResult()
        {
            string output = System.Environment.GetEnvironmentVariable("FEEDBACK_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new System.InvalidOperationException("Set FEEDBACK_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            var view = BuilderTools.FindSingleInScene<ReviewWorkbookView>(scene);
            GameObject hud = view.GetComponentInParent<Canvas>(true).rootCanvas.gameObject;
            Camera camera = FrameCanvas(scene, hud);
            // HUD의 다른 부분은 Play에서만 채워지므로 결과 창만 남긴다.
            foreach (Transform part in hud.transform)
                part.gameObject.SetActive(view.transform.IsChildOf(part));
            var card = new ReviewCard
            {
                Id = 1,
                SceneCode = "CAFE",
                Npc = new NpcSummary
                {
                    NpcCode = "CAFE_STAFF",
                    Name = "Mia",
                    RoleLabel = "카페 직원",
                },
                SituationSummary = "카페에서 음료를 주문하고 포장을 부탁했어요.",
                Targets = new List<ReviewTarget>
                {
                    new ReviewTarget
                    {
                        Expression = "Can I get that to go?",
                        MeaningKo = "포장해 주세요",
                        UsageKo = "가지고 나갈 음료를 부탁할 때 이렇게 말해요.",
                        OriginalText = "포장해 주세요",
                    },
                    new ReviewTarget
                    {
                        Expression = "I'd like an iced coffee, please.",
                        MeaningKo = "주문할 때는 I'd like로 시작하면 더 부드러워요.",
                        UsageKo = "주문할 때는 I'd like로 시작하면 더 부드러워요.",
                        OriginalText = "I want drink iced coffee",
                    },
                    new ReviewTarget
                    {
                        Expression = "Could I get a little less ice?",
                        MeaningKo = "얼음 조금만 넣어 주실 수 있나요?",
                        UsageKo = "차가운 음료의 얼음 양을 부탁할 때 써요.",
                    },
                },
            };
            var record = new DialogueRecord(
                "CAFE_STAFF",
                "Mia",
                new DialogueLine[0],
                new[] { "Hi, I'd like an iced coffee, please." },
                false,
                reviewCardId: 1,
                reviewResult: new ReviewPracticeResult
                {
                    ReviewCardId = 1,
                    PracticeCount = 1,
                    Expressions = new List<ReviewExpressionEvidence>
                    {
                        new ReviewExpressionEvidence { Expression = "Can I get that to go?" },
                        new ReviewExpressionEvidence
                        {
                            Expression = "I'd like an iced coffee, please.",
                            Evidence = new List<ReviewUtterance>
                            {
                                new ReviewUtterance
                                {
                                    TurnId = 3,
                                    Text = "Hi, I'd like an iced coffee, please.",
                                },
                            },
                        },
                        new ReviewExpressionEvidence
                        {
                            Expression = "Could I get a little less ice?",
                            Evidence = new List<ReviewUtterance>
                            {
                                new ReviewUtterance
                                {
                                    TurnId = 5,
                                    Text = "Could I get a little less ice?",
                                },
                            },
                        },
                    },
                }
            );
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                UiThemeSelection.Shared.Select(theme);
                foreach (ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene))
                    element.ApplyCurrent();
                RenderReview(camera, view, card, record, output, $"review-{theme}.png", 1600, 1000);
            }
            UiThemeSelection.Shared.Select(UiThemeId.Jelly);
            RenderReview(camera, view, card, record, output, "review-portrait.png", 900, 1400);
        }

        // 창 폭이 정해진 뒤에 줄 높이를 다시 잡아야 하므로 한 번 그린 다음 결과를 다시 채워 찍는다.
        private static void RenderReview(
            Camera camera,
            ReviewWorkbookView view,
            ReviewCard card,
            DialogueRecord record,
            string output,
            string name,
            int width,
            int height
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            view.ShowResult(card, record);
            camera.Render();
            Canvas.ForceUpdateCanvases();
            view.ShowResult(card, record);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }

        // 집 씬에서 고른 화면의 캔버스만 켜고 플레이어 뒤 카메라 앞에 그린다.
        private static Camera FrameCanvas(UnityEngine.SceneManagement.Scene scene, GameObject shown)
        {
            var camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            Quaternion rotation = Quaternion.Euler(16, player.eulerAngles.y, 0);
            Vector3 aim = player.position + Vector3.up * .97f;
            camera.transform.SetPositionAndRotation(
                aim + rotation * new Vector3(0, 0, -3.4f),
                rotation
            );
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                canvas.gameObject.SetActive(canvas.gameObject == shown);
                if (canvas.gameObject != shown)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .22f, .2f);
            return camera;
        }

        private static void Render(
            Camera camera,
            FeedbackView view,
            string output,
            int width,
            int height,
            bool scrollToBottom = false
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            camera.Render();
            Canvas.ForceUpdateCanvases();
            view.UpdateLayout();
            Canvas.ForceUpdateCanvases();
            if (scrollToBottom)
            {
                view.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition =
                    0;
                Canvas.ForceUpdateCanvases();
            }
            foreach (TMPro.TMP_Text text in view.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                if (
                    text.name != "Utterance"
                    && text.transform.parent.name != "DetailCard"
                    && text.transform.parent.name != "ExpressionExample"
                    && text.transform.parent.name != "CorrectionCard"
                )
                    continue;
                text.ForceMeshUpdate();
                if (text.isTextOverflowing)
                    throw new System.InvalidOperationException(
                        $"Feedback text exceeds its card: {text.name} ({width}x{height})."
                    );
            }
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(output, pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }
    }
}
