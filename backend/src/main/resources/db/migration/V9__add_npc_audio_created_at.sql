ALTER TABLE turns ADD COLUMN npc_audio_created_at timestamp(6) with time zone;

-- 기존 음성의 실제 생성 시각은 복원할 수 없다. 보관 기간을 새로 시작하지 않도록 턴 생성 시각을 사용한다.
UPDATE turns SET npc_audio_created_at = created_at WHERE npc_audio_key IS NOT NULL;
