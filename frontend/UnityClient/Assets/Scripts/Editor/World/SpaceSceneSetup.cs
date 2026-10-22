using System;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.EditorTools.Learning;
using A305.EditorTools.Npc;
using A305.EditorTools.ObjectHover;
using A305.EditorTools.Player;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 공간 씬에 규약 그룹과 플레이어·카메라·도착 처리·사물 호버 툴팁·대화 처리·행인 제안 화면·피드백·단어장을 붙이고 서로 연결한다.
    /// 씬에 놓인 행인도 플레이어·대화 처리와 연결한다.
    /// 디자인한 씬이든 대체 공간 씬이든 같은 방식으로 적용하며, 여러 번 실행해도 기능 객체를 다시 만들지 않는다.
    /// Environment에 놓인 디자인과 이미 놓인 플레이어 위치는 건드리지 않는다.
    /// </summary>
    public static class SpaceSceneSetup
    {
        public const string MenuPath = "Tools/A305/World/Set Up Open Scene";

        /// <summary>열려 있는 씬에 기능 연결을 적용하고 저장한 뒤 Scene List에 추가한다.</summary>
        [MenuItem(MenuPath)]
        public static void SetUpOpenScene()
        {
            BuilderTools.ThrowIfPlaying();
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                throw new InvalidOperationException(
                    "씬을 Assets/Scenes/Production에 먼저 저장한 뒤 실행합니다."
                );
            }

            Apply(scene);
            BuilderTools.SaveScene(scene, scene.path);
            BuilderTools.AddToSceneList(scene.path);
            Debug.Log($"{scene.path}에 공간 기능을 연결했습니다.");
        }

        /// <summary>
        /// <see cref="Apply"/>가 씬에 붙이는 기능의 프리팹·데이터 에셋을 준비한다.
        /// Apply는 이 에셋들을 불러오기만 하므로, 씬을 준비하는 Builder는 먼저 이 메서드를 실행한다.
        /// </summary>
        public static void PrepareFeatureAssets()
        {
            ObjectHoverBuilder.PrepareAssets();
            DialogueBuilder.PrepareAssets();
            NpcBuilder.PrepareAssets();
            LearningBuilder.PrepareAssets();
        }

        /// <summary>
        /// 씬에 기능 객체를 붙이고 연결한다. 저장은 하지 않는다.
        /// 플레이어가 없으면 출입구 이름 순으로 첫 출입구 앞에, 출입구도 없으면 원점에 놓는다.
        /// </summary>
        public static void Apply(Scene scene)
        {
            var playerPrefab = BuilderTools.LoadRequiredAsset<GameObject>(
                PlayerSandboxBuilder.PlayerPrefabPath,
                PlayerSandboxBuilder.MenuPath
            );
            var cameraPrefab = BuilderTools.LoadRequiredAsset<GameObject>(
                PlayerSandboxBuilder.CameraPrefabPath,
                PlayerSandboxBuilder.MenuPath
            );
            SceneGroups groups = BuilderTools.EnsureGroups(scene);
            WarnUngroupedRoots(scene);

            var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            if (player == null)
            {
                Pose start = FindPlayerStart(scene);
                player = BuilderTools
                    .InstantiatePrefab(playerPrefab, groups.Characters, start.position, start.rotation)
                    .GetComponent<PlayerMover>();
            }

            var cameraRig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            if (cameraRig == null)
            {
                // Editor에서 씬을 열었을 때도 플레이어 뒤에서 보이도록 놓는다. Play하면 카메라가 다시 맞춘다.
                Transform playerTransform = player.transform;
                cameraRig = BuilderTools
                    .InstantiatePrefab(
                        cameraPrefab,
                        groups.Systems,
                        playerTransform.position + playerTransform.rotation * new Vector3(0f, 2.5f, -5f),
                        playerTransform.rotation * Quaternion.Euler(20f, 0f, 0f)
                    )
                    .GetComponent<PlayerCameraRig>();
            }

            PlayerSandboxBuilder.ConnectPlayerAndCamera(player.gameObject, cameraRig.gameObject);

            var arrival = BuilderTools.FindSingleInScene<SpaceArrival>(scene);
            if (arrival == null)
            {
                Transform arrivalObject = BuilderTools.CreateEmpty(
                    "SpaceArrival",
                    groups.Systems,
                    Vector3.zero,
                    Quaternion.identity
                );
                arrival = arrivalObject.gameObject.AddComponent<SpaceArrival>();
            }

            BuilderTools.SetReference(arrival, "player", player);
            BuilderTools.SetReference(arrival, "cameraRig", cameraRig);
            BuilderTools.RequireReferences(arrival);

            ObjectHoverBuilder.ConnectToScene(groups, player, cameraRig);
            DialogueController dialogue = DialogueBuilder.ConnectToScene(groups, player, cameraRig);
            NpcBuilder.ConnectToScene(groups, player, dialogue);
            LearningBuilder.ConnectToScene(groups, player, dialogue);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        // 루트에 놓인 디자인 객체가 어느 그룹에 속하는지는 알 수 없으므로 옮기지 않고 알린다.
        private static void WarnUngroupedRoots(Scene scene)
        {
            string[] ungroupedNames = scene
                .GetRootGameObjects()
                .Select(root => root.name)
                .Where(rootName => !BuilderTools.GroupNames.Contains(rootName))
                .ToArray();
            if (ungroupedNames.Length > 0)
            {
                Debug.LogWarning(
                    $"{scene.name} 씬 루트에 그룹 밖 객체가 있습니다: {string.Join(", ", ungroupedNames)}. "
                        + "지형·건물·소품은 Environment, NPC는 Characters처럼 규약 그룹 아래로 옮깁니다."
                );
            }
        }

        private static Pose FindPlayerStart(Scene scene)
        {
            SpaceDoorway firstDoorway = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .OrderBy(doorway => doorway.DoorwayId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (firstDoorway == null || firstDoorway.ArrivalPoint == null)
            {
                return new Pose(Vector3.zero, Quaternion.identity);
            }

            return new Pose(firstDoorway.ArrivalPoint.position, firstDoorway.ArrivalPoint.rotation);
        }
    }
}
