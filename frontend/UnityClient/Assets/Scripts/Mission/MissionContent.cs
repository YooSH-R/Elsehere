namespace A305.Mission
{
    /// <summary>미션 하나의 내용. 실제 미션은 서버 데이터이며, 서버 연동 전에는 확인용 미션을 쓴다.</summary>
    public sealed class MissionContent
    {
        public MissionContent(
            string code,
            string title,
            string description,
            string placeName,
            string npcCode,
            string[] keywords
        )
        {
            Code = code;
            Title = title;
            Description = description;
            PlaceName = placeName;
            NpcCode = npcCode;
            Keywords = keywords;
        }

        public string Code { get; }
        public string Title { get; }
        public string Description { get; }
        public string PlaceName { get; }

        /// <summary>미션을 완료할 수 있는 대화 상대(ERD npcs.code)</summary>
        public string NpcCode { get; }

        /// <summary>대화에서 내가 말한 문장에 모두 들어 있으면 미션을 완료한다.</summary>
        public string[] Keywords { get; }
    }
}
