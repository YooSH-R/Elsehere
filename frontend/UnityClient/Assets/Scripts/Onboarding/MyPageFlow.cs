using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Player;

namespace A305.Onboarding
{
    /// <summary>
    /// 마이페이지의 내 정보, 닉네임 변경, 선택 동의, 대화 난이도. 열 때 BOOT-01로 내 정보를, USER-02로 바꿀 수 있는 선택 동의를,
    /// ONB-08로 대화 난이도를 읽는다. 닉네임은 가입과 같은 규칙으로 먼저 확인한 뒤 USER-04로, 동의는 USER-03으로, 난이도는 ONB-09로 바꾼다. 결과와 실패 이유는 <see cref="Message"/>로 알린다.
    /// </summary>
    public sealed class MyPageFlow
    {
        private const string LoadFailedMessage = "내 정보를 불러오지 못했어요.";
        private const string GuestNicknameMessage = "게스트는 닉네임을 바꿀 수 없어요.";
        private const string VoiceConsentType = "VOICE";

        private readonly IMyPageApi api;
        private List<ConsentStatus> consents = new List<ConsentStatus>();
        private List<ConversationLevel> levels = new List<ConversationLevel>();

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
                PlayerNickname.Set(User.Nickname);

                consents = await api.GetConsentsAsync();
                if (!string.IsNullOrEmpty(User.CurrentLanguage))
                {
                    LanguageProfileResponse profile = await api.GetLanguageProfileAsync(
                        User.CurrentLanguage
                    );
                    levels = profile.Levels ?? new List<ConversationLevel>();
                    SelectedLevel = profile.SelectedLevel;
                }
                // 게스트 닉네임은 서버가 정하므로, 바꾸는 칸을 막은 이유를 열자마자 알린다.
                if (User.Guest)
                {
                    Message = GuestNicknameMessage;
                }
            });
        }

        /// <summary>이 사용자가 바꿀 수 있는 선택 동의. 읽기 전이나 읽지 못했으면 비어 있다.</summary>
        public IReadOnlyList<ConsentStatus> Consents => consents;

        /// <summary>
        /// [회원 탈퇴]를 보일지. 게스트는 지울 계정이 없고 내용도 하루 뒤 정리되므로 보이지 않는다.
        /// 게스트인지 알기 전(읽기 전·읽지 못함)에도 보이지 않는다.
        /// </summary>
        public bool CanWithdraw => User != null && !User.Guest;

        /// <summary>닉네임을 바꿀 수 있는지. 게스트와 내 정보를 읽기 전에는 바꾸지 못한다.</summary>
        public bool CanChangeNickname => User != null && !User.Guest;

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

        /// <summary>배우는 언어의 대화 난이도 단계. 읽기 전이나 읽지 못했으면 비어 있다.</summary>
        public IReadOnlyList<ConversationLevel> Levels => levels;

        /// <summary>지금 쓰는 대화 난이도. 읽기 전이나 아직 고르지 않았으면 null.</summary>
        public int? SelectedLevel { get; private set; }

        /// <summary>
        /// 대화 난이도를 바꾼다(ONB-09). 대화는 시작할 때 난이도를 정하므로 다음 대화부터 적용된다.
        /// 아직 열리지 않은 단계는 보내지 않고, 서버가 거절하면 이전 난이도를 그대로 두고 이유를 알린다.
        /// </summary>
        /// <returns>바꿨으면 true</returns>
        public async Task<bool> ChangeLevelAsync(int level)
        {
            ConversationLevel target = levels.Find(candidate => candidate.Level == level);
            if (User == null || IsBusy || target == null || !target.Available)
            {
                return false;
            }

            if (level == SelectedLevel)
            {
                Message = "지금 쓰는 난이도예요.";
                return false;
            }

            bool changed = false;
            await RunAsync(async () =>
            {
                LevelSelectionResponse saved = await api.SelectLevelAsync(
                    User.CurrentLanguage,
                    level
                );
                SelectedLevel = saved.SelectedLevel;
                Message = $"{target.Name}으로 바꿨어요. 다음 대화부터 적용돼요.";
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

            if (User.Guest)
            {
                Message = GuestNicknameMessage;
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
                // 폰 메뉴 날짜 줄도 바뀐 닉네임을 보인다.
                PlayerNickname.Set(saved.Nickname);
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
