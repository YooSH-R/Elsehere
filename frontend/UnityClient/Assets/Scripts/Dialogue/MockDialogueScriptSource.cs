using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버·AI 연동 전에 쓰는 확인용 대화 대본. 튜토리얼 행인은 정해진 길 안내 3턴을, 나머지 NPC는 키워드 응답을 쓴다.
    /// 목록에 없는 NPC는 <see cref="fallback"/> 대본으로 대화한다.
    /// 필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 한다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "A305/Dialogue/Mock Dialogue Scripts",
        fileName = "MockDialogueScripts"
    )]
    public class MockDialogueScriptSource : DialogueScriptSource
    {
        [SerializeField]
        private List<Entry> entries = new List<Entry>
        {
            new Entry
            {
                npcCode = "TUTORIAL_PASSERBY",
                npcName = "Alex",
                roleLabel = "이웃",
                opening = new LineEntry(
                    "Excuse me! Could you tell me how to get to the café?",
                    "실례합니다! 카페에 어떻게 가는지 알려 주실 수 있나요?"
                ),
                guidedTurns = new List<GuidedTurnEntry>
                {
                    new GuidedTurnEntry(
                        "Go straight ahead.",
                        "앞으로 곧장 가세요.",
                        new LineEntry(
                            "Straight ahead? And then where should I go?",
                            "곧장이요? 그다음엔 어디로 가면 되나요?"
                        )
                    ),
                    new GuidedTurnEntry(
                        "Turn left at the corner.",
                        "모퉁이에서 왼쪽으로 도세요.",
                        new LineEntry("Got it. Is it far from here?", "알겠어요. 여기서 먼가요?")
                    ),
                    new GuidedTurnEntry(
                        "No, it's just two minutes away.",
                        "아니요, 2분이면 가요.",
                        new LineEntry(
                            "Thank you so much. You're a lifesaver!",
                            "정말 고마워요. 덕분에 살았어요!"
                        )
                    ),
                },
                defaultReply = new LineEntry("I see!", "그렇군요!"),
            },
            new Entry
            {
                npcCode = "CAFE_STAFF",
                npcName = "Mia",
                roleLabel = "카페 직원",
                opening = new LineEntry(
                    "Hi there! Welcome to our café. What can I get for you?",
                    "안녕하세요! 저희 카페에 오신 걸 환영해요. 무엇을 드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "soy", "latte" },
                        new LineEntry(
                            "One soy latte, coming right up! Would you like anything else?",
                            "두유 라테 한 잔 바로 준비할게요! 더 필요한 건 없으세요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "latte" },
                        new LineEntry(
                            "Sure! Would you like regular milk or soy milk?",
                            "물론이죠! 일반 우유로 드릴까요, 두유로 드릴까요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "dessert" },
                        new LineEntry(
                            "Yes, we have cheesecake and cookies. The first cookie is on the house!",
                            "네, 치즈케이크와 쿠키가 있어요. 첫 번째 쿠키는 서비스예요!"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "coffee" },
                        new LineEntry(
                            "Of course. Hot or iced?",
                            "물론이죠. 따뜻한 걸로 드릴까요, 아이스로 드릴까요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "thank" },
                        new LineEntry(
                            "You're welcome! Enjoy your day.",
                            "천만에요! 좋은 하루 보내세요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "I see. Is there anything else I can help you with?",
                    "그렇군요. 더 도와드릴 일이 있을까요?"
                ),
                exampleSentence = "Could I have a soy latte, please?",
            },
            new Entry
            {
                npcCode = "TOWN_PASSERBY",
                npcName = "Ben",
                roleLabel = "행인",
                opening = new LineEntry(
                    "Hey! Nice to see you. How's your day going?",
                    "안녕하세요! 반가워요. 오늘 하루 어때요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "weather" },
                        new LineEntry(
                            "It's a lovely day. Perfect for a walk!",
                            "날씨가 정말 좋네요. 산책하기 딱이에요!"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "cafe" },
                        new LineEntry(
                            "The corner café has great lattes. You should try one!",
                            "모퉁이 카페 라테가 정말 맛있어요. 한번 마셔 봐요!"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "thank" },
                        new LineEntry("Anytime! See you around.", "언제든지요! 또 봐요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "Oh, really? Tell me more!",
                    "아, 정말요? 더 얘기해 줘요!"
                ),
                exampleSentence = "It's a nice day today, isn't it?",
            },
            new Entry
            {
                npcCode = "HOTEL_STAFF",
                npcName = "Jake",
                roleLabel = "호텔 직원",
                opening = new LineEntry(
                    "Good afternoon! Welcome to Maple House. Are you checking in?",
                    "안녕하세요! 메이플 하우스에 오신 걸 환영해요. 체크인하시나요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "check" },
                        new LineEntry(
                            "Of course. May I have your name, please?",
                            "물론이죠. 성함을 알려 주시겠어요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "reservation" },
                        new LineEntry(
                            "Let me find your reservation. One moment, please.",
                            "예약을 찾아볼게요. 잠시만 기다려 주세요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "thank" },
                        new LineEntry(
                            "My pleasure. Enjoy your stay!",
                            "천만에요. 편히 쉬다 가세요!"
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "I see. How else can I help you?",
                    "그렇군요. 더 도와드릴 일이 있을까요?"
                ),
                exampleSentence = "I'd like to check in, please.",
            },
            new Entry
            {
                npcCode = "SHOP_STAFF",
                npcName = "Alex",
                roleLabel = "드럭스토어 직원",
                opening = new LineEntry(
                    "Hi! Welcome to the drugstore. Can I help you find something?",
                    "안녕하세요! 드럭스토어에 오신 걸 환영해요. 찾으시는 물건이 있나요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "umbrella" },
                        new LineEntry(
                            "Umbrellas are right by the door. They're 8 coins each.",
                            "우산은 문 바로 옆에 있어요. 하나에 8코인이에요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "card" },
                        new LineEntry(
                            "Sure, we take cards. Please tap it here.",
                            "네, 카드 돼요. 여기에 대 주세요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "thank" },
                        new LineEntry(
                            "You're welcome! Stay dry out there.",
                            "천만에요! 비 맞지 않게 조심하세요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Sure. Is there anything else you need?",
                    "네. 더 필요한 건 없으세요?"
                ),
                exampleSentence = "Do you sell umbrellas?",
            },
            new Entry
            {
                npcCode = "TAXI_DRIVER",
                npcName = "Joe",
                roleLabel = "택시 기사",
                opening = new LineEntry(
                    "Hi there! Where are you headed?",
                    "안녕하세요! 어디로 가세요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "hotel" },
                        new LineEntry(
                            "The hotel? It's just up the street. Hop in!",
                            "호텔이요? 이 길 바로 위쪽이에요. 타세요!"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "thank" },
                        new LineEntry(
                            "You're welcome! Have a nice day.",
                            "천만에요! 좋은 하루 보내세요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Sure. Please fasten your seat belt.",
                    "알겠어요. 안전벨트를 매 주세요."
                ),
                exampleSentence = "Can you take me to the hotel, please?",
            },
        };

        [SerializeField]
        [Tooltip("목록에 없는 NPC와 대화할 때 쓰는 대본")]
        private Entry fallback = new Entry
        {
            npcName = "Neighbor",
            opening = new LineEntry("Hello! Nice to meet you.", "안녕하세요! 만나서 반가워요."),
            defaultReply = new LineEntry("I see!", "그렇군요!"),
            exampleSentence = "Nice to meet you, too.",
        };

        public override bool TryGetScript(string npcCode, out DialogueScript script)
        {
            foreach (Entry entry in entries)
            {
                if (entry.npcCode == npcCode)
                {
                    script = entry.ToScript();
                    return true;
                }
            }

            script = fallback.ToScript();
            return true;
        }

        [Serializable]
        private class Entry
        {
            [Tooltip("ERD npcs.code")]
            public string npcCode;

            [Tooltip("대화 화면에 보이는 NPC 이름")]
            public string npcName;

            [Tooltip("누가 말을 거는지 알릴 때 쓰는 역할. 예: 드럭스토어 직원")]
            public string roleLabel;

            public LineEntry opening = new LineEntry("", "");

            [Tooltip("있으면 튜토리얼처럼 읽을 대사를 따라가는 대화가 된다")]
            public List<GuidedTurnEntry> guidedTurns = new List<GuidedTurnEntry>();

            [Tooltip("위에서부터 확인해 키워드가 모두 들어 있는 첫 규칙으로 답한다")]
            public List<KeywordReplyEntry> keywordReplies = new List<KeywordReplyEntry>();

            public LineEntry defaultReply = new LineEntry("", "");

            [Tooltip("[예시 문장]으로 입력창에 넣어 줄 문장")]
            public string exampleSentence = "";

            public DialogueScript ToScript()
            {
                var turns = new List<GuidedTurn>();
                foreach (GuidedTurnEntry turn in guidedTurns)
                {
                    turns.Add(
                        new GuidedTurn(
                            turn.suggestedEnglish,
                            turn.suggestedKorean,
                            turn.reply.ToLine()
                        )
                    );
                }

                var replies = new List<KeywordReply>();
                foreach (KeywordReplyEntry reply in keywordReplies)
                {
                    replies.Add(new KeywordReply(reply.keywords, reply.reply.ToLine()));
                }

                return new DialogueScript(
                    npcName,
                    opening.ToLine(),
                    turns,
                    replies,
                    defaultReply.ToLine(),
                    exampleSentence,
                    roleLabel
                );
            }
        }

        [Serializable]
        private class LineEntry
        {
            public string english;
            public string korean;

            public LineEntry() { }

            public LineEntry(string english, string korean)
            {
                this.english = english;
                this.korean = korean;
            }

            public DialogueLine ToLine()
            {
                return DialogueLine.FromNpc(english, korean);
            }
        }

        [Serializable]
        private class GuidedTurnEntry
        {
            [Tooltip("사용자가 읽어 볼 대사")]
            public string suggestedEnglish;

            public string suggestedKorean;

            [Tooltip("사용자가 말한 뒤 NPC가 이어서 하는 대사")]
            public LineEntry reply;

            public GuidedTurnEntry() { }

            public GuidedTurnEntry(string suggestedEnglish, string suggestedKorean, LineEntry reply)
            {
                this.suggestedEnglish = suggestedEnglish;
                this.suggestedKorean = suggestedKorean;
                this.reply = reply;
            }
        }

        [Serializable]
        private class KeywordReplyEntry
        {
            public string[] keywords;
            public LineEntry reply;

            public KeywordReplyEntry() { }

            public KeywordReplyEntry(string[] keywords, LineEntry reply)
            {
                this.keywords = keywords;
                this.reply = reply;
            }
        }
    }
}
