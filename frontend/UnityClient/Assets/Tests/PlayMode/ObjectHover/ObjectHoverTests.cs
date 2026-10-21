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
        public IEnumerator PointingAtTv_NearPlayer_ShowsWordAndMeaning()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerAt(new Vector3(-1.5f, 0f, 2.5f));

            hover.Refresh(ScreenPointOf(tv), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(tooltip.WordText, Is.EqualTo("TV"));
            Assert.That(tooltip.MeaningText, Is.EqualTo("텔레비전"));
        }

        [UnityTest]
        public IEnumerator PointingAwayFromTv_HidesTooltip()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerAt(new Vector3(-1.5f, 0f, 2.5f));
            hover.Refresh(ScreenPointOf(tv), LongerThanShowDelaySeconds);

            // 화면 왼쪽 아래 구석은 바닥이라 가리키는 사물이 없다.
            hover.Refresh(new Vector2(5f, 5f), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PointingAtTv_AfterPlayerMovesFarAway_HidesTooltip()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerAt(new Vector3(-1.5f, 0f, 2.5f));
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
                Is.SupersetOf(new[] { "tv", "refrigerator", "air_conditioner", "sofa", "bookshelf", "bed" })
            );
        }

        private IEnumerator StandPlayerAt(Vector3 position)
        {
            player.Teleport(position, Quaternion.identity);
            cameraRig.SnapBehindTarget();
            yield return null;
        }

        private static Vector2 ScreenPointOf(HoverableObject target)
        {
            Bounds bounds = target.GetComponentInChildren<Renderer>().bounds;
            return Camera.main.WorldToScreenPoint(bounds.center);
        }

        private static HoverableObject FindHoverable(string unityKey)
        {
            return Object
                .FindObjectsByType<HoverableObject>(FindObjectsSortMode.None)
                .Single(hoverable => hoverable.UnityKey == unityKey);
        }
    }
}
