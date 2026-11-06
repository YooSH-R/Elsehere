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

        public static void Capture()
        {
            string output = System.Environment.GetEnvironmentVariable("FEEDBACK_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new System.InvalidOperationException("Set FEEDBACK_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            var view = BuilderTools.FindSingleInScene<FeedbackView>(scene);
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
                canvas.gameObject.SetActive(canvas.gameObject == view.gameObject);
                if (canvas.gameObject != view.gameObject)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .22f, .2f);
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
            File.WriteAllText(
                Path.Combine(output, "scroll-check.txt"),
                $"Content height: {scroll.content.rect.height}; viewport: {scroll.viewport.rect.height}; external model calls: 0"
            );
        }

        private static void Render(
            Camera camera,
            FeedbackView view,
            string output,
            int width,
            int height
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            camera.Render();
            Canvas.ForceUpdateCanvases();
            view.UpdateLayout();
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
