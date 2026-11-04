using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
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
    /// <summary>Production 씬의 실제 Play 카메라와 이동·닫힌 배경 건물을 검수한다.</summary>
    [InitializeOnLoad]
    public static class TownProductionVerification
    {
        private const string SessionKey = "A305.TownProductionVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static int stage;
        private static int frames;
        private static double started;
        private static double tutorialStarted;
        private static readonly string[] TravelDoors = { "HomeDoor", "CafeDoor", "HotelDoor" };
        private static readonly string[] TravelScenes = { "HomeScene", "CafeScene", "HotelScene" };
        internal static string OutputFolder =>
            Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/production-town")
            );

        static TownProductionVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Tools/A305/World/Verify Production Town Design")]
        public static void Verify()
        {
            if (!BuilderTools.CanBuild())
                return;
            EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            SessionState.SetInt(SessionKey, 1);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (SessionState.GetInt(SessionKey, 0) == 0)
                return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                stage = -6;
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
                    throw new InvalidOperationException("Town verification timeout");
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
            if (stage < 0)
            {
                CheckTutorialApproach(scene);
                return;
            }
            if (stage >= 9)
            {
                CheckDoorTravel(scene);
                return;
            }
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            Camera camera = rig.GetComponent<Camera>();
            switch (stage++)
            {
                case 0:
                    Audit(scene, player);
                    Capture(camera, "01-player-start.png");
                    player.SetMovementLocked(true);
                    foreach (Passerby npc in BuilderTools.FindAllInScene<Passerby>(scene))
                        npc.IsApproachAllowed = false;
                    CheckWalk(
                        player,
                        new Vector3(5, .15f, -18),
                        new Vector3(5, .15f, 64),
                        "east sidewalk / north extension 82m"
                    );
                    CheckWalk(
                        player,
                        new Vector3(5, .15f, -11),
                        new Vector3(-5, .15f, -11),
                        "cafe crossing 10m"
                    );
                    CheckWalk(
                        player,
                        new Vector3(5, .15f, 17.5f),
                        new Vector3(11, .15f, 17.5f),
                        "pocket park entry 6m"
                    );
                    player.Teleport(new Vector3(-4.9f, .12f, -16), Quaternion.identity);
                    rig.SnapBehindTarget();
                    break;
                case 1:
                    Capture(camera, "02-cafe-walk.png");
                    player.Teleport(new Vector3(5, .12f, -9), Quaternion.identity);
                    rig.SnapBehindTarget();
                    break;
                case 2:
                    Capture(camera, "03-hotel-walk.png");
                    rig.enabled = false;
                    camera.transform.SetPositionAndRotation(
                        new Vector3(25, 30, -27),
                        Quaternion.identity
                    );
                    camera.transform.LookAt(new Vector3(0, 1, 9));
                    break;
                case 3:
                    Capture(camera, "04-layout-overview.png");
                    camera.transform.position = new Vector3(2, 7, -18);
                    camera.transform.LookAt(new Vector3(-9, 3, -5));
                    break;
                case 4:
                    Capture(camera, "05-cafe-and-closed-building.png");
                    camera.transform.position = new Vector3(-3, 8, -16);
                    camera.transform.LookAt(new Vector3(10, 2.5f, -9));
                    break;
                case 5:
                    Capture(camera, "06-hotel-home-courtyard.png");
                    camera.transform.position = new Vector3(-2, 8, 10);
                    camera.transform.LookAt(new Vector3(10, 1.5f, 23));
                    break;
                case 6:
                    Capture(camera, "07-residential-park.png");
                    rig.enabled = true;
                    player.Teleport(new Vector3(7.2f, .12f, -10), Quaternion.Euler(0, 90, 0));
                    NpcIdentity dialoguePartner = BuilderTools
                        .FindAllInScene<NpcIdentity>(scene)
                        .Single(item => item.Code == "TOWN_PASSERBY");
                    rig.EnterDialogueView(dialoguePartner.LookTarget);
                    break;
                case 7:
                    Require(
                        PlayerVisual(player)
                            .GetComponentsInChildren<Renderer>()
                            .All(renderer => renderer.forceRenderingOff),
                        "player visual hidden during dialogue"
                    );
                    Capture(camera, "08-dialogue-view.png");
                    rig.ExitDialogueView();
                    break;
                case 8:
                    Require(
                        PlayerVisual(player)
                            .GetComponentsInChildren<Renderer>()
                            .All(renderer => !renderer.forceRenderingOff),
                        "player visual restored after dialogue"
                    );
                    break;
            }
        }

        private static void CheckTutorialApproach(Scene scene)
        {
            switch (stage)
            {
                case -6:
                    PasserbyRestRecord.Clear();
                    PasserbyOfferRecord.Clear();
                    TutorialState.Clear();
                    SceneManager.LoadSceneAsync("HomeScene");
                    stage++;
                    return;
                case -5:
                    if (scene.name != "HomeScene")
                        return;
                    Require(
                        SpaceTravel.TryTravel("TownScene", "HomeDoor"),
                        "leave home for tutorial"
                    );
                    stage++;
                    return;
                case -4:
                    if (scene.name != "TownScene" || SpaceTravel.IsTraveling)
                        return;
                    CheckArrival(scene, "HomeDoor");
                    tutorialStarted = EditorApplication.timeSinceStartup;
                    stage++;
                    return;
            }

            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            Passerby passerby = BuilderTools
                .FindAllInScene<Passerby>(scene)
                .Single(npc => npc.GetComponent<NpcIdentity>().Code == "TOWN_PASSERBY");
            PasserbyOfferView offer = BuilderTools.FindSingleInScene<PasserbyOfferView>(scene);
            DialogueController dialogue = BuilderTools.FindSingleInScene<DialogueController>(scene);
            switch (stage)
            {
                case -3:
                    if (!offer.IsShownBy(passerby))
                    {
                        if (EditorApplication.timeSinceStartup - tutorialStarted > 15)
                            throw new InvalidOperationException(
                                "Tutorial passerby did not approach after leaving home."
                            );
                        return;
                    }
                    Vector3 offset = passerby.transform.position - player.transform.position;
                    offset.y = 0;
                    Require(
                        passerby.Phase == PasserbyPhase.Offering && offset.magnitude < 3,
                        "tutorial passerby approaches home arrival and offers within 3m"
                    );
                    Require(
                        !offer.IsDeclineShown && !player.IsMovementLocked,
                        "tutorial offer hides decline and leaves movement unlocked"
                    );
                    Capture(
                        BuilderTools
                            .FindSingleInScene<PlayerCameraRig>(scene)
                            .GetComponent<Camera>(),
                        "00-tutorial-approach.png"
                    );
                    Results.Add(
                        $"TUTORIAL offer after {EditorApplication.timeSinceStartup - tutorialStarted:F2}s at {offset.magnitude:F2}m"
                    );
                    stage++;
                    return;
                case -2:
                    offer
                        .GetComponentsInChildren<Button>()
                        .Single(button => button.name == "AcceptButton")
                        .onClick.Invoke();
                    Require(
                        dialogue.IsTalking && passerby.Phase == PasserbyPhase.Talking,
                        "accepting tutorial offer starts dialogue"
                    );
                    stage++;
                    return;
                case -1:
                    Capture(
                        BuilderTools
                            .FindSingleInScene<PlayerCameraRig>(scene)
                            .GetComponent<Camera>(),
                        "00-tutorial-dialogue.png"
                    );
                    dialogue.End();
                    Require(
                        !dialogue.IsTalking && !player.IsMovementLocked,
                        "ending tutorial dialogue restores movement"
                    );
                    stage++;
                    return;
            }
        }

        private static void CheckDoorTravel(Scene scene)
        {
            int index = (stage - 9) / 3;
            if (index == TravelDoors.Length)
            {
                Finish();
                return;
            }
            switch ((stage - 9) % 3)
            {
                case 0:
                    SpaceDoorway door = BuilderTools
                        .FindAllInScene<SpaceDoorway>(scene)
                        .Single(item => item.DoorwayId == TravelDoors[index]);
                    PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
                    player.Teleport(
                        door.transform.position + door.transform.forward * .4f,
                        Quaternion.LookRotation(-door.transform.forward)
                    );
                    stage++;
                    break;
                case 1:
                    if (scene.name != TravelScenes[index] || SpaceTravel.IsTraveling)
                        return;
                    CheckArrival(scene, "TownDoor");
                    Require(
                        SpaceTravel.TryTravel("TownScene", TravelDoors[index]),
                        "return from " + TravelScenes[index]
                    );
                    stage++;
                    break;
                case 2:
                    if (scene.name != "TownScene" || SpaceTravel.IsTraveling)
                        return;
                    CheckArrival(scene, TravelDoors[index]);
                    stage++;
                    break;
            }
        }

        private static void CheckArrival(Scene scene, string id)
        {
            Transform arrival = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(door => door.DoorwayId == id)
                .ArrivalPoint;
            Vector3 position = BuilderTools
                .FindSingleInScene<PlayerMover>(scene)
                .transform.position;
            Vector3 offset = position - arrival.position;
            offset.y = 0;
            Require(
                offset.magnitude < .05f,
                scene.name + "/" + id + " arrived at safe return point"
            );
        }

        private static Transform PlayerVisual(PlayerMover player)
        {
            var appearance = player.GetComponent<PlayerCharacterAppearance>();
            return appearance != null ? appearance.VisualRoot : player.transform.Find("TownVisual");
        }

        // F·M은 같은 리틀 멍 체형을 쓴다. 선택 외형이 없으면 이전 TownVisual 높이를 기대한다.
        private static float ExpectedVisualHeight(PlayerMover player)
        {
            return player.GetComponent<PlayerCharacterAppearance>() != null ? 1.4f : 1.6f;
        }

        private static void Audit(Scene scene, PlayerMover player)
        {
            Require(
                scene.path == TownProductionBuilder.ScenePath,
                "Production TownScene loaded in Play"
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
                BuilderTools.FindAllInScene<SpaceDoorway>(scene).Length == 3,
                "three functional doorways"
            );
            Require(
                BuilderTools.FindAllInScene<NpcIdentity>(scene).Length == 2,
                "both NPC identities preserved"
            );
            int missing = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(node =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)
                );
            Require(missing == 0, "no missing scripts");
            Bounds visual = PlayerVisual(player).GetComponentsInChildren<Renderer>()[0].bounds;
            foreach (Renderer renderer in PlayerVisual(player).GetComponentsInChildren<Renderer>())
                visual.Encapsulate(renderer.bounds);
            Require(
                Mathf.Abs(visual.size.y - ExpectedVisualHeight(player)) < .04f,
                "Selected player visual height (actual " + visual.size.y.ToString("F3") + ")"
            );
            Transform background = TownProductionBuilder
                .Root(scene, "Environment")
                .Find("TownMainstreet/BackgroundBuildings");
            Require(
                background.GetComponentsInChildren<Light>(true).Length == 0,
                "background buildings have no lights"
            );
            Require(
                background.GetComponentsInChildren<SpaceDoorway>(true).Length == 0,
                "background buildings have no travel triggers"
            );
            Require(
                !background
                    .GetComponentsInChildren<Transform>()
                    .Any(node =>
                        node.name.Contains("WarmShop")
                        || node.name.Contains("MarketCrate")
                        || node.name.Contains("Counter")
                    ),
                "no shop displays in background buildings"
            );
            foreach (SpaceDoorway door in BuilderTools.FindAllInScene<SpaceDoorway>(scene))
                Results.Add(
                    $"DOOR {door.DoorwayId} at {door.transform.position} -> {door.DestinationScene}/{door.DestinationDoorwayId}, return {door.ArrivalPoint.position}"
                );
            Results.Add(
                "RENDERERS "
                    + BuilderTools
                        .FindAllInScene<Renderer>(scene)
                        .Count(renderer => renderer.gameObject.activeInHierarchy)
            );
        }

        private static void CheckWalk(PlayerMover player, Vector3 from, Vector3 to, string name)
        {
            player.Teleport(from, Quaternion.LookRotation(to - from));
            Physics.SyncTransforms();
            CharacterController controller = player.GetComponent<CharacterController>();
            int steps = Mathf.CeilToInt(Vector3.Distance(from, to) / .08f);
            Vector3 delta = (to - from) / steps;
            for (int index = 0; index < steps; index++)
                controller.Move(delta + Vector3.down * .01f);
            Vector3 remaining = to - player.transform.position;
            remaining.y = 0;
            Require(
                remaining.magnitude < .25f,
                name + " clear (remaining " + remaining.magnitude.ToString("F2") + "m)"
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
            Debug.Log("Production Town verification: " + Errors.Count + " errors. " + OutputFolder);
            EditorApplication.ExitPlaymode();
        }
    }
}
