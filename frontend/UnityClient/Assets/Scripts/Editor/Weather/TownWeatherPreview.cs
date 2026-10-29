using System;
using System.IO;
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
    /// 서버 없이 마을 날씨를 확인한다. Play 중에는 메뉴로 날씨를 바로 바꾸고,
    /// Play 밖에서는 맑음·흐림·비를 같은 자리에서 찍는다. 찍을 때 바꾼 씬은 저장하지 않는다.
    /// </summary>
    public static class TownWeatherPreview
    {
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string PlayMenu = "Tools/A305/Weather/Play Mode/";

        [MenuItem(PlayMenu + "Show Clear")]
        private static void ShowClearInPlay() => ShowInPlay(WeatherKind.Clear);

        [MenuItem(PlayMenu + "Show Cloudy")]
        private static void ShowCloudyInPlay() => ShowInPlay(WeatherKind.Cloudy);

        [MenuItem(PlayMenu + "Show Rain")]
        private static void ShowRainInPlay() => ShowInPlay(WeatherKind.Rain);

        [MenuItem(PlayMenu + "Show Clear", true)]
        [MenuItem(PlayMenu + "Show Cloudy", true)]
        [MenuItem(PlayMenu + "Show Rain", true)]
        private static bool CanShowInPlay() =>
            EditorApplication.isPlaying
            && UnityEngine.Object.FindFirstObjectByType<WeatherPresenter>() != null;

        // 서버 상태를 다시 읽으면(씬 진입·NPC 응답·대화 종료) 서버 예보로 돌아간다.
        private static void ShowInPlay(WeatherKind kind) =>
            UnityEngine.Object.FindFirstObjectByType<WeatherPresenter>().Show(kind);

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
            foreach (WeatherKind kind in Enum.GetValues(typeof(WeatherKind)))
            {
                // 날씨마다 씬을 다시 열어 앞 날씨로 바꾼 값이 남지 않게 한다.
                Scene scene = EditorSceneManager.OpenScene(ScenePath);
                foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                    canvas.gameObject.SetActive(false);
                var weather = BuilderTools.FindSingleInScene<WeatherPresenter>(scene);
                weather.ShowImmediately(kind);
                // Edit 모드에서는 파티클이 흐르지 않으므로 빗방울이 내려온 뒤로 미리 돌린다.
                if (kind == WeatherKind.Rain)
                {
                    var fields = new SerializedObject(weather);
                    var rain = (ParticleSystem)fields.FindProperty("rain").objectReferenceValue;
                    rain.Simulate(1.5f, true, true);
                }
                Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
                Camera camera = BuilderTools
                    .FindSingleInScene<PlayerCameraRig>(scene)
                    .GetComponent<Camera>();
                Vector3 eye = player.position - player.forward * 5f + Vector3.up * 2.4f;
                Vector3 target = player.position + player.forward * 20f + Vector3.up * 4f;
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                Render(
                    camera,
                    Path.Combine(output, "town-" + kind.ToString().ToLowerInvariant() + ".png"),
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
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
