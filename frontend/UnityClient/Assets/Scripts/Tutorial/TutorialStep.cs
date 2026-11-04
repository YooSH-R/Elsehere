namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 단계. 마을에 막 도착한 여행자가 숙소에서 조작·사물 단어·단어장을 익히고, 밖으로 나가 Ben의 길 안내를 돕고
    /// 오늘의 목표(퀘스트 카드)를 확인하면 끝난다.
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>숙소 도착 인사. [시작하기]로 넘어간다.</summary>
        Arrival,

        /// <summary>걷기·달리기. 걸은 뒤 [다음으로]로 넘어간다.</summary>
        Walk,

        /// <summary>카메라로 둘러보기. 시점을 돌린 뒤 [다음으로]로 넘어간다.</summary>
        Look,

        /// <summary>사물 단어 카드를 클릭해 고정하고 단어장에 저장하면 넘어간다.</summary>
        SaveWord,

        /// <summary>오른쪽 위 메뉴(≡)에서 단어장을 열면 넘어간다.</summary>
        OpenVocabulary,

        /// <summary>숙소 문이 열리고 밖으로 나가면 넘어간다.</summary>
        LeaveLodging,

        /// <summary>Ben이 다가와 말을 걸면 대화를 시작한다.</summary>
        MeetBen,

        /// <summary>정해진 대사로 Ben에게 카페 가는 길을 알려 준다.</summary>
        BenDialogue,

        /// <summary>길 안내 대화의 피드백을 보고 닫는다.</summary>
        BenFeedback,

        /// <summary>왼쪽 위 퀘스트 카드를 눌러 오늘의 목표와 마을 지도가 담긴 수첩을 열면 끝난다.</summary>
        TodayGoal,

        Completed,
    }
}
