alter table conversations add column review_card_id bigint;
create index idx_conversations_review_card_status on conversations (review_card_id, status);
