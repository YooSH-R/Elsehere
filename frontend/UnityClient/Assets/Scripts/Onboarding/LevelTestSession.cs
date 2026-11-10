using System.Collections.Generic;
using System.Linq;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 04에서 푸는 레벨 테스트 하나(ONB-03 응답). 보이는 문항, 고른 보기·쓴 답, 듣기 횟수, 채점 결과를 담아 화면이 무엇을 보이고
    /// 무엇을 누를 수 있는지 정한다. 서버 호출은 <see cref="OnboardingFlow"/>가 맡는다.
    /// </summary>
    public sealed class LevelTestSession
    {
        /// <summary>듣기 문항은 처음 한 번과 [다시 듣기] 한 번까지 들을 수 있다.</summary>
        public const int ListeningPlayLimit = 2;

        // 결과 레벨(1부터) 순서의 난이도 이름. 화면 05의 단계 이름과 같다.
        private static readonly string[] LevelNames = { "초급", "중급", "고급" };

        private readonly List<LevelTestItem> items;
        private int itemIndex;
        private int listeningPlays;

        public LevelTestSession(LevelTestStartResponse started)
        {
            TestId = started.TestId;
            TotalItems = started.TotalItems;
            items = started.Items;
        }

        public long TestId { get; }

        /// <summary>"문항 n / N"의 N. 서버가 알려 준 전체 문항 수다.</summary>
        public int TotalItems { get; }

        public LevelTestItem CurrentItem => items[itemIndex];
        public bool IsLastItem => itemIndex == items.Count - 1;
        public string ProgressText => $"문항 {itemIndex + 1} / {TotalItems}";

        /// <summary>진행 막대 길이(0~1). 지금 문항까지 채운다.</summary>
        public float Progress => (itemIndex + 1) / (float)TotalItems;

        /// <summary>듣기 문항에서 고른 보기 순서. 고르기 전이면 null이다.</summary>
        public int? ChosenChoice { get; private set; }

        public bool CanPlayListening => listeningPlays < ListeningPlayLimit;

        /// <summary>말하기 문항 답 칸의 글자. 받아 적은 말이나 직접 쓰고 고친 말이다.</summary>
        public string SpokenAnswer { get; private set; } = "";

        /// <summary>듣기는 보기를 골라야, 말하기는 답 칸이 비어 있지 않아야 [다음]을 누를 수 있다.</summary>
        public bool CanGoNext =>
            CurrentItem.Type == LevelTestItemType.Listen
                ? ChosenChoice != null
                : !string.IsNullOrWhiteSpace(SpokenAnswer);

        /// <summary>ONB-04로 보낼 답. 듣기는 고른 보기 문자열, 말하기는 앞뒤 공백을 뺀 답이다.</summary>
        public string Answer =>
            CurrentItem.Type == LevelTestItemType.Listen
                ? CurrentItem.Choices[ChosenChoice.Value]
                : SpokenAnswer.Trim();

        /// <summary>채점 결과(ONB-05). 마지막 문항을 채점하기 전이면 null이다.</summary>
        public LevelTestResultResponse Result { get; private set; }

        public string RecommendedLevelName => LevelNames[Result.ResultLevel.Value - 1];

        /// <summary>"듣기 3 / 4"의 점수 부분. 전체는 받은 듣기 문항 수다.</summary>
        public string ListeningScoreText =>
            $"{Result.ListeningScore} / {CountOf(LevelTestItemType.Listen)}";

        /// <summary>"말하기 2 / 2 통과"의 점수 부분. 전체는 받은 말하기 문항 수다.</summary>
        public string SpeakingScoreText =>
            $"{Result.SpeakingPassed} / {CountOf(LevelTestItemType.Speak)} 통과";

        public void Choose(int choiceIndex)
        {
            ChosenChoice = choiceIndex;
        }

        public void RecordListeningPlay()
        {
            listeningPlays++;
        }

        public void ChangeAnswer(string answer)
        {
            SpokenAnswer = answer ?? "";
        }

        /// <summary>다음 문항을 보인다. 고른 보기·들은 횟수·쓴 답은 문항마다 새로 센다.</summary>
        public void MoveToNextItem()
        {
            itemIndex++;
            ChosenChoice = null;
            listeningPlays = 0;
            SpokenAnswer = "";
        }

        public void Finish(LevelTestResultResponse result)
        {
            Result = result;
        }

        private int CountOf(LevelTestItemType type)
        {
            return items.Count(item => item.Type == type);
        }
    }
}
