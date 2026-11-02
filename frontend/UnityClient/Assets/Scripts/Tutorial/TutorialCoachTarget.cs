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

        /// <summary>대화 화면의 [대화 종료]</summary>
        DialogueEnd,

        /// <summary>피드백 화면</summary>
        Feedback,

        /// <summary>왼쪽 위 지도 버튼</summary>
        MapButton,
    }
}
