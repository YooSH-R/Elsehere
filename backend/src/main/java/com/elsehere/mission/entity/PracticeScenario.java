package com.elsehere.mission.entity;

import java.util.Arrays;
import java.util.List;
import java.util.Optional;

/**
 * 장소 연습 상황. 가게마다 세 가지이며 이야기 퀘스트와 따로, 처음부터 모두 열려 있고 몇 번이든 다시 할 수 있다.
 *
 * <p>상황마다 그 가게 직원이 일관되게 대답하도록 가격·시간·있는 물건 같은 사실을 영어 지시문({@link #instruction()})에 담는다. 목표는 학습자가 먼저
 * 말해야 하는 일이며, 의도 판정은 목표의 영어 기준({@link Goal#description()})으로 한다. 이름(name)이 API의 {@code
 * scenarioId}이고 대화에 저장하는 값이므로 바꾸지 않는다.
 */
public enum PracticeScenario {
  HOTEL_CHANGE_ROOM(
      PracticePlace.HOTEL,
      1,
      "방 바꿔 달라고 하기",
      "방이 시끄럽거나 에어컨이 고장 났다고 말하고 다른 방을 부탁해요.",
      "The learner is a guest staying in room 305 and has a problem with the room; they will say what it is, such as street noise or a broken air conditioner. Room 512 on the fifth floor is free: it is quiet and its air conditioner works. You can move them there today at no extra cost and give them a new key card. Apologize for the trouble once they explain it. Do not offer a refund or a discount.",
      goal(
          "describe_room_problem",
          "방의 문제 말하기",
          "The learner says what is wrong with their room, such as it being too noisy or the air conditioner not working. Saying only that there is a problem, without what it is, does not count.",
          "방이 시끄럽거나 에어컨이 고장 났다고 말하기"),
      goal(
          "ask_other_room",
          "다른 방 부탁하기",
          "The learner asks to change to a different room or asks whether another room is available.",
          "다른 방으로 바꿔 줄 수 있는지 묻기")),
  HOTEL_LATE_CHECKOUT(
      PracticePlace.HOTEL,
      2,
      "늦은 체크아웃 묻기",
      "늦게 체크아웃할 수 있는지, 추가 요금이 있는지 물어요.",
      "The learner is a guest staying in room 305 who wants to leave later than usual. Check-out is normally by 11 a.m. You can extend check-out until 1 p.m. for an extra $15. Later than 1 p.m. is not possible because the room is booked for the next guest; only if they ask for a later time, tell them they can leave their luggage at the front desk for free after checking out.",
      goal(
          "ask_late_checkout",
          "늦은 체크아웃 부탁하기",
          "The learner asks to check out later than usual or asks whether a late check-out is possible. A specific time is optional.",
          "체크아웃을 늦게 해도 되는지 묻기"),
      goal(
          "ask_late_fee",
          "추가 요금 묻기",
          "The learner asks whether there is an extra charge for the late check-out or how much it costs.",
          "추가 요금이 있는지, 얼마인지 묻기")),
  HOTEL_WIFI_BREAKFAST(
      PracticePlace.HOTEL,
      3,
      "와이파이·조식 묻기",
      "와이파이 비밀번호와 조식 시간·장소를 물어요.",
      "The learner is a guest staying in room 305 with questions about the hotel. The Wi-Fi network is \"Hotel Guest\" and the password is \"sunflower7\". Breakfast is included in the stay. It is served from 7 to 10 a.m. in the restaurant on the first floor, next to the lobby.",
      goal(
          "ask_wifi_password",
          "와이파이 비밀번호 묻기",
          "The learner asks for the Wi-Fi password or how to connect to the Wi-Fi.",
          "와이파이 비밀번호 묻기"),
      goal(
          "ask_breakfast",
          "조식 시간·장소 묻기",
          "The learner asks what time breakfast is served or where it is served. Asking about either the time or the place counts.",
          "조식이 몇 시에, 어디서 나오는지 묻기")),
  CAFE_RECOMMEND(
      PracticePlace.CAFE,
      1,
      "메뉴 추천받기",
      "달지 않은 음료를 추천해 달라고 하고 골라요.",
      "The learner is not sure what to order and wants a drink that is not sweet. When they describe the taste they want and ask for a recommendation, recommend the americano ($4) or a cappuccino with no sugar ($5); both can be hot or iced. Then let them choose.",
      goal(
          "ask_recommendation",
          "원하는 맛 말하고 추천 부탁하기",
          "The learner says what kind of taste they want, such as not sweet, AND asks for a recommendation. Asking only 'What do you recommend?' without any taste does not count.",
          "달지 않은 음료를 추천해 달라고 하기"),
      goal(
          "choose_recommended",
          "추천받은 것 중 고르기",
          "The learner chooses one of the recommended drinks, by name or clearly, such as 'I'll have the americano.'",
          "추천받은 음료 중 하나를 골라 주문하기")),
  CAFE_CHANGE_ORDER(
      PracticePlace.CAFE,
      2,
      "주문 바꾸기",
      "우유를 오트밀크로 바꾸거나 샷을 추가해 달라고 해요.",
      "The learner orders a drink and wants to change how it is made. Lattes and cappuccinos can be made with regular, soy or oat milk at no extra cost. An extra espresso shot is $1. Make the change they ask for and tell them the new price if it changed.",
      goal(
          "order_drink",
          "음료 주문하기",
          "The learner orders a specific drink by name, such as a latte or an americano. Asking for just 'a drink' does not count.",
          "마실 음료 이름을 말해 주문하기"),
      goal(
          "change_milk_or_shot",
          "우유 바꾸기나 샷 추가 부탁하기",
          "The learner asks to change the milk in their drink, such as to oat milk, OR asks to add an extra shot.",
          "오트밀크로 바꾸거나 샷을 추가해 달라고 하기")),
  CAFE_SEAT_OUTLET(
      PracticePlace.CAFE,
      3,
      "자리·콘센트 묻기",
      "노트북 쓸 자리와 콘센트, 와이파이를 물어요.",
      "The learner wants to work on a laptop in the cafe. The table by the back wall is free and has a power outlet under it. The window seats have no outlets. The Wi-Fi is free for customers: the network is \"Corner Cafe\" and the password is \"latte2026\".",
      goal(
          "ask_seat_outlet",
          "앉을 자리와 콘센트 묻기",
          "The learner asks where they can sit AND whether there is a power outlet or a place to charge their laptop. Asking only for any seat does not count.",
          "노트북을 쓸 자리와 콘센트가 있는지 묻기"),
      goal(
          "ask_wifi",
          "와이파이 묻기",
          "The learner asks about the Wi-Fi, such as whether there is Wi-Fi or what the password is.",
          "와이파이가 되는지, 비밀번호가 뭔지 묻기")),
  DINER_TABLE(
      PracticePlace.DINER,
      1,
      "자리 잡기",
      "몇 명인지 말하고 창가 자리를 부탁해요. 자리가 없으면 얼마나 기다리는지 물어요.",
      "The learner has just walked into the diner and is standing at the entrance, waiting for a table. All the window tables are taken right now; one should be free in about 10 minutes. A booth by the wall is free now if they do not want to wait.",
      goal(
          "say_party_window",
          "인원 말하고 창가 자리 부탁하기",
          "The learner says how many people are in their party AND asks for a window seat or table. The two details can come from different learner turns.",
          "몇 명인지 말하고 창가 자리를 부탁하기"),
      goal(
          "ask_wait_time",
          "기다리는 시간 묻기",
          "The learner asks how long they need to wait for a table.",
          "얼마나 기다려야 하는지 묻기")),
  DINER_ALLERGY(
      PracticePlace.DINER,
      2,
      "못 먹는 재료 말하기",
      "알레르기가 있는 재료를 말하고 빼 달라고 해요.",
      "The learner is ordering and has a food allergy. Pancakes ($7) come with butter and chopped walnuts on top. The cheeseburger ($9) comes with cheese, tomato and onion. The kitchen can leave out any topping the learner names. Confirm which dish they want and that it will be made without that ingredient.",
      goal(
          "say_allergy",
          "알레르기 재료 말하기",
          "The learner says they are allergic to a specific food or ingredient, such as nuts, cheese or eggs, or that they cannot eat it. Mentioning an allergy without naming the food does not count.",
          "알레르기가 있는 재료 말하기 (예: 견과류, 치즈)"),
      goal(
          "ask_leave_out",
          "빼 달라고 부탁하기",
          "The learner asks for that ingredient to be left out of or removed from their dish, such as 'No cheese, please.'",
          "그 재료를 빼 달라고 하기")),
  DINER_SPLIT_TO_GO(
      PracticePlace.DINER,
      3,
      "따로 계산하고 포장하기",
      "계산을 나눠 달라고 하고, 남은 음식을 싸 달라고 해요.",
      "The learner and a friend have finished eating and are ready to pay. They had two orders of pancakes ($7 each) and two coffees ($2 each), $18 in total, and some pancakes are left over. You can split the bill so each of them pays $9, and you can pack the leftovers in a to-go box for free.",
      goal(
          "ask_split_bill",
          "계산 나눠 달라고 하기",
          "The learner asks to split the bill or to pay separately.",
          "계산을 따로 하고 싶다고 말하기"),
      goal(
          "ask_to_go_box",
          "남은 음식 포장 부탁하기",
          "The learner asks to take the leftover food home, such as asking for a to-go box or a box for the leftovers.",
          "남은 음식을 싸 달라고 하기")),
  LAUNDRY_COINS(
      PracticePlace.LAUNDRY,
      1,
      "동전 바꾸기",
      "지폐를 동전으로 바꿀 수 있는지 물어요.",
      "The learner wants to use the machines but has only paper money. The machines take only quarters: a wash ($3) needs 12 quarters and a dryer ($2) needs 8 quarters. You can change bills into quarters at the counter; for example, a $5 bill becomes 20 quarters.",
      goal(
          "need_coins",
          "동전이 필요하다고 말하기",
          "The learner says they need coins or quarters for the machines, or that they have no coins.",
          "세탁기에 넣을 동전이 없다고 말하기"),
      goal(
          "ask_change_bill",
          "지폐를 바꿔 달라고 하기",
          "The learner asks to change a paper bill into coins, such as 'Can you change this five-dollar bill?'",
          "지폐를 동전으로 바꿔 달라고 하기")),
  LAUNDRY_BROKEN(
      PracticePlace.LAUNDRY,
      2,
      "멈춘 세탁기 말하기",
      "세탁기가 멈췄다고 말하고 도움을 청해요.",
      "The learner's washing machine stopped in the middle of the wash with water still inside, and the door is locked. Accept the machine number or place the learner gives. You can restart it with the reset button, or move their clothes to a free machine and start it again at no extra cost.",
      goal(
          "describe_broken_machine",
          "어느 기계가 어떻게 됐는지 말하기",
          "The learner says which machine has the problem, such as its number or where it is, AND what happened, such as it stopped or the door will not open.",
          "몇 번 세탁기가 멈췄는지 말하기"),
      goal(
          "ask_machine_help",
          "도움 청하기",
          "The learner asks for help with the machine, such as asking Gus to check or fix it, or asking what they should do.",
          "도와 달라고 부탁하기")),
  LAUNDRY_DRYER(
      PracticePlace.LAUNDRY,
      3,
      "건조기 시간 묻기",
      "건조기를 얼마나 돌려야 하는지, 어떤 온도가 좋은지 물어요.",
      "The learner's clothes are washed and they want to use a dryer, which costs $2. A normal load takes about 40 minutes to dry; towels and jeans may need about 50 minutes. Use low heat for shirts and delicate clothes so they do not shrink, and high heat for towels and jeans.",
      goal(
          "ask_drying_time",
          "건조 시간 묻기",
          "The learner asks how long they should run the dryer or how long drying takes.",
          "건조기를 얼마나 돌려야 하는지 묻기"),
      goal(
          "ask_dryer_heat",
          "온도 묻기",
          "The learner asks which heat or temperature setting to use for their clothes.",
          "어떤 온도로 돌리면 좋은지 묻기")),
  BANK_EXCHANGE(
      PracticePlace.BANK,
      1,
      "환전하기",
      "원화를 달러로 바꾸고 환율을 물어요.",
      "The learner wants to exchange Korean won for US dollars at the teller window. Today's rate is 1,400 won to one dollar, so 140,000 won becomes 100 dollars. There is no fee. Never ask for a PIN or password.",
      goal(
          "say_exchange_amount",
          "바꿀 돈과 금액 말하기",
          "The learner asks to exchange money, such as Korean won into dollars, AND says how much they want to exchange.",
          "원화를 달러로 바꾸고 싶다고 금액과 함께 말하기"),
      goal(
          "ask_exchange_rate",
          "환율 묻기",
          "The learner asks what today's exchange rate is or how many dollars they will get.",
          "오늘 환율이 얼마인지 묻기")),
  BANK_LOST_CARD(
      PracticePlace.BANK,
      2,
      "카드 분실 신고하기",
      "카드를 잃어버렸다고 말하고 정지를 부탁해요.",
      "The learner has lost their Town Bank card. You can block the card right away so nobody can use it, and a new card will be ready in five days. To find their account you need only their name; accept any name. Never ask for a PIN, password or card number.",
      goal(
          "report_lost_card",
          "카드를 잃어버렸다고 말하기",
          "The learner says they lost their bank card or that it was stolen.",
          "카드를 잃어버렸다고 말하기"),
      goal(
          "ask_block_card",
          "카드 정지 부탁하기",
          "The learner asks to block, stop, freeze or cancel the lost card.",
          "카드를 정지해 달라고 하기")),
  BANK_SMALL_BILLS(
      PracticePlace.BANK,
      3,
      "잔돈으로 바꾸기",
      "큰 지폐를 작은 지폐로 바꿔 달라고 해요.",
      "The learner has a 100-dollar bill and needs smaller bills. You can change it into any mix of 20-, 10-, 5- and 1-dollar bills. If they ask for change without saying which bills they want, ask how they would like it.",
      goal(
          "ask_break_bill",
          "큰 지폐 바꿔 달라고 하기",
          "The learner asks to change or break a large bill into smaller bills.",
          "100달러 지폐를 작은 돈으로 바꿔 달라고 하기"),
      goal(
          "say_bill_sizes",
          "원하는 단위 말하기",
          "The learner says which bills they want, such as five twenties or some tens and fives.",
          "어떤 지폐로 받고 싶은지 말하기 (예: 20달러 다섯 장)")),
  BARBER_BOOKING(
      PracticePlace.BARBER,
      1,
      "예약하기",
      "원하는 시간으로 예약하고 얼마나 걸리는지 물어요.",
      "The learner wants to book a haircut for another day. Walk-ins are welcome, but you also take bookings. Every day this week you have openings at 10 a.m., 2 p.m. and 4 p.m. If they ask for another time, offer the closest opening. A haircut costs $20 and takes about 30 minutes.",
      goal(
          "say_booking_time",
          "원하는 날짜·시간 말하기",
          "The learner asks to make an appointment AND says the day and time they want.",
          "예약하고 싶은 날짜와 시간 말하기"),
      goal(
          "ask_duration",
          "걸리는 시간 묻기",
          "The learner asks how long the haircut will take.",
          "머리 자르는 데 얼마나 걸리는지 묻기")),
  BARBER_TRIM(
      PracticePlace.BARBER,
      2,
      "끝만 다듬기",
      "길이는 두고 끝만 다듬어 달라고 해요.",
      "The learner is sitting in your barber chair for a haircut ($20). They like their current length. Cut exactly as they ask, such as about one centimeter off the ends.",
      goal(
          "keep_length",
          "길이는 그대로 두라고 하기",
          "The learner says they want to keep the length or do not want their hair much shorter.",
          "길이는 그대로 두고 싶다고 말하기"),
      goal(
          "trim_ends",
          "끝만 다듬어 달라고 하기",
          "The learner asks for just a trim of the ends, such as 'Just a little off the ends, please.'",
          "끝만 조금 다듬어 달라고 하기")),
  BARBER_FIX(
      PracticePlace.BARBER,
      3,
      "고쳐 달라고 하기",
      "마음에 안 드는 부분을 정중히 말하고 고쳐 달라고 해요.",
      "You have just finished the learner's haircut and are holding up the mirror. Accept whatever part the learner says is not right, such as one side being longer. Apologize and fix it for free without arguing.",
      goal(
          "point_out_problem",
          "마음에 안 드는 부분 말하기",
          "The learner says which part of the haircut they do not like, such as one side being longer or the bangs being too short.",
          "어느 부분이 마음에 안 드는지 말하기 (예: 한쪽이 더 길어요)"),
      goal(
          "ask_fix_politely",
          "정중하게 고쳐 달라고 하기",
          "The learner politely asks the barber to fix it, such as with 'Could you...', 'Would you mind...' or 'please'. A rude or commanding request does not count.",
          "Could you ~?처럼 정중하게 고쳐 달라고 하기")),
  FLORIST_PLANT(
      PracticePlace.FLORIST,
      1,
      "화분 추천받기",
      "키우기 쉬운 화분을 추천해 달라고 해요.",
      "The learner wants a potted plant. Besides cut flowers, today you have small potted plants for $12: a snake plant and a pothos. Both are easy for beginners and fine in a room with little sun. Water either one about once a week, only when the top of the soil is dry.",
      goal(
          "say_plant_needs",
          "원하는 화분 조건 말하기",
          "The learner asks for a potted plant AND says what they need from it, such as easy to care for, small, or good for a room with little sun.",
          "키우기 쉬운 화분을 찾는다고 말하기"),
      goal(
          "ask_watering",
          "물 주는 법 묻기",
          "The learner asks how often or how much to water the plant.",
          "물을 얼마나 자주 줘야 하는지 묻기")),
  FLORIST_DELIVERY(
      PracticePlace.FLORIST,
      2,
      "배달 부탁하기",
      "꽃을 호텔로 배달해 달라고 하고 시간을 정해요.",
      "The learner has chosen a bouquet ($15) and wants it delivered. You do deliver anywhere in town for $5, including the hotel on the main street. Deliveries go out between 2 p.m. and 6 p.m. today, and the learner can pick a time in that window.",
      goal(
          "say_delivery_place",
          "배달할 곳 말하기",
          "The learner asks for the flowers to be delivered AND says where, such as the hotel.",
          "꽃을 호텔로 배달해 달라고 하기"),
      goal(
          "set_delivery_time",
          "배달 시간 정하기",
          "The learner says or agrees on a delivery time, such as 'Around 3 p.m., please.'",
          "언제 배달해 줬으면 좋겠는지 말하기")),
  FLORIST_CARD(
      PracticePlace.FLORIST,
      3,
      "카드 문구 부탁하기",
      "꽃에 넣을 카드에 짧은 문구를 써 달라고 해요.",
      "The learner is buying a bouquet ($15) as a gift. You can add a small message card for free and write a short message on it for them. Write exactly the message the learner tells you and read it back to check.",
      goal(
          "ask_message_card",
          "카드를 넣어 달라고 하기",
          "The learner asks to add a message card to the flowers.",
          "꽃에 카드를 넣어 달라고 하기"),
      goal(
          "say_card_message",
          "쓸 문구 말하기",
          "The learner tells the florist the message to write on the card, such as 'Happy birthday, Mom.'",
          "카드에 쓸 짧은 문구 말하기 (예: Happy birthday!)")),
  BAKERY_FRESH(
      PracticePlace.BAKERY,
      1,
      "갓 나온 빵 고르기",
      "지금 막 나온 빵이 뭔지 묻고 골라요.",
      "The learner wants something freshly baked. The croissants ($3 each) and the cinnamon rolls ($4 each) came out of the oven ten minutes ago and are still warm. The loaves of bread ($4) were baked early this morning.",
      goal(
          "ask_fresh_bread",
          "갓 나온 빵 묻기",
          "The learner asks which bread or pastry is fresh out of the oven or was just baked.",
          "지금 막 나온 빵이 뭔지 묻기"),
      goal(
          "buy_fresh_bread",
          "골라서 사기",
          "The learner chooses a specific bakery item to buy, by name or clearly, such as 'Two croissants, please.'",
          "골라서 몇 개 살지 말하기")),
  BAKERY_CAKE(
      PracticePlace.BAKERY,
      2,
      "케이크 주문하기",
      "생일 케이크를 주문하고 찾으러 올 날을 말해요.",
      "The learner wants to order a birthday cake. You make whole cakes to order in chocolate or strawberry, small (for 6 people, $25) or large (for 12 people, $40), and you can write a short name on top. A cake needs one day, so the earliest pickup is tomorrow.",
      goal(
          "describe_cake",
          "원하는 케이크 말하기",
          "The learner orders a birthday cake AND says what kind they want, such as the flavor or the size.",
          "어떤 케이크(맛이나 크기)를 원하는지 말하기"),
      goal(
          "say_pickup_time",
          "찾으러 올 날짜·시간 말하기",
          "The learner says when they will pick up the cake, such as a day or a time.",
          "언제 찾으러 올지 날짜나 시간 말하기")),
  BAKERY_NUTS(
      PracticePlace.BAKERY,
      3,
      "견과류 묻기",
      "견과류가 들어갔는지 묻고 없는 빵을 골라요.",
      "The learner cannot eat nuts. The walnut bread and the almond croissants have nuts in them. The plain croissants ($3), the white bread ($4 a loaf) and the chocolate cake ($5 a slice) have no nuts.",
      goal(
          "ask_about_nuts",
          "견과류가 들어갔는지 묻기",
          "The learner asks whether an item has nuts in it or which items have no nuts.",
          "견과류가 들어 있는지 묻기"),
      goal(
          "choose_nut_free",
          "견과류 없는 빵 고르기",
          "The learner chooses an item without nuts to buy.",
          "견과류가 없는 빵을 골라 사기")),
  DRUG_MEDICINE(
      PracticePlace.DRUGSTORE,
      1,
      "약 추천받기",
      "증상을 말하고 약을 추천받아요.",
      "The learner feels sick and wants medicine. For a headache or a fever, recommend pain reliever tablets ($6): two tablets every six hours after food, no more than eight a day. For a cold or a sore throat, recommend throat lozenges ($4): one every two to three hours. For a stomachache, recommend stomach tablets ($5): one after each meal. If the symptoms sound serious, suggest seeing a doctor.",
      goal(
          "describe_symptom",
          "증상 말하기",
          "The learner says what symptom they have, such as a headache, a fever, a cough or a stomachache.",
          "어디가 아픈지 증상 말하기 (예: 머리가 아파요)"),
      goal(
          "ask_dosage",
          "먹는 법 묻기",
          "The learner asks how to take the medicine, such as how many to take or how often.",
          "약을 언제, 몇 개씩 먹는지 묻기")),
  DRUG_FIND(
      PracticePlace.DRUGSTORE,
      2,
      "물건 찾기",
      "선크림이 어디 있는지 물어요.",
      "The learner is looking for sunscreen. Sunscreen is in aisle 2, next to the sunglasses. There is a small 50 ml tube ($8), a large 150 ml bottle ($15) and a spray ($12), all SPF 50.",
      goal(
          "ask_item_location",
          "찾는 물건 묻기",
          "The learner asks where to find sunscreen or another item they are looking for.",
          "선크림이 어디 있는지 묻기"),
      goal(
          "ask_other_option",
          "다른 크기나 종류 묻기",
          "The learner asks whether the item comes in another size or type, such as a bigger bottle or a spray.",
          "더 큰 것이나 다른 종류가 있는지 묻기")),
  DRUG_REFUND(
      PracticePlace.DRUGSTORE,
      3,
      "환불하기",
      "영수증을 보여 주고 산 물건을 환불해 달라고 해요.",
      "The learner bought something here yesterday and has the receipt. Unopened items can be refunded within 14 days with the receipt, back to the same payment method. Accept whatever item and reason the learner gives. Ask for the receipt only if they have not mentioned it.",
      goal(
          "ask_refund",
          "환불하고 싶다고 말하기",
          "The learner says they want a refund or want to return an item they bought.",
          "산 물건을 환불하고 싶다고 말하기"),
      goal(
          "give_refund_reason",
          "이유 말하기",
          "The learner says why they want to return it, such as it is the wrong size, it does not work, or they do not need it.",
          "왜 환불하고 싶은지 이유 말하기")),
  HOME_OPTIONS(
      PracticePlace.HOME_STORE,
      1,
      "다른 색·크기 묻기",
      "다른 색이나 작은 크기가 있는지 물어요.",
      "The learner is looking around the store. Floor lamps ($30) come in white, black and green, and there is also a small desk lamp ($20). Cushions ($12) come in blue, gray and yellow, in small and large. Bath towels ($10) come in white and navy.",
      goal(
          "say_item_wanted",
          "찾는 물건 말하기",
          "The learner says what item they are looking for or interested in, such as a lamp or a cushion.",
          "어떤 물건을 찾는지 말하기"),
      goal(
          "ask_color_size",
          "다른 색·크기 묻기",
          "The learner asks whether the item comes in another color or a different size, such as a smaller one.",
          "다른 색이나 더 작은 크기가 있는지 묻기")),
  HOME_DISCOUNT(
      PracticePlace.HOME_STORE,
      2,
      "할인 묻기",
      "세일 중인지, 쿠폰을 쓸 수 있는지 물어요.",
      "The learner is about to buy a lamp ($30). This week all lamps are 20 percent off, so the lamp is $24. The learner has a $5 coupon from the town newspaper; you accept it on top of the sale, so the lamp would be $19.",
      goal(
          "ask_on_sale",
          "세일 중인지 묻기",
          "The learner asks whether an item is on sale or whether there is a discount.",
          "세일 중인지 묻기"),
      goal(
          "ask_use_coupon",
          "쿠폰 써도 되는지 묻기",
          "The learner asks whether they can use a coupon.",
          "쿠폰을 쓸 수 있는지 묻기")),
  HOME_EXCHANGE(
      PracticePlace.HOME_STORE,
      3,
      "교환하기",
      "깨진 컵을 새것으로 바꿔 달라고 해요.",
      "The learner bought a set of two glass cups ($8) here yesterday and has the receipt. The same cups are in stock, and you can exchange a damaged set for a new one right away.",
      goal(
          "explain_damage",
          "무엇이 어떻게 됐는지 말하기",
          "The learner says which item has a problem AND what is wrong with it, such as a cup that was broken or cracked.",
          "산 컵이 깨져 있었다고 말하기"),
      goal(
          "ask_exchange",
          "새것으로 바꿔 달라고 하기",
          "The learner asks to exchange it for a new one or to get a replacement.",
          "새것으로 바꿔 달라고 하기")),
  ARCADE_TOKENS(
      PracticePlace.ARCADE,
      1,
      "토큰 사기",
      "얼마에 몇 개인지 묻고 토큰을 사요.",
      "The learner wants game tokens. One token is $1 and plays one claw machine game. Five tokens are $4 and ten tokens are $7. You sell tokens at the counter for cash or card.",
      goal(
          "ask_token_price",
          "토큰 가격 묻기",
          "The learner asks how much tokens cost or how many tokens they get for an amount of money.",
          "토큰이 얼마인지 묻기"),
      goal(
          "buy_tokens",
          "원하는 만큼 사기",
          "The learner says how many tokens they want to buy or how much money's worth.",
          "토큰을 몇 개 살지 말하기")),
  ARCADE_STUCK(
      PracticePlace.ARCADE,
      2,
      "고장 난 기계 말하기",
      "동전을 넣었는데 기계가 안 된다고 말해요.",
      "A machine took the learner's token but did not start. Accept the machine the learner describes. You can give them a free token back right away or restart the machine so they can play again.",
      goal(
          "describe_stuck_machine",
          "어느 기계에서 무슨 일이 있었는지 말하기",
          "The learner says which machine, such as the claw machine by the door, AND what happened, such as it took their token or coin but did not start.",
          "어느 기계가 동전만 먹고 안 됐는지 말하기"),
      goal(
          "ask_refund_or_replay",
          "돈을 돌려받거나 다시 하게 해 달라고 하기",
          "The learner asks to get their token or money back, or to play again.",
          "토큰을 돌려 달라거나 다시 하게 해 달라고 하기")),
  ARCADE_PRIZE(
      PracticePlace.ARCADE,
      3,
      "상품 바꾸기",
      "모은 티켓으로 상품을 바꿔요.",
      "The learner has collected prize tickets from the games; count them as 120 tickets. Prizes: a key ring for 50 tickets, a small stuffed bear for 100 tickets, a toy car for 120 tickets and a big stuffed rabbit for 300 tickets.",
      goal(
          "ask_prize_options",
          "티켓으로 바꿀 수 있는 상품 묻기",
          "The learner asks what prizes they can get with their tickets.",
          "티켓으로 무엇을 받을 수 있는지 묻기"),
      goal(
          "choose_prize", "원하는 상품 고르기", "The learner chooses a specific prize.", "원하는 상품을 골라 말하기")),
  POST_STAMPS(
      PracticePlace.POST_OFFICE,
      1,
      "우표 사기",
      "한국으로 보낼 엽서 우표를 사요.",
      "The learner wants to send postcards. A stamp for a postcard to another country, such as Korea, is $1.50; inside the country it is 60 cents. Postcards to Korea take about 7 to 10 days.",
      goal(
          "say_postcard_destination",
          "엽서를 어디로 보낼지 말하기",
          "The learner says they want to send a postcard AND where it is going, such as Korea.",
          "엽서를 한국으로 보낸다고 말하기"),
      goal(
          "buy_stamps",
          "우표 사기",
          "The learner asks to buy stamps for the postcards, such as saying how many they need.",
          "우표를 몇 장 사겠다고 말하기")),
  POST_PICKUP(
      PracticePlace.POST_OFFICE,
      2,
      "소포 찾기",
      "받을 소포가 왔는지 이름을 대고 물어요.",
      "The learner is here to pick up a package. Packages are kept under the receiver's name, and a package is waiting under whatever name the learner gives. Hand it over once they have given their name. Do not ask for an ID.",
      goal(
          "say_pickup_package",
          "소포를 찾으러 왔다고 말하기",
          "The learner says they are here to pick up a package or asks whether a package has arrived for them.",
          "소포를 찾으러 왔다고 말하기"),
      goal(
          "give_name",
          "이름 말하기",
          "The learner gives the name the package is under. Any name counts.",
          "소포를 받을 사람 이름 말하기")),
  POST_SHIPPING(
      PracticePlace.POST_OFFICE,
      3,
      "배송 방법 고르기",
      "언제 도착하는지 묻고 배송 방법을 골라요.",
      "The learner wants to send a small package overseas, such as to Korea. Overseas standard shipping is $15 and takes about 10 days; express shipping is $35 and takes about 3 days. You weigh the package before sending it.",
      goal(
          "ask_arrival_time",
          "도착 날짜 묻기",
          "The learner asks when the package will arrive or how long delivery takes.",
          "언제 도착하는지 묻기"),
      goal(
          "choose_shipping",
          "배송 방법 고르기",
          "The learner chooses a shipping option, such as standard or express.",
          "일반 배송과 빠른 배송 중 하나 고르기"));

  private final PracticePlace place;
  private final int number;
  private final String title;
  private final String description;
  private final String instruction;
  private final List<Goal> goals;

  PracticeScenario(
      PracticePlace place,
      int number,
      String title,
      String description,
      String instruction,
      Goal... goals) {
    this.place = place;
    this.number = number;
    this.title = title;
    this.description = description;
    this.instruction = instruction;
    this.goals = List.of(goals);
  }

  private static Goal goal(String id, String title, String description, String hint) {
    return new Goal(id, title, description, hint);
  }

  /** API의 scenarioId로 상황을 찾는다. 없는 ID면 빈 값이다. */
  public static Optional<PracticeScenario> find(String scenarioId) {
    return Arrays.stream(values())
        .filter(scenario -> scenario.name().equals(scenarioId))
        .findFirst();
  }

  /** 이 장소의 상황. 번호 순서다. */
  public static List<PracticeScenario> of(PracticePlace place) {
    return Arrays.stream(values()).filter(scenario -> scenario.place == place).toList();
  }

  public PracticePlace place() {
    return place;
  }

  /** 가게 안에서의 순서(1~3). */
  public int number() {
    return number;
  }

  public String title() {
    return title;
  }

  /** 목록에 보일 한국어 설명. */
  public String description() {
    return description;
  }

  /** 직원 NPC에게 주는 영어 상황. 의도 판정의 상황 설명에도 쓴다. */
  public String instruction() {
    return instruction;
  }

  public List<Goal> goals() {
    return goals;
  }

  /** 아직 이루지 않은 목표. 목표 순서를 지킨다. */
  public List<Goal> remainingGoals(List<String> completedGoalIds) {
    return goals.stream().filter(goal -> !completedGoalIds.contains(goal.id())).toList();
  }

  /**
   * 상황 목표.
   *
   * @param title 목록·대화창에 체크와 함께 보일 한국어 목표
   * @param description 의도 판정 기준(영어)
   * @param hint 이 목표를 이루려면 무엇을 말하면 되는지(한국어)
   */
  public record Goal(String id, String title, String description, String hint) {}
}
