namespace A305.EditorTools.ObjectHover
{
    /// <summary>실내 소품에 쓰는 미국 영어 표현. 기존 공용 단어는 그대로 재사용한다.</summary>
    internal static class IndoorObjectWordCatalog
    {
        internal static void EnsureWords()
        {
            foreach (string[] word in Definitions)
                ObjectWordAuthoring.FindOrCreateWord(word[0], word[1], word[2], word[3], word[4]);
        }

        private static readonly string[][] Definitions =
        {
            new[]
            {
                "oven",
                "oven",
                "음식을 굽는 조리 기구(오븐)",
                "The bread is in the oven.",
                "빵이 오븐 안에 있어요.",
            },
            new[]
            {
                "flour_bag",
                "bag of flour",
                "밀가루 한 포대",
                "We need another bag of flour.",
                "밀가루가 한 포대 더 필요해요.",
            },
            new[]
            {
                "watering_can",
                "watering can",
                "식물에 물을 주는 물뿌리개",
                "Fill the watering can with water.",
                "물뿌리개에 물을 채워 주세요.",
            },
            new[]
            {
                "wrapping_paper",
                "wrapping paper",
                "선물을 싸는 포장지",
                "Could you use this wrapping paper?",
                "이 포장지로 싸 주실 수 있나요?",
            },
            new[]
            {
                "ribbon",
                "ribbon",
                "장식용 띠(리본)",
                "Could you tie a ribbon around it?",
                "여기에 리본을 묶어 주실 수 있나요?",
            },
            new[]
            {
                "bottle",
                "bottle",
                "액체를 담는 병",
                "Is this bottle empty?",
                "이 병은 비어 있나요?",
            },
            new[]
            {
                "comb",
                "comb",
                "머리카락을 빗는 빗",
                "Could I borrow a comb?",
                "빗을 빌릴 수 있을까요?",
            },
            new[]
            {
                "hair_clippers",
                "hair clippers",
                "머리카락을 짧게 자르는 전기 이발기",
                "The barber uses hair clippers.",
                "이발사가 전기 이발기를 사용해요.",
            },
            new[]
            {
                "barber_tools",
                "barber tools",
                "머리를 다듬는 이발 도구",
                "The barber tools are on the counter.",
                "이발 도구는 작업대 위에 있어요.",
            },
            new[]
            {
                "spray_bottle",
                "spray bottle",
                "액체를 안개처럼 뿌리는 분무기",
                "There is water in the spray bottle.",
                "분무기 안에 물이 있어요.",
            },
            new[]
            {
                "hair_dryer",
                "hair dryer",
                "머리카락을 말리는 기기(헤어드라이어)",
                "Can I use the hair dryer?",
                "헤어드라이어를 써도 될까요?",
            },
            new[]
            {
                "hooded_hair_dryer",
                "hooded hair dryer",
                "머리 위에 씌워 사용하는 건조기",
                "Please sit under the hooded hair dryer.",
                "머리를 씌워 말리는 건조기 아래에 앉아 주세요.",
            },
            new[]
            {
                "coat_rack",
                "coat rack",
                "외투를 거는 옷걸이",
                "You can leave your jacket on the coat rack.",
                "재킷은 옷걸이에 걸어 두셔도 돼요.",
            },
            new[]
            {
                "coat",
                "coat",
                "겉에 입는 긴 외투(코트)",
                "I left my coat by the door.",
                "문 옆에 외투를 두고 왔어요.",
            },
            new[]
            {
                "hat",
                "hat",
                "머리에 쓰는 모자",
                "Is this your hat?",
                "이 모자가 당신 것인가요?",
            },
            new[]
            {
                "broom",
                "broom",
                "바닥을 쓰는 빗자루",
                "Where can I find a broom?",
                "빗자루는 어디에 있나요?",
            },
            new[]
            {
                "rolled_towels",
                "rolled towels",
                "둥글게 말아 놓은 수건",
                "The rolled towels are clean.",
                "말아 놓은 수건은 깨끗해요.",
            },
            new[]
            {
                "burger",
                "burger",
                "빵 사이에 패티와 재료를 넣은 음식(햄버거)",
                "Can I get a burger without onions?",
                "양파를 뺀 햄버거를 주시겠어요?",
            },
            new[]
            {
                "cutting_board",
                "cutting board",
                "음식 재료를 써는 도마",
                "Put the vegetables on the cutting board.",
                "채소를 도마 위에 올려 주세요.",
            },
            new[]
            {
                "range_hood",
                "range hood",
                "조리대 위 연기를 빨아들이는 환기 장치",
                "Please turn on the range hood.",
                "조리대 환기 장치를 켜 주세요.",
            },
            new[]
            {
                "cooking_pot",
                "pot",
                "음식을 끓이는 냄비",
                "The soup is in the pot.",
                "수프가 냄비 안에 있어요.",
            },
            new[]
            {
                "frying_pan",
                "frying pan",
                "음식을 볶거나 부치는 납작한 팬",
                "Heat the frying pan first.",
                "프라이팬을 먼저 달궈 주세요.",
            },
            new[]
            {
                "jukebox",
                "jukebox",
                "곡을 골라 듣는 음악 재생 기계(주크박스)",
                "Let's pick a song on the jukebox.",
                "주크박스에서 노래를 골라 봐요.",
            },
            new[]
            {
                "deep_fryer",
                "deep fryer",
                "기름에 음식을 담가 튀기는 조리 기구",
                "The fries are in the deep fryer.",
                "감자튀김이 튀김기 안에 있어요.",
            },
            new[]
            {
                "griddle",
                "griddle",
                "음식을 굽는 평평한 조리용 철판",
                "The burgers are cooking on the griddle.",
                "햄버거 패티가 철판에서 익고 있어요.",
            },
            new[]
            {
                "mail_cart",
                "mail cart",
                "우편물을 옮기는 수레",
                "Put the packages on the mail cart.",
                "소포를 우편 수레에 올려 주세요.",
            },
            new[]
            {
                "envelope",
                "envelope",
                "편지나 서류를 넣는 봉투",
                "Do you sell envelopes?",
                "봉투를 파나요?",
            },
            new[]
            {
                "form",
                "form",
                "내용을 적어 넣는 서식",
                "Where should I sign this form?",
                "이 서식 어디에 서명하면 되나요?",
            },
            new[]
            {
                "pen",
                "pen",
                "글씨를 쓰는 펜",
                "May I borrow a pen?",
                "펜을 빌릴 수 있을까요?",
            },
            new[]
            {
                "monitor",
                "monitor",
                "컴퓨터 화면을 보여 주는 장치(모니터)",
                "The address is on the monitor.",
                "주소가 모니터에 나와 있어요.",
            },
            new[]
            {
                "keyboard",
                "keyboard",
                "컴퓨터에 글자를 입력하는 자판(키보드)",
                "This keyboard is easy to use.",
                "이 키보드는 쓰기 편해요.",
            },
            new[]
            {
                "water_cooler",
                "water cooler",
                "마실 물을 공급하는 냉온수기",
                "The water cooler is by the door.",
                "냉온수기는 문 옆에 있어요.",
            },
            new[]
            {
                "queue_barrier",
                "queue barrier",
                "줄 서는 구역을 나누는 안내봉과 줄",
                "Please wait behind the queue barrier.",
                "줄 안내봉 뒤에서 기다려 주세요.",
            },
            new[]
            {
                "scale",
                "scale",
                "무게를 재는 저울",
                "Please put your package on the scale.",
                "소포를 저울 위에 올려 주세요.",
            },
            new[]
            {
                "badge",
                "badge",
                "소속이나 신분을 나타내는 표장(배지)",
                "The officer showed me her badge.",
                "경찰관이 제게 배지를 보여 주었어요.",
            },
            new[]
            {
                "magazine_rack",
                "magazine rack",
                "잡지를 세워 두는 꽂이",
                "The magazine rack is next to the chair.",
                "잡지 꽂이는 의자 옆에 있어요.",
            },
            new[]
            {
                "magazine",
                "magazine",
                "여러 기사와 사진을 실은 잡지",
                "Can I read this magazine?",
                "이 잡지를 읽어도 될까요?",
            },
            new[]
            {
                "tool_cart",
                "tool cart",
                "도구를 담아 옮기는 바퀴 달린 수납대",
                "The scissors are on the tool cart.",
                "가위는 도구 수납대 위에 있어요.",
            },
            new[]
            {
                "nightstand",
                "nightstand",
                "침대 옆에 두는 작은 탁자나 수납장",
                "My phone is on the nightstand.",
                "제 휴대폰은 침대 옆 탁자에 있어요.",
            },
            new[]
            {
                "bulletin_board",
                "bulletin board",
                "안내문을 붙이는 게시판",
                "Check the bulletin board for updates.",
                "새 안내는 게시판에서 확인하세요.",
            },
            new[]
            {
                "barber_pole",
                "barber pole",
                "이발소를 나타내는 줄무늬 기둥 간판",
                "Look for the barber pole outside.",
                "밖에 있는 이발소 줄무늬 간판을 찾아보세요.",
            },
            new[]
            {
                "candy_bar",
                "candy bar",
                "막대 모양으로 포장한 달콤한 간식",
                "How much is this candy bar?",
                "이 막대 과자는 얼마인가요?",
            },
            new[]
            {
                "chocolate_bar",
                "chocolate bar",
                "판 모양의 초콜릿",
                "I'd like a chocolate bar, please.",
                "초콜릿 한 개 주세요.",
            },
            new[]
            {
                "snack_bag",
                "bag of snacks",
                "간식이 든 봉지",
                "I'll take this bag of snacks.",
                "이 봉지 간식으로 할게요.",
            },
            new[]
            {
                "cookie",
                "cookie",
                "작고 달콤한 구운 과자(쿠키)",
                "Could I have two cookies?",
                "쿠키 두 개 주시겠어요?",
            },
            new[]
            {
                "lollipop",
                "lollipop",
                "막대에 꽂힌 사탕",
                "Which flavor is this lollipop?",
                "이 막대사탕은 무슨 맛인가요?",
            },
            new[]
            {
                "pill_bottle",
                "pill bottle",
                "알약을 담는 약병",
                "Keep the pill bottle closed.",
                "약병 뚜껑을 닫아 두세요.",
            },
            new[]
            {
                "medicine_box",
                "box of medicine",
                "약이 든 상자",
                "Where can I find this box of medicine?",
                "이 상자에 든 약은 어디에 있나요?",
            },
            new[]
            {
                "can",
                "can",
                "음식이나 음료를 담는 금속 용기(캔)",
                "Can this can be recycled?",
                "이 캔은 재활용할 수 있나요?",
            },
            new[]
            {
                "carton",
                "carton",
                "음식이나 음료를 담는 종이 용기",
                "Please put the carton in the bag.",
                "종이 용기를 가방에 넣어 주세요.",
            },
            new[]
            {
                "honey",
                "honey",
                "벌이 만드는 달콤한 꿀",
                "Do you have any honey?",
                "꿀이 있나요?",
            },
            new[]
            {
                "peanut_butter",
                "peanut butter",
                "땅콩을 갈아 만든 바르는 음식(땅콩버터)",
                "I'd like a jar of peanut butter.",
                "땅콩버터 한 병 주세요.",
            },
            new[]
            {
                "takeout_container",
                "takeout container",
                "음식을 포장해 가져가는 용기",
                "Could I get a takeout container?",
                "음식 포장 용기를 받을 수 있을까요?",
            },
            new[]
            {
                "hot_dog",
                "hot dog",
                "긴 빵 사이에 소시지를 넣은 음식(핫도그)",
                "Can I get a hot dog with mustard?",
                "머스타드를 넣은 핫도그를 주시겠어요?",
            },
            new[]
            {
                "hot_dog_roller",
                "hot dog roller",
                "소시지를 굴리며 데우는 조리 기계",
                "The hot dogs are on the hot dog roller.",
                "핫도그용 소시지가 회전 조리기 위에 있어요.",
            },
            new[]
            {
                "apple",
                "apple",
                "사과",
                "I'd like two apples, please.",
                "사과 두 개 주세요.",
            },
            new[]
            {
                "orange",
                "orange",
                "주황색 감귤류 과일(오렌지)",
                "Is this orange sweet?",
                "이 오렌지는 단가요?",
            },
            new[]
            {
                "banana",
                "banana",
                "길고 노란 과일(바나나)",
                "I'll have a banana for a snack.",
                "간식으로 바나나를 먹을게요.",
            },
            new[]
            {
                "carrot",
                "carrot",
                "당근",
                "Please cut up the carrot.",
                "당근을 썰어 주세요.",
            },
            new[]
            {
                "cabbage",
                "cabbage",
                "양배추",
                "How much is this cabbage?",
                "이 양배추는 얼마인가요?",
            },
            new[]
            {
                "corn",
                "corn",
                "옥수수",
                "Do you have fresh corn?",
                "신선한 옥수수가 있나요?",
            },
            new[]
            {
                "broccoli",
                "broccoli",
                "초록색 꽃봉오리 모양의 채소(브로콜리)",
                "I'd like some broccoli.",
                "브로콜리를 좀 주세요.",
            },
            new[]
            {
                "umbrella_stand",
                "umbrella stand",
                "우산을 세워 두는 우산꽂이",
                "Leave your umbrella in the umbrella stand.",
                "우산은 우산꽂이에 놓아 주세요.",
            },
            new[]
            {
                "bucket",
                "bucket",
                "액체나 물건을 담는 양동이",
                "Could you bring me a bucket?",
                "양동이를 가져다주시겠어요?",
            },
            new[]
            {
                "bouquet",
                "bouquet",
                "꽃을 모아 묶은 꽃다발",
                "I'd like a bouquet for my mom.",
                "엄마께 드릴 꽃다발을 사고 싶어요.",
            },
            new[]
            {
                "brochure",
                "brochure",
                "정보를 소개하는 소책자",
                "May I take a brochure?",
                "안내 책자를 가져가도 될까요?",
            },
            new[]
            {
                "brochure_rack",
                "brochure rack",
                "안내 책자를 놓는 진열대",
                "The brochure rack is near the entrance.",
                "안내 책자 진열대는 입구 근처에 있어요.",
            },
            new[]
            {
                "shirt",
                "shirt",
                "상체에 입는 옷(셔츠)",
                "I'd like to wash this shirt.",
                "이 셔츠를 세탁하고 싶어요.",
            },
            new[]
            {
                "clothes_hanger",
                "clothes hanger",
                "옷 한 벌을 걸어 두는 옷걸이",
                "Could I have a clothes hanger?",
                "옷걸이를 하나 받을 수 있을까요?",
            },
            new[]
            {
                "baguette",
                "baguette",
                "길고 겉이 바삭한 빵(바게트)",
                "I'd like one baguette, please.",
                "바게트 하나 주세요.",
            },
            new[]
            {
                "basket",
                "basket",
                "물건을 담는 바구니",
                "Can I use this basket?",
                "이 바구니를 써도 될까요?",
            },
            new[]
            {
                "bun",
                "bun",
                "햄버거 등에 쓰는 작고 둥근 빵",
                "Could you toast the bun?",
                "햄버거 빵을 구워 주실 수 있나요?",
            },
            new[]
            {
                "wooden_crate",
                "crate",
                "물건을 담아 운반하는 나무 상자",
                "The buns are in the crate.",
                "햄버거 빵은 나무 상자 안에 있어요.",
            },
        };
    }
}
