alter table conversations
    add column time_limit_final_request_id uuid;

alter table conversations
    drop constraint if exists conversations_end_reason_check;

alter table conversations
    add constraint conversations_end_reason_check
    check (end_reason in ('USER_ENDED', 'NPC_ENDED', 'QUOTA_EXHAUSTED', 'RUDE', 'EXPIRED', 'TIME_LIMIT'));
