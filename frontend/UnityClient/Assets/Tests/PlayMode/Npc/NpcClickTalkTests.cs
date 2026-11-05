using System;
using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Player;
using A305.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Npc.PlayModeTests
{
    /// <summary>
    /// 마을(TownScene)에서 NPC 몸을 클릭·탭해 말을 거는지 확인한다(기능 명세 F-22).
    /// 8m 안의 NPC만 클릭으로 대화를 시작하고, PC에서는 커서를 올린 NPC에 테두리를 그린다.
    /// 실제 마우스 대신 NPC 가슴의 화면 좌표를 넣어 확인한다.
    /// </summary>
    public class NpcClickTalkTests
    {
        private const string BenCode = "TOWN_PASSERBY";
        private const float WaitTimeoutSeconds = 15f;
        private static readonly Vector3 OutOfTown = new Vector3(0f, -200f, 0f);

        [SetUp]
        [TearDown]
        public void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator ClickingBenSixMetersAway_StartsConversation()
        {
            yield return LoadTownAfterTutorial();
            Passerby ben = FindPasserby(BenCode);
            KeepFromApproaching(ben);
            yield return FaceFrom(ben, 6f);

            Single<NpcClickTalk>().Click(AimAt(ben));
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
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return FaceFrom(ben, 4f);
            yield return WaitUntil(() => offer.IsShownBy(ben));

            Single<NpcClickTalk>().Click(AimAt(ben));
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
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return FaceFrom(ben, 4f);
            yield return WaitUntil(() => offer.IsShownBy(ben));
            Passerby neighbor = PlaceAnotherNpcBeside(ben);
            yield return null;

            Single<NpcClickTalk>().Click(AimAt(neighbor));
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

            clickTalk.RefreshHighlight(AimAt(ben));

            Assert.That(ben.CanTalkByClick(NpcClickTalk.ReachMeters), Is.False);
            Assert.That(clickTalk.HighlightedNpc, Is.Null);
        }

        // 튜토리얼을 마친 사용자로 마을을 불러온다. 다가오기·말 걸기만 보려고 Ben의 생활은 끈다(생활은 NpcRoutineTests).
        // 길을 지나는 다른 NPC가 누른 자리를 가리지 않도록, 다른 NPC는 멈춰 마을 밖으로 옮긴다.
        private static IEnumerator LoadTownAfterTutorial()
        {
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
            Vector3 standAt = npcPosition + passerby.transform.forward * distanceMeters;
            Vector3 towardNpc = npcPosition - standAt;
            towardNpc.y = 0f;
            Single<PlayerMover>().Teleport(standAt, Quaternion.LookRotation(towardNpc));
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

        private static void PlaceAt(Passerby npc, Pose pose)
        {
            var body = npc.GetComponent<CharacterController>();
            body.enabled = false;
            npc.transform.SetPositionAndRotation(pose.position, pose.rotation);
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
