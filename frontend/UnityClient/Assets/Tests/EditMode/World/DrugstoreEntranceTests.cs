using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 드럭스토어 앞벽은 문 자리에 폭 2m·높이 4m 구멍이 있다. 문 둘레가 막혀 있지 않으면 매장 안에서 문 위·옆으로 하늘이 보이고
    /// 카메라도 그 틈으로 빠져나간다.
    /// </summary>
    public sealed class DrugstoreEntranceTests
    {
        // 앞벽 안쪽 면은 z=-6.9, 문 구멍은 x=-1~1이다.
        private const float WallInnerZ = -6.9f;

        [Test]
        public void NothingSeesThroughTheEntranceAroundTheDoor()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Production/ShopScene.unity");
            Physics.SyncTransforms();

            var gapPoints = new[]
            {
                new Vector2(-.9f, .5f),
                new Vector2(-.9f, 1.8f),
                new Vector2(.9f, .5f),
                new Vector2(.9f, 1.8f),
                new Vector2(-.9f, 3.3f),
                new Vector2(0, 2.8f),
                new Vector2(0, 3.7f),
                new Vector2(.9f, 3.3f),
            };
            foreach (Vector2 point in gapPoints)
            {
                var start = new Vector3(point.x, point.y, WallInnerZ + 1.5f);
                bool blocked = Physics.Raycast(
                    start,
                    Vector3.back,
                    out RaycastHit hit,
                    2f,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );

                Assert.That(
                    blocked,
                    Is.True,
                    $"정문 ({point.x}, {point.y}) 자리로 매장 밖이 보입니다."
                );
                Assert.That(
                    hit.collider.TryGetComponent(out Renderer _),
                    Is.True,
                    $"정문 ({point.x}, {point.y}) 자리를 막는 것이 보이지 않는 충돌뿐입니다: {hit.collider.name}"
                );
            }
        }
    }
}
