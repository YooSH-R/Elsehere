using System;
using System.Collections.Generic;
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
