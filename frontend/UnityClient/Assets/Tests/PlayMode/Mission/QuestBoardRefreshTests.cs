#if UNITY_EDITOR
using System.Collections;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Mission.PlayModeTests
{
    /// <summary>
    /// 진행 상태를 받지 못한 채 퀘스트 목록을 열어도 진행 요청을 한 번만 다시 보내는지 확인한다.
    /// 예전에는 진행 요청이 끝날 때마다 열린 목록을 새로 받으면서 진행 요청도 또 보내,
    /// 연결 실패가 곧바로 돌아오면 끝없이 재귀하다 Editor가 죽었다(튜토리얼 PlayMode의 오늘의 목표 사례).
    /// </summary>
    public class QuestBoardRefreshTests
    {
        [UnityTest]
        public IEnumerator OpeningQuestList_WhileProgressFails_AsksForProgressOnlyOnce()
        {
            var previousFactory = ApiSettings.EditorClientFactory;
            var transport = new FailingProgressTransport();
            ApiSettings.EditorClientFactory = _ => new ApiClient("", transport);
            try
            {
                yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
                yield return null;
                int callsBeforeOpening = transport.ProgressCalls;

                Object.FindFirstObjectByType<FirstDayMissionView>().OpenQuestBoard();
                yield return null;

                Assert.That(
                    transport.ProgressCalls - callsBeforeOpening,
                    Is.EqualTo(1),
                    "목록을 열 때 진행을 한 번만 다시 읽습니다."
                );
            }
            finally
            {
                ApiSettings.EditorClientFactory = previousFactory;
            }
        }

        // 모든 요청을 그 자리에서 연결 실패로 돌려준다. 진행 요청이 계속 이어지면 정해 둔 횟수 뒤로는 응답하지 않아
        // 고치기 전 코드도 Editor를 죽이지 않고 실패로 끝나게 한다.
        private sealed class FailingProgressTransport : IApiTransport
        {
            private const int RunawayLimit = 50;

            public int ProgressCalls { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                if (request.Url.EndsWith("/missions/first-day/visit"))
                {
                    ProgressCalls++;
                    if (ProgressCalls > RunawayLimit)
                        return new TaskCompletionSource<ApiTransportResponse>().Task;
                }

                return Task.FromResult(ApiTransportResponse.NetworkFailed());
            }
        }
    }
}
#endif
