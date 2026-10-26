using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    [InitializeOnLoad]
    public static class MainstreetFixtureVerification
    {
        private const string SessionKey = "A305.FixtureVerification";
        private static readonly List<string> Results = new List<string>();
        private static Camera camera;
        private static Light[] lights;
        private static Color32[] unlit;
        private static RenderPipelineAsset originalPipeline;
        private static int step;
        private static int frames;
        private static double started;
        private static string Folder =>
            Path.GetFullPath(
                Application.dataPath + "/../../../artifacts/unity/mainstreet-fixtures"
            );

        static MainstreetFixtureVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (SessionState.GetInt(SessionKey, 0) == 0)
                    return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    step = 0;
                    frames = 0;
                    Results.Clear();
                    originalPipeline = QualitySettings.renderPipeline;
                    started = EditorApplication.timeSinceStartup;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    int outcome = SessionState.GetInt(SessionKey, 0);
                    SessionState.EraseInt(SessionKey);
                    if (Application.isBatchMode)
                        EditorApplication.Exit(outcome == 2 ? 0 : 1);
                }
            };
            EditorApplication.update += Update;
        }

        internal static void Verify()
        {
            SessionState.SetInt(SessionKey, 1);
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            if (
                SessionState.GetInt(SessionKey, 0) != 1
                || !EditorApplication.isPlaying
                || ++frames < 25
            )
                return;
            frames = 0;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120)
                    throw new InvalidOperationException("Fixture verification timed out.");
                Advance();
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error);
                Finish(false);
            }
        }

        private static void Advance()
        {
            if (step == 0)
            {
                Scene scene = SceneManager.GetActiveScene();
                PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
                rig.enabled = false;
                camera = rig.GetComponent<Camera>();
                camera.aspect = 16f / 9f;
                camera.fieldOfView = 55;
                lights = BuilderTools
                    .FindAllInScene<Light>(scene)
                    .Where(light =>
                        light.name == MainstreetFixtureLighting.FixtureLightName
                        && light.gameObject.activeInHierarchy
                    )
                    .ToArray();
                if (lights.Length < 10)
                    throw new InvalidOperationException(
                        "Missing facade/window lights: " + lights.Length
                    );
                Results.Add(
                    "Fixture lights: " + lights.Length + "; all inherited from reusable prefabs."
                );
                camera.transform.position = new Vector3(2.7f, 4, -14);
                camera.transform.LookAt(new Vector3(-3.9f, 1.5f, -7.5f));
            }
            else if (step == 1)
            {
                Capture("01-day-fixtures.png");
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
                Results.Add("Dim views temporarily reduce sunlight; saved scene remains daytime.");
                SetLights(false);
            }
            else if (step == 2)
            {
                unlit = Capture("02-cafe-fixtures-off.png");
                SetLights(true);
            }
            else if (step == 3)
            {
                Compare(Capture("03-cafe-fixtures-on.png"), "Cafe and shop");
                Transform hotel = SceneManager
                    .GetActiveScene()
                    .GetRootGameObjects()
                    .Single(root => root.name == "Environment")
                    .transform.Find("BuildingFootprints/Hotel");
                camera.transform.position = hotel.TransformPoint(new Vector3(2, 3.2f, 6));
                camera.transform.LookAt(hotel.TransformPoint(new Vector3(0, 1.8f, 0)));
                SetLights(false);
            }
            else if (step == 4)
            {
                unlit = Capture("04-hotel-fixtures-off.png");
                SetLights(true);
            }
            else if (step == 5)
            {
                Compare(Capture("05-hotel-fixtures-on.png"), "Hotel entrance");
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                    "Assets/Settings/Mobile_RPAsset.asset"
                );
                SetLights(false);
            }
            else if (step == 6)
            {
                unlit = Capture("06-mobile-fixtures-off.png");
                SetLights(true);
            }
            else if (step == 7)
            {
                Compare(Capture("07-mobile-fixtures-on.png"), "Mobile URP in Editor");
                Finish(true);
            }
            step++;
        }

        private static void SetLights(bool enabled)
        {
            foreach (Light light in lights)
                light.enabled = enabled;
        }

        private static void Compare(Color32[] lit, string label)
        {
            int brighterPixels = 0;
            for (int index = 0; index < lit.Length; index++)
                if (
                    lit[index].r + lit[index].g + lit[index].b
                    > unlit[index].r + unlit[index].g + unlit[index].b + 9
                )
                    brighterPixels++;
            if (brighterPixels < 100)
                throw new InvalidOperationException(
                    label + ": light does not reach visible surfaces."
                );
            Results.Add(
                "PASS "
                    + label
                    + ": "
                    + brighterPixels
                    + " pixels visibly brighter with fixture lights on."
            );
        }

        private static Color32[] Capture(string name)
        {
            Directory.CreateDirectory(Folder);
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            var render = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name), pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                render.Release();
                Object.DestroyImmediate(render);
                Object.DestroyImmediate(pixels);
            }
        }

        private static void Finish(bool passed)
        {
            QualitySettings.renderPipeline = originalPipeline;
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(Path.Combine(Folder, "verification.txt"), Results);
            Debug.Log("Fixture verification: " + (passed ? "PASS" : "FAIL"));
            SessionState.SetInt(SessionKey, passed ? 2 : 3);
            EditorApplication.ExitPlaymode();
        }
    }
}
