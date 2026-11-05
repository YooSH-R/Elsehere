using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Player.PlayModeTests
{
    /// <summary>
    /// 플레이어가 가구 옆·사이에서 공중에 떠 있지 않고, 큰 머리는 벽에 파묻히지 않으면서 낮은 가구 위로는 지나가는지 확인한다.
    /// 집 바닥(높이 0) 빈 곳에 상자를 두고 플레이어를 내려놓거나 걸어가게 한다.
    /// </summary>
    public class FurnitureEdgeTests
    {
        private const float FurnitureHeightMeters = .6f;
        private static readonly Vector3 FurnitureCenter = new Vector3(
            0f,
            FurnitureHeightMeters / 2f,
            -1f
        );

        private readonly List<GameObject> boxes = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject box in boxes)
            {
                Object.Destroy(box);
            }

            boxes.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hotel_PlayerWalksBetweenSofasAndCoffeeTables()
        {
            yield return SceneManager.LoadSceneAsync("HotelScene", LoadSceneMode.Single);
            yield return null;
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            Transform[] sofas = Object
                .FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(item => item.name == "SageSofa")
                .ToArray();
            Assert.That(sofas, Has.Length.EqualTo(2));

            float previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (Transform sofa in sofas)
                {
                    Transform table = sofa.parent.Find("CoffeeTable");
                    Bounds sofaBounds = SolidBounds(sofa);
                    Bounds tableBounds = SolidBounds(table);
                    Vector3 forwardAxis = new Vector3(
                        Mathf.Abs(sofa.forward.x),
                        0f,
                        Mathf.Abs(sofa.forward.z)
                    );
                    Vector3 sofaFront =
                        sofaBounds.center
                        + sofa.forward * Vector3.Dot(sofaBounds.extents, forwardAxis);
                    Vector3 tableBack =
                        tableBounds.center
                        - sofa.forward * Vector3.Dot(tableBounds.extents, forwardAxis);
                    float gap = Vector3.Dot(tableBack - sofaFront, sofa.forward);
                    Assert.That(gap, Is.GreaterThanOrEqualTo(1f), "소파 앞에 1m 통로를 둡니다.");

                    Vector3 aisleCenter = (sofaFront + tableBack) * .5f;
                    aisleCenter.y = .05f;
                    Vector3 start = aisleCenter - sofa.right * 1.8f;
                    yield return WalkForward(player, start, sofa.right, 1.5f);
                    // 키를 뗀 프레임을 처리해야 반대편 소파에서는 새 카메라 방향으로 걷는다.
                    yield return null;

                    TestContext.WriteLine(
                        $"Hotel aisle: gap={gap:F3}m, start={start}, end={player.transform.position}"
                    );

                    Assert.That(
                        Vector3.Dot(player.transform.position - aisleCenter, sofa.right),
                        Is.GreaterThan(.9f),
                        "책상과 소파 사이를 걸어서 반대편까지 지나가야 합니다."
                    );
                    Assert.That(player.transform.position.y, Is.LessThan(.15f));
                }
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        [UnityTest]
        public IEnumerator Player_BesideFurnitureEdge_SlidesDownToTheFloor(
            [Values(.2f, .35f, .5f)] float metersOutsideEdge
        )
        {
            yield return LoadHome();
            Box(FurnitureCenter, new Vector3(1f, FurnitureHeightMeters, 1f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();

            // 다른 가구 테스트처럼 게임과 같은 1/60초 프레임으로 기다린다. 화면 없이 도는 CI에서는 프레임이
            // 제한 없이 높아져 한 프레임 이동이 너무 작아지고, 캐릭터가 모서리에 걸린 채 남는 일이 생긴다.
            float previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                // 상자 오른쪽 모서리(x 0.5) 바깥 위에서 떨어뜨리면 캡슐 아래 둥근 면이 모서리에 먼저 닿는다.
                player.Teleport(
                    new Vector3(
                        .5f + metersOutsideEdge,
                        FurnitureHeightMeters + .3f,
                        FurnitureCenter.z
                    ),
                    Quaternion.identity
                );
                yield return WaitSeconds(1.5f);
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }

            Assert.That(
                player.transform.position.y,
                Is.LessThan(.05f),
                "가구 옆에서 바닥까지 내려와야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator Player_OverFurniture_StandsOnItsTop()
        {
            yield return LoadHome();
            Box(FurnitureCenter, new Vector3(1f, FurnitureHeightMeters, 1f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();

            player.Teleport(
                new Vector3(FurnitureCenter.x, FurnitureHeightMeters + .3f, FurnitureCenter.z),
                Quaternion.identity
            );
            yield return WaitSeconds(1.5f);

            Assert.That(
                player.transform.position.y,
                Is.EqualTo(FurnitureHeightMeters).Within(.05f),
                "가구 위에 섰으면 윗면에 서 있어야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator Player_WalkingOffFurniture_LandsWithFeetOnTheFloor()
        {
            yield return LoadHome();
            Box(FurnitureCenter, new Vector3(1f, FurnitureHeightMeters, 1f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            float previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                // 상자 위 가운데에서 앞(+x)으로 걸어 모서리 밖으로 떨어진 뒤 멈춘다.
                yield return WalkForward(
                    player,
                    new Vector3(FurnitureCenter.x, FurnitureHeightMeters, FurnitureCenter.z),
                    Vector3.right,
                    1f
                );
                yield return WaitSeconds(1.5f);

                // 계단 보정이 상자 윗면을 디딤점으로 붙들면 몸은 바닥에 있어도 외형이 상자 높이에 떠 있다.
                Assert.That(
                    LowestSkinHeight(player) - player.transform.position.y,
                    Is.LessThan(.06f),
                    "바닥에 내려서면 외형 발도 바닥에 닿아야 합니다."
                );
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        [UnityTest]
        public IEnumerator Player_JumpingInPlaceBeforeStep_LandsWithFeetOnTheFloor()
        {
            yield return LoadHome();
            // 보도 경계석처럼 높이 0.1m 턱. 플레이어는 턱 0.4m 앞에서 턱을 보고 선다.
            Box(new Vector3(0f, .05f, -1.5f), new Vector3(2f, .1f, 1f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            float previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            player.Teleport(new Vector3(0f, 0f, -.6f), Quaternion.LookRotation(Vector3.back));
            player.CameraRig.SnapBehindTarget();
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return WaitSeconds(.5f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitSeconds(2f);

                // 떨어지는 동안 계단 보정이 공중의 발 위치를 디딤점으로 잡으면 착지 뒤에도 외형이 떠 있다.
                Assert.That(
                    LowestSkinHeight(player) - player.transform.position.y,
                    Is.LessThan(.06f),
                    "제자리 점프 뒤 외형 발이 바닥에 닿아야 합니다."
                );
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
                InputSystem.settings.backgroundBehavior = oldBackground;
                Time.captureDeltaTime = previousDelta;
            }
        }

        [UnityTest]
        public IEnumerator Player_FallingIntoGapNarrowerThanHead_StandsOnTheFloor()
        {
            yield return LoadHome();
            // 소파 앞과 커피 테이블 사이처럼 0.68m 틈. 머리(폭 약 1m)보다 좁고 몸통보다 넓다.
            Box(new Vector3(0f, .3f, -2.34f), new Vector3(1.6f, .6f, 1f));
            Box(new Vector3(0f, .3f, -.66f), new Vector3(1.6f, .6f, 1f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();

            player.Teleport(new Vector3(0f, 1.2f, -1.5f), Quaternion.identity);
            yield return WaitSeconds(1.5f);

            Assert.That(
                player.transform.position.y,
                Is.LessThan(.05f),
                "몸통이 들어가는 틈이면 바닥에 서야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator Player_WalkingToLowTable_GetsCloseWithHeadOverIt()
        {
            yield return LoadHome();
            // 앞면이 z -1.5인 높이 0.6m 탁자. 머리는 0.6m보다 위에 있어 탁자 위로 지나간다.
            Box(new Vector3(0f, .3f, -2f), new Vector3(1.6f, .6f, 1f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();

            yield return WalkForward(player, new Vector3(0f, 0f, 0f), Vector3.back, 2f);

            Assert.That(
                player.transform.position.z - -1.5f,
                Is.LessThan(.45f),
                "몸통은 낮은 탁자 앞까지 다가가야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator Player_WalkingIntoWall_KeepsHeadOutOfIt()
        {
            yield return LoadHome();
            // 앞면이 z -2.3인 높은 벽
            Box(new Vector3(0f, 1.25f, -2.5f), new Vector3(3f, 2.5f, .4f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();

            yield return WalkForward(player, new Vector3(0f, 0f, -.5f), Vector3.back, 2f);

            float deepest = FarthestSkinAlong(player, Vector3.back);
            Assert.That(deepest - 2.3f, Is.LessThan(.05f), "머리가 벽 안으로 들어가면 안 됩니다.");
        }

        private static Bounds SolidBounds(Transform furniture)
        {
            Collider[] colliders = furniture
                .GetComponentsInChildren<Collider>()
                .Where(collider => collider.enabled && !collider.isTrigger)
                .ToArray();
            Assert.That(colliders, Is.Not.Empty, furniture.name);
            Bounds bounds = colliders[0].bounds;
            foreach (Collider collider in colliders)
                bounds.Encapsulate(collider.bounds);
            return bounds;
        }

        private static IEnumerator LoadHome()
        {
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
        }

        private void Box(Vector3 center, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "TestFurniture";
            box.transform.position = center;
            box.transform.localScale = size;
            boxes.Add(box);
            Physics.SyncTransforms();
        }

        /// <summary>direction을 보고 서서 카메라를 등 뒤에 두고, 앞으로 가는 키(W)를 seconds초 누른다.</summary>
        private static IEnumerator WalkForward(
            PlayerMover player,
            Vector3 start,
            Vector3 direction,
            float seconds
        )
        {
            player.Teleport(start, Quaternion.LookRotation(direction));
            player.CameraRig.SnapBehindTarget();
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return WaitSeconds(seconds);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
        }

        /// <summary>캐릭터 메시에서 direction 쪽으로 가장 멀리 나온 점의 위치(direction 축 위의 값)</summary>
        private static float FarthestSkinAlong(PlayerMover player, Vector3 direction)
        {
            float farthest = float.NegativeInfinity;
            var mesh = new Mesh();
            try
            {
                foreach (
                    SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>()
                )
                {
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        farthest = Mathf.Max(
                            farthest,
                            Vector3.Dot(skin.transform.TransformPoint(vertex), direction)
                        );
                    }
                }
            }
            finally
            {
                Object.Destroy(mesh);
            }

            return farthest;
        }

        private static float LowestSkinHeight(PlayerMover player)
        {
            float lowest = float.PositiveInfinity;
            var mesh = new Mesh();
            try
            {
                foreach (
                    SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>()
                )
                {
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y);
                    }
                }
            }
            finally
            {
                Object.Destroy(mesh);
            }

            return lowest;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }
    }
}
