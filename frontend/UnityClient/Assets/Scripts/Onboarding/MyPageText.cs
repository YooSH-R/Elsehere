namespace A305.Onboarding
{
    /// <summary>마이페이지에 보여 줄 내 정보 문구.</summary>
    public static class MyPageText
    {
        /// <summary>닉네임·캐릭터·배우는 언어를 줄마다 보여 준다.</summary>
        public static string Profile(OnboardingUser user)
        {
            return $"닉네임 · {user.Nickname}\n"
                + $"캐릭터 · {Character(user.AvatarType)}\n"
                + $"배우는 언어 · {Language(user.CurrentLanguage)}";
        }

        // 가입 화면의 캐릭터 버튼 이름과 같게 부른다.
        private static string Character(AvatarType avatarType)
        {
            return avatarType == AvatarType.M ? "남성" : "여성";
        }

        // MVP는 영어만 배우므로 이름을 아는 언어는 영어뿐이고, 그 밖의 언어는 코드를 그대로 보인다.
        private static string Language(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return "아직 고르지 않았어요";
            }

            return languageCode == "en-US" ? "영어" : languageCode;
        }
    }
}
