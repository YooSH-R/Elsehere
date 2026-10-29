using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.ObjectHover;
using A305.Player;
using A305.Tutorial;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.HomeInteriorKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>집의 실제 Play 렌더, 가구 호버, 동선과 마을 왕복을 검수한다.</summary>
    [InitializeOnLoad]
    public static class HomeInteriorVerification
    {
        private const string SessionKey = "A305.HomeInteriorVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static int stage;
        private static int frames;
        private static double started;
        private static string OutputFolder =>
            Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/home-interior")
            );

        static HomeInteriorVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Tools/A305/Home/Verify Cozy Cottage Design")]
        public static void Verify()
        {
            if (!BuilderTools.CanBuild())
                return;
            EditorSceneManager.OpenScene(HomeInteriorBuilder.ScenePath);
            SessionState.SetInt(SessionKey, 1);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (SessionState.GetInt(SessionKey, 0) == 0)
                return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0;
                frames = 0;
                started = EditorApplication.timeSinceStartup;
                Results.Clear();
                Errors.Clear();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
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
            if (
                SessionState.GetInt(SessionKey, 0) != 1
                || !EditorApplication.isPlaying
                || ++frames < 20
            )
                return;
            frames = 0;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180)
                    throw new InvalidOperationException("Home verification timeout");
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
            Scene scene = SceneManager.GetActiveScene();
            if (SpaceTravel.IsTraveling)
                return;
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            Camera camera = rig.GetComponent<Camera>();
            if (stage == 5)
            {
                if (scene.name != "TownScene")
                    return;
                CheckArrival(scene, "HomeDoor");
                SpaceDoorway townDoor = BuilderTools
                    .FindAllInScene<SpaceDoorway>(scene)
                    .Single(door => door.DoorwayId == "HomeDoor");
                player.Teleport(
                    townDoor.transform.position + townDoor.transform.forward * .4f,
                    Quaternion.LookRotation(-townDoor.transform.forward)
                );
                stage++;
                return;
            }
            if (stage == 6)
            {
                if (scene.name != "HomeScene")
                    return;
                CheckArrival(scene, "TownDoor");
                Capture(camera, "05-home-return.png");
                Finish();
                return;
            }
            switch (stage++)
            {
                case 0:
                    Audit(scene, player);
                    Capture(camera, "01-player-start.png");
                    break;
                case 1:
                    CheckWalk(
                        player,
                        V(0, .05f, -3.8f),
                        V(0, .05f, 3.3f),
                        "central learning route"
                    );
                    CheckWalk(player, V(0, .05f, -1.8f), V(1.45f, .05f, -1.8f), "bed approach");
                    CheckWalk(player, V(0, .05f, -.4f), V(-2.7f, .05f, -.4f), "sofa approach");
                    CheckWalk(player, V(0, .05f, .1f), V(3.6f, .05f, .1f), "kitchen access");
                    CheckWalk(player, V(3.6f, .05f, .1f), V(3.6f, .05f, 3.2f), "kitchen aisle");
                    rig.enabled = false;
                    camera.transform.SetPositionAndRotation(
                        V(0, 2.2f, -3.65f),
                        Quaternion.LookRotation(V(-1.2f, 1.2f, 2) - V(0, 2.2f, -3.65f))
                    );
                    player.Teleport(V(.2f, .05f, -.9f), Quaternion.identity);
                    Capture(camera, "02-living-and-kitchen.png");
                    break;
                case 2:
                    camera.transform.SetPositionAndRotation(
                        V(-1.4f, 2.2f, -4.05f),
                        Quaternion.LookRotation(V(3.2f, .8f, -1.6f) - V(-1.4f, 2.2f, -4.05f))
                    );
                    Capture(camera, "03-bedroom.png");
                    Transform shell = HomeInteriorBuilder
                        .Root(scene, "Environment")
                        .Find("CozyInterior/RoomShell");
                    foreach (string part in new[] { "Ceiling", "SouthWall", "EastWall" })
                        shell.Find(part).gameObject.SetActive(false);
                    camera.transform.SetPositionAndRotation(
                        V(10.5f, 12.5f, -13.5f),
                        Quaternion.LookRotation(V(0, .4f, 0) - V(10.5f, 12.5f, -13.5f))
                    );
                    camera.fieldOfView = 46;
                    Capture(camera, "04-cutaway-layout.png");
                    foreach (string part in new[] { "Ceiling", "SouthWall", "EastWall" })
                        shell.Find(part).gameObject.SetActive(true);
                    camera.fieldOfView = 65;
                    Results.Add(
                        "Cutaway capture hides ceiling and two walls only during inspection; saved scene remains enclosed."
                    );
                    break;
                case 3:
                    CheckHover(scene, player, camera);
                    break;
                case 4:
                    SpaceDoorway door = BuilderTools.FindSingleInScene<SpaceDoorway>(scene);
                    player.SetMovementLocked(false);
                    player.Teleport(
                        door.transform.position + door.transform.forward * .4f,
                        Quaternion.LookRotation(-door.transform.forward)
                    );
                    break;
            }
        }

        private static void Audit(Scene scene, PlayerMover player)
        {
            Require(
                scene.path == HomeInteriorBuilder.ScenePath,
                "Production HomeScene loaded in Play"
            );
            Require(
                scene
                    .GetRootGameObjects()
                    .Select(root => root.name)
                    .OrderBy(name => name)
                    .SequenceEqual(
                        new[] { "Characters", "Environment", "Lighting", "Systems", "UI" }
                    ),
                "five scene groups"
            );
            Require(
                BuilderTools.FindSingleInScene<TutorialDirector>(scene).enabled,
                "tutorial director references valid"
            );
            Require(
                BuilderTools.FindSingleInScene<ObjectHoverController>(scene).enabled,
                "hover controller references valid"
            );
            HoverableObject[] items = BuilderTools.FindAllInScene<HoverableObject>(scene);
            Require(
                items.Length == 6 && items.All(item => item.gameObject.activeInHierarchy),
                "six original furniture hover objects retained"
            );
            Require(
                BuilderTools
                    .FindAllInScene<Transform>(scene)
                    .Sum(node =>
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)
                    ) == 0,
                "no missing scripts"
            );
            Renderer[] renderers = player
                .transform.Find("HomeVisual")
                .GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            Require(Mathf.Abs(bounds.size.y - 1.6f) < .03f, "Little Mung v7 proxy height 1.6m");
            Require(
                BuilderTools
                    .FindAllInScene<Light>(scene)
                    .Count(light => light.enabled && light.gameObject.activeInHierarchy) == 4,
                "sun, room fill and two working bedside lamps"
            );
            Results.Add(
                "ACTIVE RENDERERS "
                    + BuilderTools
                        .FindAllInScene<Renderer>(scene)
                        .Count(renderer =>
                            renderer.enabled && renderer.gameObject.activeInHierarchy
                        )
            );
        }

        private static void CheckHover(Scene scene, PlayerMover player, Camera camera)
        {
            ObjectHoverController hover = BuilderTools.FindSingleInScene<ObjectHoverController>(
                scene
            );
            WordTooltipView tooltip = BuilderTools.FindSingleInScene<WordTooltipView>(scene);
            hover.enabled = false;
            player.SetMovementLocked(false);
            foreach (HoverableObject item in BuilderTools.FindAllInScene<HoverableObject>(scene))
            {
                Collider collider = item.GetComponentsInChildren<Collider>()
                    .First(candidate => candidate.enabled);
                Vector3 center = collider.bounds.center;
                Vector3 front = item.transform.forward;
                Vector3 cameraPosition = center + front * 2.3f + Vector3.up * .4f;
                player.Teleport(
                    new Vector3(cameraPosition.x, .05f, cameraPosition.z),
                    Quaternion.LookRotation(-front)
                );
                camera.transform.SetPositionAndRotation(
                    cameraPosition,
                    Quaternion.LookRotation(center - cameraPosition)
                );
                Physics.SyncTransforms();
                Vector3 pointer = camera.WorldToScreenPoint(center);
                hover.Refresh(pointer, .3f);
                Require(
                    tooltip.IsVisible && !string.IsNullOrWhiteSpace(tooltip.WordText),
                    "raycast hover displays word for " + item.UnityKey
                );
                Require(
                    tooltip.IsExampleVisible == (item.UnityKey == "tv"),
                    "saved-word example policy for " + item.UnityKey
                );
            }
            tooltip.Hide();
        }

        private static void CheckWalk(PlayerMover player, Vector3 from, Vector3 to, string name)
        {
            player.Teleport(from, Quaternion.LookRotation(to - from));
            Physics.SyncTransforms();
            CharacterController controller = player.GetComponent<CharacterController>();
            int steps = Mathf.CeilToInt(Vector3.Distance(from, to) / .08f);
            for (int i = 0; i < steps; i++)
                controller.Move((to - from) / steps + Vector3.down * .01f);
            Vector3 remaining = to - player.transform.position;
            remaining.y = 0;
            Require(
                remaining.magnitude < .25f,
                name + " clear (remaining " + remaining.magnitude.ToString("F2") + "m)"
            );
        }

        private static void CheckArrival(Scene scene, string id)
        {
            Transform arrival = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(door => door.DoorwayId == id)
                .ArrivalPoint;
            Vector3 delta =
                BuilderTools.FindSingleInScene<PlayerMover>(scene).transform.position
                - arrival.position;
            delta.y = 0;
            Require(
                delta.magnitude < .1f,
                scene.name + "/" + id + " arrived safely after doorway contact"
            );
        }

        private static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory(OutputFolder);
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(OutputFolder, name), pixels.EncodeToPNG());
                Results.Add("CAPTURE " + name);
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

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }

        private static void Finish()
        {
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllLines(
                Path.Combine(OutputFolder, "verification.txt"),
                Results.Concat(Errors.Select(error => "FAIL " + error))
            );
            SessionState.SetInt(SessionKey, Errors.Count == 0 ? 2 : 3);
            Debug.Log("Home verification: " + Errors.Count + " errors. " + OutputFolder);
            EditorApplication.ExitPlaymode();
        }
    }
}
