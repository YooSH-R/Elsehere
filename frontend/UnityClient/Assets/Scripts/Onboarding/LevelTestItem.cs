using System.Collections.Generic;

namespace A305.Onboarding
{
    /// <summary>
    /// ONB-03 레벨 테스트 문항 하나. 듣기 문항은 보기 4개와 말한 사람, 말하기 문항은 말할 정보(<see cref="Cues"/>)가 있다.
    /// 듣기 대사와 정답, 말하기 판정 기준은 서버에만 있다.
    /// </summary>
    public sealed class LevelTestItem
    {
        public string ItemCode { get; set; }
        public LevelTestItemType Type { get; set; }
        public string Instruction { get; set; }

        /// <summary>듣기는 화면의 물음, 말하기는 상황 카드 문장</summary>
        public string Question { get; set; }

        /// <summary>듣기 문항의 한국어 보기. 말하기 문항은 빈 목록이다.</summary>
        public List<string> Choices { get; set; }

        /// <summary>듣기 문항의 말한 사람("Gus · 빨래방 주인"). 말하기 문항이나 서버가 주지 않으면 null이다.</summary>
        public string Speaker { get; set; }

        /// <summary>말하기 문항의 말할 정보. 듣기 문항은 빈 목록이다.</summary>
        public List<string> Cues { get; set; }
    }
}
