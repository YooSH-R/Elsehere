using System.Collections;
using System.Linq;
using A305.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
        public IEnumerator ClickingTv_PinsCard_AndKeepsItWhenPointerLeaves()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);

            hover.Click(ScreenPointOf(tv));
            LookAtEmptySky();
            hover.Refresh(ScreenCenter(), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(tooltip.IsPinned, Is.True);
            Assert.That(tooltip.WordText, Is.EqualTo("TV"));
        }

        [UnityTest]
        public IEnumerator ClickingEmptySpace_ClosesPinnedCard()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            hover.Click(ScreenPointOf(tv));

            LookAtEmptySky();
            hover.Click(ScreenCenter());

            Assert.That(tooltip.IsVisible, Is.False);
            Assert.That(tooltip.IsPinned, Is.False);
        }

        [UnityTest]
        public IEnumerator PressingCloseButton_ClosesPinnedCard()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            hover.Click(ScreenPointOf(tv));

            Button close = tooltip
                .GetComponentsInChildren<Button>(true)
                .SingleOrDefault(button => button.name == "CloseButton");
            Assert.That(close, Is.Not.Null, "카드에 [닫기]가 없습니다.");
            Assert.That(
                close.gameObject.activeInHierarchy,
                Is.True,
                "고정한 카드에 [닫기]가 보이지 않습니다."
            );
            close.onClick.Invoke();
            LookAtEmptySky();
            hover.Refresh(ScreenCenter(), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PinnedCard_ClosesWhenPlayerMovesFarAway()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            Vector2 pointer = ScreenPointOf(tv);
            hover.Click(pointer);

            // 남쪽 문 앞 감지 영역에 들어가면 마을로 이동하므로 문에서 떨어진 남동쪽 구석에 세운다.
            player.Teleport(new Vector3(4f, 0f, -3.8f), Quaternion.identity);
            hover.Refresh(pointer, LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.False);
            Assert.That(tooltip.IsPinned, Is.False);
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

        [UnityTest]
        public IEnumerator Tooltip_FollowsPointerAboveItsPosition()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            Camera camera = Camera.main;
            Vector3 center = VisibleBounds(tv).center;
            camera.transform.SetPositionAndRotation(
                center + tv.transform.forward * 2f,
                Quaternion.LookRotation(-tv.transform.forward)
            );
            var panel = tooltip.transform.Find("Panel").GetComponent<RectTransform>();
            var canvas = tooltip.GetComponent<Canvas>();
            Vector2 first = new Vector2(Screen.width / 2f - 8f, Screen.height / 2f);
            Vector2 second = first + new Vector2(16f, 8f);
            foreach (Vector2 pointer in new[] { first, second })
            {
                hover.Refresh(pointer, LongerThanShowDelaySeconds);
                Assert.That(tooltip.IsVisible, Is.True);
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                Vector2 bottomCenter = RectTransformUtility.WorldToScreenPoint(
                    null,
                    (corners[0] + corners[3]) * .5f
                );
                Assert.That(bottomCenter.x, Is.EqualTo(pointer.x).Within(1f));
                Assert.That(
                    bottomCenter.y,
                    Is.EqualTo(pointer.y + 16f * canvas.scaleFactor).Within(1f)
                );
            }
        }

        [UnityTest]
        public IEnumerator Tooltip_UsesSpacingFromDataAsset()
        {
            yield return null;
            var settings = ScriptableObject.CreateInstance<ObjectHoverSettings>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"pointerGap\":48}", settings);
                Vector2 pointer = new Vector2(Screen.width / 2f, Screen.height / 3f);
                tooltip.Show(ObjectWord.ForHover("pillow", "베개", "", false), pointer, settings);
                var panel = tooltip.transform.Find("Panel").GetComponent<RectTransform>();
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                Vector2 bottomCenter = RectTransformUtility.WorldToScreenPoint(
                    null,
                    (corners[0] + corners[3]) * .5f
                );
                Assert.That(
                    bottomCenter.y,
                    Is.EqualTo(pointer.y + 48f * tooltip.GetComponent<Canvas>().scaleFactor)
                        .Within(1f)
                );
            }
            finally
            {
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator ApproachingBed_SwitchesFromBedToPillow_AndBack()
        {
            yield return null;
            HoverableObject bed = FindHoverable("bed");
            Vector3 point = bed.transform.TransformPoint(new Vector3(-.44f, .78f, -.9f));
            Camera camera = Camera.main;
            camera.transform.position = point + bed.transform.forward * 1.5f + Vector3.up * 2f;
            camera.transform.LookAt(point);
            Vector2 pointer = camera.WorldToScreenPoint(point);
            foreach (float distance in new[] { 4f, 2f, 4f })
            {
                Vector3 standing = point + bed.transform.forward * distance;
                standing.y = .05f;
                player.Teleport(standing, Quaternion.identity);
                Physics.SyncTransforms();
                hover.Refresh(pointer, LongerThanShowDelaySeconds);
                Assert.That(tooltip.IsVisible, Is.True);
                Assert.That(tooltip.WordText, Is.EqualTo(distance < 3f ? "pillow" : "bed"));
            }
        }

        private IEnumerator StandPlayerNear(HoverableObject target)
        {
            Vector3 position = VisibleBounds(target).center + target.transform.forward * 2f;
            position.y = .05f;
            player.Teleport(position, Quaternion.LookRotation(-target.transform.forward));
            cameraRig.SnapBehindTarget();
            yield return null;
        }

        // 벽도 공용 단어를 가지므로, 씬 위쪽의 빈 공간을 바라보게 해 아무 사물도 가리키지 않게 한다.
        private static void LookAtEmptySky()
        {
            Camera.main.transform.SetPositionAndRotation(
                new Vector3(0f, 100f, 0f),
                Quaternion.LookRotation(Vector3.up, Vector3.forward)
            );
        }

        private static Vector2 ScreenCenter()
        {
            return new Vector2(Screen.width / 2f, Screen.height / 2f);
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
