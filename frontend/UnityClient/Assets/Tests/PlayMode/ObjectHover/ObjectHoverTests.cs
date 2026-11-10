using System.Collections;
using System.Collections.Generic;
using System.Linq;
using A305.Player;
using A305.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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

        // 플레이어를 옮긴 뒤 카메라가 따라와 화면이 실제 플레이처럼 자리 잡는 데 넉넉한 프레임 수
        private const int CameraSettleFrames = 10;

        private ObjectHoverController hover;
        private WordTooltipView tooltip;
        private PlayerMover player;
        private PlayerCameraRig cameraRig;

        [UnitySetUp]
        public IEnumerator LoadHome()
        {
            LocalObjectWordApi.Forget();
            // 튜토리얼 첫 단계에서는 코치 마크가 화면을 덮어 가구를 가리킬 수 없으므로, 튜토리얼을 마친 사용자로 시작한다.
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
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

        [TearDown]
        public void ClearTutorial()
        {
            TutorialState.Clear();
            LocalObjectWordApi.Forget();
        }

        [UnityTest]
        public IEnumerator PointingAtSavedWord_NearPlayer_ShowsWordMeaningAndExample()
        {
            // 저장 여부를 서버 응답이나 미리보기 기본값에 맡기지 않고 실제 저장 버튼으로 준비한다.
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            yield return SaveWordThroughTheCard(tv);

            hover.Refresh(ScreenPointOf(tv), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(tooltip.WordText, Is.EqualTo("TV"));
            Assert.That(tooltip.MeaningText, Is.EqualTo("방송을 보는 화면 기기(텔레비전)"));
            Assert.That(tooltip.IsExampleVisible, Is.True);
            Assert.That(
                tooltip.ExampleText,
                Is.EqualTo("Let's watch TV together.\n<color=#B8C0CC>같이 TV 보자.</color>")
            );
        }

        [UnityTest]
        public IEnumerator ClickingSavedWord_PinnedCardShowsTheExample()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            yield return SaveWordThroughTheCard(tv);

            hover.Click(ScreenPointOf(tv));

            Assert.That(tooltip.IsPinned, Is.True);
            Assert.That(tooltip.IsExampleVisible, Is.True);
            Assert.That(
                tooltip.ExampleText,
                Is.EqualTo("Let's watch TV together.\n<color=#B8C0CC>같이 TV 보자.</color>")
            );
        }

        private IEnumerator SaveWordThroughTheCard(HoverableObject target)
        {
            hover.Click(ScreenPointOf(target));
            Button save = tooltip
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SaveButton");
            Assert.That(save.gameObject.activeInHierarchy, Is.True, "준비: 저장 버튼이 보입니다.");
            Assert.That(save.interactable, Is.True, "준비: 저장 전 단어로 시작합니다.");
            save.onClick.Invoke();
            yield return null;
            Assert.That(tooltip.CanSaveWord, Is.False, "준비: 저장을 마쳤습니다.");
            tooltip
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CloseButton")
                .onClick.Invoke();
            yield return null;
            Assert.That(tooltip.IsPinned, Is.False);
        }

        [UnityTest]
        public IEnumerator PointingAtUnsavedWord_ShowsMeaningWithoutExample()
        {
            HoverableObject airConditioner = FindHoverable("air_conditioner");
            yield return StandPlayerNear(airConditioner);
            // 벽 위쪽의 에어컨은 화면 위쪽에 비쳐 튜토리얼 가이드 같은 UI에 가려질 수 있다. UI 아래는 가리키지 않으므로 화면 가운데에 둔다.
            AimSoThatScreenPointShows(airConditioner, ScreenCenter());

            hover.Refresh(ScreenCenter(), LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(tooltip.MeaningText, Is.EqualTo("실내를 시원하게 하는 냉방기(에어컨)"));
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
        public IEnumerator PinnedCard_KeepsItsPlace_WhenItsContentGrows()
        {
            // [숨기기]를 누르면 카드에 두 줄 안내가 붙어 길어진다. 길어져도 카드가 다른 자리로 옮겨 가지 않는다.
            // 사물을 눌러 고정하면 카메라 자리에 따라 카드가 화면 아래 여백에 붙기도 하고, 그때는 길어지면 화면을 벗어나
            // 자리를 다시 정하는 것이 맞다. 이 규칙만 보도록 길어질 자리가 넉넉한 화면 위쪽 가운데에 카드를 고정한다.
            yield return null;
            var settings = ScriptableObject.CreateInstance<ObjectHoverSettings>();
            try
            {
                Vector2 pointer = new Vector2(Screen.width / 2f, Screen.height * .7f);
                tooltip.ShowPinned(
                    ObjectWord.ForHover("TV", "텔레비전", "", false),
                    CardAnchor.AtPointer(pointer),
                    settings,
                    WordCardButtons.None
                );
                Assert.That(tooltip.IsPinned, Is.True);
                Rect before = CardScreenRect();

                tooltip.ShowStatus(
                    "이제 마우스를 올려도 카드가 뜨지 않아요. 사물을 클릭하면 다시 볼 수 있어요."
                );
                tooltip.UpdatePlacement();
                yield return null;
                Rect after = CardScreenRect();

                Assert.That(
                    after.height,
                    Is.GreaterThan(before.height),
                    "준비: 안내가 붙어 카드가 길어집니다."
                );
                Assert.That(after.xMin, Is.EqualTo(before.xMin).Within(1f), "카드 왼쪽");
                Assert.That(after.yMax, Is.EqualTo(before.yMax).Within(1f), "카드 위쪽");
            }
            finally
            {
                Object.Destroy(settings);
            }
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
        public IEnumerator Tooltip_FollowsPointerAboveThePointedObject()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            // 화면 위쪽의 튜토리얼 가이드가 카드를 다른 자리로 밀지 않게 숨긴다.
            TutorialGuidePanel().gameObject.SetActive(false);
            // TV가 화면 절반보다 작게 보이도록 2.5m 앞에서 보고, 캐릭터는 카메라 뒤에 세워 화면에서 뺀다.
            Camera camera = Camera.main;
            Vector3 center = VisibleBounds(tv).center;
            Vector3 behindCamera = center + tv.transform.forward * 3f;
            behindCamera.y = .05f;
            player.Teleport(behindCamera, Quaternion.LookRotation(-tv.transform.forward));
            camera.transform.SetPositionAndRotation(
                center + tv.transform.forward * 2.5f,
                Quaternion.LookRotation(-tv.transform.forward)
            );
            Rect tvArea = RendererScreenRect(tv);
            Assert.That(
                tvArea.width < Screen.width / 2f && tvArea.height < Screen.height / 2f,
                Is.True,
                $"TV가 화면 절반보다 작게 보여야 합니다: {tvArea}, 화면 {Screen.width}x{Screen.height}"
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
                // 가로로는 포인터를 따라가고, 세로로는 가리킨 TV를 덮지 않게 TV 위쪽 끝 위에 둔다.
                Assert.That(bottomCenter.x, Is.EqualTo(pointer.x).Within(1f));
                Assert.That(
                    bottomCenter.y,
                    Is.EqualTo(tvArea.yMax + 16f * canvas.scaleFactor).Within(1f)
                );
            }
        }

        [UnityTest]
        public IEnumerator PointingAtFloorBesideCharacter_CardDoesNotCoverCharacter()
        {
            // 캐릭터 발 옆 바닥을 가리키면 카드가 바로 위에 떠 캐릭터 머리를 덮었다.
            yield return StandPlayerNear(FindHoverable("tv"));
            // 순간이동한 바로 그 프레임은 카메라가 아직 따라오지 않아 실제 플레이와 다르게 보인다. 따라온 뒤에 잰다.
            for (int frame = 0; frame < CameraSettleFrames; frame++)
                yield return null;
            Transform body = player.transform;
            Vector3 floor = body.position + body.right * .55f + body.forward * .2f;
            Vector2 pointer = Camera.main.WorldToScreenPoint(floor);
            Assert.That(
                PointedHoverable(pointer),
                Is.Not.Null,
                "캐릭터 옆 바닥을 가리켜야 합니다."
            );

            hover.Refresh(pointer, LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(
                CardScreenRect().Overlaps(RendererScreenRect(player)),
                Is.False,
                "카드가 캐릭터를 덮습니다."
            );
        }

        [UnityTest]
        public IEnumerator PointingJustBelowTutorialGuide_CardStaysOutOfGuide()
        {
            // 첫걸음 가이드 바로 아래 사물을 가리키면 카드가 가이드 뒤로 들어가 옆으로 비어져 나왔다.
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            Rect guide = ShowTutorialGuide();
            Vector2 belowGuide = new Vector2(guide.center.x, guide.yMin - 20f);
            AimSoThatScreenPointShows(tv, belowGuide);
            Assert.That(
                PointedHoverable(belowGuide),
                Is.EqualTo(tv),
                "가이드 아래에 TV가 보여야 합니다."
            );

            hover.Refresh(belowGuide, LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.True);
            Assert.That(CardScreenRect().Overlaps(guide), Is.False, "카드가 가이드와 겹칩니다.");
        }

        [UnityTest]
        public IEnumerator PinnedCard_StaysPut_WhenTheTutorialCoachMarkDimsTheScreen()
        {
            // 코치 마크는 밝힐 곳 둘레를 덮개 네 조각으로 덮는다. 조각마다 화면보다 작아 피할 창으로 보면,
            // 밝힌 곳 바로 옆의 고정한 카드가 덮개를 피해 밝힌 구멍 쪽으로 끌려가 밝힌 곳을 덮었다.
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            Object.FindFirstObjectByType<TutorialDirector>().enabled = false;
            CoachMarkView coachMark = Object.FindFirstObjectByType<CoachMarkView>();
            coachMark.Hide();
            Rect guide = ShowTutorialGuide();
            Vector2 belowGuide = new Vector2(guide.center.x, guide.yMin - 20f);
            AimSoThatScreenPointShows(tv, belowGuide);
            hover.Click(belowGuide);
            Assert.That(tooltip.IsPinned, Is.True);
            Rect pinned = CardScreenRect();
            Assert.That(
                pinned.Overlaps(guide),
                Is.False,
                "준비: 카드가 안내 카드를 비켜 고정됩니다."
            );

            coachMark.ShowSpotlight(new[] { TutorialGuidePanel() }, "안내 카드를 읽어 보세요");
            yield return null;
            Assert.That(
                coachMark.IsSpotlightShowing,
                Is.True,
                "준비: 코치 마크가 안내 카드 둘레를 덮습니다."
            );
            hover.Refresh(belowGuide, LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsPinned, Is.True);
            Assert.That(
                Vector2.Distance(CardScreenRect().position, pinned.position),
                Is.LessThan(1f),
                "고정한 카드가 코치 마크 덮개를 피해 옮겨 갑니다."
            );
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
                tooltip.Show(
                    ObjectWord.ForHover("pillow", "베개", "", false),
                    CardAnchor.AtPointer(pointer),
                    settings
                );
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

        [UnityTest]
        public IEnumerator PointingAtObjectBehindHudMenu_DoesNotShowCard()
        {
            // 오른쪽 위 메뉴(≡) 뒤에 TV가 보여도, 메뉴 위의 포인터는 사물을 가리키지 않는다.
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            Vector2 menu = ScreenCenterOf(HudMenuButton());
            AimSoThatScreenPointShows(tv, menu);
            Assert.That(PointedHoverable(menu), Is.EqualTo(tv), "메뉴 뒤에 TV가 보여야 합니다.");

            hover.Refresh(menu, LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PinnedCardOverHudMenu_DoesNotTakeMenuClicks()
        {
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            RectTransform menuButton = HudMenuButton();
            Vector2 menu = ScreenCenterOf(menuButton);
            // 메뉴 바로 아래의 TV를 눌러 고정하면 카드는 화면 안으로 당겨져 메뉴와 겹친다.
            Vector2 belowMenu = menu - new Vector2(0f, 60f);
            AimSoThatScreenPointShows(tv, belowMenu);
            hover.Click(belowMenu);
            Assert.That(tooltip.IsPinned, Is.True);
            Canvas.ForceUpdateCanvases();

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = menu },
                results
            );

            Assert.That(results, Is.Not.Empty);
            Assert.That(
                results[0].gameObject.transform.IsChildOf(menuButton),
                Is.True,
                $"메뉴 대신 {results[0].gameObject.name}이(가) 클릭을 받습니다."
            );
        }

        [UnityTest]
        public IEnumerator PinnedCardAtRightEdge_MenuOpenedLater_StaysOutOfMenu()
        {
            // 화면 오른쪽 끝을 눌러 카드를 고정한 뒤 메뉴(≡)를 펼치면 카드가 메뉴 버튼 뒤로 들어갔다.
            HoverableObject tv = FindHoverable("tv");
            yield return StandPlayerNear(tv);
            Button menuButton = HudMenuButton().GetComponent<Button>();
            menuButton.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Rect openMenuButton = ScreenRectOf(OpenHudMenuButton());
            Rect menuList = ScreenRectOf(HudMenuList());
            OpenHudMenuButton().GetComponent<Button>().onClick.Invoke();
            Vector2 rightOfList = new Vector2(
                (menuList.xMax + Screen.width) / 2f,
                menuList.center.y
            );
            AimSoThatScreenPointShows(tv, rightOfList);
            hover.Click(rightOfList);
            Assert.That(tooltip.IsPinned, Is.True);

            menuButton.onClick.Invoke();
            hover.Refresh(rightOfList, LongerThanShowDelaySeconds);

            Assert.That(tooltip.IsPinned, Is.True);
            Rect card = CardScreenRect();
            Assert.That(card.Overlaps(openMenuButton), Is.False, "카드가 메뉴 버튼과 겹칩니다.");
            Assert.That(card.Overlaps(menuList), Is.False, "카드가 메뉴 목록과 겹칩니다.");
        }

        private Rect CardScreenRect()
        {
            Canvas.ForceUpdateCanvases();
            return ScreenRectOf(tooltip.transform.Find("Panel").GetComponent<RectTransform>());
        }

        private static RectTransform TutorialGuidePanel()
        {
            var guide = Object.FindFirstObjectByType<TutorialGuideView>(
                FindObjectsInactive.Include
            );
            Assert.That(guide, Is.Not.Null, "HomeScene에 튜토리얼 가이드가 없습니다.");
            return guide
                .GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Panel");
        }

        // 가이드 창을 열고 화면 영역을 돌려준다.
        private static Rect ShowTutorialGuide()
        {
            RectTransform panel = TutorialGuidePanel();
            panel.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            return ScreenRectOf(panel);
        }

        private static Rect ScreenRectOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // 보이는 Renderer를 감싸는 상자를 화면에 비춘 영역
        private static Rect RendererScreenRect(Component root)
        {
            Bounds bounds = root.GetComponentsInChildren<Renderer>()
                .Where(renderer =>
                    renderer.enabled
                    && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                )
                .Select(renderer => renderer.bounds)
                .Aggregate(
                    (all, next) =>
                    {
                        all.Encapsulate(next);
                        return all;
                    }
                );
            Camera camera = Camera.main;
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z
                );
                Vector2 screen = camera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static RectTransform HudMenuButton()
        {
            Button menu = Object
                .FindObjectsByType<Button>(FindObjectsSortMode.None)
                .SingleOrDefault(button => button.name == "MenuButton");
            Assert.That(menu, Is.Not.Null, "HomeScene에 오른쪽 위 메뉴 버튼이 없습니다.");
            return (RectTransform)menu.transform;
        }

        // 메뉴를 펼친 동안 메뉴 버튼 자리에 보이는 강조 버튼
        private static RectTransform OpenHudMenuButton()
        {
            Button open = Object
                .FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(button => button.name == "OpenMenuButton");
            Assert.That(open, Is.Not.Null, "HomeScene에 펼친 메뉴 버튼이 없습니다.");
            return (RectTransform)open.transform;
        }

        // 메뉴 버튼 줄 아래로 펼치는 목록
        private static RectTransform HudMenuList()
        {
            Transform list = OpenHudMenuButton().parent.parent.Find("Menu");
            Assert.That(list, Is.Not.Null, "HomeScene에 메뉴 목록이 없습니다.");
            return (RectTransform)list;
        }

        private static Vector2 ScreenCenterOf(RectTransform rect)
        {
            return RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
        }

        // 카메라를 사물 앞에 두고, 화면의 그 좌표를 지나는 방향이 사물 가운데를 향하도록 돌린다.
        private static void AimSoThatScreenPointShows(HoverableObject target, Vector2 screenPoint)
        {
            Camera camera = Camera.main;
            Vector3 center = VisibleBounds(target).center;
            camera.transform.SetPositionAndRotation(
                center + target.transform.forward * 2f,
                Quaternion.LookRotation(-target.transform.forward)
            );
            Vector3 through = camera.ScreenPointToRay(screenPoint).direction;
            Vector3 toTarget = (center - camera.transform.position).normalized;
            camera.transform.rotation =
                Quaternion.FromToRotation(through, toTarget) * camera.transform.rotation;
        }

        private HoverableObject PointedHoverable(Vector2 screenPoint)
        {
            return new ObjectHoverRaycast().Find(
                Camera.main.ScreenPointToRay(screenPoint),
                100f,
                Physics.DefaultRaycastLayers,
                player.transform,
                out _
            );
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
