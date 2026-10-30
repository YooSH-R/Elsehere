using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Player.PlayModeTests
{
    /// <summary>
    /// 플레이어 캡슐 아래 둥근 면이 가구 모서리에 걸려 가구 옆 공중에 떠 있지 않는지 확인한다.
    /// 집 바닥(높이 0) 빈 곳에 높이 0.6m 상자를 두고 그 위·옆에서 플레이어를 내려놓는다.
    /// </summary>
    public class FurnitureEdgeTests
    {
        private const float FurnitureHeightMeters = .6f;
        private static readonly Vector3 FurnitureCenter = new Vector3(
            0f,
            FurnitureHeightMeters / 2f,
            -1f
        );

        private GameObject furniture;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (furniture != null)
            {
                Object.Destroy(furniture);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_BesideFurnitureEdge_SlidesDownToTheFloor(
            [Values(.2f, .35f, .5f)] float metersOutsideEdge
        )
        {
            PlayerMover player = null;
            yield return PlaceFurniture(found => player = found);

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
            PlayerMover player = null;
            yield return PlaceFurniture(found => player = found);

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

        private IEnumerator PlaceFurniture(System.Action<PlayerMover> found)
        {
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            furniture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            furniture.name = "TestFurniture";
            furniture.transform.position = FurnitureCenter;
            furniture.transform.localScale = new Vector3(1f, FurnitureHeightMeters, 1f);
            Physics.SyncTransforms();
            found(Object.FindFirstObjectByType<PlayerMover>());
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
