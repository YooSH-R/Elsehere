using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>동일한 카메라의 광원 OFF/ON 렌더로 바닥에 실제 빛이 도달하는지 확인한다.</summary>
    [InitializeOnLoad]
    public static class MainstreetStreetlightVerification
    {
        private const string SessionKey = "A305.StreetlightVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static Light[] lights;
        private static Camera camera;
        private static float[] unlitSamples;
        private static int stage;
        private static int frames;
        private static RenderPipelineAsset originalPipeline;
        private static double started;

        private static string OutputFolder =>
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "../../../artifacts/unity/mainstreet-streetlights"
                )
            );

        static MainstreetStreetlightVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        internal static void Verify()
        {
            EditorSceneManager.OpenScene(TownMainstreetBuilder.ScenePath);
            SessionState.SetInt(SessionKey, 1);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (SessionState.GetInt(SessionKey, 0) == 0)
                return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Results.Clear();
                Errors.Clear();
                stage = 0;
                frames = 0;
                originalPipeline = QualitySettings.renderPipeline;
                started = EditorApplication.timeSinceStartup;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                int outcome = SessionState.GetInt(SessionKey, 0);
                SessionState.EraseInt(SessionKey);
                if (Application.isBatchMode)
                    EditorApplication.Exit(outcome == 2 ? 0 : 1);
            }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (
                SessionState.GetInt(SessionKey, 0) == 1
                && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            )
                Errors.Add(message);
        }

        private static void Update()
        {
            if (SessionState.GetInt(SessionKey, 0) != 1 || !EditorApplication.isPlaying)
                return;
            if (++frames < 20)
                return;
            frames = 0;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90)
                    throw new InvalidOperationException("가로등 검수 시간이 초과됐습니다.");
                Advance();
            }
            catch (Exception error)
            {
                Errors.Add(error.ToString());
                Finish();
            }
        }

        private static void Advance()
        {
            switch (stage++)
            {
                case 0:
                    PrepareComparison();
                    SetLights(false);
                    break;
                case 1:
                    unlitSamples = Capture("01-day-light-off.png");
                    SetLights(true);
                    break;
                case 2:
                    CheckIllumination(Capture("02-day-light-on.png"), "Day");
                    PrepareDimDiagnostic();
                    SetLights(false);
                    break;
                case 3:
                    unlitSamples = Capture("03-dim-diagnostic-off.png");
                    SetLights(true);
                    break;
                case 4:
                    CheckIllumination(Capture("04-dim-diagnostic-on.png"), "Dim diagnostic");
                    var mobilePipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                        "Assets/Settings/Mobile_RPAsset.asset"
                    );
                    if (mobilePipeline == null)
                    {
                        Results.Add("SKIP Mobile pipeline asset unavailable.");
                        Finish();
                        break;
                    }
                    QualitySettings.renderPipeline = mobilePipeline;
                    SetLights(false);
                    break;
                case 5:
                    unlitSamples = Capture("05-mobile-diagnostic-off.png");
                    SetLights(true);
                    break;
                case 6:
                    CheckIllumination(Capture("06-mobile-diagnostic-on.png"), "Mobile diagnostic");
                    Finish();
                    break;
            }
        }

        private static void PrepareComparison()
        {
            Scene scene = SceneManager.GetActiveScene();
            lights = BuilderTools
                .FindAllInScene<Light>(scene)
                .Where(light => light.name == MainstreetStreetlightBuilder.LightName)
                .ToArray();
            int lampCount = BuilderTools
                .FindAllInScene<Transform>(scene)
                .Count(item => item.name == "StreetLampV2" && item.gameObject.activeInHierarchy);
            if (lampCount == 0 || lights.Length != lampCount)
                throw new InvalidOperationException("가로등마다 광원이 하나씩 있어야 합니다.");
            if (
                lights.Any(light =>
                    light.type != LightType.Point
                    || light.range <= 0
                    || light.intensity <= 0
                    || light.shadows != LightShadows.None
                )
            )
                throw new InvalidOperationException("가로등 Point Light 설정을 확인합니다.");
            Material bulb = AssetDatabase.LoadAssetAtPath<Material>(
                MainstreetArtKit.ArtFolder + "/LanternAmberV2.mat"
            );
            if (
                !bulb.IsKeywordEnabled("_EMISSION")
                || (bulb.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack)
                    != 0
            )
                throw new InvalidOperationException("전구의 발광 재질이 비활성 상태입니다.");
            Results.Add("PASS bulb emission keyword remains enabled after asset import.");
            Results.Add(
                "PASS "
                    + lights.Length
                    + " streetlamp instances inherit one realtime Point Light each."
            );
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            rig.enabled = false;
            camera = rig.GetComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 55;
            camera.aspect = 1920f / 1080f;
            camera.transform.position = new Vector3(2.7f, 4f, -14f);
            camera.transform.LookAt(new Vector3(-3.9f, 1.5f, -7.5f));
            scene.GetRootGameObjects().Single(root => root.name == "Characters").SetActive(false);
        }

        private static void PrepareDimDiagnostic()
        {
            // Temporary inspection light; no time-of-day system or saved scene change.
            foreach (
                Light light in BuilderTools.FindAllInScene<Light>(SceneManager.GetActiveScene())
            )
                if (light.type == LightType.Directional)
                    light.intensity *= .1f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.12f, .15f, .22f);
            RenderSettings.fog = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .18f, .25f);
            Results.Add(
                "NOTE Dim images use temporary reduced sunlight; the saved Sandbox stays daytime."
            );
        }

        private static void SetLights(bool enabled)
        {
            foreach (Light light in lights)
                light.enabled = enabled;
        }

        private static void CheckIllumination(float[] litSamples, string condition)
        {
            string[] surfaces = { "road (SoftMasonry)", "paving (URP Lit)" };
            for (int index = 0; index < surfaces.Length; index++)
            {
                float rise = litSamples[index] - unlitSamples[index];
                if (rise < .002f)
                    throw new InvalidOperationException(
                        condition + ": no visible light on " + surfaces[index] + ", delta " + rise
                    );
                Results.Add(
                    "PASS "
                        + condition
                        + ": "
                        + surfaces[index]
                        + " rendered luminance rises by "
                        + rise.ToString("F4")
                );
            }
        }

        private static float[] Capture(string name)
        {
            Directory.CreateDirectory(OutputFolder);
            RenderTexture previous = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var render = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(OutputFolder, name), pixels.EncodeToPNG());
                Vector3[] points =
                {
                    new Vector3(-2.1f, .02f, -8.5f),
                    new Vector3(-4.1f, .135f, -8.6f),
                    new Vector3(-3.45f, 3f, -7.5f),
                };
                var samples = new float[points.Length];
                for (int index = 0; index < points.Length; index++)
                {
                    Vector3 view = camera.WorldToViewportPoint(points[index]);
                    if (
                        view.z <= 0
                        || view.x < .01f
                        || view.x > .99f
                        || view.y < .01f
                        || view.y > .99f
                    )
                        throw new InvalidOperationException("조명 표본 위치가 카메라 밖입니다.");
                    Color[] patch = pixels.GetPixels(
                        (int)(view.x * 1920) - 2,
                        (int)(view.y * 1080) - 2,
                        5,
                        5
                    );
                    samples[index] = patch.Average(pixel =>
                        .2126f * pixel.r + .7152f * pixel.g + .0722f * pixel.b
                    );
                }
                if (samples[2] < .6f)
                    throw new InvalidOperationException(
                        "렌더에서 전구의 발광이 보이지 않습니다: " + name
                    );
                Results.Add("CAPTURE " + name + ", bulb luminance " + samples[2].ToString("F3"));
                return samples;
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                render.Release();
                Object.DestroyImmediate(render);
                Object.DestroyImmediate(pixels);
            }
        }

        private static void Finish()
        {
            QualitySettings.renderPipeline = originalPipeline;
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllLines(
                Path.Combine(OutputFolder, "verification.txt"),
                Results.Concat(Errors.Select(error => "FAIL " + error))
            );
            SessionState.SetInt(SessionKey, Errors.Count == 0 ? 2 : 3);
            Debug.Log("Streetlight verification: " + Errors.Count + " errors.");
            EditorApplication.ExitPlaymode();
        }
    }
}
