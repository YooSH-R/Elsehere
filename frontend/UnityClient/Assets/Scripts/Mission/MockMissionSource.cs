using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 서버 연동 전에 쓰는 확인용 미션. 필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "A305/Mission/Mock Missions", fileName = "MockMissions")]
    public class MockMissionSource : MissionSource
    {
        [SerializeField]
        private List<Entry> entries = new List<Entry>
        {
            new Entry(
                "CAFE_SOY_LATTE",
                "나만의 카페라테 주문하기",
                "우유 대신 두유를 넣은 카페라테를 주문해 보세요.",
                "모퉁이 카페",
                "CAFE_STAFF",
                new[] { "soy", "latte" }
            ),
            new Entry(
                "CAFE_DESSERT",
                "달콤한 디저트 찾기",
                "카페 직원에게 디저트를 파는지 물어보세요.",
                "모퉁이 카페",
                "CAFE_STAFF",
                new[] { "dessert" }
            ),
        };

        public override IReadOnlyList<MissionContent> GetMissions()
        {
            var missions = new List<MissionContent>();
            foreach (Entry entry in entries)
            {
                missions.Add(
                    new MissionContent(
                        entry.code,
                        entry.title,
                        entry.description,
                        entry.placeName,
                        entry.npcCode,
                        entry.keywords
                    )
                );
            }

            return missions;
        }

        [Serializable]
        private class Entry
        {
            [Tooltip("미션 키")]
            public string code;

            public string title;
            public string description;
            public string placeName;

            [Tooltip("미션을 완료할 수 있는 대화 상대(ERD npcs.code)")]
            public string npcCode;

            [Tooltip(
                "대화에서 내가 말한 문장에 모두 들어 있어야 하는 키워드. 대소문자를 구분하지 않는다."
            )]
            public string[] keywords;

            public Entry() { }

            public Entry(
                string code,
                string title,
                string description,
                string placeName,
                string npcCode,
                string[] keywords
            )
            {
                this.code = code;
                this.title = title;
                this.description = description;
                this.placeName = placeName;
                this.npcCode = npcCode;
                this.keywords = keywords;
            }
        }
    }
}
