using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using A305.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Weather
{
    /// <summary>
    /// 서버 없이 마을 날씨를 확인한다. Play 중에는 날씨 목록의 버튼으로 바로 바꾸고,
    /// Play 밖에서는 목록의 날씨를 같은 자리에서 찍는다. 찍을 때 바꾼 씬은 저장하지 않는다.
    /// </summary>
    public sealed class TownWeatherPreview : EditorWindow
    {
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";

        [MenuItem("Tools/A305/Weather/Weather Preview")]
        private static void Open() => GetWindow<TownWeatherPreview>("Weather Preview");

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "TownScene을 Play하면 날씨 버튼이 나옵니다. Play 밖에서는 날씨별 사진을 찍을 수 있습니다.",
                    MessageType.Info
                );
                if (GUILayout.Button("Capture Town Weather Preview"))
                    Capture();
                return;
            }
            var presenter = FindFirstObjectByType<WeatherPresenter>();
            if (presenter == null)
            {
                EditorGUILayout.HelpBox(
                    "이 씬에는 날씨 표시(WeatherPresenter)가 없습니다.",
                    MessageType.Warning
                );
                return;
            }
            EditorGUILayout.LabelField(
                "지금 날씨",
                presenter.Current != null ? presenter.Current.Code : "씬 디자인"
            );
            foreach (WeatherDefinition weather in presenter.Settings.Weathers)
            {
                if (weather != null && GUILayout.Button(weather.Code))
                    presenter.Show(weather.Code, null);
            }
            EditorGUILayout.HelpBox(
                "서버 상태를 다시 읽으면(씬 진입·NPC 응답·대화 종료) 서버 예보로 돌아갑니다.",
                MessageType.None
            );
        }

        private void OnInspectorUpdate() => Repaint();

        [MenuItem("Tools/A305/Weather/Capture Town Weather Preview")]
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("TOWN_WEATHER_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output) && !Application.isBatchMode)
                output = EditorUtility.SaveFolderPanel("날씨 미리보기를 저장할 폴더", "", "");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    "저장 폴더를 고르거나 TOWN_WEATHER_PREVIEW_OUTPUT에 폴더 경로를 지정하세요."
                );
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Directory.CreateDirectory(output);
            string[] codes = BuilderTools
                .FindSingleInScene<WeatherPresenter>(EditorSceneManager.OpenScene(ScenePath))
                .Settings.Weathers.Where(weather => weather != null)
                .Select(weather => weather.Code)
                .ToArray();
            foreach (string code in codes)
            {
                // 날씨마다 씬을 다시 열어 앞 날씨로 바꾼 값이 남지 않게 한다.
                Scene scene = EditorSceneManager.OpenScene(ScenePath);
                foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                    canvas.gameObject.SetActive(false);
                var weather = BuilderTools.FindSingleInScene<WeatherPresenter>(scene);
                weather.ShowImmediately(code);
                // Edit 모드에서는 파티클이 흐르지 않으므로 효과가 내려온 뒤로 미리 돌린다.
                foreach (
                    ParticleSystem particles in weather.GetComponentsInChildren<ParticleSystem>()
                )
                    particles.Simulate(1.5f, false, true);
                Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
                Camera camera = BuilderTools
                    .FindSingleInScene<PlayerCameraRig>(scene)
                    .GetComponent<Camera>();
                Vector3 eye = player.position - player.forward * 5f + Vector3.up * 2.4f;
                Vector3 target = player.position + player.forward * 20f + Vector3.up * 4f;
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                Render(
                    camera,
                    Path.Combine(output, "town-" + code.ToLowerInvariant() + ".png"),
                    1600,
                    900
                );
            }
            // 날씨를 입힌 채로 남은 씬을 실수로 저장하지 않게 디스크의 TownScene을 다시 연다.
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void Render(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
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
                DestroyImmediate(target);
                DestroyImmediate(pixels);
            }
        }
    }
}
