using System;
using System.IO;
using A305.EditorTools.Common;
using A305.Player;
using A305.Weather;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.World
{
    [InitializeOnLoad]
    public static class TownHorizonVerification
    {
        private const string SessionKey = "A305.TownHorizonVerification";
        private static int frames;
        private static bool passed;
        private static int stage;
        private static bool finishing;

        static TownHorizonVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (
                    state != PlayModeStateChange.EnteredEditMode
                    || !SessionState.GetBool(SessionKey, false)
                )
                    return;
                SessionState.EraseBool(SessionKey);
                if (Application.isBatchMode)
                    EditorApplication.Exit(passed ? 0 : 1);
            };
        }

        [MenuItem("Tools/A305/World/Verify Town Horizon")]
        public static void Verify()
        {
            if (!BuilderTools.CanBuild())
                return;
            EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            if (
                !SessionState.GetBool(SessionKey, false)
                || finishing
                || !EditorApplication.isPlaying
                || ++frames < 40
            )
                return;
            frames = 0;
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                foreach (
                    GameClockDriver clock in BuilderTools.FindAllInScene<GameClockDriver>(scene)
                )
                    clock.enabled = false;
                PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
                player.SetMovementLocked(true);
                PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
                rig.enabled = false;
                Camera camera = rig.GetComponent<Camera>();
                var controller = player.GetComponent<CharacterController>();
                if (stage == 0)
                {
                    TownHorizonBuilder.VerifyColliders();
                    foreach (int side in new[] { -1, 1 })
                    {
                        float edge = side > 0 ? 69 : -32;
                        controller.enabled = false;
                        player.transform.position = new Vector3(0, .2f, edge - side * 4);
                        controller.enabled = true;
                        for (int step = 0; step < 60; step++)
                            controller.Move(new Vector3(0, -.02f, side * .25f));
                        if ((player.transform.position.z - edge) * side > 0)
                            throw new InvalidOperationException(
                                "플레이어가 북남 경계를 통과했습니다."
                            );
                    }
                    SetView(scene, player, controller, camera, true, 720);
                    stage++;
                    return;
                }
                string name =
                    stage == 1 ? "north-play"
                    : stage == 2 ? "south-play"
                    : "north-night-play";
                TownHorizonBuilder.Capture(
                    camera.transform.position,
                    camera.transform.position + camera.transform.forward * 40,
                    name,
                    camera
                );
                if (stage == 1)
                {
                    SetView(scene, player, controller, camera, false, 720);
                    stage++;
                    return;
                }
                if (stage == 2)
                {
                    SetView(scene, player, controller, camera, true, 1380);
                    stage++;
                    return;
                }
                File.WriteAllText(
                    Path.Combine(TownHorizonBuilder.Output, "play-verification.txt"),
                    "PASS: CharacterController north/south movement blocked; boundary raycasts; noon and night Play camera captures.\n"
                );
                passed = true;
                finishing = true;
                EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(TownHorizonBuilder.Output);
                File.WriteAllText(
                    Path.Combine(TownHorizonBuilder.Output, "play-verification.txt"),
                    error.ToString()
                );
                Debug.LogException(error);
                finishing = true;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void SetView(
            Scene scene,
            PlayerMover player,
            CharacterController controller,
            Camera camera,
            bool north,
            int minute
        )
        {
            foreach (
                WeatherPresenter weather in BuilderTools.FindAllInScene<WeatherPresenter>(scene)
            )
                weather.ShowTime(minute);
            PosePlayer(player, controller, new Vector3(0, .2f, north ? 54 : -17), north ? 0 : 180);
            Vector3 position = new Vector3(0, 3.4f, north ? 49 : -12);
            Vector3 target = new Vector3(0, 2.7f, north ? 92 : -64);
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(target - position)
            );
        }

        private static void PosePlayer(
            PlayerMover player,
            CharacterController controller,
            Vector3 position,
            float yaw
        )
        {
            controller.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            controller.enabled = true;
        }
    }
}
