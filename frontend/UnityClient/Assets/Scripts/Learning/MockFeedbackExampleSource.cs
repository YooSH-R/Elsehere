using System;
using System.Collections.Generic;
using A305.Dialogue;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// 서버·AI 연동 전에 쓰는 확인용 참고 표현 규칙. 위에서부터 확인해 발화에 키워드가 모두 들어 있는 첫 규칙을 쓴다.
    /// 필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 한다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "A305/Learning/Mock Feedback Examples",
        fileName = "MockFeedbackExamples"
    )]
    public class MockFeedbackExampleSource : FeedbackExampleSource
    {
        private const string PoliteRequestReason =
            "Could I have…?는 카페에서 원하는 것을 정중하게 요청할 때 쓸 수 있어요.";

        [SerializeField]
        private List<Entry> entries = new List<Entry>
        {
            new Entry(
                new[] { "soy", "latte" },
                "Could I have a soy latte, please?",
                "두유 카페라테 한 잔 주시겠어요?",
                PoliteRequestReason
            ),
            new Entry(
                new[] { "latte" },
                "Could I have a latte, please?",
                "라테 한 잔 주시겠어요?",
                PoliteRequestReason
            ),
            new Entry(
                new[] { "coffee" },
                "Could I have an iced americano, please?",
                "아이스 아메리카노 한 잔 주시겠어요?",
                PoliteRequestReason
            ),
            new Entry(
                new[] { "dessert" },
                "Do you have any desserts?",
                "디저트 있나요?",
                "Do you have any…?로 가게에 원하는 것이 있는지 자연스럽게 물어볼 수 있어요."
            ),
            new Entry(
                new[] { "straight" },
                "Go straight and turn left.",
                "곧장 가서 왼쪽으로 도세요.",
                "길을 안내할 때는 동작을 순서대로 이어 말하면 이해하기 쉬워요."
            ),
            new Entry(
                new[] { "weather" },
                "It's a nice day today, isn't it?",
                "오늘 날씨 좋네요, 그렇죠?",
                "isn't it?을 붙이면 상대에게 동의를 구하며 대화를 이어갈 수 있어요."
            ),
        };

        [SerializeField]
        [Tooltip("대본 대화 상대별로 그 장면에서 현지인이 자주 쓰는 관용·추천 표현")]
        private List<SituationEntry> situations = new List<SituationEntry>
        {
            new SituationEntry(
                "Ben",
                "I'd like to check in.",
                "체크인하고 싶어요.",
                "프런트에 도착해 가장 먼저 꺼내는 말이에요.",
                "Hi, I'd like to check in. The name's Kim.",
                "안녕하세요, 체크인하려고요. 김으로 예약했어요."
            ),
            new SituationEntry(
                "Ben",
                "I have a reservation under ...",
                "…(이름)으로 예약했어요.",
                "예약자 이름을 알려 줄 때 under 뒤에 이름을 붙여요.",
                "I have a reservation under Lee.",
                "이 이름으로 예약했어요."
            ),
            new SituationEntry(
                "Ben",
                "What time is checkout?",
                "체크아웃은 몇 시예요?",
                "방 열쇠를 받을 때 함께 물어보면 좋아요.",
                "Thanks. What time is checkout?",
                "고마워요. 체크아웃은 몇 시예요?"
            ),
            new SituationEntry(
                "Ben",
                "Could you hold my bags?",
                "짐 좀 맡아 주시겠어요?",
                "방에 들어가기 전이나 나간 뒤 짐을 맡길 때 써요.",
                "Could you hold my bags until three?",
                "세 시까지 짐 좀 맡아 주시겠어요?"
            ),
        };

        public override IReadOnlyList<SituationExpressionData> GetSituationExpressions(
            string npcName
        )
        {
            var found = new List<SituationExpressionData>();
            foreach (SituationEntry entry in situations)
            {
                if (string.Equals(entry.npcName, npcName, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(
                        new SituationExpressionData
                        {
                            Expression = entry.expression,
                            MeaningKo = entry.meaningKo,
                            UsageKo = entry.usageKo,
                            ExampleEn = entry.exampleEn,
                            ExampleKo = entry.exampleKo,
                        }
                    );
                }
            }

            return found;
        }

        public override IReadOnlyList<ReferenceRule> GetRules()
        {
            var rules = new List<ReferenceRule>();
            foreach (Entry entry in entries)
            {
                rules.Add(
                    new ReferenceRule(
                        entry.keywords,
                        new ReferenceExpression(entry.english, entry.korean, entry.reason)
                    )
                );
            }

            return rules;
        }

        [Serializable]
        private class SituationEntry
        {
            [Tooltip("이 표현을 보여 줄 대본 대화 상대 이름")]
            public string npcName;

            public string expression;
            public string meaningKo;

            [Tooltip("이 장면에서 언제 쓰는지")]
            public string usageKo;

            public string exampleEn;
            public string exampleKo;

            public SituationEntry() { }

            public SituationEntry(
                string npcName,
                string expression,
                string meaningKo,
                string usageKo,
                string exampleEn,
                string exampleKo
            )
            {
                this.npcName = npcName;
                this.expression = expression;
                this.meaningKo = meaningKo;
                this.usageKo = usageKo;
                this.exampleEn = exampleEn;
                this.exampleKo = exampleKo;
            }
        }

        [Serializable]
        private class Entry
        {
            [Tooltip("발화에 모두 들어 있어야 하는 키워드. 대소문자를 구분하지 않는다.")]
            public string[] keywords;

            [Tooltip("참고 표현")]
            public string english;

            public string korean;

            [Tooltip("이 표현을 권하는 이유")]
            public string reason;

            public Entry() { }

            public Entry(string[] keywords, string english, string korean, string reason)
            {
                this.keywords = keywords;
                this.english = english;
                this.korean = korean;
                this.reason = reason;
            }
        }
    }
}
