-- 이전 5분 마감 요청의 선점은 새 턴 제한에서 더 이상 유효하지 않다.
update conversations set time_limit_final_request_id = null where status = 'ACTIVE';

alter table conversations
    drop constraint if exists conversations_end_reason_check;

alter table conversations
    add constraint conversations_end_reason_check
    check (end_reason in ('USER_ENDED', 'NPC_ENDED', 'QUOTA_EXHAUSTED', 'RUDE', 'EXPIRED', 'TIME_LIMIT', 'TURN_LIMIT'));
