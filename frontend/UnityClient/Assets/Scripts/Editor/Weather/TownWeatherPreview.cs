using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using A305.Theme;
using A305.Weather;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Weather
{
    /// <summary>
    /// 서버 없이 날씨와 밤낮을 확인한다. Play 중에는 버튼으로 마을 날씨를 바꾸거나 게임 시각을 앞으로 돌리고,
    /// Play 밖에서는 마을의 날씨·시각별 모습과 실내의 낮·밤을 같은 자리에서 찍는다. 찍을 때 바꾼 씬은 저장하지 않는다.
    /// </summary>
    public sealed class TownWeatherPreview : EditorWindow
    {
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const double MinutesPerDay = 24 * 60;
        private const double Midday = 12 * 60 + 30;
        private const double Midnight = 0;

        private static readonly (string name, string label, double minutes)[] Times =
        {
            ("dawn", "새벽 6:00", 6 * 60),
            ("noon", "한낮 12:30", Midday),
            ("sunset", "노을 19:00", 19 * 60),
            ("night", "밤 0:00", Midnight),
        };

        private static readonly string[] Interiors =
        {
            "HomeScene",
            "CafeScene",
            "HotelScene",
            "ShopScene",
        };

        [MenuItem("Tools/A305/Weather/Weather Preview")]
        private static void Open() => GetWindow<TownWeatherPreview>("Weather Preview");

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "장소 씬을 Play하면 날씨·시각 버튼이 나옵니다. Play 밖에서는 마을의 날씨·시각별 모습과 실내의 낮·밤을 찍을 수 있습니다.",
                    MessageType.Info
                );
                if (GUILayout.Button("Capture Town Weather Preview"))
                    Capture();
                return;
            }
            ShowTimeButtons();
            var presenter = FindFirstObjectByType<WeatherPresenter>();
            if (presenter == null)
                return;
            EditorGUILayout.Space();
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

        private static void ShowTimeButtons()
        {
            EditorGUILayout.LabelField("게임 시각", GameClockText.Format(GameClock.Shared.Minutes));
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach ((string _, string label, double minutes) in Times)
                {
                    if (GUILayout.Button(label))
                        FlowTo(minutes);
                }
            }
            EditorGUILayout.HelpBox(
                "게임 시각을 그 시각까지 앞으로 흘립니다. 서버에 연결돼 있으면 다음 신호(30초마다)에 서버 시각으로 돌아갑니다.",
                MessageType.None
            );
        }

        // 게임 시계는 앞으로만 흐르므로 다음에 오는 그 시각까지 흘린다.
        private static void FlowTo(double minuteOfDay)
        {
            double now = GameClock.Shared.Minutes % MinutesPerDay;
            double ahead = (minuteOfDay - now + MinutesPerDay) % MinutesPerDay;
            GameClock.Shared.Flow(ahead / GameClock.GameMinutesPerRealSecond);
        }

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
                CaptureTown(output, code, Midday, "town-" + code.ToLowerInvariant());
            foreach ((string name, string _, double minutes) in Times)
                CaptureTown(output, "CLEAR", minutes, "town-clear-" + name);
            CaptureTown(output, "RAIN", Midnight, "town-rain-night");
            foreach (string interior in Interiors)
            {
                CaptureInterior(output, interior, Midday, "noon");
                CaptureInterior(output, interior, Midnight, "night");
            }
            // 날씨·시각을 입힌 채로 남은 씬을 실수로 저장하지 않게 디스크의 TownScene을 다시 연다.
            EditorSceneManager.OpenScene(ScenePath);
            // 마지막에 찍은 밤 시각 때문에 Editor의 유리 UI가 밝혀진 채로 남지 않게 한다.
            UiBackdropLift.Show(0f);
        }

        private static void CaptureTown(string output, string code, double minutes, string file)
        {
            // 찍을 때마다 씬을 다시 열어 앞에서 바꾼 값이 남지 않게 한다.
            Scene scene = OpenWithoutCanvases(ScenePath);
            var weather = BuilderTools.FindSingleInScene<WeatherPresenter>(scene);
            weather.ShowImmediately(code);
            weather.ShowTime(minutes);
            // Edit 모드에서는 파티클이 흐르지 않으므로 효과가 내려온 뒤로 미리 돌린다.
            foreach (ParticleSystem particles in weather.GetComponentsInChildren<ParticleSystem>())
                particles.Simulate(1.5f, false, true);
            Render(ViewBehindPlayer(scene, 5f, 2.4f, 20f, 4f), Path.Combine(output, file + ".png"));
        }

        private static void CaptureInterior(
            string output,
            string sceneName,
            double minutes,
            string time
        )
        {
            Scene scene = OpenWithoutCanvases("Assets/Scenes/Production/" + sceneName + ".unity");
            BuilderTools.FindSingleInScene<InteriorDaylight>(scene).ShowTime(minutes);
            string file = sceneName.Replace("Scene", "").ToLowerInvariant() + "-" + time + ".png";
            // 실내는 입구 바로 앞에서 시작하는 곳이 있어, 카메라가 벽 밖으로 나가지 않게 가까이 둔다.
            Render(ViewBehindPlayer(scene, 1.2f, 1.8f, 6f, 1.2f), Path.Combine(output, file));
        }

        private static Scene OpenWithoutCanvases(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                canvas.gameObject.SetActive(false);
            return scene;
        }

        private static Camera ViewBehindPlayer(
            Scene scene,
            float back,
            float eyeHeight,
            float ahead,
            float lookHeight
        )
        {
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            Vector3 eye = player.position - player.forward * back + Vector3.up * eyeHeight;
            Vector3 target = player.position + player.forward * ahead + Vector3.up * lookHeight;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            return camera;
        }

        private static void Render(Camera camera, string path)
        {
            const int width = 1600;
            const int height = 900;
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
