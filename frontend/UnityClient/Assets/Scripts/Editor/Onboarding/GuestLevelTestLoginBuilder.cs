using System;
using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using A305.Onboarding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Onboarding
{
    /// <summary>기존 입구 안내창 디자인을 재사용해 레벨테스트 위에 로그인 안내 모달을 연결한다.</summary>
    public static class GuestLevelTestLoginBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";

        [MenuItem("Tools/A305/Onboarding/Apply Guest Level Test Login")]
        public static void ApplyToPrefab()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(PrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            Transform original = root.transform.Find("GuestReturnPanel");
            if (original == null)
                return;
            Transform old = root.transform.Find("GuestLevelTestLogin");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var overlay = new GameObject(
                "GuestLevelTestLogin",
                typeof(RectTransform),
                typeof(Image)
            );
            overlay.transform.SetParent(root.transform, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(.16f, .12f, .09f, .38f);
            Transform panel = Object.Instantiate(original, overlay.transform);
            panel.name = "Panel";
            Object.DestroyImmediate(panel.GetComponent<GuestReturnPanelView>());
            foreach (LayoutGroup layout in panel.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            foreach (ContentSizeFitter fitter in panel.GetComponents<ContentSizeFitter>())
                Object.DestroyImmediate(fitter);
            panel.gameObject.SetActive(true);
            var panelRect = (RectTransform)panel;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(720, 542.4f);
            TMP_Text title = panel.Find("Title").GetComponent<TMP_Text>();
            title.text = "로그인하고 시작해요";
            title.fontSize = 40;
            TMP_Text body = panel.Find("Body").GetComponent<TMP_Text>();
            body.text =
                "레벨테스트는 로그인 후 이용할 수 있어요.\n지금은 게스트로 계속 둘러볼 수도 있어요.";
            body.fontSize = 24;
            panel.Find("Notice").gameObject.SetActive(false);
            Button login = panel.Find("SignupButton").GetComponent<Button>();
            login.gameObject.SetActive(true);
            login.GetComponentInChildren<TMP_Text>(true).text = "Google로 로그인";
            Button keep = panel.Find("ContinueButton").GetComponent<Button>();
            keep.gameObject.SetActive(true);
            keep.GetComponentInChildren<TMP_Text>(true).text = "게스트로 계속하기";
            Button close = UiBuilderTools.FindOrCreateButton(
                panel,
                "CloseButton",
                "",
                title.font,
                52
            );
            UiCloseButton.Corner(close.transform, UiCloseButton.WindowSize);
            var view = overlay.AddComponent<GuestLevelTestLoginView>();
            BuilderTools.SetReference(view, "loginButton", login);
            BuilderTools.SetReference(view, "continueButton", keep);
            BuilderTools.SetReference(view, "closeButton", close);
            BuilderTools.SetReference(
                root.GetComponent<OnboardingScreen>(),
                "guestLevelTestLogin",
                view
            );
            BuilderTools.RequireReferences(view);
            overlay.SetActive(false);
        }

        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("GUEST_LEVEL_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Set GUEST_LEVEL_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
            );
            foreach (Transform child in root.transform)
                child.gameObject.SetActive(
                    child.name == "Background" || child.name == "LevelTestPanel"
                );
            root.GetComponent<OnboardingScreen>().enabled = false;
            root.transform.Find("LevelTestPanel")
                .GetComponent<LevelTestPanelView>()
                .Show(null, false, true);
            var screen = new SerializedObject(root.GetComponent<OnboardingScreen>());
            ((EntryBackdropView)screen.FindProperty("backdrop").objectReferenceValue).Show(true);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (
                Vector2Int size in new[] { new Vector2Int(1600, 900), new Vector2Int(844, 390) }
            )
            {
                root.transform.Find("GuestLevelTestLogin").gameObject.SetActive(false);
                Render(camera, size, Path.Combine(output, $"guest-intro-{size.x}.png"));
                root.transform.Find("GuestLevelTestLogin").gameObject.SetActive(true);
                Render(camera, size, Path.Combine(output, $"guest-login-{size.x}.png"));
            }
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camera.gameObject);
        }

        private static void Render(Camera camera, Vector2Int size, string path)
        {
            var target = new RenderTexture(size.x, size.y, 24);
            camera.targetTexture = target;
            camera.aspect = (float)size.x / size.y;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }
    }
}
