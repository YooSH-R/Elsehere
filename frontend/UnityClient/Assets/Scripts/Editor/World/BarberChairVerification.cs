using System;
using System.IO;
using System.Linq;
using A305.Learning;
using A305.Mission;
using A305.Npc;
using A305.Player;
using A305.Tutorial;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    [InitializeOnLoad]
    public static class BarberChairVerification
    {
        private const string Key = "A305.BarberChairVerification";
        private static double ready;
        private static int phase;
        private static RestSeat seat;
        private static string Output =>
            Path.GetFullPath(Application.dataPath + "/../../../artifacts/barber-fit");

        static BarberChairVerification()
        {
            SceneManager.sceneLoaded += (_, _) =>
            {
                if (!SessionState.GetBool(Key, false))
                    return;
                TutorialState.MarkFinishedOnServer();
                foreach (
                    MonoBehaviour component in Object.FindObjectsByType<MonoBehaviour>(
                        FindObjectsSortMode.None
                    )
                )
                {
                    if (
                        component is FirstDayMissionController
                        || component is LearningController
                        || component is GameClockDriver
                        || component is TutorialDirector
                        || component is Passerby
                        || component is NpcRoutine
                        || component is NpcPopulation
                    )
                        component.enabled = false;
                    if (component is TutorialGuideView guide)
                        guide.Hide();
                    if (component is ControlsGuideView controls)
                        controls.SetBlocked(true);
                }
            };
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (
                    SessionState.GetBool(Key, false)
                    && state == PlayModeStateChange.EnteredPlayMode
                )
                {
                    ready = EditorApplication.timeSinceStartup + 8;
                    phase = 0;
                }
            };
        }

        [MenuItem("Tools/A305/World/Verify Barber Chair Sitting")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(BarberChairFitBuilder.ScenePath);
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
            try
            {
                var player = Object.FindFirstObjectByType<PlayerMover>();
                var offer = Object.FindFirstObjectByType<PasserbyOfferView>();
                if (phase == 0)
                {
                    TutorialState.MarkFinishedOnServer();
                    seat = Object
                        .FindObjectsByType<RestSeat>(FindObjectsSortMode.None)
                        .Where(item =>
                            item.transform.parent != null
                            && item.transform.parent.name == "BarberChair"
                            && !item.IsTaken
                        )
                        .OrderByDescending(item => item.transform.position.z)
                        .First();
                    Vector3 standing = seat.transform.position - seat.transform.right * .85f;
                    standing.y = seat.transform.position.y + .1f;
                    player.Teleport(standing, seat.transform.rotation);
                    player.SetMovementLocked(false);
                    ready = EditorApplication.timeSinceStartup + 1;
                    phase++;
                    return;
                }
                if (phase == 1)
                {
                    if (!offer.IsTalkPromptShownBy(seat))
                        throw new InvalidOperationException(
                            "이발 의자에 접근했지만 앉기 제안이 표시되지 않습니다."
                        );
                    offer.ShownButtonRect.GetComponent<Button>().onClick.Invoke();
                    if (!seat.IsPlayerSeated || !player.GetComponent<PlayerSeatPose>().IsSeated)
                        throw new InvalidOperationException("실제 착석 실패");
                    ready = EditorApplication.timeSinceStartup + 1;
                    phase++;
                    return;
                }
                var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
                rig.enabled = false;
                Camera camera = Camera.main;
                Vector3 origin = seat.transform.position;
                Vector3 side = seat.transform.right;
                Vector3 front = seat.transform.forward;
                camera.fieldOfView = 50;
                camera.transform.SetPositionAndRotation(
                    origin + side * 2.7f - front * .8f + Vector3.up * 1.35f,
                    Quaternion.LookRotation(
                        origin
                            + Vector3.up * 1.0f
                            - (origin + side * 2.7f - front * .8f + Vector3.up * 1.35f)
                    )
                );
                Capture(camera, "BarberChair-seated", 1920, 1080);
                Capture(camera, "BarberChair-seated-mobile", 1280, 720);
                camera.transform.position = origin + side * 2.7f + Vector3.up * 1.25f;
                camera.transform.LookAt(origin + Vector3.up * 1.0f);
                Capture(camera, "BarberChair-side", 1920, 1080);
                Bounds bounds = seat
                    .transform.parent.Find("TripoVisual")
                    .GetComponentInChildren<Renderer>()
                    .bounds;
                offer.ShownButtonRect.GetComponent<Button>().onClick.Invoke();
                if (seat.IsPlayerSeated || player.GetComponent<PlayerSeatPose>().IsSeated)
                    throw new InvalidOperationException("일어서기 실패");
                File.WriteAllText(
                    Output + "/verification.txt",
                    $"PASS 앉기/일어서기, 모델 높이={bounds.size.y:F3}m, 좌판={seat.CushionTopMeters:F2}m\n"
                );
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
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
