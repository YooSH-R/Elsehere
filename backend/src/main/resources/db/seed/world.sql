-- 장소·NPC·사물·어휘·미션 시드. WorldSeedLoader가 서버 시작 때 실행한다.
-- 고유 키(code, unity_key 등)가 같으면 시드 값으로 갱신하므로 여러 번 실행해도 행이 늘지 않는다.
-- 코드와 unity_key는 Unity 씬·빌더에 쓴 값과 같아야 한다.

-- 장소. unity_key는 Unity 씬 이름이다.
INSERT INTO scenes (code, name, unity_key, is_open)
VALUES
  ('HOME', '집', 'HomeScene', true),
  ('TOWN', '마을', 'TownScene', true),
  ('CAFE', '모퉁이 카페', 'CafeScene', true),
  ('HOTEL', '호텔', 'HotelScene', true)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    unity_key = EXCLUDED.unity_key,
    is_open = EXCLUDED.is_open,
    updated_at = now();

-- 사물 어휘. 사물 카드에 보여주는 단어·뜻·예문이다.
-- BEGIN GENERATED OBJECT WORDS: infra/scripts/sync-object-words.ps1
INSERT INTO vocabularies (language_code, type, text, meaning_ko, example, example_ko, source, pronunciation_audio_key)
VALUES
  ('en-US', 'WORD', 'TV', '텔레비전', 'Let''s watch TV together.', '같이 TV 보자.', 'MASTER', NULL),
  ('en-US', 'WORD', 'refrigerator', '냉장고', 'The milk is in the refrigerator.', '우유는 냉장고에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'air conditioner', '에어컨', 'Can you turn on the air conditioner?', '에어컨 좀 켜 줄래요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'sofa', '소파', 'He is sitting on the sofa.', '그는 소파에 앉아 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'bookshelf', '책장', 'The bookshelf is full of books.', '책장에 책이 가득해요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'bed', '침대', 'I make my bed every morning.', '나는 매일 아침 침대를 정리해요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'counter', '계산대', 'Please order at the counter.', '계산대에서 주문해 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'menu', '메뉴', 'Can I see the menu, please?', '메뉴 좀 볼 수 있을까요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'bench', '벤치', 'Let''s sit on the bench.', '벤치에 앉아요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'table', '탁자', 'Please put the cup on the table.', '컵을 탁자 위에 놓아 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'chair', '의자', 'Is this chair taken?', '이 의자에 누가 앉아 있나요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'window', '창문', 'Please open the window.', '창문을 열어 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'curtain', '커튼', 'Close the curtain before you go to bed.', '자기 전에 커튼을 닫으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'door', '문', 'Please close the door quietly.', '문을 조용히 닫아 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'wall', '벽', 'There is a clock on the wall.', '벽에 시계가 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'floor', '바닥', 'The floor is clean.', '바닥이 깨끗해요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'ceiling', '천장', 'A light hangs from the ceiling.', '천장에 등이 매달려 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'roof', '지붕', 'The house has a blue roof.', '그 집에는 파란 지붕이 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'stairs', '계단', 'The stairs lead to the second floor.', '계단은 2층으로 이어져요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'column', '기둥', 'Wait beside the column.', '기둥 옆에서 기다리세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'porch', '현관 앞 베란다', 'We are sitting on the porch.', '우리는 현관 앞 베란다에 앉아 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'awning', '차양', 'Let''s stand under the awning.', '차양 아래에 서 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'cornice', '처마 장식', 'The cornice runs along the top of the wall.', '처마 장식이 벽의 윗부분을 따라 이어져요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'building', '건물', 'That building has three floors.', '저 건물은 3층이에요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'house', '집', 'My house is near the cafe.', '우리 집은 카페 근처에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'cafe', '카페', 'Let''s meet at the cafe.', '카페에서 만나요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'hotel', '호텔', 'We are staying at this hotel.', '우리는 이 호텔에 머물고 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'shop', '가게', 'The shop opens at nine.', '그 가게는 아홉 시에 문을 열어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'road', '도로', 'Look both ways before crossing the road.', '도로를 건너기 전에 양쪽을 살펴보세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'sidewalk', '인도', 'Please walk on the sidewalk.', '인도로 걸어 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'crosswalk', '횡단보도', 'Use the crosswalk to cross the road.', '횡단보도로 길을 건너세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'fence', '울타리', 'There is a fence around the garden.', '정원 주위에 울타리가 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'tree', '나무', 'Let''s rest under the tree.', '나무 아래에서 쉬어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'flowerbed', '화단', 'The flowerbed is beside the house.', '화단은 집 옆에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'planter', '화분 용기', 'This planter is full of flowers.', '이 화분 용기에는 꽃이 가득해요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'plant', '식물', 'Please water the plant.', '식물에 물을 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'lamp', '등', 'Please turn on the lamp.', '등을 켜 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'streetlight', '가로등', 'The streetlight comes on at night.', '밤이 되면 가로등이 켜져요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'clock', '시계', 'The clock says three o''clock.', '시계가 세 시를 가리켜요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'chandelier', '샹들리에', 'There is a chandelier in the lobby.', '로비에 샹들리에가 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'cabinet', '수납장', 'The plates are in the cabinet.', '접시는 수납장 안에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'sink', '싱크대', 'Please wash the cup in the sink.', '싱크대에서 컵을 씻어 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'faucet', '수도꼭지', 'Turn off the faucet after washing your hands.', '손을 씻은 뒤 수도꼭지를 잠그세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'stove', '레인지', 'There is a kettle on the stove.', '레인지 위에 주전자가 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'kettle', '주전자', 'Fill the kettle with water.', '주전자에 물을 채우세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'rug', '깔개', 'There is a rug under the table.', '탁자 아래에 깔개가 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'mug', '머그잔', 'I drink tea from this mug.', '나는 이 머그잔으로 차를 마셔요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'cup', '컵', 'May I have a cup of coffee?', '커피 한 잔 주시겠어요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'plate', '접시', 'Put the bread on the plate.', '빵을 접시에 놓으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'tray', '쟁반', 'Carry the cups on a tray.', '컵을 쟁반에 담아 옮기세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'book', '책', 'I am reading a book.', '나는 책을 읽고 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'picture', '그림', 'There is a picture above the sofa.', '소파 위쪽에 그림이 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'mirror', '거울', 'Look in the mirror.', '거울을 보세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'pillow', '베개', 'There is a pillow on the bed.', '침대 위에 베개가 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'blanket', '담요', 'May I have another blanket?', '담요를 하나 더 받을 수 있을까요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'vase', '꽃병', 'Put the flowers in the vase.', '꽃을 꽃병에 꽂으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'trash can', '쓰레기통', 'Put the empty cup in the trash can.', '빈 컵을 쓰레기통에 넣으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'fire hydrant', '소화전', 'Do not park in front of the fire hydrant.', '소화전 앞에 주차하지 마세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'truck', '트럭', 'The truck is delivering bread.', '트럭이 빵을 배달하고 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'fountain', '분수', 'Let''s meet by the fountain.', '분수 옆에서 만나요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'hedge', '생울타리', 'A hedge surrounds the park.', '생울타리가 공원을 둘러싸고 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'crate', '나무 상자', 'The apples are in a crate.', '사과가 나무 상자 안에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'sign', '표지판', 'Read the sign beside the door.', '문 옆의 표지판을 읽으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'chalkboard', '칠판', 'Today''s special is on the chalkboard.', '오늘의 특별 메뉴는 칠판에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'coffee machine', '커피 머신', 'The coffee machine is behind the counter.', '커피 머신은 계산대 뒤에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'cash register', '금전 등록기', 'Please pay at the cash register.', '금전 등록기가 있는 곳에서 계산해 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'display case', '진열장', 'The cakes are in the display case.', '케이크는 진열장 안에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'pastry stand', '제과 진열대', 'The muffins are on the pastry stand.', '머핀은 제과 진열대에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'price card', '가격표', 'Check the price card before you order.', '주문하기 전에 가격표를 확인하세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'takeaway cup', '포장용 컵', 'Could I have a takeaway cup?', '포장용 컵을 받을 수 있을까요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'bread', '빵', 'This bread is fresh.', '이 빵은 신선해요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'muffin', '머핀', 'I''d like a muffin, please.', '머핀 하나 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'donut', '도넛', 'This donut is sweet.', '이 도넛은 달아요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'croissant', '크루아상', 'I''d like a croissant with my coffee.', '커피와 함께 크루아상을 먹고 싶어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'cake', '케이크', 'May I have a slice of cake?', '케이크 한 조각 주시겠어요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'suitcase', '여행 가방', 'My suitcase is beside the desk.', '제 여행 가방은 데스크 옆에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'luggage cart', '짐 운반 카트', 'Put your suitcase on the luggage cart.', '여행 가방을 짐 운반 카트에 올려놓으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'reception desk', '안내 데스크', 'Please check in at the reception desk.', '안내 데스크에서 체크인해 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'guestbook', '방명록', 'Please sign the guestbook.', '방명록에 서명해 주세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'bell', '종', 'Ring the bell if you need help.', '도움이 필요하면 종을 울리세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'elevator', '엘리베이터', 'Take the elevator to the second floor.', '엘리베이터를 타고 2층으로 가세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'key cabinet', '열쇠 보관함', 'The room keys are in the key cabinet.', '객실 열쇠는 열쇠 보관함 안에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'key', '열쇠', 'May I have my room key?', '제 객실 열쇠를 받을 수 있을까요?', 'MASTER', NULL),
  ('en-US', 'WORD', 'railing', '난간', 'Hold the railing on the stairs.', '계단에서 난간을 잡으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'bollard', '차량 진입 방지 기둥', 'The bollards keep cars off the sidewalk.', '차량 진입 방지 기둥이 자동차의 인도 진입을 막아요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'jar', '단지', 'The jar is on the shelf.', '단지는 선반 위에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'shelf', '선반', 'Put the cup on the shelf.', '컵을 선반 위에 놓으세요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'entrance', '입구', 'The entrance is next to the window.', '입구는 창문 옆에 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'shutter', '셔터', 'The shop''s shutter is closed.', '가게의 셔터가 닫혀 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'cloud', '구름', 'A white cloud floats above the town.', '마을 위로 하얀 구름이 떠 있어요.', 'MASTER', NULL),
  ('en-US', 'WORD', 'kitchen counter', '주방 조리대', 'Put the vegetables on the kitchen counter.', '채소를 주방 조리대 위에 놓으세요.', 'MASTER', NULL)
ON CONFLICT (language_code, text) DO UPDATE
SET type = EXCLUDED.type,
    meaning_ko = EXCLUDED.meaning_ko,
    example = EXCLUDED.example,
    example_ko = EXCLUDED.example_ko,
    source = EXCLUDED.source,
    pronunciation_audio_key = EXCLUDED.pronunciation_audio_key,
    updated_at = now();
-- END GENERATED OBJECT WORDS

-- 사물. unity_key는 Unity 씬의 HoverableObject에 넣은 키다.
INSERT INTO world_objects (scene_id, unity_key, vocab_id)
SELECT scene.id, seed.unity_key, vocabulary.id
FROM (
  VALUES
    ('HOME', 'tv', 'TV'),
    ('HOME', 'refrigerator', 'refrigerator'),
    ('HOME', 'air_conditioner', 'air conditioner'),
    ('HOME', 'sofa', 'sofa'),
    ('HOME', 'bookshelf', 'bookshelf'),
    ('HOME', 'bed', 'bed'),
    ('CAFE', 'counter', 'counter'),
    ('CAFE', 'menu', 'menu')
) AS seed (scene_code, unity_key, word)
JOIN scenes scene ON scene.code = seed.scene_code
JOIN vocabularies vocabulary ON vocabulary.language_code = 'en-US' AND vocabulary.text = seed.word
ON CONFLICT (unity_key) DO UPDATE
SET scene_id = EXCLUDED.scene_id,
    vocab_id = EXCLUDED.vocab_id,
    updated_at = now();

-- NPC. code는 Unity 씬의 NpcIdentity에 넣은 값이다. 튜토리얼 행인(TUTORIAL_PASSERBY)은 Unity 대본 콘텐츠라 넣지 않는다.
-- persona·voice_id는 임시값이다. AI 워커 npc 형식(role, facts, scene)에 맞춘 실제 값은 대화 담당이 채운다.
INSERT INTO npcs (scene_id, code, name, npc_type, role_label, persona, voice_id, can_initiate, approach_line)
SELECT scene.id, seed.code, seed.name, seed.npc_type, seed.role_label, seed.persona::jsonb, seed.voice_id, seed.can_initiate, seed.approach_line
FROM (
  VALUES
    ('TOWN', 'TOWN_PASSERBY', 'Ben', 'PASSERBY', '행인',
     '{"role": "a friendly local walking around the town.", "facts": "", "scene": "A quiet street in the town."}',
     'af_heart', true, 'Hi there! Nice weather today, isn''t it?'),
    ('CAFE', 'CAFE_STAFF', 'Mia', 'STAFF', '카페 직원',
     '{"role": "the barista at the corner cafe.", "facts": "", "scene": "The counter of the corner cafe."}',
     'af_heart', false, NULL),
    ('HOTEL', 'HOTEL_STAFF', 'Emma', 'STAFF', '호텔 직원',
     '{"role": "the front desk clerk at the hotel.", "facts": "", "scene": "The front desk of the hotel lobby."}',
     'af_heart', false, NULL)
) AS seed (scene_code, code, name, npc_type, role_label, persona, voice_id, can_initiate, approach_line)
JOIN scenes scene ON scene.code = seed.scene_code
ON CONFLICT (code) DO UPDATE
SET scene_id = EXCLUDED.scene_id,
    name = EXCLUDED.name,
    npc_type = EXCLUDED.npc_type,
    role_label = EXCLUDED.role_label,
    persona = EXCLUDED.persona,
    voice_id = EXCLUDED.voice_id,
    can_initiate = EXCLUDED.can_initiate,
    approach_line = EXCLUDED.approach_line,
    updated_at = now();

-- 일일 미션. 카페와 호텔에 하나씩 둔다. code는 Unity 미션 콘텐츠와 같은 키다. 튜토리얼 미션은 Unity 대본 콘텐츠라 넣지 않는다.
-- intent_rule은 임시값이다. AI 의도 판정에 쓰는 실제 기준은 대화 담당이 채운다.
INSERT INTO missions (npc_id, code, title, description, keywords, intent_rule)
SELECT npc.id, seed.code, seed.title, seed.description, seed.keywords::jsonb, seed.intent_rule
FROM (
  VALUES
    ('CAFE_STAFF', 'CAFE_SOY_LATTE', '나만의 카페라테 주문하기',
     '우유 대신 두유를 넣은 카페라테를 주문해 보세요.', '["soy", "latte"]',
     'The learner orders a cafe latte made with soy milk instead of regular milk.'),
    ('HOTEL_STAFF', 'HOTEL_CHECK_IN', '호텔에 체크인하기',
     '호텔 직원에게 예약을 확인하고 체크인해 보세요.', '["check in"]',
     'The learner asks the front desk clerk to check in to the hotel.')
) AS seed (npc_code, code, title, description, keywords, intent_rule)
JOIN npcs npc ON npc.code = seed.npc_code
ON CONFLICT (code) DO UPDATE
SET npc_id = EXCLUDED.npc_id,
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    keywords = EXCLUDED.keywords,
    intent_rule = EXCLUDED.intent_rule,
    updated_at = now();
