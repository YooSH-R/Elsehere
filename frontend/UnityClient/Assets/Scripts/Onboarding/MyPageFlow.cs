using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Onboarding
{
    /// <summary>
    /// 마이페이지의 내 정보, 닉네임 변경, 선택 동의. 열 때 BOOT-01로 내 정보를, USER-02로 바꿀 수 있는 선택 동의를 읽는다.
    /// 닉네임은 가입과 같은 규칙으로 먼저 확인한 뒤 USER-04로, 동의는 USER-03으로 바꾼다. 결과와 실패 이유는 <see cref="Message"/>로 알린다.
    /// </summary>
    public sealed class MyPageFlow
    {
        private const string LoadFailedMessage = "내 정보를 불러오지 못했어요.";
        private const string VoiceConsentType = "VOICE";

        private readonly IMyPageApi api;
        private List<ConsentStatus> consents = new List<ConsentStatus>();

        public MyPageFlow(IMyPageApi api)
        {
            this.api = api;
        }

        /// <summary>읽어 온 내 정보. 읽기 전이나 읽지 못했으면 null.</summary>
        public OnboardingUser User { get; private set; }

        /// <summary>요청을 기다리는 중</summary>
        public bool IsBusy { get; private set; }

        /// <summary>마지막 동작의 결과나 실패 이유. 없으면 null.</summary>
        public string Message { get; private set; }

        /// <summary>마이페이지를 열 때마다 다시 읽어 다른 기기에서 바꾼 내용도 보인다.</summary>
        public async Task LoadAsync()
        {
            await RunAsync(async () =>
            {
                BootstrapResponse boot = await api.GetBootstrapAsync();
                User = boot.User;
                if (User == null)
                {
                    Message = LoadFailedMessage;
                    return;
                }

                consents = await api.GetConsentsAsync();
            });
        }

        /// <summary>이 사용자가 바꿀 수 있는 선택 동의. 읽기 전이나 읽지 못했으면 비어 있다.</summary>
        public IReadOnlyList<ConsentStatus> Consents => consents;

        /// <summary>음성 처리에 동의했는지. 대화의 음성 입력에 반영한다.</summary>
        public bool IsVoiceAgreed =>
            consents.Find(consent => consent.ConsentType == VoiceConsentType)?.Agreed
            ?? User?.VoiceConsent
            ?? false;

        /// <summary>선택 동의 하나를 켜거나 끈다. 서버가 거절하면 이전 상태를 그대로 두고 이유를 알린다.</summary>
        /// <returns>바꿨으면 true</returns>
        public async Task<bool> ChangeConsentAsync(string consentType, bool agreed)
        {
            int index = consents.FindIndex(consent => consent.ConsentType == consentType);
            if (index < 0 || IsBusy)
            {
                return false;
            }

            bool changed = false;
            await RunAsync(async () =>
            {
                ConsentStatus saved = await api.ChangeConsentAsync(consentType, agreed);
                consents[index] = saved;
                Message = (saved.Agreed ? "켰어요: " : "껐어요: ") + saved.Title;
                changed = true;
            });
            return changed;
        }

        /// <returns>닉네임을 바꿨으면 true</returns>
        public async Task<bool> ChangeNicknameAsync(string nickname)
        {
            if (User == null || IsBusy)
            {
                return false;
            }

            NicknameProblem problem = NicknameRule.Check(nickname);
            if (problem != NicknameProblem.None)
            {
                Message = NicknameRule.Describe(problem);
                return false;
            }

            if (nickname == User.Nickname)
            {
                Message = "지금 쓰는 닉네임이에요.";
                return false;
            }

            bool changed = false;
            await RunAsync(async () =>
            {
                NicknameChange saved = await api.ChangeNicknameAsync(nickname);
                User.Nickname = saved.Nickname;
                Message = "닉네임을 바꿨어요.";
                changed = true;
            });
            return changed;
        }

        // 서버가 거절하거나 연결하지 못하면 바꾸지 않고 서버가 준 이유를 남긴다.
        private async Task RunAsync(Func<Task> request)
        {
            IsBusy = true;
            Message = null;
            try
            {
                await request();
            }
            catch (ApiException exception)
            {
                Message = exception.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
