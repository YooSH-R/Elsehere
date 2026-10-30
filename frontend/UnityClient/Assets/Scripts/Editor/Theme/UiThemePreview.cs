using System;
using System.IO;
using System.Linq;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Mission;
using A305.ObjectHover;
using A305.Onboarding;
using A305.Player;
using A305.Theme;
using A305.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 테마 3개로 집·마을·대화·피드백·마이페이지 화면을 그려 이미지로 저장한다. Play나 서버 호출 없이 화면 배치와 색만 확인한다.
    /// THEME_PREVIEW_OUTPUT에 출력 폴더, 선택으로 FIRST_DAY_PREVIEW_JSON에 저장한 퀘스트 응답을 지정한다.
    /// </summary>
    public static class UiThemePreview
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Tools/A305/UI/Capture Theme Previews")]
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("THEME_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    "THEME_PREVIEW_OUTPUT에 출력 폴더를 지정하세요."
                );
            Directory.CreateDirectory(output);
            string missionJson = Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_JSON");
            FirstDayMissionState mission = string.IsNullOrWhiteSpace(missionJson)
                ? null
                : ApiJson.Deserialize<FirstDayMissionState>(File.ReadAllText(missionJson));
            foreach (UiThemeId theme in (UiThemeId[])Enum.GetValues(typeof(UiThemeId)))
            {
                UiThemeSelection.Shared.Select(theme);
                string name = theme.ToString().ToLowerInvariant();
                CaptureHome(Path.Combine(output, $"home-{name}.png"));
                CaptureTown(mission, Path.Combine(output, $"town-{name}.png"));
                CaptureTalk(Path.Combine(output, $"talk-{name}.png"));
                CaptureReview(Path.Combine(output, $"review-{name}.png"));
                CaptureMyPage(Path.Combine(output, $"mypage-{name}.png"));
            }
            UiThemeSelection.Shared.Select(UiThemeId.Jelly);
        }

        private static void CaptureHome(string path)
        {
            Scene scene = Open("HomeScene", out Camera camera);
            Show<LearningHudView>(scene, camera, 0.5f).SetVocabularyCount(12);
            var picker = Show<ThemePickerView>(scene, camera, 0.4f);
            Field<GameObject>(picker, "window").SetActive(true);
            // 실행 중에는 ThemePickerView가 고른 테마에만 표시를 남긴다.
            SerializedProperty options = new SerializedObject(picker).FindProperty("options");
            for (int i = 0; i < options.arraySize; i++)
            {
                SerializedProperty option = options.GetArrayElementAtIndex(i);
                (
                    (GameObject)option.FindPropertyRelative("selectedMark").objectReferenceValue
                ).SetActive(
                    option.FindPropertyRelative("theme").enumValueIndex
                        == (int)UiThemeSelection.Shared.Current
                );
            }
            var guide = Show<TutorialGuideView>(scene, camera, 0.45f);
            Field<GameObject>(guide, "panel").SetActive(false);
            Render(scene, camera, path);
        }

        private static void CaptureTown(FirstDayMissionState mission, string path)
        {
            Scene scene = Open("TownScene", out Camera camera);
            var hud = Show<LearningHudView>(scene, camera, 0.5f);
            hud.SetVocabularyCount(12);
            hud.ShowMessage("단어장에 담았어요.");
            var view = Show<FirstDayMissionView>(scene, camera, 0.45f);
            view.SetAvailable(true);
            view.BindMap("TOWN", BuilderTools.FindSingleInScene<PlayerMover>(scene).transform);
            if (mission != null)
                view.Refresh(mission, false, false);
            Field<GameObject>(view, "journal").SetActive(true);
            (
                (RectTransform)Field<TMP_Text>(view, "situation").transform.parent
            ).gameObject.SetActive(false);
            Render(scene, camera, path);
        }

        // 마이페이지 창을 열어 둔 모습. 시점 감도 슬라이더와 현재값이 보이는지 확인한다.
        private static void CaptureMyPage(string path)
        {
            Scene scene = Open("TownScene", out Camera camera);
            var page = Show<MyPageView>(scene, camera, 0.4f);
            Field<GameObject>(page, "overlay").SetActive(true);
            Field<GameObject>(page, "profilePanel").SetActive(true);
            Field<GameObject>(page, "logoutDialog").SetActive(false);
            Field<TMP_Text>(page, "profileText").text = "닉네임 미리보기 · 여자 캐릭터 · 영어";
            Field<Slider>(page, "lookSensitivitySlider").SetValueWithoutNotify(1.4f);
            Field<TMP_Text>(page, "lookSensitivityText").text = "1.4배";
            Render(scene, camera, path);
        }

        private static void CaptureTalk(string path)
        {
            Scene scene = Open("TownScene", out Camera camera);
            var dialogue = Show<DialoguePanelView>(scene, camera, 0.4f);
            Field<GameObject>(dialogue, "panel").SetActive(true);
            Field<TMP_Text>(dialogue, "linesText").text =
                "<b>Ben</b>  Excuse me, are you new here?\n<b>나</b>  Yes! Where can I buy an umbrella?";
            Field<TMP_Text>(dialogue, "guideText").text =
                "정답은 없어요. 영어로 자유롭게 말하거나 입력해 보세요.";
            Field<TMP_Text>(dialogue, "statusText").text = "Ben이 듣고 있어요";
            Field<TMP_Text>(dialogue, "suggestText").text =
                "추천 단어 · umbrella · convenience store";
            var tooltip = Show<WordTooltipView>(scene, camera, 0.45f);
            Field<TMP_Text>(tooltip, "wordLabel").text = "umbrella";
            Field<TMP_Text>(tooltip, "meaningLabel").text = "우산";
            Field<TMP_Text>(tooltip, "exampleLabel").text = "Where can I buy an umbrella?";
            var panel = Field<RectTransform>(tooltip, "panel");
            panel.anchoredPosition = new Vector2(1240, 760);
            panel.GetComponent<CanvasGroup>().alpha = 1;
            Render(scene, camera, path);
        }

        private static void CaptureReview(string path)
        {
            Scene scene = Open("TownScene", out Camera camera);
            var feedback = Show<FeedbackView>(scene, camera, 0.4f);
            Field<GameObject>(feedback, "panel").SetActive(true);
            Field<TMP_Text>(feedback, "summaryText").text =
                "길을 묻고 우산 파는 곳을 잘 알아냈어요.";
            Field<TMP_Text>(feedback, "bodyText").text =
                "더 자연스럽게 · <b>Is there a convenience store nearby?</b>";
            var guide = Show<TutorialGuideView>(scene, camera, 0.45f);
            Field<GameObject>(guide, "panel").SetActive(true);
            Field<TMP_Text>(guide, "titleText").text =
                "첫걸음 가이드 3/4 · 익숙한 사물, 새로운 이름";
            Field<TMP_Text>(guide, "bodyText").text =
                "가구에 마우스를 올리면 영어 이름과 뜻이 보여요.";
            Field<TMP_Text>(guide, "taskText").text = "멈춘 채 가구에 마우스를 올려 보세요";
            Field<TMP_Text>(guide, "continueButtonLabel").text = "다음으로";
            Render(scene, camera, path);
        }

        private static Scene Open(string sceneName, out Camera camera)
        {
            Scene scene = EditorSceneManager.OpenScene(
                $"Assets/Scenes/Production/{sceneName}.unity"
            );
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                canvas.gameObject.SetActive(false);
            camera = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene).GetComponent<Camera>();
            // 게임에서는 화면 UI가 후처리를 거치지 않으므로, 미리보기도 후처리를 끄고 UI 색을 그대로 본다.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            Quaternion rotation = Quaternion.Euler(16f, player.eulerAngles.y, 0f);
            // 실내 벽 안으로 들어가지 않는 3인칭 위치: 몸 가운데 높이를 3.4m 뒤에서 내려다본다.
            Vector3 aim = player.position + Vector3.up * 0.97f;
            camera.transform.SetPositionAndRotation(
                aim + rotation * new Vector3(0, 0, -3.4f),
                rotation
            );
            return scene;
        }

        /// <summary>화면 하나를 카메라 앞에 그리도록 바꾼다. 거리가 가까울수록 위에 그린다.</summary>
        private static T Show<T>(Scene scene, Camera camera, float distance)
            where T : Component
        {
            var view = BuilderTools.FindSingleInScene<T>(scene);
            Canvas canvas = view.GetComponentInParent<Canvas>(true);
            canvas.gameObject.SetActive(true);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = distance;
            return view;
        }

        private static T Field<T>(Component owner, string name)
            where T : UnityEngine.Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

        private static void Render(Scene scene, Camera camera, string path)
        {
            foreach (ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene))
                element.ApplyCurrent();
            var target = new RenderTexture(Width, Height, 24);
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)Width / Height;
                camera.Render();
                Canvas.ForceUpdateCanvases();
                foreach (
                    FirstDayMissionView view in BuilderTools.FindAllInScene<FirstDayMissionView>(
                        scene
                    )
                )
                    view.UpdateLayout();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
