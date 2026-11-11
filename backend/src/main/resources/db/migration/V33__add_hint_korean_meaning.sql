ALTER TABLE turns ADD COLUMN hint_meaning_ko text;
COMMENT ON COLUMN turns.hint_meaning_ko IS '힌트 추천 문장의 한국어 뜻 · 기존 힌트는 NULL';
