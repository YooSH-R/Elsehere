-- 어드민 계정은 게임 사용자(users)가 아니므로, 어드민이 실행한 AI 호출(사물 단어 발음 만들기)은 사용자 없이(null) 기록한다.
alter table ai_requests alter column user_id drop not null;
