using System;
using System.IO;
using A305.Onboarding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Onboarding
{
    /// <summary>서버 접속 없이 실제 입구 프리팹의 접속 대기 배경을 확인한다.</summary>
    public static class EntryWaitingPreview
    {
        public static void Capture()
        {
            EntryScreensBuilder.ApplyWaitingBackdrop();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Onboarding/EntryScreens.prefab"
                )
            );
            foreach (Transform child in root.transform)
                child.gameObject.SetActive(child.name == "Background");
            var fields = new SerializedObject(root.GetComponent<OnboardingScreen>());
            var message = (TMP_Text)fields.FindProperty("messageText").objectReferenceValue;
            message.gameObject.SetActive(true);
            message.text = "접속 상태를 확인하고 있어요…";
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            string output =
                Environment.GetEnvironmentVariable("ENTRY_PREVIEW_OUTPUT")
                ?? Path.GetFullPath("../../artifacts/entry-waiting-captures");
            Directory.CreateDirectory(output);
            Render(camera, 1600, 900, Path.Combine(output, "waiting-desktop.png"));
            Render(camera, 540, 960, Path.Combine(output, "waiting-mobile.png"));
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Debug.Log("Entry waiting previews saved to " + output + ". No server calls.");
        }

        private static void Render(Camera camera, int width, int height, string path)
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
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
