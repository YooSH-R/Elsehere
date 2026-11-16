using System;
using System.IO;
using System.Linq;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    [InitializeOnLoad]
    public static class ShopModelsVerification
    {
        private const string Key = "A305.ShopModelsVerification";
        private static readonly string[] Rooms =
        {
            "SudsLaundromatInterior",
            "MainStreetBarberInterior",
            "LittleArcadeInterior",
        };
        private static double ready;
        private static string Output =>
            Path.GetFullPath(Application.dataPath + "/../../../artifacts/shop-models");

        static ShopModelsVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false))
                    return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    ready = EditorApplication.timeSinceStartup + 8;
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += Next;
            };
        }

        [MenuItem("Tools/A305/World/Verify Shop Models")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verification.txt", "");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".room", -1);
            Next();
        }

        private static void Next()
        {
            int index = SessionState.GetInt(Key + ".room", -1) + 1;
            SessionState.SetInt(Key + ".room", index);
            if (index == Rooms.Length)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
                return;
            }
            EditorSceneManager.OpenScene(
                TownShopSceneBuilder.Folder + "/" + Rooms[index] + ".unity"
            );
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            if (
                !SessionState.GetBool(Key, false)
                || !EditorApplication.isPlaying
                || EditorApplication.timeSinceStartup < ready
            )
                return;
            ready = double.MaxValue;
            try
            {
                int index = SessionState.GetInt(Key + ".room", 0);
                Transform[] visuals = Object
                    .FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(node => node.name == "TripoVisual")
                    .ToArray();
                int expected =
                    index == 0 ? 13
                    : index == 1 ? 2
                    : 7;
                if (visuals.Length != expected)
                    throw new InvalidOperationException(
                        $"{Rooms[index]}: {visuals.Length}/{expected}"
                    );
                foreach (Transform visual in visuals)
                {
                    if (
                        !visual
                            .GetComponentsInChildren<Renderer>()
                            .All(renderer =>
                                renderer.sharedMaterials.All(material =>
                                    material != null && material.shader.isSupported
                                )
                            )
                    )
                        throw new InvalidOperationException("모델 재질 연결 오류");
                    if (visual.parent.GetComponentsInChildren<Collider>().Length == 0)
                        throw new InvalidOperationException("기존 충돌 연결 누락");
                }
                var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
                if (rig != null)
                    rig.enabled = false;
                Camera camera = Camera.main;
                camera.transform.SetPositionAndRotation(
                    new Vector3(0, 2.9f, -.65f),
                    Quaternion.LookRotation(new Vector3(0, 1.0f, -5) - new Vector3(0, 2.9f, -.65f))
                );
                camera.fieldOfView = 76;
                Capture(camera, Rooms[index], 1920, 1080);
                Capture(camera, Rooms[index] + "-mobile", 1280, 720);
                File.AppendAllText(
                    Output + "/verification.txt",
                    $"PASS {Rooms[index]} models={visuals.Length}\n"
                );
                EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                File.AppendAllText(Output + "/verification.txt", "FAIL " + error + "\n");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(Camera camera, string name, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
