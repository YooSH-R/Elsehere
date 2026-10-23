using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>Sandbox를 Play로 열어 실제 카메라 화면과 크기·통행을 확인하는 Editor 검수 도구.</summary>
    [InitializeOnLoad]
    public static class TownMainstreetVerification
    {
        private const string SessionKey = "A305.MainstreetVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static int stage;
        private static int frames;
        private static double started;

        static TownMainstreetVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        public static string OutputFolder =>
            Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/mainstreet-blockout")
            );

        [MenuItem("Tools/A305/World/Verify Mainstreet Sandbox")]
        public static void Verify()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }
            EditorSceneManager.OpenScene(TownMainstreetBuilder.ScenePath);
            SessionState.SetInt(SessionKey, 1);
            EditorApplication.EnterPlaymode();
        }

        public static void BuildAndVerify()
        {
            TownMainstreetBuilder.Build();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            // Let Editor startup callbacks finish before Play causes another domain reload.
            EditorApplication.delayCall += () => EditorApplication.delayCall += Verify;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (SessionState.GetInt(SessionKey, 0) == 0)
            {
                return;
            }
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0;
                frames = 0;
                started = EditorApplication.timeSinceStartup;
                Results.Clear();
                Errors.Clear();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                int outcome = SessionState.GetInt(SessionKey, 0);
                SessionState.EraseInt(SessionKey);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(outcome == 2 ? 0 : 1);
                }
            }
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (
                SessionState.GetInt(SessionKey, 0) == 1
                && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            )
            {
                Errors.Add(message);
            }
        }

        private static void Update()
        {
            if (SessionState.GetInt(SessionKey, 0) != 1 || !EditorApplication.isPlaying)
            {
                return;
            }
            if (++frames < 15)
            {
                return;
            }
            frames = 0;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90d)
                {
                    throw new InvalidOperationException("Sandbox 검수 시간이 초과됐습니다.");
                }
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
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            Camera camera = rig.GetComponent<Camera>();
            var settings = AssetDatabase.LoadAssetAtPath<A305.World.TownBlockoutSettings>(
                TownMainstreetBuilder.SettingsPath
            );
            switch (stage++)
            {
                case 0:
                    CheckGeometry(scene, player);
                    Capture(camera, "01-start-view.png");
                    player.SetMovementLocked(true);
                    player.Teleport(new Vector3(8.5f, 0.1f, -10f), Quaternion.Euler(0f, 90f, 0f));
                    rig.SnapBehindTarget();
                    Transform target = scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>())
                        .Single(item => item.name == "DialogueLookTarget");
                    rig.EnterDialogueView(target);
                    break;
                case 1:
                    Require(
                        rig.ViewMode == CameraViewMode.Dialogue && player.IsMovementLocked,
                        "대화 시점과 이동 잠금"
                    );
                    Capture(camera, "02-dialogue-view.png");
                    rig.ExitDialogueView();
                    CheckDoorway(scene, player, "Cafe", Vector3.left);
                    CheckDoorway(scene, player, "Hotel", Vector3.right);
                    CheckDoorway(scene, player, "Home", Vector3.right);
                    player.Teleport(
                        FindDoor(scene, "Cafe").position + Vector3.right * 2.5f,
                        Quaternion.Euler(0f, 270f, 0f)
                    );
                    rig.SnapBehindTarget();
                    break;
                case 2:
                    Capture(camera, "03-cafe-scale.png");
                    CheckNorthWalk(player);
                    player.Teleport(new Vector3(5f, 0.1f, 30f), Quaternion.identity);
                    rig.SnapBehindTarget();
                    break;
                case 3:
                    Capture(camera, "04-north-extension.png");
                    rig.enabled = false;
                    camera.transform.position = new Vector3(32f, 40f, -34f);
                    camera.transform.LookAt(new Vector3(0f, 0f, 5f));
                    break;
                case 4:
                    Capture(camera, "05-layout-overview.png");
                    float cafeCentreZ = FindDoor(scene, "Cafe").parent.position.z;
                    float viewCentreY = 0.1f + settings.ShopHeightMeters / 2f;
                    player.Teleport(
                        new Vector3(-5.5f, 0.1f, cafeCentreZ),
                        Quaternion.Euler(0f, 90f, 0f)
                    );
                    camera.orthographic = true;
                    camera.orthographicSize = (settings.ShopHeightMeters + 1.2f) / 2f;
                    camera.transform.position = new Vector3(-0.2f, viewCentreY, cafeCentreZ);
                    camera.transform.LookAt(new Vector3(-7f, viewCentreY, cafeCentreZ));
                    break;
                case 5:
                    Capture(camera, "06-character-building-scale.png");
                    PrepareHeightComparison(scene, player, camera);
                    break;
                case 6:
                    Capture(camera, "07-character-height-comparison.png");
                    Finish();
                    break;
            }
        }

        private static void PrepareHeightComparison(Scene scene, PlayerMover player, Camera camera)
        {
            player.gameObject.SetActive(false);
            Transform parent = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Characters")
                .transform;
            GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{TownBlockoutGeometry.PrefabFolder}/LittleMungV7Visual.prefab"
            );
            float centreZ = FindDoor(scene, "Cafe").parent.position.z;
            float[] heights = { 1.4f, 1.6f, 1.7f };
            for (int index = 0; index < heights.Length; index++)
            {
                float height = heights[index];
                float z = centreZ + (index - 1) * 2.4f;
                GameObject reference = Object.Instantiate(visual, parent);
                reference.name = $"HeightReference_{height:F1}";
                reference.transform.SetPositionAndRotation(
                    new Vector3(-5.5f, 0.1f, z),
                    Quaternion.Euler(0f, 90f, 0f)
                );
                reference.transform.localScale = Vector3.one * (height / 1.4f);
                TownBlockoutGeometry.Label(
                    "HeightComparisonLabel",
                    $"{height:F1}m",
                    parent,
                    new Vector3(-5.5f, height + 0.45f, z),
                    new Vector3(0f, 270f, 0f),
                    0.3f
                );
            }
            camera.orthographicSize = 2.2f;
            camera.transform.position = new Vector3(0f, 1.3f, centreZ);
            camera.transform.LookAt(new Vector3(-7f, 1.3f, centreZ));
        }

        private static void CheckGeometry(Scene scene, PlayerMover player)
        {
            Physics.SyncTransforms();
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (
                Renderer renderer in player
                    .transform.Find("Visual")
                    .GetComponentsInChildren<Renderer>()
            )
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            var settings = AssetDatabase.LoadAssetAtPath<A305.World.TownBlockoutSettings>(
                TownMainstreetBuilder.SettingsPath
            );
            Require(
                Mathf.Abs(bounds.size.y - settings.CharacterHeightMeters) < 0.02f,
                $"캐릭터 시각 높이 {bounds.size.y:F3}m"
            );
            Require(BuilderTools.FindAllInScene<Camera>(scene).Length == 1, "플레이 카메라 하나");
            Require(
                !EditorBuildSettings.scenes.Any(item =>
                    item.path == TownMainstreetBuilder.ScenePath
                ),
                "Sandbox가 Production Scene List에 없음"
            );
            Require(
                scene
                    .GetRootGameObjects()
                    .Select(root => root.name)
                    .OrderBy(name => name)
                    .SequenceEqual(BuilderTools.GroupNames.OrderBy(name => name)),
                "규약의 다섯 루트 그룹"
            );
            foreach (Transform root in scene.GetRootGameObjects().Select(item => item.transform))
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    Require(
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)
                            == 0,
                        "Missing Script 없음",
                        record: false
                    );
                }
            }
            Results.Add("PASS Missing Script 없음");
        }

        private static void CheckDoorway(
            Scene scene,
            PlayerMover player,
            string buildingName,
            Vector3 inward
        )
        {
            Transform door = FindDoor(scene, buildingName);
            Quaternion rotation = Quaternion.LookRotation(inward);
            Vector3 start = door.position - inward * 1.8f;
            player.Teleport(start, rotation);
            CharacterController controller = player.GetComponent<CharacterController>();
            float characterScale = player.transform.lossyScale.x;
            for (int step = 0; step <= 16; step++)
            {
                Vector3 sample = start + inward * (step * 0.2f);
                Collider[] overlaps = Physics.OverlapBox(
                    sample + Vector3.up * (0.98f * characterScale),
                    new Vector3(0.7f, 0.41f, 0.5f) * characterScale,
                    rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );
                Require(
                    overlaps.All(collider => collider.transform.IsChildOf(player.transform)),
                    $"{buildingName} 문 통과 시 최대 헤어 폭 {1.4f * characterScale:F2}m의 간섭 없음",
                    record: false
                );
                controller.Move(inward * 0.2f);
            }
            Require(
                Vector3.Dot(player.transform.position - door.position, inward) > 1f,
                $"{buildingName} 문: CharacterController 실제 통과와 최대 헤어 폭 {1.4f * characterScale:F2}m 여유 확인"
            );
        }

        private static Transform FindDoor(Scene scene, string buildingName)
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>())
                .Single(item => item.name == buildingName)
                .Find("DoorCenter");
        }

        private static void CheckNorthWalk(PlayerMover player)
        {
            var settings = AssetDatabase.LoadAssetAtPath<A305.World.TownBlockoutSettings>(
                TownMainstreetBuilder.SettingsPath
            );
            if (settings.NorthExtensionMeters <= 2f)
            {
                Results.Add("SKIP 북쪽 연장 구간 없음");
                return;
            }
            player.Teleport(new Vector3(5f, 0.1f, 29f), Quaternion.identity);
            CharacterController controller = player.GetComponent<CharacterController>();
            float distance = settings.NorthExtensionMeters - 1f;
            for (float moved = 0f; moved < distance; moved += 0.25f)
            {
                controller.Move(Vector3.forward * Mathf.Min(0.25f, distance - moved));
            }
            Require(
                Mathf.Abs(player.transform.position.z - (28f + settings.NorthExtensionMeters))
                    < 0.1f,
                $"북쪽 보도 연속 통행 {distance:F1}m / 도착 Z={player.transform.position.z:F2}"
            );
        }

        private static void Capture(Camera camera, string name)
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
                Results.Add($"CAPTURE {name}");
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

        private static void Require(bool condition, string description, bool record = true)
        {
            if (!condition)
            {
                throw new InvalidOperationException(description);
            }
            if (record)
            {
                Results.Add($"PASS {description}");
            }
        }

        private static void Finish()
        {
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllLines(
                Path.Combine(OutputFolder, "verification.txt"),
                Results.Concat(Errors.Select(error => $"FAIL {error}"))
            );
            SessionState.SetInt(SessionKey, Errors.Count == 0 ? 2 : 3);
            Debug.Log($"Mainstreet verification: {Errors.Count} errors. {OutputFolder}");
            EditorApplication.ExitPlaymode();
        }
    }
}
