using UnityEngine;

namespace A305.Onboarding
{
    /// <summary>입구 화면이 끝난 뒤 갈 공간 씬과 개발용 로그인 사용자.</summary>
    [CreateAssetMenu(
        fileName = "OnboardingSettings",
        menuName = "A305/Onboarding/Onboarding Settings"
    )]
    public class OnboardingSettings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("난이도 설정을 마치면(튜토리얼 단계) 여는 씬")]
        private string tutorialSceneName = "HomeScene";

        [SerializeField]
        [Tooltip("초기 설정을 마친 사용자가 들어가는 씬")]
        private string worldSceneName = "TownScene";

        [SerializeField]
        [Min(1)]
        [Tooltip("Editor·개발 빌드의 [개발용 로그인]이 로그인할 users.id")]
        private long devLoginUserId = 1;

        public string TutorialSceneName => tutorialSceneName;
        public string WorldSceneName => worldSceneName;
        public long DevLoginUserId => devLoginUserId;
    }
}
