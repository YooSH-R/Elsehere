using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    // 화면 05에 들어왔을 때 미리 골라 둘 난이도. 고른 적이 없으면 레벨 테스트 추천 난이도를 먼저 고른다.
    public class LevelSettingPreselectionTests
    {
        [TestCase(null, 2, 2, TestName = "추천 난이도를 미리 고른다")]
        [TestCase(1, 2, 1, TestName = "고른 난이도가 있으면 추천보다 먼저다")]
        [TestCase(null, 3, 1, TestName = "추천 난이도를 고를 수 없으면 고를 수 있는 첫 단계다")]
        public void InitialLevel_PrefersSelectedThenRecommendedThenFirstAvailable(
            int? selectedLevel,
            int? recommendedLevel,
            int expected
        )
        {
            var profile = new LanguageProfileResponse
            {
                SelectedLevel = selectedLevel,
                RecommendedLevel = recommendedLevel,
                Levels = new List<ConversationLevel>
                {
                    new ConversationLevel { Level = 1, Available = true },
                    new ConversationLevel { Level = 2, Available = true },
                    new ConversationLevel { Level = 3, Available = false },
                },
            };

            Assert.That(LevelSettingPanelView.InitialLevel(profile), Is.EqualTo(expected));
        }
    }
}
