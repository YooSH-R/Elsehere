using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using A305.Tutorial;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static A305.EditorTools.World.HomeInteriorKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>저장된 카페의 Play 화면, 동선, 호버, 직원 대화와 마을 출입을 검수한다.</summary>
    [InitializeOnLoad]
    public static class CafeInteriorVerification
    {
        private const string SessionKey = "A305.CafeInteriorVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static int stage;
        private static int frames;
        private static double started;
        private static string OutputFolder =>
            Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/cafe-interior")
            );

        static CafeInteriorVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Tools/A305/Cafe/Verify Corner Cafe Design")]
        public static void Verify()
        {
            if (!BuilderTools.CanBuild())
                return;
            EditorSceneManager.OpenScene(CafeInteriorBuilder.ScenePath);
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
                || ++frames < 40
            )
                return;
            frames = 0;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180)
                    throw new InvalidOperationException(
                        "Cafe verification timeout at stage " + stage
                    );
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
            if (stage == 8)
            {
                if (scene.name != "TownScene")
                    return;
                CheckArrival(scene, "CafeDoor");
                EnterDoor(scene, player, "CafeDoor");
                stage++;
                return;
            }
            if (stage == 9)
            {
                if (scene.name != "CafeScene")
                    return;
                CheckArrival(scene, "TownDoor");
                Capture(camera, "05-cafe-return.png");
                Finish();
                return;
            }
            switch (stage++)
            {
                case 0:
                    Audit(scene, player);
                    Capture(camera, "01-player-start.png");
                    // 카페 직원을 만날 수 있는 튜토리얼 완료 상태에서 실내 대화를 검수한다.
                    TutorialState.GetOrCreate("TOWN_PASSERBY").Skip();
                    Results.Add("Dialogue inspection uses the existing tutorial skip path.");
                    break;
                case 1:
                    CheckWalk(
                        player,
                        V(0, .05f, -4),
                        V(0, .05f, 2.65f),
                        "entrance to order counter"
                    );
                    CheckWalk(player, V(0, .05f, -1.4f), V(-3.2f, .05f, -1.4f), "booth access");
                    CheckWalk(player, V(-3.2f, .05f, -3.5f), V(-3.2f, .05f, 2.2f), "booth aisle");
                    CheckWalk(player, V(0, .05f, -1.4f), V(3.2f, .05f, -1.4f), "table access");
                    CheckWalk(player, V(3.2f, .05f, -3.5f), V(3.2f, .05f, 2.2f), "table aisle");
                    player.Teleport(V(0, .05f, 2.65f), Quaternion.identity);
                    rig.SnapBehindTarget();
                    break;
                case 2:
                    PasserbyOfferView offer = BuilderTools.FindSingleInScene<PasserbyOfferView>(
                        scene
                    );
                    Require(
                        offer.IsShowing,
                        "staff offers conversation from customer side of counter; staff="
                            + BuilderTools.FindSingleInScene<NpcIdentity>(scene).transform.position
                            + " player="
                            + player.transform.position
                            + " phase="
                            + BuilderTools.FindSingleInScene<Passerby>(scene).Phase
                    );
                    offer
                        .GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "AcceptButton")
                        .onClick.Invoke();
                    Require(
                        BuilderTools.FindSingleInScene<DialogueController>(scene).IsTalking,
                        "accept button starts cafe staff dialogue"
                    );
                    Require(
                        player.IsMovementLocked && rig.ViewMode == CameraViewMode.Dialogue,
                        "dialogue locks movement and looks at staff"
                    );
                    rig.SnapBehindTarget();
                    break;
                case 3:
                    Capture(camera, "02-staff-dialogue.png");
                    BuilderTools
                        .FindSingleInScene<DialoguePanelView>(scene)
                        .GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "EndButton")
                        .onClick.Invoke();
                    Require(
                        !player.IsMovementLocked && rig.ViewMode == CameraViewMode.ThirdPerson,
                        "end button restores exploration"
                    );
                    rig.enabled = false;
                    foreach (Passerby passerby in BuilderTools.FindAllInScene<Passerby>(scene))
                        passerby.IsApproachAllowed = false;
                    player.Teleport(V(0, .05f, -.5f), Quaternion.identity);
                    View(camera, V(-1.2f, 2.8f, -3.3f), V(.2f, 1.25f, 4.8f), 65);
                    Capture(camera, "03-counter-and-seating.png");
                    break;
                case 4:
                    Transform shell = HomeInteriorBuilder
                        .Root(scene, "Environment")
                        .Find("CornerCafe/CafeShell");
                    foreach (string part in new[] { "Ceiling", "SouthWall", "EastWall" })
                        shell.Find(part).gameObject.SetActive(false);
                    View(camera, V(13, 14, -16), V(0, .6f, .2f), 48);
                    Capture(camera, "04-cutaway-layout.png");
                    foreach (string part in new[] { "Ceiling", "SouthWall", "EastWall" })
                        shell.Find(part).gameObject.SetActive(true);
                    Results.Add(
                        "Cutaway hides ceiling and two walls during inspection only. Saved scene is enclosed."
                    );
                    break;
                case 5:
                    CheckHover(scene, player, camera);
                    break;
                case 6:
                    rig.enabled = true;
                    player.SetMovementLocked(false);
                    EnterDoor(scene, player, "TownDoor");
                    break;
                case 7:
                    // 씬 교체가 끝날 때까지 다음 단계에서 기다린다.
                    break;
            }
        }

        private static void Audit(Scene scene, PlayerMover player)
        {
            Require(
                scene.path == CafeInteriorBuilder.ScenePath,
                "Production CafeScene loaded in Play"
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
                BuilderTools
                    .FindAllInScene<HoverableObject>(scene)
                    .Select(item => item.UnityKey)
                    .OrderBy(key => key)
                    .SequenceEqual(new[] { "counter", "menu" }),
                "original two word keys retained"
            );
            Require(
                BuilderTools.FindSingleInScene<ObjectHoverController>(scene).enabled,
                "hover references valid"
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
                .transform.Find("CafeVisual")
                .GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            Require(Mathf.Abs(bounds.size.y - 1.6f) < .03f, "Little Mung v7 proxy height 1.6m");
            Require(
                BuilderTools
                    .FindAllInScene<Light>(scene)
                    .Count(light => light.enabled && light.gameObject.activeInHierarchy) == 6,
                "sun, fill and four pendant lights"
            );
            NpcIdentity staff = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == "CAFE_STAFF");
            Require(staff.GetComponent<Passerby>().enabled, "existing cafe staff enabled");
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
            player.Teleport(V(-1, .05f, 2.45f), Quaternion.identity);
            foreach (HoverableObject item in BuilderTools.FindAllInScene<HoverableObject>(scene))
            {
                Vector3 target = item.UnityKey == "counter" ? V(-1, .5f, 3.1f) : V(-1, 2.9f, 5.65f);
                View(camera, V(-1, 1.4f, 2.45f), target, 65);
                Physics.SyncTransforms();
                hover.Refresh(camera.WorldToScreenPoint(target), .3f);
                Require(
                    tooltip.IsVisible && !string.IsNullOrWhiteSpace(tooltip.WordText),
                    "customer-side raycast displays " + item.UnityKey
                );
                tooltip.Hide();
            }
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

        private static void EnterDoor(Scene scene, PlayerMover player, string id)
        {
            SpaceDoorway door = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(item => item.DoorwayId == id);
            player.Teleport(
                door.transform.position + door.transform.forward * .4f,
                Quaternion.LookRotation(-door.transform.forward)
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

        private static void View(Camera camera, Vector3 position, Vector3 target, float fov)
        {
            camera.fieldOfView = fov;
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(target - position)
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
            Debug.Log("Cafe verification: " + Errors.Count + " errors. " + OutputFolder);
            EditorApplication.ExitPlaymode();
        }
    }
}
