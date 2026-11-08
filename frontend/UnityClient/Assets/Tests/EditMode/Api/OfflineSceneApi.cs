using System;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(A305.Api.Tests.OfflineSceneApi))]

namespace A305.Api.Tests
{
    /// <summary>
    /// PlayMode 씬이 만드는 기본 API만 연결 실패로 대체한다. 실행 중인 로컬 서버와 로그인 쿠키가 테스트 결과를 바꾸지 않게 한다.
    /// 성공·거절·시간 초과를 다루는 개별 테스트는 필요한 API 또는 전송을 직접 주입한다.
    /// </summary>
    public sealed class OfflineSceneApi : ITestRunCallback
    {
        private readonly OfflineTransport transport = new OfflineTransport();

        public void RunStarted(ITest testsToRun) => UseOfflineClient();

        public void TestStarted(ITest test) => UseOfflineClient();

        public void TestFinished(ITestResult result) { }

        public void RunFinished(ITestResult testResults) => ApiSettings.EditorClientFactory = null;

        private void UseOfflineClient()
        {
            if (Application.isPlaying)
                ApiSettings.EditorClientFactory = _ => new ApiClient("", transport, transport);
        }

        private sealed class OfflineTransport : IApiTransport, IApiStreamTransport
        {
            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request) =>
                Task.FromResult(ApiTransportResponse.NetworkFailed());

            public Task<ApiTransportResponse> SendStreamAsync(
                ApiTransportRequest request,
                Action<string> onText
            ) => Task.FromResult(ApiTransportResponse.NetworkFailed());
        }
    }
}
