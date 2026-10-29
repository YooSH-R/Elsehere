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
    /// 마을의 맑음·흐림·비를 같은 자리에서 찍는다. Play나 서버 호출 없이 실행하고 씬을 저장하지 않는다.
    /// </summary>
    public static class TownWeatherPreview
    {
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";

        [MenuItem("Tools/A305/Weather/Capture Town Weather Preview")]
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("TOWN_WEATHER_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    "TOWN_WEATHER_PREVIEW_OUTPUT에 사진을 저장할 폴더 경로를 지정하세요."
                );
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
                var rain = (ParticleSystem)
                    new SerializedObject(weather).FindProperty("rain").objectReferenceValue;
                if (kind == WeatherKind.Rain)
                    rain.Simulate(1.5f, true, true);
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
