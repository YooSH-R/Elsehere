namespace A305.Learning
{
    /// <summary>대화 표현을 나누는 장소 묶음. 서버(VOCAB-01 source.placeGroup)가 NPC가 일하는 곳으로 정한다.</summary>
    public enum VocabularyPlaceGroup
    {
        /// <summary>카페, 식당, 빵집</summary>
        Food,

        /// <summary>드럭스토어, 꽃집, 생활용품점, 오락실</summary>
        Shopping,

        /// <summary>은행, 우체국, 빨래방, 이발소, 호텔, 택시</summary>
        Services,

        /// <summary>경찰서</summary>
        Safety,

        /// <summary>거리에서 만난 사람, 근무 시간 밖에 만난 NPC</summary>
        Daily,
    }
}
