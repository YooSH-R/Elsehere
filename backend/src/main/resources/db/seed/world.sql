-- 장소·NPC·사물·어휘·미션 시드. WorldSeedLoader가 서버 시작 때 실행한다.
-- 고유 키(code, unity_key 등)가 같으면 시드 값으로 갱신하므로 여러 번 실행해도 행이 늘지 않는다.
-- 코드와 unity_key는 Unity 씬·빌더에 쓴 값과 같아야 한다.

-- 장소. unity_key는 Unity 씬 이름이다.
INSERT INTO scenes (code, name, unity_key, is_open)
VALUES
  ('HOME', '집', 'HomeScene', true),
  ('TOWN', '마을', 'TownScene', true),
  ('CAFE', '모퉁이 카페', 'CafeScene', true),
  ('HOTEL', '호텔', 'HotelScene', false)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    unity_key = EXCLUDED.unity_key,
    is_open = EXCLUDED.is_open,
    updated_at = now();

-- 사물 어휘. 사물 카드에 보여주는 단어·뜻·예문이다.
INSERT INTO vocabularies (language_code, type, text, meaning_ko, example, example_ko, source)
VALUES
  ('en-US', 'WORD', 'TV', '텔레비전', 'Let''s watch TV together.', '같이 TV 보자.', 'MASTER'),
  ('en-US', 'WORD', 'refrigerator', '냉장고', 'The milk is in the refrigerator.', '우유는 냉장고에 있어요.', 'MASTER'),
  ('en-US', 'WORD', 'air conditioner', '에어컨', 'Can you turn on the air conditioner?', '에어컨 좀 켜 줄래요?', 'MASTER'),
  ('en-US', 'WORD', 'sofa', '소파', 'He is sitting on the sofa.', '그는 소파에 앉아 있어요.', 'MASTER'),
  ('en-US', 'WORD', 'bookshelf', '책장', 'The bookshelf is full of books.', '책장에 책이 가득해요.', 'MASTER'),
  ('en-US', 'WORD', 'bed', '침대', 'I make my bed every morning.', '나는 매일 아침 침대를 정리해요.', 'MASTER'),
  ('en-US', 'WORD', 'counter', '계산대', 'Please order at the counter.', '계산대에서 주문해 주세요.', 'MASTER'),
  ('en-US', 'WORD', 'menu', '메뉴', 'Can I see the menu, please?', '메뉴 좀 볼 수 있을까요?', 'MASTER')
ON CONFLICT (language_code, text) DO UPDATE
SET type = EXCLUDED.type,
    meaning_ko = EXCLUDED.meaning_ko,
    example = EXCLUDED.example,
    example_ko = EXCLUDED.example_ko,
    source = EXCLUDED.source,
    updated_at = now();

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

-- 일일 미션. code는 Unity 미션 콘텐츠와 같은 키다. 튜토리얼 미션은 Unity 대본 콘텐츠라 넣지 않는다.
-- intent_rule은 임시값이다. AI 의도 판정에 쓰는 실제 기준은 대화 담당이 채운다.
INSERT INTO missions (npc_id, code, title, description, keywords, intent_rule)
SELECT npc.id, seed.code, seed.title, seed.description, seed.keywords::jsonb, seed.intent_rule
FROM (
  VALUES
    ('CAFE_STAFF', 'CAFE_SOY_LATTE', '나만의 카페라테 주문하기',
     '우유 대신 두유를 넣은 카페라테를 주문해 보세요.', '["soy", "latte"]',
     'The learner orders a cafe latte made with soy milk instead of regular milk.'),
    ('CAFE_STAFF', 'CAFE_DESSERT', '달콤한 디저트 찾기',
     '카페 직원에게 디저트를 파는지 물어보세요.', '["dessert"]',
     'The learner asks the barista whether the cafe sells desserts.')
) AS seed (npc_code, code, title, description, keywords, intent_rule)
JOIN npcs npc ON npc.code = seed.npc_code
ON CONFLICT (code) DO UPDATE
SET npc_id = EXCLUDED.npc_id,
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    keywords = EXCLUDED.keywords,
    intent_rule = EXCLUDED.intent_rule,
    updated_at = now();
