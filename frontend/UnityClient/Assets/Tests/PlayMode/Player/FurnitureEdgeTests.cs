using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator Player_BesideFurnitureEdge_SlidesDownToTheFloor(
            [Values(.2f, .35f, .5f)] float metersOutsideEdge
        )
        {
            yield return LoadHome();
            Box(FurnitureCenter, new Vector3(1f, FurnitureHeightMeters, 1f));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();

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
