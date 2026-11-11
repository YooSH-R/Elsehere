using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Player;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Learning
{
    /// <summary>실제 프리팹을 고정 데이터로 렌더링한다. 네트워크·모델 호출·씬 저장은 하지 않는다.</summary>
    public static class FeedbackNotificationsPreview
    {
        public static void Capture()
        {
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            string output = Environment.GetEnvironmentVariable("FEEDBACK_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("FEEDBACK_PREVIEW_OUTPUT is required.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            var hud = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            var dialogue = BuilderTools.FindSingleInScene<DialoguePanelView>(scene);
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            Quaternion rotation = Quaternion.Euler(15, player.transform.eulerAngles.y, 0);
            Vector3 aim = player.transform.position + Vector3.up * 1f;
            camera.transform.SetPositionAndRotation(
                aim + rotation * new Vector3(0, 0, -3.4f),
                rotation
            );
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                bool visible =
                    canvas.gameObject == hud.gameObject || canvas.gameObject == dialogue.gameObject;
                canvas.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = canvas.gameObject == hud.gameObject ? .55f : .5f;
            }
            foreach (ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene))
                element.ApplyCurrent();
            hud.SetVocabularyCount(12);
            hud.SetFeedbackCount(2);
            Transform menu = hud.transform.Find("Panel/Menu");
            hud.transform.Find("MenuBackdrop").gameObject.SetActive(false);
            hud.transform.Find("Panel/MenuBar/OpenMenuButton").gameObject.SetActive(false);
            menu.gameObject.SetActive(false);
            hud.HistoryView.Hide();
            hud.SetTalking(true);
            hud.ControlsGuide.SetBlocked(true);
            dialogue.transform.Find("Panel").gameObject.SetActive(false);
            dialogue.transform.Find("FeedbackWait").gameObject.SetActive(true);
            Render(camera, output, "waiting-1600x900.png", 1600, 900, hud, dialogue);
            Render(camera, output, "waiting-1280x720.png", 1280, 720, hud, dialogue);
            dialogue.gameObject.SetActive(false);
            hud.SetTalking(false);
            hud.ControlsGuide.SetBlocked(false);
            Render(camera, output, "notification-1600x900.png", 1600, 900, hud, null);
            hud.transform.Find("Panel/MenuBar/MenuButton").gameObject.SetActive(false);
            hud.transform.Find("MenuBackdrop").gameObject.SetActive(true);
            menu.gameObject.SetActive(true);
            Render(camera, output, "phone-1600x900.png", 1600, 900, hud, null);
            menu.gameObject.SetActive(false);
            hud.transform.Find("MenuBackdrop").gameObject.SetActive(false);
            hud.transform.Find("Panel/MenuBar/MenuButton").gameObject.SetActive(true);
            var history = new ConversationHistory(new SampleHistory());
            history.LoadAsync().GetAwaiter().GetResult();
            hud.HistoryView.Show(history);
            hud.SetFeedbackCount(2);
            Render(camera, output, "history-1600x900.png", 1600, 900, hud, null);
            Render(camera, output, "history-1280x720.png", 1280, 720, hud, null);
        }

        private static void Render(
            Camera camera,
            string output,
            string name,
            int width,
            int height,
            LearningHudView hud,
            DialoguePanelView dialogue
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.Render();
                Canvas.ForceUpdateCanvases();
                foreach (Canvas canvas in hud.GetComponentsInChildren<Canvas>())
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
                if (dialogue != null)
                    foreach (
                        VerticalLayoutGroup layout in dialogue.GetComponentsInChildren<VerticalLayoutGroup>()
                    )
                        LayoutRebuilder.ForceRebuildLayoutImmediate(
                            (RectTransform)layout.transform
                        );
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    if (text.name == "UnreadCount" && text.isTextOverflowing)
                        throw new InvalidOperationException(
                            "Unread count overlaps at " + width + "x" + height
                        );
                }
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        private sealed class SampleHistory : IConversationHistoryApi
        {
            public Task<ConversationPage> GetPageAsync(string cursor, string sceneCode) =>
                Task.FromResult(
                    new ConversationPage
                    {
                        Items = new List<ConversationSummary>
                        {
                            Item(51, "Jake", "CAFE", true),
                            Item(50, "Sarah", "HOTEL", true),
                            Item(49, "Tom", "TOWN", false),
                        },
                    }
                );

            private static ConversationSummary Item(
                long id,
                string name,
                string scene,
                bool unread
            ) =>
                new ConversationSummary
                {
                    ConversationId = id,
                    Npc = new NpcSummary { Name = name },
                    SceneCode = scene,
                    Status = "ENDED_BY_USER",
                    StartedAt = DateTimeOffset.Now.AddMinutes(-id),
                    UserTurnCount = 3,
                    FeedbackStatus = "DONE",
                    FeedbackUnread = unread,
                };

            public Task<PastFeedback> GetFeedbackAsync(long id) =>
                throw new NotSupportedException();

            public Task<ConversationDetail> GetDetailAsync(long id) =>
                throw new NotSupportedException();
        }
    }
}
