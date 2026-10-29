using System.Collections.Generic;
using System.Linq;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 02 가입 정보 입력 상태. 규칙에 맞고 중복 확인을 마친 닉네임, 캐릭터 기본형, 필수 약관 동의가 모두 있어야 제출할 수 있다.
    /// </summary>
    public sealed class SignupForm
    {
        private readonly HashSet<string> agreedConsentTypes = new HashSet<string>();

        public SignupForm(IReadOnlyList<ConsentTerm> terms)
        {
            Terms = terms;
        }

        /// <summary>AUTH-03으로 받은 약관. 동의를 보낼 때 이 버전을 그대로 쓴다.</summary>
        public IReadOnlyList<ConsentTerm> Terms { get; }

        public string Nickname { get; private set; } = "";
        public NicknameStatus NicknameStatus { get; private set; }
        public AvatarType? Avatar { get; private set; }

        public NicknameProblem NicknameProblem => NicknameRule.Check(Nickname);

        public bool CanSubmit =>
            NicknameProblem == NicknameProblem.None
            && NicknameStatus == NicknameStatus.Available
            && Avatar != null
            && Terms.Where(term => term.Required).All(term => IsAgreed(term.ConsentType));

        /// <summary>닉네임을 바꾸면 이전 중복 확인 결과는 쓸 수 없다.</summary>
        public void ChangeNickname(string nickname)
        {
            if (nickname == Nickname)
            {
                return;
            }

            Nickname = nickname;
            NicknameStatus = NicknameStatus.Unchecked;
        }

        /// <summary>중복 확인(AUTH-06) 결과. 확인하는 사이 닉네임을 바꿨으면 늦게 온 결과는 버린다.</summary>
        public void RecordAvailability(string checkedNickname, bool available)
        {
            if (checkedNickname != Nickname)
            {
                return;
            }

            NicknameStatus = available ? NicknameStatus.Available : NicknameStatus.Taken;
        }

        public void SelectAvatar(AvatarType avatar)
        {
            Avatar = avatar;
        }

        public void SetAgreed(string consentType, bool agreed)
        {
            if (agreed)
            {
                agreedConsentTypes.Add(consentType);
            }
            else
            {
                agreedConsentTypes.Remove(consentType);
            }
        }

        public bool IsAgreed(string consentType)
        {
            return agreedConsentTypes.Contains(consentType);
        }

        /// <summary>제출할 가입 정보. 동의한 약관만 서버가 보여 준 버전으로 담는다.</summary>
        public SignupRequest ToRequest()
        {
            return new SignupRequest
            {
                Nickname = Nickname,
                AvatarType = Avatar ?? AvatarType.F,
                Consents = Terms
                    .Where(term => IsAgreed(term.ConsentType))
                    .Select(term => new ConsentAgreement
                    {
                        ConsentType = term.ConsentType,
                        Version = term.Version,
                    })
                    .ToList(),
            };
        }
    }
}
