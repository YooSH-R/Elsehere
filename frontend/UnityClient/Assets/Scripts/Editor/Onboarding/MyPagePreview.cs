using System;
using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Onboarding;
using A305.Player;
using A305.Theme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace A305.EditorTools.Onboarding
{
    /// <summary>
    /// 실제 HUD 프리팹의 내 정보 창과 설정 창을 세 테마와 세로 화면으로 렌더링한다. 서버를 부르거나 씬을 저장하지 않는다.
    /// 시점 감도·NPC 말 빠르기 줄처럼 창에 줄을 더했을 때 겹치거나 잘리지 않는지 확인하는 용도다.
    /// </summary>
    public static class MyPagePreview
    {
        public static void CaptureUpdated()
        {
            MyPageLayoutBuilder.ApplyToPrefab();
            PhoneMenuBuilder.ApplyBalanceToPrefab();
            Capture();
            PhoneMenuBuilder.CaptureControlsPreview();
        }

        [MenuItem("Tools/A305/Onboarding/Capture My Page")]
        public static void Capture()
        {
            string output =
                Environment.GetEnvironmentVariable("MY_PAGE_PREVIEW_OUTPUT")
                ?? Path.GetFullPath("../../artifacts/my-page-captures");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/CafeScene.unity");
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            Quaternion rotation = Quaternion.Euler(12, player.transform.eulerAngles.y, 0);
            camera.transform.SetPositionAndRotation(
                player.transform.position + Vector3.up * 1.5f + rotation * new Vector3(0, 0, -4),
                rotation
            );
            foreach (Canvas other in BuilderTools.FindAllInScene<Canvas>(scene))
                other.gameObject.SetActive(false);
            var view = BuilderTools.FindSingleInScene<MyPageView>(scene);
            GameObject root = view.gameObject;
            root.SetActive(true);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = .5f;

            Transform profile = Show(root, "MyPage");
            profile.Find("LogoutDialog").gameObject.SetActive(false);
            profile.Find("WithdrawDialog").gameObject.SetActive(false);
            profile.Find("Window/Profile").GetComponent<TMPro.TMP_Text>().text =
                MyPageText.ProfileForDisplay(
                    new OnboardingUser
                    {
                        Nickname = "tumblur",
                        AvatarType = AvatarType.F,
                        CurrentLanguage = "en-US",
                    }
                );
            profile.Find("Window/Status").GetComponent<TMPro.TMP_Text>().text = "";
            RenderAll(camera, root, profile, output, "my-page");

            Transform settings = Show(root, "Settings");
            settings.Find("Window/Status").GetComponent<TMPro.TMP_Text>().text = "";
            Transform consents = settings.Find("Window/Consents");
            ConsentRowView template = consents
                .Find("ConsentRowTemplate")
                .GetComponent<ConsentRowView>();
            foreach (
                string title in new[]
                {
                    "음성 처리 동의",
                    "AI 개선에 내 대화 기록 활용",
                    "AI 개선에 내 녹음 음성 활용",
                }
            )
            {
                ConsentRowView row = UnityEngine.Object.Instantiate(template, consents, false);
                row.gameObject.SetActive(true);
                row.Show(
                    new ConsentTerm { Title = title, Url = "about:blank" },
                    showRequirement: false
                );
            }
            template.gameObject.SetActive(false);
            SetSlider(settings, "Window/LookSensitivity", "1.0배", 1f);
            SetSlider(settings, "Window/NpcSpeechSpeed", "1.1배", 1.1f);
            ShowLevels(settings.Find("Window/ConversationLevel/Row/Choices"), selected: 2);
            // 효과음은 켜진 상태로 찍어 고른 버튼의 강조색과 크기 막대를 본다.
            Transform volume = settings.Find("Window/SoundEffects/VolumeRow");
            volume.Find("Slider").GetComponent<Slider>().SetValueWithoutNotify(.8f);
            volume.Find("Value").GetComponent<TMPro.TMP_Text>().text = "80%";
            Transform sound = settings.Find("Window/SoundEffects/Row/Choices");
            sound.Find("OnButton").GetComponent<ThemedElement>().SetSelected(true);
            sound.Find("OffButton").GetComponent<ThemedElement>().SetSelected(false);
            RenderAll(camera, root, settings, output, "settings");
            float windowHeight = ((RectTransform)settings.Find("Window")).rect.height;
            Debug.Log(
                $"내 정보·설정 창을 {output}에 저장했습니다. 설정 창 높이 {windowHeight:0}(캔버스 기준 1080)."
            );
        }

        // HUD에서 이 창만 켠다.
        private static Transform Show(GameObject root, string overlayName)
        {
            foreach (Transform child in root.transform)
                child.gameObject.SetActive(child.name == overlayName);
            Transform overlay = root.transform.Find(overlayName);
            overlay.Find("Window").gameObject.SetActive(true);
            return overlay;
        }

        private static void RenderAll(
            Camera camera,
            GameObject root,
            Transform overlay,
            string output,
            string name
        )
        {
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                UiThemeSelection.Shared.Select(theme);
                foreach (ThemedElement element in root.GetComponentsInChildren<ThemedElement>(true))
                    element.ApplyCurrent();
                Render(camera, overlay, 1600, 1000, Path.Combine(output, $"{name}-{theme}.png"));
            }
            Render(camera, overlay, 540, 960, Path.Combine(output, $"{name}-mobile.png"));
            // 16:9 PC 화면은 캔버스 기준 높이(1080)가 가장 빠듯하므로 창이 이 높이 안에 드는지 따로 본다.
            Render(camera, overlay, 1920, 1080, Path.Combine(output, $"{name}-16x9.png"));
        }

        private static void SetSlider(Transform overlay, string path, string label, float value)
        {
            Transform group = overlay.Find(path);
            group.Find("Row/Slider").GetComponent<Slider>().SetValueWithoutNotify(value);
            group.Find("Row/Value").GetComponent<TMPro.TMP_Text>().text = label;
        }

        // 화면(SettingsView)이 서버가 준 단계마다 버튼을 복제하는 것처럼 초급·중급·고급을 만들고, 고급은 잠근다.
        private static void ShowLevels(Transform choices, int selected)
        {
            Button template = choices.Find("LevelButtonTemplate").GetComponent<Button>();
            template.gameObject.SetActive(false);
            string[] names = { "초급", "중급", "고급" };
            for (int index = 0; index < names.Length; index++)
            {
                Button button = TemplateClones.Add(template, choices);
                button.GetComponentInChildren<TMPro.TMP_Text>(true).text = names[index];
                button.interactable = index < 2;
                button.GetComponent<ThemedElement>().SetSelected(index + 1 == selected);
            }
        }

        private static void Render(
            Camera camera,
            Transform overlay,
            int width,
            int height,
            string path
        )
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)overlay);
            Canvas.ForceUpdateCanvases();
            foreach (
                PillContentLayout button in overlay.GetComponentsInChildren<PillContentLayout>()
            )
                button.Arrange();
            camera.Render();
            RenderTexture.active = target;
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
    }
}
