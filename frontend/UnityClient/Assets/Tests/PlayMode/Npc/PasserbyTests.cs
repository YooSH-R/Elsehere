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
    /// <summary>마을(TownScene)을 실제로 불러와 행인이 먼저 다가와 대화를 제안하고, 수락·거절·무시가 이어지는지 확인한다.</summary>
    public class PasserbyTests
    {
        private const string TutorialPasserbyCode = "TUTORIAL_PASSERBY";
        private const string TownPasserbyCode = "TOWN_PASSERBY";
        private const float WaitTimeoutSeconds = 15f;

        [SetUp]
        public void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
            // 튜토리얼 행인은 튜토리얼의 이웃 만나기 단계에서만 다가오므로, 튜토리얼도 처음부터 시작한다.
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator LeavingHome_TutorialPasserbyApproachesAndOffersWithoutDecline()
        {
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            Assert.That(SpaceTravel.TryTravel("TownScene", "HomeDoor"), Is.True);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TownScene" && !SpaceTravel.IsTraveling);

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);

            Passerby tutorial = FindPasserby(TutorialPasserbyCode);
            Assert.That(tutorial.Phase, Is.EqualTo(PasserbyPhase.Offering));
            Assert.That(offer.IsDeclineShown, Is.False, "튜토리얼 행인은 거절할 수 없습니다.");
            Assert.That(HorizontalDistance(tutorial.transform, Single<PlayerMover>().transform), Is.LessThan(3f));
            Assert.That(Single<PlayerMover>().IsMovementLocked, Is.False, "제안 중에는 이동을 잠그지 않습니다.");
        }

        [UnityTest]
        public IEnumerator AcceptingOffer_EntersDialogueView_AndEndingRestoresControl()
        {
            yield return LoadTownWithPlayerNear(TutorialPasserbyCode);
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
            Assert.That(FindPasserby(TutorialPasserbyCode).Phase, Is.EqualTo(PasserbyPhase.Resting));
        }

        [UnityTest]
        public IEnumerator DecliningTownPasserby_ClosesOffer_AndDoesNotOfferAgainWhileResting()
        {
            yield return LoadTownWithPlayerNear(TownPasserbyCode);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Assert.That(offer.IsDeclineShown, Is.True);

            Click(offer, "DeclineButton");
            yield return new WaitForSeconds(2f);

            Assert.That(offer.IsShowing, Is.False);
            Assert.That(FindPasserby(TownPasserbyCode).Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator WalkingAwayFromOffer_ClosesOfferWithoutLockingPlayer()
        {
            yield return LoadTownWithPlayerNear(TownPasserbyCode);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Passerby passerby = FindPasserby(TownPasserbyCode);

            PlayerMover player = Single<PlayerMover>();
            player.Teleport(passerby.transform.position + new Vector3(0f, 0f, -12f), Quaternion.identity);
            yield return null;
            yield return null;

            Assert.That(offer.IsShowing, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(player.IsMovementLocked, Is.False);
            Assert.That(Single<PlayerCameraRig>().ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
        }

        /// <summary>마을을 불러오고 다른 행인이 끼어들지 않게 한 뒤, 플레이어를 해당 행인 앞에 세운다.</summary>
        private static IEnumerator LoadTownWithPlayerNear(string passerbyCode)
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;

            Passerby passerby = FindPasserby(passerbyCode);
            PlayerMover player = Single<PlayerMover>();
            player.Teleport(passerby.transform.position + passerby.transform.forward * 4f, Quaternion.identity);
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
            Button button = view
                .GetComponentsInChildren<Button>(true)
                .Single(candidate => candidate.name == buttonName);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, $"{buttonName}이 보이지 않습니다.");
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
