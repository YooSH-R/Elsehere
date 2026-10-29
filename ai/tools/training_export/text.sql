-- 대화 기록 학습 활용(TRAINING_TEXT)에 동의한 가입자의 대화 턴을 JSON 한 줄씩 내보낸다.
-- 지금 문구 버전(:version)에 동의했고 철회하지 않은 사용자의, 동의한 시각 이후에 시작한 대화만 담는다.
-- 이메일·닉네임은 넣지 않는다. 실행 방법은 docs/ai-training-data.md를 따른다.
with agreed as (
    select user_id, min(agreed_at) as agreed_at
    from user_consents
    where consent_type = 'TRAINING_TEXT'
      and version = :'version'
      and withdrawn_at is null
    group by user_id
)
select json_build_object(
           'user_id', c.user_id,
           'conversation_id', c.id,
           'npc_id', c.npc_id,
           'level', c.level_applied,
           'seq', t.seq,
           'speaker', t.speaker,
           'input_type', t.input_type,
           'text', t.text,
           'stt_raw_text', t.stt_raw_text,
           'guard_abusive', t.guard_abusive,
           'created_at', t.created_at)
from agreed a
join conversations c on c.user_id = a.user_id and c.started_at >= a.agreed_at
join turns t on t.conversation_id = c.id
order by c.id, t.seq;
