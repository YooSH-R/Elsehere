using System;
using System.IO;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Mission;
using A305.ObjectHover;
using A305.Player;
using A305.Theme;
using A305.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tutorial
{
    /// <summary>실제 숙소 HUD로 일반 화면·휴대폰·단어장 강조 표시를 렌더링한다. 씬 저장과 서버 호출은 하지 않는다.</summary>
    public static class TutorialHighlightPreview
    {
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("TUTORIAL_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("TUTORIAL_PREVIEW_OUTPUT이 필요합니다.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            var coach = BuilderTools.FindSingleInScene<CoachMarkView>(scene);
            var hud = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            var menu = BuilderTools.FindSingleInScene<HudMenuView>(scene);
            var mission = BuilderTools.FindSingleInScene<FirstDayMissionView>(scene);
            var tooltip = BuilderTools.FindSingleInScene<WordTooltipView>(scene);
            var vocabulary = BuilderTools.FindSingleInScene<VocabularyView>(scene);
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                bool shown =
                    canvas.gameObject == coach.gameObject
                    || canvas.gameObject == hud.gameObject
                    || canvas.gameObject == mission.gameObject
                    || canvas.gameObject == tooltip.gameObject
                    || canvas.gameObject == vocabulary.gameObject;
                canvas.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            foreach (ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene))
                element.ApplyCurrent();
            menu.SendMessage("Awake");
            coach.SendMessage("Awake");
            tooltip.SendMessage("Awake");
            vocabulary.SendMessage("Awake");
            var statusBar = (GameObject)
                new SerializedObject(mission).FindProperty("statusBar").objectReferenceValue;
            statusBar.SetActive(true);
            hud.HistoryView.Hide();
            hud.SetVocabularyCount(3);
            hud.SetFeedbackCount(0);
            Render(camera, coach, null, Path.Combine(output, "normal.png"));
            Render(camera, coach, menu.MenuButtonRect, Path.Combine(output, "phone.png"));
            menu.MenuButtonRect.GetComponent<Button>().onClick.Invoke();
            Render(camera, coach, hud.VocabularyButtonRect, Path.Combine(output, "vocabulary.png"));
            hud.VocabularyButtonRect.GetComponent<Button>().onClick.Invoke();

            ObjectWordDetails word = AssetDatabase
                .LoadAssetAtPath<ObjectWordDefinition>("Assets/Data/ObjectHover/Words/window.asset")
                .Details;
            var cards = new ObjectWordCards(new LocalObjectWordApi(new[] { word }));
            cards.LoadAsync("HOME").GetAwaiter().GetResult();
            cards.TryGet(word.Key, out WordCard card);
            tooltip.ShowPinned(
                card.Word,
                CardAnchor.AtPointer(new Vector2(600, 450)),
                AssetDatabase.LoadAssetAtPath<ObjectHoverSettings>(
                    "Assets/Data/ObjectHover/ObjectHoverSettings.asset"
                ),
                WordCardButtons.For(card)
            );
            Render(camera, coach, tooltip.SaveButtonRect, Path.Combine(output, "save.png"));
            tooltip.Hide();
            vocabulary.Show(new VocabularyBook(new LocalVocabularyApi()));
            Render(
                camera,
                coach,
                vocabulary.PanelRect,
                Path.Combine(output, "vocabulary-panel.png")
            );
        }

        private static void Render(
            Camera camera,
            CoachMarkView coach,
            RectTransform target,
            string path
        )
        {
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture;
                camera.aspect = 1280f / 720;
                camera.Render();
                Canvas.ForceUpdateCanvases();
                if (target != null)
                    coach.ShowSpotlight(new[] { target }, null);
                else
                    coach.Hide();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
