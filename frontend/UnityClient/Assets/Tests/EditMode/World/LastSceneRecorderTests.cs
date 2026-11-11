using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.World.Tests
{
    public class LastSceneRecorderTests
    {
        [TestCase("HomeScene", "HOME")]
        [TestCase("TownScene", "TOWN")]
        [TestCase("CafeScene", "CAFE")]
        [TestCase("HotelScene", "HOTEL")]
        [TestCase("ShopScene", "SHOP")]
        [TestCase("PoliceStationScene", "POLICE_STATION")]
        public async Task RecordAsync_PlaceScene_SavesItsSceneCode(
            string unitySceneName,
            string sceneCode
        )
        {
            var api = new FakeLastSceneApi();
            var recorder = new LastSceneRecorder(api);

            LastSceneRecordStatus status = await recorder.RecordAsync(unitySceneName);

            Assert.That(status, Is.EqualTo(LastSceneRecordStatus.Saved));
            Assert.That(api.SavedSceneCodes, Is.EqualTo(new[] { sceneCode }));
        }

        // 서버는 마을 매장을 마을의 일부로 보므로 매장 실내 씬에서는 마을을 재접속 위치로 저장한다.
        [TestCase("PostOfficeInterior")]
        [TestCase("LittleArcadeUpperInterior")]
        public async Task RecordAsync_TownShopScene_SavesTheTown(string unitySceneName)
        {
            var api = new FakeLastSceneApi();
            var recorder = new LastSceneRecorder(api);

            LastSceneRecordStatus status = await recorder.RecordAsync(unitySceneName);

            Assert.That(status, Is.EqualTo(LastSceneRecordStatus.Saved));
            Assert.That(api.SavedSceneCodes, Is.EqualTo(new[] { "TOWN" }));
        }

        [TestCase("EntryScene")]
        [TestCase("SampleScene")]
        [TestCase("")]
        [TestCase(null)]
        public async Task RecordAsync_NotAPlace_DoesNotCallServer(string unitySceneName)
        {
            var api = new FakeLastSceneApi();
            var recorder = new LastSceneRecorder(api);

            LastSceneRecordStatus status = await recorder.RecordAsync(unitySceneName);

            Assert.That(status, Is.EqualTo(LastSceneRecordStatus.NotAPlace));
            Assert.That(api.SavedSceneCodes, Is.Empty);
        }

        [TestCase(ApiErrorCodes.NetworkError, 0)]
        [TestCase("SCENE_CLOSED", 409)]
        public async Task RecordAsync_ServerFails_ReturnsFailureWithoutThrowing(
            string errorCode,
            long statusCode
        )
        {
            var api = new FakeLastSceneApi
            {
                Failure = new ApiException(errorCode, statusCode, "저장 실패"),
            };
            var recorder = new LastSceneRecorder(api);

            LastSceneRecordStatus status = await recorder.RecordAsync("CafeScene");

            Assert.That(status, Is.EqualTo(LastSceneRecordStatus.Failed));
            Assert.That(recorder.LastError.Code, Is.EqualTo(errorCode));
        }

        [Test]
        public async Task SaveAsync_SendsSceneCodeWithPut()
        {
            var transport = new RecordingTransport();
            var api = new LastSceneApi(new ApiClient("", transport));

            await api.SaveAsync("CAFE");

            Assert.That(transport.Request.Method, Is.EqualTo("PUT"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/me/last-scene"));
            Assert.That(transport.Request.JsonBody, Is.EqualTo("{\"sceneCode\":\"CAFE\"}"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        private sealed class FakeLastSceneApi : ILastSceneApi
        {
            public List<string> SavedSceneCodes { get; } = new List<string>();

            public ApiException Failure { get; set; }

            public Task SaveAsync(string sceneCode)
            {
                if (Failure != null)
                {
                    return Task.FromException(Failure);
                }

                SavedSceneCodes.Add(sceneCode);
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(
                    ApiTransportResponse.Received(200, "{\"sceneCode\":\"CAFE\"}")
                );
            }
        }
    }
}
