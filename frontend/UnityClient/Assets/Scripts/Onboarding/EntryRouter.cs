namespace A305.Onboarding
{
    /// <summary>
    /// 접속 상태(BOOT-01)로 첫 화면을 정한다. 로그인하지 않았으면 BOOT-01이 401이므로 로그인 화면은 호출하는 쪽이 정한다.
    /// </summary>
    public static class EntryRouter
    {
        /// <param name="lastLevelTest">ONB-08의 마지막 레벨 테스트 상태. 레벨 테스트 단계에서만 쓰며, 기록이 없으면 null이다.</param>
        public static EntryScreen Route(BootstrapResponse boot, LevelTestStatus? lastLevelTest)
        {
            // 가입은 약관 동의부터 받고, 동의하면 가입 정보(닉네임·캐릭터)로 넘어간다.
            if (boot.AuthState == AuthState.SignupRequired)
            {
                return EntryScreen.Terms;
            }

            switch (boot.User.OnboardingStep)
            {
                case OnboardingStep.Language:
                    return EntryScreen.Language;
                case OnboardingStep.Level:
                    return RouteLevelStep(lastLevelTest);
                case OnboardingStep.Tutorial:
                    return EntryScreen.Tutorial;
                default:
                    return EntryScreen.World;
            }
        }

        // 테스트를 끝냈거나(완료·그만하기·건너뛰기) 하면 난이도 설정으로, 기록이 없거나 푸는 도중 나갔으면 레벨 테스트를 처음부터 다시 보여 준다.
        private static EntryScreen RouteLevelStep(LevelTestStatus? lastLevelTest)
        {
            if (lastLevelTest == null || lastLevelTest == LevelTestStatus.InProgress)
            {
                return EntryScreen.LevelTest;
            }

            return EntryScreen.LevelSetting;
        }
    }
}
