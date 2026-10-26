using System.Collections;
using System.Linq;
using A305.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.ObjectHover.PlayModeTests
{
    /// <summary>
    /// 내 방(HomeScene)을 실제로 불러와 가구를 가리켰을 때 툴팁이 보이고 숨는지 확인한다.
    /// 실제 마우스 대신 화면 좌표를 직접 넣는다.
    /// </summary>
    public class ObjectHoverTests
    {
        // 툴팁 표시 지연보다 충분히 긴 시간
        private const float LongerThanShowDelaySeconds = 1f;

        private ObjectHoverController hover;
        private WordTooltipView tooltip;
        private PlayerMover player;
        private PlayerCameraRig cameraRig;

        [UnitySetUp]
        public IEnumerator LoadHome()
        {
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;

            hover = Object.FindFirstObjectByType<ObjectHoverController>();
            tooltip = Object.FindFirstObjectByType<WordTooltipView>();
            player = Object.FindFirstObjectByType<PlayerMover>();
            cameraRig = Object.FindFirstObjectByType<PlayerCameraRig>();
            Assert.That(hover, Is.Not.Null, "HomeScene에 ObjectHoverController가 없습니다.");
            Assert.That(tooltip, Is.Not.Null, "HomeScene에 WordTooltipView가 없습니다.");
            hover.enabled = false;
        }

        [UnityTest]
        public IEnumerator PointingAtSavedWord_NearPlayer_ShowsWordMeaningAndExample()
        {
            // 확인용 단어 목록에서 TV는 단어장에 저장된 단어다.
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);

            hover.Refresh(ScreenPointOf(tv), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(tooltip.WordText, Is.EqualTo("TV"));
            Assert.That(tooltip.MeaningText, Is.EqualTo("텔레비전"));
            Assert.That(tooltip.IsExampleVisible, Is.True);
            Assert.That(tooltip.ExampleText, Is.EqualTo("Let's watch TV together."));
        }

        [UnityTest]
        public IEnumerator PointingAtUnsavedWord_ShowsMeaningWithoutExample()
        {
            HoverableObject airConditioner = FindHoverable("air_conditioner");
            yield return StandPlayerNear(airConditioner);

            hover.Refresh(ScreenPointOf(airConditioner), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(tooltip.MeaningText, Is.EqualTo("에어컨"));
            Assert.That(tooltip.IsExampleVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PointingAwayFromTv_HidesTooltip()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            hover.Refresh(ScreenPointOf(tv), LongerThanShowDelaySeconds);
            Assert.That(tooltip.IsVisible, Is.True);

            // 벽도 공용 단어를 가지므로, 씬 위쪽의 빈 공간을 바라보며 무대상 상태를 확인한다.
            Camera.main.transform.SetPositionAndRotation(
                new Vector3(0f, 100f, 0f),
                Quaternion.LookRotation(Vector3.up, Vector3.forward)
            );
            hover.Refresh(
                new Vector2(Screen.width / 2f, Screen.height / 2f),
                LongerThanShowDelaySeconds
            );

            Assert.That(tooltip.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PointingAtTv_AfterPlayerMovesFarAway_HidesTooltip()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            Vector2 pointer = ScreenPointOf(tv);
            hover.Refresh(pointer, LongerThanShowDelaySeconds);
            Assert.That(tooltip.IsVisible, Is.True);

            // 카메라가 따라가기 전 같은 프레임에 플레이어만 멀리 옮겨, 같은 포인터가 여전히 TV를 가리키게 한다.
            // 남쪽 문 앞 감지 영역에 들어가면 마을로 이동하므로 문에서 떨어진 남동쪽 구석에 세운다.
            player.Teleport(new Vector3(4f, 0f, -3.8f), Quaternion.identity);
            hover.Refresh(pointer, LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator HomeFurniture_HaveWordKeys()
        {
            yield return null;
            string[] keys = Object
                .FindObjectsByType<HoverableObject>(FindObjectsSortMode.None)
                .Select(hoverable => hoverable.UnityKey)
                .ToArray();

            Assert.That(
                keys,
                Is.SupersetOf(
                    new[] { "tv", "refrigerator", "air_conditioner", "sofa", "bookshelf", "bed" }
                )
            );
        }

        [UnityTest]
        public IEnumerator PointingAtAnotherInstanceOfSameWord_RestartsShowDelay()
        {
            HoverableObject original = FindHoverable("tv");
            yield return StandPlayerNear(original);
            Vector2 pointer = ScreenPointOf(original);
            hover.Refresh(pointer, LongerThanShowDelaySeconds);
            Assert.That(tooltip.IsVisible, Is.True);

            // 같은 위치의 다른 인스턴스로 바꿔 단어 키가 같아도 새 사물을 가리키는 경우를 만든다.
            GameObject replacement = Object.Instantiate(
                original.gameObject,
                original.transform.position,
                original.transform.rotation
            );
            replacement.transform.localScale = original.transform.lossyScale;
            original.gameObject.SetActive(false);
            Physics.SyncTransforms();
            hover.Refresh(pointer, 0.01f);
            Assert.That(tooltip.IsVisible, Is.False);
            hover.Refresh(pointer, LongerThanShowDelaySeconds);
            Assert.That(tooltip.IsVisible, Is.True);
            Object.Destroy(replacement);
        }

        private IEnumerator StandPlayerNear(HoverableObject target)
        {
            Vector3 position = VisibleBounds(target).center + target.transform.forward * 2f;
            position.y = .05f;
            player.Teleport(position, Quaternion.LookRotation(-target.transform.forward));
            cameraRig.SnapBehindTarget();
            yield return null;
        }

        private static Vector2 ScreenPointOf(HoverableObject target)
        {
            return Camera.main.WorldToScreenPoint(VisibleBounds(target).center);
        }

        private static Bounds VisibleBounds(HoverableObject target)
        {
            // 교체 전 도형의 비활성 Renderer 대신 실제 호버를 받는 새 외형을 사용한다.
            return target
                .GetComponentsInChildren<Collider>()
                .First(collider => collider.enabled)
                .bounds;
        }

        private static HoverableObject FindHoverable(string unityKey)
        {
            return Object
                .FindObjectsByType<HoverableObject>(FindObjectsSortMode.None)
                .Single(hoverable => hoverable.UnityKey == unityKey);
        }
    }
}
