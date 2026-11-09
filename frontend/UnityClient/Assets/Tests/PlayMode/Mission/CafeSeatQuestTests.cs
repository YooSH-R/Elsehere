using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Api;
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
    public class CafeSeatQuestTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator EveryCafeChairAndSofaCompletesTheOrderedRestAfterFiveSeconds()
        {
            string previousMissionNpc = NpcDuty.MissionNpcCode;
            SceneManager.sceneLoaded += PrepareOfflineScene;
            try
            {
                yield return SceneManager.LoadSceneAsync("CafeScene");
                yield return null;
                var player = Object.FindFirstObjectByType<PlayerMover>();
                var pose = player.GetComponent<PlayerSeatPose>();
                var offer = Object.FindFirstObjectByType<PasserbyOfferView>();
                var mission = Object.FindFirstObjectByType<FirstDayMissionController>();
                var transport = new RestTransport();
                SetField(mission, "api", new FirstDayMissionApi(new ApiClient("", transport)));
                RestSeat[] seats = Object.FindObjectsByType<RestSeat>(FindObjectsSortMode.None);
                Assert.That(
                    seats,
                    Has.Length.EqualTo(10),
                    "의자 6개와 소파 2개의 두 자리씩입니다."
                );
                FollowUpStore.Shared.Clear();

                foreach (RestSeat seat in seats.OrderBy(item => item.transform.position.x))
                {
                    var ordered = new FirstDayMissionState
                    {
                        Started = true,
                        Stage = 1,
                        CanRest = true,
                        Scene = "CAFE",
                    };
                    SetField(mission, "state", ordered);
                    foreach (RestSeat cafeSeat in seats)
                        cafeSeat.SetAvailable(true, mealWait: true);
                    int completedBefore = transport.RestRequests;

                    // 의자 앞은 탁자 안쪽이므로 옆 통로에서 접근한다. 긴 소파는 앞 통로를 쓴다.
                    bool isSofa = seat.transform.parent.name == "Bench";
                    Vector3 approach = isSofa
                        ? seat.transform.forward * .8f
                        : seat.transform.right * .8f;
                    player.Teleport(
                        seat.transform.position + approach + Vector3.up * .05f,
                        seat.transform.rotation
                    );
                    player.SetMovementLocked(false);
                    yield return null;
                    yield return null;
                    Assert.That(offer.IsTalkPromptShownBy(seat), Is.True, seat.name + " 앉기 제안");
                    offer.ShownButtonRect.GetComponent<Button>().onClick.Invoke();
                    Assert.That(pose.IsSeated, Is.True, seat.name + " 실제 착석");
                    Assert.That(seat.IsPlayerSeated, Is.True);
                    Assert.That(seat.RemainingSeconds, Is.EqualTo(5f).Within(.1f));
                    yield return new WaitForSecondsRealtime(4f);
                    Assert.That(
                        transport.RestRequests,
                        Is.EqualTo(completedBefore),
                        "5초 전에는 완료하지 않습니다."
                    );
                    // 캡처의 GPU 대기 시간이 4초 검증에 더해지지 않도록 시간 검증 뒤에 찍는다.
                    CaptureIfRequested(
                        player.CameraRig,
                        isSofa ? "sofa"
                            : seat.transform.position.x < 0 ? "left-chair"
                            : "right-chair"
                    );
                    float deadline = Time.realtimeSinceStartup + 2f;
                    while (seat.IsPlayerSeated && Time.realtimeSinceStartup < deadline)
                        yield return null;

                    Assert.That(pose.IsSeated, Is.False, seat.name + " 휴식 뒤 일어서기");
                    Assert.That(
                        transport.RestRequests,
                        Is.EqualTo(completedBefore + 1),
                        seat.name + " 퀘스트 휴식 요청"
                    );
                    Assert.That(
                        transport.LastBody,
                        Is.Null,
                        "좌석에 관계없이 같은 휴식 API를 호출합니다."
                    );
                    yield return null;
                    Assert.That(
                        transport.RestRequests,
                        Is.EqualTo(completedBefore + 1),
                        "완료 요청은 한 번입니다."
                    );
                }
            }
            finally
            {
                SceneManager.sceneLoaded -= PrepareOfflineScene;
                NpcDuty.MissionNpcCode = previousMissionNpc;
            }
        }

        private static void PrepareOfflineScene(Scene scene, LoadSceneMode mode)
        {
            // 실제 씬 연결은 사용하되 첫 프레임의 서버 호출과 NPC의 자리 선점을 막는다.
            foreach (
                MonoBehaviour component in Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (component is FirstDayMissionController mission)
                {
                    mission.enabled = false;
                    SetField(mission, "followUpApi", null);
                }
                if (
                    component is LearningController
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
                    SetField(arrival, "apiSettings", null);
                if (component is DialogueController dialogue)
                {
                    SetField(dialogue, "conversationApi", null);
                    SetField(dialogue, "followUpApi", null);
                }
            }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, Fields).SetValue(target, value);

        private static void CaptureIfRequested(PlayerCameraRig rig, string furniture)
        {
            string folder = System.Environment.GetEnvironmentVariable("CAFE_SEAT_CAPTURE");
            if (string.IsNullOrEmpty(folder))
                return;
            string path = System.IO.Path.Combine(folder, furniture + ".png");
            if (System.IO.File.Exists(path))
                return;
            Camera camera = rig.GetComponent<Camera>();
            var target = new RenderTexture(1280, 800, 24);
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1.6f;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(pixels);
                Object.Destroy(target);
            }
        }

        private sealed class RestTransport : IApiTransport
        {
            public int RestRequests { get; private set; }
            public string LastBody { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Assert.That(request.Method, Is.EqualTo("POST"));
                Assert.That(request.Url, Is.EqualTo("/api/v1/missions/first-day/rest"));
                RestRequests++;
                LastBody = request.JsonBody;
                return Task.FromResult(
                    ApiTransportResponse.Received(
                        200,
                        "{\"started\":true,\"stage\":2,\"canRest\":false,\"scene\":\"CAFE\"}"
                    )
                );
            }
        }
    }
}
