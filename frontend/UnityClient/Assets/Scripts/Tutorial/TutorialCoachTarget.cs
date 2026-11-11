namespace A305.Tutorial
{
    /// <summary>코치 마크가 밝히는 화면 부분이나 가리키는 장면 속 대상.</summary>
    public enum TutorialCoachTarget
    {
        None,

        /// <summary>안내 카드. 도착 인사와, 할 일을 마친 뒤 [다음으로]를 누를 때 밝힌다.</summary>
        GuideCard,

        /// <summary>고정한 사물 카드의 [저장]</summary>
        WordSaveButton,

        /// <summary>오른쪽 위 메뉴(≡)</summary>
        MenuButton,

        /// <summary>메뉴 안의 [단어장]</summary>
        VocabularyButton,

        /// <summary>단어장 화면. 단어장을 처음 열었을 때 무엇을 하는 곳인지와 다음 할 일을 설명과 함께 알린다.</summary>
        Vocabulary,

        /// <summary>숙소 문(장면 속 표시)</summary>
        Door,

        /// <summary>Ben(머리 위 표시)</summary>
        Ben,

        /// <summary>Ben의 대화 제안 [대화하기]</summary>
        TalkOffer,

        /// <summary>대화 화면의 읽을 대사와 입력칸</summary>
        GuidedLine,

        /// <summary>대화 화면의 대사 칸. 대화 시작 티칭의 첫째</summary>
        DialogueLines,

        /// <summary>대화 화면의 [뜻]·[힌트]</summary>
        DialogueTools,

        /// <summary>대화 화면 왼쪽의 추천 단어 카드</summary>
        SuggestedWords,

        /// <summary>대화 화면의 [대화 종료]</summary>
        DialogueEnd,

        /// <summary>대화 노트(피드백)의 탭 줄. 피드백 티칭의 첫째</summary>
        FeedbackSections,

        /// <summary>대화 노트 본문의 스크롤 영역</summary>
        FeedbackPages,

        /// <summary>대화 노트를 닫는 오른쪽 위 X</summary>
        FeedbackClose,

        /// <summary>왼쪽 위 퀘스트 카드. 누르면 여행 수첩이 열린다.</summary>
        QuestCard,

        /// <summary>NPC 이름 옆에 표시되는 호감도와 무례 경고 안내.</summary>
        DialogueRelationship,
    }
}
