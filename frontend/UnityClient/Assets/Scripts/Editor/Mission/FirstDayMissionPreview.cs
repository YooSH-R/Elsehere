using System;
using System.IO;
using System.Linq;
using A305.Api;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Mission;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Mission
{
    /// <summary>저장해 둔 서버 응답으로 UI를 렌더링한다. Play나 API·모델 호출을 하지 않는다.</summary>
    public static class FirstDayMissionPreview
    {
        [MenuItem("Tools/A305/Mission/Capture First Day Preview")]
        public static void Capture()
        {
            string input = Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_JSON");
            string output = Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    "FIRST_DAY_PREVIEW_JSON과 FIRST_DAY_PREVIEW_OUTPUT에 저장된 서버 응답과 출력 폴더 경로를 지정하세요."
                );
            FirstDayMissionState state = ApiJson.Deserialize<FirstDayMissionState>(
                File.ReadAllText(input)
            );
            string sceneName =
                Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_SCENE") ?? "CafeScene";
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/" + sceneName + ".unity"
            );
            var view = BuilderTools.FindSingleInScene<FirstDayMissionView>(scene);
            // 오른쪽 위 단어장 칸도 함께 그려 왼쪽 위 퀘스트 화면과 겹치는지 확인한다.
            var vocabulary = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            Canvas[] shown = BuilderTools
                .FindAllInScene<Canvas>(scene)
                .Where(canvas =>
                    canvas.gameObject == view.gameObject
                    || (vocabulary != null && canvas.gameObject == vocabulary.gameObject)
                )
                .ToArray();
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                canvas.gameObject.SetActive(shown.Contains(canvas));
            var camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            var npc = BuilderTools.FindAllInScene<NpcIdentity>(scene).FirstOrDefault();
            if (npc != null)
            {
                Vector3 target = npc.LookTarget.position;
                Vector3 position = target + npc.transform.forward * 2.8f;
                camera.transform.SetPositionAndRotation(
                    position,
                    Quaternion.LookRotation(target - position)
                );
            }
            foreach (Canvas overlay in shown)
            {
                overlay.renderMode = RenderMode.ScreenSpaceCamera;
                overlay.worldCamera = camera;
                overlay.planeDistance = .5f;
            }
            view.SetAvailable(true);
            view.BindMap(
                A305.World.PlaceScenes.CodeOf(sceneName),
                BuilderTools.FindSingleInScene<PlayerMover>(scene).transform
            );
            view.Refresh(state, false, false);
            Directory.CreateDirectory(output);
            var fields = new SerializedObject(view);
            var drawer = (GameObject)fields.FindProperty("journal").objectReferenceValue;
            // 수첩을 펼치면 같은 내용의 목표 카드는 숨긴다.
            GameObject banner = ((Component)fields.FindProperty("situation").objectReferenceValue)
                .transform
                .parent
                .gameObject;
            drawer.SetActive(false);
            Render(camera, view, Path.Combine(output, "quest-npc.png"), 1920, 1080);
            drawer.SetActive(true);
            banner.SetActive(false);
            Render(camera, view, Path.Combine(output, "quest-journal.png"), 1920, 1080);
            Render(camera, view, Path.Combine(output, "quest-mobile.png"), 1080, 1920);
        }

        private static void Render(
            Camera camera,
            FirstDayMissionView view,
            string path,
            int width,
            int height
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                // 카메라 대상 크기가 바뀐 뒤 CanvasScaler가 새 픽셀 영역을 계산하게 한다.
                camera.Render();
                Canvas.ForceUpdateCanvases();
                view.UpdateLayout();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
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
