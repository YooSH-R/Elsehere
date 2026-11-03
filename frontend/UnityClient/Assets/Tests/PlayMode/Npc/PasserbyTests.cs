using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
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
        private const string BenCode = "TOWN_PASSERBY";
        private const float WaitTimeoutSeconds = 15f;

        [SetUp]
        [TearDown]
        public void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            // 하루 한 번 기록은 이 기기에 남으므로 끝난 뒤에도 지워 Editor에서 플레이할 때 남지 않게 한다.
            // 튜토리얼 행인은 튜토리얼의 이웃 만나기 단계에서만 다가오므로, 튜토리얼도 처음부터 시작한다.
            TutorialState.Clear();
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
        public IEnumerator TownPasserbyWhoOfferedToday_WaitsUntilTomorrow_ButPlayerCanStillTalk()
        {
            // Ben은 현실 하루에 한 번만 먼저 다가온다. 그날 이미 말을 걸었으면 플레이어가 먼저 말을 거는 것만 받는다.
            PasserbyOfferRecord.Save(BenCode, System.DateTime.Today);
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            Passerby passerby = FindPasserby(BenCode);
            PasserbyOfferView offer = Single<PasserbyOfferView>();

            yield return new WaitForSeconds(2f);
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));

            StandNear(BenCode, 1.5f);
            yield return WaitUntil(() => offer.IsTalkPromptShowing);

            // 마지막으로 말을 건 날이 어제가 되면(하루가 지나면) 다시 다가온다.
            StandNear(BenCode, 4f);
            PasserbyOfferRecord.Save(BenCode, System.DateTime.Today.AddDays(-1));
            yield return WaitUntil(() => offer.IsShowing);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Offering));
        }

        [UnityTest]
        public IEnumerator ApproachingBen_AnimatesLegs_AndStopsWhenOffering()
        {
            yield return LoadTownWithPlayerNearBen(afterTutorial: false);
            Passerby ben = FindPasserby(BenCode);
            Animator animator = ben.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null, "Ben의 모델에 걷기 Animator가 필요합니다.");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            yield return WaitUntil(() => ben.Phase == PasserbyPhase.Approaching);
            // Ben의 비즈니스맨 외형은 마스터 골격(Humanoid)이라 허벅지를 Humanoid 뼈로 찾는다.
            Transform leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Quaternion before = leg.localRotation;
            Vector3 positionBefore = ben.transform.position;
            yield return new WaitForSeconds(.15f);
            Assert.That(
                Vector3.Distance(positionBefore, ben.transform.position),
                Is.GreaterThan(.01f)
            );
            Assert.That(Quaternion.Angle(before, leg.localRotation), Is.GreaterThan(1f));
            Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(0f));

            yield return WaitUntil(() => ben.Phase == PasserbyPhase.Offering);
            yield return new WaitForSeconds(.25f);
            Assert.That(animator.GetFloat("Speed"), Is.Zero);
            before = leg.localRotation;
            yield return new WaitForSeconds(.15f);
            Assert.That(Quaternion.Angle(before, leg.localRotation), Is.LessThan(.1f));
        }

        [UnityTest]
        public IEnumerator AcceptingOffer_EntersDialogueView_AndEndingRestoresControl()
        {
            // 튜토리얼 중에는 길 안내를 마칠 때까지 Ben 앞에 붙잡아 두므로, 평소 행인으로 확인한다.
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);

            Click(offer, "AcceptButton");
            yield return null;

            PlayerMover player = Single<PlayerMover>();
            PlayerCameraRig cameraRig = Single<PlayerCameraRig>();
            DialoguePanelView panel = Single<DialoguePanelView>();
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(Single<DialogueController>().IsTalking, Is.True);
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
        public IEnumerator EndingDialogue_TurnsPlayerAndCameraTowardTheNpc()
        {
            // 튜토리얼 길 안내는 대사를 마치기 전에 끝낼 수 없으므로 평소 행인으로 확인한다.
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);

            // 대화 전에는 Ben을 등지고 서 있게 해, 끝난 뒤 방향이 대화 덕분에 바뀐 것인지 확인한다.
            PlayerMover player = Single<PlayerMover>();
            Transform face = FindPasserby(BenCode).GetComponent<NpcIdentity>().LookTarget;
            Vector3 towardBen = Vector3.ProjectOnPlane(
                face.position - player.transform.position,
                Vector3.up
            );
            player.Teleport(player.transform.position, Quaternion.LookRotation(-towardBen));
            Click(offer, "AcceptButton");
            yield return null;
            Click(Single<DialoguePanelView>(), "EndButton");
            yield return null;

            float benYaw = Quaternion.LookRotation(towardBen).eulerAngles.y;
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, benYaw)),
                Is.LessThan(2f),
                "대화가 끝나면 플레이어가 대화하던 NPC 쪽을 봅니다."
            );
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(Single<PlayerCameraRig>().ViewYawDegrees, benYaw)),
                Is.LessThan(2f),
                "카메라도 대화 시점이 보던 방향 그대로 플레이어 뒤에 섭니다."
            );
        }

        [UnityTest]
        public IEnumerator DecliningTownPasserby_ClosesOffer_AndDoesNotOfferAgainWhileResting()
        {
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
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
            yield return WaitUntil(() => offer.IsShowing);
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
        public IEnumerator RestingTownPasserby_ShowsTalkButtonNearby_AndPressingItStartsConversation()
        {
            // 쉬는 동안 행인은 다가오지 않으므로 플레이어가 [말 걸기]로 먼저 다시 말을 건다(기능 명세 F-22).
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "DeclineButton");
            Passerby passerby = FindPasserby(BenCode);
            // 제자리로 돌아갈 때까지 기다린 뒤 바로 앞에 선다.
            yield return new WaitForSeconds(3f);
            Single<PlayerMover>()
                .Teleport(
                    passerby.transform.position + passerby.transform.forward * 1.5f,
                    Quaternion.identity
                );

            yield return WaitUntil(() => offer.IsTalkPromptShowing);
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Click(offer, "TalkButton");
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
            StandNear(BenCode, 4f);

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);

            Assert.That(offer.RemainingText, Is.EqualTo("게스트 남은 대화 2번"));
        }

        [UnityTest]
        public IEnumerator GuestUsedAllConversations_PasserbyDoesNotApproach_AndTalkButtonSaysWhy()
        {
            // 새 대화를 시작할 수 없으면 행인은 먼저 다가오지 않고(기능 명세 F-39), [말 걸기] 자리에 이유를 보인다.
            yield return LoadTownAfterTutorial();
            Single<DialogueController>().Allowance.Apply(GuestStatus(remaining: 0));
            Passerby passerby = FindPasserby(BenCode);
            StandNear(BenCode, 4f);
            yield return new WaitForSeconds(1.5f);

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));

            StandNear(BenCode, 1.5f);
            yield return WaitUntil(() => offer.IsTalkPromptShowing);

            Assert.That(offer.IsTalkBlocked, Is.True);
            Assert.That(offer.TalkLabelText, Is.EqualTo("게스트 대화를 모두 썼어요"));
            Button talk = offer
                .GetComponentsInChildren<Button>(true)
                .Single(candidate => candidate.name == "TalkButton");
            Assert.That(talk.interactable, Is.False);
            talk.onClick.Invoke();
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator GuestWithConversationsLeft_TalkButtonShowsRemainingCount()
        {
            yield return LoadTownWithPlayerNearBen(afterTutorial: true);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "DeclineButton");
            Single<DialogueController>().Allowance.Apply(GuestStatus(remaining: 1));
            // 제자리로 돌아갈 때까지 기다린 뒤 바로 앞에 선다.
            yield return new WaitForSeconds(3f);
            StandNear(BenCode, 1.5f);

            yield return WaitUntil(() => offer.IsTalkPromptShowing);

            Assert.That(offer.IsTalkBlocked, Is.False);
            Assert.That(offer.TalkRemainingText, Is.EqualTo("게스트 남은 대화 1번"));
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

        private static AiStatusData GuestStatus(int remaining)
        {
            return new AiStatusData
            {
                Status = remaining > 0 ? "AI_AVAILABLE" : "AI_EXHAUSTED",
                Capabilities = new AiCapabilities { CanStartConversation = remaining > 0 },
                RemainingConversations = remaining,
            };
        }

        /// <summary>
        /// 튜토리얼을 마친 사용자로 마을을 불러온다. Ben은 평소 행인으로 다가온다. 다가오기만 보려고 생활은 끈다(생활은 NpcRoutineTests).
        /// </summary>
        private static IEnumerator LoadTownAfterTutorial()
        {
            TutorialState.MarkFinishedOnServer();
            yield return LoadTown();
            FindPasserby(BenCode).GetComponent<NpcRoutine>().enabled = false;
        }

        /// <summary>마을을 불러온 뒤 플레이어를 Ben 앞에 세운다. 튜토리얼 중이면 Ben은 길을 묻는 튜토리얼 모드다.</summary>
        private static IEnumerator LoadTownWithPlayerNearBen(bool afterTutorial)
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
            PlayerMover player = Single<PlayerMover>();
            player.Teleport(
                passerby.transform.position + passerby.transform.forward * 4f,
                Quaternion.identity
            );
            yield return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("기다리는 상태가 되지 않았습니다.");
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
