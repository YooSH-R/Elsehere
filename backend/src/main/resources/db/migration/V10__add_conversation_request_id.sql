-- 대화 시작(CONV-01) 요청 ID. 같은 요청을 다시 보내면 새 대화를 만들지 않고 같은 대화를 돌려준다. 이전 대화는 비워 둔다.
ALTER TABLE conversations ADD COLUMN request_id uuid;
ALTER TABLE conversations ADD CONSTRAINT uk_conversations_request_id UNIQUE (request_id);
