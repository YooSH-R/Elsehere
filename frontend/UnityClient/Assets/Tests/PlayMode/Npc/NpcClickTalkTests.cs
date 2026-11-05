using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using A305.Dialogue;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.Npc.PlayModeTests
{
    /// <summary>
    /// 마을(TownScene)에서 NPC 몸이나 머리 위 이름표를 클릭·탭해 말을 거는지 확인한다(기능 명세 F-22).
    /// 8m 안의 NPC만 클릭으로 대화를 시작하고, PC에서는 커서를 올린 NPC에 테두리를 그린다.
    /// 실제 마우스 대신 NPC 가슴이나 이름표 가운데의 화면 좌표를 넣어 확인한다.
    /// </summary>
    public class NpcClickTalkTests
    {
        private const string BenCode = "TOWN_PASSERBY";
        private const string BenName = "Ben";
        private const string RubyCode = "TOWN_RETIREE";
        private const string RubyName = "Ruby";
        private const float WaitTimeoutSeconds = 15f;

        // 카메라가 플레이어를 따라와 자리 잡을 때까지 기다리는 프레임 수
        private const int SettleFrames = 10;
        private static Vector3 standDirection;
        private static readonly Vector3 OutOfTown = new Vector3(0f, -200f, 0f);

        [TearDown]
        public void RestoreClock() => NpcTestClock.Restore();

        [SetUp]
        public void SetUp()
        {
            ResetCrossSceneRecords();
            SceneManager.sceneLoaded += IsolateDoorways;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= IsolateDoorways;
            ResetCrossSceneRecords();
        }

        // NPC 앞 거리만 바꾸는 검사에서 숙소 출입구를 밟아 씬이 바뀌지 않게 한다. 공간 이동은 World 테스트가 검증한다.
        private static void IsolateDoorways(Scene scene, LoadSceneMode mode)
        {
            foreach (
                SpaceDoorway door in Object.FindObjectsByType<SpaceDoorway>(
                    FindObjectsSortMode.None
                )
            )
                door.enabled = false;
            foreach (
                ShopDoorway door in Object.FindObjectsByType<ShopDoorway>(FindObjectsSortMode.None)
            )
                door.enabled = false;
        }

        private static void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator OrdinaryNpc_DoesNotInterruptWithAnUnsolicitedOffer()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            yield return FaceFrom(ben, 1.5f);
            yield return new WaitForSeconds(2f);
            Assert.That(Single<PasserbyOfferView>().IsShowing, Is.False);
            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Waiting));
            Assert.That(ben.CanTalkByClick(8f), Is.True);
        }

        [UnityTest]
        public IEnumerator ClickingNpc_AsksBeforeStarting_AndCancelKeepsTheConversationClosed()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);
            Assert.That(ben.TalkByClick(8f), Is.True);
            yield return null;
            var offer = Single<PasserbyOfferView>();
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Assert.That(offer.IsShownBy(ben), Is.True);
            Assert.That(
                offer
                    .GetComponentsInChildren<TMP_Text>()
                    .Any(text => text.text == "Ben에게 말을 걸까요?"),
                Is.True
            );
            offer
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "DeclineButton")
                .onClick.Invoke();
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator ClickingBenSixMetersAway_ConfirmingStartsConversation()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);

            Single<NpcClickTalk>().Click(AimAt(ben));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(Single<DialogueController>().IsTalking, Is.True);
            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Talking));
        }

        [UnityTest]
        public IEnumerator ClickingBenTenMetersAway_DoesNotStartConversation()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 10f);

            Single<NpcClickTalk>().Click(AimAt(ben));
            yield return null;

            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        [UnityTest]
        public IEnumerator ClickingOfferingBen_AcceptsTheOffer()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            ben.WaitAt(new Pose(ben.transform.position, ben.transform.rotation));
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return FaceFrom(ben, 4f);
            yield return WaitUntil(() => offer.IsShownBy(ben));

            Single<NpcClickTalk>().Click(AimAt(ben));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(Single<DialogueController>().IsTalking, Is.True);
            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Talking));
            Assert.That(offer.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator ClickingAnotherNpcWhileBenOffers_DeclinesBenAndTalksToClickedNpc()
        {
            // 다가와 제안하는 NPC가 있어도 원하는 NPC를 눌러 말을 걸 수 있다. 제안한 NPC는 거절한 것처럼 쉰다.
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            ben.WaitAt(new Pose(ben.transform.position, ben.transform.rotation));
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return FaceFrom(ben, 4f);
            yield return WaitUntil(() => offer.IsShownBy(ben));
            Passerby neighbor = PlaceAnotherNpcBeside(ben);
            yield return null;

            Single<NpcClickTalk>().Click(AimAt(neighbor));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(neighbor.Phase, Is.EqualTo(PasserbyPhase.Talking));
            Assert.That(Single<DialogueController>().IsTalking, Is.True);
        }

        [UnityTest]
        public IEnumerator GuestWhoUsedAllConversations_ClickingBen_OpensSignupDialog()
        {
            // [가입하고 계속 대화하기]를 누른 것과 같다(S15P31A305-578).
            yield return LoadTownAfterTutorial();
            Single<DialogueController>().Allowance.Apply(GuestOutOfConversations());
            Passerby ben = FindPasserby(BenCode);
            yield return FaceFrom(ben, 6f);

            Single<NpcClickTalk>().Click(AimAt(ben));
            yield return null;

            Assert.That(Single<GuestSignupDialogView>().IsVisible, Is.True);
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator PointerOverBenWithinReach_OutlinesBen_AndWalkingAwayRemovesIt()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);
            var clickTalk = Single<NpcClickTalk>();
            Renderer[] parts = ben.GetComponentsInChildren<Renderer>()
                .Where(part => part is SkinnedMeshRenderer || part is MeshRenderer)
                .ToArray();
            Assert.That(parts, Is.Not.Empty, "준비: Ben의 몸 그림");

            clickTalk.RefreshHighlight(AimAt(ben));

            Assert.That(clickTalk.HighlightedNpc, Is.SameAs(ben));
            Assert.That(parts.All(HasOutline), Is.True, "모든 몸 그림에 테두리가 있어야 합니다.");

            yield return FaceFrom(ben, 10f);
            clickTalk.RefreshHighlight(AimAt(ben));

            Assert.That(clickTalk.HighlightedNpc, Is.Null);
            Assert.That(parts.Any(HasOutline), Is.False, "8m 밖이면 테두리를 지워야 합니다.");
        }

        [UnityTest]
        public IEnumerator PointerOverBenWhileTalking_DoesNotOutlineBen()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);
            var clickTalk = Single<NpcClickTalk>();
            clickTalk.Click(AimAt(ben));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            clickTalk.RefreshHighlight(AimAt(ben));

            Assert.That(ben.CanTalkByClick(NpcClickTalk.ReachMeters), Is.False);
            Assert.That(clickTalk.HighlightedNpc, Is.Null);
        }

        [UnityTest]
        public IEnumerator ClickingBensNameTag_ConfirmingStartsConversation()
        {
            // 머리 위 이름표를 눌러도 몸을 누른 것과 같다.
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);
            yield return LetCameraSettle();

            Single<NpcClickTalk>().Click(NameTagOf(ben, BenName));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(Single<DialogueController>().IsTalking, Is.True);
            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Talking));
        }

        [UnityTest]
        public IEnumerator ClickingRubysNameTagWhileBenOffers_DeclinesBenAndTalksToRuby()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            ben.WaitAt(new Pose(ben.transform.position, ben.transform.rotation));
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return FaceFrom(ben, 4f);
            yield return WaitUntil(() => offer.IsShownBy(ben));
            Passerby ruby = BringNpc(
                RubyCode,
                new Pose(
                    ben.transform.position + ben.transform.right * 1.5f,
                    ben.transform.rotation
                )
            );
            yield return LetCameraSettle();

            Single<NpcClickTalk>().Click(NameTagOf(ruby, RubyName));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(offer.IsShowing, Is.False);
            Assert.That(ruby.Phase, Is.EqualTo(PasserbyPhase.Talking));
            Assert.That(Single<DialogueController>().IsTalking, Is.True);
        }

        [UnityTest]
        public IEnumerator GuestWhoUsedAllConversations_ClickingBensNameTag_OpensSignupDialog()
        {
            yield return LoadTownAfterTutorial();
            Single<DialogueController>().Allowance.Apply(GuestOutOfConversations());
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);
            yield return LetCameraSettle();

            Single<NpcClickTalk>().Click(NameTagOf(ben, BenName));
            yield return null;

            Assert.That(Single<GuestSignupDialogView>().IsVisible, Is.True);
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator PointerOverBensNameTag_OutlinesBen()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);
            yield return LetCameraSettle();
            var clickTalk = Single<NpcClickTalk>();
            Renderer[] parts = ben.GetComponentsInChildren<Renderer>()
                .Where(part => part is SkinnedMeshRenderer || part is MeshRenderer)
                .ToArray();

            clickTalk.RefreshHighlight(NameTagOf(ben, BenName));

            Assert.That(clickTalk.HighlightedNpc, Is.SameAs(ben));
            Assert.That(
                parts.All(HasOutline),
                Is.True,
                "이름표의 NPC 몸에 테두리가 있어야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator UiCoveringBensNameTag_KeepsTheClickFromReachingBen()
        {
            // 이름표는 다른 화면 UI보다 아래에 그려진다. 메뉴·창이 이름표를 덮었으면 그 UI를 누른 것이다.
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);
            yield return LetCameraSettle();
            Vector2 tag = NameTagOf(ben, BenName);
            CoverScreenWithUi();
            yield return null;

            Single<NpcClickTalk>().Click(tag);
            yield return null;

            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        // 겹친 이름표는 카메라에서 가까운 NPC 것이 위에 그려지고, 겹친 곳을 누르면 그 NPC에게 말을 건다.
        // 이름표를 그리는 순서가 NPC 목록 순서에 따르지 않는지 보려고 앞뒤를 바꿔 두 번 확인한다.
        [UnityTest]
        public IEnumerator ClickingWhereNameTagsOverlap_TalksToTheNearerNpc_BenInFront()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 5f);
            yield return LetCameraSettle();
            Passerby ruby = HoldNpcAlongCameraLine(ben, 1.5f);
            yield return LetCameraSettle();

            ruby.IsApproachAllowed = true;
            Single<NpcClickTalk>().Click(WhereNameTagsOverlap(BenName, RubyName));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(ben.Phase, Is.EqualTo(PasserbyPhase.Talking));
            Assert.That(ruby.Phase, Is.Not.EqualTo(PasserbyPhase.Talking));
        }

        [UnityTest]
        public IEnumerator ClickingWhereNameTagsOverlap_TalksToTheNearerNpc_RubyInFront()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6.5f);
            yield return LetCameraSettle();
            Passerby ruby = HoldNpcAlongCameraLine(ben, -1.5f);
            yield return LetCameraSettle();

            ruby.IsApproachAllowed = true;
            Single<NpcClickTalk>().Click(WhereNameTagsOverlap(RubyName, BenName));
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            Single<PasserbyOfferView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(ruby.Phase, Is.EqualTo(PasserbyPhase.Talking));
            Assert.That(ben.Phase, Is.Not.EqualTo(PasserbyPhase.Talking));
        }

        // 튜토리얼을 마친 사용자로 마을을 불러온다. 다가오기·말 걸기만 보려고 Ben의 생활은 끈다(생활은 NpcRoutineTests).
        // 길을 지나는 다른 NPC가 누른 자리를 가리지 않도록, 다른 NPC는 멈춰 마을 밖으로 옮긴다.
        private static IEnumerator LoadTownAfterTutorial()
        {
            NpcTestClock.SetDaytime();
            TutorialState.MarkFinishedOnServer();
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            Passerby ben = FindPasserby(BenCode);
            ben.GetComponent<NpcRoutine>().enabled = false;
            foreach (Passerby other in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None))
            {
                if (other == ben)
                {
                    continue;
                }

                if (other.TryGetComponent(out NpcRoutine routine))
                {
                    routine.enabled = false;
                }

                other.enabled = false;
                PlaceAt(other, new Pose(OutOfTown, Quaternion.identity));
            }
        }

        // 마을 행인은 하루에 한 번만 먼저 다가온다. 오늘 이미 다가온 것으로 해 두고 클릭으로만 말을 걸게 한다.
        private static void KeepFromApproaching(Passerby passerby)
        {
            PasserbyOfferRecord.Save(passerby.GetComponent<NpcIdentity>().Code, DateTime.Today);
        }

        // 플레이어를 NPC 앞 주어진 거리에 NPC를 보게 세운다.
        private static IEnumerator FaceFrom(Passerby passerby, float distanceMeters)
        {
            Vector3 npcPosition = passerby.transform.position;
            // 집을 바라보는 NPC의 정면은 건물 안일 수 있다. 벽과 출입구 밖에서 두 거리를 모두 볼 수 있는 방향을 고른다.
            Physics.SyncTransforms();
            standDirection = new[]
            {
                Vector3.forward,
                Vector3.right,
                Vector3.back,
                Vector3.left,
            }.First(direction =>
                new[] { 6f, 10f }.All(distance =>
                {
                    Vector3 eye = npcPosition + direction * distance + Vector3.up * 1.5f;
                    Vector3 chest = npcPosition + Vector3.up * 1.1f;
                    return new NpcPointerRaycast().Find(new Ray(eye, chest - eye), 30f) == passerby;
                })
            );
            Vector3 standAt = npcPosition + standDirection * distanceMeters;
            Vector3 towardNpc = npcPosition - standAt;
            towardNpc.y = 0f;
            Quaternion facing = Quaternion.LookRotation(towardNpc);
            Single<PlayerMover>().Teleport(standAt, facing);
            Single<PlayerCameraRig>().SnapView(facing.eulerAngles.y);
            yield return null;
        }

        // 마을 밖으로 옮겨 둔 다른 행인 하나를 Ben 옆 1.5m에 세운다. 생활은 끈 채로 두고 먼저 다가오지 않게 한다.
        private static Passerby PlaceAnotherNpcBeside(Passerby ben)
        {
            Passerby neighbor = Object
                .FindObjectsByType<Passerby>(FindObjectsSortMode.None)
                .First(candidate => candidate != ben && !candidate.enabled);
            KeepFromApproaching(neighbor);
            PlaceAt(
                neighbor,
                new Pose(
                    ben.transform.position + ben.transform.right * 1.5f,
                    ben.transform.rotation
                )
            );
            neighbor.enabled = true;
            return neighbor;
        }

        // 마을 밖으로 옮겨 둔 행인 하나를 주어진 자리에 세운다. 생활은 끈 채로 두고 먼저 다가오지 않게 한다.
        // 옮긴 자리를 그 NPC의 제자리로도 정한다. 생활을 끈 행인은 처음 놓인 자리(Passerby.homePose)로 걸어 돌아가므로,
        // 제자리가 마을 밖에 남아 있으면 확인하는 동안 NPC가 걸어가 이름표가 움직이거나 보이는 거리 밖으로 나간다.
        private static Passerby BringNpc(string code, Pose pose)
        {
            Passerby npc = FindPasserby(code);
            KeepFromApproaching(npc);
            PlaceAt(npc, pose);
            typeof(Passerby)
                .GetField("homePose", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(npc, pose);
            npc.enabled = true;
            return npc;
        }

        // 이름표가 겹치도록 카메라와 Ben을 잇는 선에 세우고, 준비 중 원래 자리로 돌아가지 않게 한다.
        private static Passerby HoldNpcAlongCameraLine(Passerby ben, float distanceFromBen)
        {
            Vector3 towardBen = Vector3
                .ProjectOnPlane(ben.transform.position - Camera.main.transform.position, Vector3.up)
                .normalized;
            var pose = new Pose(
                ben.transform.position + towardBen * distanceFromBen,
                ben.transform.rotation
            );
            Passerby npc = BringNpc(RubyCode, pose);
            npc.IsApproachAllowed = false;
            npc.WaitAt(pose);
            return npc;
        }

        private static void PlaceAt(Passerby npc, Pose pose)
        {
            var body = npc.GetComponent<CharacterController>();
            body.enabled = false;
            npc.transform.SetPositionAndRotation(pose.position, pose.rotation);
            // 시험에서 옮겨 세운 NPC가 이전 배치 지점으로 돌아가지 않게 기준 자리도 함께 옮긴다.
            typeof(Passerby)
                .GetField("homePose", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(npc, pose);
            body.enabled = true;
        }

        // 카메라를 플레이어 눈높이에 두고 NPC 가슴을 보게 한 뒤 그 화면 좌표를 돌려준다. 카메라 리그가 부드럽게 따라가는 위치에
        // 결과가 흔들리지 않도록 같은 프레임 안에서 이 좌표로 클릭·호버를 확인하고, 그 NPC가 맨 앞에 보이는지도 먼저 확인한다.
        private static Vector2 AimAt(Passerby passerby)
        {
            Physics.SyncTransforms();
            Camera camera = Camera.main;
            Vector3 chest = passerby.transform.position + Vector3.up * 1.1f;
            Vector3 eye = Single<PlayerMover>().transform.position + Vector3.up * 1.5f;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(chest - eye));
            Vector2 onScreen = camera.WorldToScreenPoint(chest);
            Assert.That(
                new NpcPointerRaycast().Find(camera.ScreenPointToRay(onScreen), 30f),
                Is.SameAs(passerby),
                $"준비: {passerby.name}이(가) 맨 앞에 보여야 합니다."
            );
            return onScreen;
        }

        // 카메라가 플레이어를 따라와 자리 잡고, 이름표가 그 화면에서 머리 위에 놓일 때까지 기다린다.
        private static IEnumerator LetCameraSettle()
        {
            for (int frame = 0; frame < SettleFrames; frame++)
            {
                yield return null;
            }
        }

        // NPC 머리 위 이름표 가운데의 화면 좌표. 몸 클릭이 아니라 이름표 클릭을 보도록,
        // 그 자리로 쏜 광선이 그 NPC 몸에 닿지 않는지 먼저 확인한다.
        private static Vector2 NameTagOf(Passerby passerby, string shownName)
        {
            Vector2 center = ShownNameTagRect(shownName).center;
            Physics.SyncTransforms();
            Assert.That(
                new NpcPointerRaycast().Find(Camera.main.ScreenPointToRay(center), 30f),
                Is.Not.SameAs(passerby),
                $"준비: {shownName}의 이름표 자리는 몸 밖이어야 합니다."
            );
            return center;
        }

        // 두 NPC 이름표가 겹친 곳의 가운데 화면 좌표
        private static Vector2 WhereNameTagsOverlap(string oneName, string otherName)
        {
            Rect one = ShownNameTagRect(oneName);
            Rect other = ShownNameTagRect(otherName);
            Rect overlap = Rect.MinMaxRect(
                Mathf.Max(one.xMin, other.xMin),
                Mathf.Max(one.yMin, other.yMin),
                Mathf.Min(one.xMax, other.xMax),
                Mathf.Min(one.yMax, other.yMax)
            );
            Assert.That(
                overlap.width > 0f && overlap.height > 0f,
                Is.True,
                $"준비: {oneName}와 {otherName}의 이름표가 겹쳐야 합니다. ({one} / {other})"
            );
            return overlap.center;
        }

        // 화면에 보이는 이름표의 화면 영역. 이름표는 화면을 덮는 캔버스에 있어 꼭짓점 위치가 곧 화면 좌표다.
        private static Rect ShownNameTagRect(string shownName)
        {
            RectTransform tag = Single<NpcNameTagView>()
                .GetComponentsInChildren<TMP_Text>()
                .Where(text => text.name == "Name" && text.text == shownName)
                .Select(text => (RectTransform)text.transform.parent)
                .SingleOrDefault();
            Assert.That(tag, Is.Not.Null, $"준비: {shownName}의 이름표가 보여야 합니다.");
            var corners = new Vector3[4];
            tag.GetWorldCorners(corners);
            Rect onScreen = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            Assert.That(
                new Rect(0f, 0f, Screen.width, Screen.height).Contains(onScreen.center),
                Is.True,
                $"준비: {shownName}의 이름표가 화면 안에 있어야 합니다."
            );
            return onScreen;
        }

        // 화면 전체를 덮는 창 하나를 띄운다. 정렬 0은 다른 화면 UI 캔버스의 기본값이다. 다음 시험에서 마을을 다시 불러오면 사라진다.
        private static void CoverScreenWithUi()
        {
            var cover = new GameObject("Cover", typeof(Canvas), typeof(GraphicRaycaster));
            cover.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(cover.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool HasOutline(Renderer renderer)
        {
            Material last = renderer.sharedMaterials.Last();
            return last != null && last.shader.name == "A305/NpcHoverOutline";
        }

        private static AiStatusData GuestOutOfConversations()
        {
            return new AiStatusData
            {
                Status = "AI_EXHAUSTED",
                Capabilities = new AiCapabilities { CanStartConversation = false },
                RemainingConversations = 0,
            };
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
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
    }
}
