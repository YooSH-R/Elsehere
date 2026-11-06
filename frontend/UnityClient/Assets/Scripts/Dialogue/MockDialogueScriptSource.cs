using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버·AI 연동 전에 쓰는 확인용 대화 대본. 튜토리얼 대본(키 TUTORIAL_PASSERBY)은 튜토리얼 동안 마을 행인 Ben이 말하는 정해진
    /// 길 안내 3턴이고, 나머지 NPC는 키워드 응답을 쓴다.
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
                npcName = "Ben",
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
                        ),
                        "straight · 곧장",
                        "ahead · 앞으로"
                    ),
                    new GuidedTurnEntry(
                        "Turn left at the corner.",
                        "모퉁이에서 왼쪽으로 도세요.",
                        new LineEntry("Got it. Is it far from here?", "알겠어요. 여기서 먼가요?"),
                        "turn left · 왼쪽으로 돌다",
                        "corner · 모퉁이"
                    ),
                    new GuidedTurnEntry(
                        "No, it's just two minutes away.",
                        "아니요, 2분이면 가요.",
                        new LineEntry(
                            "Thank you so much. You're a lifesaver!",
                            "정말 고마워요. 덕분에 살았어요!"
                        ),
                        "minute · 분",
                        "away · 떨어져"
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
            new Entry
            {
                npcCode = "POLICE_OFFICER",
                npcName = "Dan",
                roleLabel = "경찰관",
                opening = new LineEntry(
                    "Hello, this is the Maple Town police. What happened?",
                    "안녕하세요, 메이플 타운 경찰입니다. 무슨 일이 있었나요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "stolen", "lost", "accident" },
                        new LineEntry(
                            "I see. Where did it happen?",
                            "알겠어요. 어디서 생긴 일인가요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "thank" },
                        new LineEntry("You're welcome. Stay safe.", "천만에요. 조심히 다니세요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "Can you tell me what happened and where?",
                    "무슨 일이 어디서 있었는지 말해 줄래요?"
                ),
                exampleSentence = "My wallet was stolen at the diner.",
            },
            // 마을 거리 매장 직원(S15P31A305-505). 이름·역할은 서버 시드와 같고, 가격은 서버 상황 문장과 같다.
            new Entry
            {
                npcCode = "POST_OFFICE_STAFF",
                npcName = "Linda",
                roleLabel = "우체국 직원",
                opening = new LineEntry(
                    "Hello, welcome to the post office. What can I do for you?",
                    "안녕하세요, 우체국입니다. 무엇을 도와드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "stamp" },
                        new LineEntry(
                            "A stamp for a letter is one dollar.",
                            "편지용 우표는 1달러예요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "package" },
                        new LineEntry(
                            "Please put it on the scale. Sending a package is five dollars.",
                            "저울에 올려 주세요. 소포는 5달러예요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Would you like to send a letter or a package?",
                    "편지를 보내세요, 소포를 보내세요?"
                ),
                exampleSentence = "I'd like to send this package.",
            },
            new Entry
            {
                npcCode = "DINER_STAFF",
                npcName = "Rosa",
                roleLabel = "식당 종업원",
                opening = new LineEntry(
                    "Hi, honey! Welcome to Maple Diner. What can I get you?",
                    "어서 와요! 메이플 식당이에요. 뭘 드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "pancake" },
                        new LineEntry(
                            "Pancakes, great choice! That's seven dollars.",
                            "팬케이크, 좋은 선택이에요! 7달러예요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "coffee" },
                        new LineEntry(
                            "Coffee is two dollars, and refills are free.",
                            "커피는 2달러고 리필은 무료예요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "We have pancakes, burgers, apple pie and coffee.",
                    "팬케이크, 버거, 애플파이, 커피가 있어요."
                ),
                exampleSentence = "Can I have pancakes and coffee, please?",
            },
            new Entry
            {
                npcCode = "BANK_STAFF",
                npcName = "Henry",
                roleLabel = "은행원",
                opening = new LineEntry(
                    "Hello. Welcome to Town Bank. How may I help you today?",
                    "안녕하세요. 타운 은행입니다. 무엇을 도와드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "account" },
                        new LineEntry(
                            "To open an account, I'll need a photo ID, please.",
                            "계좌를 만들려면 사진이 있는 신분증이 필요해요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "atm" },
                        new LineEntry(
                            "The ATM is just inside the entrance, on the left.",
                            "ATM은 들어오시면 바로 왼쪽에 있어요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "I can help you with deposits and withdrawals.",
                    "입금과 출금을 도와드릴 수 있어요."
                ),
                exampleSentence = "I'd like to open an account.",
            },
            new Entry
            {
                npcCode = "LAUNDROMAT_STAFF",
                npcName = "Gus",
                roleLabel = "빨래방 주인",
                opening = new LineEntry(
                    "Hey there! Welcome to Suds. Got some laundry today?",
                    "안녕하세요! 서즈 빨래방이에요. 오늘 빨래 있어요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "wash" },
                        new LineEntry(
                            "A wash is three dollars and takes about thirty minutes.",
                            "세탁은 3달러고 30분쯤 걸려요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "dry" },
                        new LineEntry(
                            "The dryer is two dollars. It takes about forty minutes.",
                            "건조기는 2달러예요. 40분쯤 걸려요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Put your clothes in, add the soap, and press start.",
                    "옷과 세제를 넣고 시작을 누르면 돼요."
                ),
                exampleSentence = "How much is a wash?",
            },
            new Entry
            {
                npcCode = "FLOWER_SHOP_STAFF",
                npcName = "Lily",
                roleLabel = "꽃집 주인",
                opening = new LineEntry(
                    "Oh, hello. Welcome to Bloom Flowers. Who are the flowers for?",
                    "아, 안녕하세요. 블룸 플라워예요. 누구에게 줄 꽃인가요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "bouquet" },
                        new LineEntry(
                            "A bouquet is fifteen dollars. Maybe roses or tulips?",
                            "꽃다발은 15달러예요. 장미나 튤립은 어때요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "rose" },
                        new LineEntry("One rose is two dollars.", "장미 한 송이는 2달러예요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "We have roses, tulips, sunflowers and daisies today.",
                    "오늘은 장미, 튤립, 해바라기, 데이지가 있어요."
                ),
                exampleSentence = "I'd like a bouquet for my friend.",
            },
            new Entry
            {
                npcCode = "BARBER_STAFF",
                npcName = "Tony",
                roleLabel = "이발사",
                opening = new LineEntry(
                    "Hey, come on in! Take a seat. Haircut today?",
                    "어서 와요! 앉으세요. 오늘 머리 자르게요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "haircut" },
                        new LineEntry(
                            "Sure! A haircut is twenty dollars. How short do you want it?",
                            "좋아요! 커트는 20달러예요. 얼마나 짧게 할까요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "shave" },
                        new LineEntry("A shave is ten dollars.", "면도는 10달러예요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "No appointment needed. Just tell me how you want it.",
                    "예약은 필요 없어요. 어떻게 하고 싶은지만 말해 줘요."
                ),
                exampleSentence = "Just a little shorter, please.",
            },
            new Entry
            {
                npcCode = "BAKERY_STAFF",
                npcName = "Grace",
                roleLabel = "제빵사",
                opening = new LineEntry(
                    "Hello, dear! The croissants just came out of the oven.",
                    "어서 와요! 크루아상이 방금 오븐에서 나왔어요."
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "croissant" },
                        new LineEntry(
                            "A croissant is three dollars. For here or to go?",
                            "크루아상은 3달러예요. 먹고 가요, 가져가요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "bread" },
                        new LineEntry("A loaf of bread is four dollars.", "빵 한 덩이는 4달러예요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "We have bread, croissants and chocolate cake today.",
                    "오늘은 빵, 크루아상, 초콜릿 케이크가 있어요."
                ),
                exampleSentence = "Can I have a croissant to go?",
            },
            new Entry
            {
                npcCode = "ARCADE_STAFF",
                npcName = "Max",
                roleLabel = "오락실 직원",
                opening = new LineEntry(
                    "Hey! Welcome to Little Arcade! Wanna try the claw machine?",
                    "안녕! 리틀 아케이드예요! 인형 뽑기 해 볼래요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "play" },
                        new LineEntry(
                            "Move the claw with the stick, then press the button to drop it.",
                            "막대로 집게를 움직이고 버튼을 눌러 내려요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "much" },
                        new LineEntry(
                            "One game is one dollar. No promises on the prize, though!",
                            "한 판에 1달러예요. 인형이 뽑힐지는 장담 못 해요!"
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Pick a machine and give it a try!",
                    "기계를 하나 골라서 해 봐요!"
                ),
                exampleSentence = "How do I play the claw machine?",
            },
            new Entry
            {
                npcCode = "HOME_STORE_STAFF",
                npcName = "Nina",
                roleLabel = "매장 직원",
                opening = new LineEntry(
                    "Hello, welcome to Home & Living. Which room are you shopping for?",
                    "안녕하세요, 홈 앤 리빙입니다. 어느 방에 둘 물건을 찾으세요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "lamp" },
                        new LineEntry(
                            "This lamp is thirty dollars. It would look nice in a bedroom.",
                            "이 램프는 30달러예요. 침실에 잘 어울려요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "chair" },
                        new LineEntry(
                            "The armchair is one hundred fifty dollars.",
                            "안락의자는 150달러예요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Take your time and look around.",
                    "천천히 둘러보세요."
                ),
                exampleSentence = "I'm looking for a lamp for my bedroom.",
            },
            new Entry
            {
                npcCode = "CAFE_BARISTA",
                npcName = "Zoe",
                roleLabel = "카페 직원",
                opening = new LineEntry(
                    "Hi! I'm Zoe. What can I make for you today?",
                    "안녕하세요! 저는 Zoe예요. 오늘 무엇을 만들어 드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "latte" },
                        new LineEntry(
                            "One latte coming up! Hot or iced?",
                            "라테 하나 바로 만들어 드릴게요! 따뜻한 걸로요, 아이스로요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "much" },
                        new LineEntry("A small coffee is three dollars.", "작은 커피는 3달러예요.")
                    ),
                },
                defaultReply = new LineEntry("Sure! Anything else?", "네! 더 필요한 건 없으세요?"),
                exampleSentence = "Can I have a latte, please?",
            },
            new Entry
            {
                npcCode = "CAFE_SERVER",
                npcName = "Leo",
                roleLabel = "카페 직원",
                opening = new LineEntry(
                    "Hi, I'm Leo. Would you like a table by the window?",
                    "안녕하세요, 저는 Leo예요. 창가 자리로 드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "menu" },
                        new LineEntry(
                            "Here's the menu. The muffins are fresh today.",
                            "메뉴판 여기 있어요. 오늘 머핀이 갓 나왔어요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "table" },
                        new LineEntry(
                            "Sure, take any table you like.",
                            "네, 원하시는 자리에 앉으세요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Let me know when you're ready to order.",
                    "주문하실 때 말씀해 주세요."
                ),
                exampleSentence = "Can I see the menu, please?",
            },
            new Entry
            {
                npcCode = "HOTEL_RECEPTIONIST",
                npcName = "Owen",
                roleLabel = "호텔 직원",
                opening = new LineEntry(
                    "Good evening! Welcome to Maple House. Are you checking in?",
                    "안녕하세요! 메이플 하우스에 오신 걸 환영해요. 체크인하시나요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "check" },
                        new LineEntry(
                            "Sure. Can I have your name, please?",
                            "네. 성함을 알려 주시겠어요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "room" },
                        new LineEntry(
                            "Your room is on the second floor. Here is your key.",
                            "방은 2층이에요. 열쇠 여기 있어요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Let me know if you need anything.",
                    "필요한 게 있으면 말씀해 주세요."
                ),
                exampleSentence = "I'd like to check in, please.",
            },
            new Entry
            {
                npcCode = "HOTEL_CONCIERGE",
                npcName = "Walter",
                roleLabel = "꽃집 직원",
                opening = new LineEntry(
                    "Oh, hello. Welcome to Bloom Flowers. Who are the flowers for?",
                    "아, 안녕하세요. 블룸 플라워예요. 누구에게 줄 꽃인가요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "bouquet" },
                        new LineEntry(
                            "A bouquet is fifteen dollars. Maybe roses or tulips?",
                            "꽃다발은 15달러예요. 장미나 튤립은 어때요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "rose" },
                        new LineEntry("One rose is two dollars.", "장미 한 송이는 2달러예요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "We have roses, tulips, sunflowers and daisies today.",
                    "오늘은 장미, 튤립, 해바라기, 데이지가 있어요."
                ),
                exampleSentence = "I'd like a bouquet for my friend.",
            },
            new Entry
            {
                npcCode = "HOTEL_BELLHOP",
                npcName = "Diego",
                roleLabel = "벨보이",
                opening = new LineEntry(
                    "Hi! I'm Diego. Can I take your bags?",
                    "안녕하세요! 저는 Diego예요. 짐을 들어 드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "bag" },
                        new LineEntry(
                            "No problem. I'll bring them up to your room.",
                            "그럼요. 방까지 가져다 드릴게요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "elevator" },
                        new LineEntry(
                            "The elevator is right over there.",
                            "엘리베이터는 바로 저쪽이에요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Just let me know if you need a hand.",
                    "도움이 필요하면 말씀만 하세요."
                ),
                exampleSentence = "Could you help me with my bags?",
            },
            new Entry
            {
                npcCode = "HOTEL_NIGHT_MANAGER",
                npcName = "Clara",
                roleLabel = "호텔 야간 매니저",
                opening = new LineEntry(
                    "Good evening. I'm Clara, the night manager. How is your stay?",
                    "안녕하세요. 야간 매니저 Clara예요. 지내시기 괜찮으세요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "noise" },
                        new LineEntry(
                            "I'm sorry about the noise. I'll take care of it right away.",
                            "소음 때문에 불편하셨다니 죄송해요. 바로 처리할게요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "late" },
                        new LineEntry(
                            "The front desk is open all night.",
                            "프런트는 밤새 열려 있어요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Please call the front desk anytime.",
                    "언제든 프런트로 연락 주세요."
                ),
                exampleSentence = "There is too much noise in my room.",
            },
            new Entry
            {
                npcCode = "SHOP_PHARMACIST",
                npcName = "Hannah",
                roleLabel = "약사",
                opening = new LineEntry(
                    "Hello, I'm Hannah, the pharmacist. How can I help you?",
                    "안녕하세요, 약사 Hannah예요. 무엇을 도와드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "headache" },
                        new LineEntry(
                            "This medicine is good for headaches. Take one after a meal.",
                            "이 약이 두통에 좋아요. 식후에 한 알 드세요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "cold" },
                        new LineEntry(
                            "For a cold, get some rest and drink lots of water.",
                            "감기에는 푹 쉬고 물을 많이 드세요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Tell me how you feel, and I'll find something for you.",
                    "어디가 불편한지 말씀해 주시면 맞는 약을 찾아 드릴게요."
                ),
                exampleSentence = "I have a headache. What should I take?",
            },
            new Entry
            {
                npcCode = "SHOP_NIGHT_CLERK",
                npcName = "Samuel",
                roleLabel = "드럭스토어 직원",
                opening = new LineEntry(
                    "Evening. I'm Samuel. We're open all night.",
                    "안녕하세요. 저는 Samuel이에요. 밤새 문을 열어요."
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "water" },
                        new LineEntry(
                            "The water is in the fridge at the back.",
                            "물은 뒤쪽 냉장고에 있어요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "much" },
                        new LineEntry("That's two dollars.", "2달러예요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "Take your time. I'm right here.",
                    "천천히 보세요. 저는 여기 있어요."
                ),
                exampleSentence = "Where can I find some water?",
            },
            new Entry
            {
                npcCode = "POLICE_SERGEANT",
                npcName = "Victor",
                roleLabel = "경사",
                opening = new LineEntry(
                    "Good evening. I'm Sergeant Victor. What happened?",
                    "안녕하세요. 경사 Victor입니다. 무슨 일이 있었나요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "lost" },
                        new LineEntry(
                            "Tell me where you saw it last.",
                            "마지막으로 본 곳을 말씀해 주세요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "thank" },
                        new LineEntry(
                            "You're welcome. Get home safely.",
                            "천만에요. 조심히 들어가세요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "I see. Please tell me more.",
                    "그렇군요. 더 자세히 말씀해 주세요."
                ),
                exampleSentence = "I lost my wallet.",
            },
            new Entry
            {
                npcCode = "POLICE_ROOKIE",
                npcName = "Taylor",
                roleLabel = "신입 경찰관",
                opening = new LineEntry(
                    "Hi, I'm Officer Taylor. Is everything okay?",
                    "안녕하세요, 경찰관 Taylor이에요. 별일 없으세요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "stolen" },
                        new LineEntry(
                            "I'm sorry to hear that. Where did it happen?",
                            "안타깝네요. 어디서 그랬나요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "patrol" },
                        new LineEntry(
                            "I patrol the main street every evening.",
                            "저는 매일 저녁 큰길을 순찰해요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Okay. Let me write that down.",
                    "알겠어요. 적어 둘게요."
                ),
                exampleSentence = "My bag was stolen.",
            },
            new Entry
            {
                npcCode = "POLICE_PATROL_OFFICER",
                npcName = "Kevin",
                roleLabel = "베이커리 직원",
                opening = new LineEntry(
                    "Hello, dear! The croissants just came out of the oven.",
                    "어서 와요! 크루아상이 방금 오븐에서 나왔어요."
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "croissant" },
                        new LineEntry(
                            "A croissant is three dollars. For here or to go?",
                            "크루아상은 3달러예요. 먹고 가요, 가져가요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "bread" },
                        new LineEntry("A loaf of bread is four dollars.", "빵 한 덩이는 4달러예요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "We have bread, croissants and chocolate cake today.",
                    "오늘은 빵, 크루아상, 초콜릿 케이크가 있어요."
                ),
                exampleSentence = "Can I have a croissant to go?",
            },
            new Entry
            {
                npcCode = "TOWN_RETIREE",
                npcName = "Ruby",
                roleLabel = "우체국 직원",
                opening = new LineEntry(
                    "Hello, welcome to the post office. What can I do for you?",
                    "안녕하세요, 우체국입니다. 무엇을 도와드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "stamp" },
                        new LineEntry(
                            "A stamp for a letter is one dollar.",
                            "편지용 우표는 1달러예요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "package" },
                        new LineEntry(
                            "Please put it on the scale. Sending a package is five dollars.",
                            "저울에 올려 주세요. 소포는 5달러예요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Would you like to send a letter or a package?",
                    "편지를 보내세요, 소포를 보내세요?"
                ),
                exampleSentence = "I'd like to send this package.",
            },
            new Entry
            {
                npcCode = "TOWN_STUDENT",
                npcName = "Ethan",
                roleLabel = "오락실 직원",
                opening = new LineEntry(
                    "Hey! Welcome to Little Arcade! Wanna try the claw machine?",
                    "안녕! 리틀 아케이드예요! 인형 뽑기 해 볼래요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "play" },
                        new LineEntry(
                            "Move the claw with the stick, then press the button to drop it.",
                            "막대로 집게를 움직이고 버튼을 눌러 내려요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "much" },
                        new LineEntry(
                            "One game is one dollar. No promises on the prize, though!",
                            "한 판에 1달러예요. 인형이 뽑힐지는 장담 못 해요!"
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Pick a machine and give it a try!",
                    "기계를 하나 골라서 해 봐요!"
                ),
                exampleSentence = "How do I play the claw machine?",
            },
            new Entry
            {
                npcCode = "TOWN_EVENING_WALKER",
                npcName = "Maya",
                roleLabel = "은행원",
                opening = new LineEntry(
                    "Hello. Welcome to Town Bank. How may I help you today?",
                    "안녕하세요. 타운 은행입니다. 무엇을 도와드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "account" },
                        new LineEntry(
                            "To open an account, I'll need a photo ID, please.",
                            "계좌를 만들려면 사진이 있는 신분증이 필요해요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "atm" },
                        new LineEntry(
                            "The ATM is just inside the entrance, on the left.",
                            "ATM은 들어오시면 바로 왼쪽에 있어요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "I can help you with deposits and withdrawals.",
                    "입금과 출금을 도와드릴 수 있어요."
                ),
                exampleSentence = "I'd like to open an account.",
            },
            new Entry
            {
                npcCode = "TOWN_SHOPPER",
                npcName = "Helen",
                roleLabel = "은행원",
                opening = new LineEntry(
                    "Hello. Welcome to Town Bank. How may I help you today?",
                    "안녕하세요. 타운 은행입니다. 무엇을 도와드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "account" },
                        new LineEntry(
                            "To open an account, I'll need a photo ID, please.",
                            "계좌를 만들려면 사진이 있는 신분증이 필요해요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "atm" },
                        new LineEntry(
                            "The ATM is just inside the entrance, on the left.",
                            "ATM은 들어오시면 바로 왼쪽에 있어요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "I can help you with deposits and withdrawals.",
                    "입금과 출금을 도와드릴 수 있어요."
                ),
                exampleSentence = "I'd like to open an account.",
            },
            new Entry
            {
                npcCode = "TOWN_HANDYMAN",
                npcName = "Frank",
                roleLabel = "생활용품점 직원",
                opening = new LineEntry(
                    "Hello, welcome to Home & Living. Which room are you shopping for?",
                    "안녕하세요, 홈 앤 리빙입니다. 어느 방에 둘 물건을 찾으세요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "lamp" },
                        new LineEntry(
                            "This lamp is thirty dollars. It would look nice in a bedroom.",
                            "이 램프는 30달러예요. 침실에 잘 어울려요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "chair" },
                        new LineEntry(
                            "The armchair is one hundred fifty dollars.",
                            "안락의자는 150달러예요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Take your time and look around.",
                    "천천히 둘러보세요."
                ),
                exampleSentence = "I'm looking for a lamp for my bedroom.",
            },
            new Entry
            {
                npcCode = "FIRE_CAPTAIN",
                npcName = "Ray",
                roleLabel = "식당 직원",
                opening = new LineEntry(
                    "Hi, honey! Welcome to Maple Diner. What can I get you?",
                    "어서 와요! 메이플 식당이에요. 뭘 드릴까요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "pancake" },
                        new LineEntry(
                            "Pancakes, great choice! That's seven dollars.",
                            "팬케이크, 좋은 선택이에요! 7달러예요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "coffee" },
                        new LineEntry(
                            "Coffee is two dollars, and refills are free.",
                            "커피는 2달러고 리필은 무료예요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "We have pancakes, burgers, apple pie and coffee.",
                    "팬케이크, 버거, 애플파이, 커피가 있어요."
                ),
                exampleSentence = "Can I have pancakes and coffee, please?",
            },
            new Entry
            {
                npcCode = "FIRE_FIGHTER",
                npcName = "Rachel",
                roleLabel = "빨래방 직원",
                opening = new LineEntry(
                    "Hey there! Welcome to Suds. Got some laundry today?",
                    "안녕하세요! 서즈 빨래방이에요. 오늘 빨래 있어요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "wash" },
                        new LineEntry(
                            "A wash is three dollars and takes about thirty minutes.",
                            "세탁은 3달러고 30분쯤 걸려요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "dry" },
                        new LineEntry(
                            "The dryer is two dollars. It takes about forty minutes.",
                            "건조기는 2달러예요. 40분쯤 걸려요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "Put your clothes in, add the soap, and press start.",
                    "옷과 세제를 넣고 시작을 누르면 돼요."
                ),
                exampleSentence = "How much is a wash?",
            },
            new Entry
            {
                npcCode = "FIRE_ROOKIE",
                npcName = "Andre",
                roleLabel = "이발소 직원",
                opening = new LineEntry(
                    "Hey, come on in! Take a seat. Haircut today?",
                    "어서 와요! 앉으세요. 오늘 머리 자르게요?"
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "haircut" },
                        new LineEntry(
                            "Sure! A haircut is twenty dollars. How short do you want it?",
                            "좋아요! 커트는 20달러예요. 얼마나 짧게 할까요?"
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "shave" },
                        new LineEntry("A shave is ten dollars.", "면도는 10달러예요.")
                    ),
                },
                defaultReply = new LineEntry(
                    "No appointment needed. Just tell me how you want it.",
                    "예약은 필요 없어요. 어떻게 하고 싶은지만 말해 줘요."
                ),
                exampleSentence = "Just a little shorter, please.",
            },
            new Entry
            {
                npcCode = "TOWN_GRANDFATHER",
                npcName = "Arthur",
                roleLabel = "행인",
                opening = new LineEntry("Good morning.", "좋은 아침이에요."),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "weather" },
                        new LineEntry("Good weather for a walk.", "걷기 좋은 날씨네요.")
                    ),
                    new KeywordReplyEntry(
                        new[] { "train" },
                        new LineEntry(
                            "I worked on the trains for forty years.",
                            "40년 동안 기차에서 일했어요."
                        )
                    ),
                },
                defaultReply = new LineEntry("Hm. Is that so.", "흠. 그렇군요."),
                exampleSentence = "Good morning. Nice weather today.",
            },
            new Entry
            {
                npcCode = "TOWN_GRANDMOTHER",
                npcName = "Margaret",
                roleLabel = "행인",
                opening = new LineEntry(
                    "Oh, good morning! We walk down the main street every morning.",
                    "어머, 좋은 아침이에요! 저희는 아침마다 큰길을 걸어요."
                ),
                keywordReplies = new List<KeywordReplyEntry>
                {
                    new KeywordReplyEntry(
                        new[] { "piano" },
                        new LineEntry(
                            "I taught piano for many years. I still play every day.",
                            "오랫동안 피아노를 가르쳤어요. 지금도 매일 쳐요."
                        )
                    ),
                    new KeywordReplyEntry(
                        new[] { "husband" },
                        new LineEntry(
                            "This is my husband, Arthur. He doesn't talk much.",
                            "제 남편 Arthur예요. 말수가 적어요."
                        )
                    ),
                },
                defaultReply = new LineEntry(
                    "How lovely! Have a nice day, dear.",
                    "참 좋네요! 좋은 하루 보내요."
                ),
                exampleSentence = "Good morning! Do you walk here every day?",
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
                            turn.reply.ToLine(),
                            turn.suggestWords
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

            [Tooltip(
                "이 대사를 NPC 목소리로 미리 합성한 음성(WAV 바이트). 있으면 대본 대화에서도 대사와 함께 들려준다. ai/tools/synthesize_script_lines.py로 만든다."
            )]
            public TextAsset voice;

            public LineEntry() { }

            public LineEntry(string english, string korean)
            {
                this.english = english;
                this.korean = korean;
            }

            public DialogueLine ToLine()
            {
                return DialogueLine.FromNpc(
                    english,
                    korean,
                    speech: voice != null ? voice.bytes : null
                );
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

            [Tooltip(
                "이 대사에 쓸 만한 추천 단어. 대화 화면 왼쪽 [단어]에 보인다. 예: corner · 모퉁이"
            )]
            public List<string> suggestWords = new List<string>();

            public GuidedTurnEntry() { }

            public GuidedTurnEntry(
                string suggestedEnglish,
                string suggestedKorean,
                LineEntry reply,
                params string[] suggestWords
            )
            {
                this.suggestedEnglish = suggestedEnglish;
                this.suggestedKorean = suggestedKorean;
                this.reply = reply;
                this.suggestWords = new List<string>(suggestWords);
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
