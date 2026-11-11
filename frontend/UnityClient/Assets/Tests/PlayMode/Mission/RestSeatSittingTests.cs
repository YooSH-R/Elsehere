using System.Collections;
using System.Linq;
using System.Reflection;
using A305.Dialogue;
using A305.Learning;
using A305.Npc;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.Mission.PlayModeTests
{
    /// <summary>
    /// 카페 밖 매장 의자에도 카페 의자와 같은 조작으로 앉고 일어나며, NPC가 앉은 자리는 플레이어에게 권하지 않는다.
    /// 의자·소파·스툴은 모두 같은 <see cref="RestSeat"/>를 쓰므로 미용실 대기 의자 하나로 확인한다.
    /// </summary>
    public class RestSeatSittingTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string BarberShop = "MainStreetBarberInterior";

        [UnityTest]
        public IEnumerator WaitingChair_SitsFacingTheSeatAndStandsBackAfterRest()
        {
            SceneManager.sceneLoaded += PrepareOfflineScene;
            GameObject npc = null;
            try
            {
                yield return SceneManager.LoadSceneAsync(BarberShop);
                yield return null;
                // 씬을 불러온 뒤에 만들어야 단일 씬 로드에 함께 지워지지 않는다.
                npc = new GameObject("NpcStandIn");
                var player = Object.FindFirstObjectByType<PlayerMover>();
                var pose = player.GetComponent<PlayerSeatPose>();
                var offer = Object.FindFirstObjectByType<PasserbyOfferView>();
                RestSeat seat = WaitingChairSeat();
                Vector3 standing = InFrontOf(seat, player);

                player.Teleport(standing, Quaternion.LookRotation(-seat.transform.forward));
                player.SetMovementLocked(false);
                yield return null;
                yield return null;
                Assert.That(offer.IsTalkPromptShownBy(seat), Is.True, "대기 의자 앞에서 앉기 제안");

                offer.ShownButtonRect.GetComponent<Button>().onClick.Invoke();
                Assert.That(pose.IsSeated, Is.True, "실제 착석");
                Assert.That(seat.IsPlayerSeated, Is.True);
                Assert.That(
                    Vector3.Angle(player.transform.forward, seat.transform.forward),
                    Is.LessThan(1f),
                    "앉으면 자리 앞쪽을 봅니다."
                );
                Assert.That(
                    seat.TryTake(npc),
                    Is.False,
                    "플레이어가 앉은 자리에는 NPC가 앉지 않습니다."
                );

                float deadline = Time.realtimeSinceStartup + 4f;
                while (seat.IsPlayerSeated && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(pose.IsSeated, Is.False, "쉰 뒤 일어섭니다.");
                Assert.That(player.IsMovementLocked, Is.False);
                Assert.That(
                    Vector3.Distance(player.transform.position, standing),
                    Is.LessThan(.05f),
                    "앉기 전에 서 있던 곳으로 일어섭니다."
                );
            }
            finally
            {
                SceneManager.sceneLoaded -= PrepareOfflineScene;
                if (npc != null)
                    Object.Destroy(npc);
            }
        }

        [UnityTest]
        public IEnumerator WaitingChair_TakenByNpc_IsNotOfferedUntilReleased()
        {
            SceneManager.sceneLoaded += PrepareOfflineScene;
            GameObject npc = null;
            try
            {
                yield return SceneManager.LoadSceneAsync(BarberShop);
                yield return null;
                // 씬을 불러온 뒤에 만들어야 단일 씬 로드에 함께 지워지지 않는다.
                npc = new GameObject("NpcStandIn");
                var player = Object.FindFirstObjectByType<PlayerMover>();
                var pose = player.GetComponent<PlayerSeatPose>();
                var offer = Object.FindFirstObjectByType<PasserbyOfferView>();
                RestSeat seat = WaitingChairSeat();
                Assert.That(seat.TryTake(npc), Is.True, "빈 자리에는 NPC가 앉습니다.");

                player.Teleport(
                    InFrontOf(seat, player),
                    Quaternion.LookRotation(-seat.transform.forward)
                );
                player.SetMovementLocked(false);
                yield return null;
                yield return null;
                Assert.That(
                    offer.IsTalkPromptShownBy(seat),
                    Is.False,
                    "NPC가 앉은 자리는 권하지 않습니다."
                );
                Assert.That(pose.IsSeated, Is.False);

                seat.Release(npc);
                yield return null;
                yield return null;
                Assert.That(
                    offer.IsTalkPromptShownBy(seat),
                    Is.True,
                    "NPC가 일어나면 다시 권합니다."
                );
            }
            finally
            {
                SceneManager.sceneLoaded -= PrepareOfflineScene;
                if (npc != null)
                    Object.Destroy(npc);
            }
        }

        private static RestSeat WaitingChairSeat()
        {
            RestSeat seat = Object
                .FindObjectsByType<RestSeat>(FindObjectsSortMode.None)
                .Where(item =>
                    item.transform.parent != null && item.transform.parent.name == "CushionChair"
                )
                .OrderBy(item => item.transform.position.z)
                .FirstOrDefault();
            Assert.That(seat, Is.Not.Null, "미용실 대기 의자에 앉기 자리가 있어야 합니다.");
            return seat;
        }

        // 자리 앞 0.8m, 플레이어가 서 있던 바닥 높이
        private static Vector3 InFrontOf(RestSeat seat, PlayerMover player)
        {
            Vector3 standing = seat.transform.position + seat.transform.forward * .8f;
            standing.y = player.transform.position.y;
            return standing;
        }

        private static void PrepareOfflineScene(Scene scene, LoadSceneMode mode)
        {
            // 실제 씬 연결은 쓰되 서버 호출과 시계, 튜토리얼 안내, NPC의 자리 선점을 막는다.
            foreach (
                MonoBehaviour component in Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (
                    component is FirstDayMissionController
                    || component is LearningController
                    || component is GameClockDriver
                    || component is TutorialDirector
                    || component is Passerby
                    || component is NpcRoutine
                    || component is NpcPopulation
                )
                    component.enabled = false;
                if (component is TutorialGuideView guide)
                    (
                        (GameObject)
                            typeof(TutorialGuideView).GetField("panel", Fields).GetValue(guide)
                    ).SetActive(false);
                if (component is ControlsGuideView controls)
                    controls.SetBlocked(true);
                if (component is SpaceArrival arrival)
                    typeof(SpaceArrival).GetField("apiSettings", Fields).SetValue(arrival, null);
                if (component is DialogueController dialogue)
                {
                    typeof(DialogueController)
                        .GetField("conversationApi", Fields)
                        .SetValue(dialogue, null);
                    typeof(DialogueController)
                        .GetField("followUpApi", Fields)
                        .SetValue(dialogue, null);
                }
            }
        }
    }
}
