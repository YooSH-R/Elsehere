using System.Collections.Generic;

namespace A305.Onboarding
{
    /// <summary>
    /// ONB-08 언어별 학습 설정. 레벨 테스트를 마치지 않았으면 추천 난이도가, 테스트 기록이 없으면 마지막 테스트가 없다.
    /// </summary>
    public sealed class LanguageProfileResponse
    {
        public string LanguageCode { get; set; }
        public int? RecommendedLevel { get; set; }
        public int? SelectedLevel { get; set; }
        public LevelTestSummary LevelTest { get; set; }
        public List<ConversationLevel> Levels { get; set; }
    }
}
