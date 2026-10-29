-- 녹음 음성 학습 활용(TRAINING_VOICE)에 동의한 가입자의 녹음 기록을 JSON 한 줄씩 내보낸다.
-- 녹음은 동의한 동안에만 저장되고 철회하면 지워지지만, 문구 버전(:version)이 바뀐 뒤 다시 동의하지 않은 사용자의 녹음은 뺀다.
-- 파일은 audio_key 이름으로 학습용 녹음 폴더(TRAINING_RECORDING_DIR)에 있다. 실행 방법은 docs/ai-training-data.md를 따른다.
select json_build_object(
           'user_id', r.user_id,
           'request_id', r.request_id,
           'purpose', r.purpose,
           'conversation_id', r.conversation_id,
           'audio_key', r.audio_key,
           'content_type', r.content_type,
           'stt_text', r.stt_text,
           'duration_ms', r.duration_ms,
           'recorded_at', r.recorded_at)
from training_recordings r
where exists (
    select 1
    from user_consents uc
    where uc.user_id = r.user_id
      and uc.consent_type = 'TRAINING_VOICE'
      and uc.version = :'version'
      and uc.withdrawn_at is null)
order by r.id;
