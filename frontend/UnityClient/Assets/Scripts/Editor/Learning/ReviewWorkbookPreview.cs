using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.Learning;
using A305.Player;
using A305.Theme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace A305.EditorTools.Learning
{
    /// <summary>고정 데이터로 실제 복습장 프리팹을 렌더링한다. 서버·AI를 호출하거나 씬을 저장하지 않는다.</summary>
    public static class ReviewWorkbookPreview
    {
        [MenuItem("Tools/A305/Learning/Capture Review Workbook")]
        public static void Capture()
        {
            string output =
                Environment.GetEnvironmentVariable("REVIEW_PREVIEW_OUTPUT")
                ?? Path.GetFullPath("../../artifacts/review-workbook-captures");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            Quaternion rotation = Quaternion.Euler(16, player.eulerAngles.y, 0);
            camera.transform.SetPositionAndRotation(
                player.position + Vector3.up * .97f + rotation * new Vector3(0, 0, -3.4f),
                rotation
            );
            foreach (Canvas other in BuilderTools.FindAllInScene<Canvas>(scene))
                other.gameObject.SetActive(false);
            var hud = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            GameObject root = hud.gameObject;
            root.SetActive(true);
            hud.ReviewView.gameObject.SetActive(true);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = .5f;
            var view = hud.ReviewView;
            var book = new ReviewBook(new PreviewApi());
            book.LoadAsync().GetAwaiter().GetResult();
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                UiThemeSelection.Shared.Select(theme);
                foreach (ThemedElement element in root.GetComponentsInChildren<ThemedElement>(true))
                    element.ApplyCurrent();
                view.Show(book);
                Render(
                    camera,
                    1600,
                    1000,
                    () => view.Show(book),
                    Path.Combine(output, "library-" + theme + ".png")
                );
                if (theme == UiThemeId.Jelly)
                    Render(
                        camera,
                        3200,
                        2000,
                        () => view.Show(book),
                        Path.Combine(output, "library-detail.png")
                    );
            }
            Render(
                camera,
                1000,
                700,
                () => view.Show(book),
                Path.Combine(output, "library-small.png")
            );
            var result = new ReviewPracticeResult
            {
                PracticeCompleted = true,
                VerifiedExpressionCount = 2,
                PracticeCount = 1,
                Expressions = new List<ReviewExpressionEvidence>
                {
                    new ReviewExpressionEvidence
                    {
                        Expression = "Can I get that to go?",
                        Evidence = new List<ReviewUtterance>
                        {
                            new ReviewUtterance { Text = "Could I get that to go, please?" },
                        },
                    },
                    new ReviewExpressionEvidence
                    {
                        Expression = "Could I get a little less ice?",
                        Evidence = new List<ReviewUtterance>
                        {
                            new ReviewUtterance { Text = "Could I get a little less ice?" },
                        },
                    },
                },
                Utterances = new List<ReviewUtterance>
                {
                    new ReviewUtterance { Text = "Could I get that to go, please?" },
                    new ReviewUtterance { Text = "Could I get a little less ice?" },
                },
            };
            var record = new DialogueRecord(
                "CAFE_STAFF",
                "Mia",
                Array.Empty<DialogueLine>(),
                Array.Empty<string>(),
                false,
                reviewCardId: 42,
                reviewResult: result
            );
            Render(
                camera,
                1600,
                1000,
                () => view.ShowResult(book.Items[0], record),
                Path.Combine(output, "practice-result.png")
            );
            UnityEngine.Object.DestroyImmediate(root);
            CapturePractice(output);
            Debug.Log(
                "Review workbook native previews saved to "
                    + output
                    + ". No network calls or scene saves."
            );
        }

        private static void Render(
            Camera camera,
            int width,
            int height,
            Action arrange,
            string path
        )
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            arrange();
            Canvas.ForceUpdateCanvases();
            foreach (
                ReviewButtonLabel label in UnityEngine.Object.FindObjectsByType<ReviewButtonLabel>(
                    FindObjectsSortMode.None
                )
            )
                label.Arrange();
            camera.Render();
            RenderTexture.active = target;
            if (path.EndsWith("library-detail.png", StringComparison.Ordinal))
            {
                var row = UnityEngine.Object.FindObjectsByType<ReviewWorkbookRow>(
                    FindObjectsSortMode.None
                )[0];
                var rect = (RectTransform)row.transform.Find("PracticeButton");
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector3 start = camera.WorldToScreenPoint(corners[0]);
                Vector3 end = camera.WorldToScreenPoint(corners[2]);
                int left = Mathf.Max(0, Mathf.FloorToInt(start.x) - 20);
                int bottom = Mathf.Max(0, Mathf.FloorToInt(start.y) - 20);
                int detailWidth = Mathf.CeilToInt(end.x - left) + 20;
                int detailHeight = Mathf.CeilToInt(end.y - bottom) + 20;
                var detail = new Texture2D(detailWidth, detailHeight, TextureFormat.RGB24, false);
                detail.ReadPixels(new Rect(left, bottom, detailWidth, detailHeight), 0, 0);
                detail.Apply();
                File.WriteAllBytes(
                    Path.Combine(Path.GetDirectoryName(path), "practice-button.png"),
                    detail.EncodeToPNG()
                );
                UnityEngine.Object.DestroyImmediate(detail);
            }
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void CapturePractice(string output)
        {
            DialoguePanelView view = DialogueLayoutPreview.OpenCafe(
                out Camera camera,
                out Vector3 eye,
                out Vector3 face
            );
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            scripts.TryGetScript("CAFE_STAFF", out DialogueScript script);
            view.Show(new DialogueConversation("CAFE_STAFF", script));
            view.SetReviewReference(
                new[]
                {
                    new ReviewReferenceItem(
                        "Can I get that to go?",
                        "포장해 주실 수 있나요?",
                        true
                    ),
                    new ReviewReferenceItem(
                        "Could I get a little less ice?",
                        "얼음을 조금만 넣어 주세요.",
                        false
                    ),
                },
                1
            );
            var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(
                "Assets/Data/Cafe/CafeCameraSettings.asset"
            );
            eye = DialogueFraming.CameraPosition(
                face,
                eye,
                settings.DialogueDistanceMeters,
                settings.DialogueHeightOffsetMeters
            );
            Render(
                camera,
                1600,
                1000,
                () =>
                {
                    Pose pose = DialogueFraming.Pose(
                        eye,
                        face,
                        camera.fieldOfView,
                        camera.aspect,
                        settings.DialogueFacePosition
                    );
                    camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    view.UpdateLayout();
                    foreach (
                        ReviewReferenceBadges badges in view.GetComponentsInChildren<ReviewReferenceBadges>(
                            true
                        )
                    )
                        badges.SendMessage("LateUpdate");
                },
                Path.Combine(output, "practice-reference.png")
            );
        }

        private sealed class PreviewApi : IReviewApi
        {
            public Task<ReviewPage> GetPageAsync(string scene, string cursor) =>
                Task.FromResult(
                    new ReviewPage
                    {
                        Items = new List<ReviewCard>
                        {
                            new ReviewCard
                            {
                                Id = 42,
                                SceneCode = "CAFE",
                                Npc = new NpcSummary { Name = "Mia", NpcCode = "CAFE_STAFF" },
                                SituationSummary = "음료 주문과 포장 부탁하기",
                                Targets = new List<ReviewTarget>
                                {
                                    new ReviewTarget { Expression = "Can I get that to go?" },
                                    new ReviewTarget
                                    {
                                        Expression = "Could I get a little less ice?",
                                    },
                                },
                            },
                            new ReviewCard
                            {
                                Id = 43,
                                SceneCode = "HOTEL",
                                Npc = new NpcSummary { Name = "Jake", NpcCode = "HOTEL_STAFF" },
                                SituationSummary =
                                    "짐을 맡기고 체크인 시간을 확인한 뒤 근처 카페에 가는 길을 물어보기",
                                PracticeCount = 2,
                                Targets = new List<ReviewTarget>
                                {
                                    new ReviewTarget { Expression = "Could you hold my luggage?" },
                                    new ReviewTarget { Expression = "What time can I check in?" },
                                    new ReviewTarget
                                    {
                                        Expression = "Is there a cafe within walking distance?",
                                    },
                                },
                            },
                        },
                    }
                );

            public Task<ReviewCard> SaveAsync(long id) => throw new NotSupportedException();

            public Task DeleteAsync(long id) => throw new NotSupportedException();

            public Task<ConversationStart> StartAsync(long id, string requestId) =>
                throw new NotSupportedException();
        }
    }
}
