using System.Threading.Tasks;
using A305.Api;
using A305.Voice;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class VoiceConsentAgreementTests
    {
        private FakeVoiceConsentApi api;
        private VoiceConsent consent;
        private VoiceConsentAgreement agreement;

        [SetUp]
        public void CreateAgreement()
        {
            api = new FakeVoiceConsentApi();
            consent = new VoiceConsent();
            agreement = new VoiceConsentAgreement(api, consent);
        }

        [Test]
        public async Task Agree_SavesConsentAndTurnsVoiceInputOn()
        {
            bool saved = await agreement.AgreeAsync();

            Assert.That(saved, Is.True);
            Assert.That(api.Calls, Is.EqualTo(1));
            Assert.That(consent.IsAgreed, Is.True);
            Assert.That(agreement.Message, Is.Null);
            Assert.That(agreement.IsBusy, Is.False);
        }

        [Test]
        public async Task Agree_WhenServerFails_KeepsVoiceInputOffAndExplains()
        {
            api.Error = new ApiException("NETWORK_ERROR", 0, "연결이 끊겼어요.");

            bool saved = await agreement.AgreeAsync();

            Assert.That(saved, Is.False);
            Assert.That(consent.IsAgreed, Is.False);
            Assert.That(agreement.Message, Is.EqualTo(VoiceConsentAgreement.SaveFailedMessage));
            Assert.That(agreement.IsBusy, Is.False);
        }

        [Test]
        public async Task Agree_AfterFailure_CanTryAgain()
        {
            api.Error = new ApiException("NETWORK_ERROR", 0, "연결이 끊겼어요.");
            await agreement.AgreeAsync();
            api.Error = null;

            bool saved = await agreement.AgreeAsync();

            Assert.That(saved, Is.True);
            Assert.That(consent.IsAgreed, Is.True);
            Assert.That(agreement.Message, Is.Null);
        }

        [Test]
        public async Task Agree_PressedAgainWhileSaving_SendsOnlyOnce()
        {
            var pending = new TaskCompletionSource<bool>();
            api.Pending = pending.Task;

            Task<bool> first = agreement.AgreeAsync();
            bool second = await agreement.AgreeAsync();
            pending.SetResult(true);
            bool firstSaved = await first;

            Assert.That(second, Is.False);
            Assert.That(firstSaved, Is.True);
            Assert.That(api.Calls, Is.EqualTo(1));
        }

        private sealed class FakeVoiceConsentApi : IVoiceConsentApi
        {
            public int Calls { get; private set; }
            public ApiException Error { get; set; }
            public Task Pending { get; set; }

            public async Task AgreeAsync()
            {
                Calls++;
                if (Pending != null)
                {
                    await Pending;
                }

                if (Error != null)
                {
                    throw Error;
                }
            }
        }
    }
}
