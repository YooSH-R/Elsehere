using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Npc.PlayModeTests
{
    /// <summary>
    /// 마을(TownScene)을 실제로 불러와 행인이 먼저 다가와 대화를 제안하고, 수락·거절·무시가 이어지는지 확인한다.
    /// 마을 행인은 Ben 한 명이다. 튜토리얼 중에는 집 앞에서 길을 묻고(튜토리얼 모드), 튜토리얼을 마치면 평소 행인으로 다가온다.
    /// </summary>
    public class PasserbyTests
    {
        private GameObject bystander;
        private const string BenCode = "TOWN_PASSERBY";
        private const float WaitTimeoutSeconds = 15f;

        [TearDown]
        public void RestoreClock() => NpcTestClock.Restore();

        [SetUp]
        public void SetUp()
        {
            ResetCrossSceneRecords();
            SceneManager.sceneLoaded += IsolateTownInteractions;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(bystander);
            SceneManager.sceneLoaded -= IsolateTownInteractions;
            ResetCrossSceneRecords();
        }

        // 자동 제안과 버튼 응답을 검증하므로 다른 행인의 접근·생활과 실제 포인터의 NPC 클릭은 따로 격리한다.
        // sceneLoaded에서 멈춰 첫 Update가 실행되기 전에 준비한다. 클릭·생활 동작은 각각의 테스트가 검증한다.
        private static void IsolateTownInteractions(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TownScene")
                return;

            Single<NpcClickTalk>().enabled = false;
            foreach (
                Passerby passerby in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None)
            )
            {
                if (passerby.TryGetComponent(out NpcRoutine routine))
                    routine.enabled = false;
                if (passerby.GetComponent<NpcIdentity>().Code != BenCode)
                    passerby.enabled = false;
            }
        }

        private static void ResetCrossSceneRecords()
        {
            // 장면 교체 중 이전 행인이 제안 기록을 다시 남기지 않게 먼저 멈춘다.
            foreach (
                Passerby passerby in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None)
            )
            {
                passerby.enabled = false;
            }

            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            // 하루 한 번 기록은 이 기기에 남으므로 끝난 뒤에도 지워 Editor에서 플레이할 때 남지 않게 한다.
            // 튜토리얼 행인은 튜토리얼의 이웃 만나기 단계에서만 다가오므로, 튜토리얼도 처음부터 시작한다.
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator OfferedConversation_InteractionKeyAcceptsTheVisibleOfferOnce()
        {
            yield return LoadTownAfterTutorial();
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            object requester = new object();
            int accepted = 0;
            offer.Show(
                requester,
                "호텔 직원이 말을 걸었어요",
                "Are you checking in?",
                true,
                null,
                () =>
                {
                    accepted++;
                    offer.Hide(requester);
                },
                () => offer.Hide(requester)
            );
            InputSettings.BackgroundBehavior oldBackground = InputSystem
                .settings
                .backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode oldEditorInput = InputSystem
                .settings
                .editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;

                Assert.That(accepted, Is.EqualTo(1));
                Assert.That(offer.IsShowing, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            }
        }

        [UnityTest]
        public IEnumerator LeavingHome_TutorialPasserbyApproachesAndOffersWithoutDecline()
        {
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            // 숙소 안내(걷기·둘러보기·단어 저장·단어장)를 마쳐야 숙소 문이 열린다.
            TutorialProgress progress = TutorialState.GetOrCreate(BenCode);
            progress.Continue();
            progress.Report(TutorialAction.Walk);
            progress.Report(TutorialAction.Run);
            progress.Report(TutorialAction.Jump);
            progress.Continue();
            progress.Report(TutorialAction.Rotate);
            progress.Report(TutorialAction.Zoom);
            progress.Report(TutorialAction.ToggleView);
            progress.Continue();
            progress.ReportWordSaved();
            progress.ReportVocabularyOpened();
            yield return null;
            Assert.That(SpaceTravel.TryTravel("TownScene", "HomeDoor"), Is.True);
            yield return WaitUntil(() =>
                SceneManager.GetActiveScene().name == "TownScene" && !SpaceTravel.IsTraveling
            );

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);

            Passerby tutorial = FindPasserby(BenCode);
            Assert.That(tutorial.Phase, Is.EqualTo(PasserbyPhase.Offering));
            Assert.That(
                Object
                    .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                    .Count(npc => npc.Code == "TUTORIAL_PASSERBY"),
                Is.Zero,
                "튜토리얼 행인을 따로 두지 않고 Ben이 길을 묻습니다."
            );
            Assert.That(offer.IsDeclineShown, Is.False, "튜토리얼 행인은 거절할 수 없습니다.");
            Assert.That(
                HorizontalDistance(tutorial.transform, Single<PlayerMover>().transform),
                Is.LessThan(3f)
            );
            yield return null;
            Assert.That(
                Single<PlayerMover>().IsMovementLocked,
                Is.True,
                "튜토리얼에서는 [대화하기]에 답할 때까지 Ben에게서 멀어지지 않게 이동을 잠급니다."
            );
            Assert.That(
                PasserbyOfferRecord.HasOfferedOn(BenCode, System.DateTime.Today),
                Is.True,
                "튜토리얼에서 길을 물은 것도 그날 먼저 말을 건 것으로 셉니다."
            );
        }

        [UnityTest]
        public IEnumerator OrdinaryTownPasserby_DoesNotOfferEvenAfterTheDailyRecordIsCleared()
        {
            // Ben은 현실 하루에 한 번만 먼저 다가온다. 그날 이미 말을 걸었으면 플레이어가 먼저 말을 거는 것만 받는다.
            PasserbyOfferRecord.Save(BenCode, System.DateTime.Today);
            yield return LoadTownWithPlayerNearBen(afterTutorial: true, withEvent: false);
            Passerby passerby = FindPasserby(BenCode);
            PasserbyOfferView offer = Single<PasserbyOfferView>();

            yield return new WaitForSeconds(2f);
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));

            StandNear(BenCode, 1.5f);
            yield return null;
            Assert.That(offer.IsTalkPromptShowing, Is.False);
            Assert.That(passerby.CanTalkByClick(NpcClickTalk.ReachMeters), Is.True);

            // 마지막으로 말을 건 날이 어제가 되면(하루가 지나면) 다시 다가온다.
            StandNear(BenCode, 4f);
            PasserbyOfferRecord.Save(BenCode, System.DateTime.Today.AddDays(-1));
            yield return new WaitForSeconds(2f);
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        // 퀘스트가 Ben을 붙잡아 두면(비 오는 거리 단계) 생활하지 않고 정한 자리에서 기다리고, 그날 이미 말을 걸었어도 다가와 제안한다.
        [UnityTest]
        public IEnumerator BenHeldForTheQuest_WaitsAtTheSpot_AndOffersEvenAfterOfferingToday()
        {
            PasserbyOfferRecord.Save(BenCode, System.DateTime.Today);
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            Passerby ben = FindPasserby(BenCode);
            var spot = new Pose(new Vector3(-3.8f, .12f, -10.3f), Quaternion.Euler(0f, 270f, 0f));

            ben.WaitAt(spot);
            Vector3 offset = ben.transform.position - spot.position;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThan(.1f), "카페 앞 자리로 옮겨 기다립니다.");
            // 카페에서 나와 선 자리
            Single<PlayerMover>()
                .Teleport(new Vector3(-5.95f, .1f, -11f), Quaternion.Euler(0f, 90f, 0f));

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShownBy(ben));
            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Offering));
        }

        [UnityTest]
        public IEnumerator OfferAppears_WhenGameRunsSlowly()
        {
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = .05f;
                var approach = new PasserbyApproach(new PasserbyRules(6f, 2f, 6f, 60f, true));
                float distance = 3.5f;
                yield return WaitUntil(() =>
                {
                    distance -= 1.6f * Time.deltaTime;
                    approach.Tick(Time.realtimeSinceStartupAsDouble, distance, false);
                    return approach.Phase == PasserbyPhase.Offering;
                });

                Assert.That(approach.Phase, Is.EqualTo(PasserbyPhase.Offering));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator Conversation_HidesOnlyOtherNpcs_AndEndingRestoresTheirAppearance()
        {
            NpcTestClock.SetDaytime();
            TutorialState.MarkFinishedOnServer();
            yield return SceneManager.LoadSceneAsync("CafeScene", LoadSceneMode.Single);
            yield return null;
            NpcIdentity partner = NpcIdentity.Active.First(npc => npc.Code == "CAFE_STAFF");
            bystander = GameObject.CreatePrimitive(PrimitiveType.Cube);
            NpcIdentity other = bystander.AddComponent<NpcIdentity>();
            GameObject extraVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            extraVisual.transform.SetParent(bystander.transform);
            Renderer[] partnerRenderers = partner.GetComponentsInChildren<Renderer>(true);
            Renderer[] otherRenderers = other.GetComponentsInChildren<Renderer>(true);
            Assert.That(partnerRenderers, Is.Not.Empty);
            Assert.That(otherRenderers, Is.Not.Empty);
            bool[] partnerHidden = partnerRenderers
                .Select(renderer => renderer.forceRenderingOff)
                .ToArray();
            otherRenderers[0].enabled = false;
            otherRenderers[0].forceRenderingOff = true;
            bool[] otherHidden = otherRenderers
                .Select(renderer => renderer.forceRenderingOff)
                .ToArray();
            bool[] otherEnabled = otherRenderers.Select(renderer => renderer.enabled).ToArray();
            Collider[] colliders = other.GetComponentsInChildren<Collider>(true);
            bool[] colliderEnabled = colliders.Select(collider => collider.enabled).ToArray();
            DialogueController dialogue = Single<DialogueController>();

            Assert.That(dialogue.TryStart(partner.Code, partner.LookTarget), Is.True);
            yield return null;
            yield return null;

            Assert.That(
                otherRenderers.All(renderer => renderer.forceRenderingOff),
                Is.True,
                "주변 NPC 외형을 숨긴다"
            );
            Assert.That(
                partnerRenderers.Select(renderer => renderer.forceRenderingOff),
                Is.EqualTo(partnerHidden)
            );
            Assert.That(
                otherRenderers.Select(renderer => renderer.enabled),
                Is.EqualTo(otherEnabled)
            );
            Assert.That(other.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                colliders.Select(collider => collider.enabled),
                Is.EqualTo(colliderEnabled)
            );

            dialogue.End();
            yield return null;
            yield return null;

            Assert.That(
                otherRenderers.Select(renderer => renderer.forceRenderingOff),
                Is.EqualTo(otherHidden)
            );
            Assert.That(
                otherRenderers.Select(renderer => renderer.enabled),
                Is.EqualTo(otherEnabled)
            );
        }

        [UnityTest]
        public IEnumerator AcceptingOffer_EntersDialogueView_AndEndingRestoresControl()
        {
            // 튜토리얼 중에는 길 안내를 마칠 때까지 Ben 앞에 붙잡아 두므로, 평소 행인으로 확인한다.
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing, DescribeBenOfferWait);

            Click(offer, "AcceptButton");
            yield return null;

            PlayerMover player = Single<PlayerMover>();
            PlayerCameraRig cameraRig = Single<PlayerCameraRig>();
            DialoguePanelView panel = Single<DialoguePanelView>();
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(Single<DialogueController>().IsTalking, Is.True);
            Assert.That(GameClock.Shared.IsTalking, Is.True);
            double stoppedAt = GameClock.Shared.Minutes;
            GameClock.Shared.Flow(60);
            Assert.That(GameClock.Shared.Minutes, Is.EqualTo(stoppedAt));
            Assert.That(cameraRig.ViewMode, Is.EqualTo(CameraViewMode.Dialogue));
            Assert.That(player.IsMovementLocked, Is.True);
            Assert.That(panel.IsVisible, Is.True);

            Click(panel, "EndButton");
            yield return null;

            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Assert.That(cameraRig.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
            Assert.That(player.IsMovementLocked, Is.False);
            Assert.That(panel.IsVisible, Is.False);
            Assert.That(FindPasserby(BenCode).Phase, Is.EqualTo(PasserbyPhase.Resting));
        }

        [UnityTest]
        public IEnumerator DecliningTownPasserby_ClosesOffer_AndDoesNotOfferAgainWhileResting()
        {
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            Passerby ben = FindPasserby(BenCode);
            yield return WaitUntil(
                () => offer.IsShowing,
                () =>
                    $"Ben 상태={ben.Phase}, 거리={HorizontalDistance(ben.transform, Single<PlayerMover>().transform)}, "
                    + $"이동 잠금={Single<PlayerMover>().IsMovementLocked}, 대화={Single<DialogueController>().IsTalking}, "
                    + $"새 대화 가능={Single<DialogueController>().Allowance.CanStartConversation}, "
                    + $"접근 허용={ben.IsApproachAllowed}, 오늘 제안 기록={PasserbyOfferRecord.HasOfferedOn(BenCode, System.DateTime.Today)}, "
                    + $"행인 위치={ben.transform.position}, 플레이어 위치={Single<PlayerMover>().transform.position}"
            );
            Assert.That(offer.IsDeclineShown, Is.True);

            Click(offer, "DeclineButton");
            yield return new WaitForSeconds(2f);

            Assert.That(offer.IsShowing, Is.False);
            Assert.That(FindPasserby(BenCode).Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator WalkingAwayFromOffer_ClosesOfferWithoutLockingPlayer()
        {
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing, DescribeBenOfferWait);
            Passerby passerby = FindPasserby(BenCode);

            PlayerMover player = Single<PlayerMover>();
            player.Teleport(
                passerby.transform.position + new Vector3(0f, 0f, -12f),
                Quaternion.identity
            );
            yield return null;
            yield return null;

            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(player.IsMovementLocked, Is.False);
            Assert.That(Single<PlayerCameraRig>().ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
        }

        [UnityTest]
        public IEnumerator RestingTownPasserby_ShowsNoNearbyButton_AndClickingAsksBeforeConversation()
        {
            // 쉬는 NPC에게 가까이 가도 버튼은 뜨지 않고, 클릭 후 확인해야 대화를 시작한다.
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(
                () => offer.IsShowing,
                () =>
                    $"대화 제안 대기: 상태={FindPasserby(BenCode).Phase}, "
                    + $"오늘 제안 기록={PasserbyOfferRecord.HasOfferedOn(BenCode, System.DateTime.Today)}, "
                    + $"거리={HorizontalDistance(FindPasserby(BenCode).transform, Single<PlayerMover>().transform)}, "
                    + $"이동 잠금={Single<PlayerMover>().IsMovementLocked}, 다른 제안={offer.IsShowing}"
            );
            Click(offer, "DeclineButton");
            Passerby passerby = FindPasserby(BenCode);
            // 제자리로 돌아갈 때까지 기다린 뒤 바로 앞에 선다.
            yield return new WaitForSeconds(3f);
            StandNear(BenCode, 1.5f);

            yield return new WaitForSeconds(.5f);
            Assert.That(offer.IsTalkPromptShowing, Is.False);
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(passerby.TalkByClick(NpcClickTalk.ReachMeters), Is.True);
            Assert.That(offer.IsShownBy(passerby), Is.True);
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Click(offer, "AcceptButton");
            yield return null;

            Assert.That(Single<DialogueController>().IsTalking, Is.True);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Talking));
            Assert.That(offer.IsTalkPromptShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator GuestWithConversationsLeft_OfferShowsRemainingCount()
        {
            yield return LoadTownAfterTutorial();
            Single<DialogueController>().Allowance.Apply(GuestStatus(remaining: 2));
            Passerby ben = FindPasserby(BenCode);
            ben.WaitAt(new Pose(ben.transform.position, ben.transform.rotation));
            StandNear(BenCode, 4f);

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);

            Assert.That(offer.RemainingText, Is.EqualTo("게스트 남은 대화 2번"));
        }

        [UnityTest]
        public IEnumerator GuestUsedAllConversations_PasserbyDoesNotApproach_AndClickOpensSignup()
        {
            // 새 대화를 시작할 수 없으면 행인은 먼저 다가오지 않는다(기능 명세 F-39).
            // 대화를 다 쓴 게스트가 클릭하면 대화 대신 가입 안내 창을 연다(S15P31A305-578·705).
            yield return LoadTownAfterTutorial();
            Single<DialogueController>().Allowance.Apply(GuestStatus(remaining: 0));
            Passerby passerby = FindPasserby(BenCode);
            StandNear(BenCode, 4f);
            yield return new WaitForSeconds(1.5f);

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));

            StandNear(BenCode, 1.5f);
            yield return new WaitForSeconds(.5f);
            Assert.That(passerby.TalkByClick(NpcClickTalk.ReachMeters), Is.True);
            yield return null;
            Assert.That(Single<GuestSignupDialogView>().IsVisible, Is.True);
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator DailyLimitReached_NoNearbyButtonOrClickConversation()
        {
            // 회원이 오늘 대화를 다 쓰면 근접 버튼이나 가입 안내 없이 새 대화를 막는다.
            yield return LoadTownAfterTutorial();
            Single<DialogueController>().Allowance.Apply(MemberStatus(remaining: 0));
            Passerby passerby = FindPasserby(BenCode);
            StandNear(BenCode, 4f);
            yield return new WaitForSeconds(1.5f);

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));

            StandNear(BenCode, 1.5f);
            yield return new WaitForSeconds(.5f);
            Assert.That(offer.IsTalkPromptShowing, Is.False);
            Assert.That(passerby.TalkByClick(NpcClickTalk.ReachMeters), Is.False);
            yield return null;
            Assert.That(Single<GuestSignupDialogView>().IsVisible, Is.False);
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator GuestWithConversationsLeft_ClickConfirmationShowsRemainingCount()
        {
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "DeclineButton");
            Single<DialogueController>().Allowance.Apply(GuestStatus(remaining: 1));
            // 제자리로 돌아갈 때까지 기다린 뒤 바로 앞에 선다.
            yield return new WaitForSeconds(3f);
            StandNear(BenCode, 1.5f);

            yield return new WaitForSeconds(.5f);
            Assert.That(offer.IsTalkPromptShowing, Is.False);
            Passerby passerby = FindPasserby(BenCode);
            Assert.That(passerby.TalkByClick(NpcClickTalk.ReachMeters), Is.True);
            Assert.That(offer.IsShownBy(passerby), Is.True);
            Assert.That(offer.RemainingText, Is.EqualTo("게스트 남은 대화 1번"));
        }

        private static IEnumerator LoadTown()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
        }

        private static void StandNear(string passerbyCode, float distanceMeters)
        {
            Passerby passerby = FindPasserby(passerbyCode);
            Single<PlayerMover>()
                .Teleport(
                    passerby.transform.position + passerby.transform.forward * distanceMeters,
                    Quaternion.identity
                );
        }

        private static AiStatusData GuestStatus(int remaining) => Status(remaining, guest: true);

        private static AiStatusData MemberStatus(int remaining) => Status(remaining, guest: false);

        private static AiStatusData Status(int remaining, bool guest)
        {
            return new AiStatusData
            {
                Status = remaining > 0 ? "AI_AVAILABLE" : "AI_EXHAUSTED",
                Capabilities = new AiCapabilities { CanStartConversation = remaining > 0 },
                RemainingConversations = remaining,
                Guest = guest,
            };
        }

        /// <summary>
        /// 튜토리얼을 마친 사용자로 마을을 불러온다. Ben은 평소 행인으로 다가온다. 다가오기만 보려고 생활은 끈다(생활은 NpcRoutineTests).
        /// </summary>
        private static IEnumerator LoadTownAfterTutorial()
        {
            NpcTestClock.SetDaytime();
            TutorialState.MarkFinishedOnServer();
            yield return LoadTown();
        }

        /// <summary>마을을 불러온 뒤 플레이어를 Ben 앞에 세운다. 튜토리얼 중이면 Ben은 길을 묻는 튜토리얼 모드다.</summary>
        private static IEnumerator LoadTownWithPlayerNearBen(
            bool afterTutorial,
            bool withEvent = true
        )
        {
            if (afterTutorial)
            {
                yield return LoadTownAfterTutorial();
            }
            else
            {
                yield return LoadTown();
            }

            Passerby passerby = FindPasserby(BenCode);
            if (afterTutorial && withEvent)
                passerby.WaitAt(new Pose(passerby.transform.position, passerby.transform.rotation));
            PlayerMover player = Single<PlayerMover>();
            player.Teleport(
                passerby.transform.position + passerby.transform.forward * 4f,
                Quaternion.identity
            );
            yield return null;
        }

        private static string DescribeBenOfferWait()
        {
            Passerby ben = Object
                .FindObjectsByType<Passerby>(FindObjectsSortMode.None)
                .FirstOrDefault(npc => npc.GetComponent<NpcIdentity>().Code == BenCode);
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            if (ben == null || player == null)
            {
                return $"씬={SceneManager.GetActiveScene().name}, Ben 또는 플레이어가 없습니다.";
            }

            return $"씬={SceneManager.GetActiveScene().name}, Ben 상태={ben.Phase}, "
                + $"거리={HorizontalDistance(ben.transform, player.transform)}, "
                + $"이동 잠금={player.IsMovementLocked}, 접근 허용={ben.IsApproachAllowed}, "
                + $"오늘 제안 기록={PasserbyOfferRecord.HasOfferedOn(BenCode, System.DateTime.Today)}";
        }

        private static IEnumerator WaitUntil(
            System.Func<bool> condition,
            System.Func<string> diagnostics = null
        )
        {
            float gameDeadline = Time.time + WaitTimeoutSeconds;
            // 느린 프레임에서는 NPC 이동 시간이 덜 흐른다. 게임이 멈춘 경우도 끝나도록 실제 시간 상한을 둔다.
            float realDeadline = Time.realtimeSinceStartup + WaitTimeoutSeconds * 6f;
            while (!condition())
            {
                if (Time.time > gameDeadline || Time.realtimeSinceStartup > realDeadline)
                {
                    Assert.Fail($"기다리는 상태가 되지 않았습니다. {diagnostics?.Invoke()}");
                }

                yield return null;
            }
        }

        private static void Click(Component view, string buttonName)
        {
            Button button = view.GetComponentsInChildren<Button>(true)
                .Single(candidate => candidate.name == buttonName);
            Assert.That(
                button.gameObject.activeInHierarchy,
                Is.True,
                $"{buttonName}이 보이지 않습니다."
            );
            button.onClick.Invoke();
        }

        private static Passerby FindPasserby(string code)
        {
            return Object
                .FindObjectsByType<Passerby>(FindObjectsSortMode.None)
                .Single(passerby => passerby.GetComponent<NpcIdentity>().Code == code);
        }

        private static T Single<T>()
            where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), $"{typeof(T).Name} 개수");
            return found[0];
        }

        private static float HorizontalDistance(Transform first, Transform second)
        {
            Vector3 offset = first.position - second.position;
            return new Vector2(offset.x, offset.z).magnitude;
        }
    }
}
