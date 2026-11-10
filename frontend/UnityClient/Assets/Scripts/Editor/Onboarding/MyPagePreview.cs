using System;
using System.IO;
using A305.EditorTools.Common;
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
    /// 실제 HUD 프리팹의 내 정보 창을 세 테마와 세로 화면으로 렌더링한다. 서버를 부르거나 씬을 저장하지 않는다.
    /// 시점 감도·NPC 말 빠르기 줄처럼 창에 줄을 더했을 때 겹치거나 잘리지 않는지 확인하는 용도다.
    /// </summary>
    public static class MyPagePreview
    {
        [MenuItem("Tools/A305/Onboarding/Capture My Page")]
        public static void Capture()
        {
            string output =
                Environment.GetEnvironmentVariable("MY_PAGE_PREVIEW_OUTPUT")
                ?? Path.GetFullPath("../../artifacts/my-page-captures");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (Canvas other in BuilderTools.FindAllInScene<Canvas>(scene))
                other.gameObject.SetActive(false);
            var view = BuilderTools.FindSingleInScene<MyPageView>(scene);
            GameObject root = view.gameObject;
            root.SetActive(true);
            Transform overlay = root.transform.Find("MyPage");
            overlay.gameObject.SetActive(true);
            overlay.Find("Window").gameObject.SetActive(true);
            SetSlider(overlay, "Window/LookSensitivity", "1.0배", 1f);
            SetSlider(overlay, "Window/NpcSpeechSpeed", "1.3배", 1.3f);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = .5f;
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                UiThemeSelection.Shared.Select(theme);
                foreach (ThemedElement element in root.GetComponentsInChildren<ThemedElement>(true))
                    element.ApplyCurrent();
                Render(camera, overlay, 1600, 1000, Path.Combine(output, $"my-page-{theme}.png"));
            }
            Render(camera, overlay, 540, 960, Path.Combine(output, "my-page-mobile.png"));
            Debug.Log($"내 정보 창을 {output}에 저장했습니다.");
        }

        private static void SetSlider(Transform overlay, string path, string label, float value)
        {
            Transform group = overlay.Find(path);
            group.Find("Row/Slider").GetComponent<Slider>().SetValueWithoutNotify(value);
            group.Find("Row/Value").GetComponent<TMPro.TMP_Text>().text = label;
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
